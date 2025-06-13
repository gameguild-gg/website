using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using DotNetEnv;

namespace GameGuild.Data;

public class ApplicationDbContextFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
{
    public ApplicationDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<ApplicationDbContext>();

        // Load environment variables from .env file
        Env.Load();

        // Get connection string from environment variable
        string connectionString = Environment.GetEnvironmentVariable("DB_CONNECTION_STRING")
                                  ?? "Data Source=app.db"; // Fallback to SQLite for design-time

        // Configure database provider based on connection string
        if (connectionString.Contains("Data Source=") || connectionString.Contains("DataSource="))
        {
            optionsBuilder.UseSqlite(connectionString);
        }
        else
        {
            optionsBuilder.UseNpgsql(connectionString);
        }

        return new ApplicationDbContext(optionsBuilder.Options);
    }
}
