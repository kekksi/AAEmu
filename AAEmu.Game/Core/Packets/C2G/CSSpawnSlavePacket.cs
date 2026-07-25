using System.Numerics;
using AAEmu.Commons.Network;
using AAEmu.Commons.Utils;
using AAEmu.Game.Core.Network.Game;
using AAEmu.Game.GameData;
using AAEmu.Game.Models;
using AAEmu.Game.Models.Game;
using AAEmu.Game.Models.Game.Items;
using AAEmu.Game.Models.Game.Items.Templates;
using AAEmu.Game.Models.Game.World.Transform;

namespace AAEmu.Game.Core.Packets.C2G;

public class CSSpawnSlavePacket() : GamePacket(CSOffsets.CSSpawnSlavePacket, 1)
{
    public override void Read(PacketStream stream)
    {
        var slaveId = stream.ReadUInt32();
        var x = Helpers.ConvertLongX(stream.ReadInt64());
        var y = Helpers.ConvertLongY(stream.ReadInt64());
        var z = stream.ReadSingle();
        var zRot = stream.ReadSingle();
        var itemId = stream.ReadUInt64();

        // TODO : check this part with nikes
        stream.ReadByte();
        var slotType = (SlotType)stream.ReadByte();
        stream.ReadByte();
        var slot = stream.ReadByte();

        var hideSpawnEffect = stream.ReadBoolean();

        var owner = Connection.ActiveChar;
        var item = owner.Inventory.GetItem(slotType, slot);
        if (item == null || item.Id != itemId)
        {
            Logger.Warn($"SpawnSlave rejected for {owner.Name} ({owner.Id}): item {itemId} was not found in {slotType}:{slot}");
            return;
        }

        if (item is not SummonSlave summonItem || summonItem.Template is not SummonSlaveTemplate summonTemplate)
        {
            Logger.Warn($"SpawnSlave rejected for {owner.Name} ({owner.Id}): item {itemId} is not a summon-slave item");
            return;
        }

        if (summonTemplate.SlaveId != slaveId)
        {
            Logger.Warn($"SpawnSlave rejected for {owner.Name} ({owner.Id}): packet slave {slaveId} does not match item slave {summonTemplate.SlaveId}");
            return;
        }

        var slaveTemplate = SlaveGameData.Instance.GetSlaveTemplate(slaveId);
        if (slaveTemplate == null)
        {
            Logger.Warn($"SpawnSlave rejected for {owner.Name} ({owner.Id}): slave template {slaveId} does not exist");
            return;
        }

        var requestedPosition = new Vector3(x, y, z);
        var maxDistance = slaveTemplate.SpawnValidAreaRance;
        if (maxDistance > 0 && Vector3.DistanceSquared(owner.Transform.World.Position, requestedPosition) > maxDistance * maxDistance)
        {
            owner.SendErrorMessage(ErrorMessageType.SlaveSpawnErrorInvalidArea);
            Logger.Warn($"SpawnSlave rejected for {owner.Name} ({owner.Id}): requested position is outside the {maxDistance}m range");
            return;
        }

        using var spawnPosition = new Transform(null, null, owner.Transform.ZoneId, owner.Transform.InstanceId, x, y, z, zRot);
        var spawnedSlave = owner.ParentWorld.SlaveManager.Create(owner, null, slaveId, summonItem, hideSpawnEffect, spawnPosition);
        if (spawnedSlave == null)
        {
            Logger.Warn($"SpawnSlave rejected for {owner.Name} ({owner.Id}): active slave could not be replaced");
            return;
        }

        Logger.Debug($"SpawnSlave created for {owner.Name} ({owner.Id}): slave {slaveId}, item {itemId}, position {requestedPosition}");
    }
}
