using System.Text.Json;
using GameGuild.Learning.Assessments.Grading.Abstractions;
using GameGuild.Learning.Assessments.Grading.Contracts;
using GameGuild.Learning.Grading.Contracts;

namespace GameGuild.Learning.Assessments.QuizAdapter;

public sealed class QuizAnswerDecoder
{
    private static readonly IReadOnlyDictionary<string, AnswerShape> Shapes =
        new Dictionary<string, AnswerShape>(StringComparer.Ordinal)
        {
            ["SINGLE_CHOICE"] = Shape("type", "optionId"),
            ["MULTIPLE_CHOICE"] = Shape("type", "optionIds"),
            ["TRUE_FALSE"] = Shape("type", "value"),
            ["FILL_IN_THE_BLANK"] = Shape("type", "values"),
            ["SHORT_ANSWER"] = Shape("type", "value"),
            ["ESSAY"] = Shape("type", "richText", "plainText"),
            ["MATCHING"] = Shape("type", "matches"),
            ["ORDERING"] = Shape("type", "itemIds"),
            ["CATEGORIZATION"] = Shape("type", "categoryIdsByItem"),
            ["RATING"] = Shape("type", "value"),
            ["NUMERIC"] = Shape("type", "value"),
            ["FORMULA"] = Shape("type", "expression"),
            ["HOTSPOT"] = Shape("type", "point"),
            ["HIGHLIGHT"] = Shape("type", "spans"),
        };

    public JsonElement Decode(AssessmentResponseEnvelopeV1 envelope)
    {
        if (envelope.SchemaVersion != GradingContractVersions.ResponseEnvelope ||
            !string.Equals(envelope.ContentType, QuizAdapterContracts.ContentType, StringComparison.Ordinal) ||
            !string.Equals(envelope.PayloadSchema, QuizAdapterContracts.AnswerPayloadSchema, StringComparison.Ordinal))
        {
            throw new JsonException("Quiz answer envelope version or discriminator is unsupported.");
        }

        JsonContract.RequireObject(envelope.Payload, "Quiz answer payload");
        JsonContract.RequireExactProperties(envelope.Payload, JsonContract.Set("answers"));
        var answers = envelope.Payload.GetProperty("answers");
        JsonContract.RequireObject(answers, "Quiz answers");
        foreach (var answerProperty in answers.EnumerateObject())
        {
            if (string.IsNullOrWhiteSpace(answerProperty.Name)) throw new JsonException("Quiz answer item ID is required.");
            ValidateAnswer(answerProperty.Value);
        }

        return envelope.Payload.Clone();
    }

    internal static void ValidateBindings(
        JsonElement normalizedPayload,
        IReadOnlyDictionary<string, string> expectedItemTypes)
    {
        var answers = normalizedPayload.GetProperty("answers");
        foreach (var answerProperty in answers.EnumerateObject())
        {
            if (!expectedItemTypes.TryGetValue(answerProperty.Name, out var expectedType))
            {
                throw new JsonException($"Quiz answer references unknown item {answerProperty.Name}.");
            }
            if (!string.Equals(answerProperty.Value.GetProperty("type").GetString(), expectedType, StringComparison.Ordinal))
            {
                throw new JsonException($"Quiz answer type for {answerProperty.Name} does not match its question type.");
            }
        }
    }

    private static void ValidateAnswer(JsonElement answer)
    {
        JsonContract.RequireObject(answer, "Quiz answer");
        if (!answer.TryGetProperty("type", out var typeProperty)) throw new JsonException("Quiz answer type is required.");
        var type = typeProperty.GetString();
        if (type is null || !Shapes.TryGetValue(type, out var shape)) throw new JsonException("Quiz answer type is unsupported.");
        JsonContract.RequireExactProperties(answer, shape.Allowed, shape.Required);

        switch (type)
        {
            case "SINGLE_CHOICE":
                JsonContract.RequireStringOrNull(answer.GetProperty("optionId"), "optionId");
                break;
            case "MULTIPLE_CHOICE":
            case "ORDERING":
                JsonContract.RequireStringArray(answer.GetProperty(type == "MULTIPLE_CHOICE" ? "optionIds" : "itemIds"));
                break;
            case "TRUE_FALSE":
                JsonContract.RequireBooleanOrNull(answer.GetProperty("value"), "value");
                break;
            case "FILL_IN_THE_BLANK":
            case "MATCHING":
                JsonContract.RequireStringRecord(answer.GetProperty(type == "MATCHING" ? "matches" : "values"));
                break;
            case "SHORT_ANSWER":
            case "NUMERIC":
                JsonContract.RequireString(answer.GetProperty("value"), "value");
                break;
            case "FORMULA":
                JsonContract.RequireString(answer.GetProperty("expression"), "expression");
                break;
            case "ESSAY":
                JsonContract.RequireObjectOrNull(answer.GetProperty("richText"), "richText");
                JsonContract.RequireString(answer.GetProperty("plainText"), "plainText");
                break;
            case "CATEGORIZATION":
                JsonContract.RequireStringArrayRecord(answer.GetProperty("categoryIdsByItem"));
                break;
            case "RATING":
                JsonContract.RequireNumberOrNull(answer.GetProperty("value"), "value");
                break;
            case "HOTSPOT":
                ValidatePoint(answer.GetProperty("point"));
                break;
            case "HIGHLIGHT":
                ValidateSpans(answer.GetProperty("spans"));
                break;
        }
    }

    private static void ValidatePoint(JsonElement point)
    {
        if (point.ValueKind == JsonValueKind.Null) return;
        JsonContract.RequireObject(point, "point");
        JsonContract.RequireExactProperties(point, JsonContract.Set("x", "y"));
        JsonContract.RequireNumber(point.GetProperty("x"), "x");
        JsonContract.RequireNumber(point.GetProperty("y"), "y");
    }

    private static void ValidateSpans(JsonElement spans)
    {
        if (spans.ValueKind != JsonValueKind.Array) throw new JsonException("spans must be an array.");
        foreach (var span in spans.EnumerateArray())
        {
            JsonContract.RequireObject(span, "span");
            JsonContract.RequireExactProperties(span, JsonContract.Set("start", "end"));
            var start = span.GetProperty("start").GetInt32();
            var end = span.GetProperty("end").GetInt32();
            if (start < 0 || end <= start) throw new JsonException("Highlight span is invalid.");
        }
    }

    private static AnswerShape Shape(params string[] properties)
    {
        var set = JsonContract.Set(properties);
        return new AnswerShape(set, set);
    }

    private sealed record AnswerShape(IReadOnlySet<string> Allowed, IReadOnlySet<string> Required);
}

internal static class JsonContract
{
    public static IReadOnlySet<string> Set(params string[] values) =>
        new HashSet<string>(values, StringComparer.Ordinal);

    public static void RequireObject(JsonElement value, string label)
    {
        if (value.ValueKind != JsonValueKind.Object) throw new JsonException($"{label} must be an object.");
    }

    public static void RequireExactProperties(
        JsonElement value,
        IReadOnlySet<string> allowed,
        IReadOnlySet<string>? required = null)
    {
        var present = value.EnumerateObject().Select(property => property.Name).ToHashSet(StringComparer.Ordinal);
        if (present.Any(property => !allowed.Contains(property)) || (required ?? allowed).Any(property => !present.Contains(property)))
        {
            throw new JsonException("JSON contract contains unknown or missing fields.");
        }
    }

    public static void RequireString(JsonElement value, string label)
    {
        if (value.ValueKind != JsonValueKind.String) throw new JsonException($"{label} must be a string.");
    }

    public static void RequireStringOrNull(JsonElement value, string label)
    {
        if (value.ValueKind is not (JsonValueKind.String or JsonValueKind.Null)) throw new JsonException($"{label} must be a string or null.");
    }

    public static void RequireBooleanOrNull(JsonElement value, string label)
    {
        if (value.ValueKind is not (JsonValueKind.True or JsonValueKind.False or JsonValueKind.Null)) throw new JsonException($"{label} must be a boolean or null.");
    }

    public static void RequireNumberOrNull(JsonElement value, string label)
    {
        if (value.ValueKind is not (JsonValueKind.Number or JsonValueKind.Null)) throw new JsonException($"{label} must be a number or null.");
        if (value.ValueKind == JsonValueKind.Number) RequireFiniteNumber(value, label);
    }

    public static void RequireNumber(JsonElement value, string label)
    {
        if (value.ValueKind != JsonValueKind.Number) throw new JsonException($"{label} must be a number.");
        RequireFiniteNumber(value, label);
    }

    public static void RequireObjectOrNull(JsonElement value, string label)
    {
        if (value.ValueKind is not (JsonValueKind.Object or JsonValueKind.Null)) throw new JsonException($"{label} must be an object or null.");
    }

    public static void RequireStringArray(JsonElement value)
    {
        if (value.ValueKind != JsonValueKind.Array || value.EnumerateArray().Any(item => item.ValueKind != JsonValueKind.String))
        {
            throw new JsonException("Expected an array of strings.");
        }
    }

    public static void RequireStringRecord(JsonElement value)
    {
        RequireObject(value, "String record");
        if (value.EnumerateObject().Any(property => property.Value.ValueKind != JsonValueKind.String))
        {
            throw new JsonException("Expected an object containing string values.");
        }
    }

    public static void RequireStringArrayRecord(JsonElement value)
    {
        RequireObject(value, "String-array record");
        foreach (var property in value.EnumerateObject()) RequireStringArray(property.Value);
    }

    private static void RequireFiniteNumber(JsonElement value, string label)
    {
        var number = value.GetDouble();
        if (double.IsNaN(number) || double.IsInfinity(number)) throw new JsonException($"{label} must be finite.");
    }
}
