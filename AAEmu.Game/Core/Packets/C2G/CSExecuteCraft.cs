using AAEmu.Commons.Network;
using AAEmu.Game.Core.Managers;
using AAEmu.Game.Core.Network.Game;
using AAEmu.Game.Models.Game;

namespace AAEmu.Game.Core.Packets.C2G;

public class CSExecuteCraft() : GamePacket(CSOffsets.CSExecuteCraft, 1)
{
    public override void Read(PacketStream stream)
    {
        var craftId = stream.ReadUInt32();
        var objId = stream.ReadBc();
        var count = stream.ReadInt32();

        Logger.Debug("CSExecuteCraft, craftId : {0} , objId : {1}, count : {2}", craftId, objId, count);

        var character = Connection.ActiveChar;

        if (character == null)
            return;

        if (count <= 0 || !CraftManager.Instance.TryGetCraftById(craftId, out var craft))
        {
            Logger.Warn("Rejected craft request: craftId {0}, objId {1}, count {2}", craftId, objId, count);
            character.SendErrorMessage(ErrorMessageType.CraftInvalidCraftType);
            return;
        }

        if (CraftManager.Instance.IsLearnableCraft(craftId) && !character.Craft.LearnedCraft(craftId))
        {
            Logger.Warn("Rejected unlearned craft request: character {0}, craftId {1}", character.Id, craftId);
            character.SendErrorMessage(ErrorMessageType.CraftNotLearned);
            return;
        }

        if (!character.Craft.TryStartCraft(craft, count, objId))
        {
            Logger.Warn("Rejected concurrent craft request: character {0}, craftId {1}", character.Id, craftId);
            character.SendErrorMessage(ErrorMessageType.CraftCantActAnyMore);
        }
    }
}
