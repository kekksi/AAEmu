using System.Collections.Concurrent;
using System.Text;
using AAEmu.Commons.Exceptions;
using AAEmu.Commons.Network.Core;
using NLog;

namespace AAEmu.Commons.Network.Cluster;

/// <summary>
/// Frame de-serializer for the cluster RPC channel. Frame layout [ushort len][ushort typeId][body],
/// 1:1 adapted from the Login InternalProtocolHandler. Self-contained: keeps its own connection
/// table so it can be reused server-side (gateway) and client-side (zone).
/// </summary>
public class ClusterProtocolHandler : BaseProtocolHandler, IClusterProtocolHandler
{
    private static Logger Logger { get; } = LogManager.GetCurrentClassLogger();

    private readonly ConcurrentDictionary<ushort, IClusterPacketDescriptor> _packets;
    private readonly ConcurrentDictionary<uint, ClusterConnection> _connections = new();

    public event Action<ClusterConnection> ClientConnected;
    public event Action<ClusterConnection> ClientDisconnected;

    public ClusterProtocolHandler(IEnumerable<IClusterPacketDescriptor> packetDescriptors)
    {
        _packets = new ConcurrentDictionary<ushort, IClusterPacketDescriptor>(
            packetDescriptors.ToDictionary(d => d.TypeId));
    }

    public override void OnConnect(ISession session)
    {
        var con = new ClusterConnection(session);
        _connections[session.SessionId] = con;
        Logger.Info("Cluster peer {0} connected (session {1})", session.Ip, session.SessionId);
        ClientConnected?.Invoke(con);
    }

    public override void OnDisconnect(ISession session)
    {
        if (_connections.TryRemove(session.SessionId, out var con))
        {
            Logger.Info("Cluster peer {0} disconnected", session.Ip);
            ClientDisconnected?.Invoke(con);
        }
    }

    public override void OnReceive(ISession session, byte[] buf, int offset, int bytes)
    {
        if (!_connections.TryGetValue(session.SessionId, out var connection))
        {
            Logger.Error("Cluster connection not found for session {0}", session.SessionId);
            return;
        }

        var stream = new PacketStream();
        if (connection.LastPacket != null)
        {
            stream.Insert(0, connection.LastPacket);
            connection.LastPacket = null;
        }

        stream.Insert(stream.Count, buf, offset, bytes);
        while (stream is { Count: > 0 })
        {
            ushort len;
            try
            {
                len = stream.ReadUInt16();
            }
            catch (MarshalException)
            {
                stream.Rollback();
                connection.LastPacket = stream;
                stream = null;
                continue;
            }

            var packetLen = len + stream.Pos;
            if (packetLen <= stream.Count)
            {
                stream.Rollback();
                var stream2 = new PacketStream();
                stream2.Replace(stream, 0, packetLen);
                if (stream.Count > packetLen)
                {
                    var stream3 = new PacketStream();
                    stream3.Replace(stream, packetLen, stream.Count - packetLen);
                    stream = stream3;
                }
                else
                    stream = null;

                stream2.ReadUInt16();
                var type = stream2.ReadUInt16();
                if (!_packets.TryGetValue(type, out var packetDescriptor))
                {
                    HandleUnknownPacket(session, type, stream2);
                }
                else
                {
                    try
                    {
                        packetDescriptor.Dispatch(stream2, connection);
                    }
                    catch (Exception ex)
                    {
                        Logger.Error(ex, "Error on cluster packet dispatch {0}", type);
                    }
                }
            }
            else
            {
                stream.Rollback();
                connection.LastPacket = stream;
                stream = null;
            }
        }
    }

    private static void HandleUnknownPacket(ISession session, uint type, PacketStream stream)
    {
        var dump = new StringBuilder();
        for (var i = stream.Pos; i < stream.Count; i++)
            dump.Append($"{stream.Buffer[i]:x2} ");
        Logger.Error("Unknown cluster packet 0x{0:x2} from {1}:\n{2}", type, session.Ip, dump);
    }
}
