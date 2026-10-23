using GameGuild.API.Database;
using GameGuild.Identity.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace GameGuild.API.UnitTests.Database;

public sealed class EntityChangeAuditSaveEntrypointTests
{
    [Theory]
    [InlineData("sync", false, true)]
    [InlineData("sync-accept", false, true)]
    [InlineData("sync-retain", false, false)]
    [InlineData("async", true, true)]
    [InlineData("async-accept", true, true)]
    [InlineData("async-retain", true, false)]
    public async Task EveryOverloadPreservesProviderModeAcceptanceAndBusinessRowCount(string entrypoint, bool asyncSave, bool accept)
    {
        var calls = new SaveCalls();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .AddInterceptors(calls)
            .Options;
        await using var db = new ApplicationDbContext(options);
        var user = new User { Email = "entrypoint@example.test", Name = "Before" };
        db.Add(user);
        var version = user.Version;

        // Use the DbContext base reference to verify virtual dispatch, as consumers do.
        DbContext context = db;
        var saved = entrypoint switch
        {
            "sync" => context.SaveChanges(),
            "sync-accept" => context.SaveChanges(true),
            "sync-retain" => context.SaveChanges(false),
            "async" => await context.SaveChangesAsync(),
            "async-accept" => await context.SaveChangesAsync(true),
            "async-retain" => await context.SaveChangesAsync(false),
            _ => throw new ArgumentOutOfRangeException(nameof(entrypoint))
        };

        Assert.Equal(1, saved);
        Assert.Equal(version + 1, user.Version);
        Assert.Equal(asyncSave ? 0 : 1, calls.Sync);
        Assert.Equal(asyncSave ? 1 : 0, calls.Async);
        Assert.Equal(accept ? EntityState.Unchanged : EntityState.Added, db.Entry(user).State);
        var stagedAudit = Assert.Single(db.ChangeTracker.Entries<EntityChangeAuditRecord>());
        Assert.Equal(accept ? EntityState.Unchanged : EntityState.Added, stagedAudit.State);
        Assert.Equal(nameof(User), stagedAudit.Entity.EntityName);

        // SaveChanges(false) leaves acceptance to the caller for business and audit rows.
        db.ChangeTracker.AcceptAllChanges();
        Assert.Equal(0, await db.SaveChangesAsync());
        await using var observed = new ApplicationDbContext(options);
        Assert.Equal(user.Id, (await observed.Set<User>().IgnoreQueryFilters().SingleAsync()).Id);
        var persisted = Assert.Single(await observed.Set<EntityChangeAuditRecord>().ToListAsync());
        Assert.Equal(user.Id.ToString(), persisted.KeyId);
        Assert.Equal("Added", persisted.ChangeKind);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AsyncOverloadsForwardCancellationWithoutPersistingBusinessOrAudit(bool explicitAcceptance)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        await using var db = new ApplicationDbContext(options);
        db.Add(new User { Email = "cancelled@example.test", Name = "Not committed" });
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => explicitAcceptance
            ? db.SaveChangesAsync(false, cancellation.Token)
            : db.SaveChangesAsync(cancellation.Token));

        await using var observed = new ApplicationDbContext(options);
        Assert.Empty(await observed.Set<User>().IgnoreQueryFilters().ToListAsync());
        Assert.Empty(await observed.Set<EntityChangeAuditRecord>().ToListAsync());
    }

    private sealed class SaveCalls : SaveChangesInterceptor
    {
        public int Sync { get; private set; }
        public int Async { get; private set; }

        public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
        {
            Assert.IsType<ApplicationDbContext>(eventData.Context);
            Sync++;
            return result;
        }

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            Assert.IsType<ApplicationDbContext>(eventData.Context);
            Async++;
            return ValueTask.FromResult(result);
        }
    }
}
