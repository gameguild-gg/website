using GameGuild.CQRS;
using GameGuild.Identity.Authorization;
using Microsoft.EntityFrameworkCore;

namespace GameGuild.TestingLab;

public sealed record AddTestingFeedbackEndpointCommand(
    Guid TestingRequestId,
    Guid UserId,
    Guid FeedbackFormId,
    string FeedbackData,
    TestingContext Context,
    Guid? SessionId,
    string? AdditionalNotes) : ICommand<TestingFeedback>;
public sealed record SubmitTestingFeedbackEndpointCommand(SubmitFeedbackDto Feedback, Guid UserId) : ICommand;
public sealed record ReportTestingFeedbackEndpointCommand(Guid FeedbackId, string Reason, Guid UserId) : ICommand;
public sealed record RateTestingFeedbackQualityEndpointCommand(Guid FeedbackId, FeedbackQuality Quality, Guid UserId) : ICommand;

public sealed class TestingFeedbackEndpointCommandHandler(ITestingFeedbackOperations service) :
    ICommandHandler<AddTestingFeedbackEndpointCommand, TestingFeedback>,
    ICommandHandler<SubmitTestingFeedbackEndpointCommand>,
    ICommandHandler<ReportTestingFeedbackEndpointCommand>,
    ICommandHandler<RateTestingFeedbackQualityEndpointCommand>
{
    public Task<TestingFeedback> Handle(AddTestingFeedbackEndpointCommand request, CancellationToken cancellationToken) =>
        service.AddFeedbackAsync(request.TestingRequestId, request.UserId, request.FeedbackFormId, request.FeedbackData, request.Context, request.SessionId, request.AdditionalNotes);

    public async Task<Unit> Handle(SubmitTestingFeedbackEndpointCommand request, CancellationToken cancellationToken)
    {
        await service.SubmitFeedbackAsync(request.Feedback, request.UserId).ConfigureAwait(false);
        return Unit.Value;
    }

    public async Task<Unit> Handle(ReportTestingFeedbackEndpointCommand request, CancellationToken cancellationToken)
    {
        await service.ReportFeedbackAsync(request.FeedbackId, request.Reason, request.UserId).ConfigureAwait(false);
        return Unit.Value;
    }

    public async Task<Unit> Handle(RateTestingFeedbackQualityEndpointCommand request, CancellationToken cancellationToken)
    {
        await service.RateFeedbackQualityAsync(request.FeedbackId, request.Quality, request.UserId).ConfigureAwait(false);
        return Unit.Value;
    }
}

public sealed record CreateTestingLocationEndpointCommand(CreateTestingLocationDto Location) : ICommand<TestingLocation>;
public sealed record UpdateTestingLocationEndpointCommand(Guid LocationId, UpdateTestingLocationDto Location) : ICommand<TestingLocation?>;
public sealed record DeleteTestingLocationEndpointCommand(Guid LocationId) : ICommand<bool>;
public sealed record RestoreTestingLocationEndpointCommand(Guid LocationId) : ICommand<bool>;

public sealed class TestingLocationEndpointCommandHandler(ITestingLocationOperations service) :
    ICommandHandler<CreateTestingLocationEndpointCommand, TestingLocation>,
    ICommandHandler<UpdateTestingLocationEndpointCommand, TestingLocation?>,
    ICommandHandler<DeleteTestingLocationEndpointCommand, bool>,
    ICommandHandler<RestoreTestingLocationEndpointCommand, bool>
{
    public Task<TestingLocation> Handle(CreateTestingLocationEndpointCommand request, CancellationToken cancellationToken) =>
        service.CreateTestingLocationAsync(request.Location.ToTestingLocation());

    public async Task<TestingLocation?> Handle(UpdateTestingLocationEndpointCommand request, CancellationToken cancellationToken)
    {
        var location = await service.GetTestingLocationByIdAsync(request.LocationId).ConfigureAwait(false);
        if (location is null) return null;
        request.Location.UpdateTestingLocation(location);
        return await service.UpdateTestingLocationAsync(location).ConfigureAwait(false);
    }

    public Task<bool> Handle(DeleteTestingLocationEndpointCommand request, CancellationToken cancellationToken) =>
        service.DeleteTestingLocationAsync(request.LocationId);

    public Task<bool> Handle(RestoreTestingLocationEndpointCommand request, CancellationToken cancellationToken) =>
        service.RestoreTestingLocationAsync(request.LocationId);
}

public sealed record AddTestingParticipantEndpointCommand(Guid TestingRequestId, Guid UserId) : ICommand<TestingParticipant>;
public sealed record RemoveTestingParticipantEndpointCommand(Guid TestingRequestId, Guid UserId) : ICommand<bool>;
public sealed record RegisterTestingSessionEndpointCommand(Guid SessionId, Guid UserId, RegistrationType RegistrationType, string? Notes) : ICommand<SessionRegistration>;
public sealed record UnregisterTestingSessionEndpointCommand(Guid SessionId, Guid UserId) : ICommand<bool>;
public sealed record AddTestingSessionWaitlistEndpointCommand(Guid SessionId, Guid UserId, RegistrationType RegistrationType, string? Notes) : ICommand<SessionWaitlist>;
public sealed record RemoveTestingSessionWaitlistEndpointCommand(Guid SessionId, Guid UserId) : ICommand<bool>;

public sealed class TestingParticipantEndpointCommandHandler(ITestingParticipantOperations service) :
    ICommandHandler<AddTestingParticipantEndpointCommand, TestingParticipant>,
    ICommandHandler<RemoveTestingParticipantEndpointCommand, bool>,
    ICommandHandler<RegisterTestingSessionEndpointCommand, SessionRegistration>,
    ICommandHandler<UnregisterTestingSessionEndpointCommand, bool>,
    ICommandHandler<AddTestingSessionWaitlistEndpointCommand, SessionWaitlist>,
    ICommandHandler<RemoveTestingSessionWaitlistEndpointCommand, bool>
{
    public Task<TestingParticipant> Handle(AddTestingParticipantEndpointCommand request, CancellationToken cancellationToken) =>
        service.AddParticipantAsync(request.TestingRequestId, request.UserId);

    public Task<bool> Handle(RemoveTestingParticipantEndpointCommand request, CancellationToken cancellationToken) =>
        service.RemoveParticipantAsync(request.TestingRequestId, request.UserId);

    public Task<SessionRegistration> Handle(RegisterTestingSessionEndpointCommand request, CancellationToken cancellationToken) =>
        service.RegisterForSessionAsync(request.SessionId, request.UserId, request.RegistrationType, request.Notes);

    public Task<bool> Handle(UnregisterTestingSessionEndpointCommand request, CancellationToken cancellationToken) =>
        service.UnregisterFromSessionAsync(request.SessionId, request.UserId);

    public Task<SessionWaitlist> Handle(AddTestingSessionWaitlistEndpointCommand request, CancellationToken cancellationToken) =>
        service.AddToWaitlistAsync(request.SessionId, request.UserId, request.RegistrationType, request.Notes);

    public Task<bool> Handle(RemoveTestingSessionWaitlistEndpointCommand request, CancellationToken cancellationToken) =>
        service.RemoveFromWaitlistAsync(request.SessionId, request.UserId);
}

public sealed record CreateTestingSessionEndpointCommand(CreateTestingSessionDto Session, Guid UserId) : ICommand<TestingSession>;
public sealed record UpdateTestingSessionEndpointCommand(Guid SessionId, TestingSession Session) : ICommand<TestingSession>;
public sealed record DeleteTestingSessionEndpointCommand(Guid SessionId) : ICommand<bool>;
public sealed record RestoreTestingSessionEndpointCommand(Guid SessionId) : ICommand<bool>;
public sealed record UpdateTestingSessionAttendanceEndpointCommand(Guid SessionId, Guid UserId, AttendanceStatus Status, Guid UpdatedByUserId) : ICommand;

public sealed class TestingSessionEndpointCommandHandler(ITestingSessionOperations service) :
    ICommandHandler<CreateTestingSessionEndpointCommand, TestingSession>,
    ICommandHandler<UpdateTestingSessionEndpointCommand, TestingSession>,
    ICommandHandler<DeleteTestingSessionEndpointCommand, bool>,
    ICommandHandler<RestoreTestingSessionEndpointCommand, bool>,
    ICommandHandler<UpdateTestingSessionAttendanceEndpointCommand>
{
    public Task<TestingSession> Handle(CreateTestingSessionEndpointCommand request, CancellationToken cancellationToken) =>
        service.CreateTestingSessionAsync(request.Session.ToTestingSession(request.UserId));

    public Task<TestingSession> Handle(UpdateTestingSessionEndpointCommand request, CancellationToken cancellationToken) =>
        service.UpdateTestingSessionAsync(request.Session);

    public Task<bool> Handle(DeleteTestingSessionEndpointCommand request, CancellationToken cancellationToken) =>
        service.DeleteTestingSessionAsync(request.SessionId);

    public Task<bool> Handle(RestoreTestingSessionEndpointCommand request, CancellationToken cancellationToken) =>
        service.RestoreTestingSessionAsync(request.SessionId);

    public async Task<Unit> Handle(UpdateTestingSessionAttendanceEndpointCommand request, CancellationToken cancellationToken)
    {
        await service.UpdateSessionAttendanceAsync(request.SessionId, request.UserId, request.Status, request.UpdatedByUserId).ConfigureAwait(false);
        return Unit.Value;
    }
}

public sealed record CreateTestingRequestEndpointCommand(CreateTestingRequestDto Request, Guid UserId) : ICommand<TestingRequest>;
public sealed record UpdateTestingRequestEndpointCommand(Guid RequestId, UpdateTestingRequestDto Request) : ICommand<TestingRequest?>;
public sealed record DeleteTestingRequestEndpointCommand(Guid RequestId) : ICommand<bool>;
public sealed record RestoreTestingRequestEndpointCommand(Guid RequestId) : ICommand<bool>;
public sealed record CreateSimpleTestingRequestEndpointCommand(CreateSimpleTestingRequestDto Request, Guid UserId) : ICommand<TestingRequest>;

public sealed class TestingRequestEndpointCommandHandler(ITestingRequestOperations service) :
    ICommandHandler<CreateTestingRequestEndpointCommand, TestingRequest>,
    ICommandHandler<UpdateTestingRequestEndpointCommand, TestingRequest?>,
    ICommandHandler<DeleteTestingRequestEndpointCommand, bool>,
    ICommandHandler<RestoreTestingRequestEndpointCommand, bool>,
    ICommandHandler<CreateSimpleTestingRequestEndpointCommand, TestingRequest>
{
    public Task<TestingRequest> Handle(CreateTestingRequestEndpointCommand request, CancellationToken cancellationToken) =>
        service.CreateTestingRequestAsync(request.Request.ToTestingRequest(request.UserId));

    public async Task<TestingRequest?> Handle(UpdateTestingRequestEndpointCommand request, CancellationToken cancellationToken)
    {
        var entity = await service.GetTestingRequestByIdAsync(request.RequestId).ConfigureAwait(false);
        if (entity is null) return null;
        request.Request.UpdateTestingRequest(entity);
        return await service.UpdateTestingRequestAsync(entity).ConfigureAwait(false);
    }

    public Task<bool> Handle(DeleteTestingRequestEndpointCommand request, CancellationToken cancellationToken) =>
        service.DeleteTestingRequestAsync(request.RequestId);

    public Task<bool> Handle(RestoreTestingRequestEndpointCommand request, CancellationToken cancellationToken) =>
        service.RestoreTestingRequestAsync(request.RequestId);

    public Task<TestingRequest> Handle(CreateSimpleTestingRequestEndpointCommand request, CancellationToken cancellationToken) =>
        service.CreateSimpleTestingRequestAsync(request.Request, request.UserId);
}

public sealed record CreateOrUpdateTestingLabSettingsEndpointCommand(Guid? TenantId, CreateTestingLabSettingsDto Settings) : ICommand<TestingLabSettingsDto>;
public sealed record UpdateTestingLabSettingsEndpointCommand(Guid? TenantId, UpdateTestingLabSettingsDto Settings) : ICommand<TestingLabSettingsDto>;
public sealed record ResetTestingLabSettingsEndpointCommand(Guid? TenantId) : ICommand<TestingLabSettingsDto>;

public sealed class TestingLabSettingsEndpointCommandHandler(ITestingLabSettingsService service) :
    ICommandHandler<CreateOrUpdateTestingLabSettingsEndpointCommand, TestingLabSettingsDto>,
    ICommandHandler<UpdateTestingLabSettingsEndpointCommand, TestingLabSettingsDto>,
    ICommandHandler<ResetTestingLabSettingsEndpointCommand, TestingLabSettingsDto>
{
    public async Task<TestingLabSettingsDto> Handle(CreateOrUpdateTestingLabSettingsEndpointCommand request, CancellationToken cancellationToken)
    {
        await service.CreateOrUpdateTestingLabSettingsAsync(request.TenantId, request.Settings).ConfigureAwait(false);
        return await service.GetTestingLabSettingsDtoAsync(request.TenantId).ConfigureAwait(false);
    }

    public async Task<TestingLabSettingsDto> Handle(UpdateTestingLabSettingsEndpointCommand request, CancellationToken cancellationToken)
    {
        await service.UpdateTestingLabSettingsAsync(request.TenantId, request.Settings).ConfigureAwait(false);
        return await service.GetTestingLabSettingsDtoAsync(request.TenantId).ConfigureAwait(false);
    }

    public async Task<TestingLabSettingsDto> Handle(ResetTestingLabSettingsEndpointCommand request, CancellationToken cancellationToken)
    {
        await service.ResetTestingLabSettingsAsync(request.TenantId).ConfigureAwait(false);
        return await service.GetTestingLabSettingsDtoAsync(request.TenantId).ConfigureAwait(false);
    }
}

public sealed record CreateTestingLabRoleTemplateEndpointCommand(string Name, string Description, IReadOnlyCollection<PermissionTemplate> Permissions) : ICommand<RoleTemplate>;
public sealed record UpdateTestingLabRoleTemplateEndpointCommand(string IdOrName, string? Name, string Description, IReadOnlyCollection<PermissionTemplate> Permissions) : ICommand<RoleTemplate?>;
public sealed record DeleteTestingLabRoleTemplateEndpointCommand(string IdOrName) : ICommand<bool>;
public sealed record AssignTestingLabRoleEndpointCommand(Guid UserId, Guid? TenantId, string RoleName, DateTime? ExpiresAt) : ICommand;
public sealed record RevokeTestingLabRoleEndpointCommand(Guid UserId, Guid? TenantId, string RoleName) : ICommand;
public sealed record GrantTestingLabResourcePermissionEndpointCommand(Guid UserId, Guid? TenantId, string Action, string ResourceType, Guid ResourceId, DateTime? ExpiresAt, Guid GrantedByUserId) : ICommand;
public sealed record RevokeTestingLabResourcePermissionEndpointCommand(Guid UserId, Guid? TenantId, string Action, string ResourceType, Guid ResourceId, Guid RevokedByUserId) : ICommand;

public sealed class TestingLabPermissionEndpointCommandHandler(ITestingLabPermissionService service) :
    ICommandHandler<CreateTestingLabRoleTemplateEndpointCommand, RoleTemplate>,
    ICommandHandler<UpdateTestingLabRoleTemplateEndpointCommand, RoleTemplate?>,
    ICommandHandler<DeleteTestingLabRoleTemplateEndpointCommand, bool>,
    ICommandHandler<AssignTestingLabRoleEndpointCommand>,
    ICommandHandler<RevokeTestingLabRoleEndpointCommand>,
    ICommandHandler<GrantTestingLabResourcePermissionEndpointCommand>,
    ICommandHandler<RevokeTestingLabResourcePermissionEndpointCommand>
{
    public Task<RoleTemplate> Handle(CreateTestingLabRoleTemplateEndpointCommand request, CancellationToken cancellationToken) =>
        service.CreateRoleTemplateAsync(request.Name, request.Description, request.Permissions);

    public Task<RoleTemplate?> Handle(UpdateTestingLabRoleTemplateEndpointCommand request, CancellationToken cancellationToken) =>
        service.UpdateRoleTemplateAsync(request.IdOrName, request.Name, request.Description, request.Permissions);

    public Task<bool> Handle(DeleteTestingLabRoleTemplateEndpointCommand request, CancellationToken cancellationToken) =>
        service.DeleteRoleTemplateAsync(request.IdOrName);

    public async Task<Unit> Handle(AssignTestingLabRoleEndpointCommand request, CancellationToken cancellationToken)
    {
        await service.AssignRoleToUserAsync(request.UserId, request.TenantId, request.RoleName, request.ExpiresAt).ConfigureAwait(false);
        return Unit.Value;
    }

    public async Task<Unit> Handle(RevokeTestingLabRoleEndpointCommand request, CancellationToken cancellationToken)
    {
        await service.RevokeRoleFromUserAsync(request.UserId, request.TenantId, request.RoleName).ConfigureAwait(false);
        return Unit.Value;
    }

    public async Task<Unit> Handle(GrantTestingLabResourcePermissionEndpointCommand request, CancellationToken cancellationToken)
    {
        await service.GrantPermissionAsync(request.UserId, request.TenantId, request.Action, request.ResourceType, request.ResourceId, null, request.ExpiresAt, request.GrantedByUserId).ConfigureAwait(false);
        return Unit.Value;
    }

    public async Task<Unit> Handle(RevokeTestingLabResourcePermissionEndpointCommand request, CancellationToken cancellationToken)
    {
        await service.RevokePermissionAsync(request.UserId, request.TenantId, request.Action, request.ResourceType, request.ResourceId, request.RevokedByUserId).ConfigureAwait(false);
        return Unit.Value;
    }
}

public sealed record CreateTestingEventTemplateEndpointCommand(Guid TenantId, Guid UserId, UpsertTestingEventTemplateRequest Request) : ICommand<TestingEventTemplate>;
public sealed record CreateTestingEventTemplateRevisionEndpointCommand(Guid TemplateId, Guid TenantId, Guid UserId, UpsertTestingEventTemplateRequest Request) : ICommand<TestingEventTemplate?>;
public sealed record SetTestingEventTemplateArchivedEndpointCommand(Guid TemplateId, Guid TenantId, bool Archived) : ICommand<TestingEventTemplate?>;

public sealed class TestingEventTemplateEndpointCommandHandler(IApplicationDbContext context) :
    ICommandHandler<CreateTestingEventTemplateEndpointCommand, TestingEventTemplate>,
    ICommandHandler<CreateTestingEventTemplateRevisionEndpointCommand, TestingEventTemplate?>,
    ICommandHandler<SetTestingEventTemplateArchivedEndpointCommand, TestingEventTemplate?>
{
    public async Task<TestingEventTemplate> Handle(CreateTestingEventTemplateEndpointCommand request, CancellationToken cancellationToken)
    {
        var input = request.Request;
        var template = TestingEventTemplate.Create(
            request.TenantId,
            input.Name,
            input.GeneralRules,
            input.CandidateInstructions,
            input.TesterInstructions,
            input.ProjectApplicationSchema,
            input.TesterRegistrationSchema,
            input.DefaultMode,
            input.DefaultApprovalMode,
            input.DefaultRequiresFeedback,
            request.UserId,
            input.Description);
        context.Set<TestingEventTemplate>().Add(template);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return template;
    }

    public async Task<TestingEventTemplate?> Handle(CreateTestingEventTemplateRevisionEndpointCommand request, CancellationToken cancellationToken)
    {
        var template = await LoadTemplateAsync(request.TemplateId, request.TenantId, cancellationToken).ConfigureAwait(false);
        if (template is null) return null;
        var input = request.Request;
        template.CreateRevision(
            input.GeneralRules,
            input.CandidateInstructions,
            input.TesterInstructions,
            input.ProjectApplicationSchema,
            input.TesterRegistrationSchema,
            input.DefaultMode,
            input.DefaultApprovalMode,
            input.DefaultRequiresFeedback,
            request.UserId,
            input.Name,
            input.Description);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return template;
    }

    public async Task<TestingEventTemplate?> Handle(SetTestingEventTemplateArchivedEndpointCommand request, CancellationToken cancellationToken)
    {
        var template = await LoadTemplateAsync(request.TemplateId, request.TenantId, cancellationToken).ConfigureAwait(false);
        if (template is null) return null;
        if (request.Archived) template.Archive(); else template.RestoreArchivedTemplate();
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return template;
    }

    private Task<TestingEventTemplate?> LoadTemplateAsync(Guid templateId, Guid tenantId, CancellationToken cancellationToken) =>
        context.Set<TestingEventTemplate>()
            .Include(candidate => candidate.Revisions)
            .FirstOrDefaultAsync(candidate =>
                candidate.Id == templateId && candidate.TenantId == tenantId && candidate.DeletedAt == null,
                cancellationToken);
}
