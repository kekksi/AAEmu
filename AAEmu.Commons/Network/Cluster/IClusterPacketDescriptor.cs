namespace AAEmu.Commons.Network.Cluster;

public interface IClusterPacketDescriptor
{
    ushort TypeId { get; }
    void Dispatch(PacketStream stream, ClusterConnection connection);
}
