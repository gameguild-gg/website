using FluentAssertions;
using GameGuild.Commerce.Payments;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace GameGuild.Commerce.Payments.UnitTests.Services;

/// <summary>
///     Single-default invariant (issue #414): the partial unique index
/// <c>ix_payment_methods_single_default</c> atomically rejects two live defaults per
/// (tenant, user). Verified on SQLite in-memory, which enforces partial unique indexes the
/// same way PostgreSQL does for this filter shape.
/// </summary>
public class PaymentMethodSingleDefaultInvariantTests : IDisposable
{
    private readonly SqliteConnection _connection = new("DataSource=:memory:");

    private PaymentMethodsSqliteTestDbContext CreateContext()
    {
        _connection.Open();
        var options = new DbContextOptionsBuilder<PaymentMethodsSqliteTestDbContext>()
            .UseSqlite(_connection)
            .Options;
        var context = new PaymentMethodsSqliteTestDbContext(options);
        context.Database.EnsureCreated();
        return context;
    }

    public void Dispose() => _connection.Dispose();

    [Fact]
    public void SingleDefaultIndex_ShouldRejectSecondLiveDefaultPerTenantAndUser()
    {
        using var context = CreateContext();
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var first = NewDefault(tenantId, userId, "pm_default_1");
        context.Set<PaymentMethod>().Add(first);
        context.SaveChanges();

        var second = NewDefault(tenantId, userId, "pm_default_2");
        context.Set<PaymentMethod>().Add(second);

        var act = () => context.SaveChanges();

        // SQLite surfaces partial-unique violations as "UNIQUE constraint failed: <columns>"
        // (Npgsql would name ix_payment_methods_single_default); assert the columns so the
        // assertion holds on the in-memory SQLite provider either way.
        act.Should().Throw<DbUpdateException>()
            .WithInnerException<SqliteException>()
            .Which.Message.Should().Contain("UNIQUE constraint failed")
            .And.Contain("payment_methods.TenantId")
            .And.Contain("payment_methods.UserId");
    }

    [Fact]
    public void SingleDefaultIndex_ShouldAllowDefaultsForDifferentUsers()
    {
        using var context = CreateContext();
        var tenantId = Guid.NewGuid();

        context.Set<PaymentMethod>().Add(NewDefault(tenantId, Guid.NewGuid(), "pm_user_a"));
        context.Set<PaymentMethod>().Add(NewDefault(tenantId, Guid.NewGuid(), "pm_user_b"));
        context.SaveChanges();

        context.Set<PaymentMethod>().Count(pm => pm.DeletedAt == null && pm.IsDefault).Should().Be(2);
    }

    [Fact]
    public void SingleDefaultIndex_ShouldAllowADefaultPerTenantAcrossTenants()
    {
        using var context = CreateContext();
        var userId = Guid.NewGuid();

        context.Set<PaymentMethod>().Add(NewDefault(Guid.NewGuid(), userId, "pm_tenant_a"));
        context.Set<PaymentMethod>().Add(NewDefault(Guid.NewGuid(), userId, "pm_tenant_b"));
        context.SaveChanges();

        context.Set<PaymentMethod>().Count(pm => pm.DeletedAt == null && pm.IsDefault).Should().Be(2);
    }

    [Fact]
    public void SingleDefaultIndex_ShouldIgnoreSoftDeletedRows()
    {
        using var context = CreateContext();
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var original = NewDefault(tenantId, userId, "pm_soft_deleted");
        original.Version = 1;
        context.Set<PaymentMethod>().Add(original);
        context.SaveChanges();

        original.SoftDelete();
        context.SaveChanges();

        // The deleted default no longer occupies the slot; promoting another method succeeds.
        context.Set<PaymentMethod>().Add(NewDefault(tenantId, userId, "pm_new_default"));
        context.SaveChanges();

        context.Set<PaymentMethod>().Count(pm => pm.DeletedAt == null && pm.IsDefault).Should().Be(1);
    }

    [Fact]
    public void TenantGatewayUniqueIndex_ShouldRejectDuplicateLiveTokens()
    {
        using var context = CreateContext();
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        context.Set<PaymentMethod>().Add(NewDefault(tenantId, userId, "pm_dup"));
        context.SaveChanges();
        context.Set<PaymentMethod>().Add(NewNonDefault(tenantId, userId, "pm_dup"));

        var act = () => context.SaveChanges();

        // SQLite surfaces unique violations as "UNIQUE constraint failed: <columns>" without
        // the index name; the discriminator column proves the tenant+gateway index fired.
        act.Should().Throw<DbUpdateException>()
            .WithInnerException<SqliteException>()
            .Which.Message.Should().Contain("UNIQUE constraint failed")
            .And.Contain("payment_methods.GatewayPaymentMethodId");
    }

    [Fact]
    public async Task ConcurrentSetDefaultOperations_ShouldLeaveExactlyOneDefault()
    {
        // Both operations run through separate contexts against the shared database. The
        // service demotes inside a transaction before promoting; interleaved writers either
        // serialize correctly or the unique index rejects the second default. End state: one.
        using var context = CreateContext();
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var methodA = NewNonDefault(tenantId, userId, "pm_race_a");
        var methodB = NewNonDefault(tenantId, userId, "pm_race_b");
        methodA.Version = 1;
        methodB.Version = 1;
        context.Set<PaymentMethod>().AddRange(methodA, methodB);
        await context.SaveChangesAsync();

        var promoteTasks = new[]
        {
            PromoteAsync(tenantId, userId, methodA.Id),
            PromoteAsync(tenantId, userId, methodB.Id)
        };
        await Task.WhenAll(promoteTasks);

        using var verifyContext = CreateContextForVerification();
        var liveDefaults = verifyContext.Set<PaymentMethod>()
            .Count(pm => pm.DeletedAt == null && pm.IsDefault && pm.UserId == userId);
        liveDefaults.Should().Be(1, "the partial unique index is the atomic backstop for the single-default invariant");
    }

    private async Task PromoteAsync(Guid tenantId, Guid userId, Guid paymentMethodId)
    {
        using var context = CreateContextForVerification();
        var repository = new PaymentMethodRepository(context,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<PaymentMethodRepository>.Instance);
        var method = await context.Set<PaymentMethod>().SingleAsync(pm => pm.Id == paymentMethodId);
        var previousDefaults = await context.Set<PaymentMethod>()
            .Where(pm => pm.DeletedAt == null && pm.IsDefault && pm.TenantId == tenantId && pm.UserId == userId && pm.Id != paymentMethodId)
            .ToListAsync();

        await using var transaction = await context.BeginTransactionAsync();
        foreach (var previous in previousDefaults)
        {
            previous.ClearDefault();
            await repository.UpdateAsync(previous);
        }

        method.MarkAsDefault();
        try
        {
            await repository.UpdateAsync(method);
            await transaction.CommitAsync();
        }
        catch (DbUpdateException)
        {
            await transaction.RollbackAsync();
        }
    }

    private PaymentMethodsSqliteTestDbContext CreateContextForVerification()
    {
        var options = new DbContextOptionsBuilder<PaymentMethodsSqliteTestDbContext>()
            .UseSqlite(_connection)
            .Options;
        return new PaymentMethodsSqliteTestDbContext(options);
    }

    private static PaymentMethod NewDefault(Guid tenantId, Guid userId, string gatewayId)
    {
        var paymentMethod = new PaymentMethod(tenantId, userId, "stripe", gatewayId, PaymentMethodType.Card, isDefault: true);
        paymentMethod.UpdateGatewayMetadata(PaymentMethodType.Card, "visa", "4242", 12, 2031, $"fp_{gatewayId}");
        return paymentMethod;
    }

    private static PaymentMethod NewNonDefault(Guid tenantId, Guid userId, string gatewayId)
    {
        var paymentMethod = new PaymentMethod(tenantId, userId, "stripe", gatewayId, PaymentMethodType.Card, isDefault: false);
        paymentMethod.UpdateGatewayMetadata(PaymentMethodType.Card, "visa", "4242", 12, 2031, $"fp_{gatewayId}");
        return paymentMethod;
    }
}

/// <summary>
///     Minimal SQLite context containing only the payment-method registry model so the
///     partial unique indexes can be exercised without unrelated provider-specific mappings.
/// </summary>
internal sealed class PaymentMethodsSqliteTestDbContext(DbContextOptions<PaymentMethodsSqliteTestDbContext> options)
    : DbContext(options), IApplicationDbContext
{
    public Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
    {
        return Database.BeginTransactionAsync(cancellationToken);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new PaymentMethodConfiguration());
        base.OnModelCreating(modelBuilder);
    }
}
