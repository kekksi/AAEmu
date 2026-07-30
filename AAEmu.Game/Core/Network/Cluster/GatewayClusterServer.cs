using System.Net;
using AAEmu.Commons.Network.Cluster;
using AAEmu.Commons.Network.Cluster.Packets;
using AAEmu.Commons.Network.Core;
using AAEmu.Commons.Utils;
using AAEmu.Game.Models;
using NLog;

namespace AAEmu.Game.Core.Network.Cluster;

/// <summary>
/// TCP server that accepts N zone processes over the cluster RPC channel (port 1300 by default).
/// Mirrors the GameNetwork/StreamNetwork singleton style.
/// </summary>
public class GatewayClusterServer : Singleton<GatewayClusterServer>
{
    private static Logger Logger { get; } = LogManager.GetCurrentClassLogger();
    private Server _server;
    private readonly ClusterProtocolHandler _handler;

    private GatewayClusterServer()
    {
        var descriptors = new IClusterPacketDescriptor[]
        {
            new ClusterPacketDescriptor<ZGRegisterZonePacket>(
                ZGRegisterZonePacket.TypeId, new ZGRegisterZonePacketHandler()),
            new ClusterPacketDescriptor<ZGHeartbeatPacket>(
                ZGHeartbeatPacket.TypeId, new ZGHeartbeatPacketHandler()),
        };
        _handler = new ClusterProtocolHandler(descriptors);
        _handler.ClientDisconnected += con => ZoneRegistry.Instance.RemoveByConnection(con);
    }

    public void Start()
    {
        var config = AppConfiguration.Instance.ClusterNetwork
                     ?? new AppConfiguration.NetworkConfig { Host = "*", Port = 1300 };
        _server = new Server(config.Host.Equals("*") ? IPAddress.Any : IPAddress.Parse(config.Host),
            config.Port, _handler);
        _server.Start();
        Logger.Info("Cluster listening {0}", config.Port);
    }

    public void Stop()
    {
        if (_server?.IsStarted ?? false)
            _server.Stop();
        Logger.Info("Cluster gateway stopped");
    }
}
