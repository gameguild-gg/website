using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace GameGuild.Learning.Assessments.Grading.Contracts;

public static class CanonicalJson
{
    private static readonly JsonSerializerOptions StringOptions = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public static string Serialize(JsonElement value)
    {
        var output = new StringBuilder();
        Write(value, output);
        return output.ToString();
    }

    public static string Sha256(JsonElement value)
    {
        var bytes = Encoding.UTF8.GetBytes(Serialize(value));
        return Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    }

    public static string HashAuthoringSource(JsonElement canonicalSource) => Sha256(canonicalSource);

    public static string HashExecutionSnapshot(JsonElement canonicalSnapshot) => Sha256(canonicalSnapshot);

    public static string HashExecutionDelivery(JsonElement canonicalDelivery) => Sha256(canonicalDelivery);

    private static void Write(JsonElement value, StringBuilder output)
    {
        switch (value.ValueKind)
        {
            case JsonValueKind.Object:
                output.Append('{');
                var firstProperty = true;
                foreach (var property in value.EnumerateObject().OrderBy(property => property.Name, StringComparer.Ordinal))
                {
                    if (!firstProperty)
                    {
                        output.Append(',');
                    }

                    firstProperty = false;
                    output.Append(JsonSerializer.Serialize(property.Name, StringOptions));
                    output.Append(':');
                    Write(property.Value, output);
                }
                output.Append('}');
                break;
            case JsonValueKind.Array:
                output.Append('[');
                var firstItem = true;
                foreach (var item in value.EnumerateArray())
                {
                    if (!firstItem)
                    {
                        output.Append(',');
                    }

                    firstItem = false;
                    Write(item, output);
                }
                output.Append(']');
                break;
            case JsonValueKind.String:
                output.Append(JsonSerializer.Serialize(value.GetString(), StringOptions));
                break;
            case JsonValueKind.Number:
                output.Append(FormatEcmaScriptNumber(value.GetDouble()));
                break;
            case JsonValueKind.True:
                output.Append("true");
                break;
            case JsonValueKind.False:
                output.Append("false");
                break;
            case JsonValueKind.Null:
                output.Append("null");
                break;
            default:
                throw new JsonException($"JCS cannot serialize {value.ValueKind}.");
        }
    }

    private static string FormatEcmaScriptNumber(double value)
    {
        if (!double.IsFinite(value))
        {
            throw new JsonException("JCS does not support non-finite numbers.");
        }

        if (value == 0d)
        {
            return "0";
        }

        var negative = value < 0d;
        var raw = Math.Abs(value).ToString("R", CultureInfo.InvariantCulture);
        var exponentSeparator = raw.IndexOfAny(['E', 'e']);
        var mantissa = exponentSeparator < 0 ? raw : raw[..exponentSeparator];
        var explicitExponent = exponentSeparator < 0
            ? 0
            : int.Parse(raw[(exponentSeparator + 1)..], NumberStyles.Integer, CultureInfo.InvariantCulture);
        var decimalSeparator = mantissa.IndexOf('.');
        var fractionDigits = decimalSeparator < 0 ? 0 : mantissa.Length - decimalSeparator - 1;
        var digits = mantissa.Replace(".", string.Empty, StringComparison.Ordinal).TrimStart('0');
        if (digits.Length == 0)
        {
            return "0";
        }

        var decimalExponent = explicitExponent - fractionDigits;
        var absolute = Math.Abs(value);
        string formatted;
        if (absolute >= 1e21 || absolute < 1e-6)
        {
            var scientificExponent = decimalExponent + digits.Length - 1;
            var significant = digits.Length == 1 ? digits : $"{digits[0]}.{digits[1..]}";
            var exponentSign = scientificExponent >= 0 ? "+" : string.Empty;
            formatted = $"{significant}e{exponentSign}{scientificExponent}";
        }
        else
        {
            var decimalPosition = digits.Length + decimalExponent;
            formatted = decimalPosition switch
            {
                <= 0 => $"0.{new string('0', -decimalPosition)}{digits}",
                _ when decimalPosition >= digits.Length => digits + new string('0', decimalPosition - digits.Length),
                _ => $"{digits[..decimalPosition]}.{digits[decimalPosition..]}",
            };
        }

        return negative ? $"-{formatted}" : formatted;
    }
}
