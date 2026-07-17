using System.Collections.Concurrent;
using System.Net;
using AAEmu.Commons.Exceptions;
using AAEmu.Commons.Network;
using AAEmu.Commons.Network.Core;
using AAEmu.Game.Core.Network.Connections;
using NLog;

namespace AAEmu.Game.Core.Network.Stream;

public class StreamProtocolHandler : BaseProtocolHandler
{
    private const long InvalidFrameLogWindowMs = 10_000;
    private const int MaxTrackedInvalidFrameIps = 4_096;

    private static Logger Logger { get; } = LogManager.GetCurrentClassLogger();
    private static readonly ConcurrentDictionary<IPAddress, long> InvalidFrameLogTimes = new();

    private readonly ConcurrentDictionary<uint, Type> _packets = new();

    public override void OnConnect(ISession session)
    {
        Logger.Info("Connect from {0} established, session id: {1}", session.Ip.ToString(), session.SessionId.ToString());
        try
        {
            var con = new StreamConnection(session);
            StreamConnection.OnConnect();
            StreamConnectionTable.Instance.AddConnection(con);
        }
        catch (Exception e)
        {
            session.Close();
            Logger.Error(e);
        }
    }

    public override void OnDisconnect(ISession session)
    {
        try
        {
            var con = StreamConnectionTable.Instance.GetConnection(session.SessionId);
            if (con != null)
                StreamConnectionTable.Instance.RemoveConnection(session.SessionId);
        }
        catch (Exception e)
        {
            session.Close();
            Logger.Error(e);
        }

        Logger.Info("Client from {0} disconnected", session.Ip.ToString());
    }

    public override void OnReceive(ISession session, byte[] buf, int offset, int bytes)
    {
        try
        {
            var connection = StreamConnectionTable.Instance.GetConnection(session.SessionId);
            if (connection == null)
                return;
            OnReceive(connection, buf, offset, bytes);
        }
        catch (Exception e)
        {
            session.Close();
            Logger.Error(e);
        }
    }

    public void OnReceive(StreamConnection connection, byte[] buf, int offset, int bytes)
    {
        try
        {
            var stream = new PacketStream();
            if (connection.LastPacket != null)
            {
                stream.Insert(0, connection.LastPacket);
                connection.LastPacket = null;
            }

            stream.Insert(stream.Count, buf, offset, bytes);
            while (stream != null && stream.Count > 0)
            {
                ushort len;
                try
                {
                    len = stream.ReadUInt16();
                }
                catch (MarshalException)
                {
                    //Logger.Warn("Error on reading type {0}", type);
                    stream.Rollback();
                    connection.LastPacket = stream;
                    stream = null;
                    continue;
                }

                // A stream frame must at least contain its two-byte packet type.
                if (len < sizeof(ushort))
                {
                    RejectInvalidFrame(connection, $"invalid payload length {len}");
                    return;
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

                    stream2.ReadUInt16(); //len
                    var type = stream2.ReadUInt16();
                    _packets.TryGetValue(type, out var classType);
                    if (classType == null)
                    {
                        RejectInvalidFrame(connection, $"unknown packet type 0x{type:x4}");
                        return;
                    }
                    else
                    {
                        var packet = (StreamPacket)Activator.CreateInstance(classType);
                        packet.Connection = connection;
                        packet.Decode(stream2);
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
        catch (Exception e)
        {
            RejectInvalidFrame(connection, "packet decode failed", e);
        }
    }

    public void RegisterPacket(uint type, Type classType)
    {
        if (_packets.ContainsKey(type))
            _packets.TryRemove(type, out _);
        _packets.TryAdd(type, classType);
    }

    private static void RejectInvalidFrame(StreamConnection connection, string reason, Exception exception = null)
    {
        if (connection == null)
            return;

        connection.LastPacket = null;
        connection.Shutdown();

        if (!ShouldLogInvalidFrame(connection.Ip))
            return;

        if (exception == null)
            Logger.Warn("Rejected malformed stream frame from {0}: {1}; connection closed", connection.Ip, reason);
        else
            Logger.Warn(exception, "Rejected malformed stream frame from {0}: {1}; connection closed", connection.Ip, reason);
    }

    private static bool ShouldLogInvalidFrame(IPAddress ip)
    {
        var now = Environment.TickCount64;
        while (true)
        {
            if (!InvalidFrameLogTimes.TryGetValue(ip, out var lastLogged))
            {
                if (!InvalidFrameLogTimes.TryAdd(ip, now))
                    continue;

                TrimInvalidFrameLogTimes(now);
                return true;
            }

            if (now - lastLogged < InvalidFrameLogWindowMs)
                return false;

            if (InvalidFrameLogTimes.TryUpdate(ip, now, lastLogged))
            {
                TrimInvalidFrameLogTimes(now);
                return true;
            }
        }
    }

    private static void TrimInvalidFrameLogTimes(long now)
    {
        if (InvalidFrameLogTimes.Count <= MaxTrackedInvalidFrameIps)
            return;

        foreach (var entry in InvalidFrameLogTimes)
        {
            if (now - entry.Value >= InvalidFrameLogWindowMs)
                InvalidFrameLogTimes.TryRemove(entry.Key, out _);
        }

        // Keep spoofed source addresses from turning the limiter itself into unbounded state.
        foreach (var ip in InvalidFrameLogTimes.Keys)
        {
            if (InvalidFrameLogTimes.Count <= MaxTrackedInvalidFrameIps)
                break;
            InvalidFrameLogTimes.TryRemove(ip, out _);
        }
    }
}
