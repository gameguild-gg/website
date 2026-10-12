using System.Text.Json;
using System.Text.Json.Nodes;
using GameGuild.CQRS;
using Microsoft.Extensions.Logging;
using GameGuild.Identity.Provisioning.Scim;

namespace GameGuild.Identity.Provisioning;

/// <summary>
///     RFC 7644 §3.7 Bulk processor. Executes operations sequentially against the user
/// and group services, resolves <c>bulkId:</c> references between operations, honors
/// <c>failOnErrors</c>, and reports a per-operation status envelope.
/// </summary>
public sealed class ScimBulkProcessor(
    IScimProvisioningContext provisioningContext,
    ISender sender,
    Microsoft.Extensions.Options.IOptions<ScimProvisioningOptions> options,
    ILogger<ScimBulkProcessor> logger) : IScimBulkProcessor
{
    private static readonly JsonSerializerOptions PayloadOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public async Task<ScimBulkResponse> ProcessAsync(ScimBulkRequest request, CancellationToken cancellationToken)
    {
        var actor = ScimCommandGuards.RequireWrite(provisioningContext);

        var operations = request.Operations ?? Array.Empty<ScimBulkOperation>();
        if (operations.Count == 0)
        {
            throw ScimException.BadPayload("A bulk request requires at least one operation.");
        }

        if (operations.Count > options.Value.MaxBulkOperations)
        {
            throw new ScimException(413, "tooMany", $"Bulk requests are limited to {options.Value.MaxBulkOperations} operations.");
        }

        var failOnErrors = request.FailOnErrors ?? 0;
        var bulkIds = new Dictionary<string, string>(StringComparer.Ordinal);
        var responses = new List<ScimBulkResponseOperation>(operations.Count);
        var errorCount = 0;

        foreach (var operation in operations)
        {
            var response = await ExecuteOperationAsync(actor, operation, bulkIds, cancellationToken).ConfigureAwait(false);
            responses.Add(response);

            if (response.Status.Code >= 400)
            {
                errorCount++;
                if (failOnErrors > 0 && errorCount >= failOnErrors)
                {
                    break;
                }
            }
        }

        logger.LogInformation("SCIM bulk on tenant {TenantId}: {Total} operations, {Errors} errors",
            actor.TenantId, responses.Count, errorCount);

        return new ScimBulkResponse { Operations = responses };
    }

    private async Task<ScimBulkResponseOperation> ExecuteOperationAsync(
        ScimProvisioningActor actor,
        ScimBulkOperation operation,
        Dictionary<string, string> bulkIds,
        CancellationToken cancellationToken)
    {
        var method = (operation.Method ?? string.Empty).Trim().ToUpperInvariant();
        var path = operation.Path ?? string.Empty;
        try
        {
            return method switch
            {
                "POST" => await ExecutePostAsync(actor, operation, path, bulkIds, cancellationToken).ConfigureAwait(false),
                "PUT" or "PATCH" or "DELETE" => await ExecuteMutationAsync(actor, operation, method, path, bulkIds, cancellationToken).ConfigureAwait(false),
                _ => throw ScimException.BadPayload($"The bulk method '{operation.Method}' is not supported.")
            };
        }
        catch (ScimException exception)
        {
            return Failed(method, operation.BulkId, exception);
        }
    }

    private async Task<ScimBulkResponseOperation> ExecutePostAsync(
        ScimProvisioningActor actor,
        ScimBulkOperation operation,
        string path,
        Dictionary<string, string> bulkIds,
        CancellationToken cancellationToken)
    {
        if (IsUsersPath(path))
        {
            var payload = Deserialize<ScimUserRequest>(operation.Data)
                          ?? throw ScimException.BadPayload("The bulk POST data must be a User resource.");
            var result = await sender.Send(new ScimCreateUserCommand(payload), cancellationToken).ConfigureAwait(false);
            if (!result.IsSuccess)
            {
                return Failed("POST", operation.BulkId, 400, null, result.Error.Description);
            }

            RecordBulkId(bulkIds, operation.BulkId, result.Value.Resource.Id);
            return Succeeded("POST", operation.BulkId, result.Value.Created ? 201 : 200,
                $"{ScimConstants.UsersPath}/{result.Value.Resource.Id}", result.Value.Resource.Id);
        }

        if (IsGroupsPath(path))
        {
            var payload = Deserialize<ScimGroupRequest>(ResolveBulkReferences(operation.Data, bulkIds))
                          ?? throw ScimException.BadPayload("The bulk POST data must be a Group resource.");
            var result = await sender.Send(new ScimCreateGroupCommand(payload), cancellationToken).ConfigureAwait(false);
            if (!result.IsSuccess)
            {
                return Failed("POST", operation.BulkId, 400, null, result.Error.Description);
            }

            RecordBulkId(bulkIds, operation.BulkId, result.Value.Resource.Id);
            return Succeeded("POST", operation.BulkId, result.Value.Created ? 201 : 200,
                $"{ScimConstants.GroupsPath}/{result.Value.Resource.Id}", result.Value.Resource.Id);
        }

        throw ScimException.BadPayload($"The bulk path '{path}' is not a provisionable endpoint.");
    }

    private async Task<ScimBulkResponseOperation> ExecuteMutationAsync(
        ScimProvisioningActor actor,
        ScimBulkOperation operation,
        string method,
        string path,
        Dictionary<string, string> bulkIds,
        CancellationToken cancellationToken)
    {
        var (resourcePath, resourceId) = SplitPath(path);
        if (!Guid.TryParse(ResolveId(resourceId, bulkIds), out var id))
        {
            throw ScimException.BadPayload($"The bulk path '{path}' does not carry a valid resource id.");
        }

        if (IsUsersPath(resourcePath))
        {
            switch (method)
            {
                case "PUT":
                {
                    var payload = Deserialize<ScimUserRequest>(operation.Data)
                                  ?? throw ScimException.BadPayload("The bulk PUT data must be a User resource.");
                    var result = await sender.Send(new ScimReplaceUserCommand(id, payload), cancellationToken).ConfigureAwait(false);
                    return result.IsSuccess
                        ? Succeeded(method, operation.BulkId, 200, $"{ScimConstants.UsersPath}/{id}", id.ToString())
                        : Failed(method, operation.BulkId, 400, null, result.Error.Description);
                }
                case "PATCH":
                {
                    var payload = Deserialize<ScimPatchRequest>(operation.Data)
                                  ?? throw ScimException.BadPayload("The bulk PATCH data must be a PatchOp document.");
                    var result = await sender.Send(new ScimPatchUserCommand(id, payload), cancellationToken).ConfigureAwait(false);
                    return result.IsSuccess
                        ? Succeeded(method, operation.BulkId, 200, $"{ScimConstants.UsersPath}/{id}", id.ToString())
                        : Failed(method, operation.BulkId, 400, null, result.Error.Description);
                }
                case "DELETE":
                {
                    var result = await sender.Send(new ScimDeleteUserCommand(id), cancellationToken).ConfigureAwait(false);
                    return result.IsSuccess
                        ? Succeeded(method, operation.BulkId, 204, $"{ScimConstants.UsersPath}/{id}", id.ToString())
                        : Failed(method, operation.BulkId, 400, null, result.Error.Description);
                }
            }
        }

        if (IsGroupsPath(resourcePath))
        {
            switch (method)
            {
                case "PUT":
                {
                    var payload = Deserialize<ScimGroupRequest>(ResolveBulkReferences(operation.Data, bulkIds))
                                  ?? throw ScimException.BadPayload("The bulk PUT data must be a Group resource.");
                    var result = await sender.Send(new ScimReplaceGroupCommand(id, payload), cancellationToken).ConfigureAwait(false);
                    return result.IsSuccess
                        ? Succeeded(method, operation.BulkId, 200, $"{ScimConstants.GroupsPath}/{id}", id.ToString())
                        : Failed(method, operation.BulkId, 400, null, result.Error.Description);
                }
                case "PATCH":
                {
                    var payload = Deserialize<ScimPatchRequest>(ResolveBulkReferences(operation.Data, bulkIds))
                                  ?? throw ScimException.BadPayload("The bulk PATCH data must be a PatchOp document.");
                    var result = await sender.Send(new ScimPatchGroupCommand(id, payload), cancellationToken).ConfigureAwait(false);
                    return result.IsSuccess
                        ? Succeeded(method, operation.BulkId, 200, $"{ScimConstants.GroupsPath}/{id}", id.ToString())
                        : Failed(method, operation.BulkId, 400, null, result.Error.Description);
                }
                case "DELETE":
                {
                    var result = await sender.Send(new ScimDeleteGroupCommand(id), cancellationToken).ConfigureAwait(false);
                    return result.IsSuccess
                        ? Succeeded(method, operation.BulkId, 204, $"{ScimConstants.GroupsPath}/{id}", id.ToString())
                        : Failed(method, operation.BulkId, 400, null, result.Error.Description);
                }
            }
        }

        throw ScimException.BadPayload($"The bulk path '{path}' is not a provisionable endpoint.");
    }

    private static T? Deserialize<T>(JsonNode? node)
        where T : class
        => node is null ? null : node.Deserialize<T>(PayloadOptions);

    /// <summary>Replaces <c>bulkId:</c> member references with the created resource ids.</summary>
    private static JsonNode? ResolveBulkReferences(JsonNode? node, IReadOnlyDictionary<string, string> bulkIds)
    {
        if (node is null || bulkIds.Count == 0)
        {
            return node;
        }

        var serialized = node.ToJsonString(PayloadOptions);
        foreach (var (bulkId, resourceId) in bulkIds)
        {
            serialized = serialized.Replace($"bulkId:{bulkId}", resourceId, StringComparison.Ordinal);
        }

        return JsonNode.Parse(serialized);
    }

    private static void RecordBulkId(Dictionary<string, string> bulkIds, string? bulkId, string resourceId)
    {
        if (!string.IsNullOrWhiteSpace(bulkId))
        {
            bulkIds[bulkId] = resourceId;
        }
    }

    private static (string ResourcePath, string ResourceId) SplitPath(string path)
    {
        var trimmed = path.Trim('/');
        var separator = trimmed.LastIndexOf('/');
        if (separator < 0)
        {
            return (trimmed, string.Empty);
        }

        return (trimmed[..separator], trimmed[(separator + 1)..]);
    }

    private static string ResolveId(string resourceId, IReadOnlyDictionary<string, string> bulkIds)
        => bulkIds.TryGetValue(resourceId, out var resolved) ? resolved : resourceId;

    private static bool IsUsersPath(string path)
        // Bulk paths appear both with a leading slash ("/Users" for POST) and split
        // without one ("Users" after SplitPath on "/Users/{id}"); compare the trimmed
        // segment so mutation operations route to the right service.
        => path.Trim('/').Equals("Users", StringComparison.OrdinalIgnoreCase);

    private static bool IsGroupsPath(string path)
        => path.Trim('/').Equals("Groups", StringComparison.OrdinalIgnoreCase);

    private static ScimBulkResponseOperation Succeeded(string method, string? bulkId, int code, string location, string version)
        => new()
        {
            Method = method,
            BulkId = bulkId,
            Location = location,
            Version = version,
            Status = new ScimBulkStatus { Code = code }
        };

    private static ScimBulkResponseOperation Failed(string method, string? bulkId, ScimException exception)
        => Failed(method, bulkId, exception.Status, exception.ScimType, exception.Detail);

    private static ScimBulkResponseOperation Failed(string method, string? bulkId, int code, string? scimType, string? detail)
        => new()
        {
            Method = method,
            BulkId = bulkId,
            Status = new ScimBulkStatus { Code = code, ScimType = scimType, Detail = detail }
        };
}
