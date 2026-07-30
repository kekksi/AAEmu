using System.Net;
using AAEmu.Commons.Network.Core;

namespace AAEmu.Commons.Network.Cluster;

/// <summary>
/// Wraps an <see cref="ISession"/> for cluster RPC traffic. Works both server-side
/// (gateway accepting zones) and client-side (zone connecting to gateway).
/// </summary>
public class ClusterConnection(ISession session)
{
    public uint Id => session.SessionId;
    public IPAddress Ip => session.Ip;
    public bool Block { get; set; }
    public PacketStream LastPacket { get; set; }

    public void SendPacket(ClusterPacket packet)
    {
        if (Block)
            return;
        packet.Connection = this;
        byte[] buf = packet.Encode();
        session.SendPacket(buf);
    }

    public void AddAttribute(string name, object value) => session.AddAttribute(name, value);
    public object GetAttribute(string name) => session.GetAttribute(name);
}
