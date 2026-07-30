using AAEmu.Commons.Network.Cluster;
using AAEmu.Commons.Network.Cluster.Packets;
using AAEmu.Game.Core.Managers.Id;
using AAEmu.Game.Core.Managers.World;
using AAEmu.Game.Models.Game.Char;

using Microsoft.Extensions.Logging;

namespace AAEmu.Zone;

/// <summary>
/// B2.6b - zone-side character load + spawn.
///
/// The gateway forwards a <see cref="GZEnterZonePacket"/> when a client selected a character whose
/// target world lives in THIS zone process. The gateway did NOT load or spawn the character; the
/// ZONE is the authority. Here we:
///   1. bind (or reuse) the tunnel-backed <see cref="AAEmu.Game.Core.Network.Connections.GameConnection"/>
///      for the client connection (same instance the tunneled client frames dispatch against),
///   2. load the character straight from the shared DB (<see cref="Character.Load(uint,uint)"/>),
///   3. force it into the zone-local main_world instance (Transform.InstanceId resolves ParentWorld
///      against the zone-local <see cref="WorldManager"/>),
///   4. deep-load its sub-data (<see cref="Character.Load()"/>),
///   5. assign an ObjId from the ZONE's own <see cref="ObjectIdManager"/> - the ObjId authority is
///      the zone. The gateway assigns none for zone-owned connections, so no cross-process ObjId
///      collision is possible for the hand-off character.
///   6. add it to the zone-local world pool + spawn it into the WorldInstance region.
///
/// Everything is exception-swept: a bad hand-off is logged and turned into a no-op so it can never
/// take down the zone tick / cluster loop.
///
/// NOTE on persistence: the zone never calls SaveManager.Initialize() (only the periodic engines
/// Time/Task/AI/World are started in ZoneWorldBootstrap), so the zone runs NO periodic character
/// save. The gateway also does not add zone-owned characters to its own WorldManager, so it does not
/// save them either. For the B2.6b PoC the hand-off char is therefore not periodically persisted
/// from either side, so there is no double-persist against the shared dev DB.
/// </summary>
public class GZEnterZonePacketHandler(ILogger logger) : IClusterPacketHandler<GZEnterZonePacket>
{
    public void Execute(GZEnterZonePacket packet, ClusterConnection connection)
    {
        logger.LogInformation(
            "[B2.6b] zone got GZEnterZone conn={ConnId} account={Acc} char={Char}",
            packet.ConnectionId, packet.AccountId, packet.CharacterId);

        try
        {
            // 1. Bind the tunnel-backed GameConnection (shared with the client-frame tunnel path).
            var gameConnection = GZClientPacketHandler.GetOrCreateConnection(packet.ConnectionId, connection);

            // 2. Load the character straight from the shared DB. ZONE is the loader now.
            var character = Character.Load(packet.CharacterId, packet.AccountId);
            if (character == null)
            {
                logger.LogError(
                    "[B2.6b] zone char load FAILED conn={ConnId} char={Char} account={Acc} (no such character)",
                    packet.ConnectionId, packet.CharacterId, packet.AccountId);
                return;
            }

            // 3. Force into the zone-local main_world. Setting InstanceId resolves ParentWorld via
            //    WorldManager.Instance.GetWorld(DefaultInstanceId) - i.e. the zone-local instance.
            character.Transform.InstanceId = WorldManager.DefaultInstanceId;

            // 4. Deep-load sub-data (abilities/skills/quests/mails/... all zone-local managers).
            character.Load();

            // 5. Bind connection + ZONE-authoritative ObjId.
            character.Connection = gameConnection;
            gameConnection.ActiveChar = character;
            character.ObjId = ObjectIdManager.Instance.GetNextId();

            // 6. Add to the zone-local world pool and spawn into the WorldInstance region.
            var added = WorldManager.Instance.TryAddCharacter(character);
            character.Spawn();

            var players = WorldManager.Instance.GetAllCharacters().Count;
            var worldId = character.ParentWorld?.Id ?? uint.MaxValue;
            var inRegion = character.Region != null;
            logger.LogInformation(
                "[B2.6b] CHAR LIVES IN ZONE: name={Name} id={Id} objId={ObjId} pool+={Added} " +
                "world={World} zone={Zone} pos=({X:F1},{Y:F1},{Z:F1}) inRegion={InRegion} " +
                "=> players in zone instance now={Players}",
                character.Name, character.Id, character.ObjId, added, worldId,
                character.Transform.ZoneId,
                character.Transform.World.Position.X, character.Transform.World.Position.Y,
                character.Transform.World.Position.Z, inRegion, players);
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "[B2.6b] zone char load+spawn threw conn={ConnId} char={Char} - hand-off aborted, zone stays alive",
                packet.ConnectionId, packet.CharacterId);
        }
    }
}
