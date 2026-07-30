namespace AAEmu.Commons.Network.Cluster.Packets;

/// <summary>Gateway -&gt; Zone. Ack for a heartbeat, carries server time.</summary>
public class GZHeartbeatAckPacket : ClusterPacket, IClusterPacket
{
    public new static ushort TypeId => ClusterOffsets.GZHeartbeatAck;

    public long ServerTime { get; private set; }

    public GZHeartbeatAckPacket() : base(TypeId) { }

    public GZHeartbeatAckPacket(long serverTime) : base(TypeId)
    {
        ServerTime = serverTime;
    }

    public override void Read(PacketStream stream)
    {
        ServerTime = stream.ReadInt64();
    }

    public override PacketStream Write(PacketStream stream)
    {
        stream.Write(ServerTime);
        return stream;
    }
}
