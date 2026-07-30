using AAEmu.Commons.Network.Cluster;
using AAEmu.Commons.Network.Cluster.Packets;
using AAEmu.Game.Models;
using NLog;

namespace AAEmu.Game.Core.Network.Cluster;

/// <summary>
/// Gateway-side handler: validates the zone secret key (same key as the GL game-server
/// register), stores the connection in the <see cref="ZoneRegistry"/> and replies.
/// </summary>
public class ZGRegisterZonePacketHandler : IClusterPacketHandler<ZGRegisterZonePacket>
{
    private static Logger Logger { get; } = LogManager.GetCurrentClassLogger();

    public void Execute(ZGRegisterZonePacket packet, ClusterConnection connection)
    {
        if (packet.SecretKey != AppConfiguration.Instance.SecretKey)
        {
            Logger.Error("Zone register from {0} rejected: bad secret key", connection.Ip);
            connection.SendPacket(new GZRegisterResultPacket(false, "bad secret key"));
            return;
        }

        connection.AddAttribute("zoneId", packet.ZoneId);
        ZoneRegistry.Instance.Add(packet.ZoneId, connection);
        Logger.Info("Zone {0} registered (worldTemplate {1}) from {2}",
            packet.ZoneId, packet.WorldTemplateId, connection.Ip);
        connection.SendPacket(new GZRegisterResultPacket(true, $"zone {packet.ZoneId} registered"));

        // B2.4a self-test: prove the client-packet tunnel end-to-end once a zone is up.
        // Sends a synthetic client frame (level-2, op 0x0001) for a fake connection to the zone,
        // which logs it and echoes a frame back (see ZGClientPacketHandler).
        if (AppConfiguration.Instance.ClusterNetwork?.TunnelSelfTest == true)
        {
            var frame = ClientFrameCodec.BuildClientFrame(0x0001, level: 2);
            var sent = ZoneRegistry.Instance.ForwardClientPacket(packet.ZoneId, 999, frame);
            Logger.Info("[B2.4a] gateway sent synthetic GZClientPacket conn=999 op=0x0001 to zone {0} (queued={1})",
                packet.ZoneId, sent);
        }
    }
}
