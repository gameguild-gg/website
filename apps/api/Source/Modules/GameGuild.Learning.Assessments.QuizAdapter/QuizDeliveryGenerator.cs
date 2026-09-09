using System.Text.Json;
using System.Text.Json.Nodes;

namespace GameGuild.Learning.Assessments.QuizAdapter;

public sealed class QuizDeliveryGenerator
{
    private static readonly string[] CommonFields = ["type", "stem", "points", "settings"];

    public JsonElement Generate(JsonElement projectedItem)
    {
        JsonContract.RequireObject(projectedItem, "Projected quiz item");
        var itemId = projectedItem.GetProperty("itemId").GetString()
            ?? throw new JsonException("Projected quiz item ID is required.");
        var entry = projectedItem.GetProperty("authoringEntry");
        var learnerEntry = RedactEntry(entry);
        return JsonSerializer.SerializeToElement(new { itemId, entry = learnerEntry });
    }

    public static JsonElement RedactEntry(JsonElement entry)
    {
        JsonContract.RequireObject(entry, "Quiz entry");
        var type = entry.GetProperty("type").GetString() ?? throw new JsonException("Quiz entry type is required.");
        var learner = CopyFields(entry, CommonFields);
        CopyFeedback(entry, learner);
        CopyAttachments(entry, learner);

        switch (type)
        {
            case "SINGLE_CHOICE":
                Copy(entry, learner, "options");
                break;
            case "MULTIPLE_CHOICE":
                Copy(entry, learner, "options", "selectionLimit");
                break;
            case "TRUE_FALSE":
            case "SHORT_ANSWER":
                break;
            case "FILL_IN_THE_BLANK":
                learner["blanks"] = RedactBlanks(entry.GetProperty("blanks"));
                break;
            case "ESSAY":
                Copy(entry, learner, "minWordCount", "maxWordCount", "showWordCount");
                break;
            case "MATCHING":
                RedactMatching(entry, learner);
                Copy(entry, learner, "allowPartialCredit");
                break;
            case "ORDERING":
                learner["items"] = ProjectArray(entry.GetProperty("items"), ["id", "text"]);
                Copy(entry, learner, "allowPartialCredit");
                break;
            case "CATEGORIZATION":
                Copy(entry, learner, "categories");
                learner["items"] = ProjectArray(entry.GetProperty("items"), ["id", "text"]);
                break;
            case "RATING":
                Copy(entry, learner, "scale");
                break;
            case "NUMERIC":
                Copy(entry, learner, "variables", "formula", "decimalPlaces");
                break;
            case "FORMULA":
                Copy(entry, learner, "variables", "decimalPlaces");
                break;
            case "HOTSPOT":
                Copy(entry, learner, "imageAssetUri", "imageWidth", "imageHeight");
                break;
            case "HIGHLIGHT":
                Copy(entry, learner, "plainText");
                break;
            default:
                throw new JsonException($"Unsupported quiz entry type {type}.");
        }

        return JsonSerializer.SerializeToElement(learner);
    }

    private static JsonArray RedactBlanks(JsonElement blanks)
    {
        var result = new JsonArray();
        foreach (var blank in blanks.EnumerateArray())
        {
            var projected = CopyFields(blank, ["id", "position"]);
            var input = blank.GetProperty("input");
            var inputType = input.GetProperty("type").GetString();
            projected["input"] = inputType switch
            {
                "TEXT" => CopyFields(input, ["type"]),
                "NUMBER" => CopyFields(input, ["type", "requiredPrecision", "unit", "requireUnit", "allowNegative"]),
                "DROPDOWN" => CopyFields(input, ["type", "options"]),
                "WORDBANK" => CopyFields(input, ["type", "words"]),
                _ => throw new JsonException("Unsupported fill-blank input type."),
            };
            result.Add(projected);
        }

        return result;
    }

    private static void RedactMatching(JsonElement entry, JsonObject learner)
    {
        var pairs = entry.GetProperty("pairs");
        learner["pairs"] = ProjectArray(pairs, ["id", "left"]);
        var rightOptions = pairs.EnumerateArray()
            .Select(pair => pair.GetProperty("right").GetString())
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Cast<string>()
            .ToList();
        if (entry.TryGetProperty("distractors", out var distractors))
        {
            rightOptions.AddRange(distractors.EnumerateArray().Select(value => value.GetString()).OfType<string>());
        }

        rightOptions.Reverse();
        learner["rightOptions"] = JsonSerializer.SerializeToNode(rightOptions);
    }

    private static JsonArray ProjectArray(JsonElement source, IReadOnlyList<string> fields)
    {
        var result = new JsonArray();
        foreach (var item in source.EnumerateArray()) result.Add(CopyFields(item, fields));
        return result;
    }

    private static JsonObject CopyFields(JsonElement source, IReadOnlyList<string> fields)
    {
        var target = new JsonObject();
        Copy(source, target, fields.ToArray());
        return target;
    }

    private static void Copy(JsonElement source, JsonObject target, params string[] fields)
    {
        foreach (var field in fields)
        {
            if (source.TryGetProperty(field, out var value)) target[field] = JsonNode.Parse(value.GetRawText());
        }
    }

    private static void CopyFeedback(JsonElement entry, JsonObject learner)
    {
        if (!entry.TryGetProperty("feedback", out var feedback) ||
            !feedback.TryGetProperty("general", out var general)) return;
        learner["feedback"] = new JsonObject { ["general"] = JsonNode.Parse(general.GetRawText()) };
    }

    private static void CopyAttachments(JsonElement entry, JsonObject learner)
    {
        if (!entry.TryGetProperty("attachments", out var attachments) ||
            !attachments.TryGetProperty("learnerVisible", out var visible)) return;
        learner["attachments"] = new JsonObject { ["learnerVisible"] = JsonNode.Parse(visible.GetRawText()) };
    }
}
