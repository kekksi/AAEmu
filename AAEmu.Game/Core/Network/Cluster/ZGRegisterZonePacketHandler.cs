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

    // Synthetic connection id used only by the tunnel self-test (no live client socket).
    private const uint SelfTestConnId = 999;

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

        // B2.5 self-test: prove the client-packet tunnel dispatches against the REAL zone handler.
        // We send a genuine level-2 Ping frame (op 0x0012) with a 20-byte body (tPhy:i64, ping:i64,
        // local:u32). In the zone GZClientPacketHandler routes it into the real GameProtocolHandler,
        // the real PingPacket handler runs and replies with a Pong (op 0x0013) which tunnels back
        // here as a ZGClientPacket - the 0x0012 -> 0x0013 opcode change is proof the real handler
        // ran (a plain echo could never turn a Ping into a Pong).
        if (AppConfiguration.Instance.ClusterNetwork?.TunnelSelfTest == true)
        {
            // B2.5 handoff hook: register ownership of the (synthetic) test connection with the zone
            // that just came up. This exercises ClientRoutingTable.SetOwner on the gateway. The real
            // EnterWorld-driven ownership hand-off is B2.6a.
            ClientRoutingTable.Instance.SetOwner(SelfTestConnId, packet.ZoneId);
            Logger.Info("[B2.5] gateway SetOwner conn={0} -> zone {1} (owners now={2})",
                SelfTestConnId, packet.ZoneId, ClientRoutingTable.Instance.Count);

            var pingBody = new byte[20]; // tPhy(8) + ping(8) + local(4), zero-filled is valid
            var frame = ClientFrameCodec.BuildClientFrame(0x0012, level: 2, body: pingBody);
            var sent = ZoneRegistry.Instance.ForwardClientPacket(packet.ZoneId, SelfTestConnId, frame);
            Logger.Info("[B2.5] gateway sent Ping GZClientPacket conn={0} op=0x0012 to zone {1} (queued={2})",
                SelfTestConnId, packet.ZoneId, sent);
        }
    }
}
