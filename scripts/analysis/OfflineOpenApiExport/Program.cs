using System.Globalization;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.OpenApi.Writers;
using GameGuild.API;
using Swashbuckle.AspNetCore.Swagger;

// Deliberately register presentation metadata only. Never execute API Program,
// register infrastructure, start hosted services, or contact database/cloud providers.
if (args.Length is < 1 or > 2)
    throw new ArgumentException("Usage: OfflineOpenApiExport NEW_OUTPUT_JSON [document=v1]");
var output = Path.GetFullPath(args[0]);
if (File.Exists(output)) throw new IOException($"Refusing to overwrite {output}");
var documentName = args.Length == 2 ? args[1] : "v1";
var builder = WebApplication.CreateSlimBuilder(new WebApplicationOptions
{
    ApplicationName = typeof(OpenApiExtensions).Assembly.GetName().Name,
    EnvironmentName = "Testing",
    ContentRootPath = AppContext.BaseDirectory,
    Args = []
});
builder.Configuration.Sources.Clear();
builder.Services.SetupControllers(builder.Configuration, null);
builder.Services.SetupApiVersioning(builder.Configuration, null);
builder.Services.SetupApiExplorer(builder.Configuration, null);
builder.Services.SetupOpenApi(builder.Configuration, null);
// A future registration must not silently turn this metadata tool into a worker host.
if (builder.Services.Any(descriptor => descriptor.ServiceType == typeof(IHostedService)))
    throw new InvalidOperationException("Offline export must not register hosted services.");
await using var app = builder.Build();
var document = app.Services.GetRequiredService<ISwaggerProvider>().GetSwagger(documentName);
if (document.Paths.Count == 0 || document.Components.Schemas.Count == 0)
    throw new InvalidOperationException("Export produced an empty API contract.");
using var serialized = new StringWriter(CultureInfo.InvariantCulture);
document.SerializeAsV3(new OpenApiJsonWriter(serialized));
Directory.CreateDirectory(Path.GetDirectoryName(output)!);
await using (var file = new FileStream(output, FileMode.CreateNew, FileAccess.Write, FileShare.None))
await using (var writer = new StreamWriter(file))
    await writer.WriteAsync(serialized.ToString());
Console.WriteLine($"Offline export: {document.Paths.Count} paths, {document.Components.Schemas.Count} schemas -> {output}");
