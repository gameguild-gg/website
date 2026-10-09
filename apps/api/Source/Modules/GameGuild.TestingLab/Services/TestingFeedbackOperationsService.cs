namespace GameGuild.TestingLab;

using GameGuild.Identity.Context.Actors;

/// <summary>
/// Service implementation for feedback, statistics, and reporting operations.
/// Extracted from the monolithic TestService for focused responsibility.
/// </summary>
public class TestingFeedbackOperationsService(
    IApplicationDbContext context,
    IActorContextAccessor actorContextAccessor) : ITestingFeedbackOperations
{
    private Guid TenantId => actorContextAccessor.ActorContext.TenantId
        ?? throw new UnauthorizedAccessException("A selected tenant is required for Testing Lab feedback.");

    private IQueryable<TestingFeedback> TenantFeedback
    {
        get
        {
            var tenantId = TenantId;
            return context.Set<TestingFeedback>()
                .Where(feedback => feedback.TenantId == tenantId && feedback.DeletedAt == null);
        }
    }

    #region Feedback CRUD

    public async Task<TestingFeedback> AddFeedbackAsync(Guid testingRequestId, Guid userId, Guid feedbackFormId, string feedbackData, TestingContext context1, Guid? sessionId = null, string? additionalNotes = null)
    {
        await EnsureParticipantCanSubmitAsync(testingRequestId, userId).ConfigureAwait(false);
        var feedback = new TestingFeedback
        {
            Id = Guid.NewGuid(),
            TestingRequestId = testingRequestId,
            UserId = userId,
            FeedbackFormId = feedbackFormId,
            SessionId = sessionId,
            TestingContext = context1,
            FeedbackData = feedbackData,
            AdditionalNotes = additionalNotes,
            TenantId = TenantId,
        };

        context.Set<TestingFeedback>().Add(feedback);
        await context.SaveChangesAsync().ConfigureAwait(false);

        return await TenantFeedback
            .Include(tf => tf.TestingRequest)
            .Include(tf => tf.User)
            .Include(tf => tf.FeedbackForm)
            .Include(tf => tf.Session)
            .FirstAsync(tf => tf.Id == feedback.Id);
    }

    public async Task<IEnumerable<TestingFeedback>> GetTestingRequestFeedbackAsync(Guid testingRequestId)
    {
        await EnsureRequestExistsAsync(testingRequestId, includeArchived: true).ConfigureAwait(false);
        return await TenantFeedback
            .Where(tf => tf.TestingRequestId == testingRequestId)
            .Include(tf => tf.User)
            .Include(tf => tf.FeedbackForm)
            .Include(tf => tf.Session)
            .OrderByDescending(tf => tf.CreatedAt)
            .ToListAsync();
    }

    public async Task<IEnumerable<TestingFeedback>> GetFeedbackByUserAsync(Guid userId)
    {
        return await TenantFeedback
            .Where(tf => tf.UserId == userId)
            .Include(tf => tf.TestingRequest)
            .Include(tf => tf.FeedbackForm)
            .Include(tf => tf.Session)
            .OrderByDescending(tf => tf.CreatedAt)
            .ToListAsync();
    }

    public async Task<TestingFeedbackDirectoryPage> GetFeedbackDirectoryAsync(
        TestingFeedbackDirectoryQuery query,
        CancellationToken cancellationToken = default)
    {
        var skip = Math.Max(0, query.Skip);
        var take = Math.Clamp(query.Take, 1, 100);
        var feedback = TenantFeedback.AsNoTracking();

        feedback = query.Source switch
        {
            TestingFeedbackSource.Request => feedback.Where(item => item.TestingRequestId != null),
            TestingFeedbackSource.Event => feedback.Where(item => item.EventId != null),
            _ => feedback,
        };
        if (query.EventId.HasValue)
        {
            feedback = feedback.Where(item => item.EventId == query.EventId);
        }

        if (query.RequestId.HasValue)
        {
            feedback = feedback.Where(item => item.TestingRequestId == query.RequestId);
        }

        if (query.UserId.HasValue)
        {
            feedback = feedback.Where(item => item.UserId == query.UserId);
        }

        if (query.Reported.HasValue)
        {
            feedback = feedback.Where(item => item.IsReported == query.Reported);
        }

        if (query.Quality.HasValue)
        {
            feedback = feedback.Where(item => item.QualityRating == query.Quality);
        }

        var search = query.Search?.Trim();
        if (!string.IsNullOrWhiteSpace(search))
        {
            feedback = feedback.Where(item =>
                item.FeedbackData.Contains(search) ||
                (item.AdditionalNotes != null && item.AdditionalNotes.Contains(search)) ||
                (item.TestingRequest != null && item.TestingRequest.Title.Contains(search)) ||
                (item.Event != null && item.Event.Name.Contains(search)) ||
                item.User.Name.Contains(search) ||
                item.User.Email.Contains(search));
        }

        var totalCount = await feedback.CountAsync(cancellationToken).ConfigureAwait(false);
        var items = await feedback
            .OrderByDescending(item => item.CreatedAt)
            .ThenByDescending(item => item.Id)
            .Skip(skip)
            .Take(take)
            .Select(item => new TestingFeedbackDirectoryItem(
                item.Id,
                item.EventId != null ? TestingFeedbackSource.Event : TestingFeedbackSource.Request,
                item.TestingRequestId,
                item.TestingRequest == null ? null : item.TestingRequest.Title,
                item.EventId,
                item.Event == null ? null : item.Event.Name,
                item.ApplicationId,
                item.Application == null ? null : item.Application.ProjectId,
                item.Application == null ? null : item.Application.Project.Title,
                item.Application == null ? null : item.Application.ProjectVersionId,
                item.Application == null || item.Application.ProjectVersion == null
                    ? null
                    : item.Application.ProjectVersion.VersionNumber,
                item.UserId,
                item.User.Name,
                item.User.Email,
                item.TestingContext,
                item.OverallRating,
                item.WouldRecommend,
                item.FeedbackData,
                item.AdditionalNotes,
                item.IsReported,
                item.ReportReason,
                item.ReportedByUserId,
                item.ReportedAt,
                item.QualityRating,
                item.CreatedAt,
                item.UpdatedAt))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return new TestingFeedbackDirectoryPage(items, totalCount, skip, take);
    }

    #endregion

    #region Simplified Feedback

    public async Task SubmitFeedbackAsync(SubmitFeedbackDto feedbackDto, Guid userId)
    {
        await EnsureParticipantCanSubmitAsync(feedbackDto.TestingRequestId, userId).ConfigureAwait(false);
        var tenantId = TenantId;
        var existingForm = await context.Set<TestingFeedbackForm>()
            .FirstOrDefaultAsync(f =>
                f.TestingRequestId == feedbackDto.TestingRequestId &&
                f.TenantId == tenantId &&
                f.DeletedAt == null);

        Guid feedbackFormId;

        if (existingForm == null)
        {
            var feedbackForm = new TestingFeedbackForm
            {
                Id = Guid.NewGuid(),
                TestingRequestId = feedbackDto.TestingRequestId,
                FormSchema = "{ \"type\": \"simple\", \"questions\": [] }",
                IsForOnline = true,
                IsForSessions = true,
                TenantId = tenantId,
            };

            context.Set<TestingFeedbackForm>().Add(feedbackForm);
            await context.SaveChangesAsync().ConfigureAwait(false);
            feedbackFormId = feedbackForm.Id;
        }
        else
        {
            feedbackFormId = existingForm.Id;
        }

        var feedback = new TestingFeedback
        {
            Id = Guid.NewGuid(),
            TestingRequestId = feedbackDto.TestingRequestId,
            FeedbackFormId = feedbackFormId,
            UserId = userId,
            SessionId = feedbackDto.SessionId,
            TestingContext = TestingContext.Online,
            FeedbackData = feedbackDto.FeedbackResponses,
            OverallRating = feedbackDto.OverallRating,
            WouldRecommend = feedbackDto.WouldRecommend,
            AdditionalNotes = feedbackDto.AdditionalNotes,
            TenantId = tenantId,
        };

        context.Set<TestingFeedback>().Add(feedback);

        var testingRequest = await context.Set<TestingRequest>()
            .FirstAsync(request =>
                request.Id == feedbackDto.TestingRequestId &&
                request.TenantId == tenantId &&
                request.DeletedAt == null)
            .ConfigureAwait(false);

        if (testingRequest != null)
        {
            testingRequest.CurrentTesterCount = await TenantFeedback.CountAsync(f => f.TestingRequestId == feedbackDto.TestingRequestId);
            testingRequest.Touch();
        }

        await context.SaveChangesAsync().ConfigureAwait(false);
    }

    #endregion

    #region Statistics

    public async Task<object> GetTestingRequestStatisticsAsync(Guid testingRequestId)
    {
        await EnsureRequestExistsAsync(testingRequestId, includeArchived: true).ConfigureAwait(false);
        var tenantId = TenantId;
        var participantCount = await context.Set<TestingParticipant>().CountAsync(tp => tp.TestingRequestId == testingRequestId && tp.TenantId == tenantId);
        var sessionCount = await context.Set<TestingSession>().CountAsync(ts => ts.TestingRequestId == testingRequestId && ts.TenantId == tenantId && ts.DeletedAt == null);
        var feedbackCount = await TenantFeedback.CountAsync(tf => tf.TestingRequestId == testingRequestId);
        var completedSessionCount = await context.Set<TestingSession>().CountAsync(ts => ts.TestingRequestId == testingRequestId && ts.TenantId == tenantId && ts.Status == SessionStatus.Completed && ts.DeletedAt == null);

        return new
        {
            ParticipantCount = participantCount,
            SessionCount = sessionCount,
            CompletedSessionCount = completedSessionCount,
            FeedbackCount = feedbackCount
        };
    }

    #endregion

    #region Feedback Reporting & Quality

    public async Task ReportFeedbackAsync(Guid feedbackId, string reason, Guid reportedByUserId)
    {
        var feedback = await TenantFeedback.FirstOrDefaultAsync(tf => tf.Id == feedbackId);

        if (feedback == null) { throw new ArgumentException("Feedback not found"); }

        feedback.IsReported = true;
        feedback.ReportReason = reason;
        feedback.ReportedByUserId = reportedByUserId;
        feedback.ReportedAt = SystemClock.UtcNow;

        await context.SaveChangesAsync().ConfigureAwait(false);
    }

    public async Task RateFeedbackQualityAsync(Guid feedbackId, FeedbackQuality quality, Guid ratedByUserId)
    {
        var feedback = await TenantFeedback.FirstOrDefaultAsync(tf => tf.Id == feedbackId);

        if (feedback == null) { throw new ArgumentException("Feedback not found"); }

        feedback.QualityRating = quality;

        await context.SaveChangesAsync().ConfigureAwait(false);
    }

    private async Task EnsureRequestExistsAsync(Guid testingRequestId, bool includeArchived = false)
    {
        var tenantId = TenantId;
        var requests = context.Set<TestingRequest>().AsQueryable();
        if (includeArchived)
        {
            requests = requests.IgnoreQueryFilters();
        }

        var exists = await requests.AnyAsync(request =>
            request.Id == testingRequestId &&
            request.TenantId == tenantId &&
            (includeArchived || request.DeletedAt == null)).ConfigureAwait(false);
        if (!exists)
        {
            throw new ArgumentException("Testing request not found.", nameof(testingRequestId));
        }
    }

    private async Task EnsureParticipantCanSubmitAsync(Guid testingRequestId, Guid userId)
    {
        await EnsureRequestExistsAsync(testingRequestId).ConfigureAwait(false);
        var tenantId = TenantId;
        var isParticipant = await context.Set<TestingParticipant>().AnyAsync(participant =>
            participant.TestingRequestId == testingRequestId &&
            participant.UserId == userId &&
            participant.TenantId == tenantId &&
            participant.DeletedAt == null &&
            participant.Status == ParticipationStatus.Active).ConfigureAwait(false);
        if (!isParticipant)
        {
            throw new UnauthorizedAccessException("Only an active request participant can submit feedback.");
        }
    }

    #endregion
}
