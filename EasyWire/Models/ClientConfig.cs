using System;

namespace EasyWire.Models;

public class ClientConfig
{
    public string Id { get; set; }
    public string Name { get; set; }
    public string Address { get; set; }
    public string PrivateKey { get; set; }
    public string PublicKey { get; set; }
    public string PreSharedKey { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public bool Enabled { get; set; }
    public bool DownloadableConfig { get; set; }
    public string PersistentKeepalive { get; set; }
    public DateTime? LatestHandshakeAt { get; set; }
    public long? TransferRx { get; set; }
    public long? TransferTx { get; set; }
}