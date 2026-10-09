using System.Reflection;

namespace GameGuild.API;

/// <summary>
///     Utility methods for dependency injection and assembly discovery.
/// </summary>
public static class DependencyInjection
{
    /// <summary>
    ///     Gets all application assemblies to scan for types with explicit entry assembly.
    /// </summary>
    /// <param name="entryAssembly">The entry assembly (e.g., API assembly)</param>
    /// <param name="additionalAssemblies">Additional assemblies to include</param>
    /// <returns>Array of distinct assemblies</returns>
    public static Assembly[] GetApplicationAssemblies(Assembly entryAssembly, params Assembly[] additionalAssemblies)
    {
        ArgumentNullException.ThrowIfNull(entryAssembly);
        ArgumentNullException.ThrowIfNull(additionalAssemblies);

        var baseAssemblies = new[]
        {
            Assembly.GetExecutingAssembly(), // Core assembly
            entryAssembly // Explicitly provided entry assembly (e.g., API assembly)
        };

        return additionalAssemblies.Length > 0
            ? baseAssemblies.Concat(additionalAssemblies).Distinct().ToArray()
            : baseAssemblies.Distinct().ToArray();
    }

    /// <summary>
    ///     Gets assemblies from the current application domain that match the specified pattern.
    ///     Also loads GameGuild module assemblies from disk if not already loaded.
    /// </summary>
    /// <param name="pattern">The pattern to match assembly names against (e.g., "GameGuild.*")</param>
    /// <param name="loadFromDisk">If true, loads matching assemblies from disk that aren't already loaded</param>
    /// <returns>Array of matching assemblies</returns>
    public static Assembly[] GetAssembliesByPattern(string pattern = "GameGuild.*", bool loadFromDisk = false)
    {
        if (loadFromDisk)
        {
            LoadModuleAssembliesFromDisk(pattern);
        }

        return AppDomain.CurrentDomain.GetAssemblies().Where(assembly =>
                assembly.FullName?.StartsWith(pattern, StringComparison.OrdinalIgnoreCase) == true)
            .OrderBy(assembly => assembly.FullName, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    /// <summary>
    ///     Loads all GameGuild module assemblies from the application's base directory.
    ///     This ensures module assemblies are available for reflection-based discovery.
    /// </summary>
    private static void LoadModuleAssembliesFromDisk(string pattern)
    {
        var baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
        var loadedAssemblyNames = AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => !a.IsDynamic)
            .Select(a => a.GetName().Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        // Convert pattern to a search prefix (e.g., "GameGuild." -> "GameGuild.")
        var searchPattern = pattern.TrimEnd('*', '.') + ".";

        foreach (var dllPath in Directory.GetFiles(baseDirectory, "*.dll"))
        {
            var fileName = Path.GetFileNameWithoutExtension(dllPath);

            // Skip if doesn't match pattern or already loaded
            if (!fileName.StartsWith(searchPattern.TrimEnd('.'), StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (loadedAssemblyNames.Contains(fileName))
            {
                continue;
            }

            try
            {
                var assembly = Assembly.LoadFrom(dllPath);
                loadedAssemblyNames.Add(assembly.GetName().Name!);
            }
            catch
            {
                // Skip assemblies that can't be loaded
            }
        }
    }

    /// <summary>
    ///     Retrieves registration metrics from the last registration operation.
    /// </summary>
    /// <param name="serviceProvider">The service provider to get metrics from</param>
    /// <returns>Registration metrics with handler and validator counts</returns>
    public static RegistrationMetrics GetRegistrationMetrics(IServiceProvider serviceProvider)
    {
        return serviceProvider.GetService<RegistrationMetrics>() ?? new RegistrationMetrics
            { TotalHandlersRegistered = 0, TotalValidatorsRegistered = 0, RegistrationDuration = TimeSpan.Zero };
    }
}