namespace AAEmu.Commons.Network.Cluster.Packets;

/// <summary>Zone -&gt; Gateway. Periodic liveness ping.</summary>
public class ZGHeartbeatPacket : ClusterPacket, IClusterPacket
{
    public new static ushort TypeId => ClusterOffsets.ZGHeartbeat;

    public uint ZoneId { get; private set; }
    public byte Load { get; private set; }

    public ZGHeartbeatPacket() : base(TypeId) { }

    public ZGHeartbeatPacket(uint zoneId, byte load = 0) : base(TypeId)
    {
        ZoneId = zoneId;
        Load = load;
    }

    public override void Read(PacketStream stream)
    {
        ZoneId = stream.ReadUInt32();
        Load = stream.ReadByte();
    }

    public override PacketStream Write(PacketStream stream)
    {
        stream.Write(ZoneId);
        stream.Write(Load);
        return stream;
    }
}
