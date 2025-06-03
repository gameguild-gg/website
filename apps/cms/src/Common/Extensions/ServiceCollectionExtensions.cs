namespace cms.Common.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddUserModule(this IServiceCollection services)
    {
        // Register User module services
        services.AddScoped<cms.Modules.User.Services.IUserService, cms.Modules.User.Services.UserService>();
        
        return services;
    }

    public static IServiceCollection AddCommonServices(this IServiceCollection services)
    {
        // Add logging
        services.AddLogging();
        
        // Add memory cache
        services.AddMemoryCache();
        
        return services;
    }
}
