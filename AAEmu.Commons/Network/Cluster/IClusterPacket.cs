namespace AAEmu.Commons.Network.Cluster;

public interface IClusterPacket
{
    static abstract ushort TypeId { get; }
}
