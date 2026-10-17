using FluentAssertions;
using GameGuild.Identity.Context.Actors;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace GameGuild.Identity.Authorization.UnitTests.Security;

public sealed class TenantPermissionQueryIsolationTests
{
    [Fact]
    public async Task GetTenantPermissionsQuery_CannotReadAcrossTenantsEvenForTenantAdmin()
    {
        var actorId = Guid.NewGuid();
        var actorTenantId = Guid.NewGuid();
        var otherTenantId = Guid.NewGuid();
        var queryService = new Mock<IPermissionQueryService>();
        var handler = Handler(queryService, actorId, actorTenantId, "TenantAdmin");

        var act = () => handler.Handle(new GetTenantPermissionsQuery
        {
            TenantId = otherTenantId,
            UserId = actorId,
            IncludeEffective = false
        }, CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
        queryService.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task GetTenantPermissionsQuery_RegularUserCanReadOwnPermissionsInOwnTenant()
    {
        var actorId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var queryService = new Mock<IPermissionQueryService>();
        queryService.Setup(query => query.GetTenantPermissionsAsync(actorId, tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(["tenant:read"]);
        var handler = Handler(queryService, actorId, tenantId);

        var result = await handler.Handle(new GetTenantPermissionsQuery
        {
            TenantId = tenantId,
            IncludeEffective = false
        }, CancellationToken.None);

        result.UserId.Should().Be(actorId);
        result.TenantId.Should().Be(tenantId);
        result.Permissions.Should().ContainSingle().Which.Should().Be("tenant:read");
        queryService.VerifyAll();
    }

    [Fact]
    public async Task GetTenantPermissionsQuery_RegularUserCannotReadAnotherUsersPermissions()
    {
        var actorId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var queryService = new Mock<IPermissionQueryService>();
        var handler = Handler(queryService, actorId, tenantId);

        var act = () => handler.Handle(new GetTenantPermissionsQuery
        {
            TenantId = tenantId,
            UserId = Guid.NewGuid(),
            IncludeEffective = false
        }, CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
        queryService.VerifyNoOtherCalls();
    }

    private static GetTenantPermissionsQueryHandler Handler(
        Mock<IPermissionQueryService> queryService,
        Guid actorId,
        Guid tenantId,
        params string[] roles)
    {
        var accessor = new Mock<IActorContextAccessor>();
        accessor.SetupGet(context => context.ActorContext)
            .Returns(ActorContextBuilder.ForUser(actorId)
                .WithTenantId(tenantId)
                .WithRoles(roles)
                .Build());

        var securityVersionStore = Mock.Of<ITenantSecurityVersionStore>(store =>
            store.GetVersionAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()) == Task.FromResult(1L));

        return new GetTenantPermissionsQueryHandler(
            queryService.Object,
            accessor.Object,
            securityVersionStore,
            new Microsoft.Extensions.Caching.Memory.MemoryCache(new Microsoft.Extensions.Caching.Memory.MemoryCacheOptions()),
            NullLogger<GetTenantPermissionsQueryHandler>.Instance);
    }
}
