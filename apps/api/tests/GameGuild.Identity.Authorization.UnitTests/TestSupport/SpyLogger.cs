using Microsoft.Extensions.Logging;

namespace GameGuild.Identity.Authorization.UnitTests.TestSupport;

/// <summary>
///     In-memory logger capturing (level, message) pairs so tests can assert on
///     audit-relevant log output (for example the DEBUG-level owner-override use audit).
/// </summary>
public sealed record SpyLogEntry(LogLevel Level, string Message, Exception? Exception = null);

public sealed class SpyLogger<T> : ILogger<T>
{
    private readonly object _gate = new();

    public List<SpyLogEntry> Entries { get; } = [];

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        lock (_gate)
        {
            Entries.Add(new SpyLogEntry(logLevel, formatter(state, exception), exception));
        }
    }
}
