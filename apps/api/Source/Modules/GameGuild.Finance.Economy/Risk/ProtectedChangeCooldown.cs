namespace GameGuild.Finance.Economy.Risk;

public enum ProtectedChangeKind
{
    PasswordReset = 1,
    MfaReset = 2,
    EmailChange = 3,
    OwnershipTransfer = 4,
    IdentityUpdate = 5,
    BankAccount = 6,
    PayoutDestination = 7,
    NewDeviceLogin = 8,
    HighRiskSessionElevation = 9
}

public sealed record ProtectedChangeCooldown(
    Guid SubjectId,
    ProtectedChangeKind Kind,
    string ValueHash,
    long Version,
    DateTimeOffset ChangedAt,
    DateTimeOffset AvailableAt);

public sealed record ProtectedChangeEvaluation(ProtectedChangeCooldown Change, bool IsElapsed);

public sealed class ProtectedChangeCooldownRegistry
{
    private readonly object _gate = new();
    private readonly Dictionary<(Guid SubjectId, ProtectedChangeKind Kind), ProtectedChangeCooldown> _changes = [];

    public ProtectedChangeCooldown Record(
        Guid subjectId,
        ProtectedChangeKind kind,
        string valueHash,
        DateTimeOffset changedAt,
        TimeSpan cooldown)
    {
        if (subjectId == Guid.Empty) throw new ArgumentException("Subject ID cannot be empty.", nameof(subjectId));
        if (!Enum.IsDefined(kind)) throw new ArgumentOutOfRangeException(nameof(kind));
        ArgumentException.ThrowIfNullOrWhiteSpace(valueHash);
        if (cooldown <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(cooldown));

        lock (_gate)
        {
            var key = (subjectId, kind);
            var version = _changes.TryGetValue(key, out var previous) ? previous.Version + 1 : 1;
            var change = new ProtectedChangeCooldown(
                subjectId, kind, valueHash.Trim(), version, changedAt, changedAt.Add(cooldown));
            _changes[key] = change;
            return change;
        }
    }

    public ProtectedChangeEvaluation Evaluate(
        Guid subjectId,
        ProtectedChangeKind kind,
        DateTimeOffset now)
    {
        lock (_gate)
        {
            if (!_changes.TryGetValue((subjectId, kind), out var change))
                throw new KeyNotFoundException("No protected change is registered for this subject.");
            return new ProtectedChangeEvaluation(change, now >= change.AvailableAt);
        }
    }

    public IReadOnlyList<ProtectedChangeCooldown> ForSubject(Guid subjectId)
    {
        if (subjectId == Guid.Empty) throw new ArgumentException("Subject ID cannot be empty.", nameof(subjectId));
        lock (_gate)
        {
            return [.. _changes.Values
                .Where(change => change.SubjectId == subjectId)
                .OrderBy(change => change.Kind)];
        }
    }
}
