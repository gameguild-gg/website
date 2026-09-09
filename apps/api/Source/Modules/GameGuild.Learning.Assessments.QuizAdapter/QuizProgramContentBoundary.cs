using System.Text.Json;
using System.Text.Json.Nodes;
using GameGuild.Learning.Courses;

namespace GameGuild.Learning.Assessments.QuizAdapter;

public sealed class QuizProgramContentBoundary(QuizAssessmentTypeAdapter adapter) :
    IProgramContentLearnerProjector,
    IProgramContentAcademicMutationGuard
{
    public ProgramContentType ContentType => ProgramContentType.Questionnaire;

    public JsonElement Project(JsonElement authoringDocument)
    {
        var projection = adapter.ProjectAuthoring(authoringDocument);
        var content = projection.Content;
        var learnerBlocks = new JsonObject();
        foreach (var item in projection.Items)
        {
            var entry = content.GetProperty("blocks").GetProperty(item.ItemId);
            learnerBlocks[item.ItemId] = JsonNode.Parse(QuizDeliveryGenerator.RedactEntry(entry).GetRawText());
        }

        var learner = new JsonObject
        {
            ["schemaVersion"] = content.GetProperty("schemaVersion").GetInt32(),
            ["order"] = JsonNode.Parse(content.GetProperty("order").GetRawText()),
            ["blocks"] = learnerBlocks,
        };
        return JsonSerializer.SerializeToElement(learner);
    }

    public string? GetRejection(ProgramContent content, ProgramContentAcademicMutation mutation)
    {
        if (content.Type != ProgramContentType.Questionnaire || string.IsNullOrWhiteSpace(content.JsonBody)) return null;
        try
        {
            using var document = JsonDocument.Parse(content.JsonBody);
            if (adapter.ProjectAuthoring(document.RootElement).Grading is null) return null;
        }
        catch (Exception exception) when (exception is JsonException or ArgumentException or FormatException)
        {
            return $"The quiz authoring document is invalid and cannot use generic academic mutations: {exception.Message}";
        }

        return mutation switch
        {
            ProgramContentAcademicMutation.Authoring => "Graded quiz content and assessment policy must be saved atomically through the quiz assessment draft endpoint.",
            ProgramContentAcademicMutation.Start => "Graded quizzes can only be started through the assessment execution workflow.",
            ProgramContentAcademicMutation.UpdateProgress => "Graded quiz progress is projected only by the grading workflow.",
            ProgramContentAcademicMutation.Submit => "Graded quiz answers must use the assessment execution workflow.",
            ProgramContentAcademicMutation.Complete => "Graded quiz completion is projected only by the grading workflow.",
            ProgramContentAcademicMutation.Grade => "Graded quiz results must use the assessment grading workflow.",
            ProgramContentAcademicMutation.Delete => "Graded quiz content and its assessment must be removed through an atomic assessment workflow.",
            _ => "This graded quiz mutation is reserved by the assessment workflow.",
        };
    }
}
