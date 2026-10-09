using GameGuild.Learning.Assessments;
using GameGuild.Learning.Assessments.Grading.Contracts;
using GameGuild.Learning.Courses;
using GameGuild.Learning.Grading.Contracts;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
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
        {
            return;
        }

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
        ApplyNullableValueTypeProperties(schema, context.Type);

        if (context.Type == typeof(ActivitySettings))
        {
            ApplyActivitySettings(schema, context);
            return;
        }

        if (context.Type == typeof(ReviewMethods))
        {
            ApplyReviewMethods(schema);
            return;
        }

        if (context.Type != typeof(ScoreValue) && context.Type != typeof(PercentValue))
        {
            return;
        }

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

    private static void ApplyActivitySettings(OpenApiSchema schema, SchemaFilterContext context)
    {
        schema.Type = null;
        schema.Format = null;
        schema.Properties?.Clear();
        schema.Required?.Clear();
        schema.AdditionalProperties = null;
        schema.AdditionalPropertiesAllowed = true;

        var discussionSchema = context.SchemaGenerator.GenerateSchema(typeof(DiscussionActivitySettings), context.SchemaRepository);
        var reflectionSchema = context.SchemaGenerator.GenerateSchema(typeof(ReflectionActivitySettings), context.SchemaRepository);
        var surveySchema = context.SchemaGenerator.GenerateSchema(typeof(SurveyActivitySettings), context.SchemaRepository);

        ApplyActivitySettingsDiscriminator(discussionSchema, context.SchemaRepository, "discussion");
        ApplyActivitySettingsDiscriminator(reflectionSchema, context.SchemaRepository, "reflection");
        ApplyActivitySettingsDiscriminator(surveySchema, context.SchemaRepository, "survey");

        schema.OneOf = [discussionSchema, reflectionSchema, surveySchema];
        schema.Discriminator = new OpenApiDiscriminator
        {
            PropertyName = "kind",
            Mapping = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["discussion"] = "#/components/schemas/Learning_Courses_DiscussionActivitySettings",
                ["reflection"] = "#/components/schemas/Learning_Courses_ReflectionActivitySettings",
                ["survey"] = "#/components/schemas/Learning_Courses_SurveyActivitySettings"
            }
        };
        schema.Description = "Polymorphic activity settings selected by the kind discriminator.";
    }

    private static void ApplyActivitySettingsDiscriminator(
        OpenApiSchema schemaReference,
        SchemaRepository schemaRepository,
        string discriminatorValue)
    {
        if (schemaReference.Reference?.Id is not { } schemaId
            || !schemaRepository.Schemas.TryGetValue(schemaId, out var schema))
        {
            return;
        }

        schema.Properties ??= new Dictionary<string, OpenApiSchema>(StringComparer.Ordinal);
        schema.Properties["kind"] = new OpenApiSchema
        {
            Type = "string",
            Description = "Identifies the activity settings type.",
            Enum = [new OpenApiString(discriminatorValue)],
            Example = new OpenApiString(discriminatorValue)
        };
        schema.Required ??= new HashSet<string>(StringComparer.Ordinal);
        schema.Required.Add("kind");
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

    private static void ApplyNullableValueTypeProperties(OpenApiSchema schema, Type contractType)
    {
        if (schema.Properties is null || schema.Properties.Count == 0)
        {
            return;
        }

        foreach (var property in contractType.GetProperties(BindingFlags.Instance | BindingFlags.Public))
        {
            var valueType = Nullable.GetUnderlyingType(property.PropertyType);
            if (valueType?.Namespace != typeof(ScoreValue).Namespace &&
                valueType?.Namespace != typeof(AttemptContributionMode).Namespace)
            {
                continue;
            }

            var jsonName = property.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name
                ?? JsonNamingPolicy.CamelCase.ConvertName(property.Name);

            if (!schema.Properties.TryGetValue(jsonName, out var propertySchema) || propertySchema.Reference is null)
            {
                continue;
            }

            var reference = propertySchema.Reference;
            propertySchema.Reference = null;
            propertySchema.AllOf = [new OpenApiSchema { Reference = reference }];
            propertySchema.Nullable = true;
        }
    }
}
