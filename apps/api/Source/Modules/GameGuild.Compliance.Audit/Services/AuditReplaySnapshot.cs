using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace GameGuild.Compliance.Audit;

/// <summary>A frozen, bounded projection for replay. It omits free-form audit content.
/// Its digest detects content changes against an independently protected expected digest;
/// it does not establish event provenance, actor attribution or clock synchronization.</summary>
public sealed class AuditReplaySnapshot
{
    public static int MaximumEvents => 10_000;
    public static int MaximumBytes => 16 * 1024 * 1024;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public Guid? TenantId { get; }
    public DateTimeOffset StartUtc { get; }
    public DateTimeOffset EndUtc { get; }
    public DateTimeOffset CapturedAtUtc { get; }
    public IReadOnlyList<AuditReplaySnapshotEvent> Events { get; }
    public int TotalCount => Events.Count;
    public string Json { get; }
    public string Sha256 { get; }

    /// <summary>Caller must authorize and capture one complete, consistent database selection.
    /// Count validation alone cannot prove that the supplied selection is complete.</summary>
    public AuditReplaySnapshot(Guid? tenantId, IEnumerable<AuditLogDto> supplied,
        int declaredTotalCount, DateTimeOffset start, DateTimeOffset end, DateTimeOffset capturedAt)
    {
        ArgumentNullException.ThrowIfNull(supplied);
        ValidateOptionalGuid(tenantId, "Tenant");
        if (declaredTotalCount < 0 || declaredTotalCount > MaximumEvents) {
            throw Invalid("Replay requires at most 10000 events; narrow the selection.");
        }
        var startUtc = start.ToUniversalTime();
        var endUtc = end.ToUniversalTime();
        var capturedAtUtc = capturedAt.ToUniversalTime();
        if (startUtc > endUtc || endUtc > capturedAtUtc) {
            throw Invalid("Replay range must be ordered and must not extend beyond capture time.");
        }

        var events = new List<AuditReplaySnapshotEvent>(declaredTotalCount);
        var ids = new HashSet<Guid>();
        foreach (var input in supplied)
        {
            if (events.Count == MaximumEvents) {
                throw Invalid("Replay exceeds 10000 events; no partial snapshot is returned.");
            }
            if (input is null) { throw Invalid("Replay event is missing."); }
            if (input.TenantId != tenantId) { throw Invalid("Every replay event must match the selected tenant."); }
            if (input.Id == Guid.Empty || !ids.Add(input.Id)) { throw Invalid("Replay event IDs must be non-empty and unique."); }
            if (input.CreatedAt.Kind != DateTimeKind.Utc) { throw Invalid("Replay event timestamps must be explicitly UTC."); }
            var at = new DateTimeOffset(input.CreatedAt);
            if (at < startUtc || at > endUtc) { throw Invalid("Replay event timestamp is outside the captured range."); }
            ValidateOptionalGuid(input.UserId, "User");
            ValidateOptionalGuid(input.SessionId, "Session");
            events.Add(new AuditReplaySnapshotEvent(
                input.Id.ToString("D"), tenantId?.ToString("D"),
                input.CreatedAt.ToString("O", CultureInfo.InvariantCulture),
                RequiredText(input.ActionType, "Action", 100), RequiredText(input.ResourceType, "Resource type", 100),
                OptionalText(input.ResourceId, "Resource ID", 500), input.UserId?.ToString("D"),
                input.SessionId?.ToString("D"), OptionalText(input.CorrelationId, "Correlation ID", 100),
                input.Success));
        }
        if (events.Count != declaredTotalCount) { throw Invalid("Replay requires the complete declared result set."); }
        // All timestamps are normalized UTC strings with seven fraction digits.
        events.Sort((left, right) =>
        {
            var time = StringComparer.Ordinal.Compare(left.CreatedAt, right.CreatedAt);
            return time != 0 ? time : StringComparer.Ordinal.Compare(left.Id, right.Id);
        });
        var frozen = events.AsReadOnly();
        var document = new SnapshotDocument(1, tenantId?.ToString("D"), events.Count,
            startUtc.UtcDateTime.ToString("O", CultureInfo.InvariantCulture),
            endUtc.UtcDateTime.ToString("O", CultureInfo.InvariantCulture),
            capturedAtUtc.UtcDateTime.ToString("O", CultureInfo.InvariantCulture), frozen);
        using var output = new BoundedSnapshotStream();
        JsonSerializer.Serialize(output, document, JsonOptions);
        var bytes = output.ToArray();
        var json = Encoding.UTF8.GetString(bytes);
        var digest = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        TenantId = tenantId;
        StartUtc = startUtc;
        EndUtc = endUtc;
        CapturedAtUtc = capturedAtUtc;
        Events = frozen;
        Json = json;
        Sha256 = digest;
    }

    public static AuditReplaySnapshot Capture(Guid? tenantId, IEnumerable<AuditLogDto> supplied,
        int declaredTotalCount, DateTimeOffset start, DateTimeOffset end, DateTimeOffset capturedAt) =>
        new(tenantId, supplied, declaredTotalCount, start, end, capturedAt);

    public static bool MatchesDigest(string json, string expectedSha256)
    {
        ArgumentNullException.ThrowIfNull(json);
        ArgumentNullException.ThrowIfNull(expectedSha256);
        if (expectedSha256.Length != 64 ||
            expectedSha256.Any(character => character is not (>= '0' and <= '9') and not (>= 'a' and <= 'f'))) {
            return false;
        }
        if (json.Length > MaximumBytes || Encoding.UTF8.GetByteCount(json) > MaximumBytes) { return false; }
        var actual = SHA256.HashData(Encoding.UTF8.GetBytes(json));
        return CryptographicOperations.FixedTimeEquals(actual, Convert.FromHexString(expectedSha256));
    }

    private static void ValidateOptionalGuid(Guid? value, string field)
    {
        if (value == Guid.Empty) { throw Invalid(field + " ID must be non-empty when supplied."); }
    }

    private static string RequiredText(string? value, string field, int maximum)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > maximum) {
            throw Invalid(field + " has an invalid length.");
        }
        return value;
    }

    private static string? OptionalText(string? value, string field, int maximum) =>
        value is null ? null : RequiredText(value, field, maximum);

    private static ArgumentException Invalid(string message) => new(message);

    private sealed record SnapshotDocument(int SchemaVersion, string? TenantId, int TotalCount,
        string StartUtc, string EndUtc, string CapturedAtUtc, IReadOnlyList<AuditReplaySnapshotEvent> Events);

    private sealed class BoundedSnapshotStream : MemoryStream
    {
        private void EnsureCapacityFor(int count)
        {
            if (count < 0 || Position > MaximumBytes - count) {
                throw Invalid("Replay snapshot exceeds 16 MiB; narrow the selection. No partial snapshot is returned.");
            }
        }
        public override void Write(byte[] buffer, int offset, int count)
        {
            EnsureCapacityFor(count);
            base.Write(buffer, offset, count);
        }
        public override void Write(ReadOnlySpan<byte> buffer)
        {
            EnsureCapacityFor(buffer.Length);
            base.Write(buffer);
        }
        public override void WriteByte(byte value)
        {
            EnsureCapacityFor(1);
            base.WriteByte(value);
        }
    }
}

/// <summary>Safe event fields shared with the Web replay core. UserId may identify a subject;
/// it must not be presented as verified attribution without additional source evidence.</summary>
public sealed record AuditReplaySnapshotEvent(string Id, string? TenantId, string CreatedAt,
    string ActionType, string ResourceType, string? ResourceId, string? UserId,
    string? SessionId, string? CorrelationId, bool Success);
