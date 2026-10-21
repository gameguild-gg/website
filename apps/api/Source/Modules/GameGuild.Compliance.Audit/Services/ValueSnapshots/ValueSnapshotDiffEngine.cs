using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace GameGuild.Compliance.Audit;

/// <summary>One field-level difference between two mutation snapshots, with a JSON path that
/// addresses nested objects and array elements (for example <c>Address.City</c> or <c>Tags[2]</c>).</summary>
public sealed record ValueSnapshotFieldDiff(
    string Field,
    string ChangeKind,
    string? Before,
    string? After,
    string? BeforeValueSha256,
    string? AfterValueSha256,
    bool BeforeTruncated,
    bool AfterTruncated,
    decimal? NumericDelta);

/// <summary>
///     Field-level change tracking with precise value comparison and structured data comparison
///     (issue #176): computes differences between before/after value snapshots, recursing into
///     complex objects, arrays, and nested structures. Large values are represented with a
///     truncated rendering plus a SHA-256 checksum of the full value so integrity stays verifiable.
/// </summary>
public static class ValueSnapshotDiffEngine
{
    /// <summary>Default per-value truncation threshold (UTF-8 bytes of the serialized value).</summary>
    public const int DefaultValueMaxBytes = 2_048;

    private static readonly JsonSerializerOptions CanonicalJsonOptions = new(JsonSerializerDefaults.Web);

    /// <summary>Computes the structured field-level differences between two snapshot JSON documents.</summary>
    /// <param name="beforeJson">Before-state JSON object (null when the entity was created).</param>
    /// <param name="afterJson">After-state JSON object (null when the entity was deleted).</param>
    /// <param name="valueMaxBytes">Per-value truncation threshold in bytes.</param>
    public static IReadOnlyList<ValueSnapshotFieldDiff> ComputeDiff(
        string? beforeJson,
        string? afterJson,
        int valueMaxBytes = DefaultValueMaxBytes)
    {
        using var before = ParseDocument(beforeJson);
        using var after = ParseDocument(afterJson);

        var beforeRoot = ObjectProperties(before);
        var afterRoot = ObjectProperties(after);
        var differences = new List<ValueSnapshotFieldDiff>();
        var fields = beforeRoot.Keys.Union(afterRoot.Keys, StringComparer.Ordinal).OrderBy(field => field, StringComparer.Ordinal);

        foreach (var field in fields)
        {
            var hasBefore = beforeRoot.TryGetValue(field, out var beforeValue);
            var hasAfter = afterRoot.TryGetValue(field, out var afterValue);
            CompareTokens(differences, field, hasBefore ? beforeValue : null, hasBefore, hasAfter ? afterValue : null, hasAfter, valueMaxBytes);
        }

        return differences;
    }

    /// <summary>Serializes a diff list to the canonical JSON stored on <see cref="MutationValueSnapshot"/>.</summary>
    public static string SerializeDiffs(IReadOnlyList<ValueSnapshotFieldDiff> diffs) =>
        JsonSerializer.Serialize(diffs, CanonicalJsonOptions);

    /// <summary>Deserializes the structured diff payload stored on a snapshot row.</summary>
    public static IReadOnlyList<ValueSnapshotFieldDiff> DeserializeDiffs(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<List<ValueSnapshotFieldDiff>>(json, CanonicalJsonOptions) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    /// <summary>SHA-256 over the exact UTF-8 bytes of the given content, lowercase hex.</summary>
    public static string ComputeSha256(string content)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(content));
        return Convert.ToHexStringLower(hash);
    }

    /// <summary>Truncates a serialized value at the byte threshold, returning the truncated text,
    /// whether truncation happened, and the checksum of the full original value.</summary>
    public static (string Stored, bool Truncated, string FullValueSha256) ApplyLargeValueHandling(string value, int maxBytes)
    {
        var fullHash = ComputeSha256(value);
        if (maxBytes <= 0 || Encoding.UTF8.GetByteCount(value) <= maxBytes)
        {
            return (value, false, fullHash);
        }

        // Cut on a character boundary inside the byte budget so the stored text stays valid UTF-8.
        var budget = maxBytes;
        var chars = value.ToCharArray();
        var used = 0;
        var index = 0;
        while (index < chars.Length)
        {
            var size = Encoding.UTF8.GetByteCount(chars, index, 1);
            if (used + size > budget)
            {
                break;
            }

            used += size;
            index++;
        }

        var stored = value[..index] + $"…[truncated {value.Length - index} chars; sha256:{fullHash}]";
        return (stored, true, fullHash);
    }

    private static JsonDocument? ParseDocument(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            return JsonDocument.Parse(json);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static Dictionary<string, JsonElement> ObjectProperties(JsonDocument? document)
    {
        if (document is null || document.RootElement.ValueKind is not JsonValueKind.Object)
        {
            return new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        }

        return document.RootElement.EnumerateObject()
            .GroupBy(property => property.Name, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Last().Value, StringComparer.Ordinal);
    }

    private static void CompareTokens(
        List<ValueSnapshotFieldDiff> differences,
        string path,
        JsonElement? before,
        bool hasBefore,
        JsonElement? after,
        bool hasAfter,
        int maxBytes)
    {
        if (!hasBefore && !hasAfter)
        {
            return;
        }

        if (!hasBefore)
        {
            differences.Add(CreateDiff(path, "Added", null, after!.Value, maxBytes));
            return;
        }

        if (!hasAfter)
        {
            differences.Add(CreateDiff(path, "Removed", before!.Value, null, maxBytes));
            return;
        }

        var beforeToken = before!.Value;
        var afterToken = after!.Value;

        if (TokensEqual(beforeToken, afterToken))
        {
            return;
        }

        // Structured comparison: recurse into object/object and array/array changes so the
        // difference analysis reports precise nested paths instead of one opaque blob.
        if (beforeToken.ValueKind is JsonValueKind.Object && afterToken.ValueKind is JsonValueKind.Object)
        {
            CompareObjects(differences, path, beforeToken, afterToken, maxBytes);
            return;
        }

        if (beforeToken.ValueKind is JsonValueKind.Array && afterToken.ValueKind is JsonValueKind.Array)
        {
            CompareArrays(differences, path, beforeToken, afterToken, maxBytes);
            return;
        }

        differences.Add(CreateDiff(path, "Modified", beforeToken, afterToken, maxBytes));
    }

    private static void CompareObjects(
        List<ValueSnapshotFieldDiff> differences,
        string path,
        JsonElement before,
        JsonElement after,
        int maxBytes)
    {
        var beforeProperties = before.EnumerateObject()
            .GroupBy(property => property.Name, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Last().Value, StringComparer.Ordinal);
        var afterProperties = after.EnumeratePropertyList();

        foreach (var (name, value) in afterProperties)
        {
            var childPath = $"{path}.{name}";
            if (beforeProperties.TryGetValue(name, out var beforeValue))
            {
                CompareTokens(differences, childPath, beforeValue, true, value, true, maxBytes);
            }
            else
            {
                CompareTokens(differences, childPath, null, false, value, true, maxBytes);
            }
        }

        foreach (var name in beforeProperties.Keys.Where(name => afterProperties.All(pair => pair.Name != name)))
        {
            CompareTokens(
                differences, $"{path}.{name}", beforeProperties[name], true, null, false, maxBytes);
        }
    }

    private static void CompareArrays(
        List<ValueSnapshotFieldDiff> differences,
        string path,
        JsonElement before,
        JsonElement after,
        int maxBytes)
    {
        var beforeItems = before.EnumerateArray().ToList();
        var afterItems = after.EnumerateArray().ToList();
        var shared = Math.Min(beforeItems.Count, afterItems.Count);

        for (var index = 0; index < shared; index++)
        {
            CompareTokens(differences, $"{path}[{index}]", beforeItems[index], true, afterItems[index], true, maxBytes);
        }

        for (var index = shared; index < afterItems.Count; index++)
        {
            CompareTokens(differences, $"{path}[{index}]", null, false, afterItems[index], true, maxBytes);
        }

        for (var index = shared; index < beforeItems.Count; index++)
        {
            CompareTokens(differences, $"{path}[{index}]", beforeItems[index], true, null, false, maxBytes);
        }
    }

    private static ValueSnapshotFieldDiff CreateDiff(string path, string kind, JsonElement? before, JsonElement? after, int maxBytes)
    {
        var (beforeStored, beforeTruncated, beforeHash) = RenderValue(before);
        var (afterStored, afterTruncated, afterHash) = RenderValue(after);
        return new ValueSnapshotFieldDiff(
            path,
            kind,
            beforeStored,
            afterStored,
            beforeHash,
            afterHash,
            beforeTruncated,
            afterTruncated,
            ComputeNumericDelta(before, after));

        (string Stored, bool Truncated, string Hash) RenderValue(JsonElement? token)
        {
            if (token is null)
            {
                return (string.Empty, false, string.Empty);
            }

            var rendered = token.Value.ValueKind switch
            {
                JsonValueKind.String => JsonEncode(token.Value.GetString()),
                JsonValueKind.Null => "null",
                JsonValueKind.True => "true",
                JsonValueKind.False => "false",
                JsonValueKind.Number => token.Value.GetRawText(),
                _ => token.Value.GetRawText()
            };
            var (stored, truncated, hash) = ApplyLargeValueHandling(rendered, maxBytes);
            return (stored, truncated, hash);
        }
    }

    /// <summary>Numbers compare by numeric value (1 == 1.0); every other kind compares by raw JSON.</summary>
    private static bool TokensEqual(JsonElement before, JsonElement after)
    {
        if (before.ValueKind == JsonValueKind.Number && after.ValueKind == JsonValueKind.Number)
        {
            return TryDecimal(before, out var beforeNumber) && TryDecimal(after, out var afterNumber)
                ? beforeNumber == afterNumber
                : string.Equals(before.GetRawText(), after.GetRawText(), StringComparison.Ordinal);
        }

        if (before.ValueKind != after.ValueKind)
        {
            return false;
        }

        return before.ValueKind is JsonValueKind.Object or JsonValueKind.Array
            ? JsonNodeEquals(before, after)
            : string.Equals(before.GetRawText(), after.GetRawText(), StringComparison.Ordinal);
    }

    private static bool JsonNodeEquals(JsonElement before, JsonElement after)
    {
        try
        {
            return JsonNode.DeepEquals(
                JsonSerializer.SerializeToNode(before, CanonicalJsonOptions),
                JsonSerializer.SerializeToNode(after, CanonicalJsonOptions));
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static decimal? ComputeNumericDelta(JsonElement? before, JsonElement? after)
    {
        if (before is not { } beforeToken || after is not { } afterToken
            || beforeToken.ValueKind != JsonValueKind.Number || afterToken.ValueKind != JsonValueKind.Number)
        {
            return null;
        }

        return TryDecimal(beforeToken, out var beforeNumber) && TryDecimal(afterToken, out var afterNumber)
            ? afterNumber - beforeNumber
            : null;
    }

    private static bool TryDecimal(JsonElement token, out decimal value)
    {
        try
        {
            return token.TryGetDecimal(out value);
        }
        catch (FormatException)
        {
            value = 0;
            return false;
        }
        catch (InvalidOperationException)
        {
            value = 0;
            return false;
        }
    }

    private static string JsonEncode(string? value) => JsonSerializer.Serialize(value, CanonicalJsonOptions);
}

internal static class JsonElementEnumerationExtensions
{
    public static IEnumerable<(string Name, JsonElement Value)> EnumeratePropertyList(this JsonElement element)
    {
        foreach (var property in element.EnumerateObject())
        {
            yield return (property.Name, property.Value);
        }
    }
}
