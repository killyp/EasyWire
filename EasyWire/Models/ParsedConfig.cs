using System.Collections.Generic;

namespace EasyWire.Models;

public class ParsedConfig
{
    public ServerConfig Server { get; set; }
    public Dictionary<string, ClientConfig> Clients { get; set; }
}