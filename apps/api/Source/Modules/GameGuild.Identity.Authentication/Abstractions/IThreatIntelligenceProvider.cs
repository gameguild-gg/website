namespace GameGuild.Identity.Authentication;

/// <summary>
///     Result of checking a client IP address against a threat-intelligence feed.
///     A match is a defense-in-depth signal only; it must never be the sole reason
///     an authentication attempt is rejected outright.
/// </summary>
public sealed record ThreatIntelligenceIpResult(
    bool IsMatch,
    string? MatchedCidr = null,
    bool ProviderAvailable = true,
    string? ProviderError = null);

/// <summary>
///     Result of checking a candidate password's SHA-256 hex digest against a
///     breached-password corpus. The digest itself must never be echoed back in
///     results, logs, or audit metadata.
/// </summary>
public sealed record ThreatIntelligencePasswordResult(
    bool IsMatch,
    bool ProviderAvailable = true,
    string? ProviderError = null);

/// <summary>
///     Read-only access to credential-stuffing threat intelligence. Implementations
///     MUST fail open: an unavailable or unreadable feed reports
///     <c>ProviderAvailable = false</c> with no match instead of throwing, so a
///     defense-in-depth signal can never lock users out.
/// </summary>
public interface IThreatIntelligenceProvider
{
    /// <summary>Stable provider identifier (for example <c>LocalFile</c> or <c>None</c>).</summary>
    string ProviderName { get; }

    /// <summary>
    ///     Checks whether the client IP address appears in the malicious IP blocklist.
    ///     IPv4-mapped IPv6 addresses are normalized to IPv4 before matching.
    /// </summary>
    /// <param name="ipAddress">Client IP address; unparseable or missing values never match.</param>
    /// <param name="cancellationToken">Ignored by the local-file provider; kept for provider symmetry.</param>
    Task<ThreatIntelligenceIpResult> CheckIpAddressAsync(string? ipAddress, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Checks whether the SHA-256 hex digest of the candidate password appears in the
    ///     breached-password corpus. Comparison is case-insensitive.
    /// </summary>
    /// <param name="passwordSha256Hex">SHA-256 hex digest of the submitted password; missing values never match.</param>
    /// <param name="cancellationToken">Ignored by the local-file provider; kept for provider symmetry.</param>
    Task<ThreatIntelligencePasswordResult> CheckPasswordHashAsync(string? passwordSha256Hex, CancellationToken cancellationToken = default);
}
