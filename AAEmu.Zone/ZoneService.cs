using Microsoft.Extensions.Hosting;

namespace AAEmu.Zone;

public class ZoneService(ZoneClusterClient client) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        client.Start();
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        client.Stop();
        return Task.CompletedTask;
    }
}
