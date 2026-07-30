using AAEmu.Game.Core.Network.Connections;

using NLog;

namespace AAEmu.Game.Core.Network.Cluster;

/// <summary>
/// B2.6a: EnterWorld-triggered zone ownership hand-off (gateway side).
///
/// When a client actually enters the game world with a chosen character (end of the
/// CSSelectCharacter handler, after the character has been loaded and bound to its world
/// instance), the gateway hands the connection over to the owning zone process by recording an
/// entry in the <see cref="ClientRoutingTable"/>. From that point on the inbound routing weiche in
/// <c>GameProtocolHandler.OnReceive</c> tunnels every subsequent client frame to that zone instead
/// of dispatching it locally.
///
/// ROBUSTNESS / FALLBACK: the hand-off only happens if the target zone process is actually
/// registered in the <see cref="ZoneRegistry"/>. With no zone connected the routing table stays
/// empty and the gateway behaves EXACTLY like the monolith did (all frames dispatch locally). This
/// is what guarantees B2.6a introduces zero behaviour change for the stand-alone monolith.
/// </summary>
public static class ClientOwnershipHandoff
{
    private static Logger Logger { get; } = LogManager.GetCurrentClassLogger();

    /// <summary>
    /// PoC single-zone mapping: every character enters the herausgeloeste zone (main_world), which
    /// registers itself with cluster zoneId 1. Once more than one zone is split out, this resolves
    /// the target from the characters world/position instead.
    /// </summary>
    public const uint MainWorldZoneId = 1;

    /// <summary>
    /// Resolves which cluster zone should own this connection. PoC: always main_world (zone 1),
    /// matching CSSelectCharacter forcing the character into WorldManager.DefaultInstanceId.
    /// </summary>
    public static uint ResolveZoneId(GameConnection connection) => MainWorldZoneId;

    /// <summary>
    /// Attempts to hand this connection off to its target zone. Returns true if ownership was
    /// established (frames will now tunnel), false if no matching zone is registered (connection
    /// stays on the gateway = monolith behaviour).
    /// </summary>
    public static bool TryClaim(GameConnection connection)
    {
        if (connection == null)
            return false;

        var targetZoneId = ResolveZoneId(connection);

        // Only hand off to a zone that is really connected. Otherwise leave the connection on the
        // gateway - the monolith path is completely unchanged.
        if (ZoneRegistry.Instance.Get(targetZoneId) == null)
        {
            Logger.Trace("[B2.6a] no zone {0} registered - conn {1} stays on gateway (monolith path)",
                targetZoneId, connection.Id);
            return false;
        }

        ClientRoutingTable.Instance.SetOwner(connection.Id, targetZoneId);
        Logger.Info("[B2.6a] enterworld ownership: conn={0} char={1} -> zone {2} (owners now={3})",
            connection.Id, connection.ActiveChar?.Name ?? "?", targetZoneId,
            ClientRoutingTable.Instance.Count);
        return true;
    }
}
