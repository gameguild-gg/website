using System.Text.Json;
using System.Text.Json.Serialization;

namespace GameGuild.Identity.Authentication;

/// <summary>
///     JSON converter for polymorphic credential deserialization
/// </summary>
public class PolymorphicCredentialConverter : JsonConverter<ICredentialData>
{
    public override ICredentialData? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using var doc = JsonDocument.ParseValue(ref reader);
        var root = doc.RootElement;

        // Determine credential type from JSON structure
        if (root.TryGetProperty("type", out var typeElement))
        {
            var credentialType = typeElement.GetString()?.ToLowerInvariant();

            return credentialType switch
            {
                "email" => JsonSerializer.Deserialize<EmailCredentialData>(root.GetRawText(), options),
                "phone" => JsonSerializer.Deserialize<PhoneCredentialData>(root.GetRawText(), options),
                "username" => JsonSerializer.Deserialize<UsernameCredentialData>(root.GetRawText(), options),
                "oauth" => JsonSerializer.Deserialize<OAuthCredentialData>(root.GetRawText(), options),
                "web3" => JsonSerializer.Deserialize<Web3CredentialData>(root.GetRawText(), options),
                _ => throw new JsonException($"Unknown credential type: {credentialType}")
            };
        }

        // Attempt auto-detection based on properties
        if (root.TryGetProperty("email", out _))
        {
            return JsonSerializer.Deserialize<EmailCredentialData>(root.GetRawText(), options);
        }

        if (root.TryGetProperty("phoneNumber", out _))
        {
            return JsonSerializer.Deserialize<PhoneCredentialData>(root.GetRawText(), options);
        }

        if (root.TryGetProperty("username", out _))
        {
            return JsonSerializer.Deserialize<UsernameCredentialData>(root.GetRawText(), options);
        }

        if (root.TryGetProperty("provider", out _))
        {
            return JsonSerializer.Deserialize<OAuthCredentialData>(root.GetRawText(), options);
        }

        if (root.TryGetProperty("walletAddress", out _))
        {
            return JsonSerializer.Deserialize<Web3CredentialData>(root.GetRawText(), options);
        }

        throw new JsonException("Unable to determine credential type from JSON structure");
    }

    public override void Write(Utf8JsonWriter writer, ICredentialData value, JsonSerializerOptions options) { JsonSerializer.Serialize(writer, value, value.GetType(), options); }
}
