using AAEmu.Game.Core.Managers;
using AAEmu.Game.Models.Game;
using AAEmu.Game.Models.Game.Char;
using AAEmu.Game.Models.Game.Skills;
using AAEmu.Game.Utils.Scripts;

namespace AAEmu.Game.Scripts.Commands;

// Learn every tree skill of your 3 abilities up to your current ability level.
public class MaxSkills : ICommand
{
    public string[] CommandNames { get; set; } = ["maxskills", "learnall"];
    public void OnLoad() { CommandManager.Instance.Register(CommandNames, this); }
    public string GetCommandLineHelp() { return ""; }
    public string GetCommandHelpText() { return "Learns all skills of your 3 chosen abilities (up to your ability level)."; }

    public void Execute(Character character, string[] args, IMessageOutput messageOutput)
    {
        var n = 0;
        foreach (var ab in new[] { character.Ability1, character.Ability2, character.Ability3 })
        {
            if (ab == AbilityType.None) continue;
            var lvl = character.GetAbLevel(ab);
            foreach (var st in SkillManager.Instance.GetSkillsByAbility(ab, lvl))
            {
                if (character.Skills.Skills.ContainsKey(st.Id)) continue;
                character.Skills.AddSkill(st, 1, true);
                n++;
            }
        }
        CommandManager.SendNormalText(this, messageOutput, $"Learned {n} skills across your 3 abilities.");
    }
}
