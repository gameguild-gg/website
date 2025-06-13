using Microsoft.EntityFrameworkCore.Infrastructure;

namespace GameGuild.Tests.Helpers
{
    /// <summary>
    /// Extension methods for database operations in tests
    /// </summary>
    public static class DatabaseExtensions
    {
        /// <summary>
        /// Determines whether the database is a SQLite database
        /// </summary>
        public static bool IsSqlite(this DatabaseFacade database)
        {
            return database.ProviderName?.Contains("Sqlite") ?? false;
        }
        
        /// <summary>
        /// Gets the provider name of the database
        /// </summary>
        public static string GetProviderName(this DatabaseFacade database)
        {
            return database.ProviderName ?? string.Empty;
        }
    }
}
