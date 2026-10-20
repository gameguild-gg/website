using GameGuild.API.Database;
using GameGuild.API.IntegrationTests.Infrastructure;
using GameGuild.Identity.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace GameGuild.API.IntegrationTests;

[Collection(ApiPostgreSqlCollection.Name)]
public sealed class TenantSecurityVersionFreshReadPostgreSqlTests(ApiPostgreSqlFixture fixture)
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RepeatedSecuritySnapshotsObserveCommittedChangesDespiteAnAlreadyTrackedRow(bool globalScope)
    {
        await using var reader = fixture.Factory.Services.CreateAsyncScope();
        await using var writer = fixture.Factory.Services.CreateAsyncScope();
        var target = globalScope ? Guid.Empty : Guid.NewGuid();
        var requestTenant = globalScope ? Guid.NewGuid() : target;
        var writeRepository = writer.ServiceProvider.GetRequiredService<ITenantSecurityVersionRepository>();
        var initial = (await writeRepository.GetOrCreateAsync(target)).SecurityVersion;
        var readRepository = reader.ServiceProvider.GetRequiredService<ITenantSecurityVersionRepository>();
        var tracked = await readRepository.GetByTenantIdAsync(target);
        Assert.NotNull(tracked);
        Assert.Equal(initial, tracked.SecurityVersion);
        var store = reader.ServiceProvider.GetRequiredService<ITenantSecurityVersionStore>();
        var before = await store.GetTenantAndGlobalVersionsAsync(requestTenant);
        Assert.Equal(initial, globalScope ? before.GlobalVersion : before.TenantVersion);

        var committed = await writeRepository.IncrementVersionAsync(target, "concurrent security change");
        var after = await store.GetTenantAndGlobalVersionsAsync(requestTenant);
        Assert.Equal(committed, globalScope ? after.GlobalVersion : after.TenantVersion);
        Assert.Equal(initial + 1, committed);

        var committedAgain = await writeRepository.IncrementVersionAsync(target, "second concurrent security change");
        var afterAgain = await readRepository.GetVersionsAsync([target, target]);
        Assert.Equal(committedAgain, afterAgain[target]);
        Assert.Equal(committed + 1, committedAgain);
        Assert.Single(afterAgain);
        // A security snapshot must not replace the caller's existing tracked state.
        Assert.Equal(initial, tracked.SecurityVersion);
        Assert.Same(tracked, await readRepository.GetByTenantIdAsync(target));
        var db = reader.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.Equal(EntityState.Unchanged, db.Entry(tracked).State);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PersistedSecuritySnapshotNeitherPublishesNorOverwritesUnsavedCallerValues(bool globalScope)
    {
        await using var reader = fixture.Factory.Services.CreateAsyncScope();
        await using var writer = fixture.Factory.Services.CreateAsyncScope();
        var target = globalScope ? Guid.Empty : Guid.NewGuid();
        var writeRepository = writer.ServiceProvider.GetRequiredService<ITenantSecurityVersionRepository>();
        var initial = (await writeRepository.GetOrCreateAsync(target)).SecurityVersion;
        var readRepository = reader.ServiceProvider.GetRequiredService<ITenantSecurityVersionRepository>();
        var tracked = await readRepository.GetByTenantIdAsync(target);
        Assert.NotNull(tracked);
        tracked.SecurityVersion = initial + 1000;
        var db = reader.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.ChangeTracker.DetectChanges();
        Assert.Equal(EntityState.Modified, db.Entry(tracked).State);

        var committed = await writeRepository.IncrementVersionAsync(target, "committed in separate context");
        var snapshot = await readRepository.GetVersionsAsync([target]);

        Assert.Equal(committed, snapshot[target]);
        Assert.Equal(initial + 1000, tracked.SecurityVersion);
        Assert.Equal(EntityState.Modified, db.Entry(tracked).State);
        var persisted = await writer.ServiceProvider.GetRequiredService<ApplicationDbContext>()
            .Set<TenantSecurityVersion>().AsNoTracking().SingleAsync(row => row.TenantId == target && row.DeletedAt == null);
        Assert.Equal(committed, persisted.SecurityVersion);
    }

    [Fact]
    public async Task SnapshotProjectsOnlyRequestedScopesWithoutAddingTrackedVersionRows()
    {
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        var unrelated = Guid.NewGuid();
        await using var writer = fixture.Factory.Services.CreateAsyncScope();
        var writeRepository = writer.ServiceProvider.GetRequiredService<ITenantSecurityVersionRepository>();
        var firstVersion = (await writeRepository.GetOrCreateAsync(first)).SecurityVersion;
        var secondVersion = (await writeRepository.GetOrCreateAsync(second)).SecurityVersion;
        await writeRepository.GetOrCreateAsync(unrelated);
        await using var reader = fixture.Factory.Services.CreateAsyncScope();
        var db = reader.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var repository = reader.ServiceProvider.GetRequiredService<ITenantSecurityVersionRepository>();
        Assert.Empty(db.ChangeTracker.Entries<TenantSecurityVersion>());

        var snapshot = await repository.GetVersionsAsync([first, second, first]);

        Assert.Equal(2, snapshot.Count);
        Assert.Equal(firstVersion, snapshot[first]);
        Assert.Equal(secondVersion, snapshot[second]);
        Assert.DoesNotContain(unrelated, snapshot.Keys);
        Assert.Empty(db.ChangeTracker.Entries<TenantSecurityVersion>());
        Assert.Empty(await repository.GetVersionsAsync(Array.Empty<Guid>()));
    }
}
