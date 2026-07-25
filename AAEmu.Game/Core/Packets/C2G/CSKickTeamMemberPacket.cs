using AAEmu.Commons.Network;
using AAEmu.Game.Core.Managers;
using AAEmu.Game.Core.Network.Game;
using AAEmu.Game.Models.Game.Team;

namespace AAEmu.Game.Core.Packets.C2G;

public class CSKickTeamMemberPacket() : GamePacket(CSOffsets.CSKickTeamMemberPacket, 1)
{
    public override void Read(PacketStream stream)
    {
        var teamId = stream.ReadUInt32();
        var memberId = stream.ReadUInt32();

        TeamManager.Instance.AskRiskyTeam(Connection.ActiveChar, teamId, memberId, RiskyAction.Kick);
    }
}
