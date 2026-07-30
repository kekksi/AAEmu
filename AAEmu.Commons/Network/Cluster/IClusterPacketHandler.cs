namespace AAEmu.Commons.Network.Cluster;

public interface IClusterPacketHandler
{
    void Execute(ClusterPacket packet, ClusterConnection connection);
}

public interface IClusterPacketHandler<in TPacket> : IClusterPacketHandler
    where TPacket : ClusterPacket
{
    void Execute(TPacket packet, ClusterConnection connection);

    void IClusterPacketHandler.Execute(ClusterPacket packet, ClusterConnection connection) =>
        Execute((TPacket)packet, connection);
}
