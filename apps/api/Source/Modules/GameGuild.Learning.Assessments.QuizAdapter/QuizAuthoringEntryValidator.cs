using System.Text.Json;
using GameGuild.Learning.Assessments.Grading.Contracts;
using GameGuild.Learning.Grading.Contracts;

namespace GameGuild.Learning.Assessments.QuizAdapter;

internal static class QuizAuthoringEntryValidator
{
    private static readonly IReadOnlySet<string> BaseFields = JsonContract.Set(
        "type", "stem", "points", "feedback", "settings", "attachments");

    public static void Validate(JsonElement entry)
    {
        JsonContract.RequireObject(entry, "Quiz entry");
        var type = RequiredString(entry, "type");
        RequiredString(entry, "stem", allowEmpty: true);
        if (entry.TryGetProperty("points", out var points))
        {
            if (!points.TryGetInt32(out var units)) throw new JsonException("points must be a JSON integer.");
            ScoreValue.FromUnits(units);
        }
        if (!entry.TryGetProperty("settings", out var settings)) throw new JsonException("settings is required.");
        ValidateSettings(settings);
        ValidateFeedback(entry);
        ValidateAttachments(entry);

        switch (type)
        {
            case "SINGLE_CHOICE":
                Exact(entry, ["options", "correctOptionId"], ["options", "correctOptionId"]);
                ValidateChoiceOptions(entry.GetProperty("options"));
                RequiredString(entry, "correctOptionId");
                break;
            case "MULTIPLE_CHOICE":
                Exact(entry, ["options", "correctOptionIds", "selectionLimit"], ["options", "correctOptionIds"]);
                ValidateChoiceOptions(entry.GetProperty("options"));
                JsonContract.RequireStringArray(entry.GetProperty("correctOptionIds"));
                if (entry.TryGetProperty("selectionLimit", out var selectionLimit)) PositiveInteger(selectionLimit, "selectionLimit");
                break;
            case "TRUE_FALSE":
                Exact(entry, ["correctAnswer"], ["correctAnswer"]);
                RequireBoolean(entry.GetProperty("correctAnswer"), "correctAnswer");
                break;
            case "FILL_IN_THE_BLANK":
                Exact(entry, ["blanks"], ["blanks"]);
                ValidateBlanks(entry.GetProperty("blanks"));
                break;
            case "SHORT_ANSWER":
                Exact(entry, ["acceptedAnswers", "caseSensitive"], ["acceptedAnswers"]);
                JsonContract.RequireStringArray(entry.GetProperty("acceptedAnswers"));
                OptionalBoolean(entry, "caseSensitive");
                break;
            case "ESSAY":
                Exact(entry,
                    ["minWordCount", "maxWordCount", "showWordCount", "correctAnswer", "correctAnswerPlain", "requireFormatting"],
                    []);
                OptionalNonNegativeInteger(entry, "minWordCount");
                OptionalNonNegativeInteger(entry, "maxWordCount");
                OptionalBoolean(entry, "showWordCount");
                OptionalBoolean(entry, "requireFormatting");
                OptionalString(entry, "correctAnswerPlain");
                if (entry.TryGetProperty("correctAnswer", out var correctAnswer) &&
                    correctAnswer.ValueKind is not (JsonValueKind.Object or JsonValueKind.Null))
                {
                    throw new JsonException("correctAnswer must be an object or null.");
                }
                break;
            case "MATCHING":
                Exact(entry, ["pairs", "rightOptions", "distractors", "allowPartialCredit"], ["pairs"]);
                ValidateObjectArray(entry.GetProperty("pairs"), ["id", "left", "right"], item =>
                {
                    RequiredString(item, "id");
                    RequiredString(item, "left");
                    RequiredString(item, "right");
                });
                OptionalStringArray(entry, "rightOptions");
                OptionalStringArray(entry, "distractors");
                OptionalBoolean(entry, "allowPartialCredit");
                break;
            case "ORDERING":
                Exact(entry, ["items", "allowPartialCredit"], ["items"]);
                ValidateObjectArray(entry.GetProperty("items"), ["id", "text", "correctPosition"], item =>
                {
                    RequiredString(item, "id");
                    RequiredString(item, "text", allowEmpty: true);
                    NonNegativeInteger(item.GetProperty("correctPosition"), "correctPosition");
                });
                OptionalBoolean(entry, "allowPartialCredit");
                break;
            case "CATEGORIZATION":
                Exact(entry, ["categories", "items"], ["categories", "items"]);
                ValidateObjectArray(entry.GetProperty("categories"), ["id", "name", "description"], item =>
                {
                    RequiredString(item, "id");
                    RequiredString(item, "name", allowEmpty: true);
                    OptionalString(item, "description");
                }, ["id", "name"]);
                ValidateObjectArray(entry.GetProperty("items"), ["id", "text", "correctCategoryIds"], item =>
                {
                    RequiredString(item, "id");
                    RequiredString(item, "text", allowEmpty: true);
                    JsonContract.RequireStringArray(item.GetProperty("correctCategoryIds"));
                });
                break;
            case "RATING":
                Exact(entry, ["scale", "correctRating"], ["scale"]);
                ValidateRatingScale(entry.GetProperty("scale"));
                OptionalNumber(entry, "correctRating");
                break;
            case "NUMERIC":
            case "FORMULA":
                Exact(entry, ["variables", "formula", "toleranceType", "tolerance", "decimalPlaces"],
                    ["variables", "formula", "toleranceType", "tolerance", "decimalPlaces"]);
                ValidateFormula(entry);
                break;
            case "HOTSPOT":
                Exact(entry, ["imageAssetUri", "imageWidth", "imageHeight", "hotspots"],
                    ["imageAssetUri", "imageWidth", "imageHeight", "hotspots"]);
                JsonContract.RequireStringOrNull(entry.GetProperty("imageAssetUri"), "imageAssetUri");
                JsonContract.RequireNumber(entry.GetProperty("imageWidth"), "imageWidth");
                JsonContract.RequireNumber(entry.GetProperty("imageHeight"), "imageHeight");
                ValidateHotspots(entry.GetProperty("hotspots"));
                break;
            case "HIGHLIGHT":
                Exact(entry, ["sourceText", "plainText", "highlights"], ["sourceText", "plainText", "highlights"]);
                RequiredString(entry, "sourceText", allowEmpty: true);
                RequiredString(entry, "plainText", allowEmpty: true);
                ValidateObjectArray(entry.GetProperty("highlights"), ["start", "end"], span =>
                {
                    NonNegativeInteger(span.GetProperty("start"), "start");
                    NonNegativeInteger(span.GetProperty("end"), "end");
                });
                break;
            default:
                throw new JsonException($"Quiz entry type {type} is unsupported.");
        }
    }

    private static void ValidateSettings(JsonElement settings)
    {
        JsonContract.RequireObject(settings, "settings");
        JsonContract.RequireExactProperties(
            settings,
            JsonContract.Set("allowRetry", "shuffleOptions", "showFeedback", "showCorrectAnswer"),
            JsonContract.Set("allowRetry"));
        RequireBoolean(settings.GetProperty("allowRetry"), "allowRetry");
        OptionalBoolean(settings, "shuffleOptions");
        OptionalBoolean(settings, "showFeedback");
        OptionalBoolean(settings, "showCorrectAnswer");
    }

    private static void ValidateFeedback(JsonElement entry)
    {
        if (!entry.TryGetProperty("feedback", out var feedback)) return;
        JsonContract.RequireObject(feedback, "feedback");
        JsonContract.RequireExactProperties(feedback, JsonContract.Set("correct", "incorrect", "general"), JsonContract.Set());
        OptionalString(feedback, "correct");
        OptionalString(feedback, "incorrect");
        OptionalString(feedback, "general");
    }

    private static void ValidateAttachments(JsonElement entry)
    {
        if (!entry.TryGetProperty("attachments", out var attachments)) return;
        JsonContract.RequireObject(attachments, "attachments");
        JsonContract.RequireExactProperties(
            attachments,
            JsonContract.Set("learnerVisible", "authorOnly"),
            JsonContract.Set());
        foreach (var field in new[] { "learnerVisible", "authorOnly" })
        {
            if (!attachments.TryGetProperty(field, out var values)) continue;
            ValidateObjectArray(values, ["assetUri", "role", "label", "altText"], attachment =>
            {
                RequiredString(attachment, "assetUri");
                var role = RequiredString(attachment, "role");
                if (role is not ("question" or "answer" or "feedback" or "source"))
                    throw new JsonException("Attachment role is unsupported.");
                OptionalString(attachment, "label");
                OptionalString(attachment, "altText");
            }, ["assetUri", "role"]);
        }
    }

    private static void ValidateChoiceOptions(JsonElement options) =>
        ValidateObjectArray(options, ["id", "text"], option =>
        {
            RequiredString(option, "id");
            RequiredString(option, "text", allowEmpty: true);
        });

    private static void ValidateBlanks(JsonElement blanks) =>
        ValidateObjectArray(blanks, ["id", "position", "input"], blank =>
        {
            RequiredString(blank, "id");
            NonNegativeInteger(blank.GetProperty("position"), "position");
            ValidateBlankInput(blank.GetProperty("input"));
        });

    private static void ValidateBlankInput(JsonElement input)
    {
        JsonContract.RequireObject(input, "blank input");
        var type = RequiredString(input, "type");
        switch (type)
        {
            case "TEXT":
                JsonContract.RequireExactProperties(input, JsonContract.Set("type", "acceptedAnswers", "caseSensitive"), JsonContract.Set("type", "acceptedAnswers"));
                JsonContract.RequireStringArray(input.GetProperty("acceptedAnswers"));
                OptionalBoolean(input, "caseSensitive");
                break;
            case "NUMBER":
                JsonContract.RequireExactProperties(input,
                    JsonContract.Set("type", "correctValue", "tolerance", "requiredPrecision", "unit", "requireUnit", "allowNegative"),
                    JsonContract.Set("type", "correctValue"));
                JsonContract.RequireNumber(input.GetProperty("correctValue"), "correctValue");
                OptionalNonNegativeNumber(input, "tolerance");
                OptionalNonNegativeInteger(input, "requiredPrecision");
                OptionalString(input, "unit");
                OptionalBoolean(input, "requireUnit");
                OptionalBoolean(input, "allowNegative");
                break;
            case "DROPDOWN":
                JsonContract.RequireExactProperties(input, JsonContract.Set("type", "options"));
                JsonContract.RequireStringArray(input.GetProperty("options"));
                break;
            case "WORDBANK":
                JsonContract.RequireExactProperties(input, JsonContract.Set("type", "words"));
                JsonContract.RequireStringArray(input.GetProperty("words"));
                break;
            default:
                throw new JsonException("Blank input type is unsupported.");
        }
    }

    private static void ValidateRatingScale(JsonElement scale)
    {
        JsonContract.RequireObject(scale, "rating scale");
        JsonContract.RequireExactProperties(
            scale,
            JsonContract.Set("min", "max", "step", "minLabel", "maxLabel"),
            JsonContract.Set("min", "max", "step"));
        JsonContract.RequireNumber(scale.GetProperty("min"), "scale.min");
        JsonContract.RequireNumber(scale.GetProperty("max"), "scale.max");
        JsonContract.RequireNumber(scale.GetProperty("step"), "scale.step");
        OptionalString(scale, "minLabel");
        OptionalString(scale, "maxLabel");
    }

    private static void ValidateFormula(JsonElement entry)
    {
        ValidateObjectArray(entry.GetProperty("variables"), ["id", "name", "min", "max", "decimals"], variable =>
        {
            RequiredString(variable, "id");
            RequiredString(variable, "name", allowEmpty: true);
            JsonContract.RequireNumber(variable.GetProperty("min"), "min");
            JsonContract.RequireNumber(variable.GetProperty("max"), "max");
            NonNegativeInteger(variable.GetProperty("decimals"), "decimals");
        });
        RequiredString(entry, "formula", allowEmpty: true);
        var toleranceType = RequiredString(entry, "toleranceType");
        if (toleranceType is not ("absolute" or "percentage")) throw new JsonException("toleranceType is unsupported.");
        NonNegativeNumber(entry.GetProperty("tolerance"), "tolerance");
        NonNegativeInteger(entry.GetProperty("decimalPlaces"), "decimalPlaces");
    }

    private static void ValidateHotspots(JsonElement hotspots) =>
        ValidateObjectArray(hotspots, ["id", "x", "y", "zones"], hotspot =>
        {
            RequiredString(hotspot, "id");
            JsonContract.RequireNumber(hotspot.GetProperty("x"), "x");
            JsonContract.RequireNumber(hotspot.GetProperty("y"), "y");
            ValidateObjectArray(hotspot.GetProperty("zones"), ["radius", "label"], zone =>
            {
                JsonContract.RequireNumber(zone.GetProperty("radius"), "radius");
                RequiredString(zone, "label", allowEmpty: true);
            });
        });

    private static void Exact(JsonElement entry, string[] additionalAllowed, string[] additionalRequired)
    {
        var allowed = BaseFields.Concat(additionalAllowed).ToHashSet(StringComparer.Ordinal);
        var required = new[] { "type", "stem", "settings" }.Concat(additionalRequired).ToHashSet(StringComparer.Ordinal);
        JsonContract.RequireExactProperties(entry, allowed, required);
    }

    private static void ValidateObjectArray(
        JsonElement array,
        string[] allowed,
        Action<JsonElement> validate,
        string[]? required = null)
    {
        if (array.ValueKind != JsonValueKind.Array) throw new JsonException("Expected an array.");
        foreach (var item in array.EnumerateArray())
        {
            JsonContract.RequireObject(item, "Array item");
            JsonContract.RequireExactProperties(item, JsonContract.Set(allowed), JsonContract.Set(required ?? allowed));
            validate(item);
        }
    }

    private static string RequiredString(JsonElement owner, string property, bool allowEmpty = false)
    {
        if (!owner.TryGetProperty(property, out var value)) throw new JsonException($"{property} is required.");
        JsonContract.RequireString(value, property);
        var text = value.GetString()!;
        if (!allowEmpty && string.IsNullOrWhiteSpace(text)) throw new JsonException($"{property} must be non-empty.");
        return text;
    }

    private static void OptionalString(JsonElement owner, string property)
    {
        if (owner.TryGetProperty(property, out var value)) JsonContract.RequireString(value, property);
    }

    private static void OptionalStringArray(JsonElement owner, string property)
    {
        if (owner.TryGetProperty(property, out var value)) JsonContract.RequireStringArray(value);
    }

    private static void RequireBoolean(JsonElement value, string label)
    {
        if (value.ValueKind is not (JsonValueKind.True or JsonValueKind.False)) throw new JsonException($"{label} must be a boolean.");
    }

    private static void OptionalBoolean(JsonElement owner, string property)
    {
        if (owner.TryGetProperty(property, out var value)) RequireBoolean(value, property);
    }

    private static void OptionalNumber(JsonElement owner, string property)
    {
        if (owner.TryGetProperty(property, out var value)) JsonContract.RequireNumber(value, property);
    }

    private static void OptionalNonNegativeNumber(JsonElement owner, string property)
    {
        if (owner.TryGetProperty(property, out var value)) NonNegativeNumber(value, property);
    }

    private static void NonNegativeNumber(JsonElement value, string label)
    {
        JsonContract.RequireNumber(value, label);
        if (value.GetDouble() < 0) throw new JsonException($"{label} must be non-negative.");
    }

    private static void PositiveInteger(JsonElement value, string label)
    {
        if (!value.TryGetInt32(out var result) || result <= 0) throw new JsonException($"{label} must be a positive integer.");
    }

    private static void NonNegativeInteger(JsonElement value, string label)
    {
        if (!value.TryGetInt32(out var result) || result < 0) throw new JsonException($"{label} must be a non-negative integer.");
    }

    private static void OptionalNonNegativeInteger(JsonElement owner, string property)
    {
        if (owner.TryGetProperty(property, out var value)) NonNegativeInteger(value, property);
    }
}
