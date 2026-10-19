using GameGuild.Identity.Authorization;
using GameGuild.Identity.Authorization.Caching;
using GameGuild.Identity.Context.Actors;
using Moq;

namespace GameGuild.Identity.Authentication.UnitTests.Infrastructure;

/// <summary>Explicit privileged actor and side-effect doubles for legacy component tests; no atomic-storage claim.</summary>
internal static class PermissionMutationTestServices
{
    public static PermissionService Create(IApplicationDbContext context, Guid? tenant = null,
        ITenantSecurityVersionStore? versionStore = null, IHybridPermissionCache? cache = null)
    {
        var versions = new Mock<ITenantSecurityVersionStore>();
        versions.Setup(value => value.IncrementVersionAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(1L);
        var audit = new Mock<IPermissionAuditService>();
        audit.Setup(value => value.LogPermissionChangeAsync(It.IsAny<PermissionOperationType>(), It.IsAny<Guid?>(),
            It.IsAny<Guid>(), It.IsAny<Guid?>(), It.IsAny<string?>(), It.IsAny<Guid?>(), It.IsAny<string?>(), It.IsAny<string?>(),
            It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<bool>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(),
            It.IsAny<CancellationToken>())).ReturnsAsync(() => new PermissionAuditLog());
        var actors = new Mock<IActorContextAccessor>();
        actors.SetupGet(value => value.ActorContext).Returns(ActorContextBuilder.ForUser(Guid.NewGuid())
            .WithTenantId(tenant ?? Guid.NewGuid()).WithRole("SystemAdmin").Build());
        return new PermissionService(context, versionStore ?? versions.Object, audit.Object, actors.Object, cache);
    }
}
