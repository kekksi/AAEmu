using AAEmu.Commons.Network.Cluster;
using AAEmu.Commons.Network.Cluster.Packets;
using AAEmu.Game.Core.Managers;
using AAEmu.Game.Core.Managers.Id;
using AAEmu.Game.Core.Managers.World;
using AAEmu.Game.Core.Network.Connections;
using AAEmu.Game.Core.Packets.G2C;
using AAEmu.Game.Models.Game.Char;

using Microsoft.Extensions.Logging;

namespace AAEmu.Zone;

/// <summary>
/// B2.6b - zone-side character load + spawn.
/// B2.6c - after load+spawn the zone GENERATES the char-enter init sequence and sends it back
///         through the tunnel toward the client (same S2C packets the monolith emits at the end of
///         CSSelectCharacter). See <see cref="EmitCharEnterInitSequence"/>.
///
/// The gateway forwards a <see cref="GZEnterZonePacket"/> when a client selected a character whose
/// target world lives in THIS zone process. The gateway did NOT load or spawn the character; the
/// ZONE is the authority. Here we:
///   1. bind (or reuse) the tunnel-backed <see cref="GameConnection"/>
///      for the client connection (same instance the tunneled client frames dispatch against),
///   2. load the character straight from the shared DB (<see cref="Character.Load(uint,uint)"/>),
///   3. force it into the zone-local main_world instance (Transform.InstanceId resolves ParentWorld
///      against the zone-local <see cref="WorldManager"/>),
///   4. deep-load its sub-data (<see cref="Character.Load()"/>),
///   5. assign an ObjId from the ZONE's own <see cref="ObjectIdManager"/> - the ObjId authority is
///      the zone. The gateway assigns none for zone-owned connections, so no cross-process ObjId
///      collision is possible for the hand-off character.
///   6. add it to the zone-local world pool + spawn it into the WorldInstance region.
///   7. (B2.6c) emit the char-enter init sequence back through the tunnel.
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

            // 7. B2.6c - GENERATE + tunnel the char-enter init sequence toward the client.
            EmitCharEnterInitSequence(gameConnection, character);
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "[B2.6b] zone char load+spawn threw conn={ConnId} char={Char} - hand-off aborted, zone stays alive",
                packet.ConnectionId, packet.CharacterId);
        }
    }

    /// <summary>
    /// B2.6c - the char-enter init sequence, emitted from the ZONE.
    ///
    /// This mirrors the S2C init block the monolith runs at the SUCCESS branch of
    /// <c>CSSelectCharacterPacket.Read</c>. Because <paramref name="connection"/> is a genuine
    /// <see cref="GameConnection"/> backed by a <c>TunnelSession</c>, every <c>SendPacket</c> here is
    /// encoded to a real client wire-frame and tunneled to the gateway as a <c>ZGClientPacket</c>
    /// (which the gateway logs by opcode and, for a live client, writes to the socket). We REUSE the
    /// exact same S2C packet classes / Send() helpers the monolith uses - nothing is re-implemented.
    ///
    /// SCUnitState (the client's OWN spawn-state, op 0x69) is deliberately NOT sent here: in the
    /// monolith it is emitted by <c>CSSpawnCharacterPacket</c> (which the client sends AFTER select),
    /// not by the select block. In the distributed build that CSSpawnCharacter frame tunnels into the
    /// zone and dispatches against the real handler (B2.5) with ActiveChar already bound - so
    /// SCUnitState comes from that path, avoiding a double-send.
    ///
    /// Every step is individually guarded: the zone DI is a read-only subset, so a manager the
    /// monolith has but the zone does not must degrade to a logged skip, never abort the rest of the
    /// sequence nor the zone. Any skip is a concrete B2.6c coupling note.
    /// </summary>
    private void EmitCharEnterInitSequence(GameConnection connection, Character character)
    {
        logger.LogInformation("[B2.6c] zone emitting char-enter init sequence for {Name} objId={ObjId}",
            character.Name, character.ObjId);

        var step = 0;
        void Try(string name, Action action)
        {
            step++;
            try
            {
                action();
                logger.LogInformation("[B2.6c] init step {Step} OK: {Name}", step, name);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex,
                    "[B2.6c] init step {Step} SKIPPED: {Name} (manager/data missing in zone subset)",
                    step, name);
            }
        }

        // Match the monolith CSSelectCharacter success-branch ordering.
        Try("Simulation", () => character.Simulation = new AAEmu.Game.Models.Game.Units.Route.Simulation(character));

        Try("SCCharacterStatePacket", () => connection.SendPacket(new SCCharacterStatePacket(character)));
        Try("SCCharacterGamePointsPacket", () => connection.SendPacket(new SCCharacterGamePointsPacket(character)));
        Try("Inventory.Send", () => character.Inventory.Send());
        Try("SCActionSlotsPacket", () => connection.SendPacket(new SCActionSlotsPacket(character.Slots)));

        Try("Quests.Send", () => character.Quests.Send());
        Try("Quests.SendCompleted", () => character.Quests.SendCompleted());
        Try("Craft.SendLearnedCrafts", () => character.Craft.SendLearnedCrafts());

        Try("Actability.Send", () => character.Actability.Send());
        Try("Mails.SendUnreadMailCount", () => character.Mails.SendUnreadMailCount());
        Try("Appellations.Send", () => character.Appellations.Send());
        Try("Portals.Send", () => character.Portals.Send());
        Try("Friends.Send", () => character.Friends.Send());
        Try("Blocked.Send", () => character.Blocked.Send());

        Try("FactionManager.SendFactions", () => FactionManager.Instance.SendFactions(character));
        Try("FactionManager.SendRelations", () => FactionManager.Instance.SendRelations(character));
        Try("ExpeditionManager.SendExpeditions", () => ExpeditionManager.Instance.SendExpeditions(character));

        logger.LogInformation("[B2.6c] char-enter init sequence DONE for {Name} ({Steps} steps attempted). " +
            "SCUnitState (0x69) follows via tunneled CSSpawnCharacter.", character.Name, step);
    }
}
