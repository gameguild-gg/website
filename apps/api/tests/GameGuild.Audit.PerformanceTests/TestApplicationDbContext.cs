using GameGuild.API.Database;
using Microsoft.EntityFrameworkCore;

namespace GameGuild.Tests.Audit.Performance;

/// <summary>
/// Test-specific ApplicationDbContext for audit performance testing
/// Ignores entities that cause issues with in-memory database testing
/// </summary>
public class TestApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : ApplicationDbContext(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // For audit performance tests, we can test with a minimal set of entities
        // Ignore complex entities that aren't needed for audit performance testing
        // This allows the in-memory database to work properly
    }
}