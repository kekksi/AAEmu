using AAEmu.Commons.Network.Cluster;
using AAEmu.Commons.Network.Cluster.Packets;
using Microsoft.Extensions.Logging;

namespace AAEmu.Zone;

public class GZRegisterResultPacketHandler(ILogger logger) : IClusterPacketHandler<GZRegisterResultPacket>
{
    public void Execute(GZRegisterResultPacket packet, ClusterConnection connection)
    {
        if (packet.Success)
            logger.LogInformation("Zone register success: {Message}", packet.Message);
        else
            logger.LogError("Zone register failed: {Message}", packet.Message);
    }
}
