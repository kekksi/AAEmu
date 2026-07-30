using AAEmu.Commons.Network.Cluster;
using AAEmu.Commons.Network.Cluster.Packets;
using AAEmu.Game.Core.Network.Connections;
using NLog;

namespace AAEmu.Game.Core.Network.Cluster;

/// <summary>
/// Gateway-side handler for a client frame coming back FROM a zone. B2.4a: it looks up the owning
/// client session and (for now) logs. Writing the raw payload out to a live client socket is B2.5.
/// </summary>
public class ZGClientPacketHandler : IClusterPacketHandler<ZGClientPacket>
{
    private static Logger Logger { get; } = LogManager.GetCurrentClassLogger();

    public void Execute(ZGClientPacket packet, ClusterConnection connection)
    {
        var op = ClientFrameCodec.ParseOpcode(packet.Payload);
        var len = packet.Payload?.Length ?? 0;
        Logger.Info("[B2.4a] gateway got ZGClientPacket conn={0} {1} bytes op=0x{2:x4}",
            packet.ConnectionId, len, op);

        // Locate the real client session and write the raw frame back onto its socket.
        // Absent for the synthetic self-test conn (no live client) - then we only log, as before.
        var gc = GameConnectionTable.Instance.GetConnection(packet.ConnectionId);
        if (gc == null)
        {
            Logger.Info("[B2.4c] no live GameConnection for conn={0} (expected in synthetic test)",
                packet.ConnectionId);
            return;
        }

        // B2.4c: outbound tunnel write. Payload is already a complete client wire-frame.
        gc.SendRaw(packet.Payload);
        Logger.Info("[B2.4c] gateway wrote {0} raw bytes back to client conn={1}",
            len, packet.ConnectionId);
    }
}
