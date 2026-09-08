using GameGuild.Assets;
using GameGuild.CQRS;
using GameGuild.Projects;
using Microsoft.EntityFrameworkCore;

namespace GameGuild.LaunchPad;

public sealed record RejectLegacyDirectPlanCreationCommand : ICommand;

public sealed class RejectLegacyDirectPlanCreationCommandHandler : ICommandHandler<RejectLegacyDirectPlanCreationCommand>
{
    public Task<Unit> Handle(RejectLegacyDirectPlanCreationCommand request, CancellationToken cancellationToken) =>
        Task.FromResult(Unit.Value);
}

public sealed record CreateLaunchPadEventEndpointCommand(Guid TenantId, CreateLaunchPadEventRequest Request) : ICommand<LaunchPadEvent>;
public sealed record TransitionLaunchPadEventEndpointCommand(LaunchPadEvent Entity, LaunchPadEventStatus Status) : ICommand<LaunchPadEvent>;
public sealed record UpdateLaunchPadEventEndpointCommand(LaunchPadEvent Entity, UpdateLaunchPadEventRequest Request) : ICommand<LaunchPadEvent>;
public sealed record CreateLaunchPadSlotEndpointCommand(Guid TenantId, Guid EventId, CreateLaunchPadSlotRequest Request) : ICommand<LaunchPadParticipantSlot>;
public sealed record UpdateLaunchPadSlotEndpointCommand(LaunchPadParticipantSlot Entity, CreateLaunchPadSlotRequest Request) : ICommand<LaunchPadParticipantSlot>;
public sealed record DeleteLaunchPadSlotEndpointCommand(LaunchPadParticipantSlot Entity) : ICommand;

public sealed class LaunchPadEventEndpointCommandHandler(IApplicationDbContext context) :
    ICommandHandler<CreateLaunchPadEventEndpointCommand, LaunchPadEvent>,
    ICommandHandler<TransitionLaunchPadEventEndpointCommand, LaunchPadEvent>,
    ICommandHandler<UpdateLaunchPadEventEndpointCommand, LaunchPadEvent>,
    ICommandHandler<CreateLaunchPadSlotEndpointCommand, LaunchPadParticipantSlot>,
    ICommandHandler<UpdateLaunchPadSlotEndpointCommand, LaunchPadParticipantSlot>,
    ICommandHandler<DeleteLaunchPadSlotEndpointCommand>
{
    public async Task<LaunchPadEvent> Handle(CreateLaunchPadEventEndpointCommand command, CancellationToken cancellationToken)
    {
        var request = command.Request;
        var entity = LaunchPadEvent.Create(command.TenantId, request.Name, request.StartsAt, request.EndsAt, request.Description);
        if (request.ApplicationsOpenAt.HasValue && request.ApplicationsCloseAt.HasValue)
            entity.ConfigureApplicationWindow(request.ApplicationsOpenAt.Value, request.ApplicationsCloseAt.Value);
        context.Set<LaunchPadEvent>().Add(entity);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return entity;
    }

    public async Task<LaunchPadEvent> Handle(TransitionLaunchPadEventEndpointCommand command, CancellationToken cancellationToken)
    {
        switch (command.Status)
        {
            case LaunchPadEventStatus.ApplicationsOpen: command.Entity.OpenApplications(); break;
            case LaunchPadEventStatus.ApplicationsClosed: command.Entity.CloseApplications(); break;
            case LaunchPadEventStatus.Scheduled: command.Entity.Schedule(); break;
            case LaunchPadEventStatus.Active: command.Entity.Activate(); break;
            case LaunchPadEventStatus.Completed: command.Entity.Complete(); break;
            case LaunchPadEventStatus.Cancelled: command.Entity.Cancel(); break;
            case LaunchPadEventStatus.Archived: command.Entity.Archive(); break;
            default: throw new ArgumentOutOfRangeException(nameof(command.Status));
        }

        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return command.Entity;
    }

    public async Task<LaunchPadEvent> Handle(UpdateLaunchPadEventEndpointCommand command, CancellationToken cancellationToken)
    {
        var request = command.Request;
        command.Entity.Update(request.Name, request.Description, request.StartsAt, request.EndsAt,
            request.ApplicationsOpenAt, request.ApplicationsCloseAt);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return command.Entity;
    }

    public async Task<LaunchPadParticipantSlot> Handle(CreateLaunchPadSlotEndpointCommand command, CancellationToken cancellationToken)
    {
        var request = command.Request;
        var slot = LaunchPadParticipantSlot.Create(command.TenantId, command.EventId, request.Name, request.Role,
            request.Capacity, request.StartsAt, request.EndsAt);
        context.Set<LaunchPadParticipantSlot>().Add(slot);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return slot;
    }

    public async Task<LaunchPadParticipantSlot> Handle(UpdateLaunchPadSlotEndpointCommand command, CancellationToken cancellationToken)
    {
        var request = command.Request;
        command.Entity.Update(request.Name, request.Role, request.Capacity, request.StartsAt, request.EndsAt);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return command.Entity;
    }

    public async Task<Unit> Handle(DeleteLaunchPadSlotEndpointCommand command, CancellationToken cancellationToken)
    {
        context.Set<LaunchPadParticipantSlot>().Remove(command.Entity);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Unit.Value;
    }
}

public sealed record SubmitLaunchPadApplicationEndpointCommand(
    Guid TenantId, Guid EventId, Guid ProjectId, Guid ProjectVersionId, Guid ActorId, string? Pitch,
    IReadOnlyList<Guid> AssetIds, VersionSubmissionPolicy SubmissionPolicy) : ICommand<LaunchPadApplication>;
public sealed record UpdateLaunchPadApplicationEndpointCommand(
    LaunchPadApplication Entity, Guid ProjectVersionId, string? Pitch, IReadOnlyList<Guid> AssetIds) : ICommand<LaunchPadApplication>;
public sealed record WithdrawLaunchPadApplicationEndpointCommand(LaunchPadApplication Entity) : ICommand<LaunchPadApplication>;
public sealed record ReviewLaunchPadApplicationEndpointCommand(
    LaunchPadApplication Entity, LaunchPadApplicationStatus Status, Guid ReviewerId, string? LaunchPlanName) : ICommand<LaunchPadApplication>;

public sealed class LaunchPadApplicationEndpointCommandHandler(
    IApplicationDbContext context,
    IAssetScopedAccessService assetScopedAccessService) :
    ICommandHandler<SubmitLaunchPadApplicationEndpointCommand, LaunchPadApplication>,
    ICommandHandler<UpdateLaunchPadApplicationEndpointCommand, LaunchPadApplication>,
    ICommandHandler<WithdrawLaunchPadApplicationEndpointCommand, LaunchPadApplication>,
    ICommandHandler<ReviewLaunchPadApplicationEndpointCommand, LaunchPadApplication>
{
    public async Task<LaunchPadApplication> Handle(SubmitLaunchPadApplicationEndpointCommand command, CancellationToken cancellationToken)
    {
        var application = LaunchPadApplication.Submit(command.TenantId, command.EventId, command.ProjectId,
            command.ProjectVersionId, command.ActorId, command.Pitch, command.AssetIds, command.SubmissionPolicy);
        context.Set<LaunchPadApplication>().Add(application);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return application;
    }

    public async Task<LaunchPadApplication> Handle(UpdateLaunchPadApplicationEndpointCommand command, CancellationToken cancellationToken)
    {
        command.Entity.Update(command.ProjectVersionId, command.Pitch, command.AssetIds);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return command.Entity;
    }

    public async Task<LaunchPadApplication> Handle(WithdrawLaunchPadApplicationEndpointCommand command, CancellationToken cancellationToken)
    {
        command.Entity.Withdraw();
        await assetScopedAccessService.RevokeScopeAsync("LaunchPadApplication", command.Entity.Id, cancellationToken).ConfigureAwait(false);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return command.Entity;
    }

    public async Task<LaunchPadApplication> Handle(ReviewLaunchPadApplicationEndpointCommand command, CancellationToken cancellationToken)
    {
        var application = command.Entity;
        switch (command.Status)
        {
            case LaunchPadApplicationStatus.UnderReview: application.StartReview(); break;
            case LaunchPadApplicationStatus.Waitlisted: application.Waitlist(command.ReviewerId); break;
            case LaunchPadApplicationStatus.Approved:
                application.Approve(command.ReviewerId);
                var exists = await context.Set<LaunchPlan>().AnyAsync(
                    plan => plan.LaunchPadApplicationId == application.Id && plan.DeletedAt == null,
                    cancellationToken).ConfigureAwait(false);
                if (!exists)
                    context.Set<LaunchPlan>().Add(LaunchPlan.CreateForApprovedApplication(application.TenantId!.Value,
                        application.LaunchPadEventId, application.Id, application.ProjectId, application.ProjectVersionId,
                        command.LaunchPlanName ?? "Launch plan"));
                break;
            case LaunchPadApplicationStatus.Rejected: application.Reject(command.ReviewerId); break;
            default: throw new ArgumentOutOfRangeException(nameof(command.Status));
        }

        if (command.Status is LaunchPadApplicationStatus.UnderReview or LaunchPadApplicationStatus.Waitlisted &&
            application.SubmittedAssetReferenceIds.Count > 0)
        {
            var expiresAt = application.LaunchPadEvent.EndsAt.AddDays(7);
            if (expiresAt <= SystemClock.UtcNow) expiresAt = SystemClock.UtcNow.AddHours(24);
            await assetScopedAccessService.GrantAsync(application.SubmittedAssetReferenceIds, command.ReviewerId,
                application.TenantId!.Value, "LaunchPadApplication", application.Id, expiresAt, command.ReviewerId,
                cancellationToken).ConfigureAwait(false);
        }
        else if (command.Status is LaunchPadApplicationStatus.Approved or LaunchPadApplicationStatus.Rejected)
        {
            await assetScopedAccessService.RevokeScopeAsync("LaunchPadApplication", application.Id, cancellationToken).ConfigureAwait(false);
        }

        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return application;
    }
}

public sealed record RegisterLaunchPadParticipantEndpointCommand(LaunchPadParticipantSlot Slot, Guid ActorId) : ICommand<LaunchPadParticipantRegistration>;
public sealed record CancelLaunchPadRegistrationEndpointCommand(LaunchPadParticipantRegistration Entity) : ICommand<LaunchPadParticipantRegistration>;
public sealed record TransitionLaunchPadRegistrationEndpointCommand(
    LaunchPadParticipantRegistration Entity, LaunchPadParticipantStatus Status) : ICommand<LaunchPadParticipantRegistration>;

public sealed class LaunchPadRegistrationEndpointCommandHandler(IApplicationDbContext context) :
    ICommandHandler<RegisterLaunchPadParticipantEndpointCommand, LaunchPadParticipantRegistration>,
    ICommandHandler<CancelLaunchPadRegistrationEndpointCommand, LaunchPadParticipantRegistration>,
    ICommandHandler<TransitionLaunchPadRegistrationEndpointCommand, LaunchPadParticipantRegistration>
{
    public async Task<LaunchPadParticipantRegistration> Handle(RegisterLaunchPadParticipantEndpointCommand command, CancellationToken cancellationToken)
    {
        var waitlisted = !command.Slot.HasCapacity;
        if (!waitlisted) command.Slot.Reserve();
        var registration = LaunchPadParticipantRegistration.Register(command.Slot.TenantId!.Value, command.Slot.Id,
            command.ActorId, waitlisted);
        context.Set<LaunchPadParticipantRegistration>().Add(registration);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return registration;
    }

    public async Task<LaunchPadParticipantRegistration> Handle(CancelLaunchPadRegistrationEndpointCommand command, CancellationToken cancellationToken)
    {
        var reserved = command.Entity.Status == LaunchPadParticipantStatus.Registered;
        command.Entity.Cancel();
        if (reserved)
        {
            command.Entity.LaunchPadParticipantSlot.Release();
            await PromoteOldestWaitlistedAsync(command.Entity.LaunchPadParticipantSlot, cancellationToken).ConfigureAwait(false);
        }
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return command.Entity;
    }

    public async Task<LaunchPadParticipantRegistration> Handle(TransitionLaunchPadRegistrationEndpointCommand command, CancellationToken cancellationToken)
    {
        var registration = command.Entity;
        switch (command.Status)
        {
            case LaunchPadParticipantStatus.Registered:
                registration.LaunchPadParticipantSlot.Reserve();
                registration.Promote();
                break;
            case LaunchPadParticipantStatus.CheckedIn: registration.CheckIn(); break;
            case LaunchPadParticipantStatus.Attended: registration.MarkAttended(); break;
            case LaunchPadParticipantStatus.Completed: registration.Complete(); break;
            case LaunchPadParticipantStatus.NoShow: registration.MarkNoShow(); break;
            case LaunchPadParticipantStatus.Cancelled:
                var reserved = registration.Status == LaunchPadParticipantStatus.Registered;
                registration.Cancel();
                if (reserved)
                {
                    registration.LaunchPadParticipantSlot.Release();
                    await PromoteOldestWaitlistedAsync(registration.LaunchPadParticipantSlot, cancellationToken).ConfigureAwait(false);
                }
                break;
            default: throw new ArgumentOutOfRangeException(nameof(command.Status));
        }

        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return registration;
    }

    private async Task PromoteOldestWaitlistedAsync(LaunchPadParticipantSlot slot, CancellationToken cancellationToken)
    {
        if (!slot.HasCapacity) return;
        var next = await context.Set<LaunchPadParticipantRegistration>()
            .Where(registration => registration.LaunchPadParticipantSlotId == slot.Id &&
                                   registration.Status == LaunchPadParticipantStatus.Waitlisted &&
                                   registration.DeletedAt == null)
            .OrderBy(registration => registration.RegisteredAt)
            .ThenBy(registration => registration.Id)
            .FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
        if (next == null) return;
        slot.Reserve();
        next.Promote();
    }
}

public sealed record CreateDefaultLaunchPadSettingsCommand(Guid TenantId) : ICommand<LaunchPadSettings>;
public sealed record UpdateLaunchPadSettingsEndpointCommand(Guid TenantId, VersionSubmissionPolicy Policy) : ICommand<LaunchPadSettings>;

public sealed class LaunchPadSettingsEndpointCommandHandler(IApplicationDbContext context) :
    ICommandHandler<CreateDefaultLaunchPadSettingsCommand, LaunchPadSettings>,
    ICommandHandler<UpdateLaunchPadSettingsEndpointCommand, LaunchPadSettings>
{
    public async Task<LaunchPadSettings> Handle(CreateDefaultLaunchPadSettingsCommand command, CancellationToken cancellationToken)
    {
        var settings = await GetOrCreateAsync(command.TenantId, cancellationToken).ConfigureAwait(false);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return settings;
    }

    public async Task<LaunchPadSettings> Handle(UpdateLaunchPadSettingsEndpointCommand command, CancellationToken cancellationToken)
    {
        var settings = await GetOrCreateAsync(command.TenantId, cancellationToken).ConfigureAwait(false);
        settings.VersionSubmissionPolicy = command.Policy;
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return settings;
    }

    private async Task<LaunchPadSettings> GetOrCreateAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var settings = await context.Set<LaunchPadSettings>()
            .SingleOrDefaultAsync(candidate => candidate.TenantId == tenantId && candidate.DeletedAt == null, cancellationToken)
            .ConfigureAwait(false);
        if (settings != null) return settings;

        settings = new LaunchPadSettings
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            VersionSubmissionPolicy = VersionSubmissionPolicy.ReleasedImmutable
        };
        context.Set<LaunchPadSettings>().Add(settings);
        return settings;
    }
}
