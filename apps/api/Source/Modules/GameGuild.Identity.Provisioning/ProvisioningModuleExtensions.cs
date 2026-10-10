using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace GameGuild.Identity.Provisioning;

/// <summary>
///     Module registration for the SCIM 2.0 provisioning surface. The surface is
///     feature-flagged (<c>Scim:Enabled</c>, default false): registration only wires the
///     services and the authentication scheme; the feature-gate middleware hides the
///     routes on deployments that have not opted in.
/// </summary>
public static class ProvisioningModuleExtensions
{
    public static IServiceCollection AddScimProvisioningModule(this IServiceCollection services, IConfiguration configuration)
    {
        // Bind through the IOptions pipeline (the codebase-standard pattern): every
        // consumer — including ScimFeatureGateMiddleware — resolves
        // IOptions<ScimProvisioningOptions>, so the module MUST register the options
        // via AddOptions/Bind. Registering a pre-bound instance with AddSingleton only
        // publishes the concrete type and leaves IOptions<T> at its defaults
        // (Enabled=false), which 404s the whole /scim surface regardless of config.
        services
            .AddOptions<ScimProvisioningOptions>()
            .Bind(configuration.GetSection(ScimProvisioningOptions.SectionName))
            .Validate(
                static options => options.DefaultPageSize >= 1,
                $"{ScimProvisioningOptions.SectionName}:DefaultPageSize must be at least 1.")
            .Validate(
                static options => options.MaxPageSize >= options.DefaultPageSize,
                $"{ScimProvisioningOptions.SectionName}:MaxPageSize must be greater than or equal to DefaultPageSize.")
            .Validate(
                static options => options.MaxBulkOperations >= 1,
                $"{ScimProvisioningOptions.SectionName}:MaxBulkOperations must be at least 1.")
            .Validate(
                static options => options.RotationGracePeriod >= TimeSpan.Zero,
                $"{ScimProvisioningOptions.SectionName}:RotationGracePeriod must not be negative.")
            .ValidateOnStart();

        // Repositories
        services.AddScoped<IScimProvisioningTokenRepository, ScimProvisioningTokenRepository>();
        services.AddScoped<IScimUserMappingRepository, ScimUserMappingRepository>();
        services.AddScoped<IScimGroupMappingRepository, ScimGroupMappingRepository>();

        // Provisioning context (token tenant resolution, fail closed)
        services.AddHttpContextAccessor();
        services.TryAddScoped<IScimProvisioningContext, HttpScimProvisioningContext>();

        // Services
        services.AddScoped<ScimUserService>();
        services.AddScoped<ScimGroupService>();
        services.AddScoped<IScimBulkProcessor, ScimBulkProcessor>();

        // Audit sink: hosts may register a durable implementation (the compliance audit
        // module registers the central one); fall back to the null object so the module
        // works standalone.
        services.TryAddScoped<IScimProvisioningAuditSink>(static _ => NullScimProvisioningAuditSink.Instance);

        // Authentication scheme for provisioning tokens. The parameterless overload only
        // appends the scheme (the host's default scheme is untouched), and registering a
        // scheme exposes no endpoint; the controllers opt into this scheme explicitly.
        services
            .AddAuthentication()
            .AddScimProvisioningAuthentication();

        return services;
    }
}
