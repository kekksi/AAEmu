using System.Collections.Concurrent;
using AAEmu.Commons.Network.Cluster;
using AAEmu.Commons.Utils;

namespace AAEmu.Game.Core.Network.Cluster;

/// <summary>
/// Tracks zone processes currently registered with this gateway. Keyed by zoneId.
/// </summary>
public class ZoneRegistry : Singleton<ZoneRegistry>
{
    private readonly ConcurrentDictionary<uint, ClusterConnection> _zones = new();

    private ZoneRegistry() { }

    public void Add(uint zoneId, ClusterConnection connection) => _zones[zoneId] = connection;

    public ClusterConnection Get(uint zoneId) => _zones.GetValueOrDefault(zoneId);

    public bool Remove(uint zoneId) => _zones.TryRemove(zoneId, out _);

    public void RemoveByConnection(ClusterConnection connection)
    {
        foreach (var kv in _zones)
            if (kv.Value.Id == connection.Id)
                _zones.TryRemove(kv.Key, out _);
    }

    public int Count => _zones.Count;
}
