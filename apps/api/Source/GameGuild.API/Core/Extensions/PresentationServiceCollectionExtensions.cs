using System.Diagnostics;
using System.Reflection;
using System.Text.RegularExpressions;
using GameGuild.API.Core.Filters;
using GameGuild.API.Setup;
using GameGuild.Configuration;
using GameGuild.Configuration.ApplicationLayer;
using GameGuild.Configuration.PresentationLayer.Controllers;
using GameGuild.Configuration.PresentationLayer.Endpoints;
using GameGuild.Identity.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GameGuild.API;

/// <summary>
///     Extension methods for configuring presentation layer services.
/// </summary>
public static class PresentationServiceCollectionExtensions
{
    public static IServiceCollection SetupControllers(
        this IServiceCollection services,
        IConfiguration configuration,
        ControllersOptions? options)
    {
        using var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        var apiAssembly = typeof(DependencyInjection).Assembly;
        var logger = loggerFactory.CreateLogger(apiAssembly.GetName().Name!);
        var totalStopwatch = Stopwatch.StartNew();
        logger.LogInformation("Setting up controllers...");

        options ??= OptionBuilderUtilities.CreateAndBind(
            configuration,
            "Controllers",
            ControllersOptions.CreateDefault);
        options.Validate();

        var authenticationSecurityOptions = OptionBuilderUtilities.CreateAndBind(
            configuration,
            AuthenticationSecurityOptions.SectionName,
            static () => new AuthenticationSecurityOptions());
        var authenticationSecurityValidation = authenticationSecurityOptions.Validate();
        if (!authenticationSecurityValidation.IsValid)
        {
            throw new InvalidOperationException(
                $"Invalid {AuthenticationSecurityOptions.SectionName} configuration: " +
                string.Join("; ", authenticationSecurityValidation.Errors));
        }

        services.AddSingleton(authenticationSecurityOptions);
        services.AddScoped<global::GameGuild.Identity.Authentication.IPasswordSignInAdmissionService, PasswordSignInAdmissionService>();
        services.AddScoped<AuthenticationLockoutActionFilter>();

        var moduleConfiguration = new ModuleConfiguration();
        var modulesSection = configuration.GetSection("Modules");
        modulesSection.Bind(moduleConfiguration);
        var configuredModules = modulesSection.GetSection(nameof(ModuleConfiguration.EnabledModules)).Get<string[]>();
        if (configuredModules is { Length: > 0 })
        {
            moduleConfiguration.EnabledModules = configuredModules;
        }
        var applicationAssemblies = ModuleAssemblyCatalog.Resolve(apiAssembly, moduleConfiguration);

        var controllerStopwatch = Stopwatch.StartNew();
        var mvcBuilder = services.AddControllers(mvcOptions =>
            {
                mvcOptions.Conventions.Add(new MinimumOrderRouteApplicationModelConvention());
                mvcOptions.Filters.AddService<FieldMaskingResultFilter>();
                mvcOptions.Filters.AddService<AuthenticationLockoutActionFilter>();

                if (options.UseKebabCaseRoutes)
                {
                    mvcOptions.Conventions.Add(
                        new Microsoft.AspNetCore.Mvc.ApplicationModels.RouteTokenTransformerConvention(
                            new KebabCaseParameterTransformer()));
                }

                if (options.EnablePermissionAuthorizationFilter)
                {
                    mvcOptions.Filters.Add<ResourcePermissionAuthorizationFilter>();
                }
            })
            .ConfigureApplicationPartManager(manager => manager.ApplicationParts.Clear());

        services.AddScoped<FieldMaskingResultFilter>();

        foreach (var assembly in applicationAssemblies)
        {
            controllerStopwatch.Restart();
            mvcBuilder.AddApplicationPart(assembly);
            LogControllersFromAssembly(assembly, logger, controllerStopwatch);
        }

        mvcBuilder.AddJsonOptions(jsonOptions =>
        {
            jsonOptions.JsonSerializerOptions.MaxDepth = 128;
            jsonOptions.JsonSerializerOptions.ReferenceHandler =
                System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
            jsonOptions.JsonSerializerOptions.Converters.Add(
                new System.Text.Json.Serialization.JsonStringEnumConverter());
            jsonOptions.JsonSerializerOptions.PropertyNamingPolicy = options.JsonPropertyNamingPolicy switch
            {
                "CamelCase" => System.Text.Json.JsonNamingPolicy.CamelCase,
                "SnakeCaseLower" => System.Text.Json.JsonNamingPolicy.SnakeCaseLower,
                "SnakeCaseUpper" => System.Text.Json.JsonNamingPolicy.SnakeCaseUpper,
                "KebabCaseLower" => System.Text.Json.JsonNamingPolicy.KebabCaseLower,
                "KebabCaseUpper" => System.Text.Json.JsonNamingPolicy.KebabCaseUpper,
                _ => System.Text.Json.JsonNamingPolicy.CamelCase
            };
            jsonOptions.JsonSerializerOptions.WriteIndented = options.WriteIndentedJson;
        });

        totalStopwatch.Stop();
        logger.LogInformation("Completed controller setup in {ElapsedMs}ms", totalStopwatch.ElapsedMilliseconds);
        return services;
    }

    private static void LogControllersFromAssembly(Assembly assembly, ILogger logger, Stopwatch stopwatch)
    {
        var controllerBaseType = typeof(ControllerBase);
        Type[] allTypes;

        try
        {
            allTypes = assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException exception)
        {
            logger.LogWarning(
                "Could not load all types from {Assembly}: {Errors}",
                assembly.GetName().Name,
                string.Join("; ", exception.LoaderExceptions!.Select(error => error?.Message)));
            allTypes = exception.Types.Where(type => type is not null).ToArray()!;
        }

        var controllers = allTypes
            .Where(type => type.IsClass && !type.IsAbstract && controllerBaseType.IsAssignableFrom(type))
            .ToList();

        foreach (var controller in controllers)
        {
            logger.LogInformation(
                "Registered {ControllerName} in {ElapsedMs}ms",
                FormatControllerName(controller.Name),
                stopwatch.ElapsedMilliseconds);
            stopwatch.Restart();
        }
    }

    private static string FormatControllerName(string controllerName)
    {
        var formatted = Regex.Replace(controllerName, "([a-z])([A-Z])", "$1 $2");
        return Regex.Replace(formatted, "([A-Z]+)([A-Z][a-z])", "$1 $2");
    }

    public static IServiceCollection SetupEndpoints(
        this IServiceCollection services,
        IConfiguration configuration,
        EndpointsOptions? options)
    {
        options ??= OptionBuilderUtilities.CreateAndBind(
            configuration,
            "Endpoints",
            EndpointsOptions.CreateDefault);
        options.Validate();

        if (options.RegisterFromMainAssembly)
        {
            services.AddEndpoints(typeof(DependencyInjection).Assembly);
        }

        return services;
    }

    public static IServiceCollection SetupMiddlewares(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        using var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        var logger = loggerFactory.CreateLogger(typeof(DependencyInjection).Assembly.GetName().Name!);
        var totalStopwatch = Stopwatch.StartNew();
        logger.LogInformation("Starting middleware setup...");

        logger.LogInformation("Registered Correlation Id Middleware");
        logger.LogInformation("Registered Security Headers Middleware");
        logger.LogInformation("Registered Tenant Resolution Middleware");

        totalStopwatch.Stop();
        logger.LogInformation("Completed middleware setup in {ElapsedMs}ms", totalStopwatch.ElapsedMilliseconds);
        return services;
    }
}
