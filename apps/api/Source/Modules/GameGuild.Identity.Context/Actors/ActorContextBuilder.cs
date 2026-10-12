namespace GameGuild.Identity.Context.Actors;

/// <summary>
///     Builder for constructing <see cref="ActorContext"/> instances.
/// </summary>
/// <remarks>
///     <para>
///         Use this builder to construct ActorContext from various sources:
///         - Claims principal (HTTP requests)
///         - Service credentials (service-to-service)
///         - System context (background jobs)
///         - Test fixtures
///     </para>
///     <para>
///         The builder is mutable during construction, but produces an immutable ActorContext.
///     </para>
/// </remarks>
public sealed class ActorContextBuilder
{
    private ActorKind _actorKind = ActorKind.Anonymous;
    private string? _subjectId;
    private Guid? _tenantId;
    private readonly HashSet<string> _roles = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _permissions = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _attributes = new(StringComparer.OrdinalIgnoreCase);
    private string? _authScheme;
    private bool _isAuthenticated;

    /// <summary>
    ///     Creates a new builder starting with default anonymous state.
    /// </summary>
    public static ActorContextBuilder Create() => new();

    /// <summary>
    ///     Creates a builder for a user actor.
    /// </summary>
    /// <param name="userId">The user's unique identifier.</param>
    /// <returns>The builder for chaining.</returns>
    public static ActorContextBuilder ForUser(Guid userId)
    {
        return new ActorContextBuilder()
            .WithActorKind(ActorKind.User)
            .WithSubjectId(userId.ToString())
            .AsAuthenticated();
    }

    /// <summary>
    ///     Creates a builder for a user actor from a UserActor instance.
    /// </summary>
    /// <param name="actor">The user actor.</param>
    /// <returns>The builder for chaining.</returns>
    public static ActorContextBuilder ForUser(UserActor actor)
    {
        ArgumentNullException.ThrowIfNull(actor);

        var builder = ForUser(actor.UserId);

        if (actor.Email is not null)
        {
            builder.WithAttribute("email", actor.Email);
        }

        if (actor.Name is not null)
        {
            builder.WithAttribute("name", actor.Name);
        }

        return builder;
    }

    /// <summary>
    ///     Creates a builder for a service actor.
    /// </summary>
    /// <param name="serviceId">The service's client ID.</param>
    /// <param name="serviceName">The service's display name.</param>
    /// <returns>The builder for chaining.</returns>
    public static ActorContextBuilder ForService(string serviceId, string serviceName)
    {
        ArgumentNullException.ThrowIfNull(serviceId);
        ArgumentNullException.ThrowIfNull(serviceName);

        return new ActorContextBuilder()
            .WithActorKind(ActorKind.Service)
            .WithSubjectId(serviceId)
            .WithAttribute("service_name", serviceName)
            .AsAuthenticated();
    }

    /// <summary>
    ///     Creates a builder for a system actor.
    /// </summary>
    /// <param name="operationName">The name of the system operation.</param>
    /// <returns>The builder for chaining.</returns>
    public static ActorContextBuilder ForSystem(string operationName)
    {
        ArgumentNullException.ThrowIfNull(operationName);

        return new ActorContextBuilder()
            .WithActorKind(ActorKind.System)
            .WithSubjectId(SystemActor.SystemSubjectId)
            .WithAttribute("operation", operationName)
            .AsAuthenticated()
            .WithRole("SystemAdmin"); // System actors have full access
    }

    /// <summary>
    ///     Sets the actor kind.
    /// </summary>
    public ActorContextBuilder WithActorKind(ActorKind kind)
    {
        _actorKind = kind;
        return this;
    }

    /// <summary>
    ///     Sets the subject ID.
    /// </summary>
    public ActorContextBuilder WithSubjectId(string? subjectId)
    {
        _subjectId = subjectId;
        return this;
    }

    /// <summary>
    ///     Sets the tenant ID.
    /// </summary>
    public ActorContextBuilder WithTenantId(Guid? tenantId)
    {
        _tenantId = tenantId;
        return this;
    }

    /// <summary>
    ///     Adds a role to the actor.
    /// </summary>
    public ActorContextBuilder WithRole(string role)
    {
        ArgumentNullException.ThrowIfNull(role);
        _roles.Add(role);
        return this;
    }

    /// <summary>
    ///     Adds multiple roles to the actor.
    /// </summary>
    public ActorContextBuilder WithRoles(IEnumerable<string> roles)
    {
        ArgumentNullException.ThrowIfNull(roles);
        foreach (var role in roles)
        {
            _roles.Add(role);
        }
        return this;
    }

    /// <summary>
    ///     Adds a permission to the actor.
    /// </summary>
    public ActorContextBuilder WithPermission(string permission)
    {
        ArgumentNullException.ThrowIfNull(permission);
        _permissions.Add(permission);
        return this;
    }

    /// <summary>
    ///     Adds multiple permissions to the actor.
    /// </summary>
    public ActorContextBuilder WithPermissions(IEnumerable<string> permissions)
    {
        ArgumentNullException.ThrowIfNull(permissions);
        foreach (var permission in permissions)
        {
            _permissions.Add(permission);
        }
        return this;
    }

    /// <summary>
    ///     Adds an attribute to the actor.
    /// </summary>
    public ActorContextBuilder WithAttribute(string key, string value)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(value);
        _attributes[key] = value;
        return this;
    }

    /// <summary>
    ///     Adds multiple attributes to the actor.
    /// </summary>
    public ActorContextBuilder WithAttributes(IEnumerable<KeyValuePair<string, string>> attributes)
    {
        ArgumentNullException.ThrowIfNull(attributes);
        foreach (var (key, value) in attributes)
        {
            _attributes[key] = value;
        }
        return this;
    }

    /// <summary>
    ///     Sets the authentication scheme.
    /// </summary>
    public ActorContextBuilder WithAuthScheme(string? authScheme)
    {
        _authScheme = authScheme;
        return this;
    }

    /// <summary>
    ///     Marks the actor as authenticated.
    /// </summary>
    public ActorContextBuilder AsAuthenticated()
    {
        _isAuthenticated = true;
        return this;
    }

    /// <summary>
    ///     Marks the actor as having verified MFA.
    /// </summary>
    public ActorContextBuilder WithMfaVerified(bool verified = true)
    {
        _attributes["mfa_verified"] = verified.ToString().ToLowerInvariant();
        return this;
    }

    /// <summary>
    ///     Builds the immutable <see cref="ActorContext"/>.
    /// </summary>
    /// <returns>A new ActorContext instance.</returns>
    public ActorContext Build()
    {
        return new ActorContext
        {
            ActorKind = _actorKind,
            SubjectId = _subjectId,
            TenantId = _tenantId,
            Roles = _roles.ToHashSet().AsReadOnly(),
            Permissions = _permissions.ToHashSet().AsReadOnly(),
            TypedAttributes = ActorAttributes.FromDictionary(
                new Dictionary<string, string>(_attributes).AsReadOnly()),
            AuthScheme = _authScheme,
            IsAuthenticated = _isAuthenticated
        };
    }

    /// <summary>
    ///     Sets strongly-typed attributes directly.
    /// </summary>
    /// <param name="attributes">The typed attributes.</param>
    /// <returns>The builder for chaining.</returns>
    public ActorContextBuilder WithTypedAttributes(ActorAttributes attributes)
    {
        ArgumentNullException.ThrowIfNull(attributes);
        
        // Merge typed attributes into the dictionary for backward compatibility
        foreach (var kvp in attributes.ToDictionary())
        {
            _attributes[kvp.Key] = kvp.Value;
        }
        
        return this;
    }
}

/// <summary>
///     Extension methods for HashSet to create read-only wrappers.
/// </summary>
internal static class HashSetExtensions
{
    /// <summary>
    ///     Wraps a HashSet as a read-only set.
    /// </summary>
    public static IReadOnlySet<T> AsReadOnly<T>(this HashSet<T> hashSet)
    {
        return hashSet;
    }

    /// <summary>
    ///     Wraps a Dictionary as a read-only dictionary.
    /// </summary>
    public static IReadOnlyDictionary<TKey, TValue> AsReadOnly<TKey, TValue>(
        this Dictionary<TKey, TValue> dictionary) where TKey : notnull
    {
        return dictionary;
    }
}
