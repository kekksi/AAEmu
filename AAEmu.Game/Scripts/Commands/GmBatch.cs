using System.Drawing;
using System.Globalization;
using AAEmu.Game.Core.Managers;
using AAEmu.Game.Core.Managers.UnitManagers;
using AAEmu.Game.Core.Managers.World;
using AAEmu.Game.Core.Packets.G2C;
using AAEmu.Game.Models.Game;
using AAEmu.Game.Models.Game.Char;
using AAEmu.Game.Models.Game.NPChar;
using AAEmu.Game.Models.Game.Units.Static;
using AAEmu.Game.Utils.Scripts;

namespace AAEmu.Game.Scripts.Commands;

// Fully heal all online players.
public class HealAll : ICommand
{
    public string[] CommandNames { get; set; } = ["healall"];
    public void OnLoad() { CommandManager.Instance.Register(CommandNames, this); }
    public string GetCommandLineHelp() { return ""; }
    public string GetCommandHelpText() { return "Fully heals every online player (HP + MP)."; }

    public void Execute(Character character, string[] args, IMessageOutput messageOutput)
    {
        var n = 0;
        foreach (var c in WorldManager.Instance.GetAllCharacters())
        {
            if (c == null || c.Hp == 0) continue;
            var old = c.Hp;
            c.Hp = c.MaxHp;
            c.Mp = c.MaxMp;
            c.BroadcastPacket(new SCUnitPointsPacket(c.ObjId, c.Hp, c.Mp), true);
            c.PostUpdateCurrentHp(c, old, c.Hp, KillReason.Unknown);
            n++;
        }
        CommandManager.SendNormalText(this, messageOutput, $"Healed {n} online players.");
    }
}

// Remove all spawned NPCs within a radius of you.
public class ClearArea : ICommand
{
    public string[] CommandNames { get; set; } = ["cleararea", "clearnpcs"];
    public void OnLoad() { CommandManager.Instance.Register(CommandNames, this); }
    public string GetCommandLineHelp() { return "[radius=30]"; }
    public string GetCommandHelpText() { return "Removes all NPCs within [radius] metres of you (default 30)."; }

    public void Execute(Character character, string[] args, IMessageOutput messageOutput)
    {
        var radius = 30f;
        if (args.Length > 0 && float.TryParse(args[0], NumberStyles.Any, CultureInfo.InvariantCulture, out var r))
            radius = r;
        var npcs = WorldManager.GetAround<Npc>(character, radius);
        var n = 0;
        foreach (var npc in npcs)
        {
            if (npc == null) continue;
            npc.Delete();
            n++;
        }
        CommandManager.SendNormalText(this, messageOutput, $"Removed {n} NPCs within {radius}m.");
    }
}

// Spawn an NPC at your position and announce it server-wide (world-boss style).
public class Boss : ICommand
{
    public string[] CommandNames { get; set; } = ["boss"];
    public void OnLoad() { CommandManager.Instance.Register(CommandNames, this); }
    public string GetCommandLineHelp() { return "<npcId>"; }
    public string GetCommandHelpText() { return "Spawns <npcId> at your position and broadcasts a server-wide notice."; }

    public void Execute(Character character, string[] args, IMessageOutput messageOutput)
    {
        if (args.Length < 1) { CommandManager.SendDefaultHelpText(this, messageOutput); return; }
        if (!uint.TryParse(args[0], out var unitId)) { CommandManager.SendErrorText(this, messageOutput, "<npcId> parse error"); return; }
        if (!NpcManager.Instance.Exist(unitId)) { CommandManager.SendErrorText(this, messageOutput, $"NPC {unitId} does not exist"); return; }

        using var charPos = character.Transform.CloneDetached();
        var npcSpawner = new NpcSpawner { ParentWorld = character.ParentWorld, Id = 0, UnitId = unitId };
        npcSpawner.Position = charPos.CloneAsSpawnPosition();
        npcSpawner.Position.Yaw = 0;
        npcSpawner.Position.Pitch = 0;
        npcSpawner.Position.Roll = 0;
        character.ParentWorld.SpawnManager.AddNpcSpawner(npcSpawner);
        npcSpawner.SpawnAll();

        WorldManager.Instance.BroadcastPacketToServer(new SCNoticeMessagePacket(3, Color.OrangeRed, 0, "A powerful boss has appeared!"));
        CommandManager.SendNormalText(this, messageOutput, $"Boss {unitId} spawned and announced.");
    }
}
