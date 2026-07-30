using Microsoft.Extensions.DependencyInjection;

namespace AAEmu.Commons.Network.Cluster;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers a cluster packet handler and its descriptor for DI-based hosts (e.g. AAEmu.Zone).
    /// </summary>
    public static IServiceCollection AddClusterPacket<TPacket, TPacketHandler>(this IServiceCollection services)
        where TPacket : ClusterPacket, IClusterPacket, new()
        where TPacketHandler : class, IClusterPacketHandler<TPacket>
    {
        services.AddSingleton<IClusterPacketHandler<TPacket>, TPacketHandler>();
        services.AddSingleton<IClusterPacketDescriptor>(sp =>
        {
            var handler = sp.GetRequiredService<IClusterPacketHandler<TPacket>>();
            return new ClusterPacketDescriptor<TPacket>(TPacket.TypeId, handler);
        });

        return services;
    }
}
