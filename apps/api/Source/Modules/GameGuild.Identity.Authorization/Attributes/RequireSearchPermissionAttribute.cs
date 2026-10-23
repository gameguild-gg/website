using Microsoft.AspNetCore.Http;

namespace GameGuild.Identity.Authorization;

/// <summary>
/// Requires the Search operation when a collection endpoint receives non-blank search text.
/// The endpoint must independently require List and its domain read permission.
/// Resolution uses the existing DAC filter, authenticated tenant context and deny-wins engine.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
public sealed class RequireSearchPermissionAttribute : DacPermissionAttributeBase
{
    /// <summary>Names the query-string parameter which enables text discovery.</summary>
    public RequireSearchPermissionAttribute(string queryParameter)
        : base(PermissionLayer.Tenant, QueryPermission.Keys.Search)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(queryParameter);
        QueryParameter = queryParameter;
    }

    /// <summary>The request parameter that enables search.</summary>
    public string QueryParameter { get; }

    /// <inheritdoc />
    public override bool ShouldApply(HttpContext httpContext)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        return httpContext.Request.Query.TryGetValue(QueryParameter, out var values)
            && values.Any(value => !string.IsNullOrWhiteSpace(value));
    }
}
