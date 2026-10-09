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
            if (string.IsNullOrWhiteSpace(answerProperty.Name))
            {
                throw new JsonException("Quiz answer item ID is required.");
            }

            ValidateAnswer(answerProperty.Value);
        }

        return envelope.Payload.Clone();
    }

    public JsonElement Decode(
        AssessmentResponseEnvelopeV1 envelope,
        IReadOnlyList<JsonElement> projectedItems)
    {
        var normalized = Decode(envelope);
        var projections = projectedItems.ToDictionary(
            projection => RequiredProjectionText(projection, "itemId"),
            StringComparer.Ordinal);
        ValidateBindings(
            normalized,
            projections.ToDictionary(
                pair => pair.Key,
                pair => RequiredProjectionText(pair.Value, "itemType"),
                StringComparer.Ordinal));

        foreach (var answerProperty in normalized.GetProperty("answers").EnumerateObject())
        {
            ValidateAgainstProjection(answerProperty.Value, projections[answerProperty.Name]);
        }

        return normalized;
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
        if (!answer.TryGetProperty("type", out var typeProperty))
        {
            throw new JsonException("Quiz answer type is required.");
        }

        var type = typeProperty.GetString();
        if (type is null || !Shapes.TryGetValue(type, out var shape))
        {
            throw new JsonException("Quiz answer type is unsupported.");
        }

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

    private static void ValidateAgainstProjection(JsonElement answer, JsonElement projection)
    {
        var type = RequiredProjectionText(projection, "itemType");
        var entry = projection.GetProperty("authoringEntry");
        switch (type)
        {
            case "SINGLE_CHOICE":
                ValidateOptionalDomainValue(
                    answer.GetProperty("optionId"),
                    IdSet(entry.GetProperty("options")),
                    "optionId");
                break;
            case "MULTIPLE_CHOICE":
            {
                var values = ReadUniqueStrings(answer.GetProperty("optionIds"), "optionIds");
                RequireKnown(values, IdSet(entry.GetProperty("options")), "optionIds");
                var limit = entry.TryGetProperty("selectionLimit", out var selectionLimit)
                    ? selectionLimit.GetInt32()
                    : entry.GetProperty("options").GetArrayLength();
                if (values.Length > limit)
                    {
                        throw new JsonException("optionIds exceeds the configured selection limit.");
                    }

                    break;
            }
            case "FILL_IN_THE_BLANK":
                RequireKnown(
                    answer.GetProperty("values").EnumerateObject().Select(value => value.Name),
                    IdSet(entry.GetProperty("blanks")),
                    "blank IDs");
                break;
            case "MATCHING":
            {
                var matches = answer.GetProperty("matches");
                var pairs = entry.GetProperty("pairs").EnumerateArray().ToArray();
                RequireKnown(matches.EnumerateObject().Select(value => value.Name), IdSet(pairs), "matching pair IDs");
                if (matches.EnumerateObject().Count() > pairs.Length)
                    {
                        throw new JsonException("matches exceeds the number of matching pairs.");
                    }

                    var allowedValues = pairs.Select(value => RequiredProjectionText(value, "right"))
                    .Concat(OptionalStringValues(entry, "rightOptions"))
                    .Concat(OptionalStringValues(entry, "distractors"))
                    .ToHashSet(StringComparer.Ordinal);
                var selectedValues = matches.EnumerateObject().Select(value => value.Value.GetString()!).ToArray();
                RequireKnown(selectedValues, allowedValues, "matching values");
                RequireDistinct(selectedValues, "matching values");
                break;
            }
            case "ORDERING":
            {
                var values = ReadUniqueStrings(answer.GetProperty("itemIds"), "itemIds");
                var allowed = IdSet(entry.GetProperty("items"));
                RequireKnown(values, allowed, "itemIds");
                if (values.Length > allowed.Count)
                    {
                        throw new JsonException("itemIds exceeds the number of ordering items.");
                    }

                    break;
            }
            case "CATEGORIZATION":
            {
                var categories = IdSet(entry.GetProperty("categories"));
                var items = IdSet(entry.GetProperty("items"));
                var assignments = answer.GetProperty("categoryIdsByItem");
                RequireKnown(assignments.EnumerateObject().Select(value => value.Name), items, "categorization item IDs");
                foreach (var assignment in assignments.EnumerateObject())
                {
                    var values = ReadUniqueStrings(assignment.Value, $"categoryIdsByItem.{assignment.Name}");
                    RequireKnown(values, categories, "category IDs");
                    if (values.Length > categories.Count)
                        {
                            throw new JsonException("A categorization answer exceeds the number of categories.");
                        }
                    }
                break;
            }
            case "RATING":
                ValidateRating(answer.GetProperty("value"), entry.GetProperty("scale"));
                break;
            case "HOTSPOT":
                ValidateHotspotPoint(answer.GetProperty("point"));
                break;
            case "HIGHLIGHT":
                ValidateHighlightBounds(answer.GetProperty("spans"), RequiredProjectionText(entry, "plainText").Length);
                break;
        }
    }

    private static HashSet<string> IdSet(JsonElement array) => IdSet(array.EnumerateArray().ToArray());

    private static HashSet<string> IdSet(IEnumerable<JsonElement> values)
    {
        var ids = values.Select(value => RequiredProjectionText(value, "id")).ToArray();
        RequireDistinct(ids, "projection IDs");
        return ids.ToHashSet(StringComparer.Ordinal);
    }

    private static string[] OptionalStringValues(JsonElement owner, string property) =>
        owner.TryGetProperty(property, out var values)
            ? values.EnumerateArray().Select(value => value.GetString()!).ToArray()
            : [];

    private static string[] ReadUniqueStrings(JsonElement values, string label)
    {
        var result = values.EnumerateArray().Select(value => value.GetString()!).ToArray();
        RequireDistinct(result, label);
        return result;
    }

    private static void RequireDistinct(IEnumerable<string> values, string label)
    {
        var source = values.ToArray();
        if (source.Distinct(StringComparer.Ordinal).Count() != source.Length)
        {
            throw new JsonException($"{label} contains duplicate values.");
        }
    }

    private static void RequireKnown(IEnumerable<string> values, IReadOnlySet<string> allowed, string label)
    {
        var unknown = values.FirstOrDefault(value => !allowed.Contains(value));
        if (unknown is not null)
        {
            throw new JsonException($"{label} contains unknown value {unknown}.");
        }
    }

    private static void ValidateOptionalDomainValue(JsonElement value, IReadOnlySet<string> allowed, string label)
    {
        if (value.ValueKind == JsonValueKind.Null)
        {
            return;
        }

        var selected = value.GetString()!;
        if (!allowed.Contains(selected))
        {
            throw new JsonException($"{label} contains an unknown value.");
        }
    }

    private static void ValidateRating(JsonElement value, JsonElement scale)
    {
        if (value.ValueKind == JsonValueKind.Null)
        {
            return;
        }

        var selected = value.GetDecimal();
        var minimum = scale.GetProperty("min").GetDecimal();
        var maximum = scale.GetProperty("max").GetDecimal();
        var step = scale.GetProperty("step").GetDecimal();
        if (selected < minimum || selected > maximum || step <= 0 || (selected - minimum) % step != 0)
        {
            throw new JsonException("Rating value is outside its configured scale.");
        }
    }

    private static void ValidateHotspotPoint(JsonElement point)
    {
        if (point.ValueKind == JsonValueKind.Null)
        {
            return;
        }

        var x = point.GetProperty("x").GetDecimal();
        var y = point.GetProperty("y").GetDecimal();
        if (x is < 0 or > 100 || y is < 0 or > 100)
        {
            throw new JsonException("Hotspot coordinates must be between 0 and 100.");
        }
    }

    private static void ValidateHighlightBounds(JsonElement spans, int textLength)
    {
        var ranges = spans.EnumerateArray()
            .Select(span => (Start: span.GetProperty("start").GetInt32(), End: span.GetProperty("end").GetInt32()))
            .OrderBy(span => span.Start)
            .ThenBy(span => span.End)
            .ToArray();
        if (ranges.Any(span => span.End > textLength))
        {
            throw new JsonException("Highlight span exceeds the source text.");
        }

        for (var index = 1; index < ranges.Length; index++)
        {
            if (ranges[index].Start < ranges[index - 1].End)
            {
                throw new JsonException("Highlight spans cannot overlap.");
            }
        }
    }

    private static string RequiredProjectionText(JsonElement owner, string property)
    {
        if (!owner.TryGetProperty(property, out var value) || value.ValueKind != JsonValueKind.String ||
            string.IsNullOrWhiteSpace(value.GetString()))
        {
            throw new JsonException($"Projection {property} is required.");
        }
        return value.GetString()!;
    }

    private static void ValidatePoint(JsonElement point)
    {
        if (point.ValueKind == JsonValueKind.Null)
        {
            return;
        }

        JsonContract.RequireObject(point, "point");
        JsonContract.RequireExactProperties(point, JsonContract.Set("x", "y"));
        JsonContract.RequireNumber(point.GetProperty("x"), "x");
        JsonContract.RequireNumber(point.GetProperty("y"), "y");
    }

    private static void ValidateSpans(JsonElement spans)
    {
        if (spans.ValueKind != JsonValueKind.Array)
        {
            throw new JsonException("spans must be an array.");
        }

        foreach (var span in spans.EnumerateArray())
        {
            JsonContract.RequireObject(span, "span");
            JsonContract.RequireExactProperties(span, JsonContract.Set("start", "end"));
            var start = span.GetProperty("start").GetInt32();
            var end = span.GetProperty("end").GetInt32();
            if (start < 0 || end <= start)
            {
                throw new JsonException("Highlight span is invalid.");
            }
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
        if (value.ValueKind != JsonValueKind.Object)
        {
            throw new JsonException($"{label} must be an object.");
        }
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
        if (value.ValueKind != JsonValueKind.String)
        {
            throw new JsonException($"{label} must be a string.");
        }
    }

    public static void RequireStringOrNull(JsonElement value, string label)
    {
        if (value.ValueKind is not (JsonValueKind.String or JsonValueKind.Null))
        {
            throw new JsonException($"{label} must be a string or null.");
        }
    }

    public static void RequireBooleanOrNull(JsonElement value, string label)
    {
        if (value.ValueKind is not (JsonValueKind.True or JsonValueKind.False or JsonValueKind.Null))
        {
            throw new JsonException($"{label} must be a boolean or null.");
        }
    }

    public static void RequireNumberOrNull(JsonElement value, string label)
    {
        if (value.ValueKind is not (JsonValueKind.Number or JsonValueKind.Null))
        {
            throw new JsonException($"{label} must be a number or null.");
        }

        if (value.ValueKind == JsonValueKind.Number)
        {
            RequireFiniteNumber(value, label);
        }
    }

    public static void RequireNumber(JsonElement value, string label)
    {
        if (value.ValueKind != JsonValueKind.Number)
        {
            throw new JsonException($"{label} must be a number.");
        }

        RequireFiniteNumber(value, label);
    }

    public static void RequireObjectOrNull(JsonElement value, string label)
    {
        if (value.ValueKind is not (JsonValueKind.Object or JsonValueKind.Null))
        {
            throw new JsonException($"{label} must be an object or null.");
        }
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
        foreach (var property in value.EnumerateObject())
        {
            RequireStringArray(property.Value);
        }
    }

    private static void RequireFiniteNumber(JsonElement value, string label)
    {
        var number = value.GetDouble();
        if (double.IsNaN(number) || double.IsInfinity(number))
        {
            throw new JsonException($"{label} must be finite.");
        }
    }
}
