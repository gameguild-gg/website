using System.Collections.Concurrent;
using System.Reflection;
using GameGuild.CQRS.Models;

namespace GameGuild;

/// <summary>
///     Utility class for mapping property values onto entities via reflection.
///     Extracted from <see cref="EntityBase{TKey}"/> to uphold the Single Responsibility Principle.
/// </summary>
/// <remarks>
///     <para>
///     This class encapsulates all reflection-based property mapping logic that was previously
///     embedded in the entity base class. It handles:
///     </para>
///     <list type="bullet">
///         <item><description>Dictionary → entity property mapping with type coercion</description></item>
///         <item><description>Anonymous/POCO object → dictionary conversion</description></item>
///         <item><description>Nullable property detection</description></item>
///         <item><description>Common domain type conversions (Guid, TenantId)</description></item>
///     </list>
/// </remarks>
internal static class EntityPropertyMapper
{
    private static readonly ConcurrentDictionary<Type, PropertyInfo[]> s_propertyCache = new();
    /// <summary>
    ///     Sets multiple properties on a target object from a dictionary, with type coercion.
    /// </summary>
    /// <param name="target">The object whose properties will be set</param>
    /// <param name="properties">Dictionary of property names and values</param>
    /// <param name="onPropertySet">
    ///     Callback invoked after each successful property set.
    ///     The string parameter is the property name that was set.
    ///     Used by <see cref="EntityBase{TKey}"/> to update the <c>UpdatedAt</c> timestamp.
    /// </param>
    /// <exception cref="InvalidOperationException">Thrown when a property value cannot be converted to the target type</exception>
    public static void SetProperties(
        object target,
        Dictionary<string, object?> properties,
        Action<string>? onPropertySet = null)
    {
        var entityType = target.GetType();
        var cachedProperties = s_propertyCache.GetOrAdd(entityType, t => t.GetProperties(BindingFlags.Public | BindingFlags.Instance));

        foreach (var property in properties)
        {
            var propertyInfo = Array.Find(cachedProperties, p => p.Name == property.Key);

            if (propertyInfo == null || !propertyInfo.CanWrite)
            {
                continue;
            }

            var value = property.Value;

            if (value is null)
            {
                if (!IsNullableProperty(propertyInfo))
                {
                    throw new InvalidOperationException(
                        $"Cannot set non-nullable property '{property.Key}' on {entityType.Name} to null.");
                }

                propertyInfo.SetValue(target, null, null);
                onPropertySet?.Invoke(property.Key);
                continue;
            }

            var targetType = Nullable.GetUnderlyingType(propertyInfo.PropertyType) ?? propertyInfo.PropertyType;

            try
            {
                value = ConvertToTargetType(value, targetType);
            }
            catch (Exception ex) when (ex is InvalidCastException or FormatException or OverflowException or ArgumentException)
            {
                throw new InvalidOperationException(
                    $"Failed to convert value for property '{property.Key}' on {entityType.Name}. " +
                    $"Expected type '{targetType.Name}', got '{value.GetType().Name}' with value '{value}'.",
                    ex);
            }

            propertyInfo.SetValue(target, value);
            onPropertySet?.Invoke(property.Key);
        }
    }

    /// <summary>
    ///     Converts an object (anonymous type, POCO, or dictionary) into a property dictionary.
    /// </summary>
    /// <param name="source">The source object to convert</param>
    /// <returns>Dictionary of property names to values</returns>
    public static Dictionary<string, object?> ToDictionary(object source)
    {
        if (source is Dictionary<string, object?> existing)
        {
            return existing;
        }

        var properties = s_propertyCache.GetOrAdd(source.GetType(), t => t.GetProperties());
        var map = new Dictionary<string, object?>(properties.Length, StringComparer.Ordinal);
        foreach (var property in properties)
        {
            map[property.Name] = property.GetValue(source);
        }

        return map;
    }

    /// <summary>
    ///     Gets a dictionary representation of an object's readable properties.
    /// </summary>
    /// <param name="target">The object to read</param>
    /// <returns>Dictionary with property names and values</returns>
    public static Dictionary<string, object?> GetProperties(object target)
    {
        var result = new Dictionary<string, object?>();
        var properties = s_propertyCache.GetOrAdd(target.GetType(), t => t.GetProperties());

        foreach (var property in properties)
        {
            if (property.CanRead)
            {
                result[property.Name] = property.GetValue(target);
            }
        }

        return result;
    }

    /// <summary>
    ///     Converts a value to the specified target type, handling common domain types.
    /// </summary>
    /// <param name="value">The value to convert</param>
    /// <param name="targetType">The target type to convert to</param>
    /// <returns>The converted value</returns>
    internal static object ConvertToTargetType(object value, Type targetType)
    {
        // Guid conversion from string
        if (targetType == typeof(Guid) && value is string guidString)
        {
            if (!Guid.TryParse(guidString, out var guid))
            {
                throw new FormatException($"'{guidString}' is not a valid GUID.");
            }

            return guid;
        }

        // TenantId conversion
        if (targetType == typeof(TenantId) || targetType == typeof(TenantId?))
        {
            return value switch
            {
                string tenantIdString when Guid.TryParse(tenantIdString, out var parsedGuid) => new TenantId(parsedGuid),
                Guid tenantIdGuid => new TenantId(tenantIdGuid),
                TenantId tid => tid,
                _ => throw new InvalidCastException($"Cannot convert '{value.GetType().Name}' to TenantId.")
            };
        }

        // Same type — no conversion needed
        if (value.GetType() == targetType || targetType.IsAssignableFrom(value.GetType()))
        {
            return value;
        }

        return Convert.ChangeType(value, targetType);
    }

    /// <summary>
    ///     Checks whether a property type is nullable (reference type or Nullable&lt;T&gt;).
    /// </summary>
    /// <param name="propertyInfo">The property to check</param>
    /// <returns>True if the property can hold null</returns>
    internal static bool IsNullableProperty(PropertyInfo propertyInfo)
    {
        if (!propertyInfo.PropertyType.IsValueType)
        {
            return true; // Reference types are nullable
        }

        return Nullable.GetUnderlyingType(propertyInfo.PropertyType) != null;
    }
}
