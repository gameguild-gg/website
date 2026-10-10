namespace GameGuild.Identity.Provisioning.Scim;

/// <summary>
///     RFC 7644 §4 discovery documents. The ServiceProviderConfig advertises exactly
///     the capabilities this provider implements (PATCH and filtering; Bulk when
///     enabled; no sorting, ETags or change log).
/// </summary>
public static class ScimDiscoveryDocuments
{
    public static object BuildServiceProviderConfig(ScimProvisioningOptions options)
    {
        return new Dictionary<string, object?>
        {
            ["schemas"] = new[] { ScimConstants.ServiceProviderConfigSchemaUrn },
            ["documentationUri"] = "https://gameguild.gg/docs/api/scim",
            ["patch"] = new { supported = true },
            ["bulk"] = new
            {
                supported = options.BulkEnabled,
                maxOperations = options.MaxBulkOperations,
                maxPayloadSize = 1024 * 1024
            },
            ["filter"] = new
            {
                supported = true,
                maxResults = options.MaxPageSize
            },
            ["changeLog"] = new { supported = false },
            ["sort"] = new { supported = false },
            ["etag"] = new { supported = false },
            ["authenticationSchemes"] = new object[]
            {
                new
                {
                    type = "oauthbearertoken",
                    name = "OAuth Bearer Token",
                    description = "Authorization header with a tenant-scoped provisioning token issued by the SCIM token management API."
                }
            }
        };
    }

    public static object BuildResourceTypes()
    {
        return new Dictionary<string, object?>
        {
            ["schemas"] = new[] { ScimConstants.ListResponseSchema },
            ["totalResults"] = 2,
            ["startIndex"] = 1,
            ["itemsPerPage"] = 2,
            ["Resources"] = new object[]
            {
                new
                {
                    schemas = new[] { ScimConstants.ResourceTypeSchemaUrn },
                    id = "User",
                    name = "User",
                    description = "Platform user account provisioned through SCIM.",
                    endpoint = "/Users",
                    schema = ScimConstants.UserSchemaUrn,
                    schemaExtensions = Array.Empty<object>()
                },
                new
                {
                    schemas = new[] { ScimConstants.ResourceTypeSchemaUrn },
                    id = "Group",
                    name = "Group",
                    description = "Tenant role with provisioned membership, exposed as a SCIM group.",
                    endpoint = "/Groups",
                    schema = ScimConstants.GroupSchemaUrn,
                    schemaExtensions = Array.Empty<object>()
                }
            }
        };
    }

    public static object BuildSchemas()
    {
        return new Dictionary<string, object?>
        {
            ["schemas"] = new[] { ScimConstants.ListResponseSchema },
            ["totalResults"] = 2,
            ["startIndex"] = 1,
            ["itemsPerPage"] = 2,
            ["Resources"] = new object[] { BuildUserSchema(), BuildGroupSchema() }
        };
    }

    public static object? BuildSchema(string urn)
        => urn.Equals(ScimConstants.UserSchemaUrn, StringComparison.Ordinal) ? BuildUserSchema()
           : urn.Equals(ScimConstants.GroupSchemaUrn, StringComparison.Ordinal) ? BuildGroupSchema()
           : null;

    private static object BuildUserSchema()
    {
        return new Dictionary<string, object?>
        {
            ["schemas"] = new[] { ScimConstants.SchemaSchemaUrn },
            ["id"] = ScimConstants.UserSchemaUrn,
            ["name"] = "User",
            ["description"] = "User account, mapped to the platform identity store.",
            ["attributes"] = new object[]
            {
                Simple("userName", "string", required: true, mutable: true, caseExact: false,
                    description: "Unique identifier for the User, mapped to the platform username (falls back to the primary e-mail)."),
                Simple("displayName", "string", required: false, mutable: true, caseExact: false,
                    description: "Display name, mapped to the platform user name."),
                Simple("externalId", "string", required: false, mutable: true, caseExact: true,
                    description: "Identifier supplied by the provisioning client; unique per tenant and used for idempotent creation."),
                Simple("active", "boolean", required: false, mutable: true,
                    description: "False deactivates the account without deleting it."),
                new
                {
                    name = "name",
                    type = "complex",
                    multiValued = false,
                    description = "Components of the user's name. Only givenName, familyName and formatted are stored.",
                    required = false,
                    mutability = "readWrite",
                    returned = "default",
                    subAttributes = new object[]
                    {
                        Simple("givenName", "string", required: false, mutable: true, caseExact: false),
                        Simple("familyName", "string", required: false, mutable: true, caseExact: false),
                        Simple("formatted", "string", required: false, mutable: true, caseExact: false)
                    }
                },
                new
                {
                    name = "emails",
                    type = "complex",
                    multiValued = true,
                    description = "E-mail addresses. The provider stores exactly one primary e-mail.",
                    required = false,
                    mutability = "readWrite",
                    returned = "default",
                    subAttributes = new object[]
                    {
                        Simple("value", "string", required: false, mutable: true, caseExact: false),
                        Simple("type", "string", required: false, mutable: true, caseExact: true),
                        Simple("primary", "boolean", required: false, mutable: true)
                    }
                },
                new
                {
                    name = "phoneNumbers",
                    type = "complex",
                    multiValued = true,
                    description = "Phone numbers. The provider stores exactly one number.",
                    required = false,
                    mutability = "readWrite",
                    returned = "default",
                    subAttributes = new object[]
                    {
                        Simple("value", "string", required: false, mutable: true, caseExact: false),
                        Simple("type", "string", required: false, mutable: true, caseExact: true)
                    }
                },
                Simple("groups", "complex", required: false, mutable: false,
                    description: "Groups the user belongs to; read-only in this provider.")
            }
        };
    }

    private static object BuildGroupSchema()
    {
        return new Dictionary<string, object?>
        {
            ["schemas"] = new[] { ScimConstants.SchemaSchemaUrn },
            ["id"] = ScimConstants.GroupSchemaUrn,
            ["name"] = "Group",
            ["description"] = "Group, mapped to a tenant role with provisioned membership.",
            ["attributes"] = new object[]
            {
                Simple("displayName", "string", required: true, mutable: true, caseExact: false,
                    description: "Human-readable group name, mapped to the tenant role name."),
                Simple("externalId", "string", required: false, mutable: true, caseExact: true,
                    description: "Identifier supplied by the provisioning client; unique per tenant and used for idempotent creation."),
                new
                {
                    name = "members",
                    type = "complex",
                    multiValued = true,
                    description = "Group members. 'value' carries the platform user id.",
                    required = false,
                    mutability = "readWrite",
                    returned = "default",
                    subAttributes = new object[]
                    {
                        Simple("value", "string", required: true, mutable: true, caseExact: true),
                        Simple("display", "string", required: false, mutable: false, caseExact: false)
                    }
                }
            }
        };
    }

    private static object Simple(
        string name,
        string type,
        bool required,
        bool mutable,
        bool caseExact = false,
        string? description = null)
    {
        return new Dictionary<string, object?>
        {
            ["name"] = name,
            ["type"] = type,
            ["multiValued"] = false,
            ["description"] = description ?? name,
            ["required"] = required,
            ["mutability"] = mutable ? "readWrite" : "readOnly",
            ["returned"] = "default",
            ["caseExact"] = caseExact
        };
    }
}
