using System.Collections.Concurrent;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;

using AAEmu.Game.Models.Game.Char;

namespace AAEmu.Game.Services.Telemetry;

internal interface ITelemetrySink
{
    bool TryEmit(TelemetryEvent telemetryEvent, TelemetryPriority priority);
}

internal sealed record TelemetryGameplayContext(
    DateTime UpdatedAt,
    uint AccountId,
    uint CharacterId,
    uint? SessionId,
    uint? ZoneId,
    uint? WorldId,
    uint? SkillId,
    uint? TargetObjectId);

internal static class TelemetryEmitter
{
    private static readonly ConcurrentDictionary<uint, TelemetryGameplayContext> s_gameplayContexts = new();
    private static ITelemetrySink s_sink;
    private static long s_correlationId;

    internal static long NextCorrelationId() => Interlocked.Increment(ref s_correlationId);

    internal static void SetSink(ITelemetrySink sink) => Volatile.Write(ref s_sink, sink);

    internal static bool TryEmit(TelemetryEvent telemetryEvent, TelemetryPriority priority)
    {
        try
        {
            return Volatile.Read(ref s_sink)?.TryEmit(telemetryEvent, priority) ?? false;
        }
        catch
        {
            // Telemetry must never affect gameplay or exception handling.
            return false;
        }
    }

    internal static void TrackGameplay(Character character, uint? skillId = null, uint? targetObjectId = null)
    {
        try
        {
            if (character == null)
                return;

            var context = new TelemetryGameplayContext(
                DateTime.UtcNow,
                character.AccountId,
                character.Id,
                character.Connection?.Id,
                character.Transform?.ZoneId,
                character.Transform?.WorldId,
                skillId,
                targetObjectId);
            s_gameplayContexts.AddOrUpdate(
                character.Id,
                context,
                (_, previous) => context with
                {
                    SkillId = skillId ?? previous.SkillId,
                    TargetObjectId = targetObjectId ?? previous.TargetObjectId
                });
        }
        catch
        {
            // Context enrichment is best-effort only.
        }
    }

    internal static void EmitSession(Character character, uint sessionId, string outcome, string reason = null)
    {
        try
        {
            if (character == null)
                return;

            TrackGameplay(character);
            var context = s_gameplayContexts.GetValueOrDefault(character.Id);
            TryEmit(new TelemetryEvent
            {
                EventType = "session",
                Severity = outcome == "unexpected_disconnect" ? (byte)2 : (byte)0,
                AccountId = character.AccountId,
                CharacterId = character.Id,
                SessionId = sessionId,
                ZoneId = character.Transform?.ZoneId,
                WorldId = character.Transform?.WorldId,
                SkillId = context?.SkillId,
                Outcome = outcome,
                Payload = new
                {
                    reason,
                    character_name = character.Name,
                    is_in_battle = character.IsInBattle,
                    last_skill_id = context?.SkillId,
                    last_target_obj_id = context?.TargetObjectId,
                    last_packet_at = character.LastPacketActivityTime == default
                        ? null
                        : character.LastPacketActivityTime.ToUniversalTime().ToString("O")
                }
            }, outcome == "login" ? TelemetryPriority.Bulk : TelemetryPriority.Critical);
        }
        catch
        {
            // Session lifecycle must continue even when enrichment fails.
        }
    }

    internal static void EmitException(
        Exception exception,
        bool handled,
        string source,
        Character character = null,
        uint? skillId = null,
        uint? targetObjectId = null,
        bool isTerminating = false)
    {
        try
        {
            exception ??= new InvalidOperationException("Unknown exception object");
            if (exception is AggregateException aggregate)
            {
                var flattened = aggregate.Flatten();
                if (flattened.InnerExceptions.Count == 1)
                    exception = flattened.InnerExceptions[0];
            }
            var context = ResolveContext(character, skillId, targetObjectId);
            var fingerprint = CreateExceptionFingerprint(exception);

            TryEmit(new TelemetryEvent
            {
                EventType = "exception",
                Severity = handled ? (byte)2 : (byte)3,
                AccountId = context?.AccountId,
                CharacterId = context?.CharacterId,
                SessionId = context?.SessionId,
                ZoneId = context?.ZoneId,
                WorldId = context?.WorldId,
                SkillId = skillId ?? context?.SkillId,
                Outcome = handled ? "handled" : "unhandled",
                Fingerprint = fingerprint,
                Payload = new
                {
                    source,
                    is_terminating = isTerminating,
                    exception_type = exception.GetType().FullName,
                    message = Truncate(exception.Message, 2048),
                    stack_trace = Truncate(exception.ToString(), 16384),
                    last_gameplay_context = context == null ? null : new
                    {
                        context.UpdatedAt,
                        context.CharacterId,
                        context.SkillId,
                        context.ZoneId,
                        context.WorldId,
                        context.TargetObjectId
                    }
                }
            }, TelemetryPriority.Critical);
        }
        catch
        {
            // This path is also called from fatal exception handlers.
        }
    }

    internal static byte[] CreateExceptionFingerprint(Exception exception)
    {
        var builder = new StringBuilder(exception.GetType().FullName);
        var frames = new StackTrace(exception, false).GetFrames();
        if (frames != null)
        {
            foreach (var frame in frames.Take(8))
            {
                var method = frame.GetMethod();
                builder.Append('|')
                    .Append(method?.DeclaringType?.FullName)
                    .Append('.')
                    .Append(method?.Name);
            }
        }

        return SHA256.HashData(Encoding.UTF8.GetBytes(builder.ToString()));
    }

    private static TelemetryGameplayContext ResolveContext(Character character, uint? skillId, uint? targetObjectId)
    {
        if (character != null)
        {
            TrackGameplay(character, skillId, targetObjectId);
            return s_gameplayContexts.GetValueOrDefault(character.Id);
        }

        return s_gameplayContexts.Values
            .Where(x => x.UpdatedAt >= DateTime.UtcNow.AddMinutes(-5))
            .MaxBy(x => x.UpdatedAt);
    }

    private static string Truncate(string value, int maxLength)
    {
        if (string.IsNullOrEmpty(value) || value.Length <= maxLength)
            return value;
        return value[..maxLength];
    }
}
