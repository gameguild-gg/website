using System.Text.Json.Nodes;

namespace GameGuild.Identity.Provisioning.Scim.Patch;

/// <summary>
///     Mutable state of a provisioned group that PATCH operations edit. Members is the
/// desired full member list after the operation sequence; the service reconciles it
/// against the actual memberships.
/// </summary>
public sealed class ScimGroupMutableState
{
    public string? DisplayName { get; set; }

    public string? ExternalId { get; set; }

    public List<string> MemberUserIds { get; } = [];
}

/// <summary>
///     RFC 7644 §3.5.2 PATCH semantics for the implemented Group attributes, including
///     the <c>members</c> paths used by provisioning engines:
///     <c>add … path=members</c>, <c>remove … path=members</c>,
///     <c>remove … path=members[value eq "&lt;userId&gt;"]</c> and
///     <c>replace … path=members</c>.
/// </summary>
public static class ScimGroupPatchApplier
{
    public static void Apply(ScimGroupMutableState state, IEnumerable<ScimPatchOperation> operations)
    {
        foreach (var operation in operations)
        {
            ApplyOne(state, operation);
        }
    }

    private static void ApplyOne(ScimGroupMutableState state, ScimPatchOperation operation)
    {
        var op = operation.NormalizeOp();

        if (string.IsNullOrWhiteSpace(operation.Path))
        {
            ApplyPathlessOperation(state, op, operation.Value);
            return;
        }

        var path = ScimPatchPath.Parse(operation.Path!);

        if (path.Attribute.Equals("members", StringComparison.OrdinalIgnoreCase))
        {
            ApplyMembersOperation(state, op, path, operation);
            return;
        }

        if (path.HasFilter)
        {
            throw ScimException.InvalidPath($"Value filters on '{operation.Path}' are not supported for this attribute.");
        }

        switch (op)
        {
            case "add":
            case "replace":
                if (operation.Value is null)
                {
                    throw ScimException.InvalidValue("The add and replace patch operations require a value.");
                }

                SetScalar(state, path, operation.Value);
                break;
            case "remove":
                if (operation.Value is not null)
                {
                    throw ScimException.InvalidValue("The remove operation must not carry a value.");
                }

                RemoveScalar(state, path);
                break;
        }
    }

    private static void ApplyPathlessOperation(ScimGroupMutableState state, string op, JsonNode? value)
    {
        if (op == "remove")
        {
            throw ScimException.InvalidPath("The remove operation requires a path.");
        }

        if (value is not JsonObject attributes)
        {
            throw ScimException.InvalidValue("A patch operation without a path requires an object value.");
        }

        foreach (var (attributeName, attributeValue) in attributes)
        {
            var path = ScimPatchPath.Parse(attributeName);
            if (path.Attribute.Equals("members", StringComparison.OrdinalIgnoreCase))
            {
                ApplyMembersOperation(state, op, path, attributeName, attributeValue ?? null);
                continue;
            }

            if (path.HasFilter)
            {
                throw ScimException.InvalidPath($"Value filters on '{attributeName}' are not supported for this attribute.");
            }

            if (attributeValue is null)
            {
                throw ScimException.InvalidValue($"The '{attributeName}' attribute requires a value.");
            }

            SetScalar(state, path, attributeValue);
        }
    }

    private static void ApplyMembersOperation(
        ScimGroupMutableState state,
        string op,
        ScimPatchPath path,
        ScimPatchOperation operation)
    {
        var value = operation.Value;
        if (path.SubAttribute is not null
            && !path.SubAttribute.Equals("value", StringComparison.OrdinalIgnoreCase))
        {
            throw ScimException.InvalidPath($"The members sub-attribute '{path.SubAttribute}' is not patchable.");
        }

        switch (op)
        {
            case "add":
            {
                if (value is null)
                {
                    throw ScimException.InvalidValue("Adding members requires a value.");
                }

                foreach (var member in ReadMemberIds(value))
                {
                    if (!state.MemberUserIds.Contains(member))
                    {
                        state.MemberUserIds.Add(member);
                    }
                }

                break;
            }

            case "replace":
            {
                if (value is null)
                {
                    throw ScimException.InvalidValue("Replacing members requires a value.");
                }

                state.MemberUserIds.Clear();
                state.MemberUserIds.AddRange(ReadMemberIds(value));
                break;
            }

            case "remove":
            {
                if (value is not null)
                {
                    throw ScimException.InvalidValue("The remove operation must not carry a value.");
                }

                if (path.HasFilter)
                {
                    if (!path.FilterAttribute!.Equals("value", StringComparison.OrdinalIgnoreCase))
                    {
                        throw ScimException.InvalidPath(
                            $"Only 'value' filters are supported on members, found '{path.FilterAttribute}'.");
                    }

                    state.MemberUserIds.RemoveAll(member => member.Equals(path.FilterValue, StringComparison.Ordinal));
                }
                else
                {
                    state.MemberUserIds.Clear();
                }

                break;
            }
        }
    }

    private static void ApplyMembersOperation(
        ScimGroupMutableState state,
        string op,
        ScimPatchPath path,
        string attributeName,
        JsonNode? value)
    {
        switch (op)
        {
            case "add":
            case "replace":
            {
                if (value is null)
                {
                    throw ScimException.InvalidValue($"The '{attributeName}' attribute requires a value.");
                }

                if (op == "add")
                {
                    foreach (var member in ReadMemberIds(value))
                    {
                        if (!state.MemberUserIds.Contains(member))
                        {
                            state.MemberUserIds.Add(member);
                        }
                    }
                }
                else
                {
                    state.MemberUserIds.Clear();
                    state.MemberUserIds.AddRange(ReadMemberIds(value));
                }

                break;
            }

            default:
                throw ScimException.InvalidPath(
                    "Removing members through a pathless operation is not supported; use path 'members' or 'members[value eq \"…\"]'.");
        }
    }

    private static void SetScalar(ScimGroupMutableState state, ScimPatchPath path, JsonNode value)
    {
        switch (path.Full.ToLowerInvariant())
        {
            case "displayname":
                state.DisplayName = ScimUserPatchApplier.ReadString(value, "displayName");
                break;
            case "externalid":
                state.ExternalId = ScimUserPatchApplier.ReadString(value, "externalId");
                break;
            default:
                throw ScimException.InvalidPath($"The attribute '{path.Full}' is not patchable on the Group resource.");
        }
    }

    private static void RemoveScalar(ScimGroupMutableState state, ScimPatchPath path)
    {
        switch (path.Full.ToLowerInvariant())
        {
            case "externalid":
                state.ExternalId = null;
                break;
            case "displayname":
                throw ScimException.Mutability("The required attribute 'displayName' cannot be removed.");
            default:
                throw ScimException.InvalidPath($"The attribute '{path.Full}' is not patchable on the Group resource.");
        }
    }

    internal static IEnumerable<string> ReadMemberIds(JsonNode value)
    {
        var entries = value switch
        {
            JsonArray array => array,
            JsonObject single => [single],
            _ => throw ScimException.InvalidValue("The 'members' attribute requires an array of member objects.")
        };

        foreach (var entry in entries)
        {
            if (entry is not JsonObject member)
            {
                throw ScimException.InvalidValue("Each member entry must be an object with a 'value' member.");
            }

            if (!member.TryGetPropertyValue("value", out var memberValue) || memberValue is null)
            {
                throw ScimException.InvalidValue("Each member entry requires a 'value' member.");
            }

            yield return ScimUserPatchApplier.ReadString(memberValue, "members.value");
        }
    }
}
