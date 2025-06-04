using Microsoft.EntityFrameworkCore;

namespace cms.Common.Extensions;

/// <summary>
/// Extension methods for database migrations
/// </summary>
public static class MigrationExtensions
{
    /// <summary>
    /// Applies any pending migrations to the database
    /// </summary>
    /// <param name="serviceProvider">The service provider</param>
    /// <param name="logger">Optional logger</param>
    /// <returns>A task that represents the asynchronous migration operation</returns>
    public static async Task ApplyMigrationsAsync<TContext>(this IServiceProvider serviceProvider, ILogger? logger = null)
        where TContext : DbContext
    {
        using var scope = serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<TContext>();

        try
        {
            logger?.LogInformation("Applying migrations for {DbContext}", typeof(TContext).Name);
            await dbContext.Database.MigrateAsync();
            logger?.LogInformation("Migrations applied successfully for {DbContext}", typeof(TContext).Name);
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "An error occurred while applying migrations for {DbContext}", typeof(TContext).Name);

            throw;
        }
    }
}
