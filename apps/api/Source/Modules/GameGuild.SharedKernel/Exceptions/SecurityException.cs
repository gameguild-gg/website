using System.Net;

namespace GameGuild;

/// <summary>
///     Base exception for all security-related errors.
///     Provides proper HTTP status code mapping and prevents information leakage.
/// </summary>
public abstract class SecurityException : Exception
{
    /// <summary>
    ///     The HTTP status code to return (401 or 403).
    /// </summary>
    public abstract HttpStatusCode StatusCode { get; }

    /// <summary>
    ///     The user-facing message (sanitized to prevent information leakage).
    /// </summary>
    public abstract string PublicMessage { get; }

    /// <summary>
    ///     Internal message for logging (may contain sensitive details).
    /// </summary>
    public string InternalMessage { get; }

    protected SecurityException(string internalMessage) : base(internalMessage)
    {
        InternalMessage = internalMessage;
    }

    protected SecurityException(string internalMessage, Exception innerException) 
        : base(internalMessage, innerException)
    {
        InternalMessage = internalMessage;
    }
}

/// <summary>
///     Exception thrown when authentication is required but missing or invalid.
///     Maps to HTTP 401 Unauthorized.
/// </summary>
/// <remarks>
///     <para>Use this when:</para>
///     <list type="bullet">
///         <item>No authentication credentials were provided</item>
///         <item>Authentication credentials are invalid or expired</item>
///         <item>Token is malformed or cannot be validated</item>
///     </list>
///     <para>
///         The public message is always generic to prevent user enumeration attacks.
///     </para>
/// </remarks>
public sealed class AuthenticationRequiredException : SecurityException
{
    public override HttpStatusCode StatusCode => HttpStatusCode.Unauthorized;

    /// <summary>
    ///     Generic message that doesn't reveal whether the user exists.
    /// </summary>
    public override string PublicMessage => "Authentication is required to access this resource.";

    /// <summary>
    ///     Creates a new authentication required exception.
    /// </summary>
    /// <param name="internalMessage">
    ///     Detailed message for logging. This is NOT exposed to clients.
    /// </param>
    public AuthenticationRequiredException(string internalMessage = "Authentication required")
        : base(internalMessage)
    {
    }

    public AuthenticationRequiredException(string internalMessage, Exception innerException)
        : base(internalMessage, innerException)
    {
    }
}

/// <summary>
///     Exception thrown when the authenticated user lacks permission to perform an action.
///     Maps to HTTP 403 Forbidden.
/// </summary>
/// <remarks>
///     <para>Use this when:</para>
///     <list type="bullet">
///         <item>User is authenticated but lacks required permission</item>
///         <item>User is not a member of the required tenant</item>
///         <item>User is trying to access a resource they don't own</item>
///         <item>User's account is suspended or inactive</item>
///     </list>
///     <para>
///         The public message is always generic to prevent information leakage about
///         what permissions or resources exist.
///     </para>
/// </remarks>
public sealed class AccessDeniedException : SecurityException
{
    public override HttpStatusCode StatusCode => HttpStatusCode.Forbidden;

    /// <summary>
    ///     Generic message that doesn't reveal specific permission requirements.
    /// </summary>
    public override string PublicMessage => "You do not have permission to perform this action.";

    /// <summary>
    ///     Creates a new access denied exception.
    /// </summary>
    /// <param name="internalMessage">
    ///     Detailed message for logging. This is NOT exposed to clients.
    ///     Example: "User {userId} lacks permission '{permission}' for resource {resourceId}"
    /// </param>
    public AccessDeniedException(string internalMessage = "Access denied")
        : base(internalMessage)
    {
    }

    public AccessDeniedException(string internalMessage, Exception innerException)
        : base(internalMessage, innerException)
    {
    }

    /// <summary>
    ///     Factory method for permission-related denials.
    /// </summary>
    public static AccessDeniedException ForMissingPermission(
        Guid userId, 
        string permission, 
        Guid? tenantId = null,
        Guid? resourceId = null)
    {
        var msg = $"User {userId} lacks permission '{permission}'";
        if (tenantId.HasValue)
        {
            msg += $" in tenant {tenantId}";
        }

        if (resourceId.HasValue)
        {
            msg += $" for resource {resourceId}";
        }

        return new AccessDeniedException(msg);
    }

    /// <summary>
    ///     Factory method for tenant membership denials.
    /// </summary>
    public static AccessDeniedException ForTenantMembership(Guid userId, Guid tenantId)
    {
        return new AccessDeniedException($"User {userId} is not a member of tenant {tenantId}");
    }

    /// <summary>
    ///     Factory method for resource ownership denials.
    /// </summary>
    public static AccessDeniedException ForResourceOwnership(Guid userId, string resourceType, Guid resourceId)
    {
        return new AccessDeniedException($"User {userId} does not own {resourceType} {resourceId}");
    }

    /// <summary>
    ///     Factory method for inactive/suspended account denials.
    /// </summary>
    public static AccessDeniedException ForInactiveAccount(Guid userId)
    {
        return new AccessDeniedException($"User {userId} account is inactive or suspended");
    }
}

/// <summary>
///     Exception thrown when cross-tenant access is attempted.
///     Maps to HTTP 403 Forbidden.
/// </summary>
public sealed class CrossTenantAccessException : SecurityException
{
    public override HttpStatusCode StatusCode => HttpStatusCode.Forbidden;

    public override string PublicMessage => "You do not have permission to access this resource.";

    /// <summary>
    ///     The tenant ID the user attempted to access.
    /// </summary>
    public Guid AttemptedTenantId { get; }

    /// <summary>
    ///     The user's actual tenant ID (if known).
    /// </summary>
    public Guid? UserTenantId { get; }

    public CrossTenantAccessException(Guid userId, Guid attemptedTenantId, Guid? userTenantId = null)
        : base($"User {userId} attempted cross-tenant access to tenant {attemptedTenantId}" +
               (userTenantId.HasValue ? $" (user's tenant: {userTenantId})" : ""))
    {
        AttemptedTenantId = attemptedTenantId;
        UserTenantId = userTenantId;
    }
}
