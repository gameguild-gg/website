using System.Text.Json.Nodes;

namespace GameGuild.Identity.Provisioning.Scim.Patch;

/// <summary>
///     Mutable state of a provisioned user that PATCH operations edit. The service
/// loads the current values, applies the operation list, then materializes the result.
/// </summary>
public sealed class ScimUserMutableState
{
    public string? UserName { get; set; }

    public string? DisplayName { get; set; }

    /// <summary>
    ///     Whether displayName (or name.formatted) was explicitly provided by the current
    ///     request/patch. An explicit displayName wins over composing from name parts;
    ///     without this flag the stored parts would silently overwrite a patched value.
    /// </summary>
    public bool DisplayNameExplicit { get; set; }

    public string? GivenName { get; set; }

    public string? FamilyName { get; set; }

    public string? PrimaryEmail { get; set; }

    public string? PhoneNumber { get; set; }

    public bool? Active { get; set; }

    public string? ExternalId { get; set; }
}

/// <summary>
///     RFC 7644 §3.5.2 PATCH semantics for the implemented User attributes. Supports
///     no-path object values plus simple and dotted paths; value-filter paths (for
///     example <c>emails[type eq "work"]</c>) are rejected with <c>invalidPath</c>
///     because the provider stores a single primary e-mail.
/// </summary>
public static class ScimUserPatchApplier
{
    public static void Apply(ScimUserMutableState state, IEnumerable<ScimPatchOperation> operations)
    {
        foreach (var operation in operations)
        {
            ApplyOne(state, operation);
        }
    }

    private static void ApplyOne(ScimUserMutableState state, ScimPatchOperation operation)
    {
        var op = operation.NormalizeOp();

        if (string.IsNullOrWhiteSpace(operation.Path))
        {
            ApplyPathlessOperation(state, op, operation.Value);
            return;
        }

        var path = ScimPatchPath.Parse(operation.Path!);
        if (path.HasFilter)
        {
            throw ScimException.InvalidPath(
                $"Value filters on '{operation.Path}' are not supported; the provider stores a single value per multi-valued attribute.");
        }

        switch (op)
        {
            case "add":
            case "replace":
                SetAttribute(state, path, RequireValue(operation));
                break;
            case "remove":
                if (operation.Value is not null)
                {
                    throw ScimException.InvalidValue("The remove operation must not carry a value.");
                }

                RemoveAttribute(state, path);
                break;
        }
    }

    private static void ApplyPathlessOperation(ScimUserMutableState state, string op, JsonNode? value)
    {
        // RFC 7644 §3.5.2.2: the path is REQUIRED for remove, so a pathless remove is
        // invalidPath regardless of the value shape (this guard must precede the
        // object-value requirement, mirroring the group applier).
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
            if (path.HasFilter)
            {
                throw ScimException.InvalidPath(
                    $"Value filters on '{attributeName}' are not supported; the provider stores a single value per multi-valued attribute.");
            }

            SetAttribute(state, path, attributeValue ?? throw ScimException.InvalidValue($"The '{attributeName}' attribute requires a value."));
        }
    }

    private static JsonNode RequireValue(ScimPatchOperation operation)
        => operation.Value ?? throw ScimException.InvalidValue("The add and replace patch operations require a value.");

    private static void SetAttribute(ScimUserMutableState state, ScimPatchPath path, JsonNode value)
    {
        switch (path.Full.ToLowerInvariant())
        {
            case "username":
                state.UserName = ReadString(value, "userName");
                break;
            case "displayname":
                state.DisplayName = ReadString(value, "displayName");
                state.DisplayNameExplicit = true;
                break;
            case "externalid":
                state.ExternalId = ReadString(value, "externalId");
                break;
            case "active":
                state.Active = ReadBoolean(value, "active");
                break;
            case "name":
                ApplyNameObject(state, value);
                break;
            case "name.givenname":
                state.GivenName = ReadString(value, "name.givenName");
                break;
            case "name.familyname":
                state.FamilyName = ReadString(value, "name.familyName");
                break;
            case "name.formatted":
                state.DisplayName = ReadString(value, "name.formatted");
                state.DisplayNameExplicit = true;
                break;
            case "emails":
            case "emails.value":
                state.PrimaryEmail = ReadPrimaryEmail(value);
                break;
            case "phonenumbers":
            case "phonenumbers.value":
                state.PhoneNumber = ReadPrimaryPhoneNumber(value);
                break;
            default:
                throw ScimException.InvalidPath($"The attribute '{path.Full}' is not patchable on the User resource.");
        }
    }

    private static void RemoveAttribute(ScimUserMutableState state, ScimPatchPath path)
    {
        switch (path.Full.ToLowerInvariant())
        {
            case "displayname":
                state.DisplayName = null;
                break;
            case "name":
                state.GivenName = null;
                state.FamilyName = null;
                break;
            case "name.givenname":
                state.GivenName = null;
                break;
            case "name.familyname":
                state.FamilyName = null;
                break;
            case "name.formatted":
                state.DisplayName = null;
                break;
            case "phonenumbers":
            case "phonenumbers.value":
                state.PhoneNumber = null;
                break;
            case "username":
            case "emails":
            case "emails.value":
            case "externalid":
                throw ScimException.Mutability(
                    $"The required attribute '{path.Full}' cannot be removed.");
            default:
                throw ScimException.InvalidPath($"The attribute '{path.Full}' is not patchable on the User resource.");
        }
    }

    private static void ApplyNameObject(ScimUserMutableState state, JsonNode value)
    {
        if (value is not JsonObject name)
        {
            throw ScimException.InvalidValue("The 'name' attribute requires an object value.");
        }

        foreach (var (part, partValue) in name)
        {
            switch (part.ToLowerInvariant())
            {
                case "givenname":
                    state.GivenName = partValue is null ? null : ReadString(partValue, "name.givenName");
                    break;
                case "familyname":
                    state.FamilyName = partValue is null ? null : ReadString(partValue, "name.familyName");
                    break;
                case "formatted":
                    state.DisplayName = partValue is null ? null : ReadString(partValue, "name.formatted");
                    state.DisplayNameExplicit = true;
                    break;
                case "middlename":
                case "honorificprefix":
                case "honorificsuffix":
                    break; // accepted and ignored (not stored)
                default:
                    throw ScimException.InvalidPath($"The name sub-attribute '{part}' is not patchable.");
            }
        }
    }

    internal static string? ReadPrimaryEmail(JsonNode value)
    {
        var primary = ReadMultiValued(value, "emails");
        return primary;
    }

    internal static string? ReadPrimaryPhoneNumber(JsonNode value)
    {
        return ReadMultiValued(value, "phoneNumbers");
    }

    private static string? ReadMultiValued(JsonNode value, string attributeName)
    {
        switch (value)
        {
            case JsonArray array:
            {
                if (array.Count > 1)
                {
                    throw ScimException.InvalidValue(
                        $"Only one {attributeName} entry is supported; the provider stores a single value.");
                }

                return array.Count == 0 ? null : ReadEntryValue(array[0], attributeName);
            }

            case JsonObject:
                return ReadEntryValue(value, attributeName);
            case JsonValue scalar when scalar.TryGetValue<string>(out var text):
                return text;
            default:
                throw ScimException.InvalidValue($"The '{attributeName}' attribute requires an object or array value.");
        }
    }

    private static string? ReadEntryValue(JsonNode? node, string attributeName)
    {
        if (node is not JsonObject entry)
        {
            throw ScimException.InvalidValue($"Each '{attributeName}' entry must be an object with a 'value' member.");
        }

        return entry.TryGetPropertyValue("value", out var entryValue) && entryValue is not null
            ? ReadString(entryValue, $"{attributeName}.value")
            : null;
    }

    internal static string ReadString(JsonNode value, string attributeName)
    {
        if (value is JsonValue scalar && scalar.TryGetValue<string>(out var text) && text is not null)
        {
            return text;
        }

        throw ScimException.InvalidValue($"The '{attributeName}' attribute requires a string value.");
    }

    internal static bool ReadBoolean(JsonNode value, string attributeName)
    {
        if (value is JsonValue scalar && scalar.TryGetValue<bool>(out var flag))
        {
            return flag;
        }

        if (value is JsonValue text && text.TryGetValue<string>(out var raw) && bool.TryParse(raw, out var parsed))
        {
            return parsed;
        }

        throw ScimException.InvalidValue($"The '{attributeName}' attribute requires a boolean value.");
    }
}

/// <summary>Parsed PATCH path: attribute with optional sub-attribute and value filter.</summary>
public sealed record ScimPatchPath
{
    public ScimPatchPath(string attribute, string? subAttribute, string? filterAttribute, string? filterValue)
    {
        Attribute = attribute;
        SubAttribute = subAttribute;
        FilterAttribute = filterAttribute;
        FilterValue = filterValue;
        Full = subAttribute is null ? attribute : $"{attribute}.{subAttribute}";
    }

    public string Attribute { get; }

    public string? SubAttribute { get; }

    public string? FilterAttribute { get; }

    public string? FilterValue { get; }

    public string Full { get; }

    public bool HasFilter => FilterAttribute is not null;

    /// <summary>
    ///     Parses <c>attr</c>, <c>attr.sub</c> and <c>attr[sub eq "value"]</c> forms.
    ///     Only <c>eq</c> value filters are accepted per RFC 7644 §3.5.2.
    /// </summary>
    public static ScimPatchPath Parse(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var open = path.IndexOf('[', StringComparison.Ordinal);
        if (open < 0)
        {
            var parts = path.Split('.', 2);
            return new ScimPatchPath(parts[0], parts.Length == 2 ? parts[1] : null, null, null);
        }

        var close = path.IndexOf(']', StringComparison.Ordinal);
        if (close < 0 || close < open)
        {
            throw ScimException.InvalidPath($"The patch path '{path}' has an unterminated value filter.");
        }

        var attribute = path[..open];
        var attributeParts = attribute.Split('.', 2);
        var filter = path[(open + 1)..close].Trim();

        const string EqSeparator = " eq ";
        var separatorIndex = filter.IndexOf(EqSeparator, StringComparison.OrdinalIgnoreCase);
        if (separatorIndex < 0)
        {
            throw ScimException.InvalidPath(
                $"Only 'eq' value filters are supported in patch paths, found '{filter}'.");
        }

        var filterAttribute = filter[..separatorIndex].Trim();
        var filterValue = filter[(separatorIndex + EqSeparator.Length)..].Trim().Trim('"');

        return new ScimPatchPath(
            attributeParts[0],
            attributeParts.Length == 2 ? attributeParts[1] : null,
            filterAttribute,
            filterValue);
    }
}
