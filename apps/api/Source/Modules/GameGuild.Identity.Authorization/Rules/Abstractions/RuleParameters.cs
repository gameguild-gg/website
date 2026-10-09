using System.Text.Json;

namespace GameGuild.Identity.Authorization;

/// <summary>
///     Parameters for a rule, stored as JSON in the database.
///     Provides type-safe access to common parameter patterns.
/// </summary>
public sealed class RuleParameters
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly Dictionary<string, JsonElement> _values;

    /// <summary>
    ///     Creates empty parameters.
    /// </summary>
    public RuleParameters() => _values = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    ///     Creates parameters from a dictionary.
    /// </summary>
    public RuleParameters(Dictionary<string, JsonElement> values) =>
        _values = new Dictionary<string, JsonElement>(values, StringComparer.OrdinalIgnoreCase);

    /// <summary>
    ///     Creates parameters from a JSON string.
    /// </summary>
    public static RuleParameters FromJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new RuleParameters();
        }

        var dict = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json, JsonOptions)
                   ?? new Dictionary<string, JsonElement>();

        return new RuleParameters(dict);
    }

    /// <summary>
    ///     Gets a string parameter.
    /// </summary>
    public string? GetString(string key) =>
        _values.TryGetValue(key, out var element) && element.ValueKind == JsonValueKind.String
            ? element.GetString()
            : null;

    /// <summary>
    ///     Gets a required string parameter.
    /// </summary>
    public string GetRequiredString(string key) =>
        GetString(key) ?? throw new InvalidOperationException($"Required parameter '{key}' is missing");

    /// <summary>
    ///     Gets a string array parameter.
    /// </summary>
    public IReadOnlyList<string> GetStringArray(string key)
    {
        if (!_values.TryGetValue(key, out var element))
        {
            return [];
        }

        if (element.ValueKind == JsonValueKind.Array)
        {
            return element.EnumerateArray()
                .Where(e => e.ValueKind == JsonValueKind.String)
                .Select(e => e.GetString()!)
                .ToList();
        }

        // Single value as array
        if (element.ValueKind == JsonValueKind.String)
        {
            return [element.GetString()!];
        }

        return [];
    }

    /// <summary>
    ///     Gets a boolean parameter.
    /// </summary>
    public bool GetBool(string key, bool defaultValue = false)
    {
        if (!_values.TryGetValue(key, out var element))
        {
            return defaultValue;
        }

        return element.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.String => bool.TryParse(element.GetString(), out var b) && b,
            _ => defaultValue
        };
    }

    /// <summary>
    ///     Gets an integer parameter.
    /// </summary>
    public int GetInt(string key, int defaultValue = 0)
    {
        if (!_values.TryGetValue(key, out var element))
        {
            return defaultValue;
        }

        return element.ValueKind switch
        {
            JsonValueKind.Number => element.GetInt32(),
            JsonValueKind.String => int.TryParse(element.GetString(), out var i) ? i : defaultValue,
            _ => defaultValue
        };
    }

    /// <summary>
    ///     Checks if a parameter exists.
    /// </summary>
    public bool HasParameter(string key) => _values.ContainsKey(key);

    /// <summary>
    ///     Gets the raw JSON element for advanced parsing.
    /// </summary>
    public JsonElement? GetRaw(string key) =>
        _values.TryGetValue(key, out var element) ? element : null;

    /// <summary>
    ///     Creates parameters from a dictionary of JsonElement values.
    /// </summary>
    public static RuleParameters FromDictionary(Dictionary<string, JsonElement>? values)
    {
        if (values is null || values.Count == 0)
        {
            return new RuleParameters();
        }

        return new RuleParameters(values);
    }
}
