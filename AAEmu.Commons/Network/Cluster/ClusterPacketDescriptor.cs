namespace AAEmu.Commons.Network.Cluster;

public class ClusterPacketDescriptor<TPacket>(ushort packetId, IClusterPacketHandler<TPacket> handler)
    : IClusterPacketDescriptor
    where TPacket : ClusterPacket, new()
{
    public ushort TypeId { get; } = packetId;

    public void Dispatch(PacketStream stream, ClusterConnection connection)
    {
        var packet = new TPacket { Connection = connection };
        packet.Decode(stream);
        handler.Execute(packet, connection);
    }
}
