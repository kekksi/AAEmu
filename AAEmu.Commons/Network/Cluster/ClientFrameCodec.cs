namespace AAEmu.Commons.Network.Cluster;

/// <summary>
/// Helpers to build/parse a raw AA 1.2 client wire-frame, shared by both tunnel ends so the
/// gateway and zone agree on layout without pulling in the Game packet stack.
/// Frame layout: [u16 len][u8 unk=0xdd][u8 level]([u8 crc][u8 counter] if level==1)[u16 type][body].
/// len = number of bytes following the length field (i.e. frame.Length - 2).
/// </summary>
public static class ClientFrameCodec
{
    /// <summary>Builds a synthetic client frame (used for the B2.4a round-trip proof).</summary>
    public static byte[] BuildClientFrame(ushort type, byte level = 2, byte[] body = null)
    {
        var inner = new PacketStream();
        inner.Write((byte)0xdd);
        inner.Write(level);
        if (level == 1)
        {
            inner.Write((byte)0); // crc placeholder
            inner.Write((byte)0); // counter placeholder
        }
        inner.Write(type);
        if (body is { Length: > 0 })
            inner.Write(body);

        var frame = new PacketStream();
        frame.Write((ushort)inner.Count);
        frame.Write(inner, false); // append inner bytes without an extra size prefix
        return frame;
    }

    /// <summary>Extracts the client opcode (type id) from a raw frame. Returns 0xffff if malformed.</summary>
    public static ushort ParseOpcode(byte[] frame)
    {
        if (frame is not { Length: >= 6 })
            return 0xffff;
        try
        {
            var ps = new PacketStream();
            ps.Insert(0, frame, 0, frame.Length);
            ps.ReadUInt16(); // len
            ps.ReadByte();   // unk (0xdd)
            var level = ps.ReadByte();
            if (level == 1)
            {
                ps.ReadByte(); // crc
                ps.ReadByte(); // counter
            }
            return ps.ReadUInt16();
        }
        catch
        {
            return 0xffff;
        }
    }
}
