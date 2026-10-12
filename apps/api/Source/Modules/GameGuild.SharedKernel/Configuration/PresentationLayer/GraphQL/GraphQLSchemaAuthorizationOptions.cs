namespace GameGuild.Configuration.PresentationLayer.GraphQL;

/// <summary>
///     Schema-authorization settings for the GraphQL authorization framework (issue #335)
///     under the <c>GraphQL:SchemaAuthorization</c> section. These settings gate schema
///     introspection and permission-aware schema visibility; field-level enforcement via
///     the general <c>@authorize</c> directive is always active when GraphQL is enabled.
/// </summary>
/// <remarks>
///     <para>
///         Example:
///         <code>
/// /// "GraphQL": { "SchemaAuthorization": { "SchemaAdminPermission": "SystemAdmin" } }
///         </code>
///     </para>
///     <para>
///         <b>Fail-closed:</b> an unknown or blank
///         <see cref="SchemaAdminPermission"/> name never grants schema-admin access; it
///         resolves to "no user holds it" at evaluation time. Introspection evaluation
///         errors also fail closed (introspection is denied).
///     </para>
/// </remarks>
public sealed class GraphQLSchemaAuthorizationOptions
{
    /// <summary>The configuration sub-section name (<c>GraphQL:SchemaAuthorization</c>).</summary>
    public const string SectionName = "SchemaAuthorization";

    /// <summary>
    ///     The default schema-admin permission key (the <c>SystemAdmin</c> entry of the
    ///     typed permission model).
    /// </summary>
    public const string DefaultSchemaAdminPermission = "SystemAdmin";

    /// <summary>
    ///     When true (default), schema introspection hides authorization-protected
    ///     operations from callers that lack the permission to execute them: a caller
    ///     that cannot see every <c>@authorize</c>-protected field is denied introspection
    ///     entirely instead of receiving a partial schema (fail-closed; no partial leak).
    /// </summary>
    public bool FilterSchema { get; set; } = true;

    /// <summary>
    ///     When true (default), introspection in a Production environment is restricted
    ///     to callers holding <see cref="SchemaAdminPermission"/>; every other caller is
    ///     denied regardless of <see cref="FilterSchema"/>.
    /// </summary>
    public bool RestrictIntrospectionToSchemaAdminsInProduction { get; set; } = true;

    /// <summary>
    ///     The typed permission key that identifies schema administrators, for example
    ///     <c>SystemAdmin</c>. Callers holding this permission bypass per-field schema
    ///     visibility checks and are the only callers allowed to introspect in Production.
    ///     Unknown or blank values fail closed (nobody is a schema admin).
    /// </summary>
    public string SchemaAdminPermission { get; set; } = DefaultSchemaAdminPermission;

    /// <summary>
    ///     Validates the settings: the schema-admin permission key must be a non-blank,
    ///     whitespace-trimmed value. Unknown permission names are rejected later by
    ///     evaluation (fail closed), not here, to avoid binding this platform options
    ///     type to the permission enum.
    /// </summary>
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(SchemaAdminPermission) ||
            SchemaAdminPermission.AsSpan().Trim().Length != SchemaAdminPermission.Length)
        {
            throw new InvalidOperationException(
                "GraphQL schema-admin permission must be a non-empty, whitespace-trimmed permission key.");
        }
    }
}
