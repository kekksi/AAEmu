using AAEmu.Game.Core.Managers;
using AAEmu.Game.Models.Game.Char;
using AAEmu.Game.Models.Game.Units;

namespace AAEmu.Game.Models.Game.Skills.Effects.SpecialEffects;

/// <summary>
/// SpecialEffect linked with adding charges to a buff.
/// </summary>
public class Charge : SpecialEffectAction
{
    protected override SpecialType SpecialEffectActionType => SpecialType.Charge;

    public override void Execute(BaseUnit caster, SkillCaster casterObj, BaseUnit target, SkillCastTarget targetObj,
        CastAction castObj,
        Skill skill, SkillObject skillObject, DateTime time, int buffId, int minCharge, int maxCharge, int unused)
    {
        if (caster is Character) { Logger.Debug("Special effects: Charge buffId {0}, minCharge {1}, maxCharge {2}, unused {3}", buffId, minCharge, maxCharge, unused); }

        // Charges belong to the unit receiving the special effect. Source effects
        // happen to target the caster, while target effects must store and update
        // their charge on the affected unit.
        lock (target.ChargeLock)
        {
            var buff = target.Buffs.GetEffectFromBuffId((uint)buffId);
            var template = SkillManager.Instance.GetBuffTemplate((uint)buffId);

            // Some skills (e.g. Concussive Arrow) use a fixed charge amount and
            // therefore provide identical minimum and maximum values. Random.Next
            // requires an exclusive upper bound, so that case must not be sampled.
            var chargeDelta = minCharge == maxCharge
                ? minCharge
                : Random.Shared.Next(Math.Min(minCharge, maxCharge), Math.Max(minCharge, maxCharge));
            var oldCharge = buff?.Charge ?? 0;

            var newEffect =
                new Buff(target, caster, casterObj, template, skill, time)
                {
                    Charge = Math.Min(chargeDelta, template.MaxCharge)
                };

            target.Buffs.AddBuff(newEffect, buff?.Index ?? 0);

            var newCharge = Math.Min(oldCharge + chargeDelta, template.MaxCharge);
            newEffect.Charge = newCharge;
        }
    }
}
