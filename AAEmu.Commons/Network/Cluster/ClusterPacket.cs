namespace AAEmu.Commons.Network.Cluster;

/// <summary>
/// Base class for cluster RPC packets. Wire frame: [ushort len][ushort typeId][body].
/// Adapted from the Login InternalPacket stack.
/// </summary>
public abstract class ClusterPacket(ushort typeId) : PacketBase<ClusterConnection>(typeId)
{
    public override PacketStream Encode()
    {
        var ps = new PacketStream();
        try
        {
            ps.Write(new PacketStream().Write(TypeId).Write(this));
        }
        catch (Exception ex)
        {
            Logger.Fatal(ex);
            throw;
        }

        return ps;
    }

    public override PacketBase<ClusterConnection> Decode(PacketStream ps)
    {
        try
        {
            Read(ps);
        }
        catch (Exception ex)
        {
            Logger.Fatal(ex);
            throw;
        }

        return this;
    }
}
