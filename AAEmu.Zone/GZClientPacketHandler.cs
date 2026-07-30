using System.Collections.Concurrent;
using System.Reflection;
using AAEmu.Commons.Network.Cluster;
using AAEmu.Commons.Network.Cluster.Packets;
using AAEmu.Game.Core.Network.Connections;
using AAEmu.Game.Core.Network.Game;
using Microsoft.Extensions.Logging;

namespace AAEmu.Zone;

/// <summary>
/// Zone-side handler for a raw client frame tunneled in from the gateway.
///
/// B2.5: instead of the old synthetic echo it now dispatches the frame against the REAL
/// <see cref="GameProtocolHandler"/> running IN the zone process. For each tunneled connectionId it
/// holds a genuine <see cref="GameConnection"/> backed by a socket-less <see cref="TunnelSession"/>,
/// then calls <c>GameProtocolHandler.OnReceive(gameConnection, payload, 0, len)</c>. The concrete
/// C2G packet handler (e.g. PingPacket) runs exactly as it would in the monolith; any reply it emits
/// via <c>Connection.SendPacket</c> flows GameConnection -&gt; TunnelSession -&gt; ZGClientPacket back
/// to the gateway automatically.
///
/// The zone's GameProtocolHandler is obtained from <see cref="GameNetwork"/> (which builds the full
/// C2G/proxy packet registry in its private ctor) WITHOUT ever calling <c>GameNetwork.Start()</c>, so
/// no client-facing socket is opened here. The registry (<c>_handler</c>) is read once via reflection.
/// </summary>
public class GZClientPacketHandler(ILogger logger) : IClusterPacketHandler<GZClientPacket>
{
    // One real GameConnection (over a TunnelSession) per tunneled client connectionId.
    private static readonly ConcurrentDictionary<uint, GameConnection> Clients = new();

    // Lazily grab the zone-local GameProtocolHandler, fully populated with the C2G + proxy packet
    // registry. GameNetwork's private ctor runs all RegisterPacket(...) calls but opens NO socket
    // (that only happens in Start(), which we never call). We read the private _handler field once.
    private static readonly Lazy<GameProtocolHandler> ZoneHandler = new(() =>
    {
        var gameNetwork = GameNetwork.Instance;
        var handlerField = typeof(GameNetwork).GetField("_handler",
            BindingFlags.NonPublic | BindingFlags.Instance);
        return (GameProtocolHandler)handlerField!.GetValue(gameNetwork)!;
    });

    public void Execute(GZClientPacket packet, ClusterConnection connection)
    {
        var op = ClientFrameCodec.ParseOpcode(packet.Payload);
        var payload = packet.Payload ?? [];
        var len = payload.Length;
        logger.LogInformation("[B2.5] zone got client frame conn={ConnId} {Len} bytes op=0x{Op:x4}",
            packet.ConnectionId, len, op);

        // Build/reuse a genuine GameConnection whose ISession is a socket-less TunnelSession.
        var client = Clients.GetOrAdd(packet.ConnectionId,
            id => new GameConnection(new TunnelSession(id, connection)));

        // REAL dispatch: run the frame through the zone-local GameProtocolHandler. The matching
        // C2G handler executes in this process; replies flow back via GameConnection.SendPacket ->
        // TunnelSession -> ZGClientPacket -> gateway (no synthetic echo anymore).
        logger.LogInformation("[B2.5] zone dispatching conn={ConnId} op=0x{Op:x4} to REAL GameProtocolHandler",
            packet.ConnectionId, op);
        ZoneHandler.Value.OnReceive(client, payload, 0, len);
        logger.LogInformation("[B2.5] zone real dispatch complete conn={ConnId} op=0x{Op:x4}",
            packet.ConnectionId, op);
    }

    /// <summary>
    /// B2.5 handoff placeholder: called from <see cref="TunnelSession.Close"/> when the tunnel for a
    /// connection goes away. Drops the cached zone-local GameConnection so a later reconnect starts
    /// fresh. Cross-process gateway ownership-clear signaling is B2.6.
    /// </summary>
    public static void OnTunnelClosed(uint connectionId)
    {
        Clients.TryRemove(connectionId, out _);
    }
}
