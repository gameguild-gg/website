using Microsoft.Extensions.Logging;

namespace GameGuild.Identity.Authorization;

/// <summary>
///     Service for managing delegated administration scopes
/// </summary>
public class DelegatedAdminService(
    IDelegatedAdminScopeRepository repository,
    ILogger<DelegatedAdminService> logger
) : IDelegatedAdminService
{
    private readonly ILogger<DelegatedAdminService> _logger =
        logger ?? throw new ArgumentNullException(nameof(logger));

    private readonly IDelegatedAdminScopeRepository _repository =
        repository ?? throw new ArgumentNullException(nameof(repository));

    public async Task<DelegatedAdminScope> GrantDelegatedAdminAsync(
        DelegatedAdminScope scope,
        CancellationToken cancellationToken = default
    ) => await _repository.CreateAsync(scope, cancellationToken);

    public async Task<bool> RevokeDelegatedAdminAsync(
        Guid scopeId,
        CancellationToken cancellationToken = default
    )
    {
        // Unknown scope ids must not be reported as a successful revocation.
        var existing = await _repository.GetByIdAsync(scopeId, cancellationToken).ConfigureAwait(false);
        if (existing == null)
        {
            return false;
        }

        await _repository.DeleteAsync(scopeId, cancellationToken).ConfigureAwait(false);
        return true;
    }

    public async Task<DelegatedAdminScope?> GetScopeByIdAsync(
        Guid scopeId,
        CancellationToken cancellationToken = default
    ) => await _repository.GetByIdAsync(scopeId, cancellationToken);

    public async Task<List<DelegatedAdminScope>> GetAdminScopesAsync(
        Guid adminUserId,
        Guid? tenantId,
        CancellationToken cancellationToken = default
    ) => await _repository.GetByAdminUserAsync(adminUserId, tenantId, cancellationToken);

    public async Task<List<Guid>> GetManagedUsersAsync(
        Guid adminUserId,
        Guid? tenantId,
        CancellationToken cancellationToken = default
    )
    {
        var scopes = await _repository.GetByAdminUserAsync(adminUserId, tenantId, cancellationToken).ConfigureAwait(false);

        var managedUsers = new List<Guid>();
        foreach (var scope in scopes)
        {
            // Only currently-valid scopes (active and inside their validity window)
            // delegate administrative authority over managed users.
            if (!scope.IsValid()) continue;
            if (string.IsNullOrEmpty(scope.AllowedUserIds)) continue;
            try
            {
                var userIds = System.Text.Json.JsonSerializer.Deserialize<List<Guid>>(scope.AllowedUserIds);
                if (userIds != null) managedUsers.AddRange(userIds);
            }
            catch (System.Text.Json.JsonException ex)
            {
                _logger.LogWarning(ex, "Failed to parse AllowedUserIds JSON for scope {ScopeId}", scope.Id);
            }
        }

        return managedUsers.Distinct().ToList();
    }

    public async Task<List<string>> GetManagedResourceTypesAsync(
        Guid adminUserId,
        Guid? tenantId,
        CancellationToken cancellationToken = default
    )
    {
        var scopes = await _repository.GetByAdminUserAsync(adminUserId, tenantId, cancellationToken).ConfigureAwait(false);

        var resourceTypes = new List<string>();
        foreach (var scope in scopes)
        {
            // Only currently-valid scopes (active and inside their validity window)
            // delegate administrative authority over managed resource types.
            if (!scope.IsValid()) continue;
            if (string.IsNullOrEmpty(scope.AllowedResourceTypes)) continue;
            try
            {
                var types = System.Text.Json.JsonSerializer.Deserialize<List<string>>(scope.AllowedResourceTypes);
                if (types != null) resourceTypes.AddRange(types);
            }
            catch (System.Text.Json.JsonException ex)
            {
                _logger.LogWarning(ex, "Failed to parse AllowedResourceTypes JSON for scope {ScopeId}", scope.Id);
            }
        }

        return resourceTypes.Distinct().ToList();
    }

    public async Task<bool> CanManageUserAsync(
        Guid adminUserId,
        Guid targetUserId,
        Guid? tenantId,
        CancellationToken cancellationToken = default
    )
    {
        var managedUsers = await GetManagedUsersAsync(adminUserId, tenantId, cancellationToken).ConfigureAwait(false);
        return managedUsers.Contains(targetUserId);
    }

    public async Task<bool> CanManageResourceAsync(
        Guid adminUserId,
        string resourceType,
        Guid? tenantId,
        CancellationToken cancellationToken = default
    )
    {
        var managedResourceTypes = await GetManagedResourceTypesAsync(adminUserId, tenantId, cancellationToken).ConfigureAwait(false);
        return managedResourceTypes.Contains(resourceType);
    }
}
