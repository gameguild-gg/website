using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GameGuild.Commerce.Billing;

/// <summary>
///     Resolves the webhook signing secrets that are valid right now for a provider
///     (issue #408): the configuration secret plus, when the newest recorded rotation is
///     still inside its window, the rotation's active secret and its previous secret
///     (dual-secret verification). After the window the previous secret is automatically
///     deprecated — its validity is time-derived, and the rotation row is stamped on the
///     first read past the window for observability.
/// </summary>
public interface IWebhookSigningSecretProvider
{
    /// <summary>
    ///     All signing secrets currently valid for the provider, deduplicated, in
    ///     verification-preference order (rotation secrets first). Never returns plaintext
    ///     secrets in logs; callers must treat the values as credentials.
    /// </summary>
    IReadOnlyList<string> GetValidSecrets(string providerKey);
}

/// <summary>
///     Default <see cref="IWebhookSigningSecretProvider"/>. Registered as a singleton: the
///     latest rotation is read from the database through a scope on every call (webhook
///     ingress is low-volume), and the stored secrets are decrypted with ASP.NET
///     DataProtection (purpose <c>Billing.WebhookSigningSecrets</c>).
/// </summary>
public sealed class WebhookSigningSecretProvider(
    IServiceScopeFactory scopeFactory,
    IDataProtectionProvider protectionProvider,
    IOptions<BillingConfiguration> options,
    ILogger<WebhookSigningSecretProvider> logger) : IWebhookSigningSecretProvider
{
    internal const string ProtectionPurpose = "Billing.WebhookSigningSecrets";

    private readonly IDataProtector _protector = protectionProvider.CreateProtector(ProtectionPurpose);

    /// <inheritdoc />
    public IReadOnlyList<string> GetValidSecrets(string providerKey)
    {
        if (string.IsNullOrWhiteSpace(providerKey))
        {
            throw new ArgumentException("Provider key is required.", nameof(providerKey));
        }

        var normalizedProvider = PaymentProviders.Normalize(providerKey);
        var secrets = new List<string>();

        var rotation = LoadNewestRotation(normalizedProvider);
        if (rotation is not null)
        {
            var nowUtc = SystemClock.UtcNow;
            if (rotation.IsPreviousSecretValid(nowUtc))
            {
                AddSecret(secrets, DecryptSecret(rotation.EncryptedActiveSecret, rotation.Id));
                AddSecret(secrets, DecryptSecret(rotation.EncryptedPreviousSecret, rotation.Id));
            }
            else
            {
                if (rotation.DeprecatedAtUtc is null)
                {
                    // Automatic deprecation (issue #408): stamp the row once the window has
                    // lapsed so the rotation history shows when the previous secret stopped
                    // verifying. Validity itself is time-derived and already dropped.
                    rotation.MarkDeprecated(nowUtc);
                    PersistDeprecation(rotation);
                    logger.LogInformation(
                        "Webhook secret rotation {RotationId} for provider {ProviderKey} passed its window; the previous secret is no longer accepted",
                        rotation.Id,
                        normalizedProvider);
                }

                AddSecret(secrets, DecryptSecret(rotation.EncryptedActiveSecret, rotation.Id));
            }
        }

        // The configuration secret stays valid across rotations: deployments that never
        // rotate (or fall back to configuration) keep verifying.
        AddSecret(secrets, options.Value.GetWebhookSecret(normalizedProvider));

        return secrets;
    }

    private WebhookSecretRotation? LoadNewestRotation(string normalizedProvider)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
            return context.Set<WebhookSecretRotation>()
                .AsNoTracking()
                .Where(rotation => rotation.ProviderKey == normalizedProvider)
                .OrderByDescending(rotation => rotation.RotatedAtUtc)
                .FirstOrDefault();
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // Fail closed to the configuration secret: a rotation-store outage must never
            // lock the webhook endpoint out of verification entirely.
            logger.LogError(
                exception,
                "Failed to load the webhook secret rotation state for provider {ProviderKey}; falling back to the configuration secret",
                normalizedProvider);
            return null;
        }
    }

    private void PersistDeprecation(WebhookSecretRotation rotation)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
            var tracked = context.Set<WebhookSecretRotation>()
                .FirstOrDefault(candidate => candidate.Id == rotation.Id);
            if (tracked is null)
            {
                return;
            }

            tracked.MarkDeprecated(SystemClock.UtcNow);
            context.SaveChangesAsync(CancellationToken.None).GetAwaiter().GetResult();
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // Deprecation validity is time-derived; failing to stamp the row only costs
            // observability, never verification correctness.
            logger.LogWarning(
                exception,
                "Failed to persist the automatic deprecation of webhook secret rotation {RotationId}",
                rotation.Id);
        }
    }

    private string? DecryptSecret(string encryptedSecret, Guid rotationId)
    {
        try
        {
            return _protector.Unprotect(encryptedSecret);
        }
        catch (Exception exception)
        {
            // A secret that cannot be decrypted (for example after a key-ring loss) must be
            // skipped, not surfaced: it cannot verify anything either way.
            logger.LogError(
                exception,
                "Failed to decrypt a stored webhook signing secret of rotation {RotationId}; skipping it",
                rotationId);
            return null;
        }
    }

    private static void AddSecret(List<string> secrets, string? secret)
    {
        if (string.IsNullOrWhiteSpace(secret) || secrets.Contains(secret, StringComparer.Ordinal))
        {
            return;
        }

        secrets.Add(secret);
    }
}
