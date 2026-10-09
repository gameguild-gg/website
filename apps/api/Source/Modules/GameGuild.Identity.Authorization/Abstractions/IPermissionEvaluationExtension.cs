namespace GameGuild.Identity.Authorization;

/// <summary>
///     Plugin extension point for custom effective-permission evaluation logic
///     (issue #358). Extensions are registered in the DI container
///     (<c>services.AddTransient&lt;IPermissionEvaluationExtension, T&gt;()</c>) and are
///     invoked by <see cref="EffectivePermissionResolverService"/> after every built-in
///     layer has contributed, immediately before the DENY-WINS subtraction.
/// </summary>
/// <remarks>
///     <para>
///         <b>Ordering (documented contract):</b>
///         <list type="number">
///             <item>Built-in layers 1-8 run first in their fixed order (static system
///             wildcard, RBAC roles, role providers, global defaults, tenant defaults,
///             direct grants, JIT elevation, resource grants).</item>
///             <item>Extensions then run in ascending <see cref="Order"/>; extensions with
///             equal <see cref="Order"/> run in DI registration order (deterministic).</item>
///             <item>Each extension observes the allow/deny sets accumulated so far, so a
///             later extension can add a deny that an earlier extension allowed.</item>
///             <item>Finally DENY-WINS is applied: <c>Effective = (union of allows) - (union of denies)</c>.
///             Extension allows are therefore vetoable by any built-in or extension deny;
///             extensions can never bypass DENY-WINS, cannot mint static grants and cannot
///             grant the non-delegable <c>admin:*</c> wildcard.</item>
///         </list>
///     </para>
///     <para>
///         Extensions must be deterministic and side-effect free on the decision path:
///         they may only return additional allows/denies. Thrown exceptions are treated as
///         a failed extension — logged and ignored (the resolution continues fail-closed
///         without that extension's contributions).
///     </para>
/// </remarks>
public interface IPermissionEvaluationExtension
{
    /// <summary>
    ///     Unique, stable name of the extension (used in logs and source attribution).
    /// </summary>
    string Name { get; }

    /// <summary>
    ///     Execution order relative to other extensions (lower runs earlier). Ties break
    ///     by DI registration order.
    /// </summary>
    int Order { get; }

    /// <summary>
    ///     Contributes additional allow/deny permissions for the given evaluation.
    /// </summary>
    /// <param name="context">Read-only snapshot of the evaluation context and the accumulated sets.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Additional contributions; never null.</returns>
    Task<PermissionEvaluationExtensionResult> EvaluateAsync(
        PermissionEvaluationExtensionContext context,
        CancellationToken cancellationToken = default);
}

/// <summary>
///     Read-only snapshot handed to an <see cref="IPermissionEvaluationExtension"/>:
///     the authorization context plus the allow/deny sets accumulated by the built-in
///     layers and any extensions that already ran.
/// </summary>
public sealed record PermissionEvaluationExtensionContext
{
    /// <summary>Required: the user whose permissions are resolved.</summary>
    public required Guid UserId { get; init; }

    /// <summary>Required: the tenant scope.</summary>
    public required Guid TenantId { get; init; }

    /// <summary>Optional resource type (resource layer active when paired with <see cref="ResourceId"/>).</summary>
    public string? ResourceType { get; init; }

    /// <summary>Optional resource id.</summary>
    public string? ResourceId { get; init; }

    /// <summary>Allow permissions accumulated so far (read-only view).</summary>
    public required IReadOnlySet<string> CurrentAllows { get; init; }

    /// <summary>Deny permissions accumulated so far (read-only view).</summary>
    public required IReadOnlySet<string> CurrentDenies { get; init; }
}

/// <summary>
///     Contributions of one <see cref="IPermissionEvaluationExtension"/>.
/// </summary>
/// <param name="AdditionalAllows">Extra allow permissions to merge into the allow set.</param>
/// <param name="AdditionalDenies">Extra deny permissions to merge into the deny set (subject to DENY-WINS).</param>
public sealed record PermissionEvaluationExtensionResult(
    IReadOnlyCollection<string> AdditionalAllows,
    IReadOnlyCollection<string> AdditionalDenies)
{
    /// <summary>A result with no contributions.</summary>
    public static PermissionEvaluationExtensionResult None { get; } =
        new(Array.Empty<string>(), Array.Empty<string>());
}
