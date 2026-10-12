namespace GameGuild.API.Setup;

/// <summary>
///     Configuration for deterministic module discovery and registration.
/// </summary>
public sealed class ModuleConfiguration
{
    /// <summary>
    ///     Modules that form the shared application platform.
    /// </summary>
    public static readonly string[] CommonEnabledModules =
    [
        "AI",
        "Analytics",
        "Assets",
        "Commerce",
        "Commerce.Billing",
        "Commerce.Orders",
        "Commerce.Payments",
        "Commerce.Products",
        "Commerce.Subscriptions",
        "Compliance.Audit",
        "Compliance.Consent",
        "Compliance.KYC",
        "Content.Pages",
        "Features",
        "Identity.Authentication",
        "Identity.Authorization",
        "Identity.Context",
        "Identity.Provisioning",
        "Identity.Tenants",
        "Identity.Users",
        "Localization",
        "Monitoring.SLA",
        "Notifications",
        "Resources",
        "Resources.Contents",
        "SharedKernel",
        "Tags"
    ];

    public static readonly string[] DefaultEnabledModules =
        [.. CommonEnabledModules, .. ApiProductComposition.Instance.EnabledModules];

    public static readonly string[] DefaultDisabledModules =
        [.. ApiProductComposition.Instance.DisabledModules];

    public string[] EnabledModules { get; set; } = DefaultEnabledModules;

    public string AssemblyPrefix { get; set; } = "GameGuild.";

    public bool ExcludeTestAssemblies { get; set; } = true;

    public static readonly string[] HandlerTypeNames =
        ["ICommandHandler", "IQueryHandler", "IRequestHandler"];

    public bool IsEnabledAssembly(string? assemblyName)
    {
        if (string.IsNullOrWhiteSpace(assemblyName))
        {
            return false;
        }

        return EnabledModules.Any(module =>
            assemblyName.EndsWith(module, StringComparison.OrdinalIgnoreCase) ||
            NormalizeModuleName(assemblyName).EndsWith(NormalizeModuleName(module), StringComparison.OrdinalIgnoreCase));
    }

    public static bool IsTestAssembly(string? assemblyName)
    {
        if (string.IsNullOrWhiteSpace(assemblyName))
        {
            return false;
        }

        return assemblyName
            .Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Any(segment => segment.Equals("Tests", StringComparison.OrdinalIgnoreCase) ||
                            segment.EndsWith("Tests", StringComparison.OrdinalIgnoreCase));
    }

    private static string NormalizeModuleName(string value)
        => value
            .Replace(".", string.Empty, StringComparison.Ordinal)
            .Replace("-", string.Empty, StringComparison.Ordinal)
            .Replace("_", string.Empty, StringComparison.Ordinal);
}
