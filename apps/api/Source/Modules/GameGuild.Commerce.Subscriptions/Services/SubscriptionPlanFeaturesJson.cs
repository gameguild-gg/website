using System.Text.Json;

namespace GameGuild.Commerce.Subscriptions;

/// <summary>
///     Structural schema validation for the <see cref="SubscriptionPlan.Features" /> JSON
///     payload (issue #416: "Features and limits stored as JSON with schema validation").
/// </summary>
/// <remarks>
///     <para>
///         Contract: the payload must be a single flat JSON object whose leaves are scalars
///         (string, boolean or number) — the shape written by the platform and rendered by
///         the marketing site. Nested objects/arrays and JSON primitives at the root are
///         rejected so the feature matrix in the plan comparison endpoint stays well-formed.
///     </para>
///     <para>
///         Well-known feature keys (reconciled with the issue's example configuration) are
///         type-checked: boolean keys must carry booleans, numeric keys must carry numbers.
///         Unknown keys remain allowed with scalar values — feature flags evolve faster than
///         the schema — but they cannot change shape.
///     </para>
/// </remarks>
public static class SubscriptionPlanFeaturesJson
{
    private const int MaxLength = 2000; // SubscriptionPlan.Features column width
    private const int MaxKeyCount = 50;

    private static readonly IReadOnlyDictionary<string, JsonValueKind> KnownKeys = new Dictionary<string, JsonValueKind>(StringComparer.Ordinal)
    {
        // Boolean feature flags (issue #416 example configuration)
        ["basicFeatures"] = JsonValueKind.True,
        ["advancedFeatures"] = JsonValueKind.False,
        ["customDomain"] = JsonValueKind.True,
        ["apiAccess"] = JsonValueKind.True,
        ["advancedAnalytics"] = JsonValueKind.True,
        ["prioritySupport"] = JsonValueKind.True,

        // Numeric entitlements that ride along in the features payload
        ["maxUsers"] = JsonValueKind.Number,
        ["maxProjects"] = JsonValueKind.Number,
        ["storageGB"] = JsonValueKind.Number,
        ["apiRequestsPerDay"] = JsonValueKind.Number
    };

    /// <summary>
    ///     Validates a features JSON payload. An empty payload is valid (no features).
    /// </summary>
    /// <param name="features">Raw JSON text, or <c>null</c>/whitespace for "no features"</param>
    /// <param name="error">Human-readable first violation, or <c>null</c> when valid</param>
    /// <returns>True when the payload satisfies the schema</returns>
    public static bool IsValid(string? features, out string? error)
    {
        error = null;

        if (string.IsNullOrWhiteSpace(features))
            return true;

        if (features.Length > MaxLength)
        {
            error = $"Features payload cannot exceed {MaxLength} characters.";
            return false;
        }

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(features);
        }
        catch (JsonException)
        {
            error = "Features payload must be valid JSON.";
            return false;
        }

        using (document)
        {
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                error = "Features payload must be a JSON object.";
                return false;
            }

            var propertyCount = 0;
            foreach (var property in root.EnumerateObject())
            {
                propertyCount++;
                if (propertyCount > MaxKeyCount)
                {
                    error = $"Features payload cannot declare more than {MaxKeyCount} keys.";
                    return false;
                }

                if (string.IsNullOrWhiteSpace(property.Name))
                {
                    error = "Features payload keys cannot be empty.";
                    return false;
                }

                var valueKind = property.Value.ValueKind;
                if (valueKind is not (JsonValueKind.String or JsonValueKind.True or JsonValueKind.False or JsonValueKind.Number))
                {
                    error = $"Feature '{property.Name}' must be a scalar value (string, boolean or number); nested objects and arrays are not supported.";
                    return false;
                }

                if (KnownKeys.TryGetValue(property.Name, out var expectedKind))
                {
                    var matches = expectedKind switch
                    {
                        JsonValueKind.True => valueKind is JsonValueKind.True or JsonValueKind.False,
                        _ => valueKind == expectedKind
                    };

                    if (!matches)
                    {
                        var expectedDescription = expectedKind == JsonValueKind.Number ? "a number" : "a boolean";
                        error = $"Feature '{property.Name}' must be {expectedDescription}.";
                        return false;
                    }
                }
            }
        }

        return true;
    }
}
