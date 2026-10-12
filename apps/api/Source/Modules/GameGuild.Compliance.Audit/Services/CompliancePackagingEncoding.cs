using System.Security.Cryptography;
using System.Text.Json;

namespace GameGuild.Compliance.Audit;

internal static class CompliancePackagingEncoding
{
    internal static JsonSerializerOptions JsonOptions { get; } = new(JsonSerializerDefaults.Web) { MaxDepth = 32 };
    internal static byte[] Serialize<T>(T value) => JsonSerializer.SerializeToUtf8Bytes(value, JsonOptions);
    internal static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

    // Duplicate JSON properties make signed evidence ambiguous across parsers. Reject them at every boundary.
    internal static JsonDocument Parse(byte[] content)
    {
        var document = JsonDocument.Parse(content, new JsonDocumentOptions { MaxDepth = 32 });
        try { RejectDuplicateProperties(document.RootElement); return document; }
        catch { document.Dispose(); throw; }
    }

    private static void RejectDuplicateProperties(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in element.EnumerateObject())
            {
                if (!names.Add(property.Name)) { throw new JsonException("Duplicate JSON properties are not allowed."); }
                RejectDuplicateProperties(property.Value);
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray()) { RejectDuplicateProperties(item); }
        }
    }
}
