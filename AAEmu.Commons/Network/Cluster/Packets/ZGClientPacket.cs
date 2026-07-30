namespace AAEmu.Commons.Network.Cluster.Packets;

/// <summary>
/// Zone -&gt; Gateway. Tunnels a raw client wire-frame that should be written back out to the
/// client session identified by <see cref="ConnectionId"/>. B2.4a plumbing only.
/// </summary>
public class ZGClientPacket : ClusterPacket, IClusterPacket
{
    public new static ushort TypeId => ClusterOffsets.ZGClientPacket;

    public uint ConnectionId { get; private set; }
    public byte[] Payload { get; private set; }

    public ZGClientPacket() : base(TypeId) { }

    public ZGClientPacket(uint connectionId, byte[] payload) : base(TypeId)
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
