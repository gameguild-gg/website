namespace GameGuild.Identity.Provisioning;

/// <summary>
///     URN constants from RFC 7643/7644 used across the SCIM provisioning surface.
/// </summary>
public static class ScimConstants
{
    public const string MessageSchema = "urn:ietf:params:scim:api:messages:2.0";
    public const string ErrorSchema = "urn:ietf:params:scim:api:messages:2.0:Error";
    public const string ListResponseSchema = "urn:ietf:params:scim:api:messages:2.0:ListResponse";
    public const string PatchOperationSchema = "urn:ietf:params:scim:api:messages:2.0:PatchOp";
    public const string BulkRequestSchema = "urn:ietf:params:scim:api:messages:2.0:BulkRequest";
    public const string BulkResponseSchema = "urn:ietf:params:scim:api:messages:2.0:BulkResponse";
    public const string UserSchemaUrn = "urn:ietf:params:scim:schemas:core:2.0:User";
    public const string GroupSchemaUrn = "urn:ietf:params:scim:schemas:core:2.0:Group";
    public const string ResourceTypeSchemaUrn = "urn:ietf:params:scim:schemas:core:2.0:ResourceType";
    public const string SchemaSchemaUrn = "urn:ietf:params:scim:schemas:core:2.0:Schema";
    public const string ServiceProviderConfigSchemaUrn = "urn:ietf:params:scim:schemas:core:2.0:ServiceProviderConfig";

    public const string UsersPath = "/scim/v2/Users";
    public const string GroupsPath = "/scim/v2/Groups";

    /// <summary>Sort order used for SCIM list responses (stable, by resource id).</summary>
    public const string DefaultSortOrder = "id";
}
