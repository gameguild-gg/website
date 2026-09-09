using System.Text.Json;
using System.Text.Json.Nodes;
using GameGuild.Learning.Assessments.Grading.Abstractions;
using GameGuild.Learning.Assessments.Grading.Contracts;
using GameGuild.Learning.Grading.Contracts;

namespace GameGuild.Learning.Assessments.QuizAdapter;

public sealed class QuizAuthoringAdapter(QuizItemProjector projector)
{
    public string ContentType => QuizAdapterContracts.ContentType;

    public AssessmentAuthoringProjectionV1 Project(JsonElement authoringDocument)
    {
        JsonContract.RequireObject(authoringDocument, "Quiz content document");
        JsonContract.RequireExactProperties(
            authoringDocument,
            JsonContract.Set("schemaVersion", "order", "blocks", "grading"),
            JsonContract.Set("schemaVersion", "order", "blocks"));

        if (!authoringDocument.GetProperty("schemaVersion").TryGetInt32(out var schemaVersion) || schemaVersion != 1)
            throw new JsonException("Quiz content schemaVersion must be 1.");

        var blocks = authoringDocument.GetProperty("blocks");
        JsonContract.RequireObject(blocks, "Quiz blocks");
        var order = ReadOrder(authoringDocument.GetProperty("order"));
        var blockIds = blocks.EnumerateObject().Select(property => property.Name).ToHashSet(StringComparer.Ordinal);
        if (!blockIds.SetEquals(order)) throw new JsonException("Quiz order and blocks must contain the same item IDs exactly once.");

        ContentGradingDefinitionV2? grading = null;
        if (authoringDocument.TryGetProperty("grading", out var gradingElement))
        {
            grading = JsonSerializer.Deserialize<ContentGradingDefinitionV2>(gradingElement.GetRawText(), GradingJson.Options)
                ?? throw new JsonException("Quiz grading definition is required.");
            GradingContractValidator.Validate(grading);
            if (!blockIds.SetEquals(grading.Items.Keys))
                throw new JsonException("Quiz grading items must match the authored quiz items exactly.");
        }

        var items = new List<AssessmentAuthoringItemProjectionV1>(order.Count);
        foreach (var itemId in order)
        {
            var entry = blocks.GetProperty(itemId);
            var privateProjection = projector.Project(itemId, entry);
            var itemType = privateProjection.GetProperty("itemType").GetString()
                ?? throw new JsonException($"Quiz item {itemId} type is required.");
            var maxScoreElement = privateProjection.GetProperty("maxScore");
            var maxScore = ScoreValue.FromUnits(maxScoreElement.TryGetInt32(out var maxScoreUnits)
                ? maxScoreUnits
                : throw new JsonException($"Quiz item {itemId} maxScore must be an integer."));
            items.Add(new AssessmentAuthoringItemProjectionV1(
                itemId,
                itemType,
                maxScore,
                privateProjection,
                QuizAdapterContracts.AdapterKey,
                QuizAdapterContracts.Version));
        }

        var content = JsonNode.Parse(authoringDocument.GetRawText())?.AsObject()
            ?? throw new JsonException("Quiz content document is required.");
        content.Remove("grading");

        return new AssessmentAuthoringProjectionV1(
            ContentType,
            JsonSerializer.SerializeToElement(content),
            grading,
            items);
    }

    private static IReadOnlyList<string> ReadOrder(JsonElement order)
    {
        if (order.ValueKind != JsonValueKind.Array) throw new JsonException("Quiz order must be an array.");
        var result = new List<string>();
        var unique = new HashSet<string>(StringComparer.Ordinal);
        foreach (var slot in order.EnumerateArray())
        {
            if (slot.ValueKind != JsonValueKind.Array || slot.GetArrayLength() != 2)
                throw new JsonException("Each quiz order entry must be [itemId, blockType].");
            var values = slot.EnumerateArray().ToArray();
            var itemId = values[0].ValueKind == JsonValueKind.String ? values[0].GetString() : null;
            var blockType = values[1].ValueKind == JsonValueKind.String ? values[1].GetString() : null;
            if (string.IsNullOrWhiteSpace(itemId) || blockType != "quiz")
                throw new JsonException("Quiz order contains an invalid item ID or block type.");
            if (!unique.Add(itemId)) throw new JsonException($"Quiz order contains duplicate item ID {itemId}.");
            result.Add(itemId);
        }
        return result;
    }
}
