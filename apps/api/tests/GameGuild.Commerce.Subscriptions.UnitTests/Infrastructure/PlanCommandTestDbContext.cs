using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace GameGuild.Commerce.Subscriptions.UnitTests.Infrastructure;

/// <summary>
///     InMemory context for plan command handler tests (issue #416): handlers that go
///     through <c>IApplicationDbContext.Set&lt;SubscriptionPlan&gt;()</c> directly.
/// </summary>
internal sealed class PlanCommandTestDbContext(DbContextOptions<PlanCommandTestDbContext> options)
    : DbContext(options), IApplicationDbContext
{
    public DbSet<SubscriptionPlan> SubscriptionPlans => Set<SubscriptionPlan>();

    Task<IDbContextTransaction> IApplicationDbContext.BeginTransactionAsync(CancellationToken cancellationToken)
        => throw new NotSupportedException("InMemory test context does not support transactions");

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new SubscriptionPlanConfiguration());
        base.OnModelCreating(modelBuilder);
    }

    public static PlanCommandTestDbContext Create()
    {
        var options = new DbContextOptionsBuilder<PlanCommandTestDbContext>()
            .UseInMemoryDatabase($"plan-tests-{Guid.NewGuid()}")
            .Options;
        return new PlanCommandTestDbContext(options);
    }
}
