using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using AAEmu.Commons.Network.Cluster;
using AAEmu.Commons.Network.Cluster.Packets;
using AAEmu.Commons.Network.Core;

namespace AAEmu.Zone;

/// <summary>
/// B2.4d: an <see cref="ISession"/> with NO real socket. It represents a client that is physically
/// connected to the GATEWAY; from the zone process point of view it is simply the transport that a
/// real <c>GameConnection</c> writes into. Anything the GameConnection sends (via
/// <c>GameConnection.SendRaw</c> -&gt; <see cref="SendPacket"/>) is wrapped in a
/// <see cref="ZGClientPacket"/> and pushed back to the gateway over the cluster channel; the gateway
/// then writes it onto the actual client socket (B2.4c). This is what lets a GENUINE GameConnection
/// live inside a zone without owning a TCP socket of its own.
/// </summary>
public class TunnelSession : ISession
{
    private readonly uint _connectionId;
    private readonly ClusterConnection _gateway;
    private readonly ConcurrentDictionary<string, object> _attributes = new();

    public TunnelSession(uint connectionId, ClusterConnection gateway)
    {
        _connectionId = connectionId;
        _gateway = gateway;
    }

    public IPAddress Ip => IPAddress.Loopback;
    public uint SessionId => _connectionId;

    // No real socket: the client socket lives on the gateway, not here.
    public Socket Socket => null!;

    /// <summary>
    /// Sends an already-framed client payload back to the client by tunneling it to the gateway.
    /// </summary>
    public void SendPacket(byte[] packet)
    {
        _gateway.SendPacket(new ZGClientPacket(_connectionId, packet));
    }

    public void AddAttribute(string name, object attribute) => _attributes[name] = attribute;
    public object GetAttribute(string name) => _attributes.TryGetValue(name, out var v) ? v : null!;
    public void ClearAttribute(string name) => _attributes.TryRemove(name, out _);

    // B2.4d: minimal. Later (B2.5/B2.6) Close will also signal the gateway to clear the ownership
    // record for this connection in ClientRoutingTable.
    public void Close() { }
}
