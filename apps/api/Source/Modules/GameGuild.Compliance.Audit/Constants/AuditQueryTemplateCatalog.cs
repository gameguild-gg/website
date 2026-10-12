namespace GameGuild.Compliance.Audit;

/// <summary>A built-in, immutable audit query template aimed at a common investigation or compliance scenario.</summary>
public sealed record AuditQueryTemplate
{
    /// <summary>Stable template identifier.</summary>
    public required string TemplateId { get; init; }

    /// <summary>Display name.</summary>
    public required string Name { get; init; }

    /// <summary>What the template finds and when to use it.</summary>
    public required string Description { get; init; }

    /// <summary>Role/persona the template is optimized for (advisory metadata for consumers).</summary>
    public required string IntendedRole { get; init; }

    /// <summary>Structured DSL JSON, pre-validated at startup.</summary>
    public required string QueryJson { get; init; }
}

/// <summary>
///     Role-based starting points for common investigation and compliance scenarios: failed
///     authentication sweeps, permission drift, high-risk activity, tenant isolation checks.
///     Templates are validated once at construction so a broken template definition can never
///     reach callers or execute (fail fast, fail closed).
/// </summary>
public static class AuditQueryTemplateCatalog
{
    private const string FailedLoginsQuery =
        """{"filter":{"and":[{"field":"action","op":"eq","value":"LoginFailed"},{"field":"timestamp","op":"gte","value":"2026-01-01T00:00:00Z"}]}}""";

    private const string HighRiskQuery =
        """{"filter":{"or":[{"field":"severity","op":"eq","value":"High"},{"field":"severity","op":"eq","value":"Critical"}]}}""";

    private const string PermissionDenialsQuery =
        """{"filter":{"and":[{"field":"source","op":"eq","value":"Permission"},{"field":"success","op":"eq","value":false}]}}""";

    private const string PermissionGrantsQuery =
        """{"filter":{"and":[{"field":"source","op":"eq","value":"Permission"},{"field":"action","op":"eq","value":"Grant"}]}}""";

    private const string AdminActionsQuery =
        """{"filter":{"and":[{"field":"source","op":"eq","value":"General"},{"field":"action","op":"contains","value":"View"}]}}""";

    private const string AuthenticationFailuresQuery =
        """{"filter":{"and":[{"field":"source","op":"eq","value":"Authentication"},{"field":"success","op":"eq","value":false}]}}""";

    /// <summary>All built-in templates, validated at first access.</summary>
    public static IReadOnlyList<AuditQueryTemplate> Templates => LazyTemplates.Value;

    private static readonly Lazy<IReadOnlyList<AuditQueryTemplate>> LazyTemplates = new(CreateValidatedCatalog);

    private static IReadOnlyList<AuditQueryTemplate> CreateValidatedCatalog()
    {
        var parser = new AuditQueryDslParser();
        var templates = new List<AuditQueryTemplate>
        {
            new()
            {
                TemplateId = "failed-logins",
                Name = "Failed login attempts",
                Description = "Authentication attempts that failed, regardless of reason. Pair with a timestamp range for a window.",
                IntendedRole = "SecurityAnalyst",
                QueryJson = FailedLoginsQuery
            },
            new()
            {
                TemplateId = "high-risk-events",
                Name = "High and critical risk events",
                Description = "Events classified High or Critical across every audit store.",
                IntendedRole = "ComplianceInvestigator",
                QueryJson = HighRiskQuery
            },
            new()
            {
                TemplateId = "permission-denials",
                Name = "Denied permission operations",
                Description = "Permission grant/revoke operations that failed, from the permission audit log.",
                IntendedRole = "ComplianceInvestigator",
                QueryJson = PermissionDenialsQuery
            },
            new()
            {
                TemplateId = "permission-grants",
                Name = "Permission grants",
                Description = "Every recorded permission grant for drift and least-privilege reviews.",
                IntendedRole = "ComplianceInvestigator",
                QueryJson = PermissionGrantsQuery
            },
            new()
            {
                TemplateId = "admin-views",
                Name = "Administrator read access to audit data",
                Description = "Administrative views of audit logs and dashboards — who inspected the audit trail itself.",
                IntendedRole = "SecurityAnalyst",
                QueryJson = AdminActionsQuery
            },
            new()
            {
                TemplateId = "authentication-failures",
                Name = "All failed authentication activity",
                Description = "Union of failed logins across the authentication store.",
                IntendedRole = "SecurityAnalyst",
                QueryJson = AuthenticationFailuresQuery
            }
        };

        foreach (var template in templates)
        {
            var parsed = parser.ParseJson(template.QueryJson);
            if (parsed.IsFailure)
            {
                throw new InvalidOperationException(
                    $"Built-in audit query template '{template.TemplateId}' is invalid: {parsed.Error.Description}");
            }
        }

        return templates;
    }
}
