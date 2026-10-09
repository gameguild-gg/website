using System.Globalization;
using GameGuild.Configuration;
using GameGuild.Configuration.PresentationLayer.FeatureFlags;
using GameGuild.Configuration.PresentationLayer.GraphQL;
using GameGuild.Configuration.PresentationLayer.HealthChecks;
using GameGuild.Configuration.PresentationLayer.Localization;
using GameGuild.Configuration.PresentationLayer.ModelValidation;
using GameGuild.Configuration.PresentationLayer.RequestContext;
using GameGuild.Configuration.PresentationLayer.ResponseCompression;
using GameGuild.Configuration.PresentationLayer.SignalR;
using GameGuild.API.Database;
using GameGuild.API.Core.Filters;
using GameGuild.API.Core.Security;
using GameGuild.Features;
using GameGuild.Identity.Authorization;
using GameGuild.Projects;
using HotChocolate.Authorization;
using HotChocolate.Types;
using Microsoft.AspNetCore.HttpLogging;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OpenFeature;
using ProblemDetailsDetailLevel = GameGuild.Configuration.PresentationLayer.ProblemDetails.ProblemDetailsDetailLevel;
using ProblemDetailsExceptionMapping = GameGuild.Configuration.PresentationLayer.ProblemDetails.ProblemDetailsExceptionMapping;
using ProblemDetailsLocalizedText = GameGuild.Configuration.PresentationLayer.ProblemDetails.ProblemDetailsLocalizedText;
using HttpLoggingOptions = GameGuild.Configuration.PresentationLayer.HttpLogging.HttpLoggingOptions;
using ProblemDetailsOptions = GameGuild.Configuration.PresentationLayer.ProblemDetails.ProblemDetailsOptions;

namespace GameGuild.API;

/// <summary>
///     Extension methods for configuring infrastructure services
///     (HttpLogging, ProblemDetails, Localization, ResponseCompression,
///     RequestContext, FeatureFlags, ModelValidation, HealthChecks, SignalR, GraphQL).
/// </summary>
public static class InfrastructureServiceCollectionExtensions
{
    public static IServiceCollection SetupHttpLogging(this IServiceCollection services, IConfiguration configuration,
        HttpLoggingOptions? options)
    {
        options ??= OptionBuilderUtilities.CreateAndBind(configuration, "HttpLogging",
            HttpLoggingOptions.CreateDefault);
        options.Validate();

        services.AddHttpLogging(loggingOptions =>
            {
                loggingOptions.LoggingFields = HttpLoggingFields.All;

                if (options.LogRequestHeaders) loggingOptions.LoggingFields |= HttpLoggingFields.RequestHeaders;
                if (options.LogResponseHeaders) loggingOptions.LoggingFields |= HttpLoggingFields.ResponseHeaders;
                if (options.LogRequestBody) loggingOptions.LoggingFields |= HttpLoggingFields.RequestBody;
                if (options.LogResponseBody) loggingOptions.LoggingFields |= HttpLoggingFields.ResponseBody;
            }
        );

        return services;
    }

    public static IServiceCollection SetupProblemDetails(this IServiceCollection services, IConfiguration configuration,
        ProblemDetailsOptions? options)
    {
        options ??= OptionBuilderUtilities.CreateAndBind(configuration, ProblemDetailsOptions.SectionName,
            ProblemDetailsOptions.CreateDefault);
        options.Validate();
        options = options.CreateSnapshot();

        // MVC can return ProblemDetails directly (including BaseApiController domain errors and
        // ApiController model-state failures), so run those responses through the same formatter
        // callback used by ASP.NET Core's ProblemDetails service.
        services.AddScoped<ProblemDetailsResultFilter>();
        services.Configure<MvcOptions>(mvcOptions => mvcOptions.Filters.AddService<ProblemDetailsResultFilter>());

        services.AddProblemDetails(problemDetailsOptions =>
            {
                problemDetailsOptions.CustomizeProblemDetails = context =>
                {
                    var httpContext = context.HttpContext;
                    var problem = context.ProblemDetails;
                    var exception = context.Exception;
                    var databaseSchemaNotReady = exception is not null &&
                                                 IsDatabaseSchemaNotReadyException(exception);
                    var mapping = exception is null ? null : FindExceptionMapping(exception, options);
                    var problemCode = problem.Extensions.TryGetValue("code", out var codeValue)
                        ? codeValue?.ToString()
                        : null;
                    var messageKey = mapping?.LocalizedMessageKey ??
                                     (databaseSchemaNotReady
                                         ? "database-schema-not-ready"
                                         : string.IsNullOrWhiteSpace(problemCode) ? "default" : problemCode);
                    var localizedText = FindLocalizedText(options, messageKey) ??
                                        (messageKey == "default" || !string.IsNullOrWhiteSpace(problemCode)
                                            ? null
                                            : FindLocalizedText(options, "default"));

                    problem.Instance = options.IncludeInstance ? httpContext.Request.Path : null;

                    if (mapping is not null)
                    {
                        problem.Type = mapping.Type;
                        problem.Title = localizedText?.Title ?? mapping.Title;
                        problem.Status = mapping.StatusCode;
                        httpContext.Response.StatusCode = mapping.StatusCode;
                        problem.Detail = options.DetailLevel == ProblemDetailsDetailLevel.Minimal
                            ? null
                            : localizedText?.Detail ?? mapping.Detail ?? options.DefaultDetail;
                    }
                    else if (databaseSchemaNotReady)
                    {
                        problem.Type = options.DatabaseSchemaNotReadyType;
                        problem.Title = localizedText?.Title ?? options.DatabaseSchemaNotReadyTitle;
                        problem.Status = options.DatabaseSchemaNotReadyStatusCode;
                        httpContext.Response.StatusCode = options.DatabaseSchemaNotReadyStatusCode;
                        problem.Detail = options.DetailLevel == ProblemDetailsDetailLevel.Minimal
                            ? null
                            : localizedText?.Detail ?? options.DatabaseSchemaNotReadyDetail;
                    }
                    else
                    {
                        var statusCode = problem.Status ?? httpContext.Response.StatusCode;
                        if (statusCode is < 400 or > 599)
                        {
                            statusCode = StatusCodes.Status500InternalServerError;
                        }
                        problem.Status = statusCode;
                        if (string.IsNullOrWhiteSpace(problem.Type))
                        {
                            problem.Type = options.DefaultType;
                        }
                        if (localizedText?.Title is not null && (exception is not null || string.IsNullOrWhiteSpace(problemCode)))
                        {
                            problem.Title = localizedText.Title;
                        }
                        else if (string.IsNullOrWhiteSpace(problem.Title))
                        {
                            problem.Title = localizedText?.Title ?? options.DefaultTitle;
                        }

                        if (options.DetailLevel == ProblemDetailsDetailLevel.Minimal)
                        {
                            problem.Detail = null;
                        }
                        else if (exception is not null)
                        {
                            // Unmapped exception messages are not returned to clients.
                            problem.Detail = localizedText?.Detail ?? options.DefaultDetail;
                        }
                        else if (localizedText?.Detail is not null)
                        {
                            problem.Detail = localizedText.Detail;
                        }
                        else if (string.IsNullOrWhiteSpace(problem.Detail))
                        {
                            problem.Detail = localizedText?.Detail ?? options.DefaultDetail;
                        }
                    }

                    foreach (var (name, value) in options.CustomExtensions)
                    {
                        problem.Extensions[name] = value;
                    }

                    if (options.IncludeTraceId)
                    {
                        problem.Extensions[options.TraceIdExtensionName] = httpContext.TraceIdentifier;
                    }

                    if (options.IncludeCorrelationId)
                    {
                        var correlationId = ResolveCorrelationId(httpContext, options.CorrelationIdHeaderName);
                        httpContext.Response.Headers[options.CorrelationIdHeaderName] = correlationId;
                        problem.Extensions[options.CorrelationIdExtensionName] = correlationId;
                    }

                    var isDevelopment = httpContext.RequestServices?.GetService<IWebHostEnvironment>()?.IsDevelopment() == true;
                    if (isDevelopment && options.IncludeExceptionDetails &&
                        options.DetailLevel == ProblemDetailsDetailLevel.Detailed &&
                        exception is not null)
                    {
                        problem.Extensions["exception"] = exception.ToString();
                    }
                };
            }
        );

        return services;
    }

    private static ProblemDetailsExceptionMapping? FindExceptionMapping(
        Exception exception,
        ProblemDetailsOptions options)
    {
        for (var current = exception; current is not null; current = current.InnerException)
        {
            for (var exceptionType = current.GetType(); exceptionType is not null; exceptionType = exceptionType.BaseType)
            {
                if (exceptionType.FullName is { } fullName &&
                    options.ExceptionMappings.TryGetValue(fullName, out var mapping))
                {
                    return mapping;
                }
            }
        }

        return null;
    }

    private static ProblemDetailsLocalizedText? FindLocalizedText(
        ProblemDetailsOptions options,
        string messageKey)
    {
        var culture = CultureInfo.CurrentUICulture;
        var cultureNames = new[] { culture.Name, culture.Parent.Name }
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.OrdinalIgnoreCase);

        foreach (var cultureName in cultureNames)
        {
            var locale = options.LocalizedMessages.FirstOrDefault(pair =>
                string.Equals(pair.Key, cultureName, StringComparison.OrdinalIgnoreCase)).Value;
            if (locale is null)
            {
                continue;
            }

            var localized = locale.FirstOrDefault(pair =>
                string.Equals(pair.Key, messageKey, StringComparison.OrdinalIgnoreCase)).Value;
            if (localized is not null)
            {
                return localized;
            }
        }

        return null;
    }

    private static string ResolveCorrelationId(HttpContext httpContext, string headerName)
    {
        var values = httpContext.Request.Headers[headerName];
        if (values.Count == 1)
        {
            var candidate = values[0];
            if (!string.IsNullOrWhiteSpace(candidate) && candidate.Length <= 128 &&
                candidate == candidate.Trim() && !candidate.Contains(',') &&
                !candidate.Any(char.IsControl))
            {
                return candidate;
            }
        }

        return httpContext.TraceIdentifier;
    }

    public static bool IsDatabaseSchemaNotReadyException(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        for (var current = exception; current is not null; current = current.InnerException)
        {
            var sqlState = current.GetType().GetProperty("SqlState")?.GetValue(current) as string;
            if (string.Equals(sqlState, "42P01", StringComparison.Ordinal))
            {
                return true;
            }

            if (current.Message.Contains("relation", StringComparison.OrdinalIgnoreCase) &&
                current.Message.Contains("does not exist", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    public static IServiceCollection SetupLocalization(this IServiceCollection services, IConfiguration configuration,
        LocalizationOptions? options)
    {
        options ??= OptionBuilderUtilities.CreateAndBind(configuration, "Localization",
            LocalizationOptions.CreateDefault);
        options.Validate();

        services.AddLocalization(localizationOptions => { localizationOptions.ResourcesPath = "Resources"; });

        services.Configure<RequestLocalizationOptions>(requestLocalizationOptions =>
            {
                var supportedCultures = options.SupportedCultures
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToArray();
                requestLocalizationOptions.DefaultRequestCulture = new RequestCulture(options.DefaultCulture);
                requestLocalizationOptions.SupportedCultures =
                    supportedCultures.Select(c => new CultureInfo(c)).ToList();
                requestLocalizationOptions.SupportedUICultures =
                    supportedCultures.Select(c => new CultureInfo(c)).ToList();
            }
        );

        return services;
    }

    public static IServiceCollection SetupResponseCompression(this IServiceCollection services,
        IConfiguration configuration, ResponseCompressionOptions? options)
    {
        options ??= OptionBuilderUtilities.CreateAndBind(configuration, "ResponseCompression",
            ResponseCompressionOptions.CreateDefault);
        options.Validate();

        services.AddResponseCompression(compressionOptions =>
            {
                compressionOptions.MimeTypes = options.MimeTypes
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToArray();
                compressionOptions.EnableForHttps = true;
            }
        );

        return services;
    }

    public static IServiceCollection SetupRequestContext(this IServiceCollection services, IConfiguration configuration,
        RequestContextOptions? options)
    {
        options ??= OptionBuilderUtilities.CreateAndBind(configuration, "RequestContext",
            RequestContextOptions.CreateDefault);
        options.Validate();

        // Placeholder for unified request context registration
        // Future implementation will handle user, tenant, location, and feature flag contexts
        return services;
    }

    public static IServiceCollection SetupFeatureFlags(this IServiceCollection services, IConfiguration configuration,
        FeatureFlagsOptions? options)
    {
        options ??= OptionBuilderUtilities.CreateAndBind(configuration, "FeatureFlags",
            FeatureFlagsOptions.CreateDefault);
        options.Validate();

        services.TryAddSingleton<DatabaseFeatureFlagProvider>();
        services.TryAddSingleton<FeatureProvider>(provider =>
            provider.GetRequiredService<DatabaseFeatureFlagProvider>());
        services.TryAddSingleton(_ => Api.Instance);
        services.AddHostedService<OpenFeatureHostedInitializer>();

        return services;
    }

    public static IServiceCollection SetupModelValidation(this IServiceCollection services,
        IConfiguration configuration, ModelValidationOptions? options)
    {
        options ??= OptionBuilderUtilities.CreateAndBind(configuration, "ModelValidation",
            ModelValidationOptions.CreateDefault);
        options.Validate();

        services.Configure<ApiBehaviorOptions>(behaviorOptions =>
        {
            behaviorOptions.SuppressModelStateInvalidFilter = options.SuppressModelStateInvalidFilter;
        });

        return services;
    }

    public static IServiceCollection SetupHealthChecks(this IServiceCollection services, IConfiguration configuration,
        HealthChecksOptions? options)
    {
        options ??= OptionBuilderUtilities.CreateAndBind(configuration, "HealthChecks",
            HealthChecksOptions.CreateDefault);
        options.Validate();

        services.AddHealthChecks()
            .AddCheck<DatabaseReadinessHealthCheck>(
                "database",
                tags: ["ready", "dependency"]);

        return services;
    }

    public static IServiceCollection SetupSignalR(this IServiceCollection services, IConfiguration configuration,
        SignalROptions? options)
    {
        options ??= OptionBuilderUtilities.CreateAndBind(configuration, "SignalR", SignalROptions.CreateDefault);
        options.Validate();

        services.AddSignalR(signalROptions =>
            {
                signalROptions.EnableDetailedErrors = options.EnableDetailedErrors;
                signalROptions.KeepAliveInterval = options.KeepAliveInterval;
                signalROptions.ClientTimeoutInterval = options.ClientTimeoutInterval;
                signalROptions.MaximumReceiveMessageSize = options.MaximumReceiveMessageSize;
            }
        );

        return services;
    }

    public static IServiceCollection SetupGraphQL(this IServiceCollection services, IConfiguration configuration,
        GraphQLOptions? options)
    {
        options ??= OptionBuilderUtilities.CreateAndBind(configuration, "GraphQL", GraphQLOptions.CreateDefault);
        options.Validate();

        if (!options.EnableGraphQL)
        {
            return services;
        }

        services.AddSingleton(options);
        services.TryAddScoped<IPermissionEvaluationLogSink, AuditPermissionEvaluationLogSink>();
        services.AddGraphQLServer()
            .AddAuthorization()
            .AddDirectiveType<ProjectAuthorizationDirectiveType>()
            .AddQueryType(descriptor => descriptor.Name("Query"))
            .AddType<ProjectGraphQLType>()
            .AddTypeExtension<ProjectQueries>()
            .AddTypeExtension<ProjectPermissionsResolvers>()
            .AddMutationType(descriptor => descriptor.Name("Mutation"))
            .AddTypeExtension<ProjectMutations>();

        return services;
    }
}

internal sealed class DatabaseReadinessHealthCheck(ApplicationDbContext dbContext)
    : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (!await dbContext.Database.CanConnectAsync(cancellationToken).ConfigureAwait(false))
            {
                return HealthCheckResult.Unhealthy("Application database is unreachable.");
            }

            var isRelational = dbContext.Database.IsRelational();
            var appliedMigrationCount = isRelational
                ? (await dbContext.Database.GetAppliedMigrationsAsync(cancellationToken).ConfigureAwait(false)).Count()
                : 0;

            var pendingMigrationCount = isRelational
                ? (await dbContext.Database.GetPendingMigrationsAsync(cancellationToken).ConfigureAwait(false)).Count()
                : 0;

            var data = new Dictionary<string, object>
            {
                ["database"] = isRelational
                    ? dbContext.Database.GetDbConnection().Database
                    : dbContext.Database.ProviderName!,
                ["appliedMigrations"] = appliedMigrationCount,
                ["pendingMigrations"] = pendingMigrationCount,
            };

            return pendingMigrationCount == 0
                ? HealthCheckResult.Healthy("Application database is reachable and migrations are current.", data)
                : HealthCheckResult.Degraded("Application database has pending migrations.", data: data);
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Application database health check failed.", ex);
        }
    }
}
