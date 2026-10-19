using System.Text.Json;

namespace GameGuild.Compliance.Audit;

/// <summary>Recognizes canonical state-change snapshots, separately from permission access decisions.</summary>
internal static class PermissionMutationEvidence
{
    internal static int GetChangeCount(object? metadata) =>
        metadata is null ? 0 : GetChangeCount(JsonSerializer.Serialize(metadata));

    internal static int GetChangeCount(string? metadata)
    {
        if (string.IsNullOrWhiteSpace(metadata))
        {
            return 0;
        }

        try
        {
            using var document = JsonDocument.Parse(metadata);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return 0;
            }

            if ((root.TryGetProperty("ChangeCount", out var count) || root.TryGetProperty("changeCount", out count))
                && count.ValueKind == JsonValueKind.Number
                && count.TryGetInt32(out var value) && value > 0)
            {
                return value;
            }

            // Compatibility with previously spooled permission snapshots.
            return (root.TryGetProperty("Changes", out var changes) || root.TryGetProperty("changes", out changes))
                   && changes.ValueKind == JsonValueKind.Array
                ? changes.GetArrayLength()
                : 0;
        }
        catch (JsonException)
        {
            return 0;
        }
    }
}
