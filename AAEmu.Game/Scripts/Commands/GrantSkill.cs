using AAEmu.Game.Core.Managers;
using AAEmu.Game.Core.Managers.World;
using AAEmu.Game.Core.Packets.G2C;
using AAEmu.Game.Models.Game;
using AAEmu.Game.Models.Game.Char;
using AAEmu.Game.Utils.Scripts;

namespace AAEmu.Game.Scripts.Commands;

public class GrantSkill : ICommand
{
    public string[] CommandNames { get; set; } = ["grantskill", "learnskill", "grant_skill"];

    public void OnLoad()
    {
        CommandManager.Instance.Register(CommandNames, this);
    }

    public string GetCommandLineHelp()
    {
        return "(target) <skillId>";
    }

    public string GetCommandHelpText()
    {
        return "Force-grants a skill to the target (or self). The skill appears in the K skill window, drag it to a hotbar slot. Bypasses tree/skill-point checks (for 2.0 skills with no 1.2 tree node).";
    }

    public void Execute(Character character, string[] args, IMessageOutput messageOutput)
    {
        if (args.Length == 0)
        {
            CommandManager.SendDefaultHelpText(this, messageOutput);
            return;
        }

        var targetPlayer = WorldManager.Instance.GetTargetOrSelf(character, args[0], out var firstArg);
        if (targetPlayer == null)
        {
            CommandManager.SendErrorText(this, messageOutput, "No target.");
            return;
        }

        if (args.Length <= firstArg || !uint.TryParse(args[firstArg], out var skillId))
        {
            CommandManager.SendErrorText(this, messageOutput, "Missing/invalid skillId.");
            return;
        }

        var template = SkillManager.Instance.GetSkillTemplate(skillId);
        if (template == null)
        {
            CommandManager.SendErrorText(this, messageOutput, $"Unknown skillId {skillId}.");
            return;
        }

        if (targetPlayer.Skills.Skills.TryGetValue(skillId, out var existing))
        {
            targetPlayer.SendPacket(new SCSkillLearnedPacket(existing));
            CommandManager.SendNormalText(this, messageOutput, $"{targetPlayer.Name} already knows skill {skillId} - resent to client.");
            return;
        }

        targetPlayer.Skills.AddSkill(template, 1, true);
        CommandManager.SendNormalText(this, messageOutput, $"Granted skill {skillId} to {targetPlayer.Name}. Open K, drag it to a hotbar slot.");
        if (character.Id != targetPlayer.Id)
            targetPlayer.SendMessage($"[GM] {character.Name} granted you skill {skillId}.");
    }
}
