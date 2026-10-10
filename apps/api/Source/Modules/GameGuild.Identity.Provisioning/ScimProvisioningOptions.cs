namespace GameGuild.Identity.Provisioning;

/// <summary>
///     Configuration for the SCIM 2.0 provisioning surface (RFC 7643/7644).
///     The whole surface is feature-flagged and disabled by default: a deployment must
///     opt in with <c>Scim:Enabled=true</c> before any <c>/scim/v2</c> route answers.
/// </summary>
public sealed class ScimProvisioningOptions
{
    public const string SectionName = "Scim";

    /// <summary>
    ///     Master feature flag. When false every <c>/scim/v2</c> request short-circuits with 404.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    ///     Page size used when a SCIM list request omits <c>count</c>.
    /// </summary>
    public int DefaultPageSize { get; set; } = 100;

    /// <summary>
    ///     Upper bound honored for the SCIM <c>count</c> query parameter. Larger requests
    ///     are clamped to this value rather than rejected, per RFC 7644 §3.4.2.4.
    /// </summary>
    public int MaxPageSize { get; set; } = 200;

    /// <summary>
    ///     Maximum number of operations accepted by <c>POST /scim/v2/Bulk</c> (RFC 7644 §3.7).
    /// </summary>
    public int MaxBulkOperations { get; set; } = 100;

    /// <summary>
    ///     Overlap window during which a rotated provisioning token remains valid
    ///     alongside its replacement.
    /// </summary>
    public TimeSpan RotationGracePeriod { get; set; } = TimeSpan.FromMinutes(15);

    /// <summary>
    ///     Whether the Bulk endpoint is advertised and served.
    /// </summary>
    public bool BulkEnabled { get; set; } = true;

    public void Validate()
    {
        if (DefaultPageSize < 1)
        {
            throw new InvalidOperationException($"{SectionName}:DefaultPageSize must be at least 1.");
        }

        if (MaxPageSize < DefaultPageSize)
        {
            throw new InvalidOperationException($"{SectionName}:MaxPageSize must be greater than or equal to DefaultPageSize.");
        }

        if (MaxBulkOperations < 1)
        {
            throw new InvalidOperationException($"{SectionName}:MaxBulkOperations must be at least 1.");
        }

        if (RotationGracePeriod < TimeSpan.Zero)
        {
            throw new InvalidOperationException($"{SectionName}:RotationGracePeriod must not be negative.");
        }
    }
}
