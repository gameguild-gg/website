using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace GameGuild.Identity.Tenants;

/// <summary>
///     Represents a domain (top-level or subdomain) associated with a tenant for automatic user grouping.
///     Each tenant can have multiple domains with one designated as the main/principal domain.
///     Domains can be top-level (e.g., "estate.com") or subdomains (e.g., "admin.estate.com").
/// </summary>
[Table("TenantDomains")]
[Index(nameof(TopLevelDomain), nameof(Subdomain), IsUnique = true)]
[Index(nameof(TenantId), nameof(IsMainDomain))]
public class TenantDomain : EntityBase, ITenantable
{
    private string? _subdomain;

    private string _topLevelDomain = string.Empty;

    /// <summary>
    ///     Default constructor
    /// </summary>
    public TenantDomain() { }

    /// <summary>
    ///     Constructor for partial initialization
    /// </summary>
    /// <param name="partial">Partial tenant domain data</param>
    public TenantDomain(object partial) : base(partial) { }

    /// <summary>
    ///     ID of the tenant this domain belongs to
    /// </summary>
    [Required]
    public new Guid TenantId { get; set; }

    /// <summary>
    ///     The top-level domain name (e.g., "estate.com", "realestate.com")
    /// </summary>
    [Required]
    [MaxLength(255)]
    public string TopLevelDomain { get => _topLevelDomain; set => _topLevelDomain = value.ToLowerInvariant(); }

    /// <summary>
    ///     Optional subdomain prefix (e.g., "admin" for "admin.estate.com", "api" for "api.estate.com")
    /// </summary>
    [MaxLength(100)]
    public string? Subdomain { get => _subdomain; set => _subdomain = value?.ToLowerInvariant(); }

    /// <summary>
    ///     Whether this is the main/principal domain for the tenant (only one per tenant)
    /// </summary>
    public bool IsMainDomain { get; set; }

    /// <summary>
    ///     Whether this is a secondary domain for the tenant (can have multiple per tenant)
    /// </summary>
    public bool IsSecondaryDomain { get; set; }

    /// <summary>
    ///     ID of the user group that users with this domain should be automatically added to
    /// </summary>
    public Guid? UserGroupId { get; set; }

    /// <summary>
    ///     Gets the full domain string including subdomain if present
    /// </summary>
    [NotMapped]
    public string FullDomain { get => string.IsNullOrEmpty(Subdomain) ? TopLevelDomain : $"{Subdomain}.{TopLevelDomain}"; }

    /// <summary>
    ///     Navigation property to the tenant
    /// </summary>
    public Tenant? Tenant { get; set; }

    /// <summary>
    ///     Checks if a given email address matches this domain
    /// </summary>
    /// <param name="email">The email address to check</param>
    /// <returns>True if the email's domain matches this tenant domain</returns>
    public bool MatchesEmail(string email)
    {
        if (string.IsNullOrEmpty(email) || !email.Contains('@'))
        {
            return false;
        }

        var emailDomain = email.Split('@')[1].ToLowerInvariant();

        return emailDomain == FullDomain;
    }

    /// <summary>
    ///     Set as the main domain for the tenant
    /// </summary>
    public void SetAsMainDomain()
    {
        IsMainDomain = true;
        IsSecondaryDomain = false;
        Touch();
    }

    /// <summary>
    ///     Set as a secondary domain for the tenant
    /// </summary>
    public void SetAsSecondaryDomain()
    {
        IsMainDomain = false;
        IsSecondaryDomain = true;
        Touch();
    }
}
