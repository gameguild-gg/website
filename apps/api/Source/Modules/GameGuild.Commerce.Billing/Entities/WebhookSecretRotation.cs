using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GameGuild.Commerce.Billing;

/// <summary>
///     One webhook signing-secret rotation (issue #408): records that a provider's webhook
///     endpoint moved from a previous signing secret to a new active secret, and the window
///     during which the previous secret stays accepted (dual-secret rotation window) so
///     in-flight deliveries signed with either secret verify. After the window the previous
///     secret is automatically deprecated — <see cref="DeprecatedAtUtc"/> is stamped on the
///     first read past the window — and only the active secret verifies. Secrets are stored
///     encrypted at rest (ASP.NET DataProtection); the row never contains plaintext.
///     Platform-global by design: webhook endpoints are not tenant-scoped.
/// </summary>
[Table("WebhookSecretRotations")]
public class WebhookSecretRotation : EntityBase
{
    /// <summary>Parameterless constructor for EF Core.</summary>
    private WebhookSecretRotation()
    {
    }

    /// <summary>Creates a rotation record for a provider.</summary>
    /// <param name="providerKey">Provider key from <see cref="PaymentProviders"/> (stripe, paypal, ...).</param>
    /// <param name="encryptedPreviousSecret">DataProtection-encrypted previous signing secret.</param>
    /// <param name="encryptedActiveSecret">DataProtection-encrypted new active signing secret.</param>
    /// <param name="rotatedAtUtc">UTC instant the rotation was recorded.</param>
    /// <param name="windowEndsAtUtc">UTC instant after which the previous secret is rejected.</param>
    /// <param name="rotatedByUserId">Acting administrator; null for system actions.</param>
    public WebhookSecretRotation(
        string providerKey,
        string encryptedPreviousSecret,
        string encryptedActiveSecret,
        DateTime rotatedAtUtc,
        DateTime windowEndsAtUtc,
        Guid? rotatedByUserId)
    {
        if (string.IsNullOrWhiteSpace(providerKey))
        {
            throw new ArgumentException("Provider key is required.", nameof(providerKey));
        }

        if (string.IsNullOrWhiteSpace(encryptedPreviousSecret))
        {
            throw new ArgumentException("The encrypted previous secret is required.", nameof(encryptedPreviousSecret));
        }

        if (string.IsNullOrWhiteSpace(encryptedActiveSecret))
        {
            throw new ArgumentException("The encrypted active secret is required.", nameof(encryptedActiveSecret));
        }

        if (windowEndsAtUtc <= rotatedAtUtc)
        {
            throw new ArgumentException("The rotation window must end after the rotation instant.", nameof(windowEndsAtUtc));
        }

        ProviderKey = PaymentProviders.Normalize(providerKey);
        EncryptedPreviousSecret = encryptedPreviousSecret;
        EncryptedActiveSecret = encryptedActiveSecret;
        RotatedAtUtc = rotatedAtUtc;
        WindowEndsAtUtc = windowEndsAtUtc;
        RotatedByUserId = rotatedByUserId == Guid.Empty ? null : rotatedByUserId;
    }

    /// <summary>Provider key from <see cref="PaymentProviders"/> (stripe, paypal, ...).</summary>
    [Required]
    [MaxLength(50)]
    public string ProviderKey { get; private set; } = string.Empty;

    /// <summary>DataProtection-encrypted previous signing secret (valid only inside the window).</summary>
    [Required]
    public string EncryptedPreviousSecret { get; private set; } = string.Empty;

    /// <summary>DataProtection-encrypted active signing secret (valid from the rotation instant on).</summary>
    [Required]
    public string EncryptedActiveSecret { get; private set; } = string.Empty;

    /// <summary>UTC instant the rotation was recorded.</summary>
    public DateTime RotatedAtUtc { get; private set; }

    /// <summary>UTC instant after which the previous secret is automatically rejected.</summary>
    public DateTime WindowEndsAtUtc { get; private set; }

    /// <summary>UTC instant the previous secret was actually dropped; null until the window lapses.</summary>
    public DateTime? DeprecatedAtUtc { get; private set; }

    /// <summary>Acting administrator that recorded the rotation; null for system actions.</summary>
    public Guid? RotatedByUserId { get; private set; }

    /// <summary>Whether the previous secret still verifies at the given instant.</summary>
    public bool IsPreviousSecretValid(DateTime nowUtc) => DeprecatedAtUtc is null && nowUtc <= WindowEndsAtUtc;

    /// <summary>
    ///     Stamps the automatic deprecation once the rotation window has lapsed. Idempotent.
    /// </summary>
    public void MarkDeprecated(DateTime nowUtc)
    {
        if (DeprecatedAtUtc is null && nowUtc > WindowEndsAtUtc)
        {
            DeprecatedAtUtc = nowUtc;
        }
    }
}
