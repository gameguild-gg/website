using FluentAssertions;
using GameGuild.CQRS.Models;
using GameGuild.Identity.Context.Actors;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace GameGuild.Identity.Authorization.UnitTests.Queries;

/// <summary>
///     Pagination, sorting and response-caching tests for GetTenantPermissionsQuery plus
///     the HasGlobalPermissionQuery system-wide validation (issue #307) and its guard.
/// </summary>
public class TenantPermissionQueryPagingAndGlobalTests
{
    private readonly Mock<IPermissionQueryService> _queryService = new();
    private readonly Mock<ITenantSecurityVersionStore> _versionStore = new();
    private readonly Mock<IActorContextAccessor> _actorAccessor = new();
    private readonly MemoryCache _cache = new(new MemoryCacheOptions { SizeLimit = 1000 });

    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _tenantId = Guid.NewGuid();

    private GetTenantPermissionsQueryHandler CreateSut() => new(
        _queryService.Object,
        _actorAccessor.Object,
        _versionStore.Object,
        _cache,
        NullLogger<GetTenantPermissionsQueryHandler>.Instance);

    private void SetActor(Guid userId, Guid tenantId, params string[] roles) =>
        _actorAccessor
            .SetupGet(a => a.ActorContext)
            .Returns(ActorContextBuilder.ForUser(userId)
                .WithTenantId(tenantId)
                .WithRoles(roles)
                .Build());

    private void SetupVersion(long version) =>
        _versionStore
            .Setup(v => v.GetVersionAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(version);

    private void SetupPermissions(params string[] permissions) =>
        _queryService
            .Setup(q => q.GetTenantPermissionsAsync(_userId, _tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(permissions.ToList());

    [Fact]
    public async Task Pagination_ReturnsRequestedPageWithTotals()
    {
        SetActor(_userId, _tenantId);
        SetupVersion(1);
        SetupPermissions(Enumerable.Range(0, 25).Select(i => $"perm-{i:D2}").ToArray());

        var result = await CreateSut().Handle(
            new GetTenantPermissionsQuery
            {
                TenantId = new TenantId(_tenantId),
                UserId = _userId,
                IncludeEffective = false,
                Page = 3,
                PageSize = 10
            },
            CancellationToken.None);

        result.TotalCount.Should().Be(25);
        result.TotalPages.Should().Be(3);
        result.Page.Should().Be(3);
        result.PageSize.Should().Be(10);
        result.Permissions.Should().HaveCount(5);
        result.Permissions.Should().BeInAscendingOrder();
    }

    [Fact]
    public async Task Pagination_CapsPageSize()
    {
        SetActor(_userId, _tenantId);
        SetupVersion(1);
        SetupPermissions(Enumerable.Range(0, 300).Select(i => $"perm-{i:D3}").ToArray());

        var result = await CreateSut().Handle(
            new GetTenantPermissionsQuery
            {
                TenantId = new TenantId(_tenantId),
                UserId = _userId,
                IncludeEffective = false,
                Page = 1,
                PageSize = 1000
            },
            CancellationToken.None);

        result.PageSize.Should().Be(200);
        result.Permissions.Should().HaveCount(200);
    }

    [Fact]
    public async Task Sorting_DescendingOrdersByName()
    {
        SetActor(_userId, _tenantId);
        SetupVersion(1);
        SetupPermissions("alpha", "beta", "gamma");

        var result = await CreateSut().Handle(
            new GetTenantPermissionsQuery
            {
                TenantId = new TenantId(_tenantId),
                UserId = _userId,
                IncludeEffective = false,
                Page = 1,
                PageSize = 10,
                SortDescending = true
            },
            CancellationToken.None);

        result.Permissions.Should().ContainInOrder("gamma", "beta", "alpha");
    }

    [Fact]
    public async Task Cache_SecondIdenticalQuery_DoesNotHitTheQueryService()
    {
        SetActor(_userId, _tenantId);
        SetupVersion(5);
        SetupPermissions("Read", "Write");

        var sut = CreateSut();
        var query = new GetTenantPermissionsQuery
        {
            TenantId = new TenantId(_tenantId),
            UserId = _userId,
            IncludeEffective = false
        };

        _ = await sut.Handle(query, CancellationToken.None);
        _ = await sut.Handle(query, CancellationToken.None);

        _queryService.Verify(
            q => q.GetTenantPermissionsAsync(_userId, _tenantId, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Cache_VersionBump_MissesImmediately()
    {
        SetActor(_userId, _tenantId);
        _versionStore.SetupSequence(v => v.GetVersionAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(1L).ReturnsAsync(2L);
        SetupPermissions("Read", "Write");

        var sut = CreateSut();
        var query = new GetTenantPermissionsQuery
        {
            TenantId = new TenantId(_tenantId),
            UserId = _userId,
            IncludeEffective = false
        };

        _ = await sut.Handle(query, CancellationToken.None);
        _ = await sut.Handle(query, CancellationToken.None);

        // The second call saw a different security version, so the cached entry for the
        // old version must not be served — the store is consulted again.
        _queryService.Verify(
            q => q.GetTenantPermissionsAsync(_userId, _tenantId, It.IsAny<CancellationToken>()),
            Times.Exactly(2));
    }

    [Fact]
    public async Task TenantAdmin_CannotReadAnotherTenantsPermissions()
    {
        SetActor(_userId, _tenantId, "TenantAdmin");

        var act = () => CreateSut().Handle(
            new GetTenantPermissionsQuery
            {
                TenantId = new TenantId(Guid.NewGuid()),
                UserId = _userId,
                IncludeEffective = false
            },
            CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }
}

/// <summary>
///     HasGlobalPermissionQuery tests (issue #307): system-wide permission validation
///     against the global-defaults baseline with a fail-closed admin guard.
/// </summary>
public class HasGlobalPermissionQueryTests
{
    private readonly Mock<IPermissionQueryService> _queryService = new();
    private readonly Mock<IActorContextAccessor> _actorAccessor = new();

    private HasGlobalPermissionQueryHandler CreateSut() => new(
        _queryService.Object,
        _actorAccessor.Object,
        NullLogger<HasGlobalPermissionQueryHandler>.Instance);

    private void SetActor(string[] roles, string[] permissions) =>
        _actorAccessor
            .SetupGet(a => a.ActorContext)
            .Returns(ActorContextBuilder.ForUser(Guid.NewGuid())
                .WithRoles(roles)
                .WithPermissions(permissions)
                .Build());

    [Fact]
    public async Task SystemAdmin_Validation_TrueWhenBaselineGrants()
    {
        SetActor(["SystemAdmin"], []);
        _queryService
            .Setup(q => q.GetGlobalDefaultPermissionsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(["features:read", "content:read"]);

        var result = await CreateSut().Handle(
            new HasGlobalPermissionQuery { Permission = "features:read" },
            CancellationToken.None);

        result.HasPermission.Should().BeTrue();
        result.Source.Should().Be("GlobalDefault");
        result.DenialReason.Should().BeNull();
    }

    [Fact]
    public async Task SystemAdmin_Validation_FalseWhenBaselineDoesNotGrant()
    {
        SetActor(["SystemAdmin"], []);
        _queryService
            .Setup(q => q.GetGlobalDefaultPermissionsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(["features:read"]);

        var result = await CreateSut().Handle(
            new HasGlobalPermissionQuery { Permission = "system:admin" },
            CancellationToken.None);

        result.HasPermission.Should().BeFalse();
        result.DenialReason.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Validation_IsCaseInsensitive()
    {
        SetActor(["SystemAdmin"], []);
        _queryService
            .Setup(q => q.GetGlobalDefaultPermissionsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(["Features:Read"]);

        var result = await CreateSut().Handle(
            new HasGlobalPermissionQuery { Permission = "features:read" },
            CancellationToken.None);

        result.HasPermission.Should().BeTrue();
    }

    [Fact]
    public async Task UnprivilegedActor_IsRejectedFailClosed()
    {
        SetActor([], []);

        var act = () => CreateSut().Handle(
            new HasGlobalPermissionQuery { Permission = "features:read" },
            CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
        _queryService.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task ManageGlobalDefaultsHolder_MayValidate()
    {
        SetActor([], [SystemPermission.Keys.ManageGlobalDefaults]);
        _queryService
            .Setup(q => q.GetGlobalDefaultPermissionsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var result = await CreateSut().Handle(
            new HasGlobalPermissionQuery { Permission = "features:read" },
            CancellationToken.None);

        result.HasPermission.Should().BeFalse();
    }
}
