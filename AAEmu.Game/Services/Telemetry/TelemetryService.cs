using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Channels;

using AAEmu.Commons.IO;
using AAEmu.Commons.Utils.DB;

using Microsoft.Extensions.Hosting;

using MySql.Data.MySqlClient;

using NLog;

namespace AAEmu.Game.Services.Telemetry;

public sealed class TelemetryService : IHostedService, ITelemetrySink, IDisposable
{
    private const int BatchSize = 250;
    private static readonly TimeSpan FlushInterval = TimeSpan.FromMilliseconds(500);
    private static readonly TimeSpan ReplayInterval = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan HealthInterval = TimeSpan.FromMinutes(1);
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    private readonly Channel<TelemetryEvent> _criticalQueue = Channel.CreateBounded<TelemetryEvent>(
        new BoundedChannelOptions(4096)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true,
            SingleWriter = false
        });
    private readonly Channel<TelemetryEvent> _bulkQueue = Channel.CreateBounded<TelemetryEvent>(
        new BoundedChannelOptions(32768)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true,
            SingleWriter = false
        });
    private readonly CancellationTokenSource _stopSource = new();
    private readonly TaskCompletionSource _databaseReady = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly Guid _serverBootId = Guid.NewGuid();
    private readonly string _serverName = Truncate(Environment.MachineName, 24);
    private readonly string _serverBuild = ResolveServerBuild();
    private readonly string _spoolDirectory = Path.Combine(FileManager.AppPath, "Logs", "telemetry-spool");

    private Task _worker;
    private bool _canUseDatabase;
    private int _started;
    private long _sequenceNo;
    private long _enqueuedCritical;
    private long _enqueuedBulk;
    private long _droppedCritical;
    private long _droppedBulk;
    private long _written;
    private long _spooled;
    private long _replayed;
    private long _databaseFailures;
    private long _spoolFailures;
    private int _criticalQueueDepth;
    private int _bulkQueueDepth;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (Interlocked.Exchange(ref _started, 1) != 0)
            return Task.CompletedTask;

        try
        {
            Directory.CreateDirectory(_spoolDirectory);
        }
        catch (Exception exception)
        {
            Logger.Warn(exception, "Telemetry spool directory is unavailable; database writes will still be attempted");
        }
        TelemetryEmitter.SetSink(this);
        _worker = Task.Run(() => RunAsync(_stopSource.Token), CancellationToken.None);
        Logger.Info("Telemetry queue started (boot {0}, spool {1})", _serverBootId, _spoolDirectory);
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        TelemetryEmitter.SetSink(null);
        _criticalQueue.Writer.TryComplete();
        _bulkQueue.Writer.TryComplete();
        _databaseReady.TrySetResult();

        if (_worker == null)
            return;

        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(5));
            await _worker.WaitAsync(timeout.Token);
        }
        catch (OperationCanceledException)
        {
            _stopSource.Cancel();
            try
            {
                await _worker;
            }
            catch (OperationCanceledException)
            {
                // The worker spools its remaining in-memory batch in its finally block.
            }
        }

        Logger.Info("Telemetry queue stopped: {0}", GetHealthSnapshot());
    }

    public void MarkDatabaseReady()
    {
        _canUseDatabase = true;
        _databaseReady.TrySetResult();
    }

    internal TelemetryHealthSnapshot GetHealthSnapshot() => new(
        Interlocked.Read(ref _enqueuedCritical),
        Interlocked.Read(ref _enqueuedBulk),
        Interlocked.Read(ref _droppedCritical),
        Interlocked.Read(ref _droppedBulk),
        Interlocked.Read(ref _written),
        Interlocked.Read(ref _spooled),
        Interlocked.Read(ref _replayed),
        Interlocked.Read(ref _databaseFailures),
        Interlocked.Read(ref _spoolFailures),
        Volatile.Read(ref _criticalQueueDepth),
        Volatile.Read(ref _bulkQueueDepth));

    bool ITelemetrySink.TryEmit(TelemetryEvent telemetryEvent, TelemetryPriority priority)
    {
        if (telemetryEvent == null || Volatile.Read(ref _started) == 0)
            return false;

        telemetryEvent = telemetryEvent with { SequenceNo = Interlocked.Increment(ref _sequenceNo) };
        if (priority == TelemetryPriority.Critical)
        {
            Interlocked.Increment(ref _criticalQueueDepth);
            var queued = _criticalQueue.Writer.TryWrite(telemetryEvent);
            if (queued)
                Interlocked.Increment(ref _enqueuedCritical);
            else
            {
                Interlocked.Decrement(ref _criticalQueueDepth);
                Interlocked.Increment(ref _droppedCritical);
            }
            return queued;
        }

        Interlocked.Increment(ref _bulkQueueDepth);
        var bulkQueued = _bulkQueue.Writer.TryWrite(telemetryEvent);
        if (bulkQueued)
            Interlocked.Increment(ref _enqueuedBulk);
        else
        {
            Interlocked.Decrement(ref _bulkQueueDepth);
            Interlocked.Increment(ref _droppedBulk);
        }
        return bulkQueued;
    }

    public void Dispose()
    {
        TelemetryEmitter.SetSink(null);
        _stopSource.Cancel();
        _stopSource.Dispose();
    }

    private async Task RunAsync(CancellationToken cancellationToken)
    {
        var pending = new List<TelemetryEvent>(BatchSize);
        try
        {
            await _databaseReady.Task.WaitAsync(cancellationToken);
            var nextReplayAt = DateTime.UtcNow;
            var nextHealthAt = DateTime.UtcNow.Add(HealthInterval);
            using var timer = new PeriodicTimer(FlushInterval);

            while (await timer.WaitForNextTickAsync(cancellationToken))
            {
                if (_canUseDatabase && DateTime.UtcNow >= nextReplayAt)
                {
                    ReplaySpool();
                    nextReplayAt = DateTime.UtcNow.Add(ReplayInterval);
                }

                do
                {
                    DrainQueues(pending);
                    if (DateTime.UtcNow >= nextHealthAt)
                    {
                        pending.Add(AssignSequence(CreateHealthEvent()));
                        nextHealthAt = DateTime.UtcNow.Add(HealthInterval);
                    }

                    if (pending.Count > 0)
                    {
                        PersistOrSpool(pending);
                        pending.Clear();
                    }
                } while (Volatile.Read(ref _criticalQueueDepth) > 0 || Volatile.Read(ref _bulkQueueDepth) > 0);

                if (_criticalQueue.Reader.Completion.IsCompleted && _bulkQueue.Reader.Completion.IsCompleted)
                    break;
            }
        }
        catch (OperationCanceledException)
        {
            // Normal forced shutdown path.
        }
        catch (Exception exception)
        {
            Logger.Error(exception, "Telemetry writer stopped unexpectedly");
        }
        finally
        {
            DrainQueues(pending, int.MaxValue);
            if (pending.Count > 0)
            {
                if (_canUseDatabase && !_stopSource.IsCancellationRequested)
                    PersistOrSpool(pending);
                else
                    WriteSpool(pending.Select(PrepareStored).ToList());
            }
        }
    }

    private void DrainQueues(List<TelemetryEvent> target, int maximum = BatchSize)
    {
        while (target.Count < maximum && _criticalQueue.Reader.TryRead(out var critical))
        {
            Interlocked.Decrement(ref _criticalQueueDepth);
            target.Add(critical);
        }

        while (target.Count < maximum && _bulkQueue.Reader.TryRead(out var bulk))
        {
            Interlocked.Decrement(ref _bulkQueueDepth);
            target.Add(bulk);
        }
    }

    private void PersistOrSpool(IReadOnlyCollection<TelemetryEvent> events)
    {
        var storedEvents = events.Select(PrepareStored).ToList();
        if (!_canUseDatabase || !TryInsertBatch(storedEvents))
            WriteSpool(storedEvents);
    }

    private StoredTelemetryEvent PrepareStored(TelemetryEvent telemetryEvent) => new()
    {
        ServerName = _serverName,
        ServerBuild = _serverBuild,
        ServerBootId = _serverBootId,
        SequenceNo = telemetryEvent.SequenceNo,
        OccurredAt = telemetryEvent.OccurredAt.Kind == DateTimeKind.Utc
            ? telemetryEvent.OccurredAt
            : telemetryEvent.OccurredAt.ToUniversalTime(),
        EventType = telemetryEvent.EventType,
        Severity = telemetryEvent.Severity,
        AccountId = telemetryEvent.AccountId,
        CharacterId = telemetryEvent.CharacterId,
        ActorCharacterId = telemetryEvent.ActorCharacterId,
        TargetCharacterId = telemetryEvent.TargetCharacterId,
        SessionId = telemetryEvent.SessionId,
        CorrelationId = telemetryEvent.CorrelationId,
        ZoneId = telemetryEvent.ZoneId,
        WorldId = telemetryEvent.WorldId,
        SkillId = telemetryEvent.SkillId,
        QuestId = telemetryEvent.QuestId,
        QuestActId = telemetryEvent.QuestActId,
        ItemId = telemetryEvent.ItemId,
        Outcome = telemetryEvent.Outcome,
        ValueInt = telemetryEvent.ValueInt,
        Fingerprint = telemetryEvent.Fingerprint,
        PayloadJson = telemetryEvent.Payload == null ? null : JsonSerializer.Serialize(telemetryEvent.Payload, JsonOptions)
    };

    private TelemetryEvent AssignSequence(TelemetryEvent telemetryEvent) =>
        telemetryEvent with { SequenceNo = Interlocked.Increment(ref _sequenceNo) };

    private bool TryInsertBatch(List<StoredTelemetryEvent> events)
    {
        try
        {
            InsertBatch(events);
            Interlocked.Add(ref _written, events.Count);
            return true;
        }
        catch (Exception exception)
        {
            Interlocked.Increment(ref _databaseFailures);
            Logger.Warn(exception, "Telemetry database batch failed; spooling {0} events", events.Count);
            return false;
        }
    }

    private static void InsertBatch(List<StoredTelemetryEvent> events)
    {
        if (events.Count == 0)
            return;

        using var connection = MySQL.CreateConnection();
        using var transaction = connection.BeginTransaction();
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        var sql = new StringBuilder("""
            INSERT IGNORE INTO telemetry_events (
                occurred_at, received_at, server_name, server_build, server_boot_id, sequence_no,
                event_type, severity, account_id, character_id, actor_character_id,
                target_character_id, session_id, correlation_id, zone_id, world_id, skill_id,
                quest_id, quest_act_id, item_id, outcome, value_int, fingerprint, payload_json
            ) VALUES
            """);

        for (var index = 0; index < events.Count; index++)
        {
            if (index > 0)
                sql.Append(',');
            sql.Append($"(@occurred{index},UTC_TIMESTAMP(6),@server{index},@build{index},@boot{index},@sequence{index},")
                .Append($"@type{index},@severity{index},@account{index},@character{index},@actor{index},")
                .Append($"@target{index},@session{index},@correlation{index},@zone{index},@world{index},@skill{index},")
                .Append($"@quest{index},@questAct{index},@item{index},")
                .Append($"@outcome{index},@value{index},@fingerprint{index},@payload{index})");

            var telemetryEvent = events[index];
            command.Parameters.AddWithValue($"@occurred{index}", telemetryEvent.OccurredAt);
            command.Parameters.AddWithValue($"@server{index}", telemetryEvent.ServerName);
            command.Parameters.AddWithValue($"@build{index}", telemetryEvent.ServerBuild);
            command.Parameters.Add($"@boot{index}", MySqlDbType.Binary, 16).Value = telemetryEvent.ServerBootId.ToByteArray();
            command.Parameters.AddWithValue($"@sequence{index}", telemetryEvent.SequenceNo);
            command.Parameters.AddWithValue($"@type{index}", telemetryEvent.EventType);
            command.Parameters.AddWithValue($"@severity{index}", telemetryEvent.Severity);
            command.Parameters.AddWithValue($"@account{index}", DbValue(telemetryEvent.AccountId));
            command.Parameters.AddWithValue($"@character{index}", DbValue(telemetryEvent.CharacterId));
            command.Parameters.AddWithValue($"@actor{index}", DbValue(telemetryEvent.ActorCharacterId));
            command.Parameters.AddWithValue($"@target{index}", DbValue(telemetryEvent.TargetCharacterId));
            command.Parameters.AddWithValue($"@session{index}", DbValue(telemetryEvent.SessionId));
            command.Parameters.AddWithValue($"@correlation{index}", DbValue(telemetryEvent.CorrelationId));
            command.Parameters.AddWithValue($"@zone{index}", DbValue(telemetryEvent.ZoneId));
            command.Parameters.AddWithValue($"@world{index}", DbValue(telemetryEvent.WorldId));
            command.Parameters.AddWithValue($"@skill{index}", DbValue(telemetryEvent.SkillId));
            command.Parameters.AddWithValue($"@quest{index}", DbValue(telemetryEvent.QuestId));
            command.Parameters.AddWithValue($"@questAct{index}", DbValue(telemetryEvent.QuestActId));
            command.Parameters.AddWithValue($"@item{index}", DbValue(telemetryEvent.ItemId));
            command.Parameters.AddWithValue($"@outcome{index}", DbValue(telemetryEvent.Outcome));
            command.Parameters.AddWithValue($"@value{index}", DbValue(telemetryEvent.ValueInt));
            command.Parameters.Add($"@fingerprint{index}", MySqlDbType.Binary, 32).Value = DbValue(telemetryEvent.Fingerprint);
            command.Parameters.AddWithValue($"@payload{index}", DbValue(telemetryEvent.PayloadJson));
        }

        command.CommandText = sql.ToString();
        command.ExecuteNonQuery();
        transaction.Commit();
    }

    private void WriteSpool(List<StoredTelemetryEvent> events)
    {
        if (events.Count == 0)
            return;

        var stem = $"telemetry-{DateTime.UtcNow:yyyyMMdd-HHmmss-fffffff}-{Guid.NewGuid():N}";
        var temporaryPath = Path.Combine(_spoolDirectory, stem + ".tmp");
        var finalPath = Path.Combine(_spoolDirectory, stem + ".ndjson");
        try
        {
            Directory.CreateDirectory(_spoolDirectory);
            using (var stream = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            using (var writer = new StreamWriter(stream, new UTF8Encoding(false)))
            {
                foreach (var telemetryEvent in events)
                    writer.WriteLine(JsonSerializer.Serialize(telemetryEvent, JsonOptions));
                writer.Flush();
                stream.Flush(true);
            }

            File.Move(temporaryPath, finalPath);
            Interlocked.Add(ref _spooled, events.Count);
        }
        catch (Exception exception)
        {
            Interlocked.Increment(ref _spoolFailures);
            Logger.Error(exception, "Failed to spool {0} telemetry events", events.Count);
        }
    }

    private void ReplaySpool()
    {
        if (!Directory.Exists(_spoolDirectory))
            return;

        foreach (var path in Directory.EnumerateFiles(_spoolDirectory, "*.ndjson").Order())
        {
            var batch = new List<StoredTelemetryEvent>(BatchSize);
            try
            {
                foreach (var line in File.ReadLines(path))
                {
                    var telemetryEvent = JsonSerializer.Deserialize<StoredTelemetryEvent>(line, JsonOptions)
                        ?? throw new InvalidDataException($"Empty telemetry record in {path}");
                    batch.Add(telemetryEvent);
                    if (batch.Count < BatchSize)
                        continue;

                    InsertBatch(batch);
                    Interlocked.Add(ref _replayed, batch.Count);
                    batch.Clear();
                }

                if (batch.Count > 0)
                {
                    InsertBatch(batch);
                    Interlocked.Add(ref _replayed, batch.Count);
                }

                File.Delete(path);
            }
            catch (Exception exception) when (exception is JsonException or InvalidDataException)
            {
                Interlocked.Increment(ref _spoolFailures);
                var badPath = path + $".bad-{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid():N}";
                File.Move(path, badPath);
                Logger.Error(exception, "Invalid telemetry spool moved to {0}", badPath);
            }
            catch (Exception exception)
            {
                Interlocked.Increment(ref _databaseFailures);
                Logger.Warn(exception, "Telemetry spool replay paused at {0}", path);
                return;
            }
        }
    }

    private TelemetryEvent CreateHealthEvent()
    {
        var health = GetHealthSnapshot();
        return new TelemetryEvent
        {
            EventType = "telemetry_health",
            Outcome = health.DatabaseFailures > 0 || health.DroppedCritical > 0 ? "degraded" : "ok",
            ValueInt = health.CriticalQueueDepth + health.BulkQueueDepth,
            Payload = health
        };
    }

    private static object DbValue<T>(T value) => value is null ? DBNull.Value : value;

    private static string ResolveServerBuild()
    {
        var informationalVersion = Assembly.GetEntryAssembly()?
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion ?? "unknown";
        var separator = informationalVersion.LastIndexOf('+');
        if (separator >= 0 && separator + 1 < informationalVersion.Length)
            informationalVersion = informationalVersion[(separator + 1)..];
        return Truncate(informationalVersion, 40);
    }

    private static string Truncate(string value, int length) =>
        string.IsNullOrEmpty(value) || value.Length <= length ? value : value[..length];
}
