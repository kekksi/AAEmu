using AAEmu.Game.Core.Managers;
using AAEmu.Game.Core.Managers.World;
using AAEmu.Game.Core.Packets.G2C;
using AAEmu.Game.Models.Game;
using AAEmu.Game.Models.Game.Char;
using AAEmu.Game.Utils.Scripts;

namespace AAEmu.Game.Scripts.Commands;

// Bring a player to you.
public class TpPlayer : ICommand
{
    public string[] CommandNames { get; set; } = ["tpplayer", "summon", "bring"];
    public void OnLoad() { CommandManager.Instance.Register(CommandNames, this); }
    public string GetCommandLineHelp() { return "<playerName>"; }
    public string GetCommandHelpText() { return "Teleports the named online player to your position."; }

    public void Execute(Character character, string[] args, IMessageOutput messageOutput)
    {
        if (args.Length < 1) { CommandManager.SendDefaultHelpText(this, messageOutput); return; }
        var target = WorldManager.Instance.GetCharacter(args[0]);
        if (target == null) { CommandManager.SendErrorText(this, messageOutput, $"Player \"{args[0]}\" is not online."); return; }
        var p = character.Transform.World.Position;
        target.ForceDismount();
        target.DisabledSetPosition = true;
        target.SendPacket(new SCTeleportUnitPacket(0, 0, p.X, p.Y, p.Z, 0));
        CommandManager.SendNormalText(this, messageOutput, $"Brought {target.Name} to you.");
        target.SendMessage($"[GM] You were summoned by {character.Name}.");
    }
}

// Teleport yourself to a player.
public class GotoPlayer : ICommand
{
    public string[] CommandNames { get; set; } = ["gotoplayer", "goto", "tpto"];
    public void OnLoad() { CommandManager.Instance.Register(CommandNames, this); }
    public string GetCommandLineHelp() { return "<playerName>"; }
    public string GetCommandHelpText() { return "Teleports you to the named online player."; }

    public void Execute(Character character, string[] args, IMessageOutput messageOutput)
    {
        if (args.Length < 1) { CommandManager.SendDefaultHelpText(this, messageOutput); return; }
        var target = WorldManager.Instance.GetCharacter(args[0]);
        if (target == null) { CommandManager.SendErrorText(this, messageOutput, $"Player \"{args[0]}\" is not online."); return; }
        var p = target.Transform.World.Position;
        character.ForceDismount();
        character.DisabledSetPosition = true;
        character.SendPacket(new SCTeleportUnitPacket(0, 0, p.X, p.Y, p.Z, 0));
        CommandManager.SendNormalText(this, messageOutput, $"Teleported you to {target.Name}.");
    }
}
