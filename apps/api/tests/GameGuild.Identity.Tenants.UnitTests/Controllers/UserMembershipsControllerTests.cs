using FluentAssertions;
using GameGuild.CQRS;
using GameGuild.Identity.Authorization;
using GameGuild.Identity.Context.Actors;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Reflection;
using Xunit;

namespace GameGuild.Identity.Tenants.UnitTests.Controllers;

public class UserMembershipsControllerTests
{
    [Fact]
    public void AddUserMembership_Should_Require_Members_Manage()
    {
        var method = typeof(UserMembershipsController).GetMethod(nameof(UserMembershipsController.AddUserMembership));

        method.Should().NotBeNull();
        // Issue #294: member management is governed by the granular Members.Manage tier
        // (tenant match + administrative role or a delegated members:manage grant).
        method!.GetCustomAttributes<AuthorizeAttribute>()
            .Should()
            .ContainSingle(attribute => attribute.Policy == Policies.MembersManage);
    }

    [Fact]
    public async Task DelegatedMemberManager_WithMembersManagePermission_ShouldManageOwnTenant()
    {
        var tenantId = Guid.NewGuid();
        var sender = new StubSender();
        sender.Setup<AddTenantMemberCommand, AddTenantMemberResponse>(_ => new AddTenantMemberResponse
        {
            Success = true,
            MemberId = Guid.NewGuid(),
            Message = "Member added successfully"
        });
        var actor = new ActorContext
        {
            IsAuthenticated = true,
            ActorKind = ActorKind.User,
            SubjectId = Guid.NewGuid().ToString(),
            TenantId = tenantId,
            Permissions = new HashSet<string>([ManageMembersPermission.Keys.Manage], StringComparer.OrdinalIgnoreCase),
            Roles = new HashSet<string>()
        };

        var controller = CreateController(sender, actor);

        // The in-code guard honors the delegated members:manage permission within the
        // actor's own tenant (issue #294).
        var result = await controller.AddUserMembership(
            Guid.NewGuid(),
            new AddUserMembershipRequest { TenantId = tenantId, Role = "Member" },
            CancellationToken.None);

        result.Should().BeOfType<ObjectResult>()
            .Which.StatusCode.Should().Be(StatusCodes.Status201Created);
    }

    [Fact]
    public async Task DelegatedMemberManager_ForForeignTenant_ShouldBeForbidden()
    {
        var tenantId = Guid.NewGuid();
        var foreignTenant = Guid.NewGuid();
        var actor = new ActorContext
        {
            IsAuthenticated = true,
            ActorKind = ActorKind.User,
            SubjectId = Guid.NewGuid().ToString(),
            TenantId = tenantId,
            Permissions = new HashSet<string>([ManageMembersPermission.Keys.Manage], StringComparer.OrdinalIgnoreCase),
            Roles = new HashSet<string>()
        };

        var controller = CreateController(new StubSender(), actor);

        var result = await controller.AddUserMembership(
            Guid.NewGuid(),
            new AddUserMembershipRequest { TenantId = foreignTenant, Role = "Member" },
            CancellationToken.None);

        result.Should().BeOfType<ForbidResult>();
    }

    [Fact]
    public async Task GetUserMemberships_Should_Return_Ok()
    {
        var sender = new StubSender();
        sender.Setup<GetUserMembershipsQuery, GetUserMembershipsResponse>(_ => new GetUserMembershipsResponse { TotalCount = 1, Memberships = new List<UserMembershipDto>() });

        var controller = CreateController(sender, CreateActorContext("SystemAdmin"));
        var result = await controller.GetUserMemberships(Guid.NewGuid(), false, CancellationToken.None);

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task CheckUserHasMemberships_Should_Return_NotFound_When_Empty()
    {
        var sender = new StubSender();
        sender.Setup<GetUserMembershipsQuery, GetUserMembershipsResponse>(_ => new GetUserMembershipsResponse { TotalCount = 0, Memberships = new List<UserMembershipDto>() });

        var controller = CreateController(sender, CreateActorContext("SystemAdmin"));
        var result = await controller.CheckUserHasMemberships(Guid.NewGuid(), CancellationToken.None);

        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task GetMembershipCount_Should_Return_Count()
    {
        var sender = new StubSender();
        sender.Setup<GetUserMembershipsQuery, GetUserMembershipsResponse>(_ => new GetUserMembershipsResponse { TotalCount = 3, Memberships = new List<UserMembershipDto>() });

        var controller = CreateController(sender, CreateActorContext("SystemAdmin"));
        var result = await controller.GetMembershipCount(Guid.NewGuid(), CancellationToken.None);

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task AddUserMembership_Should_Return_Created_When_Command_Succeeds()
    {
        var sender = new StubSender();
        sender.Setup<AddTenantMemberCommand, AddTenantMemberResponse>(_ => new AddTenantMemberResponse
        {
            Success = true,
            MemberId = Guid.NewGuid(),
            Message = "Member added successfully"
        });

        var controller = CreateController(sender, CreateActorContext("SystemAdmin"));
        var result = await controller.AddUserMembership(
            Guid.NewGuid(),
            new AddUserMembershipRequest
            {
                TenantId = Guid.NewGuid(),
                Role = "Renter",
                InvitedByEmail = "admin@game-guild.com"
            },
            CancellationToken.None);

        result.Should().BeOfType<ObjectResult>()
            .Which.StatusCode.Should().Be(StatusCodes.Status201Created);
    }

    [Fact]
    public async Task UpdateUserMembershipRole_ShouldForbidTenantAdminFromGrantingSystemAdmin()
    {
        var sender = new StubSender();
        sender.Setup<UpdateTenantMemberRoleCommand, UpdateTenantMemberRoleResponse>(_ => new UpdateTenantMemberRoleResponse
        {
            Success = true,
            NewRole = "SystemAdmin"
        });
        var tenantId = Guid.NewGuid();
        var controller = CreateController(sender, CreateActorContext("TenantAdmin", tenantId));

        var result = await controller.UpdateUserMembershipRole(
            tenantId,
            Guid.NewGuid(),
            new UpdateUserMembershipRoleRequest { Role = "SystemAdmin" },
            CancellationToken.None);

        result.Should().BeOfType<ForbidResult>();
    }

    [Fact]
    public async Task UpdateUserMembershipRole_ShouldForbidTenantAdminFromManagingAnotherTenant()
    {
        var sender = new StubSender();
        var actorTenantId = Guid.NewGuid();
        var controller = CreateController(sender, CreateActorContext("TenantAdmin", actorTenantId));

        var result = await controller.UpdateUserMembershipRole(
            Guid.NewGuid(),
            Guid.NewGuid(),
            new UpdateUserMembershipRoleRequest { Role = "Member" },
            CancellationToken.None);

        result.Should().BeOfType<ForbidResult>();
    }

    [Fact]
    public async Task UpdateUserMembershipRole_ShouldAllowTenantAdminWithinCurrentTenant()
    {
        var sender = new StubSender();
        var tenantId = Guid.NewGuid();
        sender.Setup<UpdateTenantMemberRoleCommand, UpdateTenantMemberRoleResponse>(_ => new UpdateTenantMemberRoleResponse
        {
            Success = true,
            NewRole = "Moderator"
        });
        var controller = CreateController(sender, CreateActorContext("TenantAdmin", tenantId));

        var result = await controller.UpdateUserMembershipRole(
            Guid.NewGuid(),
            tenantId,
            new UpdateUserMembershipRoleRequest { Role = "Moderator" },
            CancellationToken.None);

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task GetUserMemberships_ShouldAllowSelfAndForbidAnotherRegularUser()
    {
        var sender = new StubSender();
        var actorId = Guid.NewGuid();
        sender.Setup<GetUserMembershipsQuery, GetUserMembershipsResponse>(_ => new GetUserMembershipsResponse
        {
            TotalCount = 0,
            Memberships = []
        });
        var controller = CreateController(sender, CreateActorContext("Member", Guid.NewGuid(), actorId));

        (await controller.GetUserMemberships(actorId, false, CancellationToken.None)).Should().BeOfType<OkObjectResult>();
        (await controller.GetUserMemberships(Guid.NewGuid(), false, CancellationToken.None)).Should().BeOfType<ForbidResult>();
    }

    [Fact]
    public async Task GetUserMemberships_ShouldOnlyReturnCurrentTenantMembership_ForTenantAdmin()
    {
        var sender = new StubSender();
        var tenantId = Guid.NewGuid();
        sender.Setup<GetUserMembershipsQuery, GetUserMembershipsResponse>(_ => new GetUserMembershipsResponse
        {
            TotalCount = 2,
            Memberships =
            [
                new UserMembershipDto { TenantId = tenantId, TenantName = "Current" },
                new UserMembershipDto { TenantId = Guid.NewGuid(), TenantName = "Other" }
            ]
        });
        var controller = CreateController(sender, CreateActorContext("TenantAdmin", tenantId));

        var result = await controller.GetUserMemberships(Guid.NewGuid(), false, CancellationToken.None);

        var response = result.Should().BeOfType<OkObjectResult>().Which.Value.Should().BeOfType<GetUserMembershipsResponse>().Subject;
        response.TotalCount.Should().Be(1);
        response.Memberships.Should().ContainSingle(membership => membership.TenantId == tenantId);
    }

    [Fact]
    public async Task AcceptUserMembershipInvite_ShouldAllowInvitedUserToAcceptOwnInvite()
    {
        var sender = new StubSender();
        var invitedUserId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        sender.Setup<UpdateTenantMemberInviteCommand, UpdateTenantMemberInviteResponse>(_ => new UpdateTenantMemberInviteResponse
        {
            Success = true,
            InviteStatus = TenantMemberInviteStatuses.Accepted
        });
        var controller = CreateController(sender, CreateActorContext("Member", userId: invitedUserId));

        var result = await controller.AcceptUserMembershipInvite(invitedUserId, tenantId, null, CancellationToken.None);

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task AcceptUserMembershipInvite_ShouldForbidAnotherRegularUser()
    {
        var controller = CreateController(new StubSender(), CreateActorContext("Member", userId: Guid.NewGuid()));

        var result = await controller.AcceptUserMembershipInvite(Guid.NewGuid(), Guid.NewGuid(), null, CancellationToken.None);

        result.Should().BeOfType<ForbidResult>();
    }

    [Fact]
    public void AcceptUserMembershipInvite_ShouldRequireAuthenticationWithoutTenantAdminPolicy()
    {
        var method = typeof(UserMembershipsController).GetMethod(nameof(UserMembershipsController.AcceptUserMembershipInvite));

        method.Should().NotBeNull();
        method!.GetCustomAttributes<AuthorizeAttribute>()
            .Should()
            .ContainSingle(attribute => string.IsNullOrWhiteSpace(attribute.Policy));
    }

    private static UserMembershipsController CreateController(StubSender sender, ActorContext actor)
    {
        var accessor = new ActorContextAccessor();
        accessor.SetActorContext(actor);
        return new UserMembershipsController(sender, accessor);
    }

    private static ActorContext CreateActorContext(string role, Guid? tenantId = null, Guid? userId = null)
        => new()
        {
            ActorKind = ActorKind.User,
            SubjectId = (userId ?? Guid.NewGuid()).ToString(),
            TenantId = tenantId,
            Roles = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { role },
            Permissions = new HashSet<string>(StringComparer.OrdinalIgnoreCase),
            TypedAttributes = ActorAttributes.Empty,
            IsAuthenticated = true
        };

    private sealed class StubSender : ISender
    {
        private readonly Dictionary<Type, Func<object, object?>> _handlers = new();

        public void Setup<TRequest, TResponse>(Func<TRequest, TResponse> handler)
        {
            _handlers[typeof(TRequest)] = request => handler((TRequest)request);
        }

        public Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
        {
            if (_handlers.TryGetValue(request.GetType(), out var handler))
            {
                return Task.FromResult((TResponse)handler(request)!);
            }

            return Task.FromResult(default(TResponse)!);
        }

        public Task<object?> Send(object request, CancellationToken cancellationToken = default)
        {
            if (_handlers.TryGetValue(request.GetType(), out var handler))
            {
                return Task.FromResult(handler(request));
            }

            return Task.FromResult<object?>(null);
        }

        public Task Send<TRequest>(TRequest request, CancellationToken cancellationToken = default) where TRequest : IRequest
        {
            return Task.CompletedTask;
        }

        public Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default) where TNotification : INotification
        {
            return Task.CompletedTask;
        }

        public Task Publish(object notification, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }
}
