using GameGuild.Identity.Authorization;
using GameGuild.Identity.Context.Actors;
using Moq;
using Xunit;
using Facade = GameGuild.Identity.Authentication.PermissionService;

namespace GameGuild.Identity.Authentication.UnitTests.Services;

public sealed class PermissionUpdateContractTests
{
    [Theory]
    [InlineData("anonymous", ErrorType.Unauthorized)]
    [InlineData("member", ErrorType.Forbidden)]
    [InlineData("foreign-admin", ErrorType.Forbidden)]
    [InlineData("global-admin", ErrorType.Forbidden)]
    [InlineData("ambiguous-global", ErrorType.Validation)]
    [InlineData("empty-tenant", ErrorType.Validation)]
    public async Task AllFamiliesRejectInvalidActorOrScopeBeforeAccessingStorage(string scenario, ErrorType expected)
    {
        var context = new Mock<IApplicationDbContext>(MockBehavior.Strict);
        var actors = new ActorContextAccessor();
        var tenant = Guid.NewGuid();
        var actor = scenario == "anonymous" ? ActorContextBuilder.Create().Build()
            : ActorContextBuilder.ForUser(Guid.NewGuid()).WithTenantId(tenant)
                .WithRoles(scenario == "member" ? [] : new[] { "TenantAdmin" }).Build();
        using var lease = actors.BeginActorContextScope(actor);
        IPermissionUpdateService service = new Facade(context.Object, actorContextAccessor: actors);
        var request = Valid<string>() with
        {
            TenantId = scenario == "foreign-admin" ? Guid.NewGuid() : scenario == "empty-tenant" ? Guid.Empty : null,
            GlobalScope = scenario is "global-admin" or "ambiguous-global"
        };
        if (scenario == "ambiguous-global")
        {
            request = request with { TenantId = tenant };
        }
        var typed = Typed(request);
        Assert.Equal(expected, (await service.UpdateTenantPermissionAsync(request)).Error.Type);
        Assert.Equal(expected, (await service.UpdateContentTypePermissionAsync(typed)).Error.Type);
        Assert.Equal(expected, (await service.UpdateResourcePermissionAsync<GenericResourcePermission, EntityBase>(typed)).Error.Type);
        context.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData("empty-id")]
    [InlineData("zero-version")]
    [InlineData("exhausted-version")]
    [InlineData("null-permissions")]
    [InlineData("long-reason")]
    [InlineData("unspecified-expiry")]
    [InlineData("past-active-expiry")]
    public async Task AllFamiliesValidateReplacementBeforeStorage(string invalid)
    {
        var context = new Mock<IApplicationDbContext>(MockBehavior.Strict);
        var actors = new ActorContextAccessor();
        using var actor = actors.BeginActorContextScope(ActorContextBuilder.ForUser(Guid.NewGuid())
            .WithTenantId(Guid.NewGuid()).WithRole("TenantAdmin").Build());
        IPermissionUpdateService service = new Facade(context.Object, actorContextAccessor: actors);
        var request = invalid switch
        {
            "empty-id" => Valid<string>() with { GrantId = Guid.Empty },
            "zero-version" => Valid<string>() with { ExpectedVersion = 0 },
            "exhausted-version" => Valid<string>() with { ExpectedVersion = int.MaxValue },
            "null-permissions" => Valid<string>() with { Permissions = null! },
            "long-reason" => Valid<string>() with { Reason = new string('x', 501) },
            "unspecified-expiry" => Valid<string>() with { ExpiresAt = new DateTime(2099, 1, 1) },
            _ => Valid<string>() with { ExpiresAt = DateTime.UtcNow.AddDays(-1) }
        };
        var typed = Typed(request) with { Permissions = request.Permissions is null ? null! : [] };
        Assert.Equal(ErrorType.Validation, (await service.UpdateTenantPermissionAsync(request)).Error.Type);
        Assert.Equal(ErrorType.Validation, (await service.UpdateContentTypePermissionAsync(typed)).Error.Type);
        Assert.Equal(ErrorType.Validation, (await service.UpdateResourcePermissionAsync<GenericResourcePermission, EntityBase>(typed)).Error.Type);
        context.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public async Task TenantPayloadRejectsBlankKeys(string? permission)
    {
        var context = new Mock<IApplicationDbContext>(MockBehavior.Strict);
        var actors = new ActorContextAccessor();
        using var actor = actors.BeginActorContextScope(ActorContextBuilder.ForUser(Guid.NewGuid())
            .WithTenantId(Guid.NewGuid()).WithPermission("permissions:manage").Build());
        IPermissionUpdateService service = new Facade(context.Object, actorContextAccessor: actors);
        var result = await service.UpdateTenantPermissionAsync(Valid<string>() with { Permissions = new[] { permission! } });
        Assert.Equal(ErrorType.Validation, result.Error.Type);
        context.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task UndefinedTypedPermissionCannotBeWrittenThroughEitherTypedFamily()
    {
        var context = new Mock<IApplicationDbContext>(MockBehavior.Strict);
        var actors = new ActorContextAccessor();
        using var actor = actors.BeginActorContextScope(ActorContextBuilder.ForUser(Guid.NewGuid())
            .WithTenantId(Guid.NewGuid()).WithRole("TenantAdmin").Build());
        IPermissionUpdateService service = new Facade(context.Object, actorContextAccessor: actors);
        var request = Valid<PermissionType>() with { Permissions = new[] { (PermissionType)int.MaxValue } };
        Assert.Equal(ErrorType.Validation, (await service.UpdateContentTypePermissionAsync(request)).Error.Type);
        Assert.Equal(ErrorType.Validation, (await service.UpdateResourcePermissionAsync<GenericResourcePermission, EntityBase>(request)).Error.Type);
        context.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task MissingMandatoryCollaboratorsFailClosedInsteadOfWritingAnUnauditedGrant()
    {
        var context = new Mock<IApplicationDbContext>(MockBehavior.Strict);
        var actors = new ActorContextAccessor();
        using var actor = actors.BeginActorContextScope(ActorContextBuilder.ForUser(Guid.NewGuid())
            .WithTenantId(Guid.NewGuid()).WithRole("TenantAdmin").Build());
        IPermissionUpdateService service = new Facade(context.Object, actorContextAccessor: actors);
        var result = await service.UpdateTenantPermissionAsync(Valid<string>());
        Assert.Equal("PermissionWrite.CollaboratorsRequired", result.Error.Code);
        context.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData("global-permission")]
    [InlineData("global-system-admin")]
    [InlineData("cross-tenant-system-admin")]
    [InlineData("tenant-manager")]
    public async Task AuthorizedScopeStillRequiresTransactionalRelationalStorage(string scenario)
    {
        var context = new Mock<IApplicationDbContext>(MockBehavior.Strict);
        var versions = new Mock<ITenantSecurityVersionStore>(MockBehavior.Strict);
        var audit = new Mock<IPermissionAuditService>(MockBehavior.Strict);
        var actors = new ActorContextAccessor();
        var builder = ActorContextBuilder.ForUser(Guid.NewGuid()).WithTenantId(Guid.NewGuid());
        if (scenario.Contains("system-admin", StringComparison.Ordinal))
        {
            builder.WithRole("SystemAdmin");
        }
        else
        {
            builder.WithPermission(scenario == "global-permission" ? SystemPermission.Keys.ManageGlobalDefaults : "permissions:manage");
        }
        using var actor = actors.BeginActorContextScope(builder.Build());
        IPermissionUpdateService service = new Facade(context.Object, versions.Object, audit.Object, actors);
        var request = Valid<string>() with
        {
            GlobalScope = scenario.StartsWith("global", StringComparison.Ordinal),
            TenantId = scenario == "cross-tenant-system-admin" ? Guid.NewGuid() : null
        };
        Assert.Equal("PermissionWrite.RelationalContextRequired", (await service.UpdateTenantPermissionAsync(request)).Error.Code);
        context.VerifyNoOtherCalls();
        versions.VerifyNoOtherCalls();
        audit.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task CancellationAndNullRequestsRemainExplicitProgrammingOutcomes()
    {
        var context = new Mock<IApplicationDbContext>(MockBehavior.Strict);
        IPermissionUpdateService service = new Facade(context.Object);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.UpdateTenantPermissionAsync(Valid<string>(), cancellation.Token));
        await Assert.ThrowsAsync<ArgumentNullException>(() => service.UpdateTenantPermissionAsync(null!));
        await Assert.ThrowsAsync<ArgumentNullException>(() => service.UpdateContentTypePermissionAsync(null!));
        await Assert.ThrowsAsync<ArgumentNullException>(() => service.UpdateResourcePermissionAsync<GenericResourcePermission, EntityBase>(null!));
        context.VerifyNoOtherCalls();
    }

    private static PermissionGrantUpdate<T> Valid<T>() => new()
    {
        GrantId = Guid.NewGuid(), ExpectedVersion = 1, Permissions = []
    };

    private static PermissionGrantUpdate<PermissionType> Typed(PermissionGrantUpdate<string> source) => new()
    {
        GrantId = source.GrantId, ExpectedVersion = source.ExpectedVersion, Permissions = [],
        TenantId = source.TenantId, GlobalScope = source.GlobalScope, IsActive = source.IsActive,
        ExpiresAt = source.ExpiresAt, Reason = source.Reason
    };
}
