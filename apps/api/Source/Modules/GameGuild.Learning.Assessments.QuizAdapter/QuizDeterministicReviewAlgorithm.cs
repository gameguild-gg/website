using System.Numerics;
using System.Text.Json;
using GameGuild.Learning.Assessments.Grading.Abstractions;
using GameGuild.Learning.Assessments.Grading.Contracts;
using GameGuild.Learning.Grading.Contracts;

namespace GameGuild.Learning.Assessments.QuizAdapter;

public sealed class QuizDeterministicReviewAlgorithm
{
    public ValueTask<GradeResultV1> EvaluateAsync(
        DeterministicReviewRequest request,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        JsonContract.RequireObject(request.NormalizedResponse, "Quiz response");
        var answers = request.NormalizedResponse.GetProperty("answers");
        JsonContract.RequireObject(answers, "Quiz answers");

        var results = request.ProjectedItems
            .Select(projectedItem => EvaluateItem(projectedItem, answers, request.HandlerKey, request.HandlerVersion))
            .ToArray();
        var state = results.All(result => result.State == GradeItemState.Graded) ? "final" : "partial";
        ScoreValue? score = state == "final"
            ? ScoreValue.Sum(results.Select(result => result.Score!.Value))
            : null;
        var maxScore = ScoreValue.Sum(results.Select(result => result.MaxScore));

        return ValueTask.FromResult(new GradeResultV1(
            GradingContractVersions.GradeResult,
            state,
            score,
            maxScore,
            results,
            []));
    }

    private static GradeItemResultV1 EvaluateItem(
        JsonElement projection,
        JsonElement answers,
        string handlerKey,
        string handlerVersion)
    {
        JsonContract.RequireObject(projection, "Quiz item projection");
        var itemId = projection.GetProperty("itemId").GetString()
            ?? throw new JsonException("Quiz item projection requires itemId.");
        var itemType = projection.GetProperty("itemType").GetString()
            ?? throw new JsonException("Quiz item projection requires itemType.");
        var maxScoreElement = projection.GetProperty("maxScore");
        var maxScore = ScoreValue.FromUnits(maxScoreElement.TryGetInt32(out var maxScoreUnits)
            ? maxScoreUnits
            : throw new JsonException("Quiz item maxScore must be an integer."));
        var entry = projection.GetProperty("authoringEntry");

        if (itemType is "ESSAY") return Unresolved(itemId, maxScore, handlerKey, handlerVersion, GradeItemState.Pending, "Essay requires instructor review.");
        if (itemType is "NUMERIC" or "FORMULA") return Unresolved(itemId, maxScore, handlerKey, handlerVersion, GradeItemState.Unsupported, "Generated formula prompts are not available.");
        if (itemType == "RATING" && !entry.TryGetProperty("correctRating", out _))
        {
            return Unresolved(itemId, maxScore, handlerKey, handlerVersion, GradeItemState.Unsupported, "Rating does not define a deterministic answer.");
        }

        if (!answers.TryGetProperty(itemId, out var answer)) return Graded(itemId, ScoreValue.Zero, maxScore, handlerKey, handlerVersion);
        if (!string.Equals(answer.GetProperty("type").GetString(), itemType, StringComparison.Ordinal))
        {
            return Graded(itemId, ScoreValue.Zero, maxScore, handlerKey, handlerVersion, "Answer type does not match question type.");
        }

        if (itemType == "MATCHING" && entry.TryGetProperty("allowPartialCredit", out var matchingPartial) && matchingPartial.GetBoolean())
        {
            return GradeMatchingPartial(itemId, entry, answer, maxScore, handlerKey, handlerVersion);
        }

        if (itemType == "ORDERING" && entry.TryGetProperty("allowPartialCredit", out var orderingPartial) && orderingPartial.GetBoolean())
        {
            return GradeOrderingPartial(itemId, entry, answer, maxScore, handlerKey, handlerVersion);
        }

        return Graded(itemId, IsCorrect(itemType, entry, answer) ? maxScore : ScoreValue.Zero, maxScore, handlerKey, handlerVersion);
    }

    private static bool IsCorrect(string itemType, JsonElement entry, JsonElement answer) => itemType switch
    {
        "SINGLE_CHOICE" => StringOrNull(answer, "optionId") == entry.GetProperty("correctOptionId").GetString(),
        "MULTIPLE_CHOICE" => SameSet(StringArray(answer, "optionIds"), StringArray(entry, "correctOptionIds")),
        "TRUE_FALSE" => BooleanOrNull(answer, "value") == entry.GetProperty("correctAnswer").GetBoolean(),
        "FILL_IN_THE_BLANK" => GradeBlanks(entry, answer),
        "SHORT_ANSWER" => MatchesAcceptedAnswer(
            answer.GetProperty("value").GetString() ?? string.Empty,
            StringArray(entry, "acceptedAnswers"),
            OptionalBoolean(entry, "caseSensitive")),
        "MATCHING" => GradeMatching(entry, answer),
        "ORDERING" => SameSequence(StringArray(answer, "itemIds"), ExpectedOrdering(entry)),
        "CATEGORIZATION" => GradeCategorization(entry, answer),
        "RATING" => NumberOrNull(answer, "value") == entry.GetProperty("correctRating").GetDouble(),
        "HOTSPOT" => GradeHotspot(entry, answer),
        "HIGHLIGHT" => GradeHighlight(entry, answer),
        _ => false,
    };

    private static bool GradeBlanks(JsonElement entry, JsonElement answer)
    {
        var blanks = entry.GetProperty("blanks").EnumerateArray().ToArray();
        if (blanks.Length == 0) return false;
        var values = answer.GetProperty("values");
        return blanks.All(blank =>
        {
            var id = blank.GetProperty("id").GetString()!;
            var value = values.TryGetProperty(id, out var rawValue) ? rawValue.GetString() ?? string.Empty : string.Empty;
            return GradeBlank(blank.GetProperty("input"), value);
        });
    }

    private static bool GradeBlank(JsonElement input, string rawValue)
    {
        var value = rawValue.Trim();
        if (value.Length == 0) return false;
        return input.GetProperty("type").GetString() switch
        {
            "TEXT" => MatchesAcceptedAnswer(value, StringArray(input, "acceptedAnswers"), OptionalBoolean(input, "caseSensitive")),
            "NUMBER" => GradeNumberBlank(input, value),
            "DROPDOWN" => value == StringArray(input, "options").FirstOrDefault(),
            "WORDBANK" => value == StringArray(input, "words").FirstOrDefault(),
            _ => false,
        };
    }

    private static bool GradeNumberBlank(JsonElement input, string value)
    {
        var numeric = value;
        if (input.TryGetProperty("unit", out var unitElement))
        {
            var unit = unitElement.GetString() ?? string.Empty;
            var hasUnit = numeric.EndsWith(unit, StringComparison.Ordinal);
            if (OptionalBoolean(input, "requireUnit") && !hasUnit) return false;
            if (hasUnit) numeric = numeric[..^unit.Length].Trim();
        }

        if (!double.TryParse(numeric, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var parsed) || !double.IsFinite(parsed))
        {
            return false;
        }
        if (!OptionalBoolean(input, "allowNegative", true) && parsed < 0) return false;
        if (input.TryGetProperty("requiredPrecision", out var precision))
        {
            var separator = numeric.IndexOf('.', StringComparison.Ordinal);
            var actualPrecision = separator < 0 ? 0 : numeric.Length - separator - 1;
            if (actualPrecision != precision.GetInt32()) return false;
        }

        var expected = input.GetProperty("correctValue").GetDouble();
        var tolerance = input.TryGetProperty("tolerance", out var toleranceElement) ? toleranceElement.GetDouble() : 0d;
        return Math.Abs(parsed - expected) <= tolerance;
    }

    private static bool GradeMatching(JsonElement entry, JsonElement answer)
    {
        var pairs = entry.GetProperty("pairs").EnumerateArray().ToArray();
        var matches = answer.GetProperty("matches");
        return pairs.Length > 0 && matches.EnumerateObject().Count() == pairs.Length &&
               pairs.All(pair => MatchIsCorrect(pair, matches));
    }

    private static GradeItemResultV1 GradeMatchingPartial(
        string itemId,
        JsonElement entry,
        JsonElement answer,
        ScoreValue maxScore,
        string handlerKey,
        string handlerVersion)
    {
        var pairs = entry.GetProperty("pairs").EnumerateArray().ToArray();
        if (pairs.Length == 0) return Graded(itemId, ScoreValue.Zero, maxScore, handlerKey, handlerVersion);
        var matches = answer.GetProperty("matches");
        var correct = pairs.Count(pair => MatchIsCorrect(pair, matches));
        return Graded(itemId, ScoreValue.ByRatio(maxScore, correct, pairs.Length), maxScore, handlerKey, handlerVersion);
    }

    private static bool MatchIsCorrect(JsonElement pair, JsonElement matches)
    {
        var id = pair.GetProperty("id").GetString()!;
        return matches.TryGetProperty(id, out var selected) && selected.GetString() == pair.GetProperty("right").GetString();
    }

    private static string[] ExpectedOrdering(JsonElement entry) => entry.GetProperty("items")
        .EnumerateArray()
        .OrderBy(item => item.GetProperty("correctPosition").GetInt32())
        .Select(item => item.GetProperty("id").GetString()!)
        .ToArray();

    private static GradeItemResultV1 GradeOrderingPartial(
        string itemId,
        JsonElement entry,
        JsonElement answer,
        ScoreValue maxScore,
        string handlerKey,
        string handlerVersion)
    {
        var expected = ExpectedOrdering(entry);
        if (expected.Length == 0) return Graded(itemId, ScoreValue.Zero, maxScore, handlerKey, handlerVersion);
        var actual = StringArray(answer, "itemIds");
        var correct = expected.Where((value, index) => actual.ElementAtOrDefault(index) == value).Count();
        return Graded(itemId, ScoreValue.ByRatio(maxScore, correct, expected.Length), maxScore, handlerKey, handlerVersion);
    }

    private static bool GradeCategorization(JsonElement entry, JsonElement answer)
    {
        var answersByItem = answer.GetProperty("categoryIdsByItem");
        var items = entry.GetProperty("items").EnumerateArray().ToArray();
        return items.Length > 0 && items.All(item =>
        {
            var itemId = item.GetProperty("id").GetString()!;
            var actual = answersByItem.TryGetProperty(itemId, out var selected)
                ? selected.EnumerateArray().Select(value => value.GetString()!).ToArray()
                : [];
            return SameSet(actual, StringArray(item, "correctCategoryIds"));
        });
    }

    private static bool GradeHotspot(JsonElement entry, JsonElement answer)
    {
        var point = answer.GetProperty("point");
        if (point.ValueKind == JsonValueKind.Null) return false;
        var x = point.GetProperty("x").GetDouble();
        var y = point.GetProperty("y").GetDouble();
        var imageWidth = entry.GetProperty("imageWidth").GetDouble();
        var imageHeight = entry.GetProperty("imageHeight").GetDouble();
        return entry.GetProperty("hotspots").EnumerateArray().Any(hotspot =>
        {
            var radius = hotspot.GetProperty("zones").EnumerateArray()
                .Select(zone => zone.GetProperty("radius").GetDouble())
                .Prepend(0d)
                .Max();
            var dx = ((x - hotspot.GetProperty("x").GetDouble()) / 100d) * imageWidth;
            var dy = ((y - hotspot.GetProperty("y").GetDouble()) / 100d) * imageHeight;
            return Math.Sqrt(dx * dx + dy * dy) <= (radius / 100d) * imageWidth;
        });
    }

    private static bool GradeHighlight(JsonElement entry, JsonElement answer)
    {
        var actual = Spans(answer.GetProperty("spans"));
        var expected = Spans(entry.GetProperty("highlights"));
        return actual.Length > 0 && expected.Length > 0 &&
               expected.All(value => actual.Any(candidate => Overlaps(candidate, value))) &&
               actual.All(value => expected.Any(candidate => Overlaps(candidate, value)));
    }

    private static (int Start, int End)[] Spans(JsonElement value) => value.EnumerateArray()
        .Select(span => (span.GetProperty("start").GetInt32(), span.GetProperty("end").GetInt32()))
        .ToArray();

    private static bool Overlaps((int Start, int End) left, (int Start, int End) right) =>
        left.Start < right.End && left.End > right.Start;

    private static bool MatchesAcceptedAnswer(string value, IEnumerable<string> accepted, bool caseSensitive)
    {
        var comparison = caseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
        var normalized = value.Trim();
        return normalized.Length > 0 && accepted.Any(candidate => string.Equals(normalized, candidate.Trim(), comparison));
    }

    private static string[] StringArray(JsonElement owner, string property) => owner.GetProperty(property)
        .EnumerateArray()
        .Select(value => value.GetString()!)
        .ToArray();

    private static bool SameSet(IEnumerable<string> left, IEnumerable<string> right) =>
        left.ToHashSet(StringComparer.Ordinal).SetEquals(right);

    private static bool SameSequence(IReadOnlyList<string> left, IReadOnlyList<string> right) =>
        left.Count == right.Count && left.SequenceEqual(right, StringComparer.Ordinal);

    private static string? StringOrNull(JsonElement owner, string property)
    {
        var value = owner.GetProperty(property);
        return value.ValueKind == JsonValueKind.Null ? null : value.GetString();
    }

    private static bool? BooleanOrNull(JsonElement owner, string property)
    {
        var value = owner.GetProperty(property);
        return value.ValueKind == JsonValueKind.Null ? null : value.GetBoolean();
    }

    private static double? NumberOrNull(JsonElement owner, string property)
    {
        var value = owner.GetProperty(property);
        return value.ValueKind == JsonValueKind.Null ? null : value.GetDouble();
    }

    private static bool OptionalBoolean(JsonElement owner, string property, bool defaultValue = false) =>
        owner.TryGetProperty(property, out var value) ? value.GetBoolean() : defaultValue;

    private static GradeItemResultV1 Graded(
        string itemId,
        ScoreValue score,
        ScoreValue maxScore,
        string handlerKey,
        string handlerVersion,
        string? feedback = null) =>
        new(
            itemId,
            GradeItemState.Graded,
            score,
            maxScore,
            [],
            ReviewMethod.AutomatedReview,
            handlerKey,
            handlerVersion,
            feedback);

    private static GradeItemResultV1 Unresolved(
        string itemId,
        ScoreValue maxScore,
        string handlerKey,
        string handlerVersion,
        GradeItemState state,
        string feedback) =>
        new(
            itemId,
            state,
            null,
            maxScore,
            [],
            ReviewMethod.AutomatedReview,
            handlerKey,
            handlerVersion,
            feedback);
}
