namespace AAEmu.Game.Services.Telemetry;

internal enum TelemetryPriority
{
    Bulk,
    Critical
}

internal sealed record TelemetryEvent
{
    public long SequenceNo { get; init; }
    public DateTime OccurredAt { get; init; } = DateTime.UtcNow;
    public required string EventType { get; init; }
    public byte Severity { get; init; }
    public uint? AccountId { get; init; }
    public uint? CharacterId { get; init; }
    public uint? ActorCharacterId { get; init; }
    public uint? TargetCharacterId { get; init; }
    public uint? SessionId { get; init; }
    public long? CorrelationId { get; init; }
    public uint? ZoneId { get; init; }
    public uint? WorldId { get; init; }
    public uint? SkillId { get; init; }
    public uint? QuestId { get; init; }
    public uint? QuestActId { get; init; }
    public uint? ItemId { get; init; }
    public string Outcome { get; init; }
    public long? ValueInt { get; init; }
    public byte[] Fingerprint { get; init; }
    public object Payload { get; init; }
}

internal sealed record StoredTelemetryEvent
{
    public required string ServerName { get; init; }
    public required string ServerBuild { get; init; }
    public Guid ServerBootId { get; init; }
    public long SequenceNo { get; init; }
    public DateTime OccurredAt { get; init; }
    public required string EventType { get; init; }
    public byte Severity { get; init; }
    public uint? AccountId { get; init; }
    public uint? CharacterId { get; init; }
    public uint? ActorCharacterId { get; init; }
    public uint? TargetCharacterId { get; init; }
    public uint? SessionId { get; init; }
    public long? CorrelationId { get; init; }
    public uint? ZoneId { get; init; }
    public uint? WorldId { get; init; }
    public uint? SkillId { get; init; }
    public uint? QuestId { get; init; }
    public uint? QuestActId { get; init; }
    public uint? ItemId { get; init; }
    public string Outcome { get; init; }
    public long? ValueInt { get; init; }
    public byte[] Fingerprint { get; init; }
    public string PayloadJson { get; init; }
}

internal readonly record struct TelemetryHealthSnapshot(
    long EnqueuedCritical,
    long EnqueuedBulk,
    long DroppedCritical,
    long DroppedBulk,
    long Written,
    long Spooled,
    long Replayed,
    long DatabaseFailures,
    long SpoolFailures,
    int CriticalQueueDepth,
    int BulkQueueDepth);
