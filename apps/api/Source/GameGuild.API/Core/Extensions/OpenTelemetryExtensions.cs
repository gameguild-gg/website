using OpenTelemetry.Exporter;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace GameGuild.API.Setup;

public static class OpenTelemetryExtensions
{
    public static WebApplicationBuilder AddOpenTelemetryObservability(this WebApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        var productName = ApiProductComposition.Instance.ApplicationName;
        var options = builder.Configuration.GetSection(OpenTelemetryRuntimeOptions.SectionName)
            .Get<OpenTelemetryRuntimeOptions>() ?? new OpenTelemetryRuntimeOptions();

        if (!options.Enabled)
        {
            return builder;
        }

        ValidateOptions(options);

        builder.Services.AddOpenTelemetry()
            .ConfigureResource(resource => resource
                .AddService(
                    serviceName: string.IsNullOrWhiteSpace(options.ServiceName)
                        ? $"{productName}.API"
                        : options.ServiceName.Trim(),
                    serviceVersion: typeof(OpenTelemetryExtensions).Assembly.GetName().Version!.ToString(),
                    serviceInstanceId: string.IsNullOrWhiteSpace(options.ServiceInstanceId)
                        ? $"{Environment.MachineName}:{Environment.ProcessId}:{Guid.NewGuid():N}"
                        : options.ServiceInstanceId.Trim())
                .AddAttributes(new Dictionary<string, object>
                {
                    ["deployment.environment"] = builder.Environment.EnvironmentName,
                    ["service.namespace"] = productName
                }))
            .WithTracing(tracing =>
            {
                tracing
                    .AddAspNetCoreInstrumentation(instrumentation =>
                    {
                        instrumentation.RecordException = true;
                        instrumentation.Filter = context =>
                            !IsHealthPath(context.Request.Path);
                    })
                    .AddHttpClientInstrumentation(instrumentation => instrumentation.RecordException = true)
                    .AddEntityFrameworkCoreInstrumentation(instrumentation =>
                    {
                        instrumentation.SetDbStatementForText = options.IncludeSqlStatements;
                        instrumentation.SetDbStatementForStoredProcedure = options.IncludeSqlStatements;
                    })
                    .AddSource(
                        "GameGuild.Resources.QuotaManagement",
                        "GameGuild.Resources.Alerts",
                        "GameGuild.Analytics.Warehouse");

                if (options.ConsoleExporterEnabled)
                {
                    tracing.AddConsoleExporter();
                }

                if (!string.IsNullOrWhiteSpace(options.OtlpEndpoint))
                {
                    tracing.AddOtlpExporter(exporter =>
                    {
                        exporter.Endpoint = new Uri(options.OtlpEndpoint.Trim());
                    exporter.Protocol = ResolveProtocol(options.OtlpProtocol);
                    });
                }
            })
            .WithMetrics(metrics =>
            {
                metrics
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddMeter(
                        "GameGuild.API.RateLimiting",
                        "GameGuild.Identity.Authentication.PermissionBulkCheck",
                        "GameGuild.Identity.Authentication.RefreshTokens",
                        "GameGuild.Identity.Authorization.Cache");

                if (options.ConsoleExporterEnabled)
                {
                    metrics.AddConsoleExporter();
                }

                if (!string.IsNullOrWhiteSpace(options.OtlpEndpoint))
                {
                    metrics.AddOtlpExporter(exporter =>
                    {
                        exporter.Endpoint = new Uri(options.OtlpEndpoint.Trim());
                        exporter.Protocol = ResolveProtocol(options.OtlpProtocol);
                    });
                }
            });

        return builder;
    }

    private static OtlpExportProtocol ResolveProtocol(string? protocol)
    {
        if (string.IsNullOrWhiteSpace(protocol)
            || string.Equals(protocol.Trim(), "http/protobuf", StringComparison.OrdinalIgnoreCase))
        {
            return OtlpExportProtocol.HttpProtobuf;
        }

        if (string.Equals(protocol.Trim(), "grpc", StringComparison.OrdinalIgnoreCase))
        {
            return OtlpExportProtocol.Grpc;
        }

        throw new InvalidOperationException(
            $"OpenTelemetry:OtlpProtocol '{protocol}' is unsupported. Use 'http/protobuf' or 'grpc'.");
    }

    private static void ValidateOptions(OpenTelemetryRuntimeOptions options)
    {
        if (!options.ConsoleExporterEnabled && string.IsNullOrWhiteSpace(options.OtlpEndpoint))
        {
            throw new InvalidOperationException(
                "OpenTelemetry is enabled, but no exporter is configured. Set OpenTelemetry:OtlpEndpoint or enable OpenTelemetry:ConsoleExporterEnabled.");
        }

        if (string.IsNullOrWhiteSpace(options.OtlpEndpoint))
        {
            return;
        }

        if (!Uri.TryCreate(options.OtlpEndpoint.Trim(), UriKind.Absolute, out var endpoint)
            || (!string.Equals(endpoint.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)
                && !string.Equals(endpoint.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException(
                "OpenTelemetry:OtlpEndpoint must be an absolute HTTP or HTTPS URI.");
        }

        _ = ResolveProtocol(options.OtlpProtocol);
    }

    private static bool IsHealthPath(PathString path)
        => path.StartsWithSegments("/health", StringComparison.OrdinalIgnoreCase)
           || path.StartsWithSegments("/live", StringComparison.OrdinalIgnoreCase)
           || path.StartsWithSegments("/ready", StringComparison.OrdinalIgnoreCase);
}

public sealed class OpenTelemetryRuntimeOptions
{
    public const string SectionName = "OpenTelemetry";

    public bool Enabled { get; set; }
    public string ServiceName { get; set; } = string.Empty;
    public string ServiceInstanceId { get; set; } = string.Empty;
    public string? OtlpEndpoint { get; set; }
    public string? OtlpProtocol { get; set; } = "http/protobuf";
    public bool ConsoleExporterEnabled { get; set; }
    public bool IncludeSqlStatements { get; set; }
}
