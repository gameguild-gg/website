using System.Security.Cryptography;
using System.Text.Json;
using GameGuild.API.Database;
using GameGuild.API.Eventing;
using GameGuild.API.IntegrationTests.Infrastructure;
using GameGuild.Compliance.Audit;
using GameGuild.Identity.Tenants;
using GameGuild.Identity.Users;
using Microsoft.EntityFrameworkCore;

namespace GameGuild.API.IntegrationTests;

[Collection(ApiPostgreSqlCollection.Name)]
public sealed class EntityChangeAuditSaveEntrypointPostgreSqlTests(ApiPostgreSqlFixture fixture)
{
    [Theory]
    [InlineData("sync")]
    [InlineData("sync-accept")]
    [InlineData("sync-retain")]
    [InlineData("async")]
    [InlineData("async-accept")]
    [InlineData("async-retain")]
    public async Task EveryOverloadPersistsLifecycleAuditRedactionAndOperationOutbox(string entrypoint)
    {
        var accessor = new UseCaseOperationContextAccessor();
        await using var db = CreateContext(accessor);
        var user = NewUser();
        var initialHash = Convert.ToHexString(RandomNumberGenerator.GetBytes(24));
        var replacementHash = Convert.ToHexString(RandomNumberGenerator.GetBytes(24));
        user.PasswordHash = initialHash;
        var actorId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var retainState = entrypoint.EndsWith("-retain", StringComparison.Ordinal);

        foreach (var change in new[] { EntityState.Added, EntityState.Modified, EntityState.Deleted })
        {
            var correlationId = Guid.NewGuid();
            using var operation = accessor.Begin(Operation(tenantId, actorId, correlationId));
            if (change == EntityState.Added)
            {
                db.Add(user);
            }
            else if (change == EntityState.Modified)
            {
                user.Name = "After";
                user.PasswordHash = replacementHash;
            }
            else
            {
                db.Remove(user);
            }

            Assert.Equal(1, await SaveAsync(db, entrypoint));
            var expectedState = retainState ? change : change == EntityState.Deleted ? EntityState.Detached : EntityState.Unchanged;
            Assert.Equal(expectedState, db.Entry(user).State);
            db.ChangeTracker.AcceptAllChanges();
            Assert.Equal(0, await SaveAsync(db, entrypoint));
            db.ChangeTracker.AcceptAllChanges();

            await using var observed = CreateContext();
            var record = Assert.Single(await Records(observed, correlationId).ToListAsync());
            Assert.Equal(nameof(User), record.EntityName);
            Assert.Equal(user.Id.ToString(), record.KeyId);
            Assert.Equal(change.ToString(), record.ChangeKind);
            Assert.Equal<Guid?>(actorId, record.ActorId);
            Assert.Equal<Guid?>(tenantId, record.TenantId);
            if (change != EntityState.Added)
            {
                Assert.Equal("[REDACTED]", Value(record.BeforeValues, nameof(User.PasswordHash)));
                Assert.Equal(change == EntityState.Modified ? "Before" : "After", Value(record.BeforeValues, nameof(User.Name)));
            }
            else
            {
                Assert.Null(record.BeforeValues);
            }
            if (change != EntityState.Deleted)
            {
                Assert.Equal("[REDACTED]", Value(record.AfterValues, nameof(User.PasswordHash)));
                Assert.Equal(change == EntityState.Added ? "Before" : "After", Value(record.AfterValues, nameof(User.Name)));
            }
            else
            {
                Assert.Null(record.AfterValues);
            }
            Assert.DoesNotContain(initialHash, (record.BeforeValues ?? string.Empty) + record.AfterValues);
            Assert.DoesNotContain(replacementHash, (record.BeforeValues ?? string.Empty) + record.AfterValues);
            var business = await observed.Set<User>().IgnoreQueryFilters().SingleOrDefaultAsync(row => row.Id == user.Id);
            if (change == EntityState.Deleted)
            {
                Assert.Null(business);
            }
            else
            {
                Assert.NotNull(business);
                Assert.Equal(change == EntityState.Added ? initialHash : replacementHash, business.PasswordHash);
            }
            Assert.Single(await observed.Set<OutboxMessage>().Where(row => row.CorrelationId == correlationId).ToListAsync());
        }
    }

    [Theory]
    [InlineData("sync")]
    [InlineData("sync-accept")]
    [InlineData("sync-retain")]
    [InlineData("async")]
    [InlineData("async-accept")]
    [InlineData("async-retain")]
    public async Task EveryOverloadRollsBackBusinessAuditAndOutboxTogether(string entrypoint)
    {
        var accessor = new UseCaseOperationContextAccessor();
        await using var db = CreateContext(accessor);
        var user = NewUser();
        var correlationId = Guid.NewGuid();
        await using (var transaction = await db.Database.BeginTransactionAsync())
        {
            using var operation = accessor.Begin(Operation(Guid.NewGuid(), Guid.NewGuid(), correlationId));
            db.Add(user);
            Assert.Equal(1, await SaveAsync(db, entrypoint));
            Assert.Single(await Records(db, correlationId).ToListAsync());
            await transaction.RollbackAsync();
        }

        await using var observed = CreateContext();
        Assert.False(await observed.Set<User>().IgnoreQueryFilters().AnyAsync(row => row.Id == user.Id));
        Assert.Empty(await Records(observed, correlationId).ToListAsync());
        Assert.Empty(await observed.Set<OutboxMessage>().Where(row => row.CorrelationId == correlationId).ToListAsync());
    }

    [Theory]
    [InlineData("sync")]
    [InlineData("sync-accept")]
    [InlineData("sync-retain")]
    [InlineData("async")]
    [InlineData("async-accept")]
    [InlineData("async-retain")]
    public async Task EveryOverloadLeavesNoPartialWriteWhenPersistenceFails(string entrypoint)
    {
        var accessor = new UseCaseOperationContextAccessor();
        await using var db = CreateContext(accessor);
        var user = NewUser();
        db.Add(user);
        await db.SaveChangesAsync();
        var member = new TenantMember { Id = Guid.NewGuid(), UserId = user.Id, TenantId = Guid.NewGuid(), Role = "Member", IsActive = true };
        var correlationId = Guid.NewGuid();
        using (accessor.Begin(Operation(member.TenantId, Guid.NewGuid(), correlationId)))
        {
            user.Name = "Must roll back";
            db.Add(member);
            await Assert.ThrowsAsync<DbUpdateException>(() => SaveAsync(db, entrypoint));
        }

        await using var observed = CreateContext();
        Assert.Equal("Before", (await observed.Set<User>().SingleAsync(row => row.Id == user.Id)).Name);
        Assert.False(await observed.Set<TenantMember>().IgnoreQueryFilters().AnyAsync(row => row.Id == member.Id));
        Assert.Empty(await Records(observed, correlationId).ToListAsync());
        Assert.Empty(await observed.Set<OutboxMessage>().Where(row => row.CorrelationId == correlationId).ToListAsync());
    }

    private ApplicationDbContext CreateContext(UseCaseOperationContextAccessor? accessor = null)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(fixture.ConnectionString).Options;
        var db = new ApplicationDbContext(options, null, accessor ?? new UseCaseOperationContextAccessor());
        Assert.Equal("Npgsql.EntityFrameworkCore.PostgreSQL", db.Database.ProviderName);
        return db;
    }

    private static async Task<int> SaveAsync(DbContext db, string entrypoint) => entrypoint switch
    {
        "sync" => db.SaveChanges(),
        "sync-accept" => db.SaveChanges(true),
        "sync-retain" => db.SaveChanges(false),
        "async" => await db.SaveChangesAsync(),
        "async-accept" => await db.SaveChangesAsync(true),
        "async-retain" => await db.SaveChangesAsync(false),
        _ => throw new ArgumentOutOfRangeException(nameof(entrypoint))
    };

    private static User NewUser()
    {
        var marker = Guid.NewGuid().ToString("N");
        return new User { Id = Guid.NewGuid(), Email = "entrypoint-" + marker + "@example.test", Username = "entry_" + marker, Name = "Before" };
    }

    private static UseCaseOperationContext Operation(Guid tenantId, Guid actorId, Guid correlationId) =>
        new("audit-save-entrypoints", "AuditSaveEntrypointCommand", tenantId, actorId, correlationId, null);

    private static IQueryable<EntityChangeAuditRecord> Records(ApplicationDbContext db, Guid correlationId) =>
        db.Set<EntityChangeAuditRecord>().IgnoreQueryFilters().AsNoTracking().Where(row => row.CorrelationId == correlationId.ToString());

    private static string? Value(string? values, string property)
    {
        Assert.NotNull(values);
        using var snapshot = JsonDocument.Parse(values);
        return snapshot.RootElement.GetProperty(property).GetString();
    }
}
