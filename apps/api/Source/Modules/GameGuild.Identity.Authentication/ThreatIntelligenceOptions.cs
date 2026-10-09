namespace GameGuild.Identity.Authentication;

/// <summary>
///     How a threat-intelligence match influences the login risk analysis.
/// </summary>
public enum ThreatIntelligenceEnforcementMode
{
    /// <summary>
    ///     Log-only: matches are recorded as detected anomalies and security events but do
    ///     not raise the risk score. Safe default while operators validate feed quality.
    /// </summary>
    Observation = 0,

    /// <summary>
    ///     Matches raise the risk score and flow into the existing step-up machinery.
    /// </summary>
    Enforce = 1
}

/// <summary>
///     Configuration for credential-stuffing threat intelligence.
/// </summary>
public sealed class ThreatIntelligenceOptions
{
    public const string SectionName = "ThreatIntelligence";

    /// <summary>Provider used as the <c>ThreatIntelligence:Provider</c> value for the disabled no-op provider.</summary>
    public const string NoneProvider = "None";

    /// <summary>Provider used as the <c>ThreatIntelligence:Provider</c> value for the local-file feed provider.</summary>
    public const string LocalFileProvider = "LocalFile";

    /// <summary>
    ///     Threat-intelligence provider. <c>LocalFile</c> (default) reads an operator-supplied
    ///     local JSON feed with zero external calls; <c>None</c> disables threat intelligence
    ///     entirely. Any other value fails startup. External feed providers are deliberately
    ///     deferred behind this switch (no third-party service is called by this module).
    /// </summary>
    public string Provider { get; set; } = LocalFileProvider;

    /// <summary>
    ///     Enforcement mode. Defaults to <see cref="ThreatIntelligenceEnforcementMode.Observation" />
    ///     (log-only) to guard against false positives from a mis-curated feed.
    /// </summary>
    public ThreatIntelligenceEnforcementMode EnforcementMode { get; set; } = ThreatIntelligenceEnforcementMode.Observation;

    /// <summary>Risk-score bump applied in <see cref="ThreatIntelligenceEnforcementMode.Enforce" /> when the client IP matches the blocklist.</summary>
    public int MaliciousIpRiskScore { get; set; } = 60;

    /// <summary>Risk-score bump applied in <see cref="ThreatIntelligenceEnforcementMode.Enforce" /> when the candidate password matches the breach corpus.</summary>
    public int BreachedPasswordRiskScore { get; set; } = 60;

    /// <summary>Settings for the <c>LocalFile</c> provider.</summary>
    public LocalFileThreatIntelligenceOptions LocalFile { get; set; } = new();
}

/// <summary>
///     Settings for <see cref="LocalFileThreatIntelligenceProvider" />.
/// </summary>
public sealed class LocalFileThreatIntelligenceOptions
{
    /// <summary>
    ///     Path to the operator-supplied feed JSON (see docs/authentication-configuration.md
    ///     for the documented format). Relative paths resolve against the content root.
    ///     A missing or unreadable feed fails open: no matches, plus a security event.
    /// </summary>
    public string FilePath { get; set; } = "config/threat-intelligence.json";

    /// <summary>Minimum time between feed reload attempts.</summary>
    public TimeSpan ReloadInterval { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    ///     Additionally reload whenever the feed file's last-write time changes, even before
    ///     <see cref="ReloadInterval" /> elapses.
    /// </summary>
    public bool ReloadOnFileChange { get; set; } = true;
}
