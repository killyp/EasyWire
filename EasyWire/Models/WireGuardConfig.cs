﻿using System;

namespace EasyWire.Models;

public class WireGuardConfig
{
    public string WgPath { get; set; } = "/etc/wireguard";
    public string WgHost { get; set; } = Environment.GetEnvironmentVariable("WG_HOST") ?? "";
    public int WgPort { get; set; } = int.Parse(Environment.GetEnvironmentVariable("WG_PORT") ?? "51820");
    public string WgDefaultDns { get; set; } = Environment.GetEnvironmentVariable("WG_DNS") ?? "1.1.1.1";
    public int? WgMtu { get; set; } = 1420;
    public string WgDefaultAddress { get; set; } = "10.0.0.x";
    public int WgPersistentKeepalive { get; set; } = 25;
    public string WgAllowedIps { get; set; } = "0.0.0.0/0, ::/0";
    public string WgPreUp { get; set; } = "echo WireGuard PreUp";
    public string WgPostUp { get; set; } = "echo WireGuard PostUp";
    public string WgPreDown { get; set; } = "echo WireGuard PreDown";
    public string WgPostDown { get; set; } = "echo WireGuard PostDown";

    public ParsedConfig ParsedConfig { get; set; }
}