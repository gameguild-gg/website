using System.Text;
using System.Text.Json;
using GameGuild.Configuration.PresentationLayer.OpenAPI;
using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace GameGuild.API;

/// <summary>Applies configured extension data and schema documentation to generated documents.</summary>
internal sealed class ConfiguredOpenApiDocumentFilter(OpenApiOptions options) : IDocumentFilter
{
    private static readonly IReadOnlyDictionary<string, string> DefaultExamples =
        new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["Identity_Authentication_ApiKeyDto"] = """{"id":"00000000-0000-0000-0000-000000000001","name":"Reporting integration","keyPrefix":"gg_example","scopes":["reports:read"],"isActive":true,"expiresAt":"2027-01-01T00:00:00Z","lastUsedAt":null,"usageCount":12,"createdAt":"2026-10-01T12:00:00Z"}""",
        ["Identity_Authentication_CreateApiKeyCommand"] = """{"name":"Reporting integration","scopes":["reports:read"],"expiresAt":"2027-01-01T00:00:00Z","ipWhitelist":"192.0.2.10"}""",
        ["Identity_Authentication_CreateApiKeyResponse"] = """{"id":"00000000-0000-0000-0000-000000000001","name":"Reporting integration","apiKey":"gg_example_not-a-valid-secret","keyPrefix":"gg_example","scopes":["reports:read"],"expiresAt":"2027-01-01T00:00:00Z","createdAt":"2026-10-01T12:00:00Z"}""",
        ["Identity_Authentication_CreateStepUpChallengeRequest"] = """{"operationType":"billing.refund","targetReference":"refund_123","payloadHash":"sha256-example-hash"}""",
        ["Identity_Authentication_MagicLinkRequestResult"] = """{"success":true,"message":"If the account exists, a sign-in link has been sent.","expiresInMinutes":10,"developmentPreviewToken":"sample-preview-token-not-valid"}""",
        ["Identity_Authentication_RotateApiKeyRequest"] = """{"name":"Reporting integration v2","scopes":["reports:read"],"expiresAt":"2027-07-01T00:00:00Z","gracePeriodMinutes":60}""",
        ["Identity_Authentication_RotateApiKeyResponse"] = """{"oldKeyId":"00000000-0000-0000-0000-000000000009","newKey":{"id":"00000000-0000-0000-0000-000000000001","name":"Reporting integration v2","apiKey":"gg_example_not-a-valid-secret","keyPrefix":"gg_example","scopes":["reports:read"],"expiresAt":"2027-07-01T00:00:00Z","createdAt":"2026-10-08T12:00:00Z"},"oldKeyGraceEndsAt":"2026-10-08T13:00:00Z","oldKeyRevoked":false}""",
        ["Identity_Authentication_RevokeApiKeyRequest"] = """{"reason":"The integration is no longer in use."}""",
        ["Identity_Authentication_StepUpChallengeResponse"] = """{"challengeId":"00000000-0000-0000-0000-000000000002","expiresAt":"2026-10-01T12:05:00Z"}""",
        ["Identity_Authentication_StepUpReceiptResponse"] = """{"receipt":"sample-receipt-not-valid","expiresAt":"2026-10-01T12:05:00Z"}""",
        ["Identity_Authentication_VerifyStepUpChallengeRequest"] = """{"method":"Totp","evidence":"000000"}""",
        ["Identity_Authorization_Controllers_ApproveElevationRequest"] = """{"reviewerId":"00000000-0000-0000-0000-000000000003","comments":"Approved for the scheduled maintenance window."}""",
        ["Identity_Authorization_Controllers_ApproveItemRequest"] = """{"reason":"Access remains required for the current role.","notes":"Reviewed on 2026-10-01."}""",
        ["Identity_Authorization_Controllers_CompleteCampaignRequest"] = """{"completedBy":"00000000-0000-0000-0000-000000000003"}""",
        ["Identity_Authorization_Controllers_DenyElevationRequest"] = """{"reviewerId":"00000000-0000-0000-0000-000000000003","comments":"The requested scope exceeds the approved maintenance window."}""",
        ["Identity_Authorization_Controllers_GrantExceptionRequest"] = """{"approvedBy":"00000000-0000-0000-0000-000000000003","justification":"Temporary exception for an audited migration."}""",
        ["Identity_Authorization_Controllers_ResolveViolationRequest"] = """{"resolvedBy":"00000000-0000-0000-0000-000000000003","action":"RevokePermission","notes":"Conflicting permission was removed."}""",
        ["Identity_Authorization_Controllers_RevokeElevationRequest"] = """{"revokedBy":"00000000-0000-0000-0000-000000000003","reason":"The maintenance window has ended."}""",
        ["Identity_Authorization_Controllers_RevokeItemRequest"] = """{"reason":"The access is no longer needed.","notes":"Confirmed with the resource owner."}""",
        ["Identity_Authorization_Controllers_UpdateSoDRuleRequest"] = """{"name":"Refund approval separation","description":"A requester cannot also approve the same refund.","conflictingPermissions":["billing:refund","billing:approve-refund"],"ruleType":"PermissionConflict","isEnabled":true}""",
        ["Identity_Authorization_PermissionAnalyticsReport"] = """{"tenantId":"00000000-0000-0000-0000-000000000004","periodStart":"2026-09-01T00:00:00Z","periodEnd":"2026-10-01T00:00:00Z","totalGrants":18,"totalRevokes":4,"activeUsers":12,"topPermissions":[],"topUsers":[],"anomalies":[]}""",
        ["Identity_Authorization_PermissionAnomaly"] = """{"userId":"00000000-0000-0000-0000-000000000005","anomalyType":"UnusualGrantRate","description":"Grant activity exceeded the configured baseline.","detectedAt":"2026-10-01T12:00:00Z","severity":"Medium"}""",
        ["Identity_Authorization_PermissionTrend"] = """{"date":"2026-10-01T00:00:00Z","grants":8,"revokes":2,"activePermissions":34}""",
        ["Identity_Authorization_PermissionUsageMetrics"] = """{"permission":"reports:read","usageCount":187,"uniqueUsers":23,"lastUsed":"2026-10-01T11:45:00Z"}""",
        ["Identity_Authorization_ResourceAccessPattern"] = """{"resourceId":"00000000-0000-0000-0000-000000000006","resourceType":"report","accessCount":54,"uniqueUsers":9}""",
        ["Identity_Authorization_UserActivitySummary"] = """{"userId":"00000000-0000-0000-0000-000000000005","totalActions":31,"permissionChanges":3,"lastActivity":"2026-10-01T11:45:00Z"}""",
        ["Identity_Tenants_SetTenantMembershipStatusRequest"] = """{"reason":"Access suspended during an account review."}""",
        ["Identity_Tenants_SetTenantMembershipStatusResponse"] = """{"success":true,"notFound":false,"message":"Membership status updated.","memberId":"00000000-0000-0000-0000-000000000007","isActive":false}""",
        ["Identity_Tenants_UpdateTenantMemberInviteResponse"] = """{"success":true,"message":"Invitation updated.","memberId":"00000000-0000-0000-0000-000000000007","inviteStatus":"Pending"}""",
        ["Learning_Courses_ActivitySettings"] = """{"kind":"discussion","allowReplies":true,"requireThreadRoot":false,"minimumBodyLength":1,"maximumBodyLength":10000}"""
    };

    public void Apply(OpenApiDocument document, DocumentFilterContext context)
    {
        foreach (var (name, json) in options.Extensions.OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            document.Extensions[name] = ParseJson(json);
        }

        if (document.Components?.Schemas is not { } schemas)
        {
            return;
        }

        foreach (var (schemaId, configured) in options.Schemas)
        {
            if (!schemas.TryGetValue(schemaId, out var schema))
            {
                continue;
            }

            if (!string.IsNullOrWhiteSpace(configured.Description))
            {
                schema.Description = configured.Description;
            }

            if (!string.IsNullOrWhiteSpace(configured.ExampleJson))
            {
                schema.Example = ParseJson(configured.ExampleJson);
            }

            foreach (var (propertyName, propertyOptions) in configured.Properties)
            {
                if (schema.Properties?.TryGetValue(propertyName, out var property) != true || property is null)
                {
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(propertyOptions.Description))
                {
                    property.Description = propertyOptions.Description;
                }

                if (!string.IsNullOrWhiteSpace(propertyOptions.ExampleJson))
                {
                    property.Example = ParseJson(propertyOptions.ExampleJson);
                }
            }
        }

        foreach (var (schemaId, schema) in schemas)
        {
            if (string.IsNullOrWhiteSpace(schema.Description))
            {
                var modelName = HumanizeSchemaId(schemaId);
                schema.Description = schema.Type == "object" || schema.Properties?.Count > 0
                    ? $"Data model for {modelName}."
                    : $"OpenAPI schema for {modelName}.";
            }

            if (schema.Example is null or OpenApiNull)
            {
                schema.Example = DefaultExamples.TryGetValue(schemaId, out var exampleJson)
                    ? ParseJson(exampleJson)
                    : CreateExample(schema, schemas, new HashSet<string>(StringComparer.Ordinal) { schemaId });
            }

        }
    }

    private static IOpenApiAny ParseJson(string json)
    {
        using var parsed = JsonDocument.Parse(json);
        return ConvertValue(parsed.RootElement);
    }

    private static IOpenApiAny ConvertValue(JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                var objectValue = new OpenApiObject();
                foreach (var property in element.EnumerateObject())
                {
                    objectValue[property.Name] = ConvertValue(property.Value);
                }
                return objectValue;
            case JsonValueKind.Array:
                var arrayValue = new OpenApiArray();
                foreach (var item in element.EnumerateArray())
                {
                    arrayValue.Add(ConvertValue(item));
                }
                return arrayValue;
            case JsonValueKind.String:
                return new OpenApiString(element.GetString());
            case JsonValueKind.Number when element.TryGetInt32(out var integer):
                return new OpenApiInteger(integer);
            case JsonValueKind.Number when element.TryGetInt64(out var longInteger):
                return new OpenApiLong(longInteger);
            case JsonValueKind.Number:
                return new OpenApiDouble(element.GetDouble());
            case JsonValueKind.True:
                return new OpenApiBoolean(true);
            case JsonValueKind.False:
                return new OpenApiBoolean(false);
            case JsonValueKind.Null:
                return new OpenApiNull();
            default:
                throw new InvalidOperationException("Unsupported OpenAPI extension JSON value.");
        }
    }

    private static IOpenApiAny CreateExample(
        OpenApiSchema schema,
        IDictionary<string, OpenApiSchema> schemas,
        HashSet<string> activeReferences,
        int depth = 0)
    {
        if (schema.Reference?.Id is { } referenceId)
        {
            if (!schemas.TryGetValue(referenceId, out var referencedSchema))
            {
                return new OpenApiObject();
            }

            if (!activeReferences.Add(referenceId))
            {
                return referencedSchema.Type == "array" ? new OpenApiArray() : new OpenApiObject();
            }

            try
            {
                return CreateExample(referencedSchema, schemas, activeReferences, depth);
            }
            finally
            {
                activeReferences.Remove(referenceId);
            }
        }

        if (schema.Default is not null and not OpenApiNull)
        {
            return schema.Default;
        }

        if (schema.Enum is { Count: > 0 })
        {
            var example = schema.Enum.FirstOrDefault(value => value is not OpenApiNull);
            if (example is not null)
            {
                return example;
            }
        }

        if (schema.OneOf is { Count: > 0 })
        {
            return CreateExample(schema.OneOf[0], schemas, activeReferences, depth + 1);
        }

        if (schema.Type == "object" || schema.Properties?.Count > 0)
        {
            var value = new OpenApiObject();
            if (depth >= 2 || schema.Properties is not { Count: > 0 } properties)
            {
                return value;
            }

            foreach (var (name, property) in properties.OrderBy(pair => pair.Key, StringComparer.Ordinal))
            {
                value[name] = CreateExample(property, schemas, activeReferences, depth + 1);
            }

            return value;
        }

        if (schema.Type == "array")
        {
            return new OpenApiArray();
        }

        if (schema.Type == "boolean")
        {
            return new OpenApiBoolean(true);
        }

        if (schema.Type == "integer")
        {
            return schema.Format == "int64" ? new OpenApiLong(1) : new OpenApiInteger(1);
        }

        if (schema.Type == "number")
        {
            return new OpenApiDouble(1.0);
        }

        if (schema.Type == "string")
        {
            return new OpenApiString(schema.Format switch
            {
                "date-time" => "2026-10-01T12:00:00Z",
                "date" => "2026-10-01",
                "time" => "12:00:00Z",
                "uuid" => "00000000-0000-0000-0000-000000000001",
                "email" => "developer@example.com",
                "uri" or "url" => "https://example.com/resource",
                "byte" => "ZXhhbXBsZQ==",
                _ => "example"
            });
        }

        return new OpenApiObject();
    }

    private static string HumanizeSchemaId(string schemaId)
    {
        var text = new StringBuilder(schemaId.Length + 16);
        for (var index = 0; index < schemaId.Length; index++)
        {
            var character = schemaId[index];
            if (character is '_' or '-' or '.')
            {
                text.Append(' ');
                continue;
            }

            if (index > 0 && char.IsUpper(character)
                && (char.IsLower(schemaId[index - 1]) || char.IsDigit(schemaId[index - 1])))
            {
                text.Append(' ');
            }

            text.Append(character);
        }

        return string.Join(' ', text.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }
}
