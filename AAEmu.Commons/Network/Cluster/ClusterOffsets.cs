namespace AAEmu.Commons.Network.Cluster;

/// <summary>
/// TypeId constants for cluster (zone &lt;-&gt; gateway) RPC packets.
/// </summary>
public static class ClusterOffsets
{
    public const ushort ZGRegisterZone = 0x0001;
    public const ushort GZRegisterResult = 0x0002;
    public const ushort ZGHeartbeat = 0x0003;
    public const ushort GZHeartbeatAck = 0x0004;
}
