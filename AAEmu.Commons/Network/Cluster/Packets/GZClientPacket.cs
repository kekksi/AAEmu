namespace AAEmu.Commons.Network.Cluster.Packets;

/// <summary>
/// Gateway -&gt; Zone. Tunnels a raw client wire-frame (the exact bytes a game client sent)
/// for the client session identified by <see cref="ConnectionId"/>. B2.4a plumbing only.
/// </summary>
public class GZClientPacket : ClusterPacket, IClusterPacket
{
    public new static ushort TypeId => ClusterOffsets.GZClientPacket;

    public uint ConnectionId { get; private set; }
    public byte[] Payload { get; private set; }

    public GZClientPacket() : base(TypeId) { }

    public GZClientPacket(uint connectionId, byte[] payload) : base(TypeId)
    {
        ConnectionId = connectionId;
        Payload = payload;
    }

    public override void Read(PacketStream stream)
    {
        ConnectionId = stream.ReadUInt32();
        Payload = stream.ReadBytes();
    }

    public override PacketStream Write(PacketStream stream)
    {
        stream.Write(ConnectionId);
        stream.Write(Payload ?? [], true);
        return stream;
    }
}
