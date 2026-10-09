using System.Reflection;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace GameGuild;

/// <summary>
///     Registry for discovering and managing GameGuild modules.
///     Handles module discovery, dependency resolution, and ordered registration.
/// </summary>
public sealed class ModuleRegistry
{
    private readonly List<ModuleDescriptor> _modules = [];
    private readonly HashSet<Type> _registeredModules = [];
    private bool _isSealed;

    /// <summary>
    ///     Gets all registered module descriptors.
    /// </summary>
    public IReadOnlyList<ModuleDescriptor> Modules => _modules.AsReadOnly();

    /// <summary>
    ///     Gets the enabled modules in registration order.
    /// </summary>
    public IEnumerable<ModuleDescriptor> EnabledModules => _modules.Where(m => m.IsEnabled);

    /// <summary>
    ///     Discovers and registers modules from the specified assemblies.
    /// </summary>
    /// <param name="assemblies">Assemblies to scan for modules</param>
    /// <param name="configuration">Configuration for checking module enabled state</param>
    /// <returns>This registry for chaining</returns>
    public ModuleRegistry DiscoverModules(IEnumerable<Assembly> assemblies, IConfiguration configuration)
    {
        ThrowIfSealed();

        foreach (var assembly in assemblies)
        {
            Type[] allTypes;
            try { allTypes = assembly.GetTypes(); }
            catch (ReflectionTypeLoadException ex)
            {
                allTypes = ex.Types.Where(t => t is not null).ToArray()!;
            }
            var moduleTypes = allTypes
                .Where(t => t.IsClass && !t.IsAbstract && typeof(IModule).IsAssignableFrom(t));

            foreach (var moduleType in moduleTypes)
            {
                RegisterModule(moduleType, configuration);
            }
        }

        return this;
    }

    /// <summary>
    ///     Registers a specific module type.
    /// </summary>
    /// <typeparam name="TModule">The module type to register</typeparam>
    /// <param name="configuration">Configuration for checking module enabled state</param>
    /// <returns>This registry for chaining</returns>
    public ModuleRegistry RegisterModule<TModule>(IConfiguration configuration) where TModule : IModule, new()
    {
        return RegisterModule(typeof(TModule), configuration);
    }

    /// <summary>
    ///     Registers a specific module type.
    /// </summary>
    /// <param name="moduleType">The module type to register</param>
    /// <param name="configuration">Configuration for checking module enabled state</param>
    /// <returns>This registry for chaining</returns>
    public ModuleRegistry RegisterModule(Type moduleType, IConfiguration configuration)
    {
        ThrowIfSealed();

        if (_registeredModules.Contains(moduleType))
        {
            return this;
        }

        if (!typeof(IModule).IsAssignableFrom(moduleType))
        {
            throw new ArgumentException($"Type {moduleType.Name} does not implement IModule", nameof(moduleType));
        }

        var module = (IModule)Activator.CreateInstance(moduleType)!;
        var isEnabled = configuration.GetValue<bool?>($"Modules:{module.Name}:Enabled") ?? module.EnabledByDefault;

        var descriptor = new ModuleDescriptor(moduleType, module, isEnabled);
        _modules.Add(descriptor);
        _registeredModules.Add(moduleType);

        return this;
    }

    /// <summary>
    ///     Registers a module instance directly.
    /// </summary>
    /// <param name="module">The module instance</param>
    /// <param name="isEnabled">Whether the module is enabled</param>
    /// <returns>This registry for chaining</returns>
    public ModuleRegistry RegisterModule(IModule module, bool isEnabled = true)
    {
        ThrowIfSealed();

        var moduleType = module.GetType();
        if (_registeredModules.Contains(moduleType))
        {
            return this;
        }

        var descriptor = new ModuleDescriptor(moduleType, module, isEnabled);
        _modules.Add(descriptor);
        _registeredModules.Add(moduleType);

        return this;
    }

    /// <summary>
    ///     Resolves dependencies and sorts modules in registration order.
    /// </summary>
    /// <returns>This registry for chaining</returns>
    public ModuleRegistry ResolveDependencies()
    {
        ThrowIfSealed();

        // Topological sort based on dependencies
        var sorted = new List<ModuleDescriptor>();
        var visited = new HashSet<Type>();
        var visiting = new HashSet<Type>();

        foreach (var descriptor in _modules)
        {
            Visit(descriptor, sorted, visited, visiting);
        }

        _modules.Clear();
        _modules.AddRange(sorted);
        _isSealed = true;

        return this;
    }

    private void Visit(ModuleDescriptor descriptor, List<ModuleDescriptor> sorted, HashSet<Type> visited, HashSet<Type> visiting)
    {
        var moduleType = descriptor.ModuleType;

        if (visited.Contains(moduleType))
        {
            return;
        }

        if (visiting.Contains(moduleType))
        {
            throw new InvalidOperationException($"Circular dependency detected for module {descriptor.Module.Name}");
        }

        visiting.Add(moduleType);

        foreach (var dependencyType in descriptor.Module.Dependencies)
        {
            var dependency = _modules.FirstOrDefault(m => m.ModuleType == dependencyType);
            if (dependency != null)
            {
                Visit(dependency, sorted, visited, visiting);
            }
        }

        visiting.Remove(moduleType);
        visited.Add(moduleType);
        sorted.Add(descriptor);
    }

    /// <summary>
    ///     Configures services for all enabled modules.
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <param name="configuration">The application configuration</param>
    /// <returns>The service collection for chaining</returns>
    public IServiceCollection ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        // Register the registry itself for later use
        services.AddSingleton(this);

        foreach (var descriptor in EnabledModules)
        {
            descriptor.Module.ConfigureServices(services, configuration);
        }

        return services;
    }

    /// <summary>
    ///     Maps endpoints for all enabled modules.
    /// </summary>
    /// <param name="endpoints">The endpoint route builder</param>
    /// <returns>The endpoint route builder for chaining</returns>
    public IEndpointRouteBuilder MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        foreach (var descriptor in EnabledModules)
        {
            descriptor.Module.MapEndpoints(endpoints);
        }

        return endpoints;
    }

    /// <summary>
    ///     Logs the module bootstrap status.
    /// </summary>
    /// <param name="logger">The logger</param>
    public void LogBootstrapStatus(ILogger logger)
    {
        var enabledCount = _modules.Count(m => m.IsEnabled);
        var totalCount = _modules.Count;

        logger.LogInformation("Module Bootstrap Status ({EnabledCount}/{TotalCount} enabled):", enabledCount, totalCount);

        foreach (var descriptor in _modules.OrderBy(m => m.Module.Name))
        {
            var status = descriptor.IsEnabled ? "ON" : "OFF";
            var logLevel = descriptor.IsEnabled ? LogLevel.Information : LogLevel.Debug;
            logger.Log(logLevel, "  [{Status}] {ModuleName}", status, descriptor.Module.Name);
        }
    }

    private void ThrowIfSealed()
    {
        if (_isSealed)
        {
            throw new InvalidOperationException("Cannot modify module registry after dependencies have been resolved");
        }
    }
}

/// <summary>
///     Describes a registered module and its state.
/// </summary>
public sealed class ModuleDescriptor
{
    /// <summary>
    ///     Creates a new module descriptor.
    /// </summary>
    public ModuleDescriptor(Type moduleType, IModule module, bool isEnabled)
    {
        ModuleType = moduleType;
        Module = module;
        IsEnabled = isEnabled;
    }

    /// <summary>
    ///     Gets the module type.
    /// </summary>
    public Type ModuleType { get; }

    /// <summary>
    ///     Gets the module instance.
    /// </summary>
    public IModule Module { get; }

    /// <summary>
    ///     Gets whether the module is enabled.
    /// </summary>
    public bool IsEnabled { get; init; }
}
