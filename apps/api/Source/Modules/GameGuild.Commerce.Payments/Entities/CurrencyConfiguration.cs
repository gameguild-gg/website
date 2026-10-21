using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace GameGuild.Commerce.Payments;

/// <summary>
///     Per-tenant currency enablement and display preferences (issue #412 spec A).
///     One row per (tenant, ISO 4217 code). Fail-closed default: a currency with no row —
///     or a disabled row — is not available for that tenant; pricing and checkout must
///     treat it as unsupported instead of silently falling back to another currency.
///     Exactly one enabled row per tenant may carry <see cref="IsBaseCurrency"/> (enforced
///     by a filtered unique index); the base currency is the tenant's calculation and
///     reporting currency and can never be disabled while it holds that role.
/// </summary>
[Table("currency_configurations")]
[Index(nameof(TenantId))]
[Index(nameof(CurrencyCode))]
[Index(nameof(TenantId), nameof(CurrencyCode), IsUnique = true)]
[Index(nameof(IsEnabled))]
public class CurrencyConfiguration : EntityBase
{
    /// <summary>Default constructor</summary>
    public CurrencyConfiguration() { }

    /// <summary>Constructor for partial initialization</summary>
    /// <param name="partial">Partial configuration data</param>
    public CurrencyConfiguration(object partial) : base(partial) { }

    /// <summary>
    ///     Tenant the configuration belongs to (inherited tenant scope); required — currency
    ///     enablement is per-tenant and handlers refuse to operate without a tenant context.
    /// </summary>
    [Required]
    public override Guid? TenantId { get; set; }

    /// <summary>ISO 4217 alphabetic code, always upper-case; validated against <see cref="Iso4217CurrencyCatalog"/>.</summary>
    [Required]
    [MaxLength(3)]
    public string CurrencyCode { get; set; } = string.Empty;

    /// <summary>Whether the currency is enabled for the tenant. Absence of a row means disabled.</summary>
    public bool IsEnabled { get; set; }

    /// <summary>Whether this currency is the tenant's base (calculation/reporting) currency.</summary>
    public bool IsBaseCurrency { get; set; }

    /// <summary>Optional display-preference override for the catalog symbol (e.g. a localized symbol).</summary>
    [MaxLength(16)]
    public string? DisplaySymbol { get; set; }

    /// <summary>Optional display preference: relative ordering in selector UIs (lower first).</summary>
    public int DisplayOrder { get; set; }
}
