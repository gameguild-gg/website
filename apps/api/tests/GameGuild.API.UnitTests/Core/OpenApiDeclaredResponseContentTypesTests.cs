using GameGuild.API.Core.OpenApi;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.OpenApi.Models;
using Moq;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace GameGuild.API.UnitTests.Core;

public sealed class OpenApiDeclaredResponseContentTypesTests
{
    [Fact]
    public void Explicit_csv_success_does_not_advertise_inherited_json_or_change_errors()
    {
        var operation = CreateOperation();
        var schema = operation.Responses["200"].Content["text/csv"].Schema;
        Apply(operation, nameof(ResponseMetadataActions.Csv));
        Assert.Equal(new[] { "text/csv" }, operation.Responses["200"].Content.Keys);
        Assert.Same(schema, operation.Responses["200"].Content["text/csv"].Schema);
        Assert.Equal(new[] { "application/problem+json" }, operation.Responses["400"].Content.Keys);
    }

    [Fact]
    public void Responses_without_explicit_media_types_preserve_existing_documentation()
    {
        var operation = CreateOperation();
        Apply(operation, nameof(ResponseMetadataActions.Default));
        Assert.Equal(3, operation.Responses["200"].Content.Count);
    }

    [Fact]
    public void Multiple_declarations_for_one_status_preserve_all_explicit_representations()
    {
        var operation = CreateOperation();
        Apply(operation, nameof(ResponseMetadataActions.Multiple));
        Assert.Equal(new[] { "text/csv", "application/zip" }, operation.Responses["200"].Content.Keys);
    }

    [Fact]
    public void Missing_response_status_is_not_synthesized_or_replaced()
    {
        var operation = CreateOperation();
        operation.Responses.Remove("200");
        Apply(operation, nameof(ResponseMetadataActions.Csv));
        Assert.Equal(new[] { "400" }, operation.Responses.Keys);
    }

    private static void Apply(OpenApiOperation operation, string method)
    {
        var context = new OperationFilterContext(new ApiDescription(), Mock.Of<ISchemaGenerator>(),
            new SchemaRepository(), typeof(ResponseMetadataActions).GetMethod(method)!);
        new DeclaredResponseContentTypesOperationFilter().Apply(operation, context);
    }

    private static OpenApiOperation CreateOperation() => new()
    {
        Responses = new OpenApiResponses
        {
            ["200"] = new OpenApiResponse
            {
                Content = new Dictionary<string, OpenApiMediaType>
                {
                    ["text/csv"] = new() { Schema = new() { Type = "string", Format = "binary" } },
                    ["application/json"] = new() { Schema = new() { Type = "string", Format = "binary" } },
                    ["application/zip"] = new() { Schema = new() { Type = "string", Format = "binary" } }
                }
            },
            ["400"] = new OpenApiResponse
            {
                Content = new Dictionary<string, OpenApiMediaType> { ["application/problem+json"] = new() }
            }
        }
    };

    private sealed class ResponseMetadataActions
    {
        [ProducesResponseType(typeof(FileContentResult), 200, "text/csv")]
        public void Csv()
        {
            // Only the response metadata is inspected; this action is never executed.
        }

        [ProducesResponseType(typeof(FileContentResult), 200)]
        public void Default()
        {
            // Only the response metadata is inspected; this action is never executed.
        }

        [ProducesResponseType(typeof(FileContentResult), 200, "text/csv")]
        [ProducesResponseType(typeof(FileContentResult), 200, "application/zip")]
        public void Multiple()
        {
            // Only the response metadata is inspected; this action is never executed.
        }
    }
}
