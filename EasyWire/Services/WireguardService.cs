using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;
using EasyWire.Models;
using Microsoft.Extensions.Logging;
using QRCoder;

namespace EasyWire.Services
{
    public class WireguardService
    {
        private WireGuardConfig _config;
        private bool _isInitialized = false;

        public WireguardService()
        {
            _config = new WireGuardConfig();
            
            if (string.IsNullOrEmpty(_config.WgHost))
            {
                throw new Exception("WG_HOST Environment Variable Not Set!");
            }
        }

        public async Task InitializeAsync()
        {
            if (string.IsNullOrEmpty(_config.WgHost))
            {
                throw new Exception("WG_HOST Environment Variable Not Set!");
            }

            Console.WriteLine("Loading configuration...");
            try
            {
                var configJsonPath = await File.ReadAllTextAsync(Path.Combine(_config.WgPath, "wg0.json"));
                _config = JsonSerializer.Deserialize<WireGuardConfig>(configJsonPath);
                Console.WriteLine("Configuration loaded.");
            }
            catch (Exception)
            {
                _config.ServerPrivateKey = await ExecuteCommandAsync("wg", "genkey");
                _config.ServerPublicKey = await ExecuteCommandAsync("wg", "pubkey", _config.ServerPrivateKey);
                _config.ServerAddress = _config.WgDefaultAddress.Replace("x", "1");
                _config.Clients = new Dictionary<string, ClientConfig>();
                Console.WriteLine("Configuration generated.");
            }

            await SaveConfigAsync();
            await ExecuteCommandAsync("wg-quick", "down wg0").ContinueWith(_ => { });
            try
            {
                await ExecuteCommandAsync("wg-quick", "up wg0");
                await ConfigureIptables(true);
            }
            catch (Exception ex)
            {
                if (ex.Message.Contains("Cannot find device \"wg0\""))
                {
                    throw new Exception("WireGuard exited with the error: Cannot find device \"wg0\"\nThis usually means that your host's kernel does not support WireGuard!");
                }
                throw;
            }
            await SyncConfigAsync();
            _isInitialized = true;
        }
        
        private async Task ConfigureIptables(bool isSettingUp)
        {
            string device = "eth0";
            if (isSettingUp)
            {
                await ExecuteCommandAsync("iptables", $"-t nat -A POSTROUTING -s {_config.ServerAddress}/24 -o {device} -j MASQUERADE");
                await ExecuteCommandAsync("iptables", $"-A INPUT -p udp -m udp --dport {_config.WgPort} -j ACCEPT");
                await ExecuteCommandAsync("iptables", $"-A FORWARD -i wg0 -j ACCEPT");
                await ExecuteCommandAsync("iptables", $"-A FORWARD -o wg0 -j ACCEPT");
            }
            else
            {
                await ExecuteCommandAsync("iptables", $"-t nat -D POSTROUTING -s {_config.ServerAddress}/24 -o {device} -j MASQUERADE");
                await ExecuteCommandAsync("iptables", $"-D INPUT -p udp -m udp --dport {_config.WgPort} -j ACCEPT");
                await ExecuteCommandAsync("iptables", $"-D FORWARD -i wg0 -j ACCEPT");
                await ExecuteCommandAsync("iptables", $"-D FORWARD -o wg0 -j ACCEPT");
            }
        }

        public async Task<List<ClientConfig>> GetClientsAsync()
        {
            if (!_isInitialized)
            {
                await InitializeAsync();
            }

            if (_config.Clients == null)
            {
                return new List<ClientConfig>();
            }

            var clients = _config.Clients.Values.ToList();

            try
            {
                var dump = await ExecuteCommandAsync("wg", "show wg0 dump");
                var lines = dump.Trim().Split('\n').Skip(1);
                foreach (var line in lines)
                {
                    var parts = line.Split('\t');
                    var publicKey = parts[0];
                    var client = clients.Find(c => c.PublicKey == publicKey);
                    if (client == null) continue;

                    client.LatestHandshakeAt = parts[4] == "0" ? null : DateTimeOffset.FromUnixTimeSeconds(long.Parse(parts[4])).DateTime;
                    client.TransferRx = long.Parse(parts[5]);
                    client.TransferTx = long.Parse(parts[6]);
                    client.PersistentKeepalive = parts[7];
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error while getting WireGuard status - {ex}");
            }

            return clients;
        }

        public async Task SaveConfigAsync()
        {
            var configContent = $@"
# Note: Do not edit this file directly.
# Your changes will be overwritten!

# Server
[Interface]
PrivateKey = {_config.ServerPrivateKey}
Address = {_config.ServerAddress}/24
ListenPort = {_config.WgPort}
PreUp = {_config.WgPreUp}
PostUp = {_config.WgPostUp}
PreDown = {_config.WgPreDown}
PostDown = {_config.WgPostDown}
";

            Console.WriteLine(configContent);
            foreach (var (clientId, client) in _config.Clients)
            {
                if (!client.Enabled) continue;

                configContent += $@"

# Client: {client.Name} ({clientId})
[Peer]
PublicKey = {client.PublicKey}
{(client.PreSharedKey != null ? $"PresharedKey = {client.PreSharedKey}\n" : "")}AllowedIPs = {client.Address}/32";
            }

            Console.WriteLine("Config saving...");
            await File.WriteAllTextAsync(Path.Combine(_config.WgPath, "wg0.json"), JsonSerializer.Serialize(_config, new JsonSerializerOptions { WriteIndented = true }));
            await File.WriteAllTextAsync(Path.Combine(_config.WgPath, "wg0.conf"), configContent);
            Console.WriteLine("Config saved.");
        }

        public async Task SyncConfigAsync()
        {
            Console.WriteLine("Config syncing...");
            var tempConfigFile = Path.Combine(Path.GetTempPath(), "wg0.sync.conf");
            try
            {
                var configContent = await ExecuteCommandAsync("wg-quick", "strip wg0");
                await File.WriteAllTextAsync(tempConfigFile, configContent);
                await ExecuteCommandAsync("wg", $"syncconf wg0 {tempConfigFile}");
            }
            finally
            {
                if (File.Exists(tempConfigFile))
                {
                    File.Delete(tempConfigFile);
                }
            }
            Console.WriteLine("Config synced.");
        }

        public async Task<ClientConfig> GetClientAsync(string clientId)
        {
            if (!_config.Clients.TryGetValue(clientId, out var client))
            {
                throw new Exception($"Client Not Found: {clientId}");
            }
            return client;
        }

        public async Task<string> GetClientConfigurationAsync(string clientId)
        {
            var client = await GetClientAsync(clientId);

            return $@"
[Interface]
PrivateKey = {client.PrivateKey}
Address = {client.Address}/24
{(_config.WgDefaultDns != null ? $"DNS = {_config.WgDefaultDns}\n" : "")}

[Peer]
PublicKey = {_config.ServerPublicKey}
{(client.PreSharedKey != null ? $"PresharedKey = {client.PreSharedKey}" : "")}
AllowedIPs = {_config.WgAllowedIps}
PersistentKeepalive = {_config.WgPersistentKeepalive}
Endpoint = {_config.WgHost}:{_config.WgPort}";
        }

        public async Task<string> GetClientQRCodeSvgAsync(string clientId)
        {
            var config = await GetClientConfigurationAsync(clientId);
            var qrGenerator = new QRCodeGenerator();
            var qrCodeData = qrGenerator.CreateQrCode(config, QRCodeGenerator.ECCLevel.Q);
            var qrCode = new SvgQRCode(qrCodeData);
            return qrCode.GetGraphic(20);
        }

        public async Task<ClientConfig> CreateClientAsync(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                throw new Exception("Missing: Name");
            }

            var privateKey = await ExecuteCommandAsync("wg", "genkey");
            var publicKey = await ExecuteCommandAsync("wg", "pubkey", privateKey);
            var preSharedKey = await ExecuteCommandAsync("wg", "genpsk");

            var address = Enumerable.Range(2, 253)
                .Select(i => _config.WgDefaultAddress.Replace("x", i.ToString()))
                .FirstOrDefault(ip => !_config.Clients.Values.Any(c => c.Address == ip));

            if (address == null)
            {
                throw new Exception("Maximum number of clients reached.");
            }

            var id = Guid.NewGuid().ToString();
            var client = new ClientConfig
            {
                Id = id,
                Name = name,
                Address = address,
                PrivateKey = privateKey,
                PublicKey = publicKey,
                PreSharedKey = preSharedKey,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                Enabled = true,
                DownloadableConfig = true
            };

            _config.Clients[id] = client;

            await SaveConfigAsync();
            await SyncConfigAsync();

            return client;
        }

        public async Task DeleteClientAsync(string clientId)
        {
            if (_config.Clients.Remove(clientId))
            {
                await SaveConfigAsync();
                await SyncConfigAsync();
            }
        }

        public async Task EnableClientAsync(string clientId)
        {
            var client = await GetClientAsync(clientId);
            client.Enabled = true;
            client.UpdatedAt = DateTime.UtcNow;
            await SaveConfigAsync();
            await SyncConfigAsync();
        }

        public async Task DisableClientAsync(string clientId)
        {
            var client = await GetClientAsync(clientId);
            client.Enabled = false;
            client.UpdatedAt = DateTime.UtcNow;
            await SaveConfigAsync();
            await SyncConfigAsync();
        }

        public async Task UpdateClientNameAsync(string clientId, string name)
        {
            var client = await GetClientAsync(clientId);
            client.Name = name;
            client.UpdatedAt = DateTime.UtcNow;
            await SaveConfigAsync();
            await SyncConfigAsync();
        }

        public async Task UpdateClientAddressAsync(string clientId, string address)
        {
            var client = await GetClientAsync(clientId);
            if (!IPAddress.TryParse(address, out var ipAddress) || ipAddress.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork)
            {
                throw new Exception($"Invalid Address: {address}");
            }
            client.Address = address;
            client.UpdatedAt = DateTime.UtcNow;
            await SaveConfigAsync();
            await SyncConfigAsync();
        }

        private async Task<string> ExecuteCommandAsync(string command, string arguments, string input = null)
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = command,
                    Arguments = arguments,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    RedirectStandardInput = input != null,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                }
            };

            process.Start();

            if (input != null)
            {
                await process.StandardInput.WriteLineAsync(input);
                await process.StandardInput.FlushAsync();
                process.StandardInput.Close();
            }

            var output = await process.StandardOutput.ReadToEndAsync();
            var error = await process.StandardError.ReadToEndAsync();

            await process.WaitForExitAsync();

            if (process.ExitCode != 0)
            {
                throw new Exception($"Command '{command} {arguments}' failed with exit code {process.ExitCode}. Error: {error}");
            }
            return output.Trim();
        }
    }
}