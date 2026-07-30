using AAEmu.Commons.Network.Cluster;
using AAEmu.Commons.Network.Cluster.Packets;
using AAEmu.Game.Models;
using NLog;

namespace AAEmu.Game.Core.Network.Cluster;

/// <summary>
/// Gateway-side handler: validates the zone secret key (same key as the GL game-server
/// register), stores the connection in the <see cref="ZoneRegistry"/> and replies.
/// </summary>
public class ZGRegisterZonePacketHandler : IClusterPacketHandler<ZGRegisterZonePacket>
{
    private static Logger Logger { get; } = LogManager.GetCurrentClassLogger();

    // Synthetic connection id used only by the tunnel self-test (no live client socket).
    private const uint SelfTestConnId = 999;

    // B2.6b character-handoff self-test: a distinct synthetic connection id + the seeded test
    // account/character in the shared DB (created via SQL, see report). Sending a GZEnterZone for
    // these makes the zone load + spawn a REAL character into its own WorldInstance - the B2.6b proof.
    private const uint HandoffTestConnId = 997;
    private const uint TestAccountId = 9001;
    private const uint TestCharacterId = 9001;

    public void Execute(ZGRegisterZonePacket packet, ClusterConnection connection)
    {
        if (packet.SecretKey != AppConfiguration.Instance.SecretKey)
        {
            Logger.Error("Zone register from {0} rejected: bad secret key", connection.Ip);
            connection.SendPacket(new GZRegisterResultPacket(false, "bad secret key"));
            return;
        }

        connection.AddAttribute("zoneId", packet.ZoneId);
        ZoneRegistry.Instance.Add(packet.ZoneId, connection);
        Logger.Info("Zone {0} registered (worldTemplate {1}) from {2}",
            packet.ZoneId, packet.WorldTemplateId, connection.Ip);
        connection.SendPacket(new GZRegisterResultPacket(true, $"zone {packet.ZoneId} registered"));

        // B2.5 self-test: prove the client-packet tunnel dispatches against the REAL zone handler.
        // We send a genuine level-2 Ping frame (op 0x0012) with a 20-byte body (tPhy:i64, ping:i64,
        // local:u32). In the zone GZClientPacketHandler routes it into the real GameProtocolHandler,
        // the real PingPacket handler runs and replies with a Pong (op 0x0013) which tunnels back
        // here as a ZGClientPacket - the 0x0012 -> 0x0013 opcode change is proof the real handler
        // ran (a plain echo could never turn a Ping into a Pong).
        if (AppConfiguration.Instance.ClusterNetwork?.TunnelSelfTest == true)
        {
            // B2.5 handoff hook: register ownership of the (synthetic) test connection with the zone
            // that just came up. This exercises ClientRoutingTable.SetOwner on the gateway. The real
            // EnterWorld-driven ownership hand-off is B2.6a.
            ClientRoutingTable.Instance.SetOwner(SelfTestConnId, packet.ZoneId);
            Logger.Info("[B2.5] gateway SetOwner conn={0} -> zone {1} (owners now={2})",
                SelfTestConnId, packet.ZoneId, ClientRoutingTable.Instance.Count);

            var pingBody = new byte[20]; // tPhy(8) + ping(8) + local(4), zero-filled is valid
            var frame = ClientFrameCodec.BuildClientFrame(0x0012, level: 2, body: pingBody);
            var sent = ZoneRegistry.Instance.ForwardClientPacket(packet.ZoneId, SelfTestConnId, frame);
            Logger.Info("[B2.5] gateway sent Ping GZClientPacket conn={0} op=0x0012 to zone {1} (queued={2})",
                SelfTestConnId, packet.ZoneId, sent);

            RunOwnershipStateMachineProof(packet.ZoneId);
            RunCharacterHandoffProof(packet.ZoneId);
        }
    }

    /// <summary>
    /// B2.6b synthetic character hand-off proof (self-test only). Sends a GZEnterZonePacket for the
    /// seeded test account/character to the zone that just registered. This is the exact packet the
    /// gateway sends from ClientOwnershipHandoff.TryEnterZone at the start of CSSelectCharacter for a
    /// zone-owned character - but here it is driven synthetically, with no live client. The zone
    /// GZEnterZonePacketHandler then loads the character straight from the shared DB and spawns it
    /// into its own WorldInstance, which its [B2.6b] log line proves.
    /// </summary>
    private static void RunCharacterHandoffProof(uint zoneId)
    {
        var zoneCon = ZoneRegistry.Instance.Get(zoneId);
        if (zoneCon == null)
        {
            Logger.Warn("[B2.6b] handoff self-test: zone {0} not registered", zoneId);
            return;
        }

        zoneCon.SendPacket(new GZEnterZonePacket(HandoffTestConnId, TestAccountId, TestCharacterId));
        Logger.Info("[B2.6b] gateway sent GZEnterZone conn={0} account={1} char={2} to zone {3} (synthetic hand-off)",
            HandoffTestConnId, TestAccountId, TestCharacterId, zoneId);
    }

    /// <summary>
    /// B2.6a synthetic proof of the EnterWorld ownership state machine, run once a zone registers
    /// (self-test only). It exercises the EXACT routing decision that
    /// <c>GameProtocolHandler.OnReceive</c> makes per frame:
    ///   1. no owner        -> TryGetOwner == false -> frame would DISPATCH LOCALLY (monolith path)
    ///   2. TryClaim (=SetOwner, as fired at end of CSSelectCharacter) -> TryGetOwner == true
    ///      -> frame is TUNNELED via ZoneRegistry.ForwardClientPacket to the owning zone
    ///   3. ClearOwner (as fired in GameProtocolHandler.OnDisconnect) -> TryGetOwner == false
    ///      -> frame would DISPATCH LOCALLY again
    /// A distinct synthetic connection id (998) is used so it never collides with a real client or
    /// the B2.5 Ping self-test connection (999).
    /// </summary>
    private static void RunOwnershipStateMachineProof(uint zoneId)
    {
        const uint proofConn = 998;
        var frame = ClientFrameCodec.BuildClientFrame(0x0012, level: 2, body: new byte[20]);

        // Phase 1: no owner -> local dispatch
        var owned0 = ClientRoutingTable.Instance.TryGetOwner(proofConn, out _);
        Logger.Info("[B2.6a-proof] phase1 conn={0} owned={1} -> route=LOCAL (monolith dispatch)", proofConn, owned0);

        // Phase 2: simulate the CSSelectCharacter hand-off, then route a follow-up frame
        ClientRoutingTable.Instance.SetOwner(proofConn, zoneId);
        var owned1 = ClientRoutingTable.Instance.TryGetOwner(proofConn, out var ownerZone);
        if (owned1)
        {
            var tunneled = ZoneRegistry.Instance.ForwardClientPacket(ownerZone, proofConn, frame);
            Logger.Info("[B2.6a-proof] phase2 conn={0} owned={1} zone={2} -> route=TUNNEL (ForwardClientPacket queued={3})",
                proofConn, owned1, ownerZone, tunneled);
        }

        // Phase 3: simulate disconnect ClearOwner -> back to local dispatch
        ClientRoutingTable.Instance.ClearOwner(proofConn);
        var owned2 = ClientRoutingTable.Instance.TryGetOwner(proofConn, out _);
        Logger.Info("[B2.6a-proof] phase3 conn={0} owned={1} -> route=LOCAL again (ownership cleared)", proofConn, owned2);
    }
}
