using System;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using GameGuild.Identity.Authorization;
using Moq;
using Xunit;

namespace GameGuild.Identity.Authorization.UnitTests.Services;

public sealed class DatabaseTenantSecurityVersionStoreTests
{
    [Fact]
    public async Task GetTenantAndGlobalVersionsAsync_BatchesTenantAndGlobalScopesInOneRepositoryRead()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        IReadOnlyCollection<Guid>? requestedScopes = null;
        var repository = new Mock<ITenantSecurityVersionRepository>();
        repository
            .Setup(store => store.GetVersionsAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .Callback<IReadOnlyCollection<Guid>, CancellationToken>((scopes, _) => requestedScopes = scopes)
            .ReturnsAsync(new Dictionary<Guid, long>
            {
                [tenantA] = 7,
                [tenantB] = 11,
                [Guid.Empty] = 3
            });
        var versionStore = new DatabaseTenantSecurityVersionStore(repository.Object);

        var versions = await versionStore.GetTenantAndGlobalVersionsAsync([tenantA, tenantB, tenantA]);

        versions.Should().HaveCount(2);
        versions[tenantA].Should().Be((7L, 3L));
        versions[tenantB].Should().Be((11L, 3L));
        requestedScopes.Should().BeEquivalentTo([tenantA, tenantB, Guid.Empty]);
        repository.Verify(
            store => store.GetVersionsAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task IncrementVersionAsync_GlobalScope_UsesReservedEmptyGuid()
    {
        var repository = new Mock<ITenantSecurityVersionRepository>();
        repository.Setup(store => store.IncrementVersionAsync(Guid.Empty, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(4);
        var versionStore = new DatabaseTenantSecurityVersionStore(repository.Object);

        var version = await versionStore.IncrementVersionAsync(Guid.Empty.ToString());

        version.Should().Be(4);
        repository.Verify(store => store.IncrementVersionAsync(Guid.Empty, null, It.IsAny<CancellationToken>()), Times.Once);
    }
}
