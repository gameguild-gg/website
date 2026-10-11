namespace GameGuild.Commerce.Payments;

/// <summary>Circuit state of one payment gateway.</summary>
public enum GatewayCircuitState
{
    /// <summary>The gateway is healthy; attempts flow freely.</summary>
    Closed = 0,

    /// <summary>
    ///     The gateway exceeded the consecutive-failure threshold; attempts are rejected
    ///     until the open duration elapses.
    /// </summary>
    Open = 1,

    /// <summary>
    ///     The open duration elapsed; exactly one probe attempt is allowed to decide
    ///     whether the gateway recovers (Closed) or reopens.
    /// </summary>
    HalfOpen = 2
}

/// <summary>
///     Health snapshot of one gateway circuit (issue #413).
/// </summary>
/// <param name="ProviderId">Provider identifier of the gateway.</param>
/// <param name="State">Current circuit state.</param>
/// <param name="ConsecutiveFailures">Consecutive retriable failures recorded while closed.</param>
/// <param name="OpenedAtUtc">When the circuit last opened; null while closed.</param>
/// <param name="FailureThreshold">Configured consecutive-failure threshold.</param>
public sealed record GatewayCircuitSnapshot(
    string ProviderId,
    GatewayCircuitState State,
    int ConsecutiveFailures,
    DateTime? OpenedAtUtc,
    int FailureThreshold);

/// <summary>
///     Per-gateway circuit breaker for the multi-gateway failover path (issue #413).
///     A gateway whose circuit is open is skipped by the failover orchestration; after the
///     open duration elapses a single half-open probe attempt decides recovery. Only
///     retriable gateway failures advance the breaker — terminal declines (for example a
///     card rejection) mean the gateway answered and stay healthy from the breaker's point
///     of view.
/// </summary>
public interface IGatewayCircuitBreaker
{
    /// <summary>Gets the circuit snapshot of one gateway.</summary>
    GatewayCircuitSnapshot GetSnapshot(string providerId);

    /// <summary>Gets the snapshot of every known gateway circuit.</summary>
    IReadOnlyList<GatewayCircuitSnapshot> GetAllSnapshots();

    /// <summary>
    ///     Tries to acquire the right to attempt one charge on the gateway. Returns false
    ///     while the circuit is open; grants the single half-open probe when the open
    ///     duration has elapsed; always true while closed.
    /// </summary>
    bool TryAcquireAttempt(string providerId);

    /// <summary>
    ///     Records a successful (or otherwise conclusive) gateway exchange. Resets the
    ///     consecutive-failure count and closes a half-open probe.
    /// </summary>
    void RecordSuccess(string providerId);

    /// <summary>
    ///     Records a retriable gateway failure. Increments the consecutive-failure count
    ///     and opens the circuit at the configured threshold; a failed half-open probe
    ///     reopens the circuit immediately.
    /// </summary>
    void RecordFailure(string providerId);
}

/// <summary>
///     In-memory, process-wide <see cref="IGatewayCircuitBreaker" /> (issue #413). Thread-safe;
///     time is read from <see cref="SystemClock" /> so tests can advance it through a time provider.
/// </summary>
public sealed class InMemoryGatewayCircuitBreaker : IGatewayCircuitBreaker
{
    private readonly object _syncRoot = new();
    private readonly int _failureThreshold;
    private readonly TimeSpan _openDuration;
    private readonly Dictionary<string, Circuit> _circuits = new(StringComparer.Ordinal);

    /// <summary>Initializes the breaker with the failover policy.</summary>
    public InMemoryGatewayCircuitBreaker(GatewayFailoverOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _failureThreshold = options.CircuitFailureThreshold;
        _openDuration = TimeSpan.FromSeconds(options.CircuitOpenSeconds);
    }

    /// <inheritdoc />
    public GatewayCircuitSnapshot GetSnapshot(string providerId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(providerId);
        lock (_syncRoot)
        {
            return BuildSnapshot(GetCircuit(providerId));
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<GatewayCircuitSnapshot> GetAllSnapshots()
    {
        lock (_syncRoot)
        {
            return _circuits.Values
                .OrderBy(circuit => circuit.ProviderId, StringComparer.Ordinal)
                .Select(BuildSnapshot)
                .ToList();
        }
    }

    /// <inheritdoc />
    public bool TryAcquireAttempt(string providerId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(providerId);
        lock (_syncRoot)
        {
            var circuit = GetCircuit(providerId);

            switch (circuit.State)
            {
                case GatewayCircuitState.Closed:
                    return true;

                case GatewayCircuitState.Open:
                    if (SystemClock.UtcNow - circuit.OpenedAtUtc < _openDuration)
                    {
                        return false;
                    }

                    // Open duration elapsed: transition to half-open and grant exactly one probe.
                    circuit.State = GatewayCircuitState.HalfOpen;
                    circuit.HalfOpenProbeInFlight = true;
                    circuit.HalfOpenProbeLeaseUtc = SystemClock.UtcNow;
                    return true;

                case GatewayCircuitState.HalfOpen:
                    // A probe is already in flight; only that probe may attempt. The lease
                    // expires after one open duration so a probe that never reported (for
                    // example a cancelled request) cannot wedge the gateway in half-open.
                    if (circuit.HalfOpenProbeInFlight &&
                        SystemClock.UtcNow - circuit.HalfOpenProbeLeaseUtc < _openDuration)
                    {
                        return false;
                    }

                    circuit.HalfOpenProbeInFlight = true;
                    circuit.HalfOpenProbeLeaseUtc = SystemClock.UtcNow;
                    return true;

                default:
                    return false;
            }
        }
    }

    /// <inheritdoc />
    public void RecordSuccess(string providerId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(providerId);
        lock (_syncRoot)
        {
            var circuit = GetCircuit(providerId);
            circuit.State = GatewayCircuitState.Closed;
            circuit.ConsecutiveFailures = 0;
            circuit.OpenedAtUtc = null;
            circuit.HalfOpenProbeInFlight = false;
            circuit.HalfOpenProbeLeaseUtc = null;
        }
    }

    /// <inheritdoc />
    public void RecordFailure(string providerId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(providerId);
        lock (_syncRoot)
        {
            var circuit = GetCircuit(providerId);
            circuit.HalfOpenProbeInFlight = false;
            circuit.HalfOpenProbeLeaseUtc = null;
            circuit.ConsecutiveFailures++;

            if (circuit.State == GatewayCircuitState.HalfOpen || circuit.ConsecutiveFailures >= _failureThreshold)
            {
                Open(circuit);
            }
        }
    }

    private void Open(Circuit circuit)
    {
        circuit.State = GatewayCircuitState.Open;
        circuit.OpenedAtUtc = SystemClock.UtcNow;
        circuit.HalfOpenProbeInFlight = false;
        circuit.HalfOpenProbeLeaseUtc = null;
    }

    private Circuit GetCircuit(string providerId)
    {
        if (_circuits.TryGetValue(providerId, out var circuit))
        {
            return circuit;
        }

        circuit = new Circuit(providerId);
        _circuits[providerId] = circuit;

        return circuit;
    }

    private GatewayCircuitSnapshot BuildSnapshot(Circuit circuit) =>
        new(
            circuit.ProviderId,
            circuit.State,
            circuit.ConsecutiveFailures,
            circuit.OpenedAtUtc,
            _failureThreshold);

    private sealed class Circuit(string providerId)
    {
        public string ProviderId { get; } = providerId;

        public GatewayCircuitState State { get; set; } = GatewayCircuitState.Closed;

        public int ConsecutiveFailures { get; set; }

        public DateTime? OpenedAtUtc { get; set; }

        public bool HalfOpenProbeInFlight { get; set; }

        public DateTime? HalfOpenProbeLeaseUtc { get; set; }
    }
}
