using FluentValidation;
using GameGuild.CQRS;
using GameGuild.Identity.Context.Actors;

namespace GameGuild.Commerce.Payments.Queries.RevenueAuditingReports;

/// <summary>Query (issue #404): compliance report for an inclusive period.</summary>
/// <param name="FromUtc">Inclusive period start (UTC).</param>
/// <param name="ToUtc">Inclusive period end (UTC).</param>
/// <param name="TenantId">Optional tenant scope.</param>
public sealed record GetRevenueComplianceReportQuery(
    DateTime FromUtc,
    DateTime ToUtc,
    Guid? TenantId = null) : IQuery<RevenueComplianceReport>;

/// <summary>Query (issue #404): daily net-revenue trend for an inclusive period.</summary>
/// <param name="FromUtc">Inclusive period start (UTC).</param>
/// <param name="ToUtc">Inclusive period end (UTC).</param>
/// <param name="TenantId">Optional tenant scope.</param>
public sealed record GetRevenueTrendReportQuery(
    DateTime FromUtc,
    DateTime ToUtc,
    Guid? TenantId = null) : IQuery<RevenueTrendReport>;

/// <summary>Query (issue #404): export the compliance and trend reports as CSV or JSON.</summary>
/// <param name="FromUtc">Inclusive period start (UTC).</param>
/// <param name="ToUtc">Inclusive period end (UTC).</param>
/// <param name="Format">Export format: "csv" or "json".</param>
/// <param name="TenantId">Optional tenant scope.</param>
public sealed record ExportRevenueReportQuery(
    DateTime FromUtc,
    DateTime ToUtc,
    string Format = "csv",
    Guid? TenantId = null) : IQuery<RevenueReportExport>;

/// <summary>Validator for <see cref="GetRevenueComplianceReportQuery" />.</summary>
public sealed class GetRevenueComplianceReportQueryValidator : AbstractValidator<GetRevenueComplianceReportQuery>
{
    /// <summary>Initializes the validator.</summary>
    public GetRevenueComplianceReportQueryValidator()
    {
        RuleFor(query => query.ToUtc)
            .GreaterThan(query => query.FromUtc).WithMessage("Period end must be after period start.");
    }
}

/// <summary>Validator for <see cref="GetRevenueTrendReportQuery" />.</summary>
public sealed class GetRevenueTrendReportQueryValidator : AbstractValidator<GetRevenueTrendReportQuery>
{
    /// <summary>Initializes the validator.</summary>
    public GetRevenueTrendReportQueryValidator()
    {
        RuleFor(query => query.ToUtc)
            .GreaterThan(query => query.FromUtc).WithMessage("Period end must be after period start.");
    }
}

/// <summary>Validator for <see cref="ExportRevenueReportQuery" />.</summary>
public sealed class ExportRevenueReportQueryValidator : AbstractValidator<ExportRevenueReportQuery>
{
    /// <summary>Initializes the validator.</summary>
    public ExportRevenueReportQueryValidator()
    {
        RuleFor(query => query.ToUtc)
            .GreaterThan(query => query.FromUtc).WithMessage("Period end must be after period start.");
        RuleFor(query => query.Format)
            .Must(format => string.Equals(format, "csv", StringComparison.OrdinalIgnoreCase)
                || string.Equals(format, "json", StringComparison.OrdinalIgnoreCase))
            .WithMessage("Export format must be 'csv' or 'json'.");
    }
}

/// <summary>Handler for <see cref="GetRevenueComplianceReportQuery" />.</summary>
public sealed class GetRevenueComplianceReportQueryHandler(
    IRevenueReportService reportService,
    IActorContextAccessor actorContextAccessor) : IQueryHandler<GetRevenueComplianceReportQuery, RevenueComplianceReport>
{
    /// <inheritdoc />
    public Task<RevenueComplianceReport> Handle(GetRevenueComplianceReportQuery request, CancellationToken cancellationToken)
        => reportService.GetComplianceReportAsync(
            request.FromUtc,
            request.ToUtc,
            ResolveTenantScope(request.TenantId, actorContextAccessor.ActorContext),
            cancellationToken);

    internal static Guid? ResolveTenantScope(Guid? requestedTenantId, ActorContext actorContext)
        => actorContext.IsAuthenticated && !actorContext.IsSystemAdmin ? actorContext.TenantId : requestedTenantId;
}

/// <summary>Handler for <see cref="GetRevenueTrendReportQuery" />.</summary>
public sealed class GetRevenueTrendReportQueryHandler(
    IRevenueReportService reportService,
    IActorContextAccessor actorContextAccessor) : IQueryHandler<GetRevenueTrendReportQuery, RevenueTrendReport>
{
    /// <inheritdoc />
    public Task<RevenueTrendReport> Handle(GetRevenueTrendReportQuery request, CancellationToken cancellationToken)
        => reportService.GetTrendReportAsync(
            request.FromUtc,
            request.ToUtc,
            GetRevenueComplianceReportQueryHandler.ResolveTenantScope(request.TenantId, actorContextAccessor.ActorContext),
            cancellationToken);
}

/// <summary>Handler for <see cref="ExportRevenueReportQuery" />.</summary>
public sealed class ExportRevenueReportQueryHandler(
    IRevenueReportService reportService,
    IActorContextAccessor actorContextAccessor) : IQueryHandler<ExportRevenueReportQuery, RevenueReportExport>
{
    /// <inheritdoc />
    public Task<RevenueReportExport> Handle(ExportRevenueReportQuery request, CancellationToken cancellationToken)
        => reportService.ExportAsync(
            request.FromUtc,
            request.ToUtc,
            request.Format,
            GetRevenueComplianceReportQueryHandler.ResolveTenantScope(request.TenantId, actorContextAccessor.ActorContext),
            cancellationToken);
}
