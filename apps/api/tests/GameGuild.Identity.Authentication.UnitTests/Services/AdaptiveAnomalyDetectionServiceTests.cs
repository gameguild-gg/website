using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using GameGuild;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace GameGuild.Identity.Authentication.UnitTests.Services;

/// <summary>
///     Tests for the adaptive (online statistical learning) anomaly detection service:
///     learned deviation scoring, cold-start abstention, drift adaptation, sensitivity
///     configuration, fail-open behavior, and per-subject state persistence.
/// </summary>
public sealed class AdaptiveAnomalyDetectionServiceTests
{
    private static readonly DateTime TrainStart = new(2026, 10, 1, 9, 0, 0, DateTimeKind.Utc);
    private const string TrainIp = "203.0.113.10";
    private const string ProbeIp = "198.51.100.99";
    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _tenantId = Guid.NewGuid();

    // ── Cold start ──────────────────────────────────────────────────────

    [Fact]
    public async Task ColdStart_AbstainsUntilTheMinimumObservationCountIsReached()
    {
        var options = new AdaptiveAnomalyDetectionOptions { MinimumObservations = 20 };
        using var store = new BaselineStore();
        var sut = CreateService(store, options);

        for (var i = 0; i < 19; i++)
        {
            var assessment = await sut.AssessAsync(TrainAttempt(i), CancellationToken.None);
            assessment.Origin.Should().Be(AdaptiveAnomalyOrigin.ColdStart);
            assessment.IsLearnedDeviation.Should().BeFalse();
            assessment.BaselineSampleCount.Should().Be(i);
        }

        // The baseline is still being learned during cold start.
        var persisted = await store.GetBySubjectKeyAsync(SubjectKey(), CancellationToken.None);
        persisted!.ObservationCount.Should().Be(19);
        persisted.LastObservedAtUtc.Should().Be(TrainAttempt(18).AttemptedAt);
    }

    [Fact]
    public async Task ColdStart_AnObviouslyDeviantProbeIsNotFlaggedWithoutBaselineData()
    {
        var options = new AdaptiveAnomalyDetectionOptions { MinimumObservations = 20 };
        using var store = new BaselineStore();
        var sut = CreateService(store, options);

        for (var i = 0; i < 10; i++)
        {
            await sut.AssessAsync(TrainAttempt(i), CancellationToken.None);
        }

        var assessment = await sut.AssessAsync(DeviantProbe(TrainStart.AddDays(30).AddHours(2)), CancellationToken.None);

        assessment.Origin.Should().Be(AdaptiveAnomalyOrigin.ColdStart);
        assessment.IsLearnedDeviation.Should().BeFalse();
        assessment.LearnedRiskScoreContribution.Should().Be(0);
    }

    // ── Learned deviation ───────────────────────────────────────────────

    [Fact]
    public async Task LearnedDeviation_FlagsUnusualHourNovelIpAndIrregularCadence()
    {
        var options = new AdaptiveAnomalyDetectionOptions { MinimumObservations = 20 };
        using var store = new BaselineStore();
        var sut = CreateService(store, options);

        for (var i = 0; i < options.MinimumObservations + 5; i++)
        {
            await sut.AssessAsync(TrainAttempt(i), CancellationToken.None);
        }

        // Trained rhythm: daily 09:00 from TrainIp. The probe lands at 11:00 (unusual hour),
        // from a novel IP, after an irregular multi-day gap.
        var assessment = await sut.AssessAsync(DeviantProbe(TrainStart.AddDays(30).AddHours(2)), CancellationToken.None);

        assessment.Origin.Should().Be(AdaptiveAnomalyOrigin.LearnedBaseline);
        assessment.IsLearnedDeviation.Should().BeTrue();
        assessment.DeviationLabels.Should().Contain(
        [
            AdaptiveAnomalyDetectionService.HourOfDayDeviationLabel,
            AdaptiveAnomalyDetectionService.IpNoveltyDeviationLabel,
            AdaptiveAnomalyDetectionService.CadenceDeviationLabel
        ]);
        assessment.CombinedZScore.Should().BeGreaterThanOrEqualTo(options.ZScoreThreshold);
        assessment.LearnedRiskScoreContribution.Should().BePositive()
            .And.BeLessThanOrEqualTo(options.MaxLearnedRiskScore);
        assessment.BaselineSampleCount.Should().BeGreaterThanOrEqualTo(options.MinimumObservations);
    }

    [Fact]
    public async Task LearnedDeviation_DoesNotFlagBehaviorConsistentWithTheBaseline()
    {
        var options = new AdaptiveAnomalyDetectionOptions { MinimumObservations = 20 };
        using var store = new BaselineStore();
        var sut = CreateService(store, options);

        for (var i = 0; i < options.MinimumObservations + 5; i++)
        {
            await sut.AssessAsync(TrainAttempt(i), CancellationToken.None);
        }

        // Same hour, same IP, same daily cadence as the training window.
        var consistent = await sut.AssessAsync(
            DeviantProbe(TrainStart.AddDays(options.MinimumObservations + 5), TrainIp), CancellationToken.None);

        consistent.Origin.Should().Be(AdaptiveAnomalyOrigin.LearnedBaseline);
        consistent.IsLearnedDeviation.Should().BeFalse();
        consistent.DeviationLabels.Should().BeEmpty();
        consistent.LearnedRiskScoreContribution.Should().Be(0);
    }

    // ── Drift handling ──────────────────────────────────────────────────

    [Fact]
    public async Task Drift_GradualScheduleChangeAdaptsWithoutFalseAlarms()
    {
        var options = new AdaptiveAnomalyDetectionOptions { MinimumObservations = 20 };
        using var store = new BaselineStore();
        var sut = CreateService(store, options);

        // Train a stable daily 09:00 rhythm...
        for (var i = 0; i < 30; i++)
        {
            await sut.AssessAsync(TrainAttempt(i), CancellationToken.None);
        }

        // ...then drift the login time gradually later (+2 minutes per day, three hours total).
        // The EWMA decay is the drift mechanism: the baseline follows without raising deviations.
        var minute = 0;
        for (var k = 0; k < 90; k++)
        {
            minute += 2;
            var assessment = await sut.AssessAsync(
                DeviantProbe(TrainStart.AddDays(30 + k).AddMinutes(minute), TrainIp), CancellationToken.None);
            assessment.IsLearnedDeviation.Should().BeFalse(
                $"gradual drift step {k} must adapt the baseline instead of raising a learned deviation");
        }

        // A continuation at the drifted time is unremarkable...
        var drifted = await sut.AssessAsync(
            DeviantProbe(TrainStart.AddDays(120).AddMinutes(minute + 2), TrainIp), CancellationToken.None);
        drifted.IsLearnedDeviation.Should().BeFalse("the baseline now describes the drifted schedule");

        // ...while the ORIGINAL hour is now the deviation: the learned baseline actually moved.
        var oldHourProbe = await sut.AssessAsync(
            DeviantProbe(TrainStart.AddDays(121).AddMinutes(minute + 2).AddHours(-3), TrainIp), CancellationToken.None);
        oldHourProbe.IsLearnedDeviation.Should().BeTrue(
            "after the baseline drifted three hours later, the original hour is the deviation");
    }

    [Fact]
    public async Task Drift_StaleBaselinesAreResetAfterProlongedInactivity()
    {
        var options = new AdaptiveAnomalyDetectionOptions { MinimumObservations = 20, StaleBaselineResetDays = 30 };
        using var store = new BaselineStore();
        var sut = CreateService(store, options);

        for (var i = 0; i < options.MinimumObservations + 5; i++)
        {
            await sut.AssessAsync(TrainAttempt(i), CancellationToken.None);
        }

        // Backdate the persisted baseline far beyond the staleness window.
        var persisted = await store.GetBySubjectKeyAsync(SubjectKey(), CancellationToken.None);
        persisted!.LastObservedAtUtc = DateTime.UtcNow.AddDays(-options.StaleBaselineResetDays - 10);
        await store.SaveChangesAsync(CancellationToken.None);

        var assessment = await sut.AssessAsync(DeviantProbe(TrainStart.AddDays(300).AddHours(2)), CancellationToken.None);

        assessment.Origin.Should().Be(AdaptiveAnomalyOrigin.ColdStart);
        assessment.IsLearnedDeviation.Should().BeFalse();

        // The stale state was replaced by a fresh baseline seeded with this attempt.
        var reset = await store.GetBySubjectKeyAsync(SubjectKey(), CancellationToken.None);
        reset!.ObservationCount.Should().Be(1);
    }

    // ── Sensitivity ─────────────────────────────────────────────────────

    [Theory]
    [InlineData(3.0, true)]
    [InlineData(250.0, false)]
    public async Task Sensitivity_TheZScoreThresholdControlsWhetherADeviationIsFlagged(double threshold, bool expected)
    {
        var options = new AdaptiveAnomalyDetectionOptions { MinimumObservations = 20, ZScoreThreshold = threshold };
        using var store = new BaselineStore();
        var sut = CreateService(store, options);

        for (var i = 0; i < options.MinimumObservations + 5; i++)
        {
            await sut.AssessAsync(TrainAttempt(i), CancellationToken.None);
        }

        var assessment = await sut.AssessAsync(DeviantProbe(TrainStart.AddDays(30).AddHours(2)), CancellationToken.None);

        assessment.IsLearnedDeviation.Should().Be(expected);
    }

    // ── Persistence and isolation ───────────────────────────────────────

    [Fact]
    public async Task SubjectState_PersistsAcrossServiceInstancesAndIsolatesSubjects()
    {
        var options = new AdaptiveAnomalyDetectionOptions { MinimumObservations = 20 };
        using var store = new BaselineStore();
        var trainer = CreateService(store, options);
        for (var i = 0; i < options.MinimumObservations + 5; i++)
        {
            await trainer.AssessAsync(TrainAttempt(i), CancellationToken.None);
        }

        // A fresh service instance (e.g., after a restart) loads the learned state and can issue verdicts.
        var successor = CreateService(store, options);
        var assessment = await successor.AssessAsync(DeviantProbe(TrainStart.AddDays(30).AddHours(2)), CancellationToken.None);
        assessment.Origin.Should().Be(AdaptiveAnomalyOrigin.LearnedBaseline);
        assessment.IsLearnedDeviation.Should().BeTrue();

        // A different subject has its own baseline: it starts cold.
        var otherAssessment = await successor.AssessAsync(new AuthenticationAttemptContext
        {
            UserId = Guid.NewGuid(),
            Identifier = "other-user@example.com",
            TenantId = _tenantId,
            IpAddress = ProbeIp,
            AttemptedAt = TrainStart.AddDays(30).AddHours(2)
        }, CancellationToken.None);
        otherAssessment.Origin.Should().Be(AdaptiveAnomalyOrigin.ColdStart);
    }

    [Fact]
    public void SubjectKey_NeverContainsRawIdentifierOrTenantMaterial()
    {
        var key = AdaptiveAnomalyDetectionService.BuildSubjectKey(_tenantId, null, "secret-identifier@example.com");

        key.Should().HaveLength(64).And.NotContain("secret-identifier");
        key.Should().NotContain(_tenantId.ToString());
        key.Should().NotContain(_tenantId.ToString("N"));
    }

    // ── Fail-open ───────────────────────────────────────────────────────

    [Fact]
    public async Task RepositoryFailure_FailsOpenWithoutThrowing()
    {
        var repository = new Mock<IAdaptiveBehaviorBaselineRepository>();
        repository
            .Setup(repo => repo.GetBySubjectKeyAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("database unavailable"));
        var sut = new AdaptiveAnomalyDetectionService(
            repository.Object,
            new AdaptiveAnomalyDetectionOptions(),
            NullLogger<AdaptiveAnomalyDetectionService>.Instance);

        var assessment = await sut.AssessAsync(TrainAttempt(0), CancellationToken.None);

        assessment.Origin.Should().Be(AdaptiveAnomalyOrigin.AssessmentFailed);
        assessment.IsLearnedDeviation.Should().BeFalse();
        assessment.LearnedRiskScoreContribution.Should().Be(0);
    }

    // ── Repository ──────────────────────────────────────────────────────

    [Fact]
    public async Task BaselineRepository_UpsertInsertsThenUpdatesTheSameRow()
    {
        using var store = new BaselineStore();
        var key = SubjectKey();

        var inserted = await store.UpsertAsync(new AdaptiveBehaviorBaseline
        {
            SubjectKey = key,
            TenantId = _tenantId,
            UserId = _userId,
            ObservationCount = 1
        }, CancellationToken.None);
        var updated = await store.UpsertAsync(new AdaptiveBehaviorBaseline
        {
            SubjectKey = key,
            TenantId = _tenantId,
            UserId = _userId,
            ObservationCount = 2
        }, CancellationToken.None);

        inserted.Id.Should().NotBeEmpty();
        updated.Id.Should().Be(inserted.Id);
        updated.ObservationCount.Should().Be(2);
        (await store.GetBySubjectKeyAsync(key, CancellationToken.None)).Should().NotBeNull();
    }

    // ── Helpers ─────────────────────────────────────────────────────────

    private AdaptiveAnomalyDetectionService CreateService(BaselineStore store, AdaptiveAnomalyDetectionOptions options) =>
        new(store, options, NullLogger<AdaptiveAnomalyDetectionService>.Instance);

    private string SubjectKey() =>
        AdaptiveAnomalyDetectionService.BuildSubjectKey(_tenantId, _userId, "user@example.com");

    /// <summary>A training attempt: daily at 09:00 from the training IP for the shared subject.</summary>
    private AuthenticationAttemptContext TrainAttempt(int dayIndex) => new()
    {
        UserId = _userId,
        Identifier = "user@example.com",
        TenantId = _tenantId,
        IpAddress = TrainIp,
        AttemptedAt = TrainStart.AddDays(dayIndex)
    };

    private AuthenticationAttemptContext DeviantProbe(DateTime attemptedAt, string ipAddress = ProbeIp) => new()
    {
        UserId = _userId,
        Identifier = "user@example.com",
        TenantId = _tenantId,
        IpAddress = ipAddress,
        AttemptedAt = attemptedAt
    };

    /// <summary>In-memory store wrapping a test DbContext that exposes the adaptive baseline set.</summary>
    private sealed class BaselineStore : IAdaptiveBehaviorBaselineRepository, IDisposable
    {
        private readonly TestBaselineDbContext _context;
        private readonly AdaptiveBehaviorBaselineRepository _repository;

        public BaselineStore()
        {
            var options = new DbContextOptionsBuilder<TestBaselineDbContext>()
                .UseInMemoryDatabase($"AdaptiveBaselines_{Guid.NewGuid()}")
                .Options;
            _context = new TestBaselineDbContext(options);
            _repository = new AdaptiveBehaviorBaselineRepository(_context);
        }

        public Task<AdaptiveBehaviorBaseline?> GetBySubjectKeyAsync(string subjectKey, CancellationToken cancellationToken = default) =>
            _context.Baselines.FirstOrDefaultAsync(baseline => baseline.SubjectKey == subjectKey, cancellationToken);

        public Task<AdaptiveBehaviorBaseline> UpsertAsync(AdaptiveBehaviorBaseline baseline, CancellationToken cancellationToken = default) =>
            _repository.UpsertAsync(baseline, cancellationToken);

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
            _context.SaveChangesAsync(cancellationToken);

        public void Dispose() => _context.Dispose();

        /// <summary>
        ///     DbContext implements <see cref="IApplicationDbContext.Set{T}" /> natively, which is what
        ///     the repository consumes; only the baseline entity is modeled.
        /// </summary>
        private sealed class TestBaselineDbContext(DbContextOptions<TestBaselineDbContext> options)
            : DbContext(options), IApplicationDbContext
        {
            public DbSet<AdaptiveBehaviorBaseline> Baselines { get; set; } = null!;

            public Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default) =>
                throw new NotSupportedException();
        }
    }
}
