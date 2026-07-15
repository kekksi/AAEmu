using AAEmu.Commons.Network;
using AAEmu.Game.Core.Network.Game;
using AAEmu.Game.Core.Packets.Debug;
using AAEmu.Game.Core.Packets.G2C;
using AAEmu.Game.Models.Game.Skills;
using AAEmu.Game.Models.Game.Skills.SkillControllers;

namespace AAEmu.Game.Core.Packets.C2G;

public class CSSkillControllerStatePacket() : GamePacket(CSOffsets.CSSkillControllerStatePacket, 1)
{
    public override void Read(PacketStream stream)
    {
        var objId = stream.ReadBc();
        var scType = stream.ReadByte();
        float? len = null;
        bool? teared = null;
        bool? cutouted = null;
        if (scType == 0)
        {
            len = stream.ReadSingle();
            teared = stream.ReadBoolean();
            cutouted = stream.ReadBoolean();
        }

        SkillControllerPacketDebug.LogCsSkillControllerState(objId, scType, len, teared, cutouted);

        if (Connection.ActiveChar != null && scType == 0 && len.HasValue && teared.HasValue && cutouted.HasValue)
            ShipHarpoonRopeController.TryApplySkillControllerState(Connection.ActiveChar, objId, len.Value, teared.Value, cutouted.Value);

        var character = Connection.ActiveChar;
        if (character == null || objId != character.ObjId)
            return;

        // Movement controllers are client-driven, but their state must be
        // acknowledged by the server. Without this reply skills such as Free
        // Runner and Jump Pack can remain active after the client ends them.
        character.BroadcastPacket(new SCSkillControllerStatePacket(
            objId,
            scType,
            len ?? 0f,
            teared ?? false,
            cutouted ?? false), true);
    }

    // TODO 
    /*
     *
          if ( a2->Reader->field_1C() )
          {
            a2->Reader->ReadByte("scType", &v7 + 3, 0);
            v2[1] = HIBYTE(v7);
          }
          else
          {
            HIBYTE(v7) = *(v2 + 4);
            a2->Reader->ReadByte("scType", &v7 + 3, 0);
          }
          if ( !v2[1] )
          {
            a2->Reader->ReadFloat("len", v2 + 2, 0);
            a2->Reader->ReadBool("teared", (v2 + 3), 0);
            a2->Reader->ReadBool("cutouted", v2 + 13, 0);
          }
     */
}
