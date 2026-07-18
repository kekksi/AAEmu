using System.Collections.Concurrent;

using AAEmu.Game.Core.Managers;
using AAEmu.Game.Models.Game.Char;
using AAEmu.Game.Models.Game.Skills.Effects;
using AAEmu.Game.Models.Game.Skills.Plots.Tree;
using AAEmu.Game.Models.Game.Skills.Templates;
using AAEmu.Game.Models.Game.Units;

namespace AAEmu.Game.Services.Telemetry;

internal enum SkillDamageResult
{
    Positive,
    Zero,
    Avoided,
    Immune,
    Skipped
}

internal sealed class SkillTelemetryState
{
    private static readonly ConcurrentDictionary<uint, bool> s_damageExpectation = new();

    private readonly DateTime _startedAt = DateTime.UtcNow;
    private readonly long _correlationId = TelemetryEmitter.NextCorrelationId();
    private readonly Character _caster;
    private readonly bool _expectsDamage;
    private readonly uint _skillId;
    private readonly uint? _initialTargetCharacterId;
    private readonly uint? _initialTargetObjectId;
    private readonly uint? _initialTargetTemplateId;
    private readonly uint? _plotId;
    private int _completed;
    private int _damageApplications;
    private int _positiveHits;
    private int _zeroHits;
    private int _avoidedHits;
    private int _immuneHits;
    private int _skippedHits;
    private long _totalDamage;
    private int _maxDamage;
    private string _zeroReason;

    private SkillTelemetryState(SkillTemplate template, Character caster, BaseUnit target)
    {
        _caster = caster;
        _skillId = template.Id;
        _expectsDamage = s_damageExpectation.GetOrAdd(template.Id, _ => ExpectsDirectDamage(template));
        _initialTargetCharacterId = target?.GetOwnerCharacter()?.Id;
        _initialTargetObjectId = target?.ObjId;
        _initialTargetTemplateId = target?.TemplateId;
        _plotId = template.Plot?.Id;
        TelemetryEmitter.TrackGameplay(caster, template.Id, target?.ObjId);
    }

    internal static SkillTelemetryState Begin(SkillTemplate template, BaseUnit caster, BaseUnit target)
    {
        try
        {
            var player = caster?.GetOwnerCharacter();
            return template == null || player == null ? null : new SkillTelemetryState(template, player, target);
        }
        catch
        {
            // A telemetry classification failure must not reject the cast.
            return null;
        }
    }

    internal void RecordDamage(SkillDamageResult result, int damage, string zeroReason = null)
    {
        Interlocked.Increment(ref _damageApplications);
        switch (result)
        {
            case SkillDamageResult.Positive:
                Interlocked.Increment(ref _positiveHits);
                Interlocked.Add(ref _totalDamage, damage);
                UpdateMaximum(ref _maxDamage, damage);
                break;
            case SkillDamageResult.Zero:
                Interlocked.Increment(ref _zeroHits);
                SetZeroReason(zeroReason ?? "computed_zero");
                break;
            case SkillDamageResult.Avoided:
                Interlocked.Increment(ref _avoidedHits);
                break;
            case SkillDamageResult.Immune:
                Interlocked.Increment(ref _immuneHits);
                break;
            case SkillDamageResult.Skipped:
                Interlocked.Increment(ref _skippedHits);
                SetZeroReason(zeroReason ?? "skipped");
                break;
        }
    }

    internal void Complete(string forcedOutcome = null, string zeroReason = null)
    {
        if (Interlocked.Exchange(ref _completed, 1) != 0)
            return;

        SetZeroReason(zeroReason);
        var outcome = forcedOutcome ?? DetermineOutcome(
            _expectsDamage,
            Volatile.Read(ref _damageApplications),
            Volatile.Read(ref _positiveHits),
            Volatile.Read(ref _zeroHits),
            Volatile.Read(ref _avoidedHits),
            Volatile.Read(ref _immuneHits),
            Volatile.Read(ref _skippedHits));
        var durationMs = Math.Max(0L, (long)(DateTime.UtcNow - _startedAt).TotalMilliseconds);

        TelemetryEmitter.TryEmit(new TelemetryEvent
        {
            OccurredAt = _startedAt,
            EventType = "skill_cast_result",
            AccountId = _caster.AccountId,
            CharacterId = _caster.Id,
            ActorCharacterId = _caster.Id,
            TargetCharacterId = _initialTargetCharacterId,
            SessionId = _caster.Connection?.Id,
            CorrelationId = _correlationId,
            ZoneId = _caster.Transform?.ZoneId,
            WorldId = _caster.Transform?.WorldId,
            SkillId = _skillId,
            Outcome = outcome,
            ValueInt = Interlocked.Read(ref _totalDamage),
            Payload = new
            {
                duration_ms = durationMs,
                expected_damage = _expectsDamage,
                damage_applications = Volatile.Read(ref _damageApplications),
                positive_hits = Volatile.Read(ref _positiveHits),
                zero_hits = Volatile.Read(ref _zeroHits),
                avoided_hits = Volatile.Read(ref _avoidedHits),
                immune_hits = Volatile.Read(ref _immuneHits),
                skipped_hits = Volatile.Read(ref _skippedHits),
                total_damage = Interlocked.Read(ref _totalDamage),
                max_damage = Volatile.Read(ref _maxDamage),
                zero_reason = Volatile.Read(ref _zeroReason),
                initial_target_obj_id = _initialTargetObjectId,
                initial_target_template_id = _initialTargetTemplateId,
                plot_id = _plotId
            }
        }, TelemetryPriority.Bulk);
    }

    internal static string DetermineOutcome(
        bool expectsDamage,
        int damageApplications,
        int positiveHits,
        int zeroHits,
        int avoidedHits,
        int immuneHits,
        int skippedHits)
    {
        var anomalous = zeroHits + avoidedHits + immuneHits + skippedHits;

        if (positiveHits > 0)
            return anomalous > 0 ? "mixed" : "damage";
        if (zeroHits > 0 || skippedHits > 0)
            return "zero";
        if (avoidedHits > 0)
            return "avoided";
        if (immuneHits > 0)
            return "immune";
        if (expectsDamage && damageApplications == 0)
            return "no_damage_effect";
        return "utility";
    }

    private static bool ExpectsDirectDamage(SkillTemplate template)
    {
        if (template.Effects.Any(effect => effect.Template is DamageEffect))
            return true;

        var root = template.Plot?.Tree?.RootNode;
        if (root == null)
            return false;

        var nodes = new Stack<PlotNode>();
        nodes.Push(root);
        while (nodes.Count > 0)
        {
            var node = nodes.Pop();
            if (node.Event?.Effects.Any(effect =>
                    SkillManager.Instance.GetEffectTemplate(effect.ActualId, effect.ActualType) is DamageEffect) == true)
                return true;

            foreach (var child in node.Children)
                nodes.Push(child);
        }

        return false;
    }

    private static void UpdateMaximum(ref int location, int value)
    {
        var current = Volatile.Read(ref location);
        while (value > current)
        {
            var observed = Interlocked.CompareExchange(ref location, value, current);
            if (observed == current)
                return;
            current = observed;
        }
    }

    private void SetZeroReason(string zeroReason)
    {
        if (!string.IsNullOrWhiteSpace(zeroReason))
            Interlocked.CompareExchange(ref _zeroReason, zeroReason, null);
    }
}
