using EasyWire.Components;
using EasyWire.Services;
using Microsoft.AspNetCore.DataProtection;
using MudBlazor.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Configure Data Protection
builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo("/app/keys"))
    .SetApplicationName("EasyWire");

// Add MudBlazor service
builder.Services.AddMudServices();

// Register Wireguard service
builder.Services.AddSingleton<WireguardService>();

// Build the app builder
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

app.UseStaticFiles();
app.UseRouting();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode().DisableAntiforgery();

app.Run();