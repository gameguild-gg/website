using FluentAssertions;
using GameGuild.Configuration.PresentationLayer.Authorization;
using GameGuild.Identity.Authorization.Caching;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Moq;

namespace GameGuild.Identity.Authorization.UnitTests;

public sealed class CachedAccessControlListInvalidationTests
{
    [Theory]
    [InlineData(AclPrincipalType.Role)]
    [InlineData(AclPrincipalType.Group)]
    public async Task GrantAccess_InvalidatesEverySubjectOfTheResourceOnlyInItsTenant(AclPrincipalType principalType)
    {
        var inner = new Mock<IAccessControlListService>();
        var tenants = new Mock<ITenantSecurityVersionStore>();
        var users = new Mock<IUserSecurityVersionStore>();
        var metrics = new Mock<ICacheMetricsService>();
        using var cache = new MemoryCache(new MemoryCacheOptions());
        tenants.Setup(value => value.GetTenantAndGlobalVersionsAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((1L, 1L));
        users.Setup(value => value.GetVersionAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(1);
        inner.Setup(value => value.EvaluateAccessAsync(It.IsAny<AclSubject>(), It.IsAny<Guid>(), "Document", It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(AccessLevel.Read);
        var service = new CachedAccessControlListService(inner.Object, cache, tenants.Object, users.Object,
            Options.Create(new AuthorizationCacheOptions { AccessControlListTtlSeconds = 60 }), hybridCache: null, metrics: metrics.Object);
        var tenantId = Guid.NewGuid();
        var otherTenantId = Guid.NewGuid();
        var first = AclSubject.ForUser(Guid.NewGuid());
        var second = AclSubject.ForUser(Guid.NewGuid());
        var grantor = Guid.NewGuid();
        var principal = Guid.NewGuid();
        using var cancellation = new CancellationTokenSource();

        await service.EvaluateAccessAsync(first, tenantId, "Document", "target");
        await service.EvaluateAccessAsync(second, tenantId, "Document", "target");
        await service.EvaluateAccessAsync(first, otherTenantId, "Document", "target");
        await service.EvaluateAccessAsync(first, tenantId, "Document", "other");

        await service.GrantAccessAsync(grantor, principalType, principal, tenantId, "Document", "target", AccessLevel.Write, cancellation.Token);

        await service.EvaluateAccessAsync(first, tenantId, "Document", "target");
        await service.EvaluateAccessAsync(second, tenantId, "Document", "target");
        await service.EvaluateAccessAsync(first, otherTenantId, "Document", "target");
        await service.EvaluateAccessAsync(first, tenantId, "Document", "other");

        inner.Verify(value => value.GrantAccessAsync(grantor, principalType, principal, tenantId, "Document", "target", AccessLevel.Write, cancellation.Token), Times.Once);
        inner.Verify(value => value.EvaluateAccessAsync(first, tenantId, "Document", "target", It.IsAny<CancellationToken>()), Times.Exactly(2));
        inner.Verify(value => value.EvaluateAccessAsync(second, tenantId, "Document", "target", It.IsAny<CancellationToken>()), Times.Exactly(2));
        inner.Verify(value => value.EvaluateAccessAsync(first, otherTenantId, "Document", "target", It.IsAny<CancellationToken>()), Times.Once);
        inner.Verify(value => value.EvaluateAccessAsync(first, tenantId, "Document", "other", It.IsAny<CancellationToken>()), Times.Once);
        metrics.Verify(value => value.RecordEviction(CacheLevel.L1, "acl"), Times.Exactly(2));
    }
}
