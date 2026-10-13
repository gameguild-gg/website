using FluentAssertions;
using GameGuild.Identity.Authorization;
using GameGuild.Identity.Context.Actors;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using GameGuild.CQRS;
using Xunit;

namespace GameGuild.Identity.Authentication.UnitTests.Controllers;

public sealed class ApiKeyControllerTests
{
    [Fact]
    public async Task CreateApiKey_WhenMediatorSucceeds_ReturnsOk()
    {
        var mediator = new Mock<IMediator>();
        var command = new CreateApiKeyCommand { Name = "integration-key", Scopes = ["read"] };
        var response = new CreateApiKeyResponse
        {
            Id = Guid.NewGuid(),
            Name = "integration-key",
            ApiKey = "plain-text-key",
            KeyPrefix = "itg_",
            Scopes = ["read"],
            CreatedAt = DateTime.UtcNow
        };

        mediator
            .Setup(x => x.Send(It.Is<CreateApiKeyCommand>(c => c.Name == command.Name && c.Scopes.SequenceEqual(command.Scopes)), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(response));

        var controller = new ApiKeyController(mediator.Object);

        var result = await controller.CreateApiKey(command, CancellationToken.None);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().BeSameAs(response);
    }

    [Fact]
    public async Task CreateApiKey_WhenMediatorFails_ReturnsBadRequest()
    {
        var mediator = new Mock<IMediator>();
        var error = Error.Validation("ApiKey.Invalid", "Name is required");

        mediator
            .Setup(x => x.Send(It.IsAny<CreateApiKeyCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure<CreateApiKeyResponse>(error));

        var controller = new ApiKeyController(mediator.Object);

        var result = await controller.CreateApiKey(new CreateApiKeyCommand { Name = string.Empty, Scopes = ["read"] }, CancellationToken.None);

        var badRequest = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        GetAnonymousProperty<Error>(badRequest.Value!, "error").Should().Be(error);
    }

    [Fact]
    public async Task ListApiKeys_WhenMediatorSucceeds_ReturnsOk()
    {
        var mediator = new Mock<IMediator>();
        var apiKeys = new List<ApiKeyDto>
        {
            new() { Id = Guid.NewGuid(), Name = "Primary", KeyPrefix = "pk_", Scopes = ["read"], IsActive = true, CreatedAt = DateTime.UtcNow }
        };

        mediator
            .Setup(x => x.Send(It.IsAny<ListApiKeysQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(apiKeys));

        var controller = new ApiKeyController(mediator.Object);

        var result = await controller.ListApiKeys(CancellationToken.None);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().BeSameAs(apiKeys);
    }

    [Fact]
    public async Task ListApiKeys_WhenMediatorFails_ReturnsBadRequest()
    {
        var mediator = new Mock<IMediator>();
        var error = Error.Failure("ApiKey.ListFailed", "Unable to list API keys");

        mediator
            .Setup(x => x.Send(It.IsAny<ListApiKeysQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure<List<ApiKeyDto>>(error));

        var controller = new ApiKeyController(mediator.Object);

        var result = await controller.ListApiKeys(CancellationToken.None);

        var badRequest = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        GetAnonymousProperty<Error>(badRequest.Value!, "error").Should().Be(error);
    }

    [Fact]
    public async Task RevokeApiKey_WhenMediatorSucceeds_ReturnsOkAndUsesRequestReason()
    {
        var mediator = new Mock<IMediator>();
        RevokeApiKeyCommand? captured = null;

        mediator
            .Setup(x => x.Send(It.IsAny<RevokeApiKeyCommand>(), It.IsAny<CancellationToken>()))
            .Callback<object, CancellationToken>((request, _) => captured = (RevokeApiKeyCommand)request)
            .ReturnsAsync(Result.Success(true));

        var controller = new ApiKeyController(mediator.Object);
        var keyId = Guid.NewGuid();

        var result = await controller.RevokeApiKey(keyId, new RevokeApiKeyRequest { Reason = "rotated" }, CancellationToken.None);

        captured.Should().NotBeNull();
        captured!.KeyId.Should().Be(keyId);
        captured.Reason.Should().Be("rotated");

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        GetAnonymousProperty<string>(ok.Value!, "message").Should().Be("API key revoked successfully");
    }

    [Fact]
    public async Task RevokeApiKey_WhenMediatorFails_ReturnsBadRequestAndAllowsNullReason()
    {
        var mediator = new Mock<IMediator>();
        var error = Error.NotFound("ApiKey.NotFound", "API key not found");
        RevokeApiKeyCommand? captured = null;

        mediator
            .Setup(x => x.Send(It.IsAny<RevokeApiKeyCommand>(), It.IsAny<CancellationToken>()))
            .Callback<object, CancellationToken>((request, _) => captured = (RevokeApiKeyCommand)request)
            .ReturnsAsync(Result.Failure<bool>(error));

        var controller = new ApiKeyController(mediator.Object);

        var result = await controller.RevokeApiKey(Guid.NewGuid(), null, CancellationToken.None);

        captured.Should().NotBeNull();
        captured!.Reason.Should().BeNull();

        var badRequest = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        GetAnonymousProperty<Error>(badRequest.Value!, "error").Should().Be(error);
    }

    private static T GetAnonymousProperty<T>(object target, string propertyName)
    {
        return (T)target.GetType().GetProperty(propertyName)!.GetValue(target)!;
    }
}

public sealed class RolesControllerTests
{
    [Fact]
    public void Controller_ShouldExposeRoleApiAndProtectAdministrativeMutations()
    {
        typeof(RolesController)
            .GetCustomAttributes(typeof(ApiExplorerSettingsAttribute), inherit: true)
            .Cast<ApiExplorerSettingsAttribute>()
            .Should()
            .NotContain(attribute => attribute.IgnoreApi);

        var administrativeEndpointNames = new[]
        {
            nameof(RolesController.GetAll),
            nameof(RolesController.GetById),
            nameof(RolesController.Create),
            nameof(RolesController.Update),
            nameof(RolesController.Delete),
            nameof(RolesController.GetUserRoles),
            nameof(RolesController.AssignRoleToUser),
            nameof(RolesController.BulkAssignRoles),
            nameof(RolesController.RemoveRoleFromUser)
        };

        foreach (var methodName in administrativeEndpointNames)
        {
            var authorize = typeof(RolesController)
                .GetMethod(methodName)!
                .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
                .Cast<AuthorizeAttribute>()
                .Should()
                .ContainSingle()
                .Subject;

            authorize.Policy.Should().Be(Policies.RolesManage);
        }
    }

    [Fact]
    public async Task GetAll_ShouldReturnOkAndForwardQuery()
    {
        var sender = new Mock<ISender>();
        List<RoleDto>? response = [CreateRoleDto()];
        GetRolesQuery? captured = null;

        sender
            .Setup(x => x.Send(It.IsAny<GetRolesQuery>(), It.IsAny<CancellationToken>()))
            .Callback<object, CancellationToken>((request, _) => captured = (GetRolesQuery)request)
            .ReturnsAsync(response);

        var controller = CreateRolesController(sender, actorIsSystemAdmin: true);
        var tenantId = Guid.NewGuid();

        var result = await controller.GetAll(tenantId, includeInactive: true, CancellationToken.None);

        captured.Should().NotBeNull();
        captured!.TenantId.Should().Be(tenantId);
        captured.IncludeInactive.Should().BeTrue();

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().BeSameAs(response);
    }

    [Fact]
    public async Task GetById_WhenRoleExists_ShouldReturnOk()
    {
        var sender = new Mock<ISender>();
        var role = CreateRoleDto();
        GetRoleByIdQuery? captured = null;

        sender
            .Setup(x => x.Send(It.IsAny<GetRoleByIdQuery>(), It.IsAny<CancellationToken>()))
            .Callback<object, CancellationToken>((request, _) => captured = (GetRoleByIdQuery)request)
            .ReturnsAsync(role);

        var controller = CreateRolesController(sender, actorIsSystemAdmin: true);

        var result = await controller.GetById(role.Id, CancellationToken.None);

        captured.Should().NotBeNull();
        captured!.RoleId.Should().Be(role.Id);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().BeSameAs(role);
    }

    [Fact]
    public async Task GetById_WhenRoleMissing_ShouldReturnNotFound()
    {
        var sender = new Mock<ISender>();
        var roleId = Guid.NewGuid();

        sender
            .Setup(x => x.Send(It.IsAny<GetRoleByIdQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((RoleDto?)null);

        var controller = CreateRolesController(sender, actorIsSystemAdmin: true);

        var result = await controller.GetById(roleId, CancellationToken.None);

        var notFound = result.Should().BeOfType<NotFoundObjectResult>().Subject;
        notFound.Value.Should().Be($"Role with ID '{roleId}' not found");
    }

    [Fact]
    public async Task Create_ShouldReturnCreatedAtActionAndMapRequest()
    {
        var sender = new Mock<ISender>();
        var request = new CreateRoleRequest
        {
            Name = "Admin",
            Description = "Administrator",
            Permissions = ["users.read", "users.write"],
            TenantId = Guid.NewGuid()
        };
        var role = CreateRoleDto(name: request.Name, tenantId: request.TenantId);
        CreateRoleCommand? captured = null;

        sender
            .Setup(x => x.Send(It.IsAny<CreateRoleCommand>(), It.IsAny<CancellationToken>()))
            .Callback<object, CancellationToken>((requestValue, _) => captured = (CreateRoleCommand)requestValue)
            .ReturnsAsync(role);

        var controller = CreateRolesController(sender, actorIsSystemAdmin: true);

        var result = await controller.Create(request, CancellationToken.None);

        captured.Should().NotBeNull();
        captured!.Name.Should().Be(request.Name);
        captured.Description.Should().Be(request.Description);
        captured.Permissions.Should().Equal(request.Permissions);
        captured.TenantId.Should().Be(request.TenantId);

        var created = result.Should().BeOfType<CreatedAtActionResult>().Subject;
        created.ActionName.Should().Be(nameof(RolesController.GetById));
        created.RouteValues!["roleId"].Should().Be(role.Id);
        created.Value.Should().BeSameAs(role);
    }

    [Fact]
    public async Task Update_ShouldReturnOkAndMapRequest()
    {
        var sender = new Mock<ISender>();
        var roleId = Guid.NewGuid();
        var request = new UpdateRoleRequest
        {
            Name = "Editor",
            Description = "Updated description",
            Permissions = ["content.edit"],
            IsActive = true
        };
        var role = CreateRoleDto(id: roleId, name: request.Name!);
        UpdateRoleCommand? captured = null;

        sender
            .Setup(x => x.Send(It.IsAny<UpdateRoleCommand>(), It.IsAny<CancellationToken>()))
            .Callback<object, CancellationToken>((requestValue, _) => captured = (UpdateRoleCommand)requestValue)
            .ReturnsAsync(role);

        var controller = CreateRolesController(sender, actorIsSystemAdmin: true);

        var result = await controller.Update(roleId, request, CancellationToken.None);

        captured.Should().NotBeNull();
        captured!.RoleId.Should().Be(roleId);
        captured.Name.Should().Be(request.Name);
        captured.Description.Should().Be(request.Description);
        captured.Permissions.Should().Equal(request.Permissions!);
        captured.IsActive.Should().BeTrue();

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().BeSameAs(role);
    }

    [Fact]
    public async Task Delete_ShouldReturnNoContent()
    {
        var sender = new Mock<ISender>();
        DeleteRoleCommand? captured = null;
        var roleId = Guid.NewGuid();

        sender
            .Setup(x => x.Send(It.IsAny<DeleteRoleCommand>(), It.IsAny<CancellationToken>()))
            .Callback<object, CancellationToken>((requestValue, _) => captured = (DeleteRoleCommand)requestValue)
            .ReturnsAsync(true);

        var controller = CreateRolesController(sender, actorIsSystemAdmin: true);

        var result = await controller.Delete(roleId, CancellationToken.None);

        captured.Should().NotBeNull();
        captured!.RoleId.Should().Be(roleId);
        result.Should().BeOfType<NoContentResult>();
    }

    [Fact]
    public async Task GetUserRoles_ShouldReturnOkAndForwardQuery()
    {
        var sender = new Mock<ISender>();
        var userId = Guid.NewGuid();
        var roles = new List<RoleDto> { CreateRoleDto() };
        GetUserRolesQuery? captured = null;

        sender
            .Setup(x => x.Send(It.IsAny<GetUserRolesQuery>(), It.IsAny<CancellationToken>()))
            .Callback<object, CancellationToken>((requestValue, _) => captured = (GetUserRolesQuery)requestValue)
            .ReturnsAsync(roles);

        var controller = CreateRolesController(sender, actorIsSystemAdmin: true);

        var result = await controller.GetUserRoles(userId, includeExpired: true, CancellationToken.None);

        captured.Should().NotBeNull();
        captured!.UserId.Should().Be(userId);
        captured.IncludeExpired.Should().BeTrue();

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().BeSameAs(roles);
    }

    [Fact]
    public async Task AssignRoleToUser_ShouldReturnCreatedAtActionAndMapRequest()
    {
        var sender = new Mock<ISender>();
        var request = new AssignRoleToUserRequest
        {
            UserId = Guid.NewGuid(),
            RoleId = Guid.NewGuid(),
            ExpiresAt = DateTime.UtcNow.AddDays(7)
        };
        var userRole = new UserRoleDto { Id = Guid.NewGuid(), UserId = request.UserId, RoleId = request.RoleId, ExpiresAt = request.ExpiresAt };
        AssignRoleToUserCommand? captured = null;

        sender
            .Setup(x => x.Send(It.IsAny<AssignRoleToUserCommand>(), It.IsAny<CancellationToken>()))
            .Callback<object, CancellationToken>((requestValue, _) => captured = (AssignRoleToUserCommand)requestValue)
            .ReturnsAsync(userRole);

        var controller = CreateRolesController(sender, actorIsSystemAdmin: true);
        var assignedBy = Guid.NewGuid();
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                    [new Claim(ClaimTypes.NameIdentifier, assignedBy.ToString())],
                    "test"))
            }
        };

        var result = await controller.AssignRoleToUser(request, CancellationToken.None);

        captured.Should().NotBeNull();
        captured!.UserId.Should().Be(request.UserId);
        captured.RoleId.Should().Be(request.RoleId);
        captured.ExpiresAt.Should().Be(request.ExpiresAt);
        captured.AssignedBy.Should().Be(assignedBy);

        var created = result.Should().BeOfType<CreatedAtActionResult>().Subject;
        created.ActionName.Should().Be(nameof(RolesController.GetUserRoles));
        created.RouteValues!["userId"].Should().Be(request.UserId);
        created.Value.Should().BeSameAs(userRole);
    }

    [Fact]
    public async Task BulkAssignRoles_ShouldForwardCommandAndReturnOutcomes()
    {
        var sender = new Mock<ISender>();
        var command = new BulkAssignRolesCommand
        {
            RoleId = Guid.NewGuid(),
            UserIds = [Guid.NewGuid(), Guid.NewGuid()],
            ExpiresAt = DateTime.UtcNow.AddDays(7)
        };
        var response = new BulkRoleAssignmentResult
        {
            RoleId = command.RoleId,
            TotalRequested = command.UserIds.Count,
            Assigned = command.UserIds.Count
        };
        BulkAssignRolesCommand? captured = null;

        sender
            .Setup(x => x.Send(It.IsAny<BulkAssignRolesCommand>(), It.IsAny<CancellationToken>()))
            .Callback<object, CancellationToken>((requestValue, _) => captured = (BulkAssignRolesCommand)requestValue)
            .ReturnsAsync(response);

        var controller = CreateRolesController(sender, actorIsSystemAdmin: true);

        var result = await controller.BulkAssignRoles(command, CancellationToken.None);

        captured.Should().BeSameAs(command);
        var ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().BeSameAs(response);
    }

    [Fact]
    public async Task RemoveRoleFromUser_ShouldReturnNoContentAndMapRequest()
    {
        var sender = new Mock<ISender>();
        var request = new RemoveRoleFromUserRequest { UserId = Guid.NewGuid(), RoleId = Guid.NewGuid() };
        RemoveRoleFromUserCommand? captured = null;

        sender
            .Setup(x => x.Send(It.IsAny<RemoveRoleFromUserCommand>(), It.IsAny<CancellationToken>()))
            .Callback<object, CancellationToken>((requestValue, _) => captured = (RemoveRoleFromUserCommand)requestValue)
            .ReturnsAsync(true);

        var controller = CreateRolesController(sender, actorIsSystemAdmin: true);

        var result = await controller.RemoveRoleFromUser(request, CancellationToken.None);

        captured.Should().NotBeNull();
        captured!.UserId.Should().Be(request.UserId);
        captured.RoleId.Should().Be(request.RoleId);
        result.Should().BeOfType<NoContentResult>();
    }

    // ─── Issue #294: delegated role-administration clamping ───

    [Fact]
    public async Task Create_ByDelegatedActor_ForOwnTenant_IsAllowed()
    {
        var sender = new Mock<ISender>();
        var tenantId = Guid.NewGuid();
        var request = new CreateRoleRequest
        {
            Name = "Editor",
            Description = "Editor role",
            Permissions = ["content.edit"],
            TenantId = tenantId
        };
        var role = CreateRoleDto(name: request.Name, tenantId: tenantId);

        sender
            .Setup(x => x.Send(It.IsAny<CreateRoleCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(role);

        var controller = CreateRolesController(sender, actorIsSystemAdmin: false, tenantId: tenantId,
            permissions: [ManageRolesPermission.Keys.Manage]);

        var result = await controller.Create(request, CancellationToken.None);

        result.Should().BeOfType<CreatedAtActionResult>();
    }

    [Fact]
    public async Task Create_ByDelegatedActor_ForGlobalRole_IsForbidden()
    {
        var sender = new Mock<ISender>();
        var request = new CreateRoleRequest
        {
            Name = "Global",
            Description = "Global role",
            Permissions = ["users.read"],
            TenantId = null
        };

        var controller = CreateRolesController(sender, actorIsSystemAdmin: false, tenantId: Guid.NewGuid(),
            permissions: [ManageRolesPermission.Keys.Manage]);

        var result = await controller.Create(request, CancellationToken.None);

        result.Should().BeOfType<ForbidResult>();
        sender.Verify(x => x.Send(It.IsAny<CreateRoleCommand>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Update_ByDelegatedActor_ForForeignTenantRole_IsForbidden()
    {
        var sender = new Mock<ISender>();
        var foreignTenant = Guid.NewGuid();
        var role = CreateRoleDto(tenantId: foreignTenant);

        sender
            .Setup(x => x.Send(It.IsAny<GetRoleByIdQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(role);

        var controller = CreateRolesController(sender, actorIsSystemAdmin: false, tenantId: Guid.NewGuid(),
            permissions: [ManageRolesPermission.Keys.Manage]);

        var result = await controller.Update(role.Id, new UpdateRoleRequest { Name = "Escalated" }, CancellationToken.None);

        result.Should().BeOfType<ForbidResult>();
        sender.Verify(x => x.Send(It.IsAny<UpdateRoleCommand>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Delete_ByDelegatedActor_ForGlobalRole_IsForbidden()
    {
        var sender = new Mock<ISender>();
        var role = CreateRoleDto(tenantId: null); // global role

        sender
            .Setup(x => x.Send(It.IsAny<GetRoleByIdQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(role);

        var controller = CreateRolesController(sender, actorIsSystemAdmin: false, tenantId: Guid.NewGuid(),
            permissions: [ManageRolesPermission.Keys.Manage]);

        var result = await controller.Delete(role.Id, CancellationToken.None);

        result.Should().BeOfType<ForbidResult>();
        sender.Verify(x => x.Send(It.IsAny<DeleteRoleCommand>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Update_ByDelegatedActor_ForOwnTenantRole_IsAllowed()
    {
        var sender = new Mock<ISender>();
        var tenantId = Guid.NewGuid();
        var role = CreateRoleDto(tenantId: tenantId);

        sender
            .Setup(x => x.Send(It.IsAny<GetRoleByIdQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(role);
        sender
            .Setup(x => x.Send(It.IsAny<UpdateRoleCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(role);

        var controller = CreateRolesController(sender, actorIsSystemAdmin: false, tenantId: tenantId,
            permissions: [ManageRolesPermission.Keys.Manage]);

        var result = await controller.Update(role.Id, new UpdateRoleRequest { Name = "Renamed" }, CancellationToken.None);

        result.Should().BeOfType<OkObjectResult>();
    }

    private static RolesController CreateRolesController(
        Mock<ISender> sender,
        bool actorIsSystemAdmin,
        Guid? tenantId = null,
        string[]? permissions = null)
    {
        var actor = new ActorContext
        {
            IsAuthenticated = true,
            ActorKind = ActorKind.User,
            SubjectId = Guid.NewGuid().ToString(),
            TenantId = tenantId,
            Permissions = new HashSet<string>(permissions ?? [], StringComparer.OrdinalIgnoreCase),
            Roles = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "SystemAdmin" }
        };
        if (!actorIsSystemAdmin)
        {
            actor = actor with { Roles = new HashSet<string>(StringComparer.OrdinalIgnoreCase) };
        }

        var actorContextAccessor = new Mock<IActorContextAccessor>();
        actorContextAccessor.SetupGet(accessor => accessor.ActorContext).Returns(actor);

        return new RolesController(NullLogger<RolesController>.Instance, sender.Object, actorContextAccessor.Object);
    }

    private static RoleDto CreateRoleDto(Guid? id = null, string name = "Admin", Guid? tenantId = null)
    {
        return new RoleDto
        {
            Id = id ?? Guid.NewGuid(),
            Name = name,
            Description = "Role description",
            Permissions = ["users.read"],
            IsActive = true,
            TenantId = tenantId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }
}
