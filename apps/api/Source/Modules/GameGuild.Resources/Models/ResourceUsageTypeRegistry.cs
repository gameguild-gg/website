using System.Collections.Concurrent;
using System.Collections.Frozen;

namespace GameGuild.Resources;

/// <summary>
/// Represents a registered resource usage type with metadata.
/// This allows modules to define their own quotable resource types
/// without modifying the core ResourceUsageType enum.
/// </summary>
public sealed record ResourceUsageTypeInfo
{
    /// <summary>
    /// The unique identifier for this resource type.
    /// For built-in types, this matches the ResourceUsageType enum value.
    /// For custom types, use values >= 1000.
    /// </summary>
    public required int Id { get; init; }

    /// <summary>
    /// The unique string key for this resource type (e.g., "Users", "Projects", "Assets").
    /// Used for serialization and API contracts.
    /// </summary>
    public required string Key { get; init; }

    /// <summary>
    /// Human-readable display name.
    /// </summary>
    public required string DisplayName { get; init; }

    /// <summary>
    /// Description of what this resource type tracks.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// The unit of measurement (e.g., "count", "bytes", "requests").
    /// </summary>
    public string Unit { get; init; } = "count";

    /// <summary>
    /// Whether this resource type supports soft limits.
    /// </summary>
    public bool SupportsSoftLimit { get; init; } = true;

    /// <summary>
    /// Default period for quota reset. Null means no automatic reset.
    /// </summary>
    public ResourceQuotaPeriod? DefaultPeriod { get; init; }

    /// <summary>
    /// Whether this is a built-in type (from ResourceUsageType enum).
    /// </summary>
    public bool IsBuiltIn { get; init; }

    /// <summary>
    /// The module that registered this type (e.g., "GameGuild.Resources", "GameGuild.Assets").
    /// </summary>
    public string? OwnerModule { get; init; }

    /// <summary>
    /// Converts to the enum representation for built-in types.
    /// Throws if this is a custom type.
    /// </summary>
    public ResourceUsageType ToEnum()
    {
        if (!IsBuiltIn)
        {
            throw new InvalidOperationException($"Custom resource type '{Key}' cannot be converted to enum.");
        }

        return (ResourceUsageType)Id;
    }

    /// <summary>
    /// Creates from an enum value.
    /// </summary>
    public static ResourceUsageTypeInfo FromEnum(ResourceUsageType type) 
        => ResourceUsageTypeRegistry.Get(type);
}

/// <summary>
/// Registry for resource usage types.
/// Provides a centralized, extensible way to register quotable resource types.
/// 
/// Built-in types from ResourceUsageType enum are pre-registered.
/// Modules can register custom types at startup using Register().
/// </summary>
/// <example>
/// // In module startup:
/// ResourceUsageTypeRegistry.Register(new ResourceUsageTypeInfo
/// {
///     Id = 1001,
///     Key = "Assets",
///     DisplayName = "Assets",
///     Description = "File assets stored per tenant",
///     Unit = "count",
///     OwnerModule = "GameGuild.Assets"
/// });
/// </example>
public static class ResourceUsageTypeRegistry
{
    private static readonly ConcurrentDictionary<int, ResourceUsageTypeInfo> _typesById = new();
    private static readonly ConcurrentDictionary<string, ResourceUsageTypeInfo> _typesByKey = new(StringComparer.OrdinalIgnoreCase);
    private static FrozenDictionary<int, ResourceUsageTypeInfo>? _frozenById;
    private static FrozenDictionary<string, ResourceUsageTypeInfo>? _frozenByKey;
    private static bool _isSealed;

    /// <summary>
    /// Reserved ID range for custom types (modules should use IDs >= this value).
    /// </summary>
    public const int CustomTypeIdStart = 1000;

    static ResourceUsageTypeRegistry()
    {
        // Register all built-in types from the enum
        RegisterBuiltInTypes();
    }

    /// <summary>
    /// Registers a new resource usage type.
    /// Must be called during application startup, before the registry is sealed.
    /// </summary>
    /// <exception cref="InvalidOperationException">If registry is sealed or duplicate registration.</exception>
    public static void Register(ResourceUsageTypeInfo info)
    {
        ArgumentNullException.ThrowIfNull(info);

        if (_isSealed)
        {
            throw new InvalidOperationException(
                "ResourceUsageTypeRegistry is sealed. Register types during application startup.");
        }

        if (info.Id < CustomTypeIdStart && !info.IsBuiltIn)
        {
            throw new ArgumentException(
                $"Custom resource type IDs must be >= {CustomTypeIdStart}. Got: {info.Id}",
                nameof(info));
        }

        if (!_typesById.TryAdd(info.Id, info))
        {
            throw new InvalidOperationException(
                $"Resource usage type with ID {info.Id} is already registered.");
        }

        if (!_typesByKey.TryAdd(info.Key, info))
        {
            _typesById.TryRemove(info.Id, out _);
            throw new InvalidOperationException(
                $"Resource usage type with key '{info.Key}' is already registered.");
        }
    }

    /// <summary>
    /// Seals the registry, preventing further registrations.
    /// Call this after all modules have registered their types.
    /// </summary>
    public static void Seal()
    {
        if (_isSealed)
        {
            return;
        }

        _frozenById = _typesById.ToFrozenDictionary();
        _frozenByKey = _typesByKey.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);
        _isSealed = true;
    }

    /// <summary>
    /// Gets a resource type by its enum value.
    /// </summary>
    public static ResourceUsageTypeInfo Get(ResourceUsageType type) 
        => GetById((int)type);

    /// <summary>
    /// Gets a resource type by its integer ID.
    /// </summary>
    public static ResourceUsageTypeInfo GetById(int id)
    {
        var dict = _frozenById ?? (IReadOnlyDictionary<int, ResourceUsageTypeInfo>)_typesById;
        
        if (dict.TryGetValue(id, out var info))
        {
            return info;
        }

        throw new KeyNotFoundException($"Resource usage type with ID {id} is not registered.");
    }

    /// <summary>
    /// Gets a resource type by its string key.
    /// </summary>
    public static ResourceUsageTypeInfo GetByKey(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        
        var dict = _frozenByKey ?? (IReadOnlyDictionary<string, ResourceUsageTypeInfo>)_typesByKey;
        
        if (dict.TryGetValue(key, out var info))
        {
            return info;
        }

        throw new KeyNotFoundException($"Resource usage type with key '{key}' is not registered.");
    }

    /// <summary>
    /// Tries to get a resource type by ID.
    /// </summary>
    public static bool TryGetById(int id, out ResourceUsageTypeInfo? info)
    {
        var dict = _frozenById ?? (IReadOnlyDictionary<int, ResourceUsageTypeInfo>)_typesById;
        return dict.TryGetValue(id, out info);
    }

    /// <summary>
    /// Tries to get a resource type by key.
    /// </summary>
    public static bool TryGetByKey(string key, out ResourceUsageTypeInfo? info)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            info = null;
            return false;
        }

        var dict = _frozenByKey ?? (IReadOnlyDictionary<string, ResourceUsageTypeInfo>)_typesByKey;
        return dict.TryGetValue(key, out info);
    }

    /// <summary>
    /// Gets all registered resource usage types.
    /// </summary>
    public static IEnumerable<ResourceUsageTypeInfo> GetAll()
    {
        var dict = _frozenById ?? (IReadOnlyDictionary<int, ResourceUsageTypeInfo>)_typesById;
        return dict.Values;
    }

    /// <summary>
    /// Gets all built-in resource usage types.
    /// </summary>
    public static IEnumerable<ResourceUsageTypeInfo> GetBuiltIn()
        => GetAll().Where(t => t.IsBuiltIn);

    /// <summary>
    /// Gets all custom (module-registered) resource usage types.
    /// </summary>
    public static IEnumerable<ResourceUsageTypeInfo> GetCustom()
        => GetAll().Where(t => !t.IsBuiltIn);

    /// <summary>
    /// Checks if a type is registered.
    /// </summary>
    public static bool IsRegistered(int id) => TryGetById(id, out _);

    /// <summary>
    /// Checks if a type is registered by key.
    /// </summary>
    public static bool IsRegistered(string key) => TryGetByKey(key, out _);

    /// <summary>
    /// Converts an enum value to its string key.
    /// </summary>
    public static string ToKey(ResourceUsageType type) => Get(type).Key;

    /// <summary>
    /// Converts a string key to an enum value (for built-in types only).
    /// </summary>
    public static ResourceUsageType ToEnum(string key) => GetByKey(key).ToEnum();

    private static void RegisterBuiltInTypes()
    {
        // Register all enum values with their metadata
        RegisterBuiltIn(ResourceUsageType.Users, "Users", "User accounts per tenant");
        RegisterBuiltIn(ResourceUsageType.Projects, "Projects", "Projects created per tenant");
        RegisterBuiltIn(ResourceUsageType.Teams, "Teams", "Teams created per tenant");
        RegisterBuiltIn(ResourceUsageType.Storage, "Storage", "Storage usage", "bytes");
        RegisterBuiltIn(ResourceUsageType.ApiCalls, "ApiCalls", "API calls per period", "requests", ResourceQuotaPeriod.Daily);
        RegisterBuiltIn(ResourceUsageType.Programs, "Programs", "Programs (learning paths) per tenant");
        RegisterBuiltIn(ResourceUsageType.Courses, "Courses", "Courses per tenant");
        RegisterBuiltIn(ResourceUsageType.FeatureFlags, "FeatureFlags", "Feature flags per tenant");
        RegisterBuiltIn(ResourceUsageType.SubscriptionPlans, "SubscriptionPlans", "Subscription plans per tenant");
        RegisterBuiltIn(ResourceUsageType.Products, "Products", "Products in catalog per tenant");
        RegisterBuiltIn(ResourceUsageType.TestingSessions, "TestingSessions", "Testing sessions per tenant");
        RegisterBuiltIn(ResourceUsageType.Roles, "Roles", "Roles per tenant");
        RegisterBuiltIn(ResourceUsageType.Tenants, "Tenants", "Tenants created (platform-level)");
        RegisterBuiltIn(ResourceUsageType.Subscriptions, "Subscriptions", "Active subscriptions per tenant");
        RegisterBuiltIn(ResourceUsageType.SLOs, "SLOs", "Service Level Objectives per tenant");
        RegisterBuiltIn(ResourceUsageType.AccessReviewCampaigns, "AccessReviewCampaigns", "Access review campaigns per tenant");
        RegisterBuiltIn(ResourceUsageType.SoDRules, "SoDRules", "Separation of Duties rules per tenant");
        RegisterBuiltIn(ResourceUsageType.AbacPolicies, "AbacPolicies", "ABAC policies per tenant");
        RegisterBuiltIn(ResourceUsageType.ConditionalPolicies, "ConditionalPolicies", "Conditional access policies per tenant");
        RegisterBuiltIn(ResourceUsageType.Wallets, "Wallets", "Crypto wallets per tenant");
        RegisterBuiltIn(ResourceUsageType.Disputes, "Disputes", "Payment disputes per tenant");
        RegisterBuiltIn(ResourceUsageType.PromoCodes, "PromoCodes", "Promotional codes per tenant");
        RegisterBuiltIn(ResourceUsageType.Orders, "Orders", "Orders per tenant (commerce)");
        RegisterBuiltIn(ResourceUsageType.AuditEntries, "AuditEntries", "Audit log entries", "entries", ResourceQuotaPeriod.Daily);
    }

    private static void RegisterBuiltIn(
        ResourceUsageType type,
        string key,
        string displayName,
        string unit = "count",
        ResourceQuotaPeriod? defaultPeriod = null)
    {
        var info = new ResourceUsageTypeInfo
        {
            Id = (int)type,
            Key = key,
            DisplayName = displayName,
            Description = displayName,
            Unit = unit,
            DefaultPeriod = defaultPeriod,
            IsBuiltIn = true,
            OwnerModule = "GameGuild.Resources"
        };

        _typesById[(int)type] = info;
        _typesByKey[key] = info;
    }

    /// <summary>
    /// Resets the registry. FOR TESTING ONLY.
    /// </summary>
    internal static void Reset()
    {
        _isSealed = false;
        _frozenById = null;
        _frozenByKey = null;
        _typesById.Clear();
        _typesByKey.Clear();
        RegisterBuiltInTypes();
    }
}
