using System.Text.Json;

namespace GameGuild.AI;

internal static class AiJsonHelpers
{
    public static bool TryGetString(JsonElement element, string propertyName, out string? value)
    {
        if (element.ValueKind == JsonValueKind.Object
            && element.TryGetProperty(propertyName, out var property)
            && property.ValueKind == JsonValueKind.String)
        {
            value = property.GetString();
            return true;
        }

        value = null;
        return false;
    }

    public static bool TryGetBoolean(JsonElement element, string propertyName, out bool value)
    {
        if (element.ValueKind == JsonValueKind.Object
            && element.TryGetProperty(propertyName, out var property)
            && (property.ValueKind == JsonValueKind.True || property.ValueKind == JsonValueKind.False))
        {
            value = property.GetBoolean();
            return true;
        }

        value = default;
        return false;
    }

    public static int? TryGetInt(JsonElement element, string propertyName)
    {
        if (element.ValueKind == JsonValueKind.Object
            && element.TryGetProperty(propertyName, out var property)
            && property.TryGetInt32(out var value))
        {
            return value;
        }

        return null;
    }

    public static IReadOnlyList<string> TryGetStringArray(JsonElement element, string propertyName)
    {
        if (element.ValueKind != JsonValueKind.Object
            || !element.TryGetProperty(propertyName, out var property)
            || property.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return property
            .EnumerateArray()
            .Where(static item => item.ValueKind == JsonValueKind.String)
            .Select(static item => item.GetString())
            .Where(static item => !string.IsNullOrWhiteSpace(item))
            .Select(static item => item!.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public static string? ExtractTextFromParts(JsonElement partsElement)
    {
        if (partsElement.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        var parts = partsElement
            .EnumerateArray()
            .Where(static part => part.ValueKind == JsonValueKind.Object && part.TryGetProperty("text", out _))
            .Select(static part => part.GetProperty("text").GetString())
            .Where(static text => !string.IsNullOrWhiteSpace(text));

        return string.Join("\n", parts!);
    }
}