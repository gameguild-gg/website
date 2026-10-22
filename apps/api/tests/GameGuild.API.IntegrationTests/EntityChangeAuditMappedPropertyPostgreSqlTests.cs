using System.Security.Cryptography;
using System.Text.Json;
using GameGuild.API.Database;
using GameGuild.API.IntegrationTests.Infrastructure;
using GameGuild.Compliance.Audit;
using GameGuild.Identity.Tenants;
using GameGuild.Identity.Users;
using Microsoft.EntityFrameworkCore;

namespace GameGuild.API.IntegrationTests;

[Collection(ApiPostgreSqlCollection.Name)]
public sealed class EntityChangeAuditMappedPropertyPostgreSqlTests(ApiPostgreSqlFixture fixture)
{
    [Theory]
    [InlineData(EntityState.Added)]
    [InlineData(EntityState.Modified)]
    [InlineData(EntityState.Deleted)]
    public async Task TenantMemberChangesPersistExactlyOneAuditWithMappedTenantAndActor(EntityState change)
    {
        var accessor = new UseCaseOperationContextAccessor();
        await using var db = CreateContext(accessor);
        var (user, tenant) = await SeedOwnerAsync(db);
        var member = NewMember(user, tenant);
        // This hidden base property is not the required Guid mapped by TenantMember.
        ((EntityBase)member).TenantId = Guid.NewGuid();
        db.Add(member);
        if (change != EntityState.Added)
        {
            await db.SaveChangesAsync();
        }

        var actorId = Guid.NewGuid();
        var correlationId = Guid.NewGuid();
        using (accessor.Begin(Operation(tenant.Id, actorId, correlationId)))
        {
            if (change == EntityState.Modified)
            {
                member.Role = "Reviewer";
            }
            else if (change == EntityState.Deleted)
            {
                db.Remove(member);
            }

            Assert.Equal(1, await db.SaveChangesAsync());
        }

        await using var observed = CreateContext();
        var record = Assert.Single(await Records(observed, correlationId).ToListAsync());
        Assert.Equal(nameof(TenantMember), record.EntityName);
        Assert.Equal(member.Id.ToString(), record.KeyId);
        Assert.Equal(change.ToString(), record.ChangeKind);
        Assert.Equal<Guid?>(tenant.Id, record.TenantId);
        Assert.Equal<Guid?>(actorId, record.ActorId);
        Assert.Equal(correlationId.ToString(), record.CorrelationId);

        if (change == EntityState.Added)
        {
            Assert.Null(record.BeforeValues);
            AssertMemberSnapshot(record.AfterValues, tenant.Id, "Member");
        }
        else if (change == EntityState.Modified)
        {
            Assert.Equal("Member", ReadString(record.BeforeValues, nameof(TenantMember.Role)));
            Assert.Equal("Reviewer", ReadString(record.AfterValues, nameof(TenantMember.Role)));
            using var snapshot = JsonDocument.Parse(record.AfterValues!);
            Assert.False(snapshot.RootElement.TryGetProperty(nameof(TenantMember.UserId), out _));
            Assert.False(snapshot.RootElement.TryGetProperty(nameof(TenantMember.TenantId), out _));
        }
        else
        {
            AssertMemberSnapshot(record.BeforeValues, tenant.Id, "Member");
            Assert.Null(record.AfterValues);
        }

        var persisted = await observed.Set<TenantMember>().IgnoreQueryFilters().SingleOrDefaultAsync(row => row.Id == member.Id);
        if (change == EntityState.Deleted)
        {
            Assert.Null(persisted);
        }
        else
        {
            Assert.NotNull(persisted);
            Assert.Equal(tenant.Id, persisted.TenantId);
            Assert.Equal(change == EntityState.Modified ? "Reviewer" : "Member", persisted.Role);
        }
    }

    [Theory]
    [InlineData(EntityState.Added)]
    [InlineData(EntityState.Modified)]
    [InlineData(EntityState.Deleted)]
    public async Task SensitiveUserValuesStayRedactedInPersistedLifecycleSnapshots(EntityState change)
    {
        var accessor = new UseCaseOperationContextAccessor();
        await using var db = CreateContext(accessor);
        var original = Convert.ToHexString(RandomNumberGenerator.GetBytes(24));
        var replacement = Convert.ToHexString(RandomNumberGenerator.GetBytes(24));
        var user = NewUser();
        user.PasswordHash = original;
        db.Add(user);
        if (change != EntityState.Added)
        {
            await db.SaveChangesAsync();
        }

        var correlationId = Guid.NewGuid();
        using (accessor.Begin(Operation(Guid.NewGuid(), Guid.NewGuid(), correlationId)))
        {
            if (change == EntityState.Modified)
            {
                user.PasswordHash = replacement;
            }
            else if (change == EntityState.Deleted)
            {
                db.Remove(user);
            }

            Assert.Equal(1, await db.SaveChangesAsync());
        }

        await using var observed = CreateContext();
        var record = Assert.Single(await Records(observed, correlationId).ToListAsync());
        Assert.Equal(nameof(User), record.EntityName);
        Assert.Equal(change.ToString(), record.ChangeKind);
        Assert.Equal(user.Id.ToString(), record.KeyId);
        if (change != EntityState.Added)
        {
            Assert.Equal("[REDACTED]", ReadString(record.BeforeValues, nameof(User.PasswordHash)));
        }
        if (change != EntityState.Deleted)
        {
            Assert.Equal("[REDACTED]", ReadString(record.AfterValues, nameof(User.PasswordHash)));
        }
        Assert.DoesNotContain(original, (record.BeforeValues ?? string.Empty) + record.AfterValues);
        Assert.DoesNotContain(replacement, (record.BeforeValues ?? string.Empty) + record.AfterValues);
        var savedUser = await observed.Set<User>().IgnoreQueryFilters().SingleOrDefaultAsync(row => row.Id == user.Id);
        if (change == EntityState.Deleted)
        {
            Assert.Null(savedUser);
        }
        else
        {
            Assert.NotNull(savedUser);
            Assert.Equal(change == EntityState.Modified ? replacement : original, savedUser.PasswordHash);
        }
    }

    [Fact]
    public async Task BulkMembershipChangesRetainEachEntityTenantAndDoNotRecurseIntoAuditRows()
    {
        var accessor = new UseCaseOperationContextAccessor();
        await using var db = CreateContext(accessor);
        var (firstUser, firstTenant) = await SeedOwnerAsync(db);
        var (secondUser, secondTenant) = await SeedOwnerAsync(db);
        var members = new[] { NewMember(firstUser, firstTenant), NewMember(secondUser, secondTenant) };
        db.AddRange(members);
        var correlationId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        using (accessor.Begin(Operation(firstTenant.Id, actorId, correlationId)))
        {
            Assert.Equal(2, await db.SaveChangesAsync());
            Assert.Equal(0, await db.SaveChangesAsync());
        }

        await using var observed = CreateContext();
        var records = await Records(observed, correlationId).ToListAsync();
        Assert.Equal(2, records.Count);
        foreach (var member in members)
        {
            var record = Assert.Single(records, row => row.KeyId == member.Id.ToString());
            Assert.Equal(nameof(TenantMember), record.EntityName);
            Assert.Equal<Guid?>(member.TenantId, record.TenantId);
            Assert.Equal<Guid?>(actorId, record.ActorId);
            AssertMemberSnapshot(record.AfterValues, member.TenantId, "Member");
        }
    }

    [Fact]
    public async Task RolledBackMembershipAndItsAuditBothDisappearFromPostgreSql()
    {
        var accessor = new UseCaseOperationContextAccessor();
        await using var db = CreateContext(accessor);
        var (user, tenant) = await SeedOwnerAsync(db);
        var member = NewMember(user, tenant);
        var correlationId = Guid.NewGuid();
        await using (var transaction = await db.Database.BeginTransactionAsync())
        {
            using var operation = accessor.Begin(Operation(tenant.Id, Guid.NewGuid(), correlationId));
            db.Add(member);
            Assert.Equal(1, await db.SaveChangesAsync());
            Assert.Single(await Records(db, correlationId).ToListAsync());
            await transaction.RollbackAsync();
        }

        await using var observed = CreateContext();
        Assert.False(await observed.Set<TenantMember>().IgnoreQueryFilters().AnyAsync(row => row.Id == member.Id));
        Assert.Empty(await Records(observed, correlationId).ToListAsync());
    }

    [Fact]
    public async Task FailedMembershipPersistenceDoesNotCommitOtherChangesOrTheirAudit()
    {
        var accessor = new UseCaseOperationContextAccessor();
        await using var db = CreateContext(accessor);
        var (user, tenant) = await SeedOwnerAsync(db);
        var originalName = user.Name;
        var member = NewMember(user, tenant);
        member.TenantId = Guid.NewGuid();
        var correlationId = Guid.NewGuid();
        using (accessor.Begin(Operation(tenant.Id, Guid.NewGuid(), correlationId)))
        {
            user.Name = "Uncommitted name";
            db.Add(member);
            await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        }

        await using var observed = CreateContext();
        Assert.Equal(originalName, (await observed.Set<User>().SingleAsync(row => row.Id == user.Id)).Name);
        Assert.False(await observed.Set<TenantMember>().IgnoreQueryFilters().AnyAsync(row => row.Id == member.Id));
        Assert.Empty(await Records(observed, correlationId).ToListAsync());
    }

    private ApplicationDbContext CreateContext(UseCaseOperationContextAccessor? accessor = null)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(fixture.ConnectionString).Options;
        var context = new ApplicationDbContext(options, null, accessor ?? new UseCaseOperationContextAccessor());
        Assert.Equal("Npgsql.EntityFrameworkCore.PostgreSQL", context.Database.ProviderName);
        return context;
    }

    private static async Task<(User User, Tenant Tenant)> SeedOwnerAsync(ApplicationDbContext db)
    {
        var user = NewUser();
        var marker = Guid.NewGuid().ToString("N");
        var tenant = new Tenant { Id = Guid.NewGuid(), Name = "Audit " + marker, Slug = "audit-" + marker,
            IsActive = true, AdminEmail = "audit-" + marker + "@example.test" };
        db.AddRange(user, tenant);
        await db.SaveChangesAsync();
        return (user, tenant);
    }

    private static User NewUser()
    {
        var marker = Guid.NewGuid().ToString("N");
        return new User { Id = Guid.NewGuid(), Email = "audit-" + marker + "@example.test",
            Name = "Audit " + marker, Username = "audit_" + marker };
    }

    private static TenantMember NewMember(User user, Tenant tenant) => new()
    {
        Id = Guid.NewGuid(), TenantId = tenant.Id, UserId = user.Id, Role = "Member", IsActive = true
    };

    private static UseCaseOperationContext Operation(Guid tenantId, Guid actorId, Guid correlationId) =>
        new("audit-mapped-property", "AuditMappedPropertyCommand", tenantId, actorId, correlationId, null);

    private static IQueryable<EntityChangeAuditRecord> Records(ApplicationDbContext db, Guid correlationId) =>
        db.Set<EntityChangeAuditRecord>().IgnoreQueryFilters().AsNoTracking()
            .Where(row => row.CorrelationId == correlationId.ToString());

    private static void AssertMemberSnapshot(string? values, Guid tenantId, string role)
    {
        Assert.NotNull(values);
        using var snapshot = JsonDocument.Parse(values);
        Assert.Equal(tenantId, snapshot.RootElement.GetProperty(nameof(TenantMember.TenantId)).GetGuid());
        Assert.Equal(role, snapshot.RootElement.GetProperty(nameof(TenantMember.Role)).GetString());
    }

    private static string? ReadString(string? values, string property)
    {
        Assert.NotNull(values);
        using var snapshot = JsonDocument.Parse(values);
        return snapshot.RootElement.GetProperty(property).GetString();
    }
}
