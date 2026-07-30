namespace AAEmu.Commons.Network.Cluster.Packets;

/// <summary>Zone -&gt; Gateway. Zone announces itself and authenticates via the shared secret key.</summary>
public class ZGRegisterZonePacket : ClusterPacket, IClusterPacket
{
    public new static ushort TypeId => ClusterOffsets.ZGRegisterZone;

    public string SecretKey { get; private set; }
    public uint ZoneId { get; private set; }
    public uint WorldTemplateId { get; private set; }

    public ZGRegisterZonePacket() : base(TypeId) { }

    public ZGRegisterZonePacket(string secretKey, uint zoneId, uint worldTemplateId) : base(TypeId)
    {
        SecretKey = secretKey;
        ZoneId = zoneId;
        WorldTemplateId = worldTemplateId;
    }

    public override void Read(PacketStream stream)
    {
        SecretKey = stream.ReadString();
        ZoneId = stream.ReadUInt32();
        WorldTemplateId = stream.ReadUInt32();
    }

    public override PacketStream Write(PacketStream stream)
    {
        stream.Write(SecretKey);
        stream.Write(ZoneId);
        stream.Write(WorldTemplateId);
        return stream;
    }
}
