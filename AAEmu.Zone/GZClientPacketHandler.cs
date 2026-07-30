using System.Collections.Concurrent;
using AAEmu.Commons.Network.Cluster;
using AAEmu.Commons.Network.Cluster.Packets;
using AAEmu.Game.Core.Network.Connections;
using Microsoft.Extensions.Logging;

namespace AAEmu.Zone;

/// <summary>
/// Zone-side handler for a raw client frame tunneled in from the gateway. B2.4d: for each tunneled
/// connectionId it now holds a REAL <see cref="GameConnection"/> backed by a <see cref="TunnelSession"/>
/// (a socket-less <c>ISession</c>). To prove the whole stack is live it echoes a synthetic reply frame
/// through <c>GameConnection.SendRaw</c>, which flows GameConnection -&gt; ISession.SendPacket -&gt;
/// TunnelSession -&gt; ZGClientPacket -&gt; gateway.
/// It does NOT yet dispatch against the real GameProtocolHandler - that is B2.5.
/// </summary>
public class GZClientPacketHandler(ILogger logger) : IClusterPacketHandler<GZClientPacket>
{
    // One real GameConnection (over a TunnelSession) per tunneled client connectionId.
    private static readonly ConcurrentDictionary<uint, GameConnection> Clients = new();

    public void Execute(GZClientPacket packet, ClusterConnection connection)
    {
        var op = ClientFrameCodec.ParseOpcode(packet.Payload);
        var len = packet.Payload?.Length ?? 0;
        logger.LogInformation("[B2.4d] zone got client packet conn={ConnId} {Len} bytes op=0x{Op:x4}",
            packet.ConnectionId, len, op);

        // Build/reuse a genuine GameConnection whose ISession is a socket-less TunnelSession.
        var client = Clients.GetOrAdd(packet.ConnectionId,
            id => new GameConnection(new TunnelSession(id, connection)));

        // Round-trip proof through the REAL stack: GameConnection.SendRaw -> ISession.SendPacket ->
        // TunnelSession wraps a ZGClientPacket -> back to the gateway. (Dispatch is B2.5.)
        var reply = ClientFrameCodec.BuildClientFrame(0x0002, level: 2);
        client.SendRaw(reply);
        logger.LogInformation("[B2.4d] zone sent reply via GameConnection(TunnelSession) conn={ConnId} op=0x0002 ({Len} bytes)",
            packet.ConnectionId, reply.Length);
    }
}
