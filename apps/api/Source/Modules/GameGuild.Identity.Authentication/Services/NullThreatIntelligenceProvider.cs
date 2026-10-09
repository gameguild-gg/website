namespace GameGuild.Identity.Authentication;

/// <summary>
///     Disabled no-op provider registered when <c>ThreatIntelligence:Provider</c> is
///     <c>None</c>. Every check reports an available provider with no match, so callers
///     need no special-casing and sign-in risk scoring is untouched.
/// </summary>
public sealed class NullThreatIntelligenceProvider : IThreatIntelligenceProvider
{
    public static readonly NullThreatIntelligenceProvider Instance = new();

    public string ProviderName => ThreatIntelligenceOptions.NoneProvider;

    public Task<ThreatIntelligenceIpResult> CheckIpAddressAsync(string? ipAddress, CancellationToken cancellationToken = default)
        => Task.FromResult(new ThreatIntelligenceIpResult(IsMatch: false));

    public Task<ThreatIntelligencePasswordResult> CheckPasswordHashAsync(string? passwordSha256Hex, CancellationToken cancellationToken = default)
        => Task.FromResult(new ThreatIntelligencePasswordResult(IsMatch: false));
}
