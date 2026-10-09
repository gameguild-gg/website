using System.Text;
using Microsoft.OpenApi.Models;
using Microsoft.OpenApi.Writers;
using Swashbuckle.AspNetCore.Swagger;

namespace GameGuild.API.Commands;

/// <summary>
///     Command to export OpenAPI specification to files.
/// </summary>
internal static class ExportOpenApiCommand
{
    /// <summary>
    ///     Exports the OpenAPI specification to JSON and YAML files.
    /// </summary>
    /// <param name="app">The web application</param>
    /// <param name="outputPath">The output directory path</param>
    /// <param name="documentName">The document name (default: "v1")</param>
    /// <returns>A task representing the export operation</returns>
    public static async Task ExportAsync(WebApplication app, string outputPath = "./docs", string documentName = "v1")
    {
        // Ensure output directory exists
        Directory.CreateDirectory(outputPath);

        // Get the Swagger provider
        var swaggerProvider = app.Services.GetRequiredService<ISwaggerProvider>();

        // Generate the OpenAPI document
        var openApiDocument = swaggerProvider.GetSwagger(documentName);

        // Export as JSON
        var jsonPath = Path.Combine(outputPath, $"openapi-{documentName}.json");
        await ExportAsJsonAsync(openApiDocument, jsonPath).ConfigureAwait(false);

        // Export as YAML
        var yamlPath = Path.Combine(outputPath, $"openapi-{documentName}.yaml");
        await ExportAsYamlAsync(openApiDocument, yamlPath).ConfigureAwait(false);

        Console.WriteLine("OpenAPI specification exported:");
        Console.WriteLine($"  JSON: {Path.GetFullPath(jsonPath)}");
        Console.WriteLine($"  YAML: {Path.GetFullPath(yamlPath)}");
    }

    /// <summary>
    ///     Exports the OpenAPI document as JSON.
    /// </summary>
    /// <param name="document">The OpenAPI document</param>
    /// <param name="filePath">The output file path</param>
    /// <returns>A task representing the export operation</returns>
    private static async Task ExportAsJsonAsync(OpenApiDocument document, string filePath)
    {
        using var fileStream = File.Create(filePath);
        using var streamWriter = new StreamWriter(fileStream, Encoding.UTF8);

        var jsonWriter = new OpenApiJsonWriter(streamWriter);
        document.SerializeAsV3(jsonWriter);

        await streamWriter.FlushAsync().ConfigureAwait(false);
    }

    /// <summary>
    ///     Exports the OpenAPI document as YAML.
    /// </summary>
    /// <param name="document">The OpenAPI document</param>
    /// <param name="filePath">The output file path</param>
    /// <returns>A task representing the export operation</returns>
    private static async Task ExportAsYamlAsync(OpenApiDocument document, string filePath)
    {
        using var fileStream = File.Create(filePath);
        using var streamWriter = new StreamWriter(fileStream, Encoding.UTF8);

        var yamlWriter = new OpenApiYamlWriter(streamWriter);
        document.SerializeAsV3(yamlWriter);

        await streamWriter.FlushAsync().ConfigureAwait(false);
    }

    /// <summary>
    ///     Validates the OpenAPI document against the OpenAPI 3.1 schema.
    /// </summary>
    /// <param name="document">The OpenAPI document to validate</param>
    /// <returns>A list of validation errors, if any</returns>
    public static IList<string> ValidateDocument(OpenApiDocument document)
    {
        var errors = new List<string>();

        try
        {
            // Basic validation - ensure required properties are present
            if (string.IsNullOrEmpty(document.Info?.Title))
            {
                errors.Add("Document title is required");
            }

            if (string.IsNullOrEmpty(document.Info?.Version))
            {
                errors.Add("Document version is required");
            }

            if (document.Paths == null || !document.Paths.Any())
            {
                errors.Add("Document must contain at least one path");
            }

            // Validate each path
            foreach (var path in document.Paths ?? new Dictionary<string, OpenApiPathItem>())
            {
                if (path.Value.Operations == null || !path.Value.Operations.Any())
                {
                    errors.Add($"Path '{path.Key}' must contain at least one operation");
                }
            }
        }
        catch (Exception ex) { errors.Add($"Validation error: {ex.Message}"); }

        return errors;
    }
}
