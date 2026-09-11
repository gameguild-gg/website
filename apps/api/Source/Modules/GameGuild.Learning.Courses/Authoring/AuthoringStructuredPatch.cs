using System.Text.Json;
using System.Text.Json.Nodes;

namespace GameGuild.Learning.Courses;

/// <summary>
/// Applies the deliberately small JSON patch dialect accepted from the authoring copilot.
/// Operations mutate only the addressed value, so Lexical nodes and quiz fields unknown to
/// this service survive unchanged.
/// </summary>
public static class AuthoringStructuredPatch
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
    };

    public static string Apply(string sourceJson, string patchJson, AiProposalKind kind)
    {
        if (kind is not (AiProposalKind.LexicalPatch or AiProposalKind.QuizPatch))
            throw new ArgumentOutOfRangeException(nameof(kind), "Structured patches are only valid for Lexical lessons and quizzes.");

        var document = JsonNode.Parse(sourceJson) ?? throw new ArgumentException("The source document is invalid JSON.", nameof(sourceJson));
        var patch = JsonNode.Parse(patchJson) as JsonObject
                    ?? throw new ArgumentException("The AI patch must be a JSON object.", nameof(patchJson));
        var operations = patch["operations"] as JsonArray
                         ?? throw new ArgumentException("The AI patch must contain an operations array.", nameof(patchJson));
        if (operations.Count is 0 or > 100)
            throw new ArgumentException("The AI patch must contain between 1 and 100 operations.", nameof(patchJson));

        foreach (var node in operations)
        {
            var operation = node as JsonObject
                            ?? throw new ArgumentException("Every AI patch operation must be an object.", nameof(patchJson));
            ApplyOperation(document, operation, kind);
        }

        return document.ToJsonString(JsonOptions);
    }

    private static void ApplyOperation(JsonNode document, JsonObject operation, AiProposalKind kind)
    {
        var op = operation["op"]?.GetValue<string>()?.Trim().ToLowerInvariant()
                 ?? throw new ArgumentException("Every AI patch operation requires an op value.", nameof(operation));
        var pointer = operation["path"]?.GetValue<string>()
                      ?? throw new ArgumentException("Every AI patch operation requires a path value.", nameof(operation));
        var segments = ParsePointer(pointer);
        if (segments.Count == 0)
            throw new ArgumentException("Replacing the structured document root is not allowed.", nameof(operation));
        if (kind == AiProposalKind.LexicalPatch && segments.Any(IsProtectedLexicalProperty))
            throw new ArgumentException("Lexical node identity and type metadata cannot be changed by AI.", nameof(operation));

        var (parent, leaf) = ResolveParent(document, segments);
        switch (op)
        {
            case "replace":
                Replace(parent, leaf, operation["value"]?.DeepClone());
                break;
            case "add":
                Add(parent, leaf, operation["value"]?.DeepClone());
                break;
            case "remove":
                Remove(parent, leaf, kind);
                break;
            default:
                throw new ArgumentException($"Unsupported AI patch operation '{op}'.", nameof(operation));
        }
    }

    private static (JsonNode Parent, string Leaf) ResolveParent(JsonNode document, IReadOnlyList<string> segments)
    {
        var current = document;
        for (var index = 0; index < segments.Count - 1; index++)
        {
            current = ResolveChild(current, segments[index])
                      ?? throw new ArgumentException($"AI patch path segment '{segments[index]}' does not exist.");
        }
        return (current, segments[^1]);
    }

    private static JsonNode? ResolveChild(JsonNode node, string segment) => node switch
    {
        JsonObject obj => obj[segment],
        JsonArray array => array[ParseArrayIndex(segment, array.Count, allowAppend: false)],
        _ => throw new ArgumentException("AI patch paths can only traverse objects and arrays."),
    };

    private static void Replace(JsonNode parent, string leaf, JsonNode? value)
    {
        switch (parent)
        {
            case JsonObject obj when obj.ContainsKey(leaf):
                obj[leaf] = value;
                return;
            case JsonArray array:
                array[ParseArrayIndex(leaf, array.Count, allowAppend: false)] = value;
                return;
            default:
                throw new ArgumentException("AI replace operations must address an existing value.");
        }
    }

    private static void Add(JsonNode parent, string leaf, JsonNode? value)
    {
        switch (parent)
        {
            case JsonObject obj:
                obj[leaf] = value;
                return;
            case JsonArray array when leaf == "-":
                array.Add(value);
                return;
            case JsonArray array:
                array.Insert(ParseArrayIndex(leaf, array.Count, allowAppend: true), value);
                return;
            default:
                throw new ArgumentException("AI add operations require an object or array parent.");
        }
    }

    private static void Remove(JsonNode parent, string leaf, AiProposalKind kind)
    {
        JsonNode? target;
        switch (parent)
        {
            case JsonObject obj:
                target = obj[leaf] ?? throw new ArgumentException("AI remove operations must address an existing value.");
                EnsureRemovalAllowed(target, kind);
                obj.Remove(leaf);
                return;
            case JsonArray array:
                var index = ParseArrayIndex(leaf, array.Count, allowAppend: false);
                target = array[index];
                EnsureRemovalAllowed(target, kind);
                array.RemoveAt(index);
                return;
            default:
                throw new ArgumentException("AI remove operations require an object or array parent.");
        }
    }

    private static void EnsureRemovalAllowed(JsonNode? target, AiProposalKind kind)
    {
        if (kind != AiProposalKind.LexicalPatch || target is not JsonObject obj)
            return;
        var type = obj["type"]?.GetValue<string>();
        if (!string.IsNullOrWhiteSpace(type) && type is not ("text" or "paragraph" or "heading" or "quote" or "listitem"))
            throw new ArgumentException($"AI cannot remove the interactive or unknown Lexical node type '{type}'.");
    }

    private static IReadOnlyList<string> ParsePointer(string pointer)
    {
        if (pointer.Length == 0)
            return [];
        if (!pointer.StartsWith("/", StringComparison.Ordinal))
            throw new ArgumentException("AI patch paths must use JSON Pointer syntax.", nameof(pointer));
        return pointer.Split('/').Skip(1)
            .Select(static segment => segment.Replace("~1", "/", StringComparison.Ordinal).Replace("~0", "~", StringComparison.Ordinal))
            .ToArray();
    }

    private static int ParseArrayIndex(string value, int count, bool allowAppend)
    {
        if (!int.TryParse(value, out var index) || index < 0 || index > count || (!allowAppend && index == count))
            throw new ArgumentException($"'{value}' is not a valid array index.");
        return index;
    }

    private static bool IsProtectedLexicalProperty(string value) =>
        value.Equals("type", StringComparison.OrdinalIgnoreCase) ||
        value.Equals("version", StringComparison.OrdinalIgnoreCase) ||
        value.Equals("key", StringComparison.OrdinalIgnoreCase);
}
