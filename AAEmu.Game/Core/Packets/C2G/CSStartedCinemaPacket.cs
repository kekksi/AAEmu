using AAEmu.Commons.Network;
using AAEmu.Game.Core.Network.Game;
using AAEmu.Game.Models.Game.Units;

namespace AAEmu.Game.Core.Packets.C2G;

public class CSStartedCinemaPacket() : GamePacket(CSOffsets.CSStartedCinemaPacket, 1)
{
    public override void Read(PacketStream stream)
    {
        // Empty struct
        Logger.Warn("StartedCinema");
        var character = Connection.ActiveChar;
        character.CurrentlyPlayingCinemaId = character.PendingCinemaId;
        character.Events.OnCinemaStarted(character, new OnCinemaStartedArgs { CinemaId = character.CurrentlyPlayingCinemaId });
    }
}
