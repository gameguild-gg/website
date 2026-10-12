namespace GameGuild.Configuration.PresentationLayer.Authorization;

/// <summary>
///     Owner-override precedence modes for the effective-permission resolution engine
///     (issue #372). The mode decides how a resource owner's override interacts with
///     explicit DENY entries. Every unknown value resolves to <see cref="Off"/>
///     (fail-closed: an override only ever widens access, so an unparseable
///     configuration can never silently grant it).
/// </summary>
public enum OwnerOverrideMode
{
    /// <summary>Placeholder for unparsed/unknown values; never stored, never effective.</summary>
    Unknown = 0,

    /// <summary>
    ///     Owner overrides are disabled entirely: ownership contributes nothing beyond
    ///     the regular resource-grant layer (pre-#372 behavior).
    /// </summary>
    Off = 1,

    /// <summary>
    ///     Default. An owner's grant-check short-circuits to allow for owner-scoped
    ///     operations unless an explicit deny exists anywhere — deny-wins over owner
    ///     (fail-closed).
    /// </summary>
    AllowUnlessDenied = 2,

    /// <summary>
    ///     Owner grants are non-deniable: an explicit deny cannot remove a permission
    ///     minted by the owner override. Only the static system-account wildcard shares
    ///     this property otherwise. Opt-in; document the trade-off before enabling.
    /// </summary>
    AlwaysAllow = 3
}

/// <summary>
///     Owner-override configuration (issue #372) under the
///     <c>Authorization:OwnerOverride</c> section.
/// </summary>
/// <remarks>
///     <para>
///         Example:
///         <code>
/// /// "Authorization": { "OwnerOverride": { "Mode": "AllowUnlessDenied" } }
///         </code>
///     </para>
///     <para>
///         <b>Fail-closed:</b> an unknown, empty or null <see cref="Mode"/> is treated as
///         <see cref="OwnerOverrideMode.Off"/> at evaluation time (the override is a
///         permission-widening feature, so unparseable configuration disables it) and a
///         warning is logged by the resolver. Startup is not crashed on purpose: a
///         configuration typo must not take the host down when the safe behavior is
///         simply "no owner override".
///     </para>
/// </remarks>
public sealed class OwnerOverrideOptions
{
    /// <summary>The configuration sub-section name (<c>Authorization:OwnerOverride</c>).</summary>
    public const string SectionName = "OwnerOverride";

    /// <summary>The default mode (<c>AllowUnlessDenied</c> — deny-wins over owner, fail-closed).</summary>
    public const string DefaultMode = "AllowUnlessDenied";

    /// <summary>
    ///     The configured mode name (case-insensitive): <c>Off</c>, <c>AllowUnlessDenied</c>
    ///     or <c>AlwaysAllow</c>. Unknown values fail closed to <c>Off</c>.
    /// </summary>
    public string Mode { get; set; } = DefaultMode;

    /// <summary>
    ///     True when <see cref="Mode"/> is a recognized mode name.
    /// </summary>
    public bool IsKnownMode => TryResolveMode(out _);

    /// <summary>
    ///     Parses the configured mode. Returns <see cref="OwnerOverrideMode.Off"/> for
    ///     unknown, empty or null values (fail-closed); use <see cref="IsKnownMode"/> to
    ///     distinguish a deliberate <c>Off</c> from an unparseable value for warning logs.
    /// </summary>
    public OwnerOverrideMode ResolveMode() => TryResolveMode(out var mode) ? mode : OwnerOverrideMode.Off;

    /// <summary>
    ///     Attempts to parse the configured mode name (case-insensitive). Values padded
    ///     with leading/trailing whitespace are treated as unknown (fail closed):
    ///     <c>Enum.TryParse</c> silently trims its input, and a typo'd configuration
    ///     value must never be silently normalized into a permission-widening mode.
    /// </summary>
    public bool TryResolveMode(out OwnerOverrideMode mode)
    {
        if (string.IsNullOrEmpty(Mode) || Mode.AsSpan().Trim().Length != Mode.Length)
        {
            mode = OwnerOverrideMode.Unknown;
            return false;
        }

        return Enum.TryParse(Mode, ignoreCase: true, out mode)
            && mode is OwnerOverrideMode.Off or OwnerOverrideMode.AllowUnlessDenied or OwnerOverrideMode.AlwaysAllow;
    }
}
