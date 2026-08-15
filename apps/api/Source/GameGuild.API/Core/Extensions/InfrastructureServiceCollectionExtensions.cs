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
using GameGuild.Features;
using Microsoft.AspNetCore.HttpLogging;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OpenFeature;
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
        options ??= OptionBuilderUtilities.CreateAndBind(configuration, "ProblemDetails",
            ProblemDetailsOptions.CreateDefault);
        options.Validate();

        services.AddProblemDetails(problemDetailsOptions =>
            {
                problemDetailsOptions.CustomizeProblemDetails = context =>
                {
                    context.ProblemDetails.Instance = context.HttpContext.Request.Path;
                    context.ProblemDetails.Extensions["traceId"] = context.HttpContext.TraceIdentifier;

                    if (context.Exception is not null && IsDatabaseSchemaNotReadyException(context.Exception))
                    {
                        context.HttpContext.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
                        context.ProblemDetails.Type = "urn:problem-type:database-schema-not-ready";
                        context.ProblemDetails.Title = "Database Schema Not Ready";
                        context.ProblemDetails.Status = StatusCodes.Status503ServiceUnavailable;
                        context.ProblemDetails.Detail = "Database schema is not ready. Apply pending migrations before retrying.";
                    }

                    if (options.IncludeExceptionDetails && context.Exception != null)
                    {
                        context.ProblemDetails.Extensions["exception"] = context.Exception.ToString();
                    }
                };
            }
        );

        return services;
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
                var supportedCultures = options.SupportedCultures;
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
                compressionOptions.MimeTypes = options.MimeTypes;
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

        // GraphQL services can be configured by the application layer if enabled.

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
