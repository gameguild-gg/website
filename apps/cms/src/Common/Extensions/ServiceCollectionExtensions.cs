namespace cms.Common.Extensions;

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
        services.AddScoped<Modules.Tenant.Services.ITenantRoleService, Modules.Tenant.Services.TenantRoleService>();

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
