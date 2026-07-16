using AAEmu.Commons.Network;
using AAEmu.Game.Core.Managers.World;
using AAEmu.Game.Core.Network.Game;
using AAEmu.Game.Models.Game.Units;

namespace AAEmu.Game.Core.Packets.C2G;

public class CSCompletedCinemaPacket() : GamePacket(CSOffsets.CSCompletedCinemaPacket, 1)
{
    public override void Read(PacketStream stream)
    {
        // Empty struct
        Logger.Warn("CompletedCinema");

        var character = Connection.ActiveChar;
        if (character is null)
            return;

        var cinemaId = character.CurrentlyPlayingCinemaId;

        // Complete the quest/cinema state before the world is re-synchronised.
        // Re-sending units first puts the client back into the world while its
        // local cinema state is still active, leaving it with the cinematic
        // camera and depth-of-field blur.
        character.Events.OnCinemaEnded(character, new OnCinemaEndedArgs { CinemaId = cinemaId });
        character.CurrentlyPlayingCinemaId = 0;
        WorldManager.ResendVisibleObjectsToCharacter(character);
    }
}
