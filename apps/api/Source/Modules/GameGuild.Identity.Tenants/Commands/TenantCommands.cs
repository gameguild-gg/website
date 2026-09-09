using GameGuild.CQRS;

namespace GameGuild.Identity.Tenants;

// Tenant Validation Commands
public sealed record ValidateTenantCommand(string Name, string Slug, string AdminEmail) : IRequest<TenantValidationResponse>;

// Tenant Metadata Commands
public sealed record UpdateTenantMetadataCommand(Guid TenantId, UpdateTenantMetadataRequest Request) : ICommand;

public sealed record ReplaceTenantMetadataCommand(Guid TenantId, ReplaceTenantMetadataRequest Request) : ICommand;

public sealed record UpdateTenantCustomFieldsCommand(Guid TenantId, UpdateTenantCustomFieldsRequest Request) : ICommand;

public sealed record UpdateTenantTagsCommand(Guid TenantId, UpdateTenantTagsRequest Request) : ICommand;

public sealed record ReplaceTenantTagsCommand(Guid TenantId, ReplaceTenantTagsRequest Request) : ICommand;

// Tenant Settings Commands
public sealed record UpdateTenantSettingsCommand(Guid TenantId, UpdateTenantSettingsRequest Request) : ICommand;

public sealed record ReplaceTenantSettingsCommand(Guid TenantId, ReplaceTenantSettingsRequest Request) : ICommand;

public sealed record UpdateTenantFeatureFlagsCommand(Guid TenantId, UpdateTenantFeatureFlagsRequest Request) : ICommand;

public sealed record UpdateTenantSystemLimitsCommand(Guid TenantId, UpdateTenantSystemLimitsRequest Request) : ICommand;

public sealed record UpdateTenantIntegrationSettingsCommand(Guid TenantId, UpdateTenantIntegrationSettingsRequest Request) : ICommand;
