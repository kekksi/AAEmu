using AAEmu.Game.Models.Game.Skills.Templates;
using AAEmu.Game.Models.Game.Units;

namespace AAEmu.Game.Models.Game.Skills.Effects;

public class EffectSource
{
    public Skill Skill { get; set; }
    public BuffTemplate Buff { get; set; }
    public Unit Caster { get; set; }
    public int Amount { get; set; }
    public bool IsTrigger { get; set; }

    public EffectSource()
    {
    }

    public EffectSource(Unit caster)
    {
        Caster = caster;
    }

    public EffectSource(Skill skill, Unit caster = null)
    {
        Skill = skill;
        Caster = caster;
    }

    public EffectSource(BuffTemplate buff, Unit caster = null)
    {
        Buff = buff;
        Caster = caster;
    }

    public EffectSource(Skill skill, BuffTemplate buff, Unit caster = null)
    {
        Skill = skill;
        Buff = buff;
        Caster = caster;
    }
}
