using System.Text.Json;
using GameGuild.Learning.Assessments.Grading.Contracts;
using GameGuild.Learning.Grading.Contracts;

namespace GameGuild.Learning.Assessments.QuizAdapter;

public sealed class QuizItemProjector
{
    public JsonElement Project(string itemId, JsonElement authoringItem)
    {
        if (string.IsNullOrWhiteSpace(itemId))
        {
            throw new ArgumentException("Item ID is required.", nameof(itemId));
        }

        QuizAuthoringEntryValidator.Validate(authoringItem);

        var itemType = authoringItem.GetProperty("type").GetString();
        if (itemType is null)
        {
            throw new JsonException("Quiz entry type is required.");
        }

        var maxScore = authoringItem.TryGetProperty("points", out var points)
            ? ScoreValue.FromUnits(points.TryGetInt32(out var units)
                ? units
                : throw new JsonException("Quiz points must be a JSON integer."))
            : ScoreValue.FromUnits(100);
        var partialCreditAlgorithm = itemType switch
        {
            "MATCHING" when authoringItem.TryGetProperty("allowPartialCredit", out var matching) && matching.GetBoolean() =>
                QuizAdapterContracts.MatchingPartialCreditAlgorithm,
            "ORDERING" when authoringItem.TryGetProperty("allowPartialCredit", out var ordering) && ordering.GetBoolean() =>
                QuizAdapterContracts.OrderingPartialCreditAlgorithm,
            _ => null,
        };

        return JsonSerializer.SerializeToElement(new
        {
            schemaVersion = 1,
            itemId,
            itemType,
            maxScore,
            partialCreditAlgorithm,
            source = new { contentType = QuizAdapterContracts.ContentType, itemId },
            authoringEntry = authoringItem.Clone(),
        }, GradingJson.Options);
    }
}
