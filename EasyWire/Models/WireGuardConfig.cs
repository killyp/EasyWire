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
    public string WireguardPath { get; set; }
    public string Host { get; set; }
    public string PasswordHash { get; set; }
    public int ExternalVpnPort { get; set; }
    public int InternalVpnPort { get; set; }
    public string VpnDns { get; set; }
    public string VpnAddressSpace { get; set; }
    public int VpnKeepalive { get; set; }
    public string WgAllowedIps { get; set; }
}