﻿using System.Diagnostics;
using System.Net;
using System.Text.Json;
using QRCoder;
using EasyWire.Models;

namespace EasyWire.Services;

public class WireguardService
{
    private readonly ILogger<WireguardService> _logger;
    private readonly WireGuardConfig _config;
    private bool _isInitialized = false;

    public WireguardService(ILogger<WireguardService> logger, WireGuardConfig config)
    {
        _logger = logger;
        _config = config;
    }

    public async Task InitializeAsync()
    {
        if (string.IsNullOrEmpty(_config.WgHost))
        {
            throw new Exception("WG_HOST Environment Variable Not Set!");
        }

        _logger.LogDebug("Loading configuration...");
        try
        {
            var configJson = await File.ReadAllTextAsync(Path.Combine(_config.WgPath, "wg0.json"));
            _config.ParsedConfig = JsonSerializer.Deserialize<ParsedConfig>(configJson);
            _logger.LogDebug("Configuration loaded.");
        }
        catch (Exception)
        {
            var privateKey = await ExecuteCommandAsync("wg", "genkey");
            var publicKey = await ExecuteCommandAsync("wg", "pubkey", privateKey);
            var address = _config.WgDefaultAddress.Replace("x", "1");

            _config.ParsedConfig = new ParsedConfig
            {
                Server = new ServerConfig
                {
                    PrivateKey = privateKey,
                    PublicKey = publicKey,
                    Address = address
                },
                Clients = new Dictionary<string, ClientConfig>()
            };
            _logger.LogDebug("Configuration generated.");
        }

        await SaveConfigAsync();
        await ExecuteCommandAsync("wg-quick", "down wg0").ContinueWith(_ => { });
        try
        {
            await ExecuteCommandAsync("wg-quick", "up wg0");
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

    public async Task<List<ClientInfo>> GetClientsAsync()
    {
        if (!_isInitialized)
        {
            await InitializeAsync();
        }

        if (_config.ParsedConfig?.Clients == null)
        {
            return new List<ClientInfo>();
        }

        var clients = _config.ParsedConfig.Clients.Select(kvp => new ClientInfo
        {
            Id = kvp.Key,
            Name = kvp.Value.Name,
            Enabled = kvp.Value.Enabled,
            Address = kvp.Value.Address,
            PublicKey = kvp.Value.PublicKey,
            CreatedAt = kvp.Value.CreatedAt,
            UpdatedAt = kvp.Value.UpdatedAt,
            AllowedIPs = kvp.Value.AllowedIPs,
            DownloadableConfig = kvp.Value.PrivateKey != null,
            PersistentKeepalive = null,
            LatestHandshakeAt = null,
            TransferRx = null,
            TransferTx = null
        }).ToList();

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
            _logger.LogError(ex, "Error while getting WireGuard status");
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
PrivateKey = {_config.ParsedConfig.Server.PrivateKey}
Address = {_config.ParsedConfig.Server.Address}/24
ListenPort = {_config.WgPort}
PreUp = {_config.WgPreUp}
PostUp = {_config.WgPostUp}
PreDown = {_config.WgPreDown}
PostDown = {_config.WgPostDown}
";

        foreach (var (clientId, client) in _config.ParsedConfig.Clients)
        {
            if (!client.Enabled) continue;

            configContent += $@"

# Client: {client.Name} ({clientId})
[Peer]
PublicKey = {client.PublicKey}
{(client.PreSharedKey != null ? $"PresharedKey = {client.PreSharedKey}\n" : "")}AllowedIPs = {client.Address}/32";
        }

        _logger.LogDebug("Config saving...");
        await File.WriteAllTextAsync(Path.Combine(_config.WgPath, "wg0.json"), JsonSerializer.Serialize(_config.ParsedConfig, new JsonSerializerOptions { WriteIndented = true }));
        await File.WriteAllTextAsync(Path.Combine(_config.WgPath, "wg0.conf"), configContent);
        _logger.LogDebug("Config saved.");
    }

    public async Task SyncConfigAsync()
    {
        _logger.LogDebug("Config syncing...");
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
        _logger.LogDebug("Config synced.");
    }


    public async Task<ClientConfig> GetClientAsync(string clientId)
    {
        if (!_config.ParsedConfig.Clients.TryGetValue(clientId, out var client))
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
PrivateKey = {(client.PrivateKey != null ? client.PrivateKey : "REPLACE_ME")}
Address = {client.Address}/24
{(_config.WgDefaultDns != null ? $"DNS = {_config.WgDefaultDns}\n" : "")}
{(_config.WgMtu != null ? $"MTU = {_config.WgMtu}\n" : "")}

[Peer]
PublicKey = {_config.ParsedConfig.Server.PublicKey}
{(client.PreSharedKey != null ? $"PresharedKey = {client.PreSharedKey}\n" : "")}
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
            .FirstOrDefault(ip => !_config.ParsedConfig.Clients.Values.Any(c => c.Address == ip));

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
            Enabled = true
        };

        _config.ParsedConfig.Clients[id] = client;

        await SaveConfigAsync();
        await SyncConfigAsync();

        return client;
    }

    public async Task DeleteClientAsync(string clientId)
    {
        if (_config.ParsedConfig.Clients.Remove(clientId))
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

    public async Task RestoreConfigurationAsync(string configJson)
    {
        _logger.LogDebug("Starting configuration restore process.");
        _config.ParsedConfig = JsonSerializer.Deserialize<ParsedConfig>(configJson);
        await SaveConfigAsync();
        await SyncConfigAsync();
        _logger.LogDebug("Configuration restore process completed.");
    }

    public async Task<string> BackupConfigurationAsync()
    {
        _logger.LogDebug("Starting configuration backup.");
        var backup = JsonSerializer.Serialize(_config.ParsedConfig, new JsonSerializerOptions { WriteIndented = true });
        _logger.LogDebug("Configuration backup completed.");
        return backup;
    }

    public async Task ShutdownAsync()
    {
        await ExecuteCommandAsync("wg-quick", "down wg0").ContinueWith(_ => { });
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