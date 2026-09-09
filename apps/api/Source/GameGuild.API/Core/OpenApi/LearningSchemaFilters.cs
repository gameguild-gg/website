using GameGuild.Learning.Assessments;
using GameGuild.Learning.Assessments.Grading.Contracts;
using GameGuild.Learning.Courses;
using GameGuild.Learning.Grading.Contracts;
using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace GameGuild.API.Setup;

internal sealed class LegacyProgramContentTypeSchemaFilter : ISchemaFilter
{
    private static readonly HashSet<string> LegacyValues =
    [
        nameof(ProgramContentType.Page),
        nameof(ProgramContentType.Challenge),
    ];

    public void Apply(OpenApiSchema schema, SchemaFilterContext context)
    {
        if (context.Type != typeof(ProgramContentType) || schema.Enum is null)
            return;

        schema.Enum = schema.Enum
            .Where(value => value is not OpenApiString text || !LegacyValues.Contains(text.Value))
            .ToList();
        schema.Description =
            $"{schema.Description} Legacy values Page and Challenge are normalized on read and are not valid for new content."
                .Trim();
    }
}

internal sealed class LearningContractSchemaFilter : ISchemaFilter
{
    private static readonly int[] ValidReviewWorkflows = [0, 1, 2, 4, 8, 9, 10, 12, 16, 24];

    public void Apply(OpenApiSchema schema, SchemaFilterContext context)
    {
        if (context.Type == typeof(ReviewMethods))
        {
            ApplyReviewMethods(schema);
            return;
        }

        if (context.Type != typeof(ScoreValue) && context.Type != typeof(PercentValue))
            return;

        schema.Type = "integer";
        schema.Format = "int32";
        schema.Minimum = 0;
        schema.Maximum = context.Type == typeof(PercentValue) ? PercentValue.Hundred.Units : int.MaxValue;
        schema.Properties?.Clear();
        schema.Required?.Clear();
        schema.AdditionalProperties = null;
        schema.Description = context.Type == typeof(PercentValue)
            ? "Percentage in integer units scaled by 100 (100 units = 1%, range 0..10000)."
            : "Score in non-negative integer units scaled by 100 (100 units = 1 point).";
    }

    private static void ApplyReviewMethods(OpenApiSchema schema)
    {
        schema.Type = "integer";
        schema.Format = "int32";
        schema.Minimum = 0;
        schema.Maximum = 24;
        schema.Enum = ValidReviewWorkflows
            .Select(value => (IOpenApiAny)new OpenApiInteger(value))
            .ToList();
        schema.Properties?.Clear();
        schema.Required?.Clear();
        schema.AdditionalProperties = null;
        schema.Description =
            "Numeric review-workflow bitmask. Valid values are 0, 1, 2, 4, 8, 9, 10, 12, 16, and 24.";
    }
}
