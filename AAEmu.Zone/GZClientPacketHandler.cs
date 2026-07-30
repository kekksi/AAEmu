using System.Collections.Concurrent;
using AAEmu.Commons.Network.Cluster;
using AAEmu.Commons.Network.Cluster.Packets;
using Microsoft.Extensions.Logging;

namespace AAEmu.Zone;

/// <summary>
/// Zone-side handler for a raw client frame tunneled in from the gateway. B2.4a: it parses the
/// opcode, logs, and echoes a synthetic reply frame back through the tunnel to prove the round-trip.
/// It does NOT yet dispatch against the real GameProtocolHandler - that is B2.5.
/// </summary>
public class GZClientPacketHandler(ILogger logger) : IClusterPacketHandler<GZClientPacket>
{
    // One lightweight client stub per tunneled connectionId.
    private static readonly ConcurrentDictionary<uint, ZoneClientConnection> Clients = new();

    public void Execute(GZClientPacket packet, ClusterConnection connection)
    {
        var op = ClientFrameCodec.ParseOpcode(packet.Payload);
        var len = packet.Payload?.Length ?? 0;
        logger.LogInformation("[B2.4a] zone got client packet conn={ConnId} {Len} bytes op=0x{Op:x4}",
            packet.ConnectionId, len, op);

        var client = Clients.GetOrAdd(packet.ConnectionId, id => new ZoneClientConnection(id, connection));

        // Round-trip proof: echo a synthetic reply frame (level-2, op 0x0002) back to the gateway.
        var reply = ClientFrameCodec.BuildClientFrame(0x0002, level: 2);
        client.SendPacket(reply);
        logger.LogInformation("[B2.4a] zone sent synthetic reply conn={ConnId} op=0x0002 ({Len} bytes)",
            packet.ConnectionId, reply.Length);
    }
}
