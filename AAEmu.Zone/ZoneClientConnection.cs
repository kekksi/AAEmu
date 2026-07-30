using AAEmu.Commons.Network.Cluster;
using AAEmu.Commons.Network.Cluster.Packets;

namespace AAEmu.Zone;

/// <summary>
/// B2.4a stub for a client session as seen from inside a zone. It has NO real socket: it represents
/// a client that is physically connected to the gateway. Anything the zone wants to send to that
/// client is wrapped in a <see cref="ZGClientPacket"/> and pushed back over the cluster channel.
/// The real per-session game state (GameConnection) stays on the gateway until B2.5/B2.6.
/// </summary>
public class ZoneClientConnection
{
    public uint Id { get; }
    private readonly ClusterConnection _gateway;

    public ZoneClientConnection(uint id, ClusterConnection gateway)
    {
        Id = id;
        _gateway = gateway;
    }

    /// <summary>Sends a raw client wire-frame back to this client via the gateway.</summary>
    public void SendPacket(byte[] payload)
    {
        _gateway.SendPacket(new ZGClientPacket(Id, payload));
    }
}
