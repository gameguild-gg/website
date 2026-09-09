using GameGuild.Identity.Users;
using GameGuild.Learning.Lti;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace GameGuild.Learning.Lti.Tests;

/// <summary>
/// In-memory context applying the LTI module model configuration (mirrors TestAssessmentDbContext pattern).
/// </summary>
public sealed class TestLtiDbContext(DbContextOptions<TestLtiDbContext> options) : DbContext(options), IApplicationDbContext
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        new LtiModelConfiguration().Configure(modelBuilder);

        // Minimal User mapping: strip cross-module navigations so the Users module
        // entity can be queried without pulling its whole configuration graph.
        modelBuilder.Entity<User>(b =>
        {
            b.ToTable("Users");
            b.HasKey(u => u.Id);
            b.Ignore(u => u.Profile);
            b.Ignore(u => u.Metadata);
            b.Ignore(u => u.Preferences);
            b.Ignore(u => u.Notifications);
            b.Ignore(u => u.TenantMemberships);
        });
    }

    public Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException("Transactions are not required for LTI tests.");
    }
}
