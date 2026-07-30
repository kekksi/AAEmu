using System.Collections.Concurrent;
using AAEmu.Commons.Utils;

namespace AAEmu.Game.Core.Network.Cluster;

/// <summary>
/// B2.4b: gateway-side ownership map. Records which zone process currently "owns" a given client
/// connection (keyed by <see cref="Connections.GameConnection.Id"/>). While an owner is set, complete
/// client wire-frames for that connection are tunneled to the owning zone instead of being dispatched
/// locally on the gateway.
///
/// Default state is EMPTY: with no owners registered the gateway behaves exactly as the monolith did
/// (every frame takes the existing local dispatch path). Ownership is only established by the hand-off
/// path (B2.5/B2.6), so this table introduces zero behaviour change on its own.
/// </summary>
public class ClientRoutingTable : Singleton<ClientRoutingTable>
{
    private readonly ConcurrentDictionary<uint, uint> _owners = new();

    private ClientRoutingTable() { }

    /// <summary>Marks <paramref name="connectionId"/> as owned by <paramref name="zoneId"/>.</summary>
    public void SetOwner(uint connectionId, uint zoneId) => _owners[connectionId] = zoneId;

    /// <summary>Removes any ownership record for <paramref name="connectionId"/>.</summary>
    public void ClearOwner(uint connectionId) => _owners.TryRemove(connectionId, out _);

    /// <summary>True (and sets <paramref name="zoneId"/>) if the connection is currently owned by a zone.</summary>
    public bool TryGetOwner(uint connectionId, out uint zoneId) => _owners.TryGetValue(connectionId, out zoneId);

    public int Count => _owners.Count;
}
