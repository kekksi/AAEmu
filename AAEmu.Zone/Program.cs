using AAEmu.Zone;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

var builder = Host.CreateApplicationBuilder(args);

builder.Configuration.AddJsonFile("Config.json", optional: false, reloadOnChange: false);

builder.Services.Configure<ZoneConfig>(builder.Configuration.GetSection("Zone"));
builder.Services.AddSingleton<ZoneClusterClient>();
builder.Services.AddHostedService<ZoneService>();

builder.Logging.ClearProviders();
builder.Logging.SetMinimumLevel(LogLevel.Debug);
builder.Logging.AddSimpleConsole(o =>
{
    o.SingleLine = true;
    o.TimestampFormat = "HH:mm:ss ";
});

// --- B2.2: build zone-local DI subset + create one WorldInstance (main_world) ---
// Runs once at startup, before the B2.1 gateway heartbeat host. On failure we log
// and continue so the B2.1 behaviour (cluster register + heartbeat) is preserved.
try
{
    ZoneWorldBootstrap.Run();
}
catch (Exception ex)
{
    NLog.LogManager.GetCurrentClassLogger().Error(ex, "[B2.2] Zone world bootstrap FAILED");
}

var host = builder.Build();
await host.RunAsync();
