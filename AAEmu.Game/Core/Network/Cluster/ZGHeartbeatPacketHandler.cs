using AAEmu.Commons.Network.Cluster;
using AAEmu.Commons.Network.Cluster.Packets;
using NLog;

namespace AAEmu.Game.Core.Network.Cluster;

/// <summary>Gateway-side handler: logs the heartbeat and acks with server time.</summary>
public class ZGHeartbeatPacketHandler : IClusterPacketHandler<ZGHeartbeatPacket>
{
    private static Logger Logger { get; } = LogManager.GetCurrentClassLogger();

    public void Execute(ZGHeartbeatPacket packet, ClusterConnection connection)
    {
        Logger.Debug("Heartbeat from zone {0} (load {1})", packet.ZoneId, packet.Load);
        connection.SendPacket(new GZHeartbeatAckPacket(DateTime.UtcNow.Ticks));
    }
}
