namespace AAEmu.Commons.Network.Cluster.Packets;

/// <summary>
/// Gateway -&gt; Zone. B2.6b character hand-off. When a client selects a character whose target
/// world is a herausgeloeste (split-out) zone process, the gateway does NOT load/spawn the
/// character locally. Instead it sends this packet so the ZONE loads the character straight from
/// the shared DB and spawns it into its own <c>WorldInstance</c>. Carries just enough for the zone
/// to do the DB load itself: the tunneled client <see cref="ConnectionId"/> (so the zone can bind a
/// real GameConnection over its TunnelSession), the owning <see cref="AccountId"/> and the
/// <see cref="CharacterId"/> to load.
/// </summary>
public class GZEnterZonePacket : ClusterPacket, IClusterPacket
{
    public new static ushort TypeId => ClusterOffsets.GZEnterZone;

    public uint ConnectionId { get; private set; }
    public uint AccountId { get; private set; }
    public uint CharacterId { get; private set; }

    public GZEnterZonePacket() : base(TypeId) { }

    public GZEnterZonePacket(uint connectionId, uint accountId, uint characterId) : base(TypeId)
    {
        ConnectionId = connectionId;
        AccountId = accountId;
        CharacterId = characterId;
    }

    public override void Read(PacketStream stream)
    {
        ConnectionId = stream.ReadUInt32();
        AccountId = stream.ReadUInt32();
        CharacterId = stream.ReadUInt32();
    }

    public override PacketStream Write(PacketStream stream)
    {
        stream.Write(ConnectionId);
        stream.Write(AccountId);
        stream.Write(CharacterId);
        return stream;
    }
}
