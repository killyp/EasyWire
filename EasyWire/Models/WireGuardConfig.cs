using System;

namespace EasyWire.Models;

public class WireGuardConfig
{
    // Server configuration
    public string ServerPrivateKey { get; set; }
    public string ServerPublicKey { get; set; }
    public string ServerAddress { get; set; }

    // Client configurations
    public Dictionary<string, ClientConfig> Clients { get; set; }

    // General configuration settings
    public string WgPath { get; set; } = "/etc/wireguard";
    public string WgHost { get; set; } = Environment.GetEnvironmentVariable("WG_HOST") ?? "";
    public int WgPort { get; set; } = int.Parse(Environment.GetEnvironmentVariable("WG_PORT") ?? "51820");
    public string WgDefaultDns { get; set; } = Environment.GetEnvironmentVariable("WG_DNS") ?? "1.1.1.1";
    public string WgDefaultAddress { get; set; } = "192.168.2.x";
    public int WgPersistentKeepalive { get; set; } = 25;
    public string WgAllowedIps { get; set; } = "0.0.0.0/0, ::/0";
    public string WgPreUp { get; set; } = "echo WireGuard PreUp";
    public string WgPostUp { get; set; } = "echo WireGuard PostUp";
    public string WgPreDown { get; set; } = "echo WireGuard PreDown";
    public string WgPostDown { get; set; } = "echo WireGuard PostDown";
}