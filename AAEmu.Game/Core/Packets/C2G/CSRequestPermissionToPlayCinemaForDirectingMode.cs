using AAEmu.Commons.Network;
using AAEmu.Game.Core.Managers;
using AAEmu.Game.Core.Network.Game;

namespace AAEmu.Game.Core.Packets.C2G;

public class CSRequestPermissionToPlayCinemaForDirectingMode()
    : GamePacket(CSOffsets.CSRequestPermissionToPlayCinemaForDirectingMode, 1)
{
    public override void Read(PacketStream stream)
    {
        var questContextId = stream.ReadUInt32();
        var npcObjId = stream.ReadBc();
        var doodadObjId = stream.ReadBc();

        Logger.Warn("CSRequestPermissionToPlayCinemaForDirectingMode");

        var character = Connection.ActiveChar;
        if (character?.Quests.ActiveQuests.TryGetValue(questContextId, out var quest) == true)
        {
            var component = QuestManager.Instance.GetComponent(quest.ComponentId);
            character.PendingCinemaId = component?.CinemaId ?? 0;
        }
    }
}
