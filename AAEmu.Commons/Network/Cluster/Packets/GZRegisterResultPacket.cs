namespace AAEmu.Commons.Network.Cluster.Packets;

/// <summary>Gateway -&gt; Zone. Result of a zone register request.</summary>
public class GZRegisterResultPacket : ClusterPacket, IClusterPacket
{
    public new static ushort TypeId => ClusterOffsets.GZRegisterResult;

    public bool Success { get; private set; }
    public string Message { get; private set; }

    public GZRegisterResultPacket() : base(TypeId) { }

    public GZRegisterResultPacket(bool success, string message) : base(TypeId)
    {
        Success = success;
        Message = message;
    }

    public override void Read(PacketStream stream)
    {
        Success = stream.ReadBoolean();
        Message = stream.ReadString();
    }

    public override PacketStream Write(PacketStream stream)
    {
        stream.Write(Success);
        stream.Write(Message ?? string.Empty);
        return stream;
    }
}
