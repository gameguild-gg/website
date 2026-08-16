using FluentAssertions;
using GameGuild.API.Integration;
using GameGuild.Commerce.Subscriptions;
using Microsoft.Extensions.Options;

namespace GameGuild.API.UnitTests.Core.Integration;

public sealed class MonthlyStatementIntegrationHelpersTests
{
    [Fact]
    public void LinkBuilder_ExpandsStatementTemplatesAndReturnsDashboardPath()
    {
        var options = new SubscriptionNotificationLinkOptions
        {
            WorkspaceLabel = "Billing workspace",
            BillingDashboardPath = "/billing",
            StatementPageTemplate = "/statements?from={from}&to={to}",
            StatementPdfTemplate = "/statements.pdf?from={from}&to={to}",
            StatementCsvTemplate = "/statements.csv?from={from}&to={to}"
        };
        var builder = new MonthlyStatementLinkBuilder(Options.Create(options));

        var links = builder.Build(new DateOnly(2026, 1, 2), new DateOnly(2026, 2, 3));

        links.WorkspaceLabel.Should().Be("Billing workspace");
        links.BillingDashboardPath.Should().Be("/billing");
        links.StatementPagePath.Should().Be("/statements?from=2026-01-02&to=2026-02-03");
        links.StatementPdfPath.Should().Be("/statements.pdf?from=2026-01-02&to=2026-02-03");
        links.StatementCsvPath.Should().Be("/statements.csv?from=2026-01-02&to=2026-02-03");
        builder.GetBillingDashboardPath().Should().Be("/billing");
    }

    [Fact]
    public void LinkOptions_DefaultToEmptyStringsAndAcceptAssignments()
    {
        var options = new SubscriptionNotificationLinkOptions();

        options.WorkspaceLabel.Should().BeEmpty();
        options.BillingDashboardPath.Should().BeEmpty();
        options.StatementPageTemplate.Should().BeEmpty();
        options.StatementPdfTemplate.Should().BeEmpty();
        options.StatementCsvTemplate.Should().BeEmpty();

        options.WorkspaceLabel = "Workspace";
        options.BillingDashboardPath = "/billing";
        options.StatementPageTemplate = "/page";
        options.StatementPdfTemplate = "/pdf";
        options.StatementCsvTemplate = "/csv";

        options.WorkspaceLabel.Should().Be("Workspace");
        options.BillingDashboardPath.Should().Be("/billing");
        options.StatementPageTemplate.Should().Be("/page");
        options.StatementPdfTemplate.Should().Be("/pdf");
        options.StatementCsvTemplate.Should().Be("/csv");
    }

    [Fact]
    public async Task AttachmentBuilder_DelegatesAndComposesArtifacts()
    {
        var tenantId = Guid.NewGuid();
        var fromDate = new DateOnly(2026, 3, 1);
        var toDate = new DateOnly(2026, 3, 31);
        using var cancellation = new CancellationTokenSource();
        var provider = new RecordingStatementDataProvider(CreateContext(tenantId, fromDate, toDate));
        var builder = new MonthlyStatementAttachmentBuilder(provider);

        var artifacts = await builder.BuildAsync(tenantId, fromDate, toDate, cancellation.Token);

        provider.TenantId.Should().Be(tenantId);
        provider.FromDate.Should().Be(fromDate);
        provider.ToDate.Should().Be(toDate);
        provider.CancellationToken.Should().Be(cancellation.Token);
        artifacts.Report.TenantId.Should().Be(tenantId);
        artifacts.Attachments.Select(attachment => attachment.FileName)
            .Should().BeEquivalentTo("monthly-statement.csv", "monthly-statement.pdf");
    }

    private static MonthlyStatementBuildContext CreateContext(Guid tenantId, DateOnly fromDate, DateOnly toDate)
        => new(
            new MonthlyStatementSourceData(
                tenantId,
                new DateTime(2026, 3, 31, 12, 0, 0, DateTimeKind.Utc),
                fromDate,
                toDate,
                0,
                0,
                0,
                0m,
                0m,
                0m,
                0m,
                Array.Empty<StatementCategorySummary>(),
                Array.Empty<StatementPeriodSummary>(),
                Array.Empty<StatementTransactionSummary>(),
                Array.Empty<StatementOwnerSummary>(),
                Array.Empty<StatementRenterSummary>(),
                null),
            new MonthlyStatementDocumentOptions(
                "monthly-statement",
                "Monthly statement",
                MonthlyStatementDocumentProfile.Compact));

    private sealed class RecordingStatementDataProvider(MonthlyStatementBuildContext context)
        : IMonthlyStatementDataProvider
    {
        public Guid TenantId { get; private set; }

        public DateOnly FromDate { get; private set; }

        public DateOnly ToDate { get; private set; }

        public CancellationToken CancellationToken { get; private set; }

        public Task<MonthlyStatementBuildContext> BuildAsync(
            Guid tenantId,
            DateOnly fromDate,
            DateOnly toDate,
            CancellationToken cancellationToken = default)
        {
            TenantId = tenantId;
            FromDate = fromDate;
            ToDate = toDate;
            CancellationToken = cancellationToken;
            return Task.FromResult(context);
        }
    }
}
