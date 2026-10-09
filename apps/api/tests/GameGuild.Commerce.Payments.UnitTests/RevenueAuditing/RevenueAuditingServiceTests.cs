using System.Globalization;
using System.Text.Json;
using FluentAssertions;
using GameGuild.Identity.Context.Actors;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace GameGuild.Commerce.Payments.UnitTests.RevenueAuditing;

public class RevenueReconciliationServiceTests
{
    private readonly Mock<IRevenueEventRepository> _revenueEvents = new();
    private readonly Mock<IRevenueReconciliationRepository> _runs = new();
    private readonly Mock<IExternalRevenueStatementSource> _statementSource = new();
    private readonly RevenueAuditingOptions _options = new() { MaxStatementLinesPerRun = 5 };

    private RevenueReconciliationService CreateService()
    {
        return new RevenueReconciliationService(
            _revenueEvents.Object,
            _runs.Object,
            _statementSource.Object,
            Options.Create(_options),
            NullLogger<RevenueReconciliationService>.Instance);
    }

    private static RevenueEvent InternalEvent(
        string referenceId,
        decimal amount,
        string currency = "USD",
        RevenueEventStatus status = RevenueEventStatus.Processed,
        DateTime? timestamp = null)
        => new()
        {
            ReferenceId = referenceId,
            Amount = amount,
            Currency = currency,
            Status = status,
            Timestamp = timestamp ?? new DateTime(2026, 09, 30, 12, 0, 0, DateTimeKind.Utc)
        };

    private static ExternalRevenueStatementLine Line(string referenceId, decimal amount, string currency = "USD")
        => new(referenceId, amount, currency, new DateTime(2026, 09, 30, 12, 0, 0, DateTimeKind.Utc));

    [Fact]
    public async Task ReconcileAsync_MatchesCleanStatementWithoutDiscrepancies()
    {
        _revenueEvents
            .Setup(repository => repository.GetInPeriodAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([InternalEvent("ref-1", 100m), InternalEvent("ref-2", 25.5m)]);

        var run = await CreateService().ReconcileAsync(new RevenueReconciliationRequest(
            TenantId: null,
            Source: "stripe-payouts",
            PeriodStartUtc: new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc),
            PeriodEndUtc: new DateTime(2026, 9, 30, 23, 59, 59, DateTimeKind.Utc),
            Lines: [Line("ref-1", 100m), Line("ref-2", 25.5m)]));

        run.Status.Should().Be(RevenueReconciliationStatus.Completed);
        run.MatchedCount.Should().Be(2);
        run.DiscrepancyCount.Should().Be(0);
        run.CompletedAtUtc.Should().NotBeNull();
        _runs.Verify(repository => repository.AddRunAsync(run, It.IsAny<CancellationToken>()), Times.Once);
        _runs.Verify(repository => repository.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task ReconcileAsync_RecordsEveryDiscrepancyKind()
    {
        _revenueEvents
            .Setup(repository => repository.GetInPeriodAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                InternalEvent("match", 50m),
                InternalEvent("amount-off", 50m),
                InternalEvent("currency-off", 50m, "EUR"),
                InternalEvent("never-on-statement", 10m),
                InternalEvent("cancelled", 10m, status: RevenueEventStatus.Cancelled)
            ]);

        var run = await CreateService().ReconcileAsync(new RevenueReconciliationRequest(
            TenantId: null,
            Source: "manual-export",
            PeriodStartUtc: new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc),
            PeriodEndUtc: new DateTime(2026, 9, 30, 23, 59, 59, DateTimeKind.Utc),
            Lines:
            [
                Line("match", 50m),
                Line("amount-off", 99m),
                Line("currency-off", 50m, "BRL"),
                Line("not-recorded", 7m),
                Line("match", 50m) // duplicate external reference
            ]));

        run.MatchedCount.Should().Be(1);
        run.DiscrepancyCount.Should().Be(5);

        _runs.Verify(
            repository => repository.AddDiscrepancyAsync(
                It.Is<RevenueReconciliationDiscrepancy>(discrepancy => discrepancy.Kind == RevenueDiscrepancyKind.MissingInternal),
                It.IsAny<CancellationToken>()),
            Times.Once);
        _runs.Verify(
            repository => repository.AddDiscrepancyAsync(
                It.Is<RevenueReconciliationDiscrepancy>(discrepancy => discrepancy.Kind == RevenueDiscrepancyKind.MissingExternal),
                It.IsAny<CancellationToken>()),
            Times.Once);
        _runs.Verify(
            repository => repository.AddDiscrepancyAsync(
                It.Is<RevenueReconciliationDiscrepancy>(discrepancy => discrepancy.Kind == RevenueDiscrepancyKind.AmountMismatch),
                It.IsAny<CancellationToken>()),
            Times.Once);
        _runs.Verify(
            repository => repository.AddDiscrepancyAsync(
                It.Is<RevenueReconciliationDiscrepancy>(discrepancy => discrepancy.Kind == RevenueDiscrepancyKind.CurrencyMismatch),
                It.IsAny<CancellationToken>()),
            Times.Once);
        _runs.Verify(
            repository => repository.AddDiscrepancyAsync(
                It.Is<RevenueReconciliationDiscrepancy>(discrepancy => discrepancy.Kind == RevenueDiscrepancyKind.DuplicateExternalReference),
                It.IsAny<CancellationToken>()),
            Times.Once);

        // Cancelled internal events never participate: they are neither matched nor flagged MissingExternal.
        var summary = JsonDocument.Parse(run.SummaryJson!).RootElement;
        summary.GetProperty("missingExternal").GetInt32().Should().Be(1);
        summary.GetProperty("missingInternal").GetInt32().Should().Be(1);
        summary.GetProperty("amountMismatch").GetInt32().Should().Be(1);
        summary.GetProperty("currencyMismatch").GetInt32().Should().Be(1);
        summary.GetProperty("duplicateExternalReference").GetInt32().Should().Be(1);
    }

    [Fact]
    public async Task ReconcileAsync_RejectsStatementsAboveTheLineLimit()
    {
        var oversized = Enumerable.Range(0, 6).Select(index => Line($"ref-{index}", 10m)).ToList();

        var act = () => CreateService().ReconcileAsync(new RevenueReconciliationRequest(
            TenantId: null,
            Source: "manual-export",
            PeriodStartUtc: new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc),
            PeriodEndUtc: new DateTime(2026, 9, 30, 23, 59, 59, DateTimeKind.Utc),
            Lines: oversized));

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*exceeds the maximum*");
    }

    [Fact]
    public async Task ReconcileAsync_FallsBackToConfiguredExternalSource_WhenNoInlineLines()
    {
        _statementSource
            .Setup(source => source.GetLinesAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([Line("src-1", 42m)]);
        _revenueEvents
            .Setup(repository => repository.GetInPeriodAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([InternalEvent("src-1", 42m)]);

        var run = await CreateService().ReconcileAsync(new RevenueReconciliationRequest(
            TenantId: Guid.NewGuid(),
            Source: "erp",
            PeriodStartUtc: new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc),
            PeriodEndUtc: new DateTime(2026, 9, 30, 23, 59, 59, DateTimeKind.Utc),
            Lines: null));

        run.StatementLineCount.Should().Be(1);
        run.MatchedCount.Should().Be(1);
        _statementSource.Verify(
            source => source.GetLinesAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ReconcileAsync_MarksRunFailed_WhenMatchingThrows()
    {
        _revenueEvents
            .Setup(repository => repository.GetInPeriodAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("database exploded"));

        var request = new RevenueReconciliationRequest(
            TenantId: null,
            Source: "stripe-payouts",
            PeriodStartUtc: new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc),
            PeriodEndUtc: new DateTime(2026, 9, 30, 23, 59, 59, DateTimeKind.Utc),
            Lines: []);

        var act = () => CreateService().ReconcileAsync(request);

        await act.Should().ThrowAsync<InvalidOperationException>();
        request.Should().NotBeNull();
        _runs.Verify(
            repository => repository.UpdateRunAsync(
                It.Is<RevenueReconciliationRun>(run => run.Status == RevenueReconciliationStatus.Failed && run.FailureReason == "database exploded"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }
}

public class RevenueAnomalyServiceTests
{
    private readonly Mock<IRevenueEventRepository> _revenueEvents = new();
    private readonly Mock<IRevenueAnomalyAlertRepository> _alerts = new();
    private readonly RevenueAuditingOptions _options = new()
    {
        AnomalyEvaluationDays = 1,
        AnomalyBaselineDays = 7,
        AnomalyMinBaselineDays = 5,
        AnomalyZScoreThreshold = 3.0m
    };

    private RevenueAnomalyService CreateService()
    {
        return new RevenueAnomalyService(
            _revenueEvents.Object,
            _alerts.Object,
            Options.Create(_options),
            NullLogger<RevenueAnomalyService>.Instance);
    }

    private static RevenueDailyTotal Day(string date, decimal net)
        => new(DateTime.Parse(date, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal), 10m, 10m - net, net, 3);

    [Fact]
    public async Task DetectAsync_SkipsEvaluation_WhenBaselineIsTooThin()
    {
        _revenueEvents
            .Setup(repository => repository.GetDailyTotalsAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([Day("2026-09-20", 100m), Day("2026-09-21", 100m)]);

        var candidates = await CreateService().DetectAsync(new DateTime(2026, 9, 28, 0, 0, 0, DateTimeKind.Utc), null);

        candidates.Should().BeEmpty();
    }

    [Fact]
    public async Task DetectAsync_FlagsSpikeAndDrop_AboveTheZScoreThreshold()
    {
        var totals = new List<RevenueDailyTotal>();
        for (var day = 21; day <= 27; day++)
        {
            totals.Add(Day($"2026-09-{day}", 100m)); // flat baseline of 7 active days
        }

        totals.Add(Day("2026-09-28", 1000m)); // evaluation day: massive spike

        _revenueEvents
            .Setup(repository => repository.GetDailyTotalsAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(totals);

        var candidates = await CreateService().DetectAsync(new DateTime(2026, 9, 28, 0, 0, 0, DateTimeKind.Utc), null);

        var candidate = candidates.Should().ContainSingle().Subject;
        candidate.Kind.Should().Be(RevenueAnomalyKind.Spike);
        candidate.ObservedNetRevenue.Should().Be(1000m);
        candidate.ExpectedNetRevenue.Should().Be(100m);
        candidate.ZScore.Should().BePositive().And.BeGreaterThan(3m);
        candidate.BaselineDays.Should().Be(7);
    }

    [Fact]
    public async Task DetectAsync_FlagsDrop_WhenRevenueCollapses()
    {
        var totals = new List<RevenueDailyTotal>();
        for (var day = 21; day <= 27; day++)
        {
            totals.Add(Day($"2026-09-{day}", 100m));
        }

        totals.Add(Day("2026-09-28", 0m)); // total collapse

        _revenueEvents
            .Setup(repository => repository.GetDailyTotalsAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(totals);

        var candidates = await CreateService().DetectAsync(new DateTime(2026, 9, 28, 0, 0, 0, DateTimeKind.Utc), null);

        candidates.Should().ContainSingle()
            .Which.Kind.Should().Be(RevenueAnomalyKind.Drop);
    }

    [Fact]
    public async Task DetectAndPersistAsync_SkipsDaysAlreadyAlerted_AndSavesOnce()
    {
        _revenueEvents
            .Setup(repository => repository.GetDailyTotalsAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                Day("2026-09-21", 100m), Day("2026-09-22", 100m), Day("2026-09-23", 100m),
                Day("2026-09-24", 100m), Day("2026-09-25", 100m), Day("2026-09-26", 100m),
                Day("2026-09-27", 100m),
                Day("2026-09-28", 900m),
            ]);
        _alerts
            .Setup(alerts => alerts.ExistsForDayAsync(RevenueAnomalyKind.Spike, It.IsAny<DateTime>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var created = await CreateService().DetectAndPersistAsync(new DateTime(2026, 9, 28, 0, 0, 0, DateTimeKind.Utc), null);

        created.Should().Be(0);
        _alerts.Verify(alerts => alerts.AddAsync(It.IsAny<RevenueAnomalyAlert>(), It.IsAny<CancellationToken>()), Times.Never);
        _alerts.Verify(alerts => alerts.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task DetectAndPersistAsync_PersistsNewAlert_WithBaselineEvidence()
    {
        _revenueEvents
            .Setup(repository => repository.GetDailyTotalsAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                Day("2026-09-21", 100m), Day("2026-09-22", 100m), Day("2026-09-23", 100m),
                Day("2026-09-24", 100m), Day("2026-09-25", 100m), Day("2026-09-26", 100m),
                Day("2026-09-27", 100m),
                Day("2026-09-28", 900m),
            ]);
        _alerts
            .Setup(alerts => alerts.ExistsForDayAsync(It.IsAny<RevenueAnomalyKind>(), It.IsAny<DateTime>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var created = await CreateService().DetectAndPersistAsync(new DateTime(2026, 9, 28, 0, 0, 0, DateTimeKind.Utc), tenantId: Guid.NewGuid());

        created.Should().Be(1);
        _alerts.Verify(
            alerts => alerts.AddAsync(
                It.Is<RevenueAnomalyAlert>(alert =>
                    alert.Kind == RevenueAnomalyKind.Spike
                    && alert.ObservedNetRevenue == 900m
                    && alert.ExpectedNetRevenue == 100m
                    && alert.Status == RevenueAnomalyStatus.Open),
                It.IsAny<CancellationToken>()),
            Times.Once);
        _alerts.Verify(alerts => alerts.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}

public class RevenueReportServiceTests
{
    private readonly Mock<IRevenueEventRepository> _revenueEvents = new();
    private readonly Mock<IRevenueReconciliationRepository> _runs = new();

    private RevenueReportService CreateService()
        => new(_revenueEvents.Object, _runs.Object);

    [Fact]
    public async Task GetComplianceReportAsync_AggregatesTotalsUncountedAndReconciliation()
    {
        _revenueEvents
            .Setup(repository => repository.GetGroupedTotalsAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<Guid?>(), RevenueEventTotalGrouping.EventType, It.IsAny<CancellationToken>()))
            .ReturnsAsync([new RevenueEventGroupTotal(nameof(RevenueEventType.PaymentReceived), 2, 150m)]);
        _revenueEvents
            .Setup(repository => repository.GetGroupedTotalsAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<Guid?>(), RevenueEventTotalGrouping.Source, It.IsAny<CancellationToken>()))
            .ReturnsAsync([new RevenueEventGroupTotal(nameof(RevenueSource.Subscription), 2, 150m)]);
        _revenueEvents
            .Setup(repository => repository.GetGroupedTotalsAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<Guid?>(), RevenueEventTotalGrouping.Status, It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                new RevenueEventGroupTotal(nameof(RevenueEventStatus.Processed), 2, 150m),
                new RevenueEventGroupTotal(nameof(RevenueEventStatus.Pending), 1, 30m)
            ]);
        _runs
            .Setup(repository => repository.GetRunsOverlappingPeriodAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                new RevenueReconciliationRun
                {
                    Status = RevenueReconciliationStatus.Completed,
                    MatchedCount = 2,
                    DiscrepancyCount = 1,
                    CompletedAtUtc = new DateTime(2026, 9, 30, 6, 0, 0, DateTimeKind.Utc)
                }
            ]);

        var report = await CreateService().GetComplianceReportAsync(
            new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 9, 30, 23, 59, 59, DateTimeKind.Utc),
            null);

        report.TotalsByEventType.Should().ContainSingle().Which.Total.Should().Be(150m);
        report.UncountedEventCount.Should().Be(1);
        report.Reconciliation.ReconciliationRuns.Should().Be(1);
        report.Reconciliation.MatchedLines.Should().Be(2);
        report.Reconciliation.Discrepancies.Should().Be(1);
        report.Attestation.Should().Contain("3 revenue events recorded");
        report.Attestation.Should().Contain("1 events not yet processed");
    }

    [Fact]
    public async Task GetTrendReportAsync_IncludesZeroActivityDays_AndComputesRangeTotals()
    {
        var from = new DateTime(2026, 9, 28, 0, 0, 0, DateTimeKind.Utc);
        var to = new DateTime(2026, 9, 30, 0, 0, 0, DateTimeKind.Utc);
        _revenueEvents
            .Setup(repository => repository.GetDailyTotalsAsync(from, to, It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                new RevenueDailyTotal(new DateTime(2026, 9, 28, 0, 0, 0, DateTimeKind.Utc), 100m, 20m, 80m, 4),
                new RevenueDailyTotal(new DateTime(2026, 9, 30, 0, 0, 0, DateTimeKind.Utc), 10m, 0m, 10m, 1)
            ]);

        var trend = await CreateService().GetTrendReportAsync(from, to, null);

        trend.Points.Should().HaveCount(3);
        trend.Points[1].EventCount.Should().Be(0);
        trend.Points[1].NetTotal.Should().Be(0m);
        trend.TotalCredit.Should().Be(110m);
        trend.TotalDebit.Should().Be(20m);
        trend.TotalNet.Should().Be(90m);
    }

    [Theory]
    [InlineData("xml")]
    [InlineData("csv;DROP TABLE users")]
    [InlineData("")]
    public async Task ExportAsync_RejectsFormatsOtherThanCsvOrJson(string format)
    {
        var act = () => CreateService().ExportAsync(
            new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 9, 30, 0, 0, 0, DateTimeKind.Utc),
            format,
            null);

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*csv*json*");
    }

    [Fact]
    public async Task ExportAsync_ProducesRfc4180Csv_WithAllSections()
    {
        _revenueEvents
            .Setup(repository => repository.GetGroupedTotalsAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<Guid?>(), It.IsAny<RevenueEventTotalGrouping>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([new RevenueEventGroupTotal(nameof(RevenueEventType.PaymentReceived), 1, 10m)]);
        _revenueEvents
            .Setup(repository => repository.GetDailyTotalsAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([new RevenueDailyTotal(new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc), 10m, 0m, 10m, 1)]);
        _runs
            .Setup(repository => repository.GetRunsOverlappingPeriodAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var export = await CreateService().ExportAsync(
            new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc).AddDays(1),
            "csv",
            null);

        export.FileName.Should().Be("revenue-audit-20260901-20260902.csv");
        export.ContentType.Should().Be("text/csv; charset=utf-8");
        export.Content.SplitLines().First().Should().Be("section,key,count,total");
        export.Content.Should().Contain($"event_type,{nameof(RevenueEventType.PaymentReceived)},1,10.00");
        export.Content.Should().Contain("daily,2026-09-01,1,10.00");
        export.Content.Should().Contain("daily,2026-09-02,0,0.00");
    }

    [Fact]
    public async Task ExportAsync_ProducesJsonExport_ContainingComplianceAndTrend()
    {
        _revenueEvents
            .Setup(repository => repository.GetGroupedTotalsAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<Guid?>(), It.IsAny<RevenueEventTotalGrouping>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        _revenueEvents
            .Setup(repository => repository.GetDailyTotalsAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        _runs
            .Setup(repository => repository.GetRunsOverlappingPeriodAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var export = await CreateService().ExportAsync(
            new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 9, 2, 0, 0, 0, DateTimeKind.Utc),
            "json",
            null);

        export.FileName.Should().Be("revenue-audit-20260901-20260902.json");
        export.ContentType.Should().Be("application/json");
        var document = JsonDocument.Parse(export.Content);
        document.RootElement.GetProperty("compliance").GetProperty("attestation").GetString().Should().NotBeNullOrEmpty();
        document.RootElement.GetProperty("trend").GetProperty("points").GetArrayLength().Should().Be(2);
    }
}

public class RevenueAuditingEntityTests
{
    [Fact]
    public void RevenueReconciliationRun_CompleteAndFail_AreOnceOnly()
    {
        var run = new RevenueReconciliationRun();
        run.Complete(1, 0, "{}");

        run.Status.Should().Be(RevenueReconciliationStatus.Completed);
        run.MatchedCount.Should().Be(1);

        var completeAgain = () => run.Complete(2, 0, "{}");
        var failAfterComplete = () => run.Fail("boom");

        completeAgain.Should().Throw<InvalidOperationException>().WithMessage("*immutable*");
        failAfterComplete.Should().Throw<InvalidOperationException>().WithMessage("*immutable*");
    }

    [Fact]
    public void RevenueAnomalyAlert_Acknowledge_RecordsOperatorAndRejectsDoubleAcknowledge()
    {
        var alert = new RevenueAnomalyAlert
        {
            Kind = RevenueAnomalyKind.Drop,
            DetectedForDateUtc = new DateTime(2026, 9, 28, 0, 0, 0, DateTimeKind.Utc)
        };
        var operatorId = Guid.NewGuid();

        alert.Acknowledge(operatorId, "investigated; caused by a gateway outage");

        alert.Status.Should().Be(RevenueAnomalyStatus.Acknowledged);
        alert.AcknowledgedByUserId.Should().Be(operatorId);
        alert.AcknowledgedAtUtc.Should().NotBeNull();
        alert.AcknowledgementNotes.Should().Be("investigated; caused by a gateway outage");

        var acknowledgeAgain = () => alert.Acknowledge(Guid.NewGuid());
        acknowledgeAgain.Should().Throw<InvalidOperationException>().WithMessage("*already acknowledged*");
    }
}

public class RevenueAuditingOptionsValidatorTests
{
    private readonly RevenueAuditingOptionsValidator _validator = new();

    [Fact]
    public void DefaultOptions_AreValid()
    {
        var result = _validator.Validate(null, new RevenueAuditingOptions());
        result.Succeeded.Should().BeTrue();
    }

    [Fact]
    public void OutOfRangeValues_FailValidation()
    {
        var result = _validator.Validate(null, new RevenueAuditingOptions
        {
            AnomalyEvaluationDays = 0,
            AnomalyZScoreThreshold = 0.5m,
            WorkerEnabled = true,
            WorkerIntervalMinutes = 1
        });

        result.Succeeded.Should().BeFalse();
        result.Failures.Should().Contain(failure => failure.Contains(nameof(RevenueAuditingOptions.AnomalyEvaluationDays)));
        result.Failures.Should().Contain(failure => failure.Contains(nameof(RevenueAuditingOptions.AnomalyZScoreThreshold)));
        result.Failures.Should().Contain(failure => failure.Contains(nameof(RevenueAuditingOptions.WorkerIntervalMinutes)));
    }

    [Fact]
    public void MinBaselineDays_AboveBaselineDays_FailsValidation()
    {
        var result = _validator.Validate(null, new RevenueAuditingOptions
        {
            AnomalyBaselineDays = 7,
            AnomalyMinBaselineDays = 30
        });

        result.Succeeded.Should().BeFalse();
        result.Failures.Should().Contain(failure => failure.Contains(nameof(RevenueAuditingOptions.AnomalyMinBaselineDays)));
    }
}

public class ConfigurationExternalRevenueStatementSourceTests
{
    [Fact]
    public async Task GetLinesAsync_FiltersAndOrders_ByPeriodAndProviderOrder()
    {
        var options = Options.Create(new RevenueAuditingOptions
        {
            StatementSource = new RevenueStatementSourceOptions
            {
                ProviderName = "erp-adapter",
                Lines =
                [
                    new ExternalRevenueStatementLine("late", 5m, "USD", new DateTime(2026, 9, 30, 0, 0, 0, DateTimeKind.Utc)),
                    new ExternalRevenueStatementLine("early", 5m, "USD", new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc)),
                    new ExternalRevenueStatementLine("outside", 5m, "USD", new DateTime(2026, 10, 5, 0, 0, 0, DateTimeKind.Utc))
                ]
            }
        });
        var source = new ConfigurationExternalRevenueStatementSource(options);

        source.ProviderName.Should().Be("erp-adapter");
        var lines = await source.GetLinesAsync(
            new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 9, 30, 23, 59, 59, DateTimeKind.Utc));

        lines.Select(line => line.ReferenceId).Should().Equal("early", "late");
    }

    [Fact]
    public async Task GetLinesAsync_ReportsManualExport_WhenNoProviderConfigured()
    {
        var source = new ConfigurationExternalRevenueStatementSource(Options.Create(new RevenueAuditingOptions()));

        source.ProviderName.Should().Be("manual-export");
        (await source.GetLinesAsync(DateTime.MinValue, DateTime.MaxValue)).Should().BeEmpty();
    }
}

file static class StringSplitExtensions
{
    public static IEnumerable<string> SplitLines(this string text)
    {
        using var reader = new StringReader(text);
        while (reader.ReadLine() is { } line)
        {
            yield return line;
        }
    }
}
