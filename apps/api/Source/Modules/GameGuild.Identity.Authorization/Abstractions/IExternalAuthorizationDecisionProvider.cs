namespace GameGuild.Identity.Authorization;

/// <summary>
///     Outcome of one external authorization-decision evaluation (issue #146).
/// </summary>
public enum ExternalAuthorizationOutcome
{
    /// <summary>
    ///     The external service allows the queried action. An external Allow is an
    ///     endorsement only: it never grants a permission that local resolution did not
    ///     already grant and never overrides a local deny.
    /// </summary>
    Allow = 0,

    /// <summary>
    ///     The external service denies the queried action. An external deny overrides a
    ///     local allow through DENY-WINS; only the non-deniable static system-account
    ///     wildcard survives it.
    /// </summary>
    Deny = 1,

    /// <summary>
    ///     The external service has no decision for the queried action. Local resolution
    ///     is unchanged (passthrough).
    /// </summary>
    NotApplicable = 2
}

/// <summary>
///     One authorization question sent to an external authorization-decision provider
///     (issue #146). The query is permission-shaped: it always names exactly one
///     permission and the authorization context it is evaluated in.
/// </summary>
public sealed record ExternalAuthorizationQuery
{
    /// <summary>Required: the user whose action is being decided.</summary>
    public required Guid UserId { get; init; }

    /// <summary>Required: the tenant scope of the action.</summary>
    public required Guid TenantId { get; init; }

    /// <summary>Required: the permission being evaluated (for example <c>reports:read</c>).</summary>
    public required string Permission { get; init; }

    /// <summary>Optional: resource type when the decision is resource-scoped.</summary>
    public string? ResourceType { get; init; }

    /// <summary>Optional: resource id when the decision is resource-scoped.</summary>
    public string? ResourceId { get; init; }
}

/// <summary>
///     The decision returned by an external authorization service for one
///     <see cref="ExternalAuthorizationQuery"/>.
/// </summary>
public sealed record ExternalAuthorizationDecision
{
    /// <summary>The outcome for the queried action.</summary>
    public required ExternalAuthorizationOutcome Outcome { get; init; }

    /// <summary>
    ///     Human-readable reasons supplied by the external service (never null; may be
    ///     empty). Used for logging and audit context only.
    /// </summary>
    public IReadOnlyList<string> Reasons { get; init; } = Array.Empty<string>();

    /// <summary>Creates an <see cref="ExternalAuthorizationOutcome.Allow"/> decision.</summary>
    public static ExternalAuthorizationDecision Allow { get; } =
        new() { Outcome = ExternalAuthorizationOutcome.Allow };

    /// <summary>Creates a <see cref="ExternalAuthorizationOutcome.NotApplicable"/> decision.</summary>
    public static ExternalAuthorizationDecision NotApplicable { get; } =
        new() { Outcome = ExternalAuthorizationOutcome.NotApplicable };

    /// <summary>Creates a <see cref="ExternalAuthorizationOutcome.Deny"/> decision with reasons.</summary>
    /// <param name="reasons">Reasons attached to the denial.</param>
    public static ExternalAuthorizationDecision Deny(params string[] reasons) =>
        new() { Outcome = ExternalAuthorizationOutcome.Deny, Reasons = reasons };
}

/// <summary>
///     Contract for external authorization-decision providers (issue #146): services
///     that answer individual permission questions, such as an OAuth2-protected policy
///     decision point. This is the externalized <b>authorization</b> seam — external
///     <b>authentication</b> (identity assertion, protocol login) is handled separately
///     by the authentication modules.
/// </summary>
/// <remarks>
///     <para>
///         <b>Precedence (documented contract, see
///         <c>apps/api/docs/effective-permission-resolution.md</c>):</b> the external
///         decision layer runs after every local layer and is <b>veto-only</b>:
///         <list type="bullet">
///             <item>An external <see cref="ExternalAuthorizationOutcome.Deny"/> overrides a
///             local allow (it unions into the DENY-WINS deny set).</item>
///             <item>An external <see cref="ExternalAuthorizationOutcome.Allow"/> can never
///             override a local deny and never grants a permission on its own — grants
///             remain exclusively local so a misconfigured or compromised external
///             service cannot widen access (fail-closed paramount, absent = deny).</item>
///             <item><see cref="ExternalAuthorizationOutcome.NotApplicable"/> and a
///             <c>null</c> return leave local resolution unchanged.</item>
///             <item>The static system-account wildcard stays non-deniable.</item>
///         </list>
///     </para>
///     <para>
///         <b>Never-throw contract.</b> Implementations must not throw; an unavailable
///         or failing service is expressed through the configured fail mode (deny when
///         enforcing, no decision when observing). The resolver additionally treats an
///         unexpected throw as a denial of the permission under question (fail-closed).
///     </para>
/// </remarks>
public interface IExternalAuthorizationDecisionProvider
{
    /// <summary>
    ///     Evaluates one authorization question against the external service.
    /// </summary>
    /// <param name="query">The permission-shaped authorization question.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    ///     The external decision, or <c>null</c> when no decision applies (provider
    ///     disabled, service unavailable in observe mode, or no answer for the query).
    ///     Never throws.
    /// </returns>
    Task<ExternalAuthorizationDecision?> EvaluateAsync(
        ExternalAuthorizationQuery query,
        CancellationToken cancellationToken = default);
}
