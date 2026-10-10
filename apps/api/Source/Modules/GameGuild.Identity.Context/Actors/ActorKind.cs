namespace GameGuild.Identity.Context.Actors;

/// <summary>
///     Attribute to define how an ActorKind is identified from claims.
///     This enables OCP-compliant ActorKind resolution without modifying switch statements.
/// </summary>
[AttributeUsage(AttributeTargets.Field, AllowMultiple = true)]
public sealed class ActorKindIdentifierAttribute : Attribute
{
    /// <summary>
    ///     The claim value that identifies this actor kind (e.g., "service", "system", "webhook").
    /// </summary>
    public string? ClaimValue { get; init; }

    /// <summary>
    ///     The grant type that identifies this actor kind (e.g., "client_credentials").
    /// </summary>
    public string? GrantType { get; init; }

    /// <summary>
    ///     A specific subject ID that identifies this actor kind (e.g., SystemActor.SystemSubjectId).
    /// </summary>
    public string? SubjectId { get; init; }
}

/// <summary>
///     Defines the type of actor making a request.
///     Used to distinguish between different authentication and authorization flows.
/// </summary>
/// <remarks>
///     <para>
///         To add a new ActorKind without modifying the resolution logic:
///     </para>
///     <list type="number">
///         <item>Add a new enum value with <see cref="ActorKindIdentifierAttribute"/></item>
///         <item>The attribute defines how to identify this actor from claims</item>
///     </list>
///     <para>
///         Example:
///     </para>
///     <code>
///     [ActorKindIdentifier(ClaimValue = "bot")]
///     Bot = 6,
///     </code>
/// </remarks>
public enum ActorKind
{
    /// <summary>
    ///     An anonymous or unauthenticated actor.
    /// </summary>
    Anonymous = 0,

    /// <summary>
    ///     A human user authenticated via standard authentication flows (JWT, OAuth, etc.).
    ///     This is the default for authenticated actors without specific identifiers.
    /// </summary>
    User = 1,

    /// <summary>
    ///     A service or application acting on its own behalf (service-to-service).
    ///     Typically authenticated via API keys, client credentials, or mTLS.
    /// </summary>
    [ActorKindIdentifier(GrantType = "client_credentials")]
    [ActorKindIdentifier(ClaimValue = "service")]
    Service = 2,

    /// <summary>
    ///     The system itself acting on behalf of background jobs, schedulers, or internal processes.
    ///     Has elevated privileges for system-level operations.
    /// </summary>
    [ActorKindIdentifier(ClaimValue = "system")]
    [ActorKindIdentifier(SubjectId = SystemActor.SystemSubjectIdConstant)]
    System = 3,

    /// <summary>
    ///     An external webhook or integration callback.
    ///     May have limited, pre-configured permissions.
    /// </summary>
    [ActorKindIdentifier(ClaimValue = "webhook")]
    Webhook = 4,

    /// <summary>
    ///     An external third-party actor (partner API, federated identity, etc.).
    ///     Subject to additional validation and restrictions.
    /// </summary>
    [ActorKindIdentifier(ClaimValue = "external")]
    External = 5
}

/// <summary>
///     Resolves ActorKind from claims using the attribute-based configuration.
///     OCP-compliant: adding new ActorKind values with attributes automatically works.
/// </summary>
public static class ActorKindResolver
{
    private static readonly Dictionary<string, ActorKind> ClaimValueMap;
    private static readonly Dictionary<string, ActorKind> GrantTypeMap;
    private static readonly Dictionary<string, ActorKind> SubjectIdMap;

    static ActorKindResolver()
    {
        ClaimValueMap = new Dictionary<string, ActorKind>(StringComparer.OrdinalIgnoreCase);
        GrantTypeMap = new Dictionary<string, ActorKind>(StringComparer.OrdinalIgnoreCase);
        SubjectIdMap = new Dictionary<string, ActorKind>(StringComparer.Ordinal);

        // Build maps from attributes at startup
        foreach (var field in typeof(ActorKind).GetFields())
        {
            if (!field.IsLiteral)
            {
                continue;
            }

            var actorKind = (ActorKind)field.GetValue(null)!;
            var attributes = field.GetCustomAttributes(typeof(ActorKindIdentifierAttribute), false);

            foreach (ActorKindIdentifierAttribute attr in attributes)
            {
                if (!string.IsNullOrEmpty(attr.ClaimValue))
                {
                    ClaimValueMap[attr.ClaimValue] = actorKind;
                }

                if (!string.IsNullOrEmpty(attr.GrantType))
                {
                    GrantTypeMap[attr.GrantType] = actorKind;
                }

                if (!string.IsNullOrEmpty(attr.SubjectId))
                {
                    SubjectIdMap[attr.SubjectId] = actorKind;
                }
            }
        }
    }

    /// <summary>
    ///     Resolves the ActorKind from the provided claims data.
    /// </summary>
    /// <param name="grantType">The OAuth grant type (e.g., "client_credentials").</param>
    /// <param name="actorTypeClaim">The explicit actor type claim value.</param>
    /// <param name="subjectId">The subject ID of the actor.</param>
    /// <returns>The resolved ActorKind, defaulting to User for authenticated actors.</returns>
    public static ActorKind Resolve(string? grantType, string? actorTypeClaim, string? subjectId)
    {
        // Priority 1: Grant type (e.g., client_credentials → Service)
        if (!string.IsNullOrEmpty(grantType) && GrantTypeMap.TryGetValue(grantType, out var kindFromGrant))
        {
            return kindFromGrant;
        }

        // Priority 2: Explicit actor type claim
        if (!string.IsNullOrEmpty(actorTypeClaim) && ClaimValueMap.TryGetValue(actorTypeClaim, out var kindFromClaim))
        {
            return kindFromClaim;
        }

        // Priority 3: Known subject ID (e.g., system subject)
        if (!string.IsNullOrEmpty(subjectId) && SubjectIdMap.TryGetValue(subjectId, out var kindFromSubject))
        {
            return kindFromSubject;
        }

        // Default: User
        return ActorKind.User;
    }
}

