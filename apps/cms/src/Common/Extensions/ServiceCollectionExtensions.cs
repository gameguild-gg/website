using GameGuild.Common.Services;

namespace GameGuild.Common.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddUserModule(this IServiceCollection services)
    {
        // Register User module services
        services.AddScoped<Modules.User.Services.IUserService, Modules.User.Services.UserService>();

        return services;
    }

    public static IServiceCollection AddTenantModule(this IServiceCollection services)
    {
        // Register Tenant module services
        services.AddScoped<Modules.Tenant.Services.ITenantService, Modules.Tenant.Services.TenantService>();

        return services;
    }

    public static IServiceCollection AddUserProfileModule(this IServiceCollection services)
    {
        // Register UserProfile module services
        services.AddScoped<Modules.UserProfile.Services.IUserProfileService, Modules.UserProfile.Services.UserProfileService>();

        return services;
    }

    public static IServiceCollection AddCommonServices(this IServiceCollection services)
    {
        // Add logging
        services.AddLogging();

        // Add memory cache
        services.AddMemoryCache();

        // Add permission service for three-layer permission system
        services.AddScoped<IPermissionService, PermissionService>();

        return services;
    }
}
