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

var host = builder.Build();
await host.RunAsync();
