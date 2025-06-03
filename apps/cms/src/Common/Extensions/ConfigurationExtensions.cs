using cms.Config;

namespace cms.Common.Extensions;

public static class ConfigurationExtensions
{
    public static IServiceCollection AddAppConfiguration(this IServiceCollection services, IConfiguration configuration)
    {
        // Configure AppConfig (similar to NestJS appConfig)
        var appConfig = new AppConfig();
        configuration.GetSection("App").Bind(appConfig);
        
        // Set environment-specific defaults
        string environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Development";
        appConfig.Environment = environment;
        appConfig.IsDevelopmentEnvironment = environment == "Development";
        appConfig.IsProductionEnvironment = environment == "Production";
        appConfig.IsDocumentationEnabled = appConfig.IsDevelopmentEnvironment || 
                                          Environment.GetEnvironmentVariable("DOCUMENTATION_ENABLED")?.ToLower() == "true";
        
        services.AddSingleton(appConfig);
        
        // Configure DatabaseConfig
        var dbConfig = new DatabaseConfig();
        configuration.GetSection("Database").Bind(dbConfig);
        
        // Override with environment variable if present
        string? connectionString = Environment.GetEnvironmentVariable("DB_CONNECTION_STRING");
        if (!string.IsNullOrEmpty(connectionString))
        {
            dbConfig.ConnectionString = connectionString;
        }
        
        dbConfig.EnableSensitiveDataLogging = appConfig.IsDevelopmentEnvironment;
        dbConfig.EnableDetailedErrors = appConfig.IsDevelopmentEnvironment;
        
        services.AddSingleton(dbConfig);
        
        return services;
    }
}
