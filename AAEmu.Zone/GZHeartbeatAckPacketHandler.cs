using AAEmu.Commons.Network.Cluster;
using AAEmu.Commons.Network.Cluster.Packets;
using Microsoft.Extensions.Logging;

namespace AAEmu.Zone;

public class GZHeartbeatAckPacketHandler(ILogger logger) : IClusterPacketHandler<GZHeartbeatAckPacket>
{
    public void Execute(GZHeartbeatAckPacket packet, ClusterConnection connection)
    {
        logger.LogInformation("Heartbeat ack from gateway (serverTime {ServerTime})", packet.ServerTime);
    }
}
