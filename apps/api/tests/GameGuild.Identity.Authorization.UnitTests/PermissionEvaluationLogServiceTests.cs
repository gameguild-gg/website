using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace GameGuild.Identity.Authorization.UnitTests;

public sealed class PermissionEvaluationLogServiceTests
{
    private static PermissionEvaluationRecord CreateRecord(
        PermissionEvaluationOutcome outcome = PermissionEvaluationOutcome.Allow,
        DateTime? evaluatedAtUtc = null) => new(
        Guid.Parse("4b50fdd6-2e85-42bb-a9fa-27f6cb7e97c6"),
        Guid.Parse("7b37d70c-6ecd-4eb2-9f21-c08fc9563e85"),
        "Project",
        "b659b7bf-6281-42e6-a7ef-23d296cff5dd",
        ["Read", "Edit"],
        outcome,
        "graphql",
        "guarded",
        outcome == PermissionEvaluationOutcome.Allow ? null : "permission_denied",
        evaluatedAtUtc ?? default);

    [Fact]
    public async Task RecordAsync_PersistsUserTenantResourcePermissionsAndOutcomeInEverySink()
    {
        var first = new RecordingSink(persisted: true);
        var second = new RecordingSink(persisted: true);
        var service = CreateService(first, second);

        var result = await service.RecordAsync(CreateRecord(), CancellationToken.None);

        result.Should().BeEquivalentTo(new PermissionEvaluationLogResult(Persisted: true, SinkCount: 2, FailureCount: 0));
        foreach (var sink in new[] { first, second })
        {
            sink.Received.Should().ContainSingle().Which.Should().Match<PermissionEvaluationRecord>(
                record =>
                    record.UserId == Guid.Parse("4b50fdd6-2e85-42bb-a9fa-27f6cb7e97c6") &&
                    record.TenantId == Guid.Parse("7b37d70c-6ecd-4eb2-9f21-c08fc9563e85") &&
                    record.ResourceType == "Project" &&
                    record.ResourceId == "b659b7bf-6281-42e6-a7ef-23d296cff5dd" &&
                    record.RequiredPermissions.SequenceEqual(new[] { "Read", "Edit" }) &&
                    record.Outcome == PermissionEvaluationOutcome.Allow &&
                    record.Source == "graphql" &&
                    record.Operation == "guarded" &&
                    record.EvaluatedAtUtc != default);
        }
    }

    [Fact]
    public async Task RecordAsync_PreservesExplicitEvaluationTimestamp()
    {
        var sink = new RecordingSink(persisted: true);
        var service = CreateService(sink);
        var timestamp = new DateTime(2026, 10, 8, 12, 0, 0, DateTimeKind.Utc);

        await service.RecordAsync(CreateRecord(evaluatedAtUtc: timestamp), CancellationToken.None);

        sink.Received.Should().ContainSingle().Which.EvaluatedAtUtc.Should().Be(timestamp);
    }

    [Theory]
    [InlineData(PermissionEvaluationOutcome.Deny)]
    [InlineData(PermissionEvaluationOutcome.Error)]
    public async Task RecordAsync_RecordsDeniedAndErroredEvaluationsWithOutcomeIntact(
        PermissionEvaluationOutcome outcome)
    {
        var sink = new RecordingSink(persisted: true);
        var service = CreateService(sink);

        await service.RecordAsync(CreateRecord(outcome), CancellationToken.None);

        sink.Received.Should().ContainSingle().Which.Outcome.Should().Be(outcome);
    }

    [Fact]
    public async Task RecordAsync_SurfacesSinkExceptionsInsteadOfThrowingOrSwallowing()
    {
        var failing = new ThrowingSink(new InvalidOperationException("audit store unavailable"));
        var healthy = new RecordingSink(persisted: true);
        var service = CreateService(failing, healthy);

        var result = await service.RecordAsync(CreateRecord(), CancellationToken.None);

        result.Should().BeEquivalentTo(
            new PermissionEvaluationLogResult(Persisted: true, SinkCount: 2, FailureCount: 1));
        healthy.Received.Should().ContainSingle();
    }

    [Fact]
    public async Task RecordAsync_ReportsUnpersistedWhenEverySinkFailsOrDeclines()
    {
        var throwing = new ThrowingSink(new InvalidOperationException("audit store unavailable"));
        var declining = new RecordingSink(persisted: false);
        var service = CreateService(throwing, declining);

        var result = await service.RecordAsync(CreateRecord(), CancellationToken.None);

        result.Should().BeEquivalentTo(
            new PermissionEvaluationLogResult(Persisted: false, SinkCount: 2, FailureCount: 2));
    }

    [Fact]
    public async Task RecordAsync_ReportsMissingDurabilityWhenNoSinkIsConfigured()
    {
        var service = CreateService();

        var result = await service.RecordAsync(CreateRecord(), CancellationToken.None);

        result.Should().BeEquivalentTo(new PermissionEvaluationLogResult(Persisted: false, SinkCount: 0, FailureCount: 0));
    }

    [Fact]
    public async Task RecordAsync_PropagatesRequestCancellation()
    {
        var sink = new RecordingSink(persisted: true);
        var service = CreateService(sink);
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        var act = () => service.RecordAsync(CreateRecord(), cancelled.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
        sink.Received.Should().BeEmpty();
    }

    [Fact]
    public async Task RecordAsync_RejectsNullRecord()
    {
        var service = CreateService(new RecordingSink(persisted: true));

        var act = () => service.RecordAsync(null!, CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    private static PermissionEvaluationLogService CreateService(params IPermissionEvaluationLogSink[] sinks) =>
        new(sinks, NullLogger<PermissionEvaluationLogService>.Instance);

    private sealed class RecordingSink(bool persisted) : IPermissionEvaluationLogSink
    {
        private readonly List<PermissionEvaluationRecord> _received = [];

        public IReadOnlyList<PermissionEvaluationRecord> Received => _received;

        public Task<bool> TryRecordAsync(
            PermissionEvaluationRecord record,
            CancellationToken cancellationToken = default)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return Task.FromCanceled<bool>(cancellationToken);
            }

            _received.Add(record);
            return Task.FromResult(persisted);
        }
    }

    private sealed class ThrowingSink(Exception exception) : IPermissionEvaluationLogSink
    {
        public Task<bool> TryRecordAsync(
            PermissionEvaluationRecord record,
            CancellationToken cancellationToken = default)
            => Task.FromException<bool>(exception);
    }
}
