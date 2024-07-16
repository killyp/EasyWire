using EasyWire.Components;
using EasyWire.Models;
using EasyWire.Services;
using MudBlazor.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Add MudBlazor service
builder.Services.AddMudServices();

// Configure WireGuardConfig
builder.Services.AddSingleton<WireGuardConfig>(sp =>
{
    var config = new WireGuardConfig
    {
        // Required
        WgHost = Environment.GetEnvironmentVariable("WG_HOST"),
        WgPort = int.TryParse(Environment.GetEnvironmentVariable("WG_PORT"), out var port) ? port : 51820,
        //EwPort = int.TryParse(Environment.GetEnvironmentVariable("EW_PORT"), out var ewPort) ? ewPort : 80,
        
        // Defaults
        WgPath = Environment.GetEnvironmentVariable("WG_PATH") ?? "/etc/wireguard",
        WgMtu = int.TryParse(Environment.GetEnvironmentVariable("WG_MTU"), out var mtu) ? (int?)mtu : null,
        WgDefaultDns = Environment.GetEnvironmentVariable("WG_DEFAULT_DNS") ?? "1.1.1.1",
        WgDefaultAddress = Environment.GetEnvironmentVariable("WG_DEFAULT_ADDRESS") ?? "10.8.0.x",
        WgPersistentKeepalive = int.TryParse(Environment.GetEnvironmentVariable("WG_PERSISTENT_KEEPALIVE"), out var keepalive) ? keepalive : 25,
        WgAllowedIps = Environment.GetEnvironmentVariable("WG_ALLOWED_IPS") ?? "0.0.0.0/0, ::/0",
        WgPreUp = Environment.GetEnvironmentVariable("WG_PRE_UP") ?? "echo WireGuard PreUp",
        WgPostUp = Environment.GetEnvironmentVariable("WG_POST_UP") ?? "echo WireGuard PostUp",
        WgPreDown = Environment.GetEnvironmentVariable("WG_PRE_DOWN") ?? "echo WireGuard PreDown",
        WgPostDown = Environment.GetEnvironmentVariable("WG_POST_DOWN") ?? "echo WireGuard PostDown"
    };
    
    if (string.IsNullOrEmpty(config.WgHost))
    {
        throw new InvalidOperationException("WG_HOST is required.");
    }
    return config;
});

// Register Wireguard service
builder.Services.AddSingleton<WireguardService>();

var app = builder.Build();

// Initialize Wireguard service
using (var scope = app.Services.CreateScope())
{
    var wireguardService = scope.ServiceProvider.GetRequiredService<WireguardService>();
    await wireguardService.InitializeAsync();
}


// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseStaticFiles();
app.UseAntiforgery();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();