using GameGuild.Identity.Authorization.Configuration;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace GameGuild.Identity.Authorization;

/// <summary>
///     Authorization module providing comprehensive permission management, ABAC policies,
///     JIT elevation, permission delegation, Separation of Duties, access reviews,
///     and delegated administration capabilities.
/// </summary>
public class AuthorizationModule : ModuleBase
{
    /// <inheritdoc />
    public override string Name => "Authorization";

    /// <inheritdoc />
    public override int Order => 15; // After Authentication (10), before business modules (100+)

    /// <inheritdoc />
    public override IReadOnlyList<Type> Dependencies => []; // No module dependencies, uses Permissions via project reference

    /// <inheritdoc />
    public override IServiceCollection ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        // Configuration options
        services.AddAuthorizationOptions(configuration);

        // Core authorization services (policy store, ACL, caching)
        services.AddAuthorizationApplication(enableCaching: true);

        // Repository implementations
        services.AddAuthorizationRepositories();

        // Presentation layer (handlers, policy provider, tenant context)
        services.AddAuthorizationPresentation();

        // Rule-based authorization
        services.AddRuleBasedAuthorization();

        // Permission services (audit, templates)
        services.AddPermissionServices();

        // Advanced permission services (JIT, delegation, SoD, access reviews, delegated admin)
        services.AddAdvancedPermissionServices();

        // Permission evaluation engine (issue #358): throttle, webhooks, external sync,
        // restoration, compliance reporting, evaluation extensions host point.
        services.AddPermissionEngineServices();

        return services;
    }

    /// <inheritdoc />
    public override IEndpointRouteBuilder MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        // Controllers are automatically discovered via [ApiController] attribute
        // No additional minimal API endpoints needed for this module
        return endpoints;
    }

    /// <summary>
    ///     Configure EF Core model for Authorization entities
    /// </summary>
    public static void ConfigureAuthorizationModel(ModelBuilder modelBuilder)
    {
        // Apply all entity configurations from Configuration folder
        // These include proper TenantId value converters for all entities
        modelBuilder.ApplyConfiguration(new AccessControlListEntryConfiguration());
        modelBuilder.ApplyConfiguration(new PolicyDefinitionEntityConfiguration());
        modelBuilder.ApplyConfiguration(new TenantSecurityVersionConfiguration());
        modelBuilder.ApplyConfiguration(new ResourceUserPermissionConfiguration());
        modelBuilder.ApplyConfiguration(new ResourceInvitationConfiguration());
        modelBuilder.ApplyConfiguration(new JitElevationRequestConfiguration());
        modelBuilder.ApplyConfiguration(new PermissionDelegationConfiguration());
        modelBuilder.ApplyConfiguration(new SoDRuleConfiguration());
        modelBuilder.ApplyConfiguration(new SoDViolationConfiguration());
        modelBuilder.ApplyConfiguration(new AccessReviewCampaignConfiguration());
        modelBuilder.ApplyConfiguration(new AccessReviewItemConfiguration());
        modelBuilder.ApplyConfiguration(new DelegatedAdminScopeConfiguration());
        modelBuilder.ApplyConfiguration(new AbacPolicyConfiguration());
        modelBuilder.ApplyConfiguration(new ConditionalPolicyConfiguration());
        modelBuilder.ApplyConfiguration(new DataMaskingRuleConfiguration());
        modelBuilder.ApplyConfiguration(new TenantPermissionConfiguration());
        modelBuilder.ApplyConfiguration(new PermissionTemplateConfiguration());
        modelBuilder.ApplyConfiguration(new PermissionAuditLogConfiguration());

        // RBAC: Dynamic roles with deny permission support
        modelBuilder.ApplyConfiguration(new DynamicRoleConfiguration());
        modelBuilder.ApplyConfiguration(new DynamicRoleAssignmentConfiguration());

        // Permission evaluation log (issues #359/#358): durable compliance reporting source
        modelBuilder.ApplyConfiguration(new PermissionEvaluationLogEntryConfiguration());
    }
}
