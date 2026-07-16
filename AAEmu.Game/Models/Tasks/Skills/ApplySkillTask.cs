using AAEmu.Game.Models.Game.Skills;
using AAEmu.Game.Models.Game.Units;
using NLog;

namespace AAEmu.Game.Models.Tasks.Skills;

public class ApplySkillTask(
    Skill skill,
    BaseUnit caster,
    SkillCaster casterCaster,
    BaseUnit target,
    SkillCastTarget targetCaster,
    SkillObject skillObject)
    : Task
{
    private static Logger Logger { get; } = LogManager.GetCurrentClassLogger();

    public override void Execute()
    {
        try
        {
            skill.ApplyEffects(caster, casterCaster, target, targetCaster, skillObject);
        }
        catch (Exception e)
        {
            // TaskManager runs delayed effects in a fire-and-forget task. Without
            // this guard an effect exception skips EndSkill and leaves the client
            // in its casting/attack state indefinitely.
            Logger.Error(e, "Failed to apply delayed effects for skill {0} (tlId {1}, caster {2})",
                skill.Template?.Id, skill.TlId, caster?.ObjId);
        }
        finally
        {
            skill.EndSkill(caster);
        }
    }
}
