using AAEmu.Commons.Network;
using AAEmu.Game.Core.Network.Game;

namespace AAEmu.Game.Core.Packets.G2C;

public class SCCraftItemUnlockPacket(uint craftId) : GamePacket(SCOffsets.SCCraftItemUnlockPacket, 1)
{
    public override PacketStream Write(PacketStream stream)
    {
        stream.Write(craftId);
        return stream;
    }
}
