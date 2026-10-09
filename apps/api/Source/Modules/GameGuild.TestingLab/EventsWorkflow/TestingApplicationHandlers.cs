using GameGuild.CQRS;
using GameGuild.Identity.Authorization;
using GameGuild.Identity.Context.Actors;
using GameGuild.Identity.Tenants;
using GameGuild.Projects;
using GameGuild.Teams;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace GameGuild.TestingLab;

public sealed class TestingApplicationHandlers(
    IApplicationDbContext context,
    IActorContextAccessor actorContextAccessor,
    IProjectAuthorizationService projectAuthorizationService,
    ILogger<TestingApplicationHandlers> logger,
    IProjectLifecycleLock? capacityLock = null,
    ITestingLabPermissionService? testingLabPermissionService = null,
    GameGuild.Assets.IAssetScopedAccessService? assetScopedAccessService = null,
    GameGuild.Assets.IAssetAccessService? assetAccessService = null,
    GameGuild.CQRS.IMediator? mediator = null) :
    ICommandHandler<CreateTestingProjectApplicationDraftCommand, Result<TestingProjectApplicationProjection>>,
    ICommandHandler<SaveTestingProjectApplicationDraftCommand, Result<TestingProjectApplicationProjection>>,
    ICommandHandler<SubmitTestingProjectApplicationDraftCommand, Result<TestingProjectApplicationProjection>>,
    ICommandHandler<SubmitTestingProjectApplicationCommand, Result<TestingProjectApplicationProjection>>,
    ICommandHandler<UpdateTestingProjectApplicationCommand, Result<TestingProjectApplicationProjection>>,
    ICommandHandler<WithdrawTestingProjectApplicationCommand, Result<TestingProjectApplicationProjection>>,
    ICommandHandler<BeginReviewTestingProjectApplicationCommand, Result<TestingProjectApplicationProjection>>,
    ICommandHandler<CastTestingApplicationVoteCommand, Result<TestingApplicationVoteProjection>>,
    ICommandHandler<ApproveTestingProjectApplicationCommand, Result<TestingProjectApplicationProjection>>,
    ICommandHandler<RejectTestingProjectApplicationCommand, Result<TestingProjectApplicationProjection>>,
    ICommandHandler<WaitlistTestingProjectApplicationCommand, Result<TestingProjectApplicationProjection>>,
    ICommandHandler<AssignTestingProjectApplicationSlotCommand, Result<TestingProjectApplicationProjection>>,
    IQueryHandler<GetTestingProjectApplicationQuery, Result<TestingProjectApplicationProjection>>,
    IQueryHandler<GetMyTestingProjectApplicationsQuery, Result<IReadOnlyList<TestingProjectApplicationProjection>>>,
    IQueryHandler<GetTestingEventApplicationsQuery, Result<IReadOnlyList<TestingProjectApplicationProjection>>>,
    IQueryHandler<GetTestingEventApplicationAccessQuery, Result<TestingEventApplicationAccessProjection>>,
    IQueryHandler<GetTestingApplicationTesterEligibilityQuery, Result<IReadOnlyList<TestingApplicationTesterEligibilityProjection>>>,
    IQueryHandler<GetTestingApplicationReviewPackageQuery, Result<TestingApplicationReviewPackageProjection>>
{
    private readonly IProjectLifecycleLock _capacityLock = capacityLock ?? new ProjectLifecycleLock(context);
    private bool IsTenantAdmin => actorContextAccessor.ActorContext.IsTenantAdmin;

    public async Task<Result<TestingProjectApplicationProjection>> Handle(
        CreateTestingProjectApplicationDraftCommand request,
        CancellationToken cancellationToken)
    {
        var actor = await RequireActorAsync(cancellationToken).ConfigureAwait(false);
        if (actor.Error != null)
        {
            return Result.Failure<TestingProjectApplicationProjection>(actor.Error);
        }

        var testingEvent = await context.Set<TestingEvent>().FirstOrDefaultAsync(candidate =>
            candidate.Id == request.EventId && candidate.TenantId == actor.TenantId && candidate.DeletedAt == null,
            cancellationToken).ConfigureAwait(false);
        if (testingEvent == null)
        {
            return Result.Failure<TestingProjectApplicationProjection>(Error.NotFound("TestingLab.EventNotFound", "Testing event not found."));
        }

        if (!AcceptsApplicationChanges(testingEvent))
        {
            return Result.Failure<TestingProjectApplicationProjection>(Validation("This event is not accepting project application drafts."));
        }

        if (!await projectAuthorizationService.HasPermissionAsync(request.ProjectId, PermissionType.Edit, cancellationToken).ConfigureAwait(false))
        {
            return Result.Failure<TestingProjectApplicationProjection>(Error.NotFound("TestingLab.ProjectNotFound", "Project not found."));
        }

        var projectExists = await context.Set<Project>().AsNoTracking().AnyAsync(project =>
            project.Id == request.ProjectId && project.TenantId == actor.TenantId && project.DeletedAt == null &&
            project.Status != ContentStatus.Archived && project.Status != ContentStatus.Deleted,
            cancellationToken).ConfigureAwait(false);
        if (!projectExists)
        {
            return Result.Failure<TestingProjectApplicationProjection>(Validation("The selected project is unavailable."));
        }

        var duplicate = await context.Set<TestingProjectApplication>().AnyAsync(application =>
            application.EventId == request.EventId && application.ProjectId == request.ProjectId &&
            application.TenantId == actor.TenantId && application.DeletedAt == null &&
            application.Status != TestingApplicationStatus.Rejected && application.Status != TestingApplicationStatus.Withdrawn,
            cancellationToken).ConfigureAwait(false);
        if (duplicate)
        {
            return Result.Failure<TestingProjectApplicationProjection>(Error.Conflict("TestingLab.ApplicationExists", "This project already has an active application for the event."));
        }

        var application = TestingProjectApplication.CreateDraft(request.EventId, request.ProjectId, actor.UserId, actor.TenantId);
        context.Set<TestingProjectApplication>().Add(application);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Result.Success(ToProjection(application));
    }

    public async Task<Result<TestingProjectApplicationProjection>> Handle(
        SaveTestingProjectApplicationDraftCommand request,
        CancellationToken cancellationToken)
    {
        var loaded = await LoadApplicationAsync(request.ApplicationId, cancellationToken).ConfigureAwait(false);
        if (loaded.Error != null)
        {
            return Result.Failure<TestingProjectApplicationProjection>(loaded.Error);
        }

        var application = loaded.Application!;
        var actor = loaded.Actor!;
        if (application.Status is not (TestingApplicationStatus.Draft or TestingApplicationStatus.Pending))
        {
            return Result.Failure<TestingProjectApplicationProjection>(Validation("The application package is frozen."));
        }

        if (!await projectAuthorizationService.HasPermissionAsync(application.ProjectId, PermissionType.Edit, cancellationToken).ConfigureAwait(false))
        {
            return Result.Failure<TestingProjectApplicationProjection>(Error.Forbidden("TestingLab.ProjectEditRequired", "Project edit access is required."));
        }

        if (!AcceptsApplicationChanges(application.Event))
        {
            return Result.Failure<TestingProjectApplicationProjection>(Validation("This event is not accepting project application updates."));
        }

        if (request.ProjectVersionId.HasValue && request.ProjectVersionId != application.ProjectVersionId &&
            application.Status == TestingApplicationStatus.Pending &&
            !ProjectVersionEligibility.CanReplaceAfterSubmission(application.SubmissionVersionPolicy))
        {
            return Result.Failure<TestingProjectApplicationProjection>(Validation("The submitted project version is immutable under this application's policy."));
        }

        var policy = await GetCurrentVersionPolicyAsync(actor.TenantId, cancellationToken).ConfigureAwait(false);
        if (request.ProjectVersionId.HasValue)
        {
            var version = await context.Set<ProjectVersion>().AsNoTracking().FirstOrDefaultAsync(candidate =>
                candidate.Id == request.ProjectVersionId.Value && candidate.ProjectId == application.ProjectId &&
                candidate.TenantId == actor.TenantId && candidate.DeletedAt == null,
                cancellationToken).ConfigureAwait(false);
            if (version == null || !ProjectVersionEligibility.IsEligible(version.Status, policy))
            {
                return Result.Failure<TestingProjectApplicationProjection>(Validation("Project version is not eligible under the current Testing Lab policy."));
            }
        }
        try
        {
            var effectiveVersionId = request.ProjectVersionId ?? application.ProjectVersionId;
            if ((request.SubmittedAssetReferenceIds?.Count ?? 0) > 0 && !effectiveVersionId.HasValue)
            {
                throw new InvalidOperationException("A project version is required before assets can be attached.");
            }

            if (effectiveVersionId.HasValue)
            {
                await ValidateSubmittedAssetsAsync(application.ProjectId, effectiveVersionId.Value, request.SubmittedAssetReferenceIds, actor.TenantId, cancellationToken).ConfigureAwait(false);
            }

            application.UpdateDraftPackage(
                request.ProjectVersionId,
                request.Brief,
                request.EventApplicationResponse,
                request.AcceptedRules,
                request.SubmittedAssetReferenceIds,
                request.PreferredAvailability);
            if (request.FeedbackQuestionnaire != null)
            {
                var nextRevision = application.QuestionnaireRevisions.Count == 0
                    ? 1
                    : application.QuestionnaireRevisions.Max(revision => revision.RevisionNumber) + 1;
                var revision = TestingQuestionnaireRevision.Create(
                    application.Id, nextRevision, request.FeedbackQuestionnaire, actor.UserId, actor.TenantId,
                    ensureValid: false);
                application.UseQuestionnaireRevision(revision);
                context.Set<TestingQuestionnaireRevision>().Add(revision);
            }
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return Result.Success(ToProjection(application));
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            return Result.Failure<TestingProjectApplicationProjection>(Validation(exception.Message));
        }
    }

    public async Task<Result<TestingProjectApplicationProjection>> Handle(
        SubmitTestingProjectApplicationDraftCommand request,
        CancellationToken cancellationToken)
    {
        var loaded = await LoadApplicationAsync(request.ApplicationId, cancellationToken).ConfigureAwait(false);
        if (loaded.Error != null)
        {
            return Result.Failure<TestingProjectApplicationProjection>(loaded.Error);
        }

        var application = loaded.Application!;
        var actor = loaded.Actor!;
        if (!await projectAuthorizationService.HasPermissionAsync(application.ProjectId, PermissionType.Edit, cancellationToken).ConfigureAwait(false))
        {
            return Result.Failure<TestingProjectApplicationProjection>(Error.Forbidden("TestingLab.ProjectEditRequired", "Project edit access is required."));
        }

        if (!AcceptsApplicationChanges(application.Event))
        {
            return Result.Failure<TestingProjectApplicationProjection>(Validation("This event is not accepting project applications."));
        }

        if (!application.ProjectVersionId.HasValue)
        {
            return Result.Failure<TestingProjectApplicationProjection>(Validation("A project version is required."));
        }

        var policy = await GetCurrentVersionPolicyAsync(actor.TenantId, cancellationToken).ConfigureAwait(false);
        var version = await context.Set<ProjectVersion>().AsNoTracking().FirstOrDefaultAsync(candidate =>
            candidate.Id == application.ProjectVersionId.Value && candidate.ProjectId == application.ProjectId &&
            candidate.TenantId == actor.TenantId && candidate.DeletedAt == null,
            cancellationToken).ConfigureAwait(false);
        if (version == null || !ProjectVersionEligibility.IsEligible(version.Status, policy))
        {
            return Result.Failure<TestingProjectApplicationProjection>(Validation("Project version is not eligible under the current Testing Lab policy."));
        }

        var questionnaire = application.QuestionnaireRevisions
            .SingleOrDefault(revision => revision.Id == application.CurrentQuestionnaireRevisionId);
        if (questionnaire == null)
        {
            return Result.Failure<TestingProjectApplicationProjection>(Validation("A feedback questionnaire is required."));
        }

        if (application.Brief == null || application.EventApplicationResponse == null || !application.RulesAcceptedAt.HasValue)
        {
            return Result.Failure<TestingProjectApplicationProjection>(Validation(
                "Test brief, event application responses, and rules acceptance are required."));
        }

        try
        {
            application.Brief.EnsureValid();
            questionnaire.Schema.EnsureValid();
            if (application.Event.RequiresFeedback && questionnaire.Schema.Questions.Count == 0)
            {
                throw new InvalidOperationException("At least one developer feedback question is required.");
            }

            var eventSchema = application.Event.ProjectApplicationSchema
                ?? throw new InvalidOperationException("The event application questionnaire is not configured.");
            QuestionnaireResponseValidator.EnsureValid(eventSchema, application.EventApplicationResponse);
            application.SubmitDraft(policy);
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return Result.Success(ToProjection(application));
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            return Result.Failure<TestingProjectApplicationProjection>(Validation(exception.Message));
        }
    }

    public async Task<Result<TestingProjectApplicationProjection>> Handle(
        SubmitTestingProjectApplicationCommand request,
        CancellationToken cancellationToken)
    {
        var actor = await RequireActorAsync(cancellationToken).ConfigureAwait(false);
        if (actor.Error != null)
        {
            return Result.Failure<TestingProjectApplicationProjection>(actor.Error);
        }

        var testingEvent = await context.Set<TestingEvent>()
            .FirstOrDefaultAsync(candidate =>
                candidate.Id == request.EventId &&
                candidate.TenantId == actor.TenantId &&
                candidate.DeletedAt == null,
                cancellationToken)
            .ConfigureAwait(false);
        if (testingEvent == null)
        {
            return Result.Failure<TestingProjectApplicationProjection>(Error.NotFound("TestingLab.EventNotFound", "Testing event not found."));
        }

        var now = SystemClock.UtcNow;
        if (testingEvent.Status != TestingEventStatus.ApplicationsOpen ||
            now < testingEvent.ApplicationsOpenAt ||
            now > testingEvent.ApplicationsCloseAt)
        {
            return Result.Failure<TestingProjectApplicationProjection>(Validation("This event is not accepting project applications."));
        }

        if (!await projectAuthorizationService.HasPermissionAsync(request.ProjectId, PermissionType.Edit, cancellationToken).ConfigureAwait(false))
        {
            return Result.Failure<TestingProjectApplicationProjection>(Error.NotFound("TestingLab.ProjectNotFound", "Project not found."));
        }

        var projectTitle = await context.Set<Project>()
            .Where(project => project.Id == request.ProjectId &&
            project.TenantId == actor.TenantId &&
            project.DeletedAt == null &&
            project.Status != ContentStatus.Archived &&
            project.Status != ContentStatus.Deleted)
            .Select(project => project.Title)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        var projectExists = projectTitle != null;
        if (!projectExists)
        {
            return Result.Failure<TestingProjectApplicationProjection>(Validation("The selected project is unavailable."));
        }

        var version = await context.Set<ProjectVersion>().AsNoTracking().FirstOrDefaultAsync(version =>
                version.Id == request.ProjectVersionId &&
                version.ProjectId == request.ProjectId &&
                version.TenantId == actor.TenantId &&
                version.DeletedAt == null,
                cancellationToken).ConfigureAwait(false);
        if (version == null)
        {
            return Result.Failure<TestingProjectApplicationProjection>(Validation("Project version must be active and belong to the selected project."));
        }

        var submissionPolicy = await GetCurrentVersionPolicyAsync(actor.TenantId, cancellationToken).ConfigureAwait(false);
        if (!ProjectVersionEligibility.IsEligible(version.Status, submissionPolicy))
        {
            return Result.Failure<TestingProjectApplicationProjection>(Validation("Project version is not eligible under the Testing Lab submission policy."));
        }

        var duplicate = await context.Set<TestingProjectApplication>().AnyAsync(application =>
            application.EventId == request.EventId &&
            application.ProjectId == request.ProjectId &&
            application.TenantId == actor.TenantId &&
            application.DeletedAt == null &&
            application.Status != TestingApplicationStatus.Rejected &&
            application.Status != TestingApplicationStatus.Withdrawn,
            cancellationToken).ConfigureAwait(false);
        if (duplicate)
        {
            return Result.Failure<TestingProjectApplicationProjection>(Error.Conflict("TestingLab.ApplicationExists", "This project already has an active application for the event."));
        }

        if (request.Brief == null || request.FeedbackQuestionnaire == null || request.EventApplicationResponse == null)
        {
            return Result.Failure<TestingProjectApplicationProjection>(Validation(
                "Test brief, developer feedback questionnaire, and event application responses are required."));
        }

        var application = TestingProjectApplication.CreateDraft(
            request.EventId,
            request.ProjectId,
            actor.UserId,
            actor.TenantId);
        try
        {
            var eventSchema = testingEvent.ProjectApplicationSchema
                ?? throw new InvalidOperationException("The event application questionnaire is not configured.");
            request.Brief.EnsureValid();
            QuestionnaireResponseValidator.EnsureValid(eventSchema, request.EventApplicationResponse);
            await ValidateSubmittedAssetsAsync(
                request.ProjectId,
                request.ProjectVersionId,
                request.SubmittedAssetReferenceIds,
                actor.TenantId,
                cancellationToken).ConfigureAwait(false);
            var revision = TestingQuestionnaireRevision.Create(
                application.Id,
                1,
                request.FeedbackQuestionnaire,
                actor.UserId,
                actor.TenantId);
            if (testingEvent.RequiresFeedback && revision.Schema.Questions.Count == 0)
            {
                throw new InvalidOperationException("At least one developer feedback question is required.");
            }

            application.UpdateDraftPackage(
                request.ProjectVersionId,
                request.Brief,
                request.EventApplicationResponse,
                request.AcceptedRules,
                request.SubmittedAssetReferenceIds,
                request.PreferredAvailability);
            application.UseQuestionnaireRevision(revision);
            application.SubmitDraft(submissionPolicy);
            context.Set<TestingProjectApplication>().Add(application);
            context.Set<TestingQuestionnaireRevision>().Add(revision);
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            return Result.Failure<TestingProjectApplicationProjection>(Validation(exception.Message));
        }
        logger.LogInformation("Actor {ActorId} submitted project {ProjectId} to Testing Lab event {EventId}", actor.UserId, request.ProjectId, request.EventId);

        if (mediator is not null)
        {
            await mediator.Send(new GameGuild.Announcements.Contracts.AnnouncePublicationCommand
            {
                Kind = GameGuild.Announcements.Contracts.PublicationKind.ProjectJoinedTestingEvent,
                ActorId = actor.UserId,
                Title = projectTitle!,
                EntityId = testingEvent.Id,
                SecondaryTitle = testingEvent.Name,
                NotifyUserId = testingEvent.ManagerUserId,
                TenantId = actor.TenantId,
            }, cancellationToken).ConfigureAwait(false);
        }

        return Result.Success(ToProjection(application));
    }

    public async Task<Result<TestingProjectApplicationProjection>> Handle(
        UpdateTestingProjectApplicationCommand request,
        CancellationToken cancellationToken)
    {
        var loaded = await LoadApplicationAsync(request.ApplicationId, cancellationToken).ConfigureAwait(false);
        if (loaded.Error != null)
        {
            return Result.Failure<TestingProjectApplicationProjection>(loaded.Error);
        }

        var application = loaded.Application!;
        var actor = loaded.Actor!;

        if (!await projectAuthorizationService.HasPermissionAsync(
                application.ProjectId,
                PermissionType.Edit,
                cancellationToken).ConfigureAwait(false))
        {
            return Result.Failure<TestingProjectApplicationProjection>(
                Error.Forbidden("TestingLab.ProjectEditRequired", "Project edit access is required to update its application."));
        }

        var now = SystemClock.UtcNow;
        if (application.Event.Status != TestingEventStatus.ApplicationsOpen ||
            now < application.Event.ApplicationsOpenAt ||
            now > application.Event.ApplicationsCloseAt)
        {
            return Result.Failure<TestingProjectApplicationProjection>(Validation("This event is not accepting project application updates."));
        }

        if (request.ProjectVersionId != application.ProjectVersionId &&
            !ProjectVersionEligibility.CanReplaceAfterSubmission(application.SubmissionVersionPolicy))
        {
            return Result.Failure<TestingProjectApplicationProjection>(Validation("The submitted project version is immutable under this application's policy."));
        }

        var version = await context.Set<ProjectVersion>().AsNoTracking().FirstOrDefaultAsync(version =>
            version.Id == request.ProjectVersionId &&
            version.ProjectId == application.ProjectId &&
            version.TenantId == actor.TenantId &&
            version.DeletedAt == null,
            cancellationToken).ConfigureAwait(false);
        if (version == null)
        {
            return Result.Failure<TestingProjectApplicationProjection>(Validation("Project version must be active and belong to the applied project."));
        }

        var currentPolicy = await GetCurrentVersionPolicyAsync(actor.TenantId, cancellationToken).ConfigureAwait(false);
        if (!ProjectVersionEligibility.IsEligible(version.Status, currentPolicy))
        {
            return Result.Failure<TestingProjectApplicationProjection>(Validation("Project version is not eligible under the current Testing Lab submission policy."));
        }

        var submittedAssetIds = request.SubmittedAssetReferenceIds?
            .Where(id => id != Guid.Empty).Distinct().Take(100).ToArray() ?? [];
        if (submittedAssetIds.Length > 0)
        {
            var validAssetCount = await context.Set<GameGuild.Assets.AssetReference>().AsNoTracking().CountAsync(asset =>
                submittedAssetIds.Contains(asset.Id) &&
                asset.TenantId == actor.TenantId &&
                asset.DeletedAt == null &&
                ((asset.ParentResourceType == nameof(Project) && asset.ParentResourceId == application.ProjectId) ||
                 (asset.ParentResourceType == nameof(ProjectVersion) && asset.ParentResourceId == request.ProjectVersionId)),
                cancellationToken).ConfigureAwait(false);
            if (validAssetCount != submittedAssetIds.Length)
            {
                return Result.Failure<TestingProjectApplicationProjection>(
                    Validation("Every submitted file must belong to the applied project or selected project version."));
            }
        }

        try
        {
            application.UpdateSubmission(request.ProjectVersionId, request.PreferredAvailability, submittedAssetIds);
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            logger.LogInformation(
                "Actor {ActorId} updated Testing Lab application {ApplicationId} for project {ProjectId}",
                actor.UserId,
                application.Id,
                application.ProjectId);
            return Result.Success(ToProjection(application));
        }
        catch (InvalidOperationException exception)
        {
            return Result.Failure<TestingProjectApplicationProjection>(Validation(exception.Message));
        }
    }

    public async Task<Result<TestingProjectApplicationProjection>> Handle(
        WithdrawTestingProjectApplicationCommand request,
        CancellationToken cancellationToken)
    {
        var loaded = await LoadApplicationAsync(request.ApplicationId, cancellationToken).ConfigureAwait(false);
        if (loaded.Error != null)
        {
            return Result.Failure<TestingProjectApplicationProjection>(loaded.Error);
        }

        if (!await projectAuthorizationService.HasPermissionAsync(
                loaded.Application!.ProjectId,
                PermissionType.Edit,
                cancellationToken).ConfigureAwait(false))
        {
            return Result.Failure<TestingProjectApplicationProjection>(
                Error.Forbidden("TestingLab.ProjectEditRequired", "Project edit access is required to withdraw its application."));
        }

        try
        {
            loaded.Application.Withdraw();
            if (assetScopedAccessService != null)
            {
                await assetScopedAccessService.RevokeScopeAsync(
                    TestingLabAssetScopes.ApplicationReview,
                    loaded.Application.Id,
                    cancellationToken).ConfigureAwait(false);
            }

            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return Result.Success(ToProjection(loaded.Application));
        }
        catch (UnauthorizedAccessException exception)
        {
            return Result.Failure<TestingProjectApplicationProjection>(Error.Forbidden("TestingLab.ApplicationOwnerRequired", exception.Message));
        }
        catch (InvalidOperationException exception)
        {
            return Result.Failure<TestingProjectApplicationProjection>(Validation(exception.Message));
        }
    }

    public async Task<Result<TestingProjectApplicationProjection>> Handle(
        BeginReviewTestingProjectApplicationCommand request,
        CancellationToken cancellationToken)
    {
        var loaded = await LoadManagedApplicationAsync(request.ApplicationId, cancellationToken).ConfigureAwait(false);
        if (loaded.Error != null)
        {
            return Result.Failure<TestingProjectApplicationProjection>(loaded.Error);
        }

        try
        {
            loaded.Application!.BeginReview();
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return Result.Success(ToProjection(loaded.Application));
        }
        catch (InvalidOperationException exception)
        {
            return Result.Failure<TestingProjectApplicationProjection>(Validation(exception.Message));
        }
    }

    public async Task<Result<TestingApplicationVoteProjection>> Handle(
        CastTestingApplicationVoteCommand request,
        CancellationToken cancellationToken)
    {
        var loaded = await LoadApplicationAsync(request.ApplicationId, cancellationToken).ConfigureAwait(false);
        if (loaded.Error != null)
        {
            return Result.Failure<TestingApplicationVoteProjection>(loaded.Error);
        }

        var application = loaded.Application!;
        var actor = loaded.Actor!;
        if (application.Event.ApprovalMode != TestingEventApprovalMode.Committee)
        {
            return Result.Failure<TestingApplicationVoteProjection>(Validation("This event does not use committee review."));
        }

        if (application.Status != TestingApplicationStatus.UnderReview)
        {
            return Result.Failure<TestingApplicationVoteProjection>(Validation("The application must be under review before voting."));
        }

        var isReviewer = await context.Set<TestingCommitteeMember>().AnyAsync(member =>
            member.EventId == application.EventId &&
            member.UserId == actor.UserId &&
            member.IsActive &&
            member.DeletedAt == null,
            cancellationToken).ConfigureAwait(false);
        if (!isReviewer && !IsTenantAdmin)
        {
            return Result.Failure<TestingApplicationVoteProjection>(Error.Forbidden("TestingLab.CommitteeMemberRequired", "Only active committee members can vote."));
        }

        var duplicate = await context.Set<TestingApplicationVote>().AnyAsync(vote =>
            vote.ApplicationId == application.Id &&
            vote.ReviewerId == actor.UserId &&
            vote.DeletedAt == null,
            cancellationToken).ConfigureAwait(false);
        if (duplicate)
        {
            return Result.Failure<TestingApplicationVoteProjection>(Error.Conflict("TestingLab.DuplicateVote", "A reviewer can vote only once per application."));
        }

        var vote = TestingApplicationVote.Cast(application.Id, actor.UserId, request.Decision, request.Comments, actor.TenantId);
        context.Set<TestingApplicationVote>().Add(vote);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Result.Success(ToProjection(vote));
    }

    public async Task<Result<TestingProjectApplicationProjection>> Handle(
        ApproveTestingProjectApplicationCommand request,
        CancellationToken cancellationToken)
    {
        var loaded = await LoadManagedApplicationAsync(request.ApplicationId, cancellationToken).ConfigureAwait(false);
        if (loaded.Error != null)
        {
            return Result.Failure<TestingProjectApplicationProjection>(loaded.Error);
        }

        var application = loaded.Application!;
        var committeeDecision = await ValidateCommitteeDecisionAsync(application, TestingApplicationVoteDecision.Approve, cancellationToken).ConfigureAwait(false);
        if (committeeDecision != null)
        {
            return Result.Failure<TestingProjectApplicationProjection>(committeeDecision);
        }

        await using var lockHandle = await _capacityLock.AcquireAsync(request.SlotId, cancellationToken).ConfigureAwait(false);
        var slot = await context.Set<TestingEventSlot>().FirstOrDefaultAsync(candidate =>
            candidate.Id == request.SlotId &&
            candidate.EventId == application.EventId &&
            candidate.TenantId == loaded.Actor!.TenantId &&
            candidate.DeletedAt == null,
            cancellationToken).ConfigureAwait(false);
        if (slot == null)
        {
            return Result.Failure<TestingProjectApplicationProjection>(Error.NotFound("TestingLab.EventSlotNotFound", "Testing event slot not found."));
        }

        var capacityError = await ValidateProjectCapacityAsync(slot, application.Id, cancellationToken).ConfigureAwait(false);
        if (capacityError != null)
        {
            return Result.Failure<TestingProjectApplicationProjection>(capacityError);
        }

        try
        {
            application.Approve(loaded.Actor!.UserId, slot.Id, request.Rationale);
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            await lockHandle.CommitAsync(cancellationToken).ConfigureAwait(false);
            logger.LogInformation("Manager {ManagerId} approved Testing Lab application {ApplicationId} for slot {SlotId}", loaded.Actor.UserId, application.Id, slot.Id);
            return Result.Success(ToProjection(application));
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            return Result.Failure<TestingProjectApplicationProjection>(Validation(exception.Message));
        }
    }

    public async Task<Result<TestingProjectApplicationProjection>> Handle(
        RejectTestingProjectApplicationCommand request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Rationale))
        {
            return Result.Failure<TestingProjectApplicationProjection>(Validation("A rejection rationale is required."));
        }

        var loaded = await LoadManagedApplicationAsync(request.ApplicationId, cancellationToken).ConfigureAwait(false);
        if (loaded.Error != null)
        {
            return Result.Failure<TestingProjectApplicationProjection>(loaded.Error);
        }

        var committeeDecision = await ValidateCommitteeDecisionAsync(loaded.Application!, TestingApplicationVoteDecision.Reject, cancellationToken).ConfigureAwait(false);
        if (committeeDecision != null)
        {
            return Result.Failure<TestingProjectApplicationProjection>(committeeDecision);
        }

        try
        {
            loaded.Application!.Reject(loaded.Actor!.UserId, request.Rationale);
            if (assetScopedAccessService != null)
            {
                await assetScopedAccessService.RevokeScopeAsync(
                    TestingLabAssetScopes.ApplicationReview,
                    loaded.Application.Id,
                    cancellationToken).ConfigureAwait(false);
            }

            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return Result.Success(ToProjection(loaded.Application));
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            return Result.Failure<TestingProjectApplicationProjection>(Validation(exception.Message));
        }
    }

    public async Task<Result<TestingProjectApplicationProjection>> Handle(
        WaitlistTestingProjectApplicationCommand request,
        CancellationToken cancellationToken)
    {
        var loaded = await LoadManagedApplicationAsync(request.ApplicationId, cancellationToken).ConfigureAwait(false);
        if (loaded.Error != null)
        {
            return Result.Failure<TestingProjectApplicationProjection>(loaded.Error);
        }

        try
        {
            loaded.Application!.PlaceOnWaitlist(loaded.Actor!.UserId, request.Rationale);
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return Result.Success(ToProjection(loaded.Application));
        }
        catch (InvalidOperationException exception)
        {
            return Result.Failure<TestingProjectApplicationProjection>(Validation(exception.Message));
        }
    }

    public async Task<Result<TestingProjectApplicationProjection>> Handle(
        AssignTestingProjectApplicationSlotCommand request,
        CancellationToken cancellationToken)
    {
        var loaded = await LoadManagedApplicationAsync(request.ApplicationId, cancellationToken).ConfigureAwait(false);
        if (loaded.Error != null)
        {
            return Result.Failure<TestingProjectApplicationProjection>(loaded.Error);
        }

        await using var lockHandle = await _capacityLock.AcquireAsync(request.SlotId, cancellationToken).ConfigureAwait(false);
        var slot = await context.Set<TestingEventSlot>().FirstOrDefaultAsync(candidate =>
            candidate.Id == request.SlotId &&
            candidate.EventId == loaded.Application!.EventId &&
            candidate.TenantId == loaded.Actor!.TenantId &&
            candidate.DeletedAt == null,
            cancellationToken).ConfigureAwait(false);
        if (slot == null)
        {
            return Result.Failure<TestingProjectApplicationProjection>(Error.NotFound("TestingLab.EventSlotNotFound", "Testing event slot not found."));
        }

        var capacityError = await ValidateProjectCapacityAsync(slot, loaded.Application!.Id, cancellationToken).ConfigureAwait(false);
        if (capacityError != null)
        {
            return Result.Failure<TestingProjectApplicationProjection>(capacityError);
        }

        try
        {
            loaded.Application.ReassignSlot(slot.Id);
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            await lockHandle.CommitAsync(cancellationToken).ConfigureAwait(false);
            return Result.Success(ToProjection(loaded.Application));
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            return Result.Failure<TestingProjectApplicationProjection>(Validation(exception.Message));
        }
    }

    public async Task<Result<TestingProjectApplicationProjection>> Handle(
        GetTestingProjectApplicationQuery request,
        CancellationToken cancellationToken)
    {
        var loaded = await LoadApplicationAsync(request.ApplicationId, cancellationToken).ConfigureAwait(false);
        if (loaded.Error != null)
        {
            return Result.Failure<TestingProjectApplicationProjection>(loaded.Error);
        }

        var application = loaded.Application!;
        var actor = loaded.Actor!;
        var canReview = IsTenantAdmin ||
            application.Event.ManagerUserId == actor.UserId ||
            await HasApplicationPermissionAsync(actor, TestingLabActions.Read, application.Id, cancellationToken).ConfigureAwait(false) ||
            await context.Set<TestingCommitteeMember>().AnyAsync(member =>
            member.EventId == application.EventId &&
            member.UserId == actor.UserId &&
            member.IsActive &&
            member.DeletedAt == null,
            cancellationToken).ConfigureAwait(false);
        var canReadProject = await projectAuthorizationService.HasPermissionAsync(
            application.ProjectId,
            PermissionType.Read,
            cancellationToken).ConfigureAwait(false);
        if (!canReadProject && !canReview)
        {
            return Result.Failure<TestingProjectApplicationProjection>(Error.Forbidden("TestingLab.ApplicationForbidden", "Application owner or reviewer access is required."));
        }

        return Result.Success(ToProjection(application));
    }

    public async Task<Result<IReadOnlyList<TestingProjectApplicationProjection>>> Handle(
        GetTestingEventApplicationsQuery request,
        CancellationToken cancellationToken)
    {
        var access = await ResolveEventApplicationAccessAsync(request.EventId, cancellationToken).ConfigureAwait(false);
        if (access.Error != null)
        {
            return Result.Failure<IReadOnlyList<TestingProjectApplicationProjection>>(access.Error);
        }

        if (!access.CanViewApplications)
        {
            return Result.Failure<IReadOnlyList<TestingProjectApplicationProjection>>(Error.Forbidden("TestingLab.EventReviewerRequired", "Event manager or committee access is required."));
        }

        var query = context.Set<TestingProjectApplication>()
            .AsNoTracking()
            .Include(application => application.Votes)
            .Include(application => application.QuestionnaireRevisions)
            .Where(application =>
                application.EventId == request.EventId &&
                application.TenantId == access.Actor!.TenantId &&
                application.DeletedAt == null &&
                application.Status != TestingApplicationStatus.Draft);
        if (request.Status.HasValue)
        {
            query = query.Where(application => application.Status == request.Status.Value);
        }

        var applications = await query
            .OrderBy(application => application.CreatedAt)
            .Skip(Math.Max(0, request.Skip))
            .Take(Math.Clamp(request.Take, 1, 100))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        return Result.Success<IReadOnlyList<TestingProjectApplicationProjection>>(applications.Select(ToProjection).ToList());
    }

    public async Task<Result<TestingEventApplicationAccessProjection>> Handle(
        GetTestingEventApplicationAccessQuery request,
        CancellationToken cancellationToken)
    {
        var access = await ResolveEventApplicationAccessAsync(request.EventId, cancellationToken).ConfigureAwait(false);
        if (access.Error != null)
        {
            return Result.Failure<TestingEventApplicationAccessProjection>(access.Error);
        }

        if (!access.CanViewApplications)
        {
            return Result.Failure<TestingEventApplicationAccessProjection>(Error.Forbidden("TestingLab.EventReviewerRequired", "Event manager or committee access is required."));
        }

        return Result.Success(new TestingEventApplicationAccessProjection(
            access.CanViewApplications,
            access.CanManageApplications,
            access.CanVote));
    }

    public async Task<Result<IReadOnlyList<TestingProjectApplicationProjection>>> Handle(
        GetMyTestingProjectApplicationsQuery request,
        CancellationToken cancellationToken)
    {
        var actor = await RequireActorAsync(cancellationToken).ConfigureAwait(false);
        if (actor.Error != null)
        {
            return Result.Failure<IReadOnlyList<TestingProjectApplicationProjection>>(actor.Error);
        }

        var query = context.Set<TestingProjectApplication>()
            .AsNoTracking()
            .Include(application => application.Votes)
            .Include(application => application.QuestionnaireRevisions)
            .Where(application =>
                application.TenantId == actor.TenantId &&
                application.DeletedAt == null);
        if (request.EventId.HasValue)
        {
            query = query.Where(application => application.EventId == request.EventId.Value);
        }

        var candidates = await query
            .OrderByDescending(application => application.CreatedAt)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var applications = new List<TestingProjectApplication>();
        foreach (var application in candidates)
        {
            if (await projectAuthorizationService.HasPermissionAsync(
                    application.ProjectId,
                    PermissionType.Read,
                    cancellationToken).ConfigureAwait(false))
            {
                applications.Add(application);
            }
        }
        return Result.Success<IReadOnlyList<TestingProjectApplicationProjection>>(
            applications.Select(ToProjection).ToList());
    }

    public async Task<Result<IReadOnlyList<TestingApplicationTesterEligibilityProjection>>> Handle(
        GetTestingApplicationTesterEligibilityQuery request,
        CancellationToken cancellationToken)
    {
        var actor = await RequireActorAsync(cancellationToken).ConfigureAwait(false);
        if (actor.Error != null)
        {
            return Result.Failure<IReadOnlyList<TestingApplicationTesterEligibilityProjection>>(actor.Error);
        }

        var testingEvent = await context.Set<TestingEvent>()
            .AsNoTracking()
            .FirstOrDefaultAsync(candidate =>
                candidate.Id == request.EventId &&
                candidate.TenantId == actor.TenantId &&
                candidate.DeletedAt == null,
                cancellationToken)
            .ConfigureAwait(false);
        if (testingEvent == null)
        {
            return Result.Failure<IReadOnlyList<TestingApplicationTesterEligibilityProjection>>(
                Error.NotFound("TestingLab.EventNotFound", "Testing event not found."));
        }

        var isCommitteeMember = await context.Set<TestingCommitteeMember>().AnyAsync(member =>
            member.EventId == request.EventId &&
            member.UserId == actor.UserId &&
            member.IsActive &&
            member.DeletedAt == null,
            cancellationToken).ConfigureAwait(false);
        var hasApplicationRead = await HasApplicationPermissionAsync(
            actor,
            TestingLabActions.Read,
            null,
            cancellationToken).ConfigureAwait(false);
        if (testingEvent.ManagerUserId != actor.UserId && !isCommitteeMember && !IsTenantAdmin && !hasApplicationRead)
        {
            return Result.Failure<IReadOnlyList<TestingApplicationTesterEligibilityProjection>>(
                Error.Forbidden("TestingLab.EventReviewerRequired", "Event manager or committee access is required."));
        }

        var requestedTesterIds = request.TesterUserIds
            .Where(id => id != Guid.Empty)
            .Distinct()
            .Take(100)
            .ToArray();
        if (requestedTesterIds.Length == 0)
        {
            return Result.Success<IReadOnlyList<TestingApplicationTesterEligibilityProjection>>([]);
        }

        var activeTesterIds = await context.Set<TenantMember>()
            .AsNoTracking()
            .Where(member =>
                member.TenantId == actor.TenantId &&
                requestedTesterIds.Contains(member.UserId) &&
                member.IsActive &&
                member.DeletedAt == null)
            .Select(member => member.UserId)
            .Distinct()
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var applications = await (
                from application in context.Set<TestingProjectApplication>().AsNoTracking()
                join project in context.Set<Project>().AsNoTracking() on application.ProjectId equals project.Id
                where application.EventId == request.EventId &&
                      application.TenantId == actor.TenantId &&
                      application.Status == TestingApplicationStatus.Approved &&
                      application.DeletedAt == null &&
                      project.TenantId == actor.TenantId &&
                      project.DeletedAt == null
                select new { ApplicationId = application.Id, application.ProjectId, project.CreatedById })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var projectIds = applications.Select(application => application.ProjectId).Distinct().ToArray();

        var collaboratorConflicts = await context.Set<ProjectCollaborator>()
            .AsNoTracking()
            .Where(collaborator =>
                projectIds.Contains(collaborator.ProjectId) &&
                activeTesterIds.Contains(collaborator.UserId) &&
                collaborator.IsActive &&
                collaborator.DeletedAt == null &&
                collaborator.LeftAt == null)
            .Select(collaborator => new { collaborator.ProjectId, collaborator.UserId })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var teamConflicts = await (
                from projectTeam in context.Set<ProjectTeam>().AsNoTracking()
                join team in context.Set<Team>().AsNoTracking() on projectTeam.TeamId equals team.Id
                join member in context.Set<TeamMember>().AsNoTracking() on team.Id equals member.TeamId
                where projectIds.Contains(projectTeam.ProjectId) &&
                      activeTesterIds.Contains(member.UserId) &&
                      projectTeam.TenantId == actor.TenantId &&
                      projectTeam.IsActive &&
                      projectTeam.DeletedAt == null &&
                      projectTeam.EndedAt == null &&
                      team.TenantId == actor.TenantId &&
                      team.IsActive &&
                      team.DeletedAt == null &&
                      member.TenantId == actor.TenantId &&
                      member.IsActive &&
                      member.DeletedAt == null &&
                      member.LeftAt == null
                select new { projectTeam.ProjectId, member.UserId })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var conflicts = collaboratorConflicts
            .Select(item => (item.ProjectId, item.UserId))
            .Concat(teamConflicts.Select(item => (item.ProjectId, item.UserId)))
            .ToHashSet();

        var result = requestedTesterIds.Select(testerId =>
            new TestingApplicationTesterEligibilityProjection(
                testerId,
                activeTesterIds.Contains(testerId)
                    ? applications
                        .Where(application =>
                            application.CreatedById != testerId &&
                            !conflicts.Contains((application.ProjectId, testerId)))
                        .Select(application => application.ApplicationId)
                        .ToArray()
                    : [])).ToList();
        return Result.Success<IReadOnlyList<TestingApplicationTesterEligibilityProjection>>(result);
    }

    public async Task<Result<TestingApplicationReviewPackageProjection>> Handle(
        GetTestingApplicationReviewPackageQuery request,
        CancellationToken cancellationToken)
    {
        var loaded = await LoadApplicationAsync(request.ApplicationId, cancellationToken).ConfigureAwait(false);
        if (loaded.Error != null)
        {
            return Result.Failure<TestingApplicationReviewPackageProjection>(loaded.Error);
        }

        var application = loaded.Application!;
        var actor = loaded.Actor!;
        var isCommitteeMember = await context.Set<TestingCommitteeMember>().AnyAsync(member =>
            member.EventId == application.EventId && member.UserId == actor.UserId &&
            member.IsActive && member.DeletedAt == null,
            cancellationToken).ConfigureAwait(false);
        var isAssignedTester = await context.Set<TestingFeedbackObligation>().AnyAsync(obligation =>
            obligation.ApplicationId == application.Id &&
            obligation.TesterUserId == actor.UserId &&
            obligation.TenantId == actor.TenantId &&
            obligation.DeletedAt == null,
            cancellationToken).ConfigureAwait(false);
        var canReview = IsTenantAdmin || application.Event.ManagerUserId == actor.UserId || isCommitteeMember ||
            isAssignedTester ||
            await HasApplicationPermissionAsync(actor, TestingLabActions.Read, application.Id, cancellationToken).ConfigureAwait(false);
        if (!canReview)
        {
            return Result.Failure<TestingApplicationReviewPackageProjection>(
                Error.Forbidden("TestingLab.EventReviewerRequired", "Only an event reviewer can open the submitted review package."));
        }

        if (application.ProjectVersionId == null)
        {
            return Result.Failure<TestingApplicationReviewPackageProjection>(
                Error.NotFound("TestingLab.ProjectVersionNotFound", "The submitted project version is unavailable."));
        }

        var version = await context.Set<ProjectVersion>().AsNoTracking().SingleOrDefaultAsync(candidate =>
            candidate.Id == application.ProjectVersionId && candidate.ProjectId == application.ProjectId &&
            candidate.TenantId == actor.TenantId && candidate.DeletedAt == null,
            cancellationToken).ConfigureAwait(false);
        if (version == null)
        {
            return Result.Failure<TestingApplicationReviewPackageProjection>(
                Error.NotFound("TestingLab.ProjectVersionNotFound", "The submitted project version is unavailable."));
        }

        var assetIds = application.SubmittedAssetReferenceIds.ToArray();
        List<GameGuild.Assets.AssetReference> assets = assetIds.Length == 0
            ? []
            : await context.Set<GameGuild.Assets.AssetReference>().AsNoTracking()
                .Where(asset => assetIds.Contains(asset.Id) && asset.TenantId == actor.TenantId && asset.DeletedAt == null)
                .OrderBy(asset => asset.DisplayName).ToListAsync(cancellationToken).ConfigureAwait(false);
        var assetResults = new List<TestingApplicationReviewAssetProjection>(assets.Count);
        if (assets.Count > 0 && assetScopedAccessService != null && assetAccessService != null)
        {
            var expiresAt = SystemClock.UtcNow.AddMinutes(15);
            await assetScopedAccessService.GrantAsync(
                assets.Select(asset => asset.Id).ToArray(),
                actor.UserId,
                actor.TenantId,
                TestingLabAssetScopes.ApplicationReview,
                application.Id,
                expiresAt,
                actor.UserId,
                cancellationToken).ConfigureAwait(false);
            foreach (var asset in assets)
            {
                var access = await assetAccessService.GenerateAccessUrlAsync(
                    asset.Id,
                    actor.UserId,
                    actor.TenantId,
                    ct: cancellationToken).ConfigureAwait(false);
                if (access != null)
                {
                    assetResults.Add(new TestingApplicationReviewAssetProjection(
                        asset.Id, asset.DisplayName, access.MimeType, access.Url, access.ExpiresAt));
                }
            }
        }

        return Result.Success(new TestingApplicationReviewPackageProjection(
            application.Id,
            application.ProjectId,
            version.Id,
            version.VersionNumber,
            version.Status,
            version.ReleaseNotes,
            assetResults,
            application.Brief,
            application.QuestionnaireRevisions
                .SingleOrDefault(revision => revision.Id == application.CurrentQuestionnaireRevisionId)
                ?.Schema));
    }

    private async Task<LoadedApplication> LoadApplicationAsync(Guid applicationId, CancellationToken cancellationToken)
    {
        var actor = await RequireActorAsync(cancellationToken).ConfigureAwait(false);
        if (actor.Error != null)
        {
            return new(null, null, actor.Error);
        }

        var application = await context.Set<TestingProjectApplication>()
            .Include(candidate => candidate.Event)
            .Include(candidate => candidate.Votes)
            .Include(candidate => candidate.QuestionnaireRevisions)
            .FirstOrDefaultAsync(candidate =>
                candidate.Id == applicationId &&
                candidate.TenantId == actor.TenantId &&
                candidate.DeletedAt == null,
                cancellationToken)
            .ConfigureAwait(false);
        return application == null
            ? new(null, null, Error.NotFound("TestingLab.ApplicationNotFound", "Testing project application not found."))
            : new(application, actor, null);
    }

    private async Task<LoadedApplication> LoadManagedApplicationAsync(Guid applicationId, CancellationToken cancellationToken)
    {
        var loaded = await LoadApplicationAsync(applicationId, cancellationToken).ConfigureAwait(false);
        if (loaded.Error != null)
        {
            return loaded;
        }

        var actor = loaded.Actor!;
        var hasManagementPermission = await HasApplicationPermissionAsync(
            actor,
            TestingLabActions.Approve,
            applicationId,
            cancellationToken).ConfigureAwait(false) || await HasApplicationPermissionAsync(
            actor,
            TestingLabActions.Manage,
            applicationId,
            cancellationToken).ConfigureAwait(false);
        return loaded.Application!.Event.ManagerUserId == actor.UserId || IsTenantAdmin || hasManagementPermission
            ? loaded
            : new(null, null, Error.Forbidden("TestingLab.EventManagerRequired", "Only the event manager can decide applications."));
    }

    private async Task<EventApplicationAccess> ResolveEventApplicationAccessAsync(
        Guid eventId,
        CancellationToken cancellationToken)
    {
        var actor = await RequireActorAsync(cancellationToken).ConfigureAwait(false);
        if (actor.Error != null)
        {
            return new(null, null, false, false, false, actor.Error);
        }

        var testingEvent = await context.Set<TestingEvent>().AsNoTracking().FirstOrDefaultAsync(candidate =>
            candidate.Id == eventId &&
            candidate.TenantId == actor.TenantId &&
            candidate.DeletedAt == null,
            cancellationToken).ConfigureAwait(false);
        if (testingEvent == null)
        {
            return new(null, null, false, false, false, Error.NotFound("TestingLab.EventNotFound", "Testing event not found."));
        }

        var isCommitteeMember = await context.Set<TestingCommitteeMember>().AnyAsync(member =>
            member.EventId == eventId &&
            member.UserId == actor.UserId &&
            member.IsActive &&
            member.DeletedAt == null,
            cancellationToken).ConfigureAwait(false);
        var canReadApplications = testingEvent.ManagerUserId == actor.UserId ||
            isCommitteeMember ||
            IsTenantAdmin ||
            await HasApplicationPermissionAsync(actor, TestingLabActions.Read, null, cancellationToken).ConfigureAwait(false);
        var canManageApplications = testingEvent.ManagerUserId == actor.UserId ||
            IsTenantAdmin ||
            await HasApplicationPermissionAsync(actor, TestingLabActions.Approve, null, cancellationToken).ConfigureAwait(false) ||
            await HasApplicationPermissionAsync(actor, TestingLabActions.Manage, null, cancellationToken).ConfigureAwait(false);
        var canVote = testingEvent.ApprovalMode == TestingEventApprovalMode.Committee &&
            (isCommitteeMember || IsTenantAdmin);
        return new(testingEvent, actor, canReadApplications, canManageApplications, canVote, null);
    }

    private Task<bool> HasApplicationPermissionAsync(
        ActorScope actor,
        string action,
        Guid? applicationId,
        CancellationToken cancellationToken)
        => testingLabPermissionService == null
            ? Task.FromResult(false)
            : testingLabPermissionService.HasPermissionAsync(
                actor.UserId,
                actor.TenantId,
                action,
                TestingLabResourceTypes.Application,
                applicationId);

    private async Task<ActorScope> RequireActorAsync(CancellationToken cancellationToken)
    {
        var actor = actorContextAccessor.ActorContext;
        var userId = actor.SubjectIdAsGuid;
        if (!actor.IsAuthenticated || userId == null || actor.TenantId == null)
        {
            return new(Guid.Empty, Guid.Empty, Error.Unauthorized("TestingLab.Unauthenticated", "An authenticated tenant actor is required."));
        }

        if (!await projectAuthorizationService.IsActorActiveTenantMemberAsync(cancellationToken).ConfigureAwait(false))
        {
            return new(Guid.Empty, Guid.Empty, Error.Unauthorized("TestingLab.InactiveActor", "An active user and tenant membership are required."));
        }

        return new(userId.Value, actor.TenantId.Value, null);
    }

    private async Task<Error?> ValidateCommitteeDecisionAsync(
        TestingProjectApplication application,
        TestingApplicationVoteDecision requestedDecision,
        CancellationToken cancellationToken)
    {
        if (application.Event.ApprovalMode == TestingEventApprovalMode.ManagerOnly)
        {
            return null;
        }

        var reviewerCount = await context.Set<TestingCommitteeMember>().CountAsync(member =>
            member.EventId == application.EventId && member.IsActive && member.DeletedAt == null,
            cancellationToken).ConfigureAwait(false);
        if (reviewerCount == 0)
        {
            return Validation("Committee review requires at least one active reviewer.");
        }

        var votes = await context.Set<TestingApplicationVote>()
            .Where(vote => vote.ApplicationId == application.Id && vote.DeletedAt == null)
            .Select(vote => vote.Decision)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var matchingVotes = votes.Count(vote => vote == requestedDecision);
        if (matchingVotes > reviewerCount / 2)
        {
            return null;
        }

        var approveVotes = votes.Count(vote => vote == TestingApplicationVoteDecision.Approve);
        var rejectVotes = votes.Count(vote => vote == TestingApplicationVoteDecision.Reject);
        if (votes.Count >= reviewerCount && approveVotes == rejectVotes)
        {
            return null;
        }

        return Validation("The committee has not reached the required majority for this decision.");
    }

    private async Task<Error?> ValidateProjectCapacityAsync(
        TestingEventSlot slot,
        Guid currentApplicationId,
        CancellationToken cancellationToken)
    {
        if (!slot.MaxProjects.HasValue)
        {
            return null;
        }

        var approvedProjects = await context.Set<TestingProjectApplication>().CountAsync(application =>
            application.AssignedSlotId == slot.Id &&
            application.Id != currentApplicationId &&
            application.Status == TestingApplicationStatus.Approved &&
            application.DeletedAt == null,
            cancellationToken).ConfigureAwait(false);
        return approvedProjects >= slot.MaxProjects.Value
            ? Error.Conflict("TestingLab.ProjectCapacityReached", "The selected event slot has no project capacity remaining.")
            : null;
    }

    private static Error Validation(string message) => Error.Validation("TestingLab.Validation", message);

    private static bool AcceptsApplicationChanges(TestingEvent testingEvent)
    {
        var now = SystemClock.UtcNow;
        return testingEvent.Status == TestingEventStatus.ApplicationsOpen &&
               now >= testingEvent.ApplicationsOpenAt &&
               now <= testingEvent.ApplicationsCloseAt;
    }

    private async Task ValidateSubmittedAssetsAsync(
        Guid projectId,
        Guid projectVersionId,
        IReadOnlyList<Guid>? assetReferenceIds,
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        var normalizedIds = assetReferenceIds?
            .Where(id => id != Guid.Empty)
            .Distinct()
            .Take(100)
            .ToArray() ?? [];
        if (normalizedIds.Length == 0)
        {
            return;
        }

        var validAssetCount = await context.Set<GameGuild.Assets.AssetReference>().AsNoTracking().CountAsync(asset =>
            normalizedIds.Contains(asset.Id) &&
            asset.TenantId == tenantId &&
            asset.DeletedAt == null &&
            ((asset.ParentResourceType == nameof(Project) && asset.ParentResourceId == projectId) ||
             (asset.ParentResourceType == nameof(ProjectVersion) && asset.ParentResourceId == projectVersionId)),
            cancellationToken).ConfigureAwait(false);
        if (validAssetCount != normalizedIds.Length)
        {
            throw new ArgumentException("Every submitted file must belong to the selected project or project version.");
        }
    }

    private static TestingProjectApplicationProjection ToProjection(TestingProjectApplication application) => new(
        application.Id,
        application.EventId,
        application.ProjectId,
        application.ProjectVersionId,
        application.SubmittedByUserId,
        application.PreferredAvailability,
        application.Status,
        application.AssignedSlotId,
        application.DecidedByUserId,
        application.DecisionRationale,
        application.DecidedAt,
        application.Votes
            .Where(vote => vote.DeletedAt == null)
            .OrderBy(vote => vote.CreatedAt)
            .Select(ToProjection)
            .ToList(),
        application.SubmittedAssetReferenceIds,
        application.SubmissionVersionPolicy,
        application.Brief,
        application.EventApplicationResponse,
        application.RulesAcceptedAt,
        application.CurrentQuestionnaireRevisionId,
        application.QuestionnaireRevisions
            .SingleOrDefault(revision => revision.Id == application.CurrentQuestionnaireRevisionId)
            ?.Schema);

    private async Task<VersionSubmissionPolicy> GetCurrentVersionPolicyAsync(
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        return await context.Set<TestingLabSettings>().AsNoTracking()
            .Where(settings => settings.TenantId == tenantId && settings.DeletedAt == null)
            .Select(settings => (VersionSubmissionPolicy?)settings.VersionSubmissionPolicy)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false)
            ?? VersionSubmissionPolicy.ReadyMutableUntilReview;
    }

    private static TestingApplicationVoteProjection ToProjection(TestingApplicationVote vote) => new(
        vote.Id,
        vote.ReviewerId,
        vote.Decision,
        vote.Comments,
        vote.CreatedAt);

    private sealed record ActorScope(Guid UserId, Guid TenantId, Error? Error);
    private sealed record LoadedApplication(TestingProjectApplication? Application, ActorScope? Actor, Error? Error);
    private sealed record EventApplicationAccess(
        TestingEvent? Event,
        ActorScope? Actor,
        bool CanViewApplications,
        bool CanManageApplications,
        bool CanVote,
        Error? Error);
}
