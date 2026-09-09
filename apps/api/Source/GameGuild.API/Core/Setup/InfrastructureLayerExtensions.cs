using System.Diagnostics;
using System.Reflection;
using System.Text.RegularExpressions;
using GameGuild.API.Context;
using GameGuild.AI;
using GameGuild.Analytics;
using GameGuild.API.Database;
using GameGuild.API.Eventing;
using GameGuild.API.Core.Quotas;
using GameGuild.API.Core.CostAccounting;
using GameGuild.Assets.Extensions;
using GameGuild.Commerce.Billing;
using GameGuild.Commerce.Orders;
using GameGuild.Commerce.Payments;
using GameGuild.Commerce.Subscriptions;
using GameGuild.Compliance.Audit;
using GameGuild.Features;
using GameGuild.Identity.Authentication;
using GameGuild.Identity.Authorization;
using GameGuild.Identity.Context;
using GameGuild.Identity.Tenants;
using GameGuild.Localization;
using GameGuild.Monitoring.SLA;
using GameGuild.Configuration.ConfigurationFromAPI.InfrastructureLayer;
using GameGuild.Configuration.InfrastructureLayer;
using GameGuild.Content.Pages;
using GameGuild.Resources;
using GameGuild.Resources.Contents;
using GameGuild.Notifications;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace GameGuild.API.Setup;

/// <summary>
///     Extension methods for configuring the infrastructure layer services.
///     Includes repositories, external service integrations, and data access components.
/// </summary>
public static class InfrastructureLayerExtensions
{
    #region WebApplicationBuilder Extensions

    /// <summary>
    ///     Adds the infrastructure layer services with default options.
    /// </summary>
    /// <param name="builder">The WebApplicationBuilder instance</param>
    /// <returns>The WebApplicationBuilder for method chaining</returns>
    /// <exception cref="ArgumentNullException">Thrown when the builder is null</exception>
    public static WebApplicationBuilder AddInfrastructureLayer(this WebApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        var logger = StartupLogger.Create();
        builder.Services.AddInfrastructureLayer(builder.Configuration, logger);

        return builder;
    }

    /// <summary>
    ///     Adds the infrastructure layer services with custom options.
    /// </summary>
    /// <param name="builder">The WebApplicationBuilder instance</param>
    /// <param name="configureOptions">Action to configure infrastructure layer options</param>
    /// <returns>The WebApplicationBuilder for method chaining</returns>
    /// <exception cref="ArgumentNullException">Thrown when the builder or configureOptions is null</exception>
    public static WebApplicationBuilder AddInfrastructureLayer(this WebApplicationBuilder builder,
        Action<InfrastructureLayerSetupOptions> configureOptions)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configureOptions);

        var logger = StartupLogger.Create();
        builder.Services.AddInfrastructureLayer(builder.Configuration, logger, configureOptions);

        return builder;
    }

    #endregion

    #region IServiceCollection Extensions

    /// <summary>
    ///     Adds infrastructure layer services to the service collection with default options.
    /// </summary>
    /// <param name="services">The service collection to configure</param>
    /// <param name="configuration">The application configuration</param>
    /// <param name="logger">Logger for diagnostics</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddInfrastructureLayer(
        this IServiceCollection services,
        IConfiguration configuration,
        ILogger logger)
    {
        return services.AddInfrastructureLayer(configuration, logger, _ => { });
    }

    /// <summary>
    ///     Adds infrastructure layer services to the service collection with custom options.
    /// </summary>
    /// <param name="services">The service collection to configure</param>
    /// <param name="configuration">The application configuration</param>
    /// <param name="logger">Logger for diagnostics</param>
    /// <param name="configureOptions">Action to configure infrastructure layer options</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddInfrastructureLayer(
        this IServiceCollection services,
        IConfiguration configuration,
        ILogger logger,
        Action<InfrastructureLayerSetupOptions> configureOptions)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(configureOptions);

        var setupOptions = new InfrastructureLayerSetupOptions();
        configureOptions(setupOptions);

        var stopwatch = Stopwatch.StartNew();
        logger.LogInformation("Starting infrastructure layer setup...");

        var options = InfrastructureLayerOptionsBuilder.CreateWithValidation(configuration);
        options.Validate();

        // Infrastructure layer services registration order matters for some services.

        // 01. Database (ApplicationDbContext with PostgreSQL)
        var stepStopwatch = Stopwatch.StartNew();
        if (options.EnableDatabase)
        {
            services.AddDatabase(configuration, options.Database);
            services.AddDurableEventTransport();
            logger.LogInformation("Database registered in {ElapsedMs}ms", stepStopwatch.ElapsedMilliseconds);
        }

        // 02. Memory Caching (foundation for other services)
        if (options.EnableMemoryCaching)
        {
            stepStopwatch.Restart();
            services.SetupMemoryCaching(configuration, options.MemoryCaching);
            logger.LogInformation("MemoryCaching registered in {ElapsedMs}ms", stepStopwatch.ElapsedMilliseconds);
        }

        // 03. HTTP Client (required for OAuth and external API calls)
        stepStopwatch.Restart();
        services.AddHttpClient();
        logger.LogInformation("HttpClient registered in {ElapsedMs}ms", stepStopwatch.ElapsedMilliseconds);

        // 03x. Identity Context Module (IActorContextAccessor, ISecurityAuditLogger - required by many services)
        stepStopwatch.Restart();
        services.AddIdentityContextModule(configuration);
        services.AddScoped<IRequestContextAccessor, RequestContextAccessor>();
        logger.LogInformation("Identity Context Module registered in {ElapsedMs}ms", stepStopwatch.ElapsedMilliseconds);

        // 03y. Compliance Audit Module (unified audit queries + ELK-ready structured audit services)
        stepStopwatch.Restart();
        services.AddAuditServices();
        logger.LogInformation("Compliance Audit Module registered in {ElapsedMs}ms", stepStopwatch.ElapsedMilliseconds);

        // 03a. Authentication Application (command handlers, validators, core auth services)
        stepStopwatch.Restart();
        services.AddAuthenticationApplication();
        logger.LogInformation("Authentication Application registered in {ElapsedMs}ms", stepStopwatch.ElapsedMilliseconds);

        // 03b. Authentication Data (repositories, JWT services, security services)
        stepStopwatch.Restart();
        services.AddAuthenticationData(configuration);
        logger.LogInformation("Authentication Data registered in {ElapsedMs}ms", stepStopwatch.ElapsedMilliseconds);

        // 04. Authorization Application (policy infrastructure, Access Control List service, Permission Services)
        stepStopwatch.Restart();
        services.AddAuthorizationApplication();
        logger.LogInformation("Authorization Application registered in {ElapsedMs}ms", stepStopwatch.ElapsedMilliseconds);

        // 05. Authorization Repositories
        stepStopwatch.Restart();
        services.AddAuthorizationRepositories();
        logger.LogInformation("Authorization Repositories registered in {ElapsedMs}ms", stepStopwatch.ElapsedMilliseconds);

        // 07. Advanced Permission Services (JIT elevation, delegation, SoD, access reviews, delegated admin)
        stepStopwatch.Restart();
        services.AddAdvancedPermissionServices();
        logger.LogInformation("Advanced Permission Services registered in {ElapsedMs}ms", stepStopwatch.ElapsedMilliseconds);

        // 08. Rule-based Authorization
        stepStopwatch.Restart();
        services.AddRuleBasedAuthorization();
        logger.LogInformation("Rule-based Authorization registered in {ElapsedMs}ms", stepStopwatch.ElapsedMilliseconds);

        // 09. Permission Services (audit, templates)
        stepStopwatch.Restart();
        services.AddPermissionServices();
        logger.LogInformation("Permission Services registered in {ElapsedMs}ms", stepStopwatch.ElapsedMilliseconds);

        // 10. Unified Authorization Layer (Policy Gates, Permission Resolution)
        stepStopwatch.Restart();
        services.AddUnifiedAuthorizationLayer();
        logger.LogInformation("Unified Authorization Layer registered in {ElapsedMs}ms", stepStopwatch.ElapsedMilliseconds);

        // 10a. Resources Module (quota, usage tracking, SLA services)
        stepStopwatch.Restart();
        services.AddResourcesInfrastructure(configuration);
        services.AddResourceQuotaBehavior();
        services.AddTransient(typeof(GameGuild.CQRS.IPipelineBehavior<,>), typeof(UseCaseOperationBehavior<,>));
        services.Configure<QuotaReconciliationOptions>(
            configuration.GetSection(QuotaReconciliationOptions.SectionName));
        services.AddScoped<QuotaReconciliationService>();
        services.AddHostedService<QuotaReconciliationBackgroundService>();
        logger.LogInformation("Resources Module registered in {ElapsedMs}ms", stepStopwatch.ElapsedMilliseconds);

        // 10a.0. SLA Monitoring Module (repositories, calculators, alerts, and monitoring service)
        stepStopwatch.Restart();
        services.AddSlaMonitoringApplication();
        logger.LogInformation("SLA Monitoring Module registered in {ElapsedMs}ms", stepStopwatch.ElapsedMilliseconds);

        // 10a.1. Resources Contents Module (templates, contract generation, content versioning)
        stepStopwatch.Restart();
        new ContentsModule().ConfigureServices(services, configuration);
        logger.LogInformation("Resources Contents Module registered in {ElapsedMs}ms", stepStopwatch.ElapsedMilliseconds);

        // 10ab. Analytics Module (events, KPIs, and warehouse materialization)
        stepStopwatch.Restart();
        services.AddAnalyticsModule(configuration);
        logger.LogInformation("Analytics Module registered in {ElapsedMs}ms", stepStopwatch.ElapsedMilliseconds);

        // 10ac. AI Module (provider adapters, history, prompt templates)
        stepStopwatch.Restart();
        services.AddAiModule(configuration);
        logger.LogInformation("AI Module registered in {ElapsedMs}ms", stepStopwatch.ElapsedMilliseconds);

        // 10aa. Assets Module (S3 storage, upload/access services, asset security helpers)
        stepStopwatch.Restart();
        services.AddOrdersModule();
        logger.LogInformation("Commerce Orders Module registered in {ElapsedMs}ms", stepStopwatch.ElapsedMilliseconds);

        // 10aa. Assets Module (S3 storage, upload/access services, asset security helpers)
        stepStopwatch.Restart();
        services.AddAssetsModule(configuration);
        logger.LogInformation("Assets Module registered in {ElapsedMs}ms", stepStopwatch.ElapsedMilliseconds);

        // 10b. Commerce Subscriptions Module (must be registered before Billing since Billing depends on it)
        stepStopwatch.Restart();
        services.AddSubscriptionsModule();
        logger.LogInformation("Subscriptions Module registered in {ElapsedMs}ms", stepStopwatch.ElapsedMilliseconds);

        // 10d. Commerce Billing Module (webhook services for Stripe, PayPal, ApplePay)
        stepStopwatch.Restart();
        services.AddBillingModule(configuration);
        logger.LogInformation("Billing Module registered in {ElapsedMs}ms", stepStopwatch.ElapsedMilliseconds);

        // 10e. Commerce Payments Module (payment gateway, payment services)
        stepStopwatch.Restart();
        services.AddPaymentsModule(configuration);
        logger.LogInformation("Payments Module registered in {ElapsedMs}ms", stepStopwatch.ElapsedMilliseconds);

        // 10f. Features Module (feature flags, OpenFeature provider, analytics)
        stepStopwatch.Restart();
        services.AddFeaturesModule();
        logger.LogInformation("Features Module registered in {ElapsedMs}ms", stepStopwatch.ElapsedMilliseconds);

        // 10g. Content Pages Module (pages, sections, content resources, OpenGraph)
        stepStopwatch.Restart();
        services.AddContentPagesModule();
        logger.LogInformation("Content Pages Module registered in {ElapsedMs}ms", stepStopwatch.ElapsedMilliseconds);

        // 10h. Notifications Module (in-app delivery, templates, preferences)
        stepStopwatch.Restart();
        services.AddNotificationsModule();
        logger.LogInformation("Notifications Module registered in {ElapsedMs}ms", stepStopwatch.ElapsedMilliseconds);

        // 10i. Localization Module (request context, language data, sanitization, and translation services)
        stepStopwatch.Restart();
        services.AddLocalizationServices();
        logger.LogInformation("Localization Module registered in {ElapsedMs}ms", stepStopwatch.ElapsedMilliseconds);

        // 10h. Tenants Module - explicit registrations (ITenantResolver doesn't match
        // the I*Service / I*Repository convention used by AddRepositories)
        stepStopwatch.Restart();
        services.AddScoped<ITenantResolver, TenantResolver>();
        logger.LogInformation("Tenants Module registered in {ElapsedMs}ms", stepStopwatch.ElapsedMilliseconds);

        // 11. Repositories
        services.AddRepositories(logger);

        stopwatch.Stop();
        logger.LogInformation("Completed infrastructure layer setup in {ElapsedMs}ms", stopwatch.ElapsedMilliseconds);

        return services;
    }

    #endregion

    #region Private Methods

    /// <summary>
    ///     Registers ApplicationDbContext with PostgreSQL
    /// </summary>
    private static void AddDatabase(this IServiceCollection services, IConfiguration configuration,
        DatabaseOptions? databaseOptions = null)
    {
        databaseOptions ??= DatabaseOptions.CreateDefault();

        var connectionString = PostgresConnectionString.Resolve(configuration, databaseOptions.ConnectionStringName)
                               ?? throw new InvalidOperationException(
                                   $"Connection string '{databaseOptions.ConnectionStringName}' not found. " +
                                   $"Add 'ConnectionStrings:{databaseOptions.ConnectionStringName}' or POSTGRES_* values.");

        services.AddScoped<CostTelemetryContext>();
        services.AddScoped<ICostTelemetryRecorder>(provider => provider.GetRequiredService<CostTelemetryContext>());
        services.AddScoped<CostTelemetryDbCommandInterceptor>();
        services.AddDbContext<ApplicationDbContext>((provider, options) =>
        {
            options.AddInterceptors(provider.GetRequiredService<CostTelemetryDbCommandInterceptor>());
            options.UseNpgsql(connectionString, npgsqlOptions =>
            {
                npgsqlOptions.MigrationsAssembly(typeof(ApplicationDbContext).Assembly.FullName);

                if (databaseOptions.EnableRetryOnFailure)
                {
                    npgsqlOptions.EnableRetryOnFailure(
                        maxRetryCount: databaseOptions.MaxRetryCount,
                        maxRetryDelay: TimeSpan.FromSeconds((double)databaseOptions.MaxRetryDelaySeconds),
                        errorCodesToAdd: null);
                }

                npgsqlOptions.CommandTimeout(databaseOptions.CommandTimeoutSeconds);
            });

            if (databaseOptions.EnableSensitiveDataLogging)
            {
                options.EnableSensitiveDataLogging();
            }

            if (databaseOptions.EnableDetailedErrors)
            {
                options.EnableDetailedErrors();
            }

            // Suppress PendingModelChangesWarning caused by dynamic HasData values
            options.ConfigureWarnings(w =>
                w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning));
        });

        services.AddScoped<IApplicationDbContext>(provider => provider.GetRequiredService<ApplicationDbContext>());

        // Register DbContext as ApplicationDbContext for modules that depend on the abstract DbContext type
        services.AddScoped<DbContext>(provider => provider.GetRequiredService<ApplicationDbContext>());
    }

    /// <summary>
    ///     Registers repository, service, and reader implementations by convention from module assemblies.
    ///     Discovers interfaces matching I{Name}Repository → {Name}Repository and
    ///     I{Name}Service → {Name}Service, and I{Name}Reader → {Name}Reader patterns.
    /// </summary>
    /// <remarks>
    ///     Includes startup validation that warns about concrete types whose names end in
    ///     "Repository" or "Service" but were NOT matched by the convention scanner.
    ///     This prevents silent registration failures when a class/interface is renamed.
    /// </remarks>
    private static void AddRepositories(this IServiceCollection services, ILogger logger)
    {
        var totalStopwatch = Stopwatch.StartNew();

        logger.LogInformation("Starting repository and service discovery...");

        // Only register repositories/services from enabled modules
        var enabledModules = ModuleConfiguration.DefaultEnabledModules;

        // Discover module assemblies - load from disk to ensure all modules are available
        var allAssemblies = DependencyInjection.GetAssembliesByPattern("GameGuild.", loadFromDisk: true)
            .Where(a => !ModuleConfiguration.IsTestAssembly(a.GetName().Name))
            .Where(a => !a.FullName?.Contains("API", StringComparison.OrdinalIgnoreCase) ?? true)
            .ToArray();

        // Filter to only enabled modules
        var assemblies = allAssemblies
            .Where(a => enabledModules.Any(m => a.GetName().Name?.EndsWith(m, StringComparison.OrdinalIgnoreCase) ?? false))
            .ToArray();

        logger.LogInformation("Discovered {TotalCount} module assemblies, {EnabledCount} enabled ({Modules})",
            allAssemblies.Length, assemblies.Length, string.Join(", ", enabledModules));

        // Collect all registrations first
        var repositoryRegistrations = new List<(Type interfaceType, Type implementationType)>();
        var serviceRegistrations = new List<(Type interfaceType, Type implementationType)>();
        var matchedTypes = new HashSet<Type>();

        foreach (var assembly in assemblies)
        {
            var types = GetLoadablePublicTypes(assembly, logger);

            foreach (var implementationType in types)
            {
                // Find interfaces this type implements
                var interfaces = implementationType.GetInterfaces();

                foreach (var interfaceType in interfaces)
                {
                    var interfaceName = interfaceType.Name;

                    // Skip generic interface definitions and system interfaces
                    if (interfaceType.IsGenericType || interfaceName.StartsWith("IDisposable") || interfaceName.StartsWith("IAsyncDisposable"))
                        continue;

                    // Match repository pattern: I{Name}Repository -> {Name}Repository
                    if (interfaceName.StartsWith("I") && interfaceName.EndsWith("Repository"))
                    {
                        var expectedImplName = interfaceName.Substring(1); // Remove 'I' prefix
                        if (implementationType.Name == expectedImplName)
                        {
                            repositoryRegistrations.Add((interfaceType, implementationType));
                            matchedTypes.Add(implementationType);
                        }
                    }
                    // Match service pattern: I{Name}Service -> {Name}Service (but not IApplicationDbContext etc.)
                    else if (interfaceName.StartsWith("I") && interfaceName.EndsWith("Service")
                             && !interfaceName.Contains("DbContext"))
                    {
                        var expectedImplName = interfaceName.Substring(1); // Remove 'I' prefix
                        if (implementationType.Name == expectedImplName)
                        {
                            serviceRegistrations.Add((interfaceType, implementationType));
                            matchedTypes.Add(implementationType);
                        }
                    }
                    else if (interfaceName.StartsWith("I") && interfaceName.EndsWith("Reader"))
                    {
                        var expectedImplName = interfaceName[1..];
                        if (implementationType.Name == expectedImplName)
                        {
                            serviceRegistrations.Add((interfaceType, implementationType));
                            matchedTypes.Add(implementationType);
                        }
                    }
                }
            }

            static bool IsSkippableInterface(Type interfaceType)
            {
                var interfaceName = interfaceType.Name;
                return interfaceType.IsGenericType
                    || interfaceName.StartsWith("IDisposable", StringComparison.Ordinal)
                    || interfaceName.StartsWith("IAsyncDisposable", StringComparison.Ordinal);
            }

            bool HasExplicitInterfaceRegistration(Type implementationType)
            {
                return implementationType
                    .GetInterfaces()
                    .Where(interfaceType => !IsSkippableInterface(interfaceType))
                    .Any(interfaceType => services.Any(sd => sd.ServiceType == interfaceType));
            }

            static bool IsDecoratorLike(Type implementationType)
            {
                var implementedInterfaces = implementationType
                    .GetInterfaces()
                    .Where(interfaceType => !IsSkippableInterface(interfaceType))
                    .ToHashSet();

                if (implementedInterfaces.Count == 0)
                {
                    return false;
                }

                var constructor = implementationType
                    .GetConstructors()
                    .OrderByDescending(currentConstructor => currentConstructor.GetParameters().Length)
                    .FirstOrDefault();

                if (constructor is null)
                {
                    return false;
                }

                return constructor
                    .GetParameters()
                    .Any(parameter => implementedInterfaces.Contains(parameter.ParameterType));
            }

            // ── Validation: warn about unmatched types ───────────────────
            // Log concrete types that look like repositories/services but were not matched.
            // This catches silent failures from naming mismatches (e.g. IFooRepo vs FooRepository).
            var unmatchedTypes = types
                .Where(t => !matchedTypes.Contains(t))
                .Where(t => t.Name.EndsWith("Repository") || t.Name.EndsWith("Service") || t.Name.EndsWith("Reader"))
                .Where(t => !typeof(IHostedService).IsAssignableFrom(t))
                .Where(t => !t.Name.Contains("Decorator") && !t.Name.Contains("Cached") && !t.Name.Contains("Logging") && !t.Name.Contains("Default"))
                .Where(t => t.Namespace?.Contains(".Decorators", StringComparison.Ordinal) != true)
                .Where(t => t.Namespace?.Contains(".Examples", StringComparison.Ordinal) != true)
                .Where(t => !services.Any(sd => sd.ServiceType == t || sd.ImplementationType == t))
                .Where(t => !HasExplicitInterfaceRegistration(t))
                .Where(t => !IsDecoratorLike(t))
                .ToList();

            foreach (var unmatched in unmatchedTypes)
            {
                logger.LogWarning(
                    "Convention scan: {TypeName} in {Assembly} was NOT registered — " +
                    "no matching interface found. " +
                    "If this type should be registered, verify the interface follows the IName naming convention.",
                    unmatched.Name, assembly.GetName().Name);
            }
        }

        // Register and log repositories individually
        logger.LogInformation("Setting up {Count} repositories...", repositoryRegistrations.Count);
        var stepStopwatch = Stopwatch.StartNew();

        foreach (var (interfaceType, implementationType) in repositoryRegistrations)
        {
            stepStopwatch.Restart();

            // Check if the implementation takes IApplicationDbContext
            var constructor = implementationType.GetConstructors().FirstOrDefault();
            if (constructor != null)
            {
                var parameters = constructor.GetParameters();
                if (parameters.Length == 1 && parameters[0].ParameterType == typeof(IApplicationDbContext))
                {
                    // Register with factory that injects ApplicationDbContext
                    services.AddScoped(interfaceType, provider =>
                    {
                        var context = provider.GetRequiredService<ApplicationDbContext>();
                        return Activator.CreateInstance(implementationType, context)!;
                    });
                }
                else
                {
                    services.AddScoped(interfaceType, implementationType);
                }
            }
            else
            {
                services.AddScoped(interfaceType, implementationType);
            }

            logger.LogInformation("Registered {Repository} in {ElapsedMs}ms",
                FormatInterfaceName(interfaceType.Name), stepStopwatch.ElapsedMilliseconds);
        }

        logger.LogInformation("Completed repository setup in {ElapsedMs}ms", totalStopwatch.ElapsedMilliseconds);

        // Register and log services individually
        logger.LogInformation("Setting up {Count} services...", serviceRegistrations.Count);
        var serviceStopwatch = Stopwatch.StartNew();

        foreach (var (interfaceType, implementationType) in serviceRegistrations)
        {
            if (ShouldSkipServiceRegistration(services, interfaceType, logger))
                continue;

            stepStopwatch.Restart();
            services.AddScoped(interfaceType, implementationType);
            logger.LogInformation("Registered {Service} in {ElapsedMs}ms",
                FormatInterfaceName(interfaceType.Name), stepStopwatch.ElapsedMilliseconds);
        }

        services.Replace(ServiceDescriptor.Scoped<ITenantMembershipChecker, TenantMembershipChecker>());

        totalStopwatch.Stop();
        logger.LogInformation("Completed service setup in {ElapsedMs}ms", serviceStopwatch.ElapsedMilliseconds);
        logger.LogInformation(
            "Convention-based DI registration complete: {RepoCount} repositories, {ServiceCount} services in {ElapsedMs}ms",
            repositoryRegistrations.Count, serviceRegistrations.Count, totalStopwatch.ElapsedMilliseconds);
    }

    private static bool ShouldSkipServiceRegistration(
        IServiceCollection services,
        Type interfaceType,
        ILogger logger)
    {
        var existingDescriptors = services
            .Where(descriptor => descriptor.ServiceType == interfaceType)
            .ToArray();
        var onlyFailClosedMembershipFallbacks =
            interfaceType == typeof(ITenantMembershipChecker) &&
            existingDescriptors.Length > 0 &&
            existingDescriptors.All(descriptor =>
                descriptor.ImplementationType == typeof(FailClosedTenantMembershipChecker));

        if (existingDescriptors.Length > 0 && !onlyFailClosedMembershipFallbacks)
        {
            logger.LogInformation("Skipped {Service} — already registered",
                FormatInterfaceName(interfaceType.Name));
            return true;
        }

        if (onlyFailClosedMembershipFallbacks)
            services.RemoveAll(interfaceType);

        return false;
    }

    private static List<Type> GetLoadablePublicTypes(Assembly assembly, ILogger logger)
    {
        try
        {
            return assembly.GetTypes()
                .Where(type => type.IsClass && !type.IsAbstract && type.IsPublic)
                .ToList();
        }
        catch (ReflectionTypeLoadException exception)
        {
            var loadableTypes = exception.Types
                .OfType<Type>()
                .Where(type => type.IsClass && !type.IsAbstract && type.IsPublic)
                .ToList();
            logger.LogWarning(
                exception,
                "Convention scan: {Assembly} had loadable-type failures. Continuing with {LoadableCount} loadable types.",
                assembly.GetName().Name,
                loadableTypes.Count);

            foreach (var loaderException in exception.LoaderExceptions.Where(value => value is not null))
            {
                logger.LogWarning(
                    loaderException,
                    "Convention scan: Loader exception while scanning {Assembly}: {Message}",
                    assembly.GetName().Name,
                    loaderException!.Message);
            }

            return loadableTypes;
        }
    }

    /// <summary>
    ///     Formats an interface name from IUserRepository to "User Repository".
    /// </summary>
    private static string FormatInterfaceName(string interfaceName)
    {
        // Remove the 'I' prefix if present
        var name = interfaceName.StartsWith("I", StringComparison.Ordinal) && interfaceName.Length > 1 && char.IsUpper(interfaceName[1])
            ? interfaceName[1..]
            : interfaceName;

        // Insert space before each uppercase letter (except the first)
        var formatted = Regex.Replace(name, "([a-z])([A-Z])", "$1 $2");
        // Also handle consecutive uppercase letters
        formatted = Regex.Replace(formatted, "([A-Z]+)([A-Z][a-z])", "$1 $2");
        return formatted;
    }

    #endregion
}
