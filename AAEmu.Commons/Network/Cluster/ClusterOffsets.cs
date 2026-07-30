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

    // B2.4a: client packet tunnel envelopes (raw client wire-frames)
    public const ushort GZClientPacket = 0x0010;
    public const ushort ZGClientPacket = 0x0011;

    // B2.6b: gateway -> zone character hand-off (zone loads + spawns the char itself)
    public const ushort GZEnterZone = 0x0012;
}
