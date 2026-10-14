using GameGuild.Learning;

namespace GameGuild.Learning.Enrollments.UnitTests;

/// <summary>A learning audit sink that records every event it receives.</summary>
public sealed class RecordingLearningAuditEventSink : ILearningAuditEventSink
{
    public List<LearningAuditEvent> Events { get; } = [];

    public Task RecordAsync(LearningAuditEvent auditEvent, CancellationToken cancellationToken)
    {
        Events.Add(auditEvent);
        return Task.CompletedTask;
    }
}

/// <summary>A learning audit sink that never records anything; used where tests don't assert audit output.</summary>
public sealed class NoopLearningAuditEventSink : ILearningAuditEventSink
{
    public Task RecordAsync(LearningAuditEvent auditEvent, CancellationToken cancellationToken) => Task.CompletedTask;
}

/// <summary>A learning audit sink that fails persistently, for fail-closed rollback tests.</summary>
public sealed class ThrowingLearningAuditEventSink(Exception? exception = null) : ILearningAuditEventSink
{
    private readonly Exception _exception = exception ?? new InvalidOperationException("Synthetic learning audit persistence failure.");

    public Task RecordAsync(LearningAuditEvent auditEvent, CancellationToken cancellationToken) => Task.FromException(_exception);
}
