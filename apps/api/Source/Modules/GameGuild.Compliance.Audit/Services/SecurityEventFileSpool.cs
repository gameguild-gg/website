using System.Text;
using System.Text.Json;

namespace GameGuild.Compliance.Audit;

/// <summary>One durable spool record: the audit payload as JSON plus the event identity for replay idempotency.</summary>
public sealed record SecurityEventSpoolRecord(Guid EventId, DateTime OccurredAtUtc, string PayloadJson);

/// <summary>Statistics of the local durable spool.</summary>
public sealed record SecurityEventSpoolStats(int PendingCount, DateTime? OldestOccurredAtUtc);

/// <summary>
///     Durable local spool for security events that could not be persisted to the database.
///     Records are appended as JSON lines and survive process restarts and database outages;
///     the drainer replays them and removes delivered records with an atomic file replacement.
///     The spool is per-instance (its directory is configurable); it is the last-resort guarantee
///     that no security event is silently lost.
/// </summary>
public interface ISecurityEventSpool
{
    void Append(SecurityEventSpoolRecord record);

    IReadOnlyList<SecurityEventSpoolRecord> ReadAll();

    /// <summary>Removes the delivered records; undelivered records are preserved verbatim.</summary>
    void Remove(IEnumerable<Guid> deliveredEventIds);

    SecurityEventSpoolStats GetStats();
}

public sealed class SecurityEventFileSpool : ISecurityEventSpool
{
    private const string SpoolFileName = "security-events.spool.jsonl";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly object gate = new();

    public SecurityEventFileSpool(SecurityEventPipelineOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        DirectoryPath = options.SpoolDirectoryPath;
    }

    public string DirectoryPath { get; }

    private string SpoolPath => Path.Combine(DirectoryPath, SpoolFileName);

    public void Append(SecurityEventSpoolRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);
        if (string.IsNullOrWhiteSpace(record.PayloadJson))
        {
            throw new ArgumentException("Spool payload cannot be empty.", nameof(record));
        }

        lock (gate)
        {
            Directory.CreateDirectory(DirectoryPath);
            var line = JsonSerializer.Serialize(record, JsonOptions);
            File.AppendAllText(SpoolPath, line + Environment.NewLine, Encoding.UTF8);
        }
    }

    public IReadOnlyList<SecurityEventSpoolRecord> ReadAll()
    {
        lock (gate)
        {
            if (!File.Exists(SpoolPath))
            {
                return [];
            }

            var records = new List<SecurityEventSpoolRecord>();
            foreach (var line in File.ReadLines(SpoolPath))
            {
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                try
                {
                    var record = JsonSerializer.Deserialize<SecurityEventSpoolRecord>(line, JsonOptions);
                    if (record is { PayloadJson.Length: > 0 } && record.EventId != Guid.Empty)
                    {
                        records.Add(record);
                    }
                }
                catch (JsonException)
                {
                    // A partially written line (for example after a process kill mid-append) is skipped;
                    // complete lines before and after it remain readable.
                }
            }

            return records;
        }
    }

    public void Remove(IEnumerable<Guid> deliveredEventIds)
    {
        ArgumentNullException.ThrowIfNull(deliveredEventIds);
        var delivered = deliveredEventIds.ToHashSet();

        lock (gate)
        {
            if (!File.Exists(SpoolPath))
            {
                return;
            }

            var remaining = new List<string>();
            foreach (var line in File.ReadLines(SpoolPath))
            {
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                try
                {
                    var record = JsonSerializer.Deserialize<SecurityEventSpoolRecord>(line, JsonOptions);
                    if (record is not null && delivered.Contains(record.EventId))
                    {
                        continue;
                    }
                }
                catch (JsonException)
                {
                    // Keep unparseable lines so an operator can inspect them rather than losing data.
                }

                remaining.Add(line);
            }

            var temporaryPath = SpoolPath + ".tmp";
            File.WriteAllLines(temporaryPath, remaining, Encoding.UTF8);
            File.Move(temporaryPath, SpoolPath, overwrite: true);
        }
    }

    public SecurityEventSpoolStats GetStats()
    {
        var records = ReadAll();
        return new SecurityEventSpoolStats(records.Count, records.Count > 0 ? records.Min(record => record.OccurredAtUtc) : null);
    }
}

/// <summary>
///     Volatile, per-instance status of the security event pipeline drain loop. The durable truth
///     is the spool itself; this tracker reports drain recency and the last drain error.
/// </summary>
public interface ISecurityEventPipelineStatusTracker
{
    DateTime? LastDrainAttemptedAtUtc { get; }

    DateTime? LastDrainSucceededAtUtc { get; }

    string? LastDrainError { get; }

    void RecordDrainAttempt(DateTime attemptedAtUtc);

    void RecordDrainSuccess(DateTime succeededAtUtc);

    void RecordDrainFailure(DateTime failedAtUtc, string error);
}

public sealed class SecurityEventPipelineStatusTracker : ISecurityEventPipelineStatusTracker
{
    private readonly object gate = new();

    public DateTime? LastDrainAttemptedAtUtc { get; private set; }

    public DateTime? LastDrainSucceededAtUtc { get; private set; }

    public string? LastDrainError { get; private set; }

    public void RecordDrainAttempt(DateTime attemptedAtUtc)
    {
        lock (gate)
        {
            LastDrainAttemptedAtUtc = attemptedAtUtc;
        }
    }

    public void RecordDrainSuccess(DateTime succeededAtUtc)
    {
        lock (gate)
        {
            LastDrainSucceededAtUtc = succeededAtUtc;
            LastDrainError = null;
        }
    }

    public void RecordDrainFailure(DateTime failedAtUtc, string error)
    {
        lock (gate)
        {
            LastDrainAttemptedAtUtc = failedAtUtc;
            LastDrainError = error;
        }
    }
}
