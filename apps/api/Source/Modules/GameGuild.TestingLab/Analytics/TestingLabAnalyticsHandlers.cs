using System.Globalization;
using System.Text;
using GameGuild.CQRS;
using GameGuild.Identity.Context.Actors;
using GameGuild.Identity.Tenants;
using GameGuild.Identity.Users;

namespace GameGuild.TestingLab;

public sealed class TestingLabAnalyticsHandlers(
    IApplicationDbContext context,
    IActorContextAccessor actorContextAccessor) :
    IQueryHandler<GetTestingLabAnalyticsReportQuery, Result<TestingLabAnalyticsReportProjection>>,
    IQueryHandler<ExportTestingLabAnalyticsReportQuery, Result<TestingLabAnalyticsExportProjection>>
{
    private static readonly TimeSpan DefaultPeriod = TimeSpan.FromDays(30);
    private static readonly TimeSpan MaximumPeriod = TimeSpan.FromDays(366);

    public async Task<Result<TestingLabAnalyticsReportProjection>> Handle(
        GetTestingLabAnalyticsReportQuery request,
        CancellationToken cancellationToken)
    {
        var actor = await RequireActorAsync(cancellationToken).ConfigureAwait(false);
        if (actor.Error != null)
        {
            return Result.Failure<TestingLabAnalyticsReportProjection>(actor.Error);
        }

        var period = NormalizePeriod(request.FromDate, request.ToDate);
        if (period.Error != null)
        {
            return Result.Failure<TestingLabAnalyticsReportProjection>(period.Error);
        }

        var current = await LoadWindowAsync(
            actor.TenantId,
            period.FromDate,
            period.ToDate,
            includeDetails: true,
            cancellationToken).ConfigureAwait(false);

        TestingLabAnalyticsSummaryProjection? previous = null;
        if (request.IncludeComparison)
        {
            var duration = period.ToDate - period.FromDate;
            previous = (await LoadWindowAsync(
                actor.TenantId,
                period.FromDate - duration,
                period.FromDate,
                includeDetails: false,
                cancellationToken).ConfigureAwait(false)).Summary;
        }

        var locations = await context.Set<TestingLocation>()
            .AsNoTracking()
            .Where(location => location.TenantId == actor.TenantId && location.DeletedAt == null)
            .Select(location => location.Status)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return Result.Success(new TestingLabAnalyticsReportProjection(
            period.FromDate,
            period.ToDate,
            SystemClock.UtcNow,
            current.Summary,
            previous,
            new TestingLabLocationAnalyticsProjection(
                locations.Count,
                locations.Count(status => status == LocationStatus.Active)),
            current.Trend,
            current.Events));
    }

    public async Task<Result<TestingLabAnalyticsExportProjection>> Handle(
        ExportTestingLabAnalyticsReportQuery request,
        CancellationToken cancellationToken)
    {
        var report = await Handle(
            new GetTestingLabAnalyticsReportQuery(request.FromDate, request.ToDate, IncludeComparison: false),
            cancellationToken).ConfigureAwait(false);
        if (report.IsFailure)
        {
            return Result.Failure<TestingLabAnalyticsExportProjection>(report.Error);
        }

        var value = report.Value;
        var builder = new StringBuilder();
        builder.AppendLine("Event,Status,Mode,Starts at,Applications,Approved projects,Registered testers,Attended testers,Feedback,Average rating,Capacity,Fill rate");
        foreach (var item in value.Events)
        {
            builder.Append(EscapeCsv(item.Name)).Append(',')
                .Append(item.Status).Append(',')
                .Append(item.Mode).Append(',')
                .Append(item.StartsAt.ToString("O", CultureInfo.InvariantCulture)).Append(',')
                .Append(item.Applications).Append(',')
                .Append(item.ApprovedProjects).Append(',')
                .Append(item.RegisteredTesters).Append(',')
                .Append(item.AttendedTesters).Append(',')
                .Append(item.Feedback).Append(',')
                .Append(FormatNumber(item.AverageRating)).Append(',')
                .Append(item.Capacity).Append(',')
                .Append(FormatNumber(item.FillRate))
                .AppendLine();
        }

        var inclusiveEnd = value.ToDate.AddTicks(-1);
        return Result.Success(new TestingLabAnalyticsExportProjection(
            "text/csv",
            $"testing-lab-{value.FromDate:yyyyMMdd}-{inclusiveEnd:yyyyMMdd}.csv",
            Encoding.UTF8.GetBytes(builder.ToString())));
    }

    private async Task<WindowData> LoadWindowAsync(
        Guid tenantId,
        DateTime fromDate,
        DateTime toDate,
        bool includeDetails,
        CancellationToken cancellationToken)
    {
        var events = await context.Set<TestingEvent>()
            .AsNoTracking()
            .Where(item =>
                item.TenantId == tenantId &&
                item.DeletedAt == null &&
                item.StartsAt >= fromDate &&
                item.StartsAt < toDate)
            .OrderByDescending(item => item.StartsAt)
            .Select(item => new EventData(item.Id, item.Name, item.Status, item.Mode, item.StartsAt))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var applications = await context.Set<TestingProjectApplication>()
            .AsNoTracking()
            .Where(item =>
                item.TenantId == tenantId &&
                item.DeletedAt == null &&
                item.CreatedAt >= fromDate &&
                item.CreatedAt < toDate)
            .Select(item => new ApplicationData(item.EventId, item.Status, item.CreatedAt))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var registrations = await context.Set<TestingSlotRegistration>()
            .AsNoTracking()
            .Where(item =>
                item.TenantId == tenantId &&
                item.DeletedAt == null &&
                item.CreatedAt >= fromDate &&
                item.CreatedAt < toDate &&
                item.Status != TestingSlotRegistrationStatus.Cancelled &&
                item.Status != TestingSlotRegistrationStatus.Waitlisted)
            .Select(item => new RegistrationData(item.EventId, item.Status, item.CreatedAt))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var feedback = await context.Set<TestingFeedback>()
            .AsNoTracking()
            .Where(item =>
                item.TenantId == tenantId &&
                item.DeletedAt == null &&
                item.EventId != null &&
                item.CreatedAt >= fromDate &&
                item.CreatedAt < toDate)
            .Select(item => new FeedbackData(item.EventId!.Value, item.OverallRating, item.WouldRecommend, item.CreatedAt))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        // Capacity must cover both events occurring in the reporting window and
        // events that received registrations during it. Registrations are an
        // activity metric (CreatedAt), so an upcoming event can legitimately
        // contribute registrations before its StartsAt enters the same window.
        var eventIds = events.Select(item => item.Id)
            .Concat(registrations.Select(item => item.EventId))
            .Distinct()
            .ToArray();
        var slots = await context.Set<TestingEventSlot>()
            .AsNoTracking()
            .Where(item =>
                item.TenantId == tenantId &&
                item.DeletedAt == null &&
                eventIds.Contains(item.EventId))
            .Select(item => new SlotData(item.EventId, item.MaxTesters))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var summary = BuildSummary(events, applications, registrations, feedback, slots);
        if (!includeDetails)
        {
            return new WindowData(summary, [], []);
        }

        var eventRows = events.Select(item =>
        {
            var eventApplications = applications.Where(candidate => candidate.EventId == item.Id).ToList();
            var eventRegistrations = registrations.Where(candidate => candidate.EventId == item.Id).ToList();
            var eventFeedback = feedback.Where(candidate => candidate.EventId == item.Id).ToList();
            var capacity = slots.Where(candidate => candidate.EventId == item.Id).Sum(candidate => candidate.MaxTesters ?? 0);
            return new TestingLabEventAnalyticsProjection(
                item.Id,
                item.Name,
                item.Status,
                item.Mode,
                item.StartsAt,
                eventApplications.Count,
                eventApplications.Count(candidate => candidate.Status == TestingApplicationStatus.Approved),
                eventRegistrations.Count,
                eventRegistrations.Count(candidate => IsAttended(candidate.Status)),
                eventFeedback.Count,
                AverageRating(eventFeedback),
                capacity,
                Percentage(eventRegistrations.Count, capacity));
        }).ToList();

        var dayCount = (int)Math.Ceiling((toDate - fromDate).TotalDays);
        var trend = Enumerable.Range(0, dayCount)
            .Select(offset =>
            {
                var date = fromDate.Date.AddDays(offset);
                var next = date.AddDays(1);
                return new TestingLabAnalyticsTrendProjection(
                    date,
                    events.Count(item => item.StartsAt >= date && item.StartsAt < next),
                    applications.Count(item => item.CreatedAt >= date && item.CreatedAt < next),
                    registrations.Count(item => item.CreatedAt >= date && item.CreatedAt < next),
                    registrations.Count(item => item.CreatedAt >= date && item.CreatedAt < next && IsAttended(item.Status)),
                    feedback.Count(item => item.CreatedAt >= date && item.CreatedAt < next));
            })
            .ToList();

        return new WindowData(summary, trend, eventRows);
    }

    private static TestingLabAnalyticsSummaryProjection BuildSummary(
        IReadOnlyCollection<EventData> events,
        IReadOnlyCollection<ApplicationData> applications,
        IReadOnlyCollection<RegistrationData> registrations,
        IReadOnlyCollection<FeedbackData> feedback,
        IReadOnlyCollection<SlotData> slots)
    {
        var capacity = slots.Sum(item => item.MaxTesters ?? 0);
        var ratings = feedback.Where(item => item.OverallRating.HasValue).Select(item => item.OverallRating!.Value).ToList();
        var recommendations = feedback.Where(item => item.WouldRecommend.HasValue).Select(item => item.WouldRecommend!.Value).ToList();
        return new TestingLabAnalyticsSummaryProjection(
            events.Count,
            events.Count(item => item.Status == TestingEventStatus.Completed),
            applications.Count,
            applications.Count(item => item.Status == TestingApplicationStatus.Approved),
            registrations.Count,
            registrations.Count(item => IsAttended(item.Status)),
            feedback.Count,
            ratings.Count == 0 ? null : Math.Round((decimal)ratings.Average(), 2),
            recommendations.Count == 0 ? null : Percentage(recommendations.Count(value => value), recommendations.Count),
            capacity,
            Percentage(registrations.Count, capacity));
    }

    private async Task<ActorScope> RequireActorAsync(CancellationToken cancellationToken)
    {
        var actor = actorContextAccessor.ActorContext;
        var userId = actor.SubjectIdAsGuid;
        if (!actor.IsAuthenticated || userId == null || actor.TenantId == null)
        {
            return new(Guid.Empty, Error.Unauthorized(
                "TestingLab.Unauthenticated",
                "An authenticated tenant actor is required."));
        }

        var hasAccess = await TestingLabActorAccess.IsActiveTenantActorAsync(context, actor, cancellationToken).ConfigureAwait(false);
        return hasAccess
            ? new(actor.TenantId.Value, null)
            : new(Guid.Empty, Error.Unauthorized(
                "TestingLab.InactiveActor",
                "An active user and tenant membership are required."));
    }

    private static PeriodScope NormalizePeriod(DateTime? fromDate, DateTime? toDate)
    {
        var end = (toDate ?? SystemClock.UtcNow.Date.AddDays(1)).ToUniversalTime();
        var start = (fromDate ?? end.Subtract(DefaultPeriod)).ToUniversalTime();
        if (start >= end || end - start > MaximumPeriod)
        {
            return new(default, default, Error.Validation(
                "TestingLab.InvalidAnalyticsPeriod",
                "Analytics period must be positive and no longer than 366 days."));
        }

        return new(start, end, null);
    }

    private static bool IsAttended(TestingSlotRegistrationStatus status) => status is
        TestingSlotRegistrationStatus.Attended or TestingSlotRegistrationStatus.Completed;

    private static decimal? AverageRating(IReadOnlyCollection<FeedbackData> feedback)
    {
        var ratings = feedback.Where(item => item.OverallRating.HasValue).Select(item => item.OverallRating!.Value).ToList();
        return ratings.Count == 0 ? null : Math.Round((decimal)ratings.Average(), 2);
    }

    private static decimal Percentage(int numerator, int denominator) =>
        denominator <= 0 ? 0 : Math.Round((decimal)numerator / denominator * 100, 2);

    private static string FormatNumber(decimal? value) =>
        value?.ToString("0.##", CultureInfo.InvariantCulture) ?? string.Empty;

    private static string EscapeCsv(string value) =>
        value.IndexOfAny([',', '"', '\r', '\n']) >= 0
            ? $"\"{value.Replace("\"", "\"\"")}\""
            : value;

    private sealed record ActorScope(Guid TenantId, Error? Error);
    private sealed record PeriodScope(DateTime FromDate, DateTime ToDate, Error? Error);
    private sealed record EventData(Guid Id, string Name, TestingEventStatus Status, TestingEventMode Mode, DateTime StartsAt);
    private sealed record ApplicationData(Guid EventId, TestingApplicationStatus Status, DateTime CreatedAt);
    private sealed record RegistrationData(Guid EventId, TestingSlotRegistrationStatus Status, DateTime CreatedAt);
    private sealed record FeedbackData(Guid EventId, int? OverallRating, bool? WouldRecommend, DateTime CreatedAt);
    private sealed record SlotData(Guid EventId, int? MaxTesters);
    private sealed record WindowData(
        TestingLabAnalyticsSummaryProjection Summary,
        IReadOnlyList<TestingLabAnalyticsTrendProjection> Trend,
        IReadOnlyList<TestingLabEventAnalyticsProjection> Events);
}
