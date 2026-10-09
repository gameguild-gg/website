using GameGuild.CQRS;
using GameGuild.Identity.Authorization;
using GameGuild.Identity.Authorization.Caching;
using GameGuild.Identity.Context.Actors;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace GameGuild.Social.Groups;

public sealed record SocialGroupDto(
    Guid Id,
    Guid? TenantId,
    Guid OwnerId,
    string Name,
    string Slug,
    string? Description,
    SocialGroupType Type,
    SocialGroupVisibility Visibility,
    SocialGroupStatus Status,
    int MemberCount,
    int PendingMemberCount,
    DateTime CreatedAt,
    DateTime UpdatedAt);

public sealed record SocialGroupMemberDto(
    Guid Id,
    Guid GroupId,
    Guid UserId,
    SocialGroupMemberRole Role,
    SocialGroupMembershipStatus Status,
    DateTime RequestedAt,
    DateTime? JoinedAt,
    Guid? ApprovedByUserId,
    DateTime? RemovedAt);

public sealed record CreateSocialGroupRequest(
    Guid OwnerId,
    string Name,
    string Slug,
    SocialGroupType Type,
    SocialGroupVisibility Visibility,
    string? Description = null,
    Guid? TenantId = null);

public sealed record UpdateSocialGroupRequest(
    string Name,
    string Slug,
    SocialGroupType Type,
    SocialGroupVisibility Visibility,
    string? Description = null);

public sealed record JoinSocialGroupRequest(Guid UserId, SocialGroupMemberRole RequestedRole = SocialGroupMemberRole.Member);

public sealed record ApproveSocialGroupMemberRequest(Guid ApprovedByUserId);

public sealed record ChangeSocialGroupMemberRoleRequest(SocialGroupMemberRole Role);

public sealed record CreateSocialGroupCommand(
    Guid OwnerId,
    string Name,
    string Slug,
    SocialGroupType Type,
    SocialGroupVisibility Visibility,
    string? Description,
    Guid? TenantId) : ICommand<SocialGroupDto>;

public sealed record UpdateSocialGroupCommand(
    Guid GroupId,
    string Name,
    string Slug,
    SocialGroupType Type,
    SocialGroupVisibility Visibility,
    string? Description) : ICommand<SocialGroupDto?>;

public sealed record SetSocialGroupStatusCommand(Guid GroupId, SocialGroupStatus Status) : ICommand<bool>;

public sealed record JoinSocialGroupCommand(Guid GroupId, Guid UserId, SocialGroupMemberRole RequestedRole) : ICommand<SocialGroupMemberDto?>;

public sealed record ApproveSocialGroupMemberCommand(Guid GroupId, Guid UserId, Guid ApprovedByUserId) : ICommand<bool>;

public sealed record RejectSocialGroupMemberCommand(Guid GroupId, Guid UserId) : ICommand<bool>;

public sealed record ChangeSocialGroupMemberRoleCommand(Guid GroupId, Guid UserId, SocialGroupMemberRole Role) : ICommand<bool>;

public sealed record LeaveSocialGroupCommand(Guid GroupId, Guid UserId) : ICommand<bool>;

public sealed record GetSocialGroupQuery(Guid GroupId) : IQuery<SocialGroupDto?>;

public sealed record ListSocialGroupsQuery(
    Guid? TenantId = null,
    Guid? OwnerId = null,
    SocialGroupType? Type = null,
    SocialGroupVisibility? Visibility = null,
    SocialGroupStatus? Status = null,
    string? Search = null,
    int Skip = 0,
    int Take = 50) : IQuery<IReadOnlyList<SocialGroupDto>>;

public sealed record ListSocialGroupMembersQuery(
    Guid GroupId,
    SocialGroupMembershipStatus? Status = null,
    int Skip = 0,
    int Take = 50) : IQuery<IReadOnlyList<SocialGroupMemberDto>>;

public interface ISocialGroupRepository
{
    Task<SocialGroup?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<SocialGroup?> GetBySlugAsync(string slug, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SocialGroup>> ListAsync(ListSocialGroupsQuery query, CancellationToken cancellationToken = default);

    Task AddAsync(SocialGroup group, CancellationToken cancellationToken = default);

    Task UpdateAsync(SocialGroup group, CancellationToken cancellationToken = default);
}

public interface ISocialGroupMemberRepository
{
    Task<SocialGroupMember?> GetByGroupUserAsync(Guid groupId, Guid userId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Guid>> ListActiveGroupIdsByUserAsync(Guid userId, Guid? tenantId);

    Task<IReadOnlyList<Guid>> ListActiveGroupIdsByUserAsync(Guid userId, Guid? tenantId, CancellationToken cancellationToken);

    Task<IReadOnlyList<SocialGroupMember>> ListByGroupAsync(Guid groupId, SocialGroupMembershipStatus? status, int skip, int take, CancellationToken cancellationToken = default);

    Task AddAsync(SocialGroupMember member, CancellationToken cancellationToken = default);

    Task UpdateAsync(SocialGroupMember member, CancellationToken cancellationToken = default);
}

public sealed class SocialGroupRepository(IApplicationDbContext context) : ISocialGroupRepository
{
    public Task<SocialGroup?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => context.Set<SocialGroup>().FirstOrDefaultAsync(group => group.Id == id, cancellationToken);

    public Task<SocialGroup?> GetBySlugAsync(string slug, CancellationToken cancellationToken = default)
        => context.Set<SocialGroup>().FirstOrDefaultAsync(group => group.Slug == slug.ToLowerInvariant(), cancellationToken);

    public async Task<IReadOnlyList<SocialGroup>> ListAsync(ListSocialGroupsQuery query, CancellationToken cancellationToken = default)
    {
        var groups = context.Set<SocialGroup>().AsQueryable();

        if (query.TenantId.HasValue)
        {
            groups = groups.Where(group => group.TenantId == query.TenantId.Value);
        }

        if (query.OwnerId.HasValue)
        {
            groups = groups.Where(group => group.OwnerId == query.OwnerId.Value);
        }

        if (query.Type.HasValue)
        {
            groups = groups.Where(group => group.Type == query.Type.Value);
        }

        if (query.Visibility.HasValue)
        {
            groups = groups.Where(group => group.Visibility == query.Visibility.Value);
        }

        if (query.Status.HasValue)
        {
            groups = groups.Where(group => group.Status == query.Status.Value);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim().ToLowerInvariant();
            groups = groups.Where(group => group.Name.ToLower().Contains(search) || group.Slug.Contains(search));
        }

        return await groups
            .OrderByDescending(group => group.MemberCount)
            .ThenBy(group => group.Name)
            .Skip(Math.Max(0, query.Skip))
            .Take(Math.Clamp(query.Take, 1, 100))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task AddAsync(SocialGroup group, CancellationToken cancellationToken = default)
    {
        context.Set<SocialGroup>().Add(group);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task UpdateAsync(SocialGroup group, CancellationToken cancellationToken = default)
    {
        context.Set<SocialGroup>().Update(group);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}

public sealed class SocialGroupMemberRepository(IApplicationDbContext context) : ISocialGroupMemberRepository
{
    public Task<SocialGroupMember?> GetByGroupUserAsync(Guid groupId, Guid userId, CancellationToken cancellationToken = default)
        => context.Set<SocialGroupMember>()
            .FirstOrDefaultAsync(member => member.GroupId == groupId && member.UserId == userId, cancellationToken);

    public Task<IReadOnlyList<Guid>> ListActiveGroupIdsByUserAsync(Guid userId, Guid? tenantId)
        => ListActiveGroupIdsByUserAsync(userId, tenantId, CancellationToken.None);

    public async Task<IReadOnlyList<Guid>> ListActiveGroupIdsByUserAsync(
        Guid userId,
        Guid? tenantId,
        CancellationToken cancellationToken)
    {
        var groups = context.Set<SocialGroupMember>()
            .AsNoTracking()
            .Where(member => member.UserId == userId && member.Status == SocialGroupMembershipStatus.Active)
            .Join(
                context.Set<SocialGroup>().AsNoTracking(),
                member => member.GroupId,
                group => group.Id,
                (member, group) => new { member.GroupId, group.TenantId });

        groups = tenantId.HasValue
            ? groups.Where(group => group.TenantId == null || group.TenantId == tenantId.Value)
            : groups.Where(group => group.TenantId == null);

        return await groups
            .Select(group => group.GroupId)
            .Distinct()
            .Order()
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<SocialGroupMember>> ListByGroupAsync(
        Guid groupId,
        SocialGroupMembershipStatus? status,
        int skip,
        int take,
        CancellationToken cancellationToken = default)
    {
        var query = context.Set<SocialGroupMember>().Where(member => member.GroupId == groupId);

        if (status.HasValue)
        {
            query = query.Where(member => member.Status == status.Value);
        }

        return await query
            .OrderBy(member => member.Role)
            .ThenBy(member => member.RequestedAt)
            .Skip(Math.Max(0, skip))
            .Take(Math.Clamp(take, 1, 100))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task AddAsync(SocialGroupMember member, CancellationToken cancellationToken = default)
    {
        context.Set<SocialGroupMember>().Add(member);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task UpdateAsync(SocialGroupMember member, CancellationToken cancellationToken = default)
    {
        context.Set<SocialGroupMember>().Update(member);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}

/// <summary>Supplies active Social Groups memberships when authentication issues an authorization token.</summary>
public sealed class SocialGroupAuthorizationMembershipProvider(ISocialGroupMemberRepository members) : IAuthorizationGroupMembershipProvider
{
    public Task<IReadOnlyCollection<Guid>> GetActiveGroupIdsAsync(Guid userId, Guid? tenantId)
        => GetActiveGroupIdsAsync(userId, tenantId, CancellationToken.None);

    public async Task<IReadOnlyCollection<Guid>> GetActiveGroupIdsAsync(
        Guid userId,
        Guid? tenantId,
        CancellationToken cancellationToken)
        => await members.ListActiveGroupIdsByUserAsync(userId, tenantId, cancellationToken).ConfigureAwait(false);
}

public interface ISocialGroupService
{
    Task<SocialGroupDto> CreateAsync(CreateSocialGroupCommand command, CancellationToken cancellationToken = default);

    Task<SocialGroupDto?> UpdateAsync(UpdateSocialGroupCommand command, CancellationToken cancellationToken = default);

    Task<bool> SetStatusAsync(Guid groupId, SocialGroupStatus status, CancellationToken cancellationToken = default);

    Task<SocialGroupDto?> GetAsync(Guid groupId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SocialGroupDto>> ListAsync(ListSocialGroupsQuery query, CancellationToken cancellationToken = default);

    Task<SocialGroupMemberDto?> JoinAsync(JoinSocialGroupCommand command, CancellationToken cancellationToken = default);

    Task<bool> ApproveMemberAsync(Guid groupId, Guid userId, Guid approvedByUserId, CancellationToken cancellationToken = default);

    Task<bool> RejectMemberAsync(Guid groupId, Guid userId, CancellationToken cancellationToken = default);

    Task<bool> ChangeRoleAsync(Guid groupId, Guid userId, SocialGroupMemberRole role, CancellationToken cancellationToken = default);

    Task<bool> LeaveAsync(Guid groupId, Guid userId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SocialGroupMemberDto>> ListMembersAsync(ListSocialGroupMembersQuery query, CancellationToken cancellationToken = default);
}

public sealed class SocialGroupService(
    ISocialGroupRepository groups,
    ISocialGroupMemberRepository members,
    ICacheInvalidationService cacheInvalidationService,
    IUserAuthorizationTokenVersionService tokenVersionService) : ISocialGroupService
{
    public async Task<SocialGroupDto> CreateAsync(CreateSocialGroupCommand command, CancellationToken cancellationToken = default)
    {
        var group = SocialGroup.Create(
            command.OwnerId,
            command.Name,
            command.Slug,
            command.Type,
            command.Visibility,
            command.Description,
            command.TenantId);
        var owner = SocialGroupMember.CreateOwner(group.Id, command.OwnerId);

        await groups.AddAsync(group, cancellationToken).ConfigureAwait(false);
        await members.AddAsync(owner, cancellationToken).ConfigureAwait(false);
        await tokenVersionService.IncrementAsync(command.OwnerId, cancellationToken).ConfigureAwait(false);
        await InvalidateMembershipCacheAsync(command.OwnerId, group, cancellationToken).ConfigureAwait(false);

        return ToDto(group);
    }

    public async Task<SocialGroupDto?> UpdateAsync(UpdateSocialGroupCommand command, CancellationToken cancellationToken = default)
    {
        var group = await groups.GetByIdAsync(command.GroupId, cancellationToken).ConfigureAwait(false);
        if (group is null)
        {
            return null;
        }

        group.UpdateDetails(command.Name, command.Slug, command.Description, command.Type, command.Visibility);
        await groups.UpdateAsync(group, cancellationToken).ConfigureAwait(false);
        return ToDto(group);
    }

    public async Task<bool> SetStatusAsync(Guid groupId, SocialGroupStatus status, CancellationToken cancellationToken = default)
    {
        var group = await groups.GetByIdAsync(groupId, cancellationToken).ConfigureAwait(false);
        if (group is null)
        {
            return false;
        }

        switch (status)
        {
            case SocialGroupStatus.Active:
                group.Activate();
                break;
            case SocialGroupStatus.Archived:
                group.Archive();
                break;
            case SocialGroupStatus.Suspended:
                group.Suspend();
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(status), status, null);
        }

        await groups.UpdateAsync(group, cancellationToken).ConfigureAwait(false);
        return true;
    }

    public async Task<SocialGroupDto?> GetAsync(Guid groupId, CancellationToken cancellationToken = default)
    {
        var group = await groups.GetByIdAsync(groupId, cancellationToken).ConfigureAwait(false);
        return group is null ? null : ToDto(group);
    }

    public async Task<IReadOnlyList<SocialGroupDto>> ListAsync(ListSocialGroupsQuery query, CancellationToken cancellationToken = default)
        => (await groups.ListAsync(query, cancellationToken).ConfigureAwait(false)).Select(ToDto).ToList();

    public async Task<SocialGroupMemberDto?> JoinAsync(JoinSocialGroupCommand command, CancellationToken cancellationToken = default)
    {
        var group = await groups.GetByIdAsync(command.GroupId, cancellationToken).ConfigureAwait(false);
        if (group is null || group.Status != SocialGroupStatus.Active)
        {
            return null;
        }

        var existing = await members.GetByGroupUserAsync(command.GroupId, command.UserId, cancellationToken)
            .ConfigureAwait(false);
        if (existing is not null && existing.Status is SocialGroupMembershipStatus.Active or SocialGroupMembershipStatus.Pending)
        {
            return ToDto(existing);
        }

        var approveImmediately = group.Visibility == SocialGroupVisibility.Public;
        if (existing is not null)
        {
            existing.RequestAgain(command.RequestedRole, approveImmediately);
            if (approveImmediately)
            {
                group.RecordMembershipActivated();
            }
            else
            {
                group.RecordMembershipRequested();
            }

            await members.UpdateAsync(existing, cancellationToken).ConfigureAwait(false);
            await groups.UpdateAsync(group, cancellationToken).ConfigureAwait(false);
            if (approveImmediately)
            {
                await tokenVersionService.IncrementAsync(command.UserId, cancellationToken).ConfigureAwait(false);
                await InvalidateMembershipCacheAsync(command.UserId, group, cancellationToken).ConfigureAwait(false);
            }

            return ToDto(existing);
        }

        var membership = SocialGroupMember.Request(command.GroupId, command.UserId, command.RequestedRole, approveImmediately);

        if (approveImmediately)
        {
            group.RecordMembershipActivated();
        }
        else
        {
            group.RecordMembershipRequested();
        }

        await members.AddAsync(membership, cancellationToken).ConfigureAwait(false);
        await groups.UpdateAsync(group, cancellationToken).ConfigureAwait(false);
        if (approveImmediately)
        {
            await tokenVersionService.IncrementAsync(command.UserId, cancellationToken).ConfigureAwait(false);
            await InvalidateMembershipCacheAsync(command.UserId, group, cancellationToken).ConfigureAwait(false);
        }

        return ToDto(membership);
    }

    public async Task<bool> ApproveMemberAsync(Guid groupId, Guid userId, Guid approvedByUserId, CancellationToken cancellationToken = default)
    {
        var state = await GetMutableMembershipAsync(groupId, userId, cancellationToken).ConfigureAwait(false);
        if (state is null || state.Value.Member.Status != SocialGroupMembershipStatus.Pending)
        {
            return false;
        }

        state.Value.Member.Approve(approvedByUserId);
        state.Value.Group.RecordMembershipApproved();
        await members.UpdateAsync(state.Value.Member, cancellationToken).ConfigureAwait(false);
        await groups.UpdateAsync(state.Value.Group, cancellationToken).ConfigureAwait(false);
        await tokenVersionService.IncrementAsync(userId, cancellationToken).ConfigureAwait(false);
        await InvalidateMembershipCacheAsync(userId, state.Value.Group, cancellationToken).ConfigureAwait(false);
        return true;
    }

    public async Task<bool> RejectMemberAsync(Guid groupId, Guid userId, CancellationToken cancellationToken = default)
    {
        var state = await GetMutableMembershipAsync(groupId, userId, cancellationToken).ConfigureAwait(false);
        if (state is null || state.Value.Member.Status != SocialGroupMembershipStatus.Pending)
        {
            return false;
        }

        state.Value.Member.Reject();
        state.Value.Group.RecordMembershipRejected();
        await members.UpdateAsync(state.Value.Member, cancellationToken).ConfigureAwait(false);
        await groups.UpdateAsync(state.Value.Group, cancellationToken).ConfigureAwait(false);
        return true;
    }

    public async Task<bool> ChangeRoleAsync(Guid groupId, Guid userId, SocialGroupMemberRole role, CancellationToken cancellationToken = default)
    {
        var state = await GetMutableMembershipAsync(groupId, userId, cancellationToken).ConfigureAwait(false);
        if (state is null || state.Value.Member.Status != SocialGroupMembershipStatus.Active || state.Value.Member.Role == SocialGroupMemberRole.Owner)
        {
            return false;
        }

        state.Value.Member.ChangeRole(role);
        await members.UpdateAsync(state.Value.Member, cancellationToken).ConfigureAwait(false);
        await InvalidateMembershipCacheAsync(userId, state.Value.Group, cancellationToken).ConfigureAwait(false);
        return true;
    }

    public async Task<bool> LeaveAsync(Guid groupId, Guid userId, CancellationToken cancellationToken = default)
    {
        var state = await GetMutableMembershipAsync(groupId, userId, cancellationToken).ConfigureAwait(false);
        if (state is null || state.Value.Member.Role == SocialGroupMemberRole.Owner)
        {
            return false;
        }

        var previousStatus = state.Value.Member.Status;
        state.Value.Member.Remove();
        state.Value.Group.RecordMembershipRemoved(previousStatus);
        await members.UpdateAsync(state.Value.Member, cancellationToken).ConfigureAwait(false);
        await groups.UpdateAsync(state.Value.Group, cancellationToken).ConfigureAwait(false);
        if (previousStatus == SocialGroupMembershipStatus.Active)
        {
            await tokenVersionService.IncrementAsync(userId, cancellationToken).ConfigureAwait(false);
            await InvalidateMembershipCacheAsync(userId, state.Value.Group, cancellationToken).ConfigureAwait(false);
        }

        return true;
    }

    public async Task<IReadOnlyList<SocialGroupMemberDto>> ListMembersAsync(ListSocialGroupMembersQuery query, CancellationToken cancellationToken = default)
        => (await members.ListByGroupAsync(query.GroupId, query.Status, query.Skip, query.Take, cancellationToken)
                .ConfigureAwait(false))
            .Select(ToDto)
            .ToList();

    private async Task<(SocialGroup Group, SocialGroupMember Member)?> GetMutableMembershipAsync(Guid groupId, Guid userId, CancellationToken cancellationToken)
    {
        var group = await groups.GetByIdAsync(groupId, cancellationToken).ConfigureAwait(false);
        if (group is null)
        {
            return null;
        }

        var member = await members.GetByGroupUserAsync(groupId, userId, cancellationToken).ConfigureAwait(false);
        return member is null ? null : (group, member);
    }

    private Task InvalidateMembershipCacheAsync(Guid userId, SocialGroup group, CancellationToken cancellationToken)
    {
        if (group.TenantId is { } tenantId && tenantId != Guid.Empty)
        {
            return cacheInvalidationService.InvalidateUserAsync(userId, tenantId, cancellationToken);
        }

        return cacheInvalidationService.InvalidateGlobalAsync(cancellationToken);
    }

    private static SocialGroupDto ToDto(SocialGroup group)
        => new(
            group.Id,
            group.TenantId,
            group.OwnerId,
            group.Name,
            group.Slug,
            group.Description,
            group.Type,
            group.Visibility,
            group.Status,
            group.MemberCount,
            group.PendingMemberCount,
            group.CreatedAt,
            group.UpdatedAt);

    private static SocialGroupMemberDto ToDto(SocialGroupMember member)
        => new(
            member.Id,
            member.GroupId,
            member.UserId,
            member.Role,
            member.Status,
            member.RequestedAt,
            member.JoinedAt,
            member.ApprovedByUserId,
            member.RemovedAt);
}

public sealed class CreateSocialGroupCommandHandler(ISocialGroupService service) : ICommandHandler<CreateSocialGroupCommand, SocialGroupDto>
{
    public Task<SocialGroupDto> Handle(CreateSocialGroupCommand request, CancellationToken cancellationToken)
        => service.CreateAsync(request, cancellationToken);
}

public sealed class UpdateSocialGroupCommandHandler(ISocialGroupService service) : ICommandHandler<UpdateSocialGroupCommand, SocialGroupDto?>
{
    public Task<SocialGroupDto?> Handle(UpdateSocialGroupCommand request, CancellationToken cancellationToken)
        => service.UpdateAsync(request, cancellationToken);
}

public sealed class SetSocialGroupStatusCommandHandler(ISocialGroupService service) : ICommandHandler<SetSocialGroupStatusCommand, bool>
{
    public Task<bool> Handle(SetSocialGroupStatusCommand request, CancellationToken cancellationToken)
        => service.SetStatusAsync(request.GroupId, request.Status, cancellationToken);
}

public sealed class JoinSocialGroupCommandHandler(ISocialGroupService service) : ICommandHandler<JoinSocialGroupCommand, SocialGroupMemberDto?>
{
    public Task<SocialGroupMemberDto?> Handle(JoinSocialGroupCommand request, CancellationToken cancellationToken)
        => service.JoinAsync(request, cancellationToken);
}

public sealed class ApproveSocialGroupMemberCommandHandler(ISocialGroupService service) : ICommandHandler<ApproveSocialGroupMemberCommand, bool>
{
    public Task<bool> Handle(ApproveSocialGroupMemberCommand request, CancellationToken cancellationToken)
        => service.ApproveMemberAsync(request.GroupId, request.UserId, request.ApprovedByUserId, cancellationToken);
}

public sealed class RejectSocialGroupMemberCommandHandler(ISocialGroupService service) : ICommandHandler<RejectSocialGroupMemberCommand, bool>
{
    public Task<bool> Handle(RejectSocialGroupMemberCommand request, CancellationToken cancellationToken)
        => service.RejectMemberAsync(request.GroupId, request.UserId, cancellationToken);
}

public sealed class ChangeSocialGroupMemberRoleCommandHandler(ISocialGroupService service) : ICommandHandler<ChangeSocialGroupMemberRoleCommand, bool>
{
    public Task<bool> Handle(ChangeSocialGroupMemberRoleCommand request, CancellationToken cancellationToken)
        => service.ChangeRoleAsync(request.GroupId, request.UserId, request.Role, cancellationToken);
}

public sealed class LeaveSocialGroupCommandHandler(ISocialGroupService service) : ICommandHandler<LeaveSocialGroupCommand, bool>
{
    public Task<bool> Handle(LeaveSocialGroupCommand request, CancellationToken cancellationToken)
        => service.LeaveAsync(request.GroupId, request.UserId, cancellationToken);
}

public sealed class GetSocialGroupQueryHandler(ISocialGroupService service) : IQueryHandler<GetSocialGroupQuery, SocialGroupDto?>
{
    public Task<SocialGroupDto?> Handle(GetSocialGroupQuery request, CancellationToken cancellationToken)
        => service.GetAsync(request.GroupId, cancellationToken);
}

public sealed class ListSocialGroupsQueryHandler(ISocialGroupService service) : IQueryHandler<ListSocialGroupsQuery, IReadOnlyList<SocialGroupDto>>
{
    public Task<IReadOnlyList<SocialGroupDto>> Handle(ListSocialGroupsQuery request, CancellationToken cancellationToken)
        => service.ListAsync(request, cancellationToken);
}

public sealed class ListSocialGroupMembersQueryHandler(ISocialGroupService service) : IQueryHandler<ListSocialGroupMembersQuery, IReadOnlyList<SocialGroupMemberDto>>
{
    public Task<IReadOnlyList<SocialGroupMemberDto>> Handle(ListSocialGroupMembersQuery request, CancellationToken cancellationToken)
        => service.ListMembersAsync(request, cancellationToken);
}

[ApiController]
[Route("api/social/groups")]
[Authorize]
public sealed class SocialGroupsController(
    ISender sender,
    IActorContextAccessor actorContextAccessor) : ControllerBase
{
    [HttpGet]
    public Task<IReadOnlyList<SocialGroupDto>> List(
        [FromQuery] Guid? tenantId,
        [FromQuery] Guid? ownerId,
        [FromQuery] SocialGroupType? type,
        [FromQuery] SocialGroupVisibility? visibility,
        [FromQuery] SocialGroupStatus? status,
        [FromQuery] string? search,
        [FromQuery] int skip,
        [FromQuery] int take,
        CancellationToken cancellationToken)
    {
        var actor = actorContextAccessor.ActorContext;
        if (actor.IsSystemAdmin)
        {
            return sender.Send(
                new ListSocialGroupsQuery(tenantId, ownerId, type, visibility, status, search, skip, take <= 0 ? 50 : take),
                cancellationToken);
        }

        if (actor.IsTenantAdmin && actor.TenantId.HasValue)
        {
            return sender.Send(
                new ListSocialGroupsQuery(actor.TenantId.Value, ownerId, type, visibility, status, search, skip, take <= 0 ? 50 : take),
                cancellationToken);
        }

        return sender.Send(
            new ListSocialGroupsQuery(null, ownerId, type, SocialGroupVisibility.Public, SocialGroupStatus.Active, search, skip, take <= 0 ? 50 : take),
            cancellationToken);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<SocialGroupDto>> Get(Guid id, CancellationToken cancellationToken)
    {
        var group = await sender.Send(new GetSocialGroupQuery(id), cancellationToken).ConfigureAwait(false);
        if (group is null)
        {
            return NotFound();
        }

        return IsPubliclyVisible(group) || CanManageGroup(group)
            ? Ok(group)
            : NotFound();
    }

    [HttpPost]
    [Authorize(Policy = Policies.TenantAdmin)]
    public async Task<ActionResult<SocialGroupDto>> Create(CreateSocialGroupRequest request, CancellationToken cancellationToken)
    {
        var actor = actorContextAccessor.ActorContext;
        if (!actor.SubjectIdAsGuid.HasValue || (!actor.IsSystemAdmin && !actor.TenantId.HasValue))
        {
            return Forbid();
        }

        var tenantId = actor.IsSystemAdmin
            ? request.TenantId ?? actor.TenantId
            : actor.TenantId;
        var group = await sender.Send(
            new CreateSocialGroupCommand(
                actor.SubjectIdAsGuid.Value,
                request.Name,
                request.Slug,
                request.Type,
                request.Visibility,
                request.Description,
                tenantId),
            cancellationToken).ConfigureAwait(false);

        return CreatedAtAction(nameof(Get), new { id = group.Id }, group);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = Policies.TenantAdmin)]
    public async Task<ActionResult<SocialGroupDto>> Update(Guid id, UpdateSocialGroupRequest request, CancellationToken cancellationToken)
    {
        var access = await EnsureCanManageGroupAsync(id, cancellationToken).ConfigureAwait(false);
        if (access.Failure is not null)
        {
            return access.Failure;
        }

        var group = await sender.Send(
            new UpdateSocialGroupCommand(id, request.Name, request.Slug, request.Type, request.Visibility, request.Description),
            cancellationToken).ConfigureAwait(false);
        return group is null ? NotFound() : Ok(group);
    }

    [HttpPost("{id:guid}/activate")]
    [Authorize(Policy = Policies.TenantAdmin)]
    public Task<IActionResult> Activate(Guid id, CancellationToken cancellationToken)
        => SetStatusAsync(id, SocialGroupStatus.Active, cancellationToken);

    [HttpPost("{id:guid}/archive")]
    [Authorize(Policy = Policies.TenantAdmin)]
    public Task<IActionResult> Archive(Guid id, CancellationToken cancellationToken)
        => SetStatusAsync(id, SocialGroupStatus.Archived, cancellationToken);

    [HttpPost("{id:guid}/suspend")]
    [Authorize(Policy = Policies.TenantAdmin)]
    public Task<IActionResult> Suspend(Guid id, CancellationToken cancellationToken)
        => SetStatusAsync(id, SocialGroupStatus.Suspended, cancellationToken);

    [HttpGet("{id:guid}/members")]
    [Authorize(Policy = Policies.TenantAdmin)]
    public async Task<ActionResult<IReadOnlyList<SocialGroupMemberDto>>> ListMembers(
        Guid id,
        [FromQuery] SocialGroupMembershipStatus? status,
        [FromQuery] int skip,
        [FromQuery] int take,
        CancellationToken cancellationToken)
    {
        var access = await EnsureCanManageGroupAsync(id, cancellationToken).ConfigureAwait(false);
        if (access.Failure is not null)
        {
            return access.Failure;
        }

        var members = await sender.Send(
            new ListSocialGroupMembersQuery(id, status, skip, take <= 0 ? 50 : take),
            cancellationToken).ConfigureAwait(false);
        return Ok(members);
    }

    [HttpPost("{id:guid}/members")]
    [Authorize(Policy = Policies.TenantAdmin)]
    public async Task<ActionResult<SocialGroupMemberDto>> Join(Guid id, JoinSocialGroupRequest request, CancellationToken cancellationToken)
    {
        var access = await EnsureCanManageGroupAsync(id, cancellationToken).ConfigureAwait(false);
        if (access.Failure is not null)
        {
            return access.Failure;
        }

        var membership = await sender.Send(new JoinSocialGroupCommand(id, request.UserId, request.RequestedRole), cancellationToken)
            .ConfigureAwait(false);
        return membership is null ? NotFound() : Ok(membership);
    }

    [HttpPost("{id:guid}/members/{userId:guid}/approve")]
    [Authorize(Policy = Policies.TenantAdmin)]
    public async Task<IActionResult> Approve(Guid id, Guid userId, ApproveSocialGroupMemberRequest request, CancellationToken cancellationToken)
    {
        var access = await EnsureCanManageGroupAsync(id, cancellationToken).ConfigureAwait(false);
        if (access.Failure is not null)
        {
            return access.Failure;
        }

        var approverId = actorContextAccessor.ActorContext.SubjectIdAsGuid;
        if (!approverId.HasValue)
        {
            return Forbid();
        }

        return await sender.Send(new ApproveSocialGroupMemberCommand(id, userId, approverId.Value), cancellationToken).ConfigureAwait(false)
            ? NoContent()
            : NotFound();
    }

    [HttpPost("{id:guid}/members/{userId:guid}/reject")]
    [Authorize(Policy = Policies.TenantAdmin)]
    public async Task<IActionResult> Reject(Guid id, Guid userId, CancellationToken cancellationToken)
    {
        var access = await EnsureCanManageGroupAsync(id, cancellationToken).ConfigureAwait(false);
        if (access.Failure is not null)
        {
            return access.Failure;
        }

        return await sender.Send(new RejectSocialGroupMemberCommand(id, userId), cancellationToken).ConfigureAwait(false)
            ? NoContent()
            : NotFound();
    }

    [HttpPut("{id:guid}/members/{userId:guid}/role")]
    [Authorize(Policy = Policies.TenantAdmin)]
    public async Task<IActionResult> ChangeRole(Guid id, Guid userId, ChangeSocialGroupMemberRoleRequest request, CancellationToken cancellationToken)
    {
        var access = await EnsureCanManageGroupAsync(id, cancellationToken).ConfigureAwait(false);
        if (access.Failure is not null)
        {
            return access.Failure;
        }

        return await sender.Send(new ChangeSocialGroupMemberRoleCommand(id, userId, request.Role), cancellationToken).ConfigureAwait(false)
            ? NoContent()
            : NotFound();
    }

    [HttpDelete("{id:guid}/members/{userId:guid}")]
    [Authorize(Policy = Policies.TenantAdmin)]
    public async Task<IActionResult> Leave(Guid id, Guid userId, CancellationToken cancellationToken)
    {
        var access = await EnsureCanManageGroupAsync(id, cancellationToken).ConfigureAwait(false);
        if (access.Failure is not null)
        {
            return access.Failure;
        }

        return await sender.Send(new LeaveSocialGroupCommand(id, userId), cancellationToken).ConfigureAwait(false)
            ? NoContent()
            : NotFound();
    }

    private async Task<IActionResult> SetStatusAsync(Guid id, SocialGroupStatus status, CancellationToken cancellationToken)
    {
        var access = await EnsureCanManageGroupAsync(id, cancellationToken).ConfigureAwait(false);
        if (access.Failure is not null)
        {
            return access.Failure;
        }

        return await sender.Send(new SetSocialGroupStatusCommand(id, status), cancellationToken).ConfigureAwait(false)
            ? NoContent()
            : NotFound();
    }

    private async Task<(SocialGroupDto? Group, ActionResult? Failure)> EnsureCanManageGroupAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        var group = await sender.Send(new GetSocialGroupQuery(id), cancellationToken).ConfigureAwait(false);
        if (group is null)
        {
            return (null, NotFound());
        }

        return CanManageGroup(group)
            ? (group, null)
            : (null, Forbid());
    }

    private bool CanManageGroup(SocialGroupDto group)
    {
        var actor = actorContextAccessor.ActorContext;
        return actor.IsSystemAdmin ||
               actor.IsTenantAdmin && actor.TenantId.HasValue && group.TenantId == actor.TenantId;
    }

    private static bool IsPubliclyVisible(SocialGroupDto group) =>
        group.Visibility == SocialGroupVisibility.Public && group.Status == SocialGroupStatus.Active;
}

public sealed class SocialGroupsModelConfiguration : IModelConfiguration
{
    public void Configure(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new SocialGroupConfiguration());
        modelBuilder.ApplyConfiguration(new SocialGroupMemberConfiguration());
    }
}

public sealed class SocialGroupConfiguration : IEntityTypeConfiguration<SocialGroup>
{
    public void Configure(EntityTypeBuilder<SocialGroup> builder)
    {
        builder.ToTable("social_groups");
        builder.HasKey(group => group.Id);
        builder.Property(group => group.Name).HasMaxLength(120).IsRequired();
        builder.Property(group => group.Slug).HasMaxLength(160).IsRequired();
        builder.Property(group => group.Description).HasMaxLength(1000);
        builder.Property(group => group.Type).HasConversion<string>().HasMaxLength(40);
        builder.Property(group => group.Visibility).HasConversion<string>().HasMaxLength(40);
        builder.Property(group => group.Status).HasConversion<string>().HasMaxLength(40);
        builder.HasIndex(group => group.Slug).IsUnique();
        builder.HasIndex(group => group.OwnerId);
        builder.HasIndex(group => group.TenantId);
        builder.HasIndex(group => new { group.Status, group.Visibility, group.Type });
    }
}

public sealed class SocialGroupMemberConfiguration : IEntityTypeConfiguration<SocialGroupMember>
{
    public void Configure(EntityTypeBuilder<SocialGroupMember> builder)
    {
        builder.ToTable("social_group_members");
        builder.HasKey(member => member.Id);
        builder.Property(member => member.Role).HasConversion<string>().HasMaxLength(40);
        builder.Property(member => member.Status).HasConversion<string>().HasMaxLength(40);
        builder.HasIndex(member => new { member.GroupId, member.UserId }).IsUnique();
        builder.HasIndex(member => new { member.GroupId, member.Status });
        builder.HasOne<SocialGroup>()
            .WithMany()
            .HasForeignKey(member => member.GroupId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public static class SocialGroupsDependencyInjection
{
    public static IServiceCollection AddSocialGroupsModule(this IServiceCollection services)
    {
        services.AddScoped<ISocialGroupRepository, SocialGroupRepository>();
        services.AddScoped<ISocialGroupMemberRepository, SocialGroupMemberRepository>();
        services.AddScoped<IAuthorizationGroupMembershipProvider, SocialGroupAuthorizationMembershipProvider>();
        services.AddScoped<ISocialGroupService, SocialGroupService>();

        services.AddScoped<ICommandHandler<CreateSocialGroupCommand, SocialGroupDto>, CreateSocialGroupCommandHandler>();
        services.AddScoped<IRequestHandler<CreateSocialGroupCommand, SocialGroupDto>>(sp => sp.GetRequiredService<ICommandHandler<CreateSocialGroupCommand, SocialGroupDto>>());
        services.AddScoped<ICommandHandler<UpdateSocialGroupCommand, SocialGroupDto?>, UpdateSocialGroupCommandHandler>();
        services.AddScoped<IRequestHandler<UpdateSocialGroupCommand, SocialGroupDto?>>(sp => sp.GetRequiredService<ICommandHandler<UpdateSocialGroupCommand, SocialGroupDto?>>());
        services.AddScoped<ICommandHandler<SetSocialGroupStatusCommand, bool>, SetSocialGroupStatusCommandHandler>();
        services.AddScoped<IRequestHandler<SetSocialGroupStatusCommand, bool>>(sp => sp.GetRequiredService<ICommandHandler<SetSocialGroupStatusCommand, bool>>());
        services.AddScoped<ICommandHandler<JoinSocialGroupCommand, SocialGroupMemberDto?>, JoinSocialGroupCommandHandler>();
        services.AddScoped<IRequestHandler<JoinSocialGroupCommand, SocialGroupMemberDto?>>(sp => sp.GetRequiredService<ICommandHandler<JoinSocialGroupCommand, SocialGroupMemberDto?>>());
        services.AddScoped<ICommandHandler<ApproveSocialGroupMemberCommand, bool>, ApproveSocialGroupMemberCommandHandler>();
        services.AddScoped<IRequestHandler<ApproveSocialGroupMemberCommand, bool>>(sp => sp.GetRequiredService<ICommandHandler<ApproveSocialGroupMemberCommand, bool>>());
        services.AddScoped<ICommandHandler<RejectSocialGroupMemberCommand, bool>, RejectSocialGroupMemberCommandHandler>();
        services.AddScoped<IRequestHandler<RejectSocialGroupMemberCommand, bool>>(sp => sp.GetRequiredService<ICommandHandler<RejectSocialGroupMemberCommand, bool>>());
        services.AddScoped<ICommandHandler<ChangeSocialGroupMemberRoleCommand, bool>, ChangeSocialGroupMemberRoleCommandHandler>();
        services.AddScoped<IRequestHandler<ChangeSocialGroupMemberRoleCommand, bool>>(sp => sp.GetRequiredService<ICommandHandler<ChangeSocialGroupMemberRoleCommand, bool>>());
        services.AddScoped<ICommandHandler<LeaveSocialGroupCommand, bool>, LeaveSocialGroupCommandHandler>();
        services.AddScoped<IRequestHandler<LeaveSocialGroupCommand, bool>>(sp => sp.GetRequiredService<ICommandHandler<LeaveSocialGroupCommand, bool>>());

        services.AddScoped<IQueryHandler<GetSocialGroupQuery, SocialGroupDto?>, GetSocialGroupQueryHandler>();
        services.AddScoped<IRequestHandler<GetSocialGroupQuery, SocialGroupDto?>>(sp => sp.GetRequiredService<IQueryHandler<GetSocialGroupQuery, SocialGroupDto?>>());
        services.AddScoped<IQueryHandler<ListSocialGroupsQuery, IReadOnlyList<SocialGroupDto>>, ListSocialGroupsQueryHandler>();
        services.AddScoped<IRequestHandler<ListSocialGroupsQuery, IReadOnlyList<SocialGroupDto>>>(sp => sp.GetRequiredService<IQueryHandler<ListSocialGroupsQuery, IReadOnlyList<SocialGroupDto>>>());
        services.AddScoped<IQueryHandler<ListSocialGroupMembersQuery, IReadOnlyList<SocialGroupMemberDto>>, ListSocialGroupMembersQueryHandler>();
        services.AddScoped<IRequestHandler<ListSocialGroupMembersQuery, IReadOnlyList<SocialGroupMemberDto>>>(sp => sp.GetRequiredService<IQueryHandler<ListSocialGroupMembersQuery, IReadOnlyList<SocialGroupMemberDto>>>());

        return services;
    }
}

public sealed class SocialGroupsModule : ModuleBase
{
    public override string Name => "Social.Groups";
    public override int Order => 164;

    public override IServiceCollection ConfigureServices(IServiceCollection services, IConfiguration configuration)
        => services.AddSocialGroupsModule();

    public override IEndpointRouteBuilder MapEndpoints(IEndpointRouteBuilder endpoints) => endpoints;
}
