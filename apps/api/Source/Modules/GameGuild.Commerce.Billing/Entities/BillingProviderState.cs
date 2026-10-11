using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GameGuild.Commerce.Billing;

/// <summary>
///     Persisted runtime management state for an external billing provider
///     (issue #397 provider-management API). One row per provider key that an
///     administrator has explicitly toggled; the absence of a row means the
///     provider keeps its default <c>enabled</c> disposition (availability is
///     governed by configuration, the runtime override can only disable).
///     Platform-global by design: no tenant is ever assigned to a provider row.
/// </summary>
[Table("BillingProviderStates")]
public class BillingProviderState : EntityBase
{
    /// <summary>
    ///     Parameterless constructor for EF Core.
    /// </summary>
    private BillingProviderState()
    {
    }

    /// <summary>
    ///     Creates an explicit management state row for a provider.
    /// </summary>
    public BillingProviderState(string providerKey, bool isEnabled, Guid changedByUserId)
    {
        if (string.IsNullOrWhiteSpace(providerKey))
        {
            throw new ArgumentException("Provider key is required.", nameof(providerKey));
        }

        ProviderKey = PaymentProviders.Normalize(providerKey);
        IsEnabled = isEnabled;
        LastChangedByUserId = changedByUserId == Guid.Empty ? null : changedByUserId;
    }

    /// <summary>
    ///     Provider key from <see cref="PaymentProviders"/> (stripe, paypal, ...).
    /// </summary>
    [Required]
    [MaxLength(50)]
    public string ProviderKey { get; private set; } = string.Empty;

    /// <summary>
    ///     Whether the provider is enabled at runtime. Only rows created by an
    ///     explicit admin action exist, so this records the override decision.
    /// </summary>
    public bool IsEnabled { get; private set; }

    /// <summary>
    ///     Actor that last changed the enabled state (null for system actions).
    /// </summary>
    public Guid? LastChangedByUserId { get; private set; }

    /// <summary>
    ///     Applies a new enabled state.
    /// </summary>
    public void SetEnabled(bool isEnabled, Guid changedByUserId)
    {
        IsEnabled = isEnabled;
        LastChangedByUserId = changedByUserId == Guid.Empty ? null : changedByUserId;
        Touch();
    }
}
