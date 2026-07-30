using System.Net;
using AAEmu.Commons.Network.Cluster;
using AAEmu.Commons.Network.Cluster.Packets;
using AAEmu.Commons.Network.Core;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AAEmu.Zone;

/// <summary>
/// Zone-side TCP client. Connects to the gateway on port 1300, registers this zone,
/// then heartbeats every 5s. Uses the shared ClusterProtocolHandler to parse gateway replies.
/// </summary>
public class ZoneClusterClient
{
    private readonly ZoneConfig _config;
    private readonly ILogger<ZoneClusterClient> _logger;

    private Client? _client;
    private ClusterProtocolHandler? _handler;
    private ClusterConnection? _connection;
    private Timer? _heartbeatTimer;

    public ZoneClusterClient(IOptions<ZoneConfig> config, ILogger<ZoneClusterClient> logger)
    {
        _config = config.Value;
        _logger = logger;
    }

    public void Start()
    {
        var descriptors = new IClusterPacketDescriptor[]
        {
            new ClusterPacketDescriptor<GZRegisterResultPacket>(
                GZRegisterResultPacket.TypeId, new GZRegisterResultPacketHandler(_logger)),
            new ClusterPacketDescriptor<GZHeartbeatAckPacket>(
                GZHeartbeatAckPacket.TypeId, new GZHeartbeatAckPacketHandler(_logger)),
        };
        _handler = new ClusterProtocolHandler(descriptors);
        _handler.ClientConnected += OnConnected;
        _handler.ClientDisconnected += _ => _logger.LogWarning("Disconnected from gateway");

        var address = Dns.GetHostAddresses(_config.GatewayHost).First();
        _client = new Client(address, _config.GatewayPort, _handler);
        _logger.LogInformation("Connecting to gateway {Host}:{Port}", _config.GatewayHost, _config.GatewayPort);
        _client.ConnectAsync();
    }

    private void OnConnected(ClusterConnection connection)
    {
        _connection = connection;
        _logger.LogInformation("Connected to gateway, registering zone {ZoneId}", _config.ZoneId);
        connection.SendPacket(new ZGRegisterZonePacket(_config.SecretKey, _config.ZoneId, _config.WorldTemplateId));

        _heartbeatTimer = new Timer(_ =>
        {
            try
            {
                _connection?.SendPacket(new ZGHeartbeatPacket(_config.ZoneId, 0));
                _logger.LogDebug("Heartbeat sent for zone {ZoneId}", _config.ZoneId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Heartbeat send failed");
            }
        }, null, TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(5));
    }

    public void Stop()
    {
        _heartbeatTimer?.Dispose();
        if (_client?.IsConnected ?? false)
            _client.DisconnectAsync();
    }
}
