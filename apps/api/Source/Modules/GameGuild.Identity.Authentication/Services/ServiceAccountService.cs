using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Microsoft.Extensions.Logging;

namespace GameGuild.Identity.Authentication;

/// <summary>
///     Service for managing service accounts and client credentials authentication.
/// </summary>
public sealed class ServiceAccountService : IServiceAccountService
{
    private readonly IServiceAccountRepository _repository;
    private readonly IRefreshTokenHasher _hasher;
    private readonly ILogger<ServiceAccountService> _logger;
    private const int ClientSecretLength = 64;
    private const int LockThreshold = 10;

    public ServiceAccountService(
        IServiceAccountRepository repository,
        IRefreshTokenHasher hasher,
        ILogger<ServiceAccountService> logger)
    {
        _repository = repository;
        _hasher = hasher;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<(ServiceAccount Account, string ClientSecret)> CreateServiceAccountAsync(
        string name,
        string? description,
        Guid? tenantId,
        string scopes,
        string createdBy,
        string? allowedIpAddresses = null,
        DateTime? expiresAt = null,
        CancellationToken cancellationToken = default)
    {
        // Generate unique client ID
        var clientId = $"svc_{Guid.NewGuid():N}";

        // Ensure client ID is unique
        while (await _repository.ClientIdExistsAsync(clientId, cancellationToken))
        {
            clientId = $"svc_{Guid.NewGuid():N}";
        }

        // Generate secure client secret
        var clientSecret = GenerateSecureSecret();
        var clientSecretHash = _hasher.HashToken(clientSecret);

        var serviceAccount = new ServiceAccount
        {
            Id = Guid.NewGuid(),
            ClientId = clientId,
            ClientSecretHash = clientSecretHash,
            Name = name,
            Description = description,
            TenantId = tenantId,
            Scopes = scopes,
            IsActive = true,
            ExpiresAt = expiresAt, CreatedBy = createdBy, AllowedIpAddresses = allowedIpAddresses
        };

        var created = await _repository.CreateAsync(serviceAccount, cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "Created service account {ServiceAccountId} with client ID {ClientId} for tenant {TenantId}",
            created.Id, created.ClientId, tenantId);

        return (created, clientSecret);
    }

    /// <inheritdoc />
    public async Task<ServiceAccount?> AuthenticateAsync(
        string clientId,
        string clientSecret,
        string? ipAddress,
        CancellationToken cancellationToken = default)
    {
        var serviceAccount = await _repository.GetByClientIdAsync(clientId, cancellationToken).ConfigureAwait(false);

        if (serviceAccount == null)
        {
            _logger.LogWarning("Authentication failed: service account with client ID {ClientId} not found", clientId);
            return null;
        }

        if (!serviceAccount.CanAuthenticate)
        {
            _logger.LogWarning(
                "Authentication failed: service account {ServiceAccountId} cannot authenticate (IsActive={IsActive}, IsLocked={IsLocked}, Expired={Expired})",
                serviceAccount.Id, serviceAccount.IsActive, serviceAccount.IsLocked,
                serviceAccount.ExpiresAt.HasValue && serviceAccount.ExpiresAt <= SystemClock.UtcNow);
            return null;
        }

        // Validate IP address if restrictions are set
        if (!string.IsNullOrEmpty(serviceAccount.AllowedIpAddresses) && !string.IsNullOrEmpty(ipAddress))
        {
            if (!IsIpAllowed(ipAddress, serviceAccount.AllowedIpAddresses))
            {
                _logger.LogWarning(
                    "Authentication failed: IP {IpAddress} not in allowed list for service account {ServiceAccountId}",
                    ipAddress, serviceAccount.Id);
                serviceAccount.RecordFailedAuthentication(LockThreshold);
                await _repository.UpdateAsync(serviceAccount, cancellationToken).ConfigureAwait(false);
                return null;
            }
        }

        // Verify client secret
        if (!_hasher.VerifyToken(clientSecret, serviceAccount.ClientSecretHash))
        {
            _logger.LogWarning(
                "Authentication failed: invalid client secret for service account {ServiceAccountId}",
                serviceAccount.Id);
            serviceAccount.RecordFailedAuthentication(LockThreshold);
            await _repository.UpdateAsync(serviceAccount, cancellationToken).ConfigureAwait(false);
            return null;
        }

        // Record successful authentication
        serviceAccount.RecordSuccessfulAuthentication(ipAddress);
        await _repository.UpdateAsync(serviceAccount, cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "Service account {ServiceAccountId} authenticated successfully from IP {IpAddress}",
            serviceAccount.Id, ipAddress);

        return serviceAccount;
    }

    /// <inheritdoc />
    public async Task<ServiceAccount?> AuthenticateWithCertificateAsync(
        string clientId,
        X509Certificate2 clientCertificate,
        string? ipAddress,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(clientCertificate);

        var serviceAccount = await _repository.GetByClientIdAsync(clientId, cancellationToken).ConfigureAwait(false);

        if (serviceAccount == null)
        {
            _logger.LogWarning("Authentication failed: service account with client ID {ClientId} not found", clientId);
            return null;
        }

        if (!serviceAccount.CanAuthenticate)
        {
            _logger.LogWarning(
                "Authentication failed: service account {ServiceAccountId} cannot authenticate (IsActive={IsActive}, IsLocked={IsLocked}, Expired={Expired})",
                serviceAccount.Id, serviceAccount.IsActive, serviceAccount.IsLocked,
                serviceAccount.ExpiresAt.HasValue && serviceAccount.ExpiresAt <= SystemClock.UtcNow);
            return null;
        }

        // Validate IP address if restrictions are set
        if (!string.IsNullOrEmpty(serviceAccount.AllowedIpAddresses) && !string.IsNullOrEmpty(ipAddress))
        {
            if (!IsIpAllowed(ipAddress, serviceAccount.AllowedIpAddresses))
            {
                _logger.LogWarning(
                    "Authentication failed: IP {IpAddress} not in allowed list for service account {ServiceAccountId}",
                    ipAddress, serviceAccount.Id);
                serviceAccount.RecordFailedAuthentication(LockThreshold);
                await _repository.UpdateAsync(serviceAccount, cancellationToken).ConfigureAwait(false);
                return null;
            }
        }

        // SECURITY: fail closed when no certificate is bound, or when the presented
        // certificate matches neither the bound thumbprint nor the bound SPKI key pin.
        var thumbprint = ClientCertificateAuthenticationUtilities.GetNormalizedThumbprint(clientCertificate);
        var spkiSha256 = ClientCertificateAuthenticationUtilities.ComputeSpkiSha256Hex(clientCertificate);

        var thumbprintMatches = !string.IsNullOrWhiteSpace(serviceAccount.CertificateThumbprint)
                                && string.Equals(serviceAccount.CertificateThumbprint, thumbprint, StringComparison.OrdinalIgnoreCase);
        var spkiMatches = !string.IsNullOrWhiteSpace(serviceAccount.CertificateSpkiSha256)
                          && string.Equals(serviceAccount.CertificateSpkiSha256, spkiSha256, StringComparison.OrdinalIgnoreCase);

        if (!thumbprintMatches && !spkiMatches)
        {
            _logger.LogWarning(
                "Authentication failed: client certificate {Thumbprint} does not match the certificate bound to service account {ServiceAccountId}",
                thumbprint, serviceAccount.Id);
            serviceAccount.RecordFailedAuthentication(LockThreshold);
            await _repository.UpdateAsync(serviceAccount, cancellationToken).ConfigureAwait(false);
            return null;
        }

        // Record successful authentication
        serviceAccount.RecordSuccessfulAuthentication(ipAddress);
        await _repository.UpdateAsync(serviceAccount, cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "Service account {ServiceAccountId} authenticated successfully with client certificate {Thumbprint} from IP {IpAddress}",
            serviceAccount.Id, thumbprint, ipAddress);

        return serviceAccount;
    }

    /// <inheritdoc />
    public async Task<string> RotateSecretAsync(Guid serviceAccountId, CancellationToken cancellationToken = default)
    {
        var serviceAccount = await _repository.GetByIdAsync(serviceAccountId, cancellationToken)
                             ?? throw new InvalidOperationException($"Service account {serviceAccountId} not found");

        var newSecret = GenerateSecureSecret();
        var newSecretHash = _hasher.HashToken(newSecret);

        serviceAccount.RotateSecret(newSecretHash);
        await _repository.UpdateAsync(serviceAccount, cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "Rotated secret for service account {ServiceAccountId} (rotation #{RotationCount})",
            serviceAccountId, serviceAccount.SecretRotationCount);

        return newSecret;
    }

    /// <inheritdoc />
    public async Task UnlockAsync(Guid serviceAccountId, CancellationToken cancellationToken = default)
    {
        var serviceAccount = await _repository.GetByIdAsync(serviceAccountId, cancellationToken)
                             ?? throw new InvalidOperationException($"Service account {serviceAccountId} not found");

        serviceAccount.Unlock();
        await _repository.UpdateAsync(serviceAccount, cancellationToken).ConfigureAwait(false);

        _logger.LogInformation("Unlocked service account {ServiceAccountId}", serviceAccountId);
    }

    /// <inheritdoc />
    public async Task LockAsync(Guid serviceAccountId, string reason, CancellationToken cancellationToken = default)
    {
        var serviceAccount = await _repository.GetByIdAsync(serviceAccountId, cancellationToken)
                             ?? throw new InvalidOperationException($"Service account {serviceAccountId} not found");

        serviceAccount.Lock(reason);
        await _repository.UpdateAsync(serviceAccount, cancellationToken).ConfigureAwait(false);

        _logger.LogInformation("Locked service account {ServiceAccountId} with reason: {Reason}", serviceAccountId, reason);
    }

    /// <inheritdoc />
    public async Task<PagedAuditResult> GetAuditLogAsync(
        Guid serviceAccountId,
        int skip = 0,
        int take = 20,
        CancellationToken cancellationToken = default)
    {
        var serviceAccount = await _repository.GetByIdAsync(serviceAccountId, cancellationToken).ConfigureAwait(false);
        if (serviceAccount is null)
        {
            return new PagedAuditResult([], 0);
        }

        var entries = BuildDerivedAuditEntries(serviceAccount)
            .OrderByDescending(entry => entry.Timestamp)
            .ThenByDescending(entry => entry.Action, StringComparer.Ordinal)
            .ToList();

        var safeSkip = Math.Max(0, skip);
        var safeTake = Math.Clamp(take, 1, 100);

        return new PagedAuditResult(entries.Skip(safeSkip).Take(safeTake).ToList(), entries.Count);
    }

    /// <inheritdoc />
    public async Task DeactivateAsync(Guid serviceAccountId, CancellationToken cancellationToken = default)
    {
        var serviceAccount = await _repository.GetByIdAsync(serviceAccountId, cancellationToken)
                             ?? throw new InvalidOperationException($"Service account {serviceAccountId} not found");

        serviceAccount.IsActive = false;
        serviceAccount.UpdatedAt = SystemClock.UtcNow;
        await _repository.UpdateAsync(serviceAccount, cancellationToken).ConfigureAwait(false);

        _logger.LogInformation("Deactivated service account {ServiceAccountId}", serviceAccountId);
    }

    /// <inheritdoc />
    public async Task ReactivateAsync(Guid serviceAccountId, CancellationToken cancellationToken = default)
    {
        var serviceAccount = await _repository.GetByIdAsync(serviceAccountId, cancellationToken)
                             ?? throw new InvalidOperationException($"Service account {serviceAccountId} not found");

        serviceAccount.IsActive = true;
        serviceAccount.UpdatedAt = SystemClock.UtcNow;
        await _repository.UpdateAsync(serviceAccount, cancellationToken).ConfigureAwait(false);

        _logger.LogInformation("Reactivated service account {ServiceAccountId}", serviceAccountId);
    }

    /// <inheritdoc />
    public async Task UpdateScopesAsync(Guid serviceAccountId, string scopes, CancellationToken cancellationToken = default)
    {
        var serviceAccount = await _repository.GetByIdAsync(serviceAccountId, cancellationToken)
                             ?? throw new InvalidOperationException($"Service account {serviceAccountId} not found");

        serviceAccount.Scopes = scopes;
        serviceAccount.UpdatedAt = SystemClock.UtcNow;
        await _repository.UpdateAsync(serviceAccount, cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "Updated scopes for service account {ServiceAccountId} to: {Scopes}",
            serviceAccountId, scopes);
    }

    /// <inheritdoc />
    public Task<ServiceAccount?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => _repository.GetByIdAsync(id, cancellationToken);

    /// <inheritdoc />
    public Task<IReadOnlyList<ServiceAccount>> GetByTenantAsync(Guid tenantId, CancellationToken cancellationToken = default)
        => _repository.GetByTenantIdAsync(tenantId, cancellationToken);

    /// <inheritdoc />
    public Task<IReadOnlyList<ServiceAccount>> GetAllAsync(CancellationToken cancellationToken = default)
        => _repository.GetAllAsync(cancellationToken);

    private static string GenerateSecureSecret()
    {
        var bytes = RandomNumberGenerator.GetBytes(ClientSecretLength);
        return Convert.ToBase64String(bytes).Replace("+", "-").Replace("/", "_").TrimEnd('=');
    }

    private static bool IsIpAllowed(string ipAddress, string allowedIpAddresses)
    {
        var allowed = allowedIpAddresses.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (var allowedIp in allowed)
        {
            // Simple exact match for now (CIDR support could be added)
            if (allowedIp.Equals(ipAddress, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    private static IEnumerable<ServiceAccountAuditEntry> BuildDerivedAuditEntries(ServiceAccount account)
    {
        yield return new ServiceAccountAuditEntry
        {
            Id = StableAuditId(account.Id, "created", account.CreatedAt),
            Timestamp = account.CreatedAt,
            Action = "Created",
            PerformedBy = account.CreatedBy,
            Details = $"Service account '{account.Name}' created with scopes: {account.Scopes}"
        };

        if (account.SecretRotatedAt.HasValue)
        {
            yield return new ServiceAccountAuditEntry
            {
                Id = StableAuditId(account.Id, "secret-rotated", account.SecretRotatedAt.Value),
                Timestamp = account.SecretRotatedAt.Value,
                Action = "SecretRotated",
                Details = $"Secret rotation count: {account.SecretRotationCount}"
            };
        }

        if (account.LastAuthenticatedAt.HasValue)
        {
            yield return new ServiceAccountAuditEntry
            {
                Id = StableAuditId(account.Id, "authenticated", account.LastAuthenticatedAt.Value),
                Timestamp = account.LastAuthenticatedAt.Value,
                Action = "Authenticated",
                IpAddress = account.LastAuthenticatedFromIp,
                Details = $"Successful authentication count: {account.AuthenticationCount}"
            };
        }

        if (account.FailedAuthenticationAttempts > 0)
        {
            yield return new ServiceAccountAuditEntry
            {
                Id = StableAuditId(account.Id, "failed-auth", account.UpdatedAt),
                Timestamp = account.UpdatedAt,
                Action = "AuthenticationFailed",
                Details = $"Failed attempts since last success: {account.FailedAuthenticationAttempts}"
            };
        }

        if (account.LockedAt.HasValue)
        {
            yield return new ServiceAccountAuditEntry
            {
                Id = StableAuditId(account.Id, "locked", account.LockedAt.Value),
                Timestamp = account.LockedAt.Value,
                Action = "Locked",
                Details = "Service account locked"
            };
        }

        yield return new ServiceAccountAuditEntry
        {
            Id = StableAuditId(account.Id, account.IsActive ? "active-state" : "deactivated", account.UpdatedAt),
            Timestamp = account.UpdatedAt,
            Action = account.IsActive ? "Updated" : "Deactivated",
            Details = account.IsActive
                ? $"Current active state; scopes: {account.Scopes}"
                : "Service account is inactive"
        };
    }

    private static Guid StableAuditId(Guid serviceAccountId, string action, DateTime timestamp)
    {
        var input = $"{serviceAccountId:N}:{action}:{timestamp.ToUniversalTime():O}";
        var hash = SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(input));
        return new Guid(hash[..16]);
    }
}
