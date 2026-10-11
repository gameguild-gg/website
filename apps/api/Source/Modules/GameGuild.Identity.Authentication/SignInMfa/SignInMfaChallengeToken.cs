using System.Security.Cryptography;
using System.Text;

namespace GameGuild.Identity.Authentication;

/// <summary>256-bit opaque challenge bearers with canonical encoding and one-way persistence.</summary>
public static class SignInMfaChallengeToken
{
    public static string Create() => Encode(RandomNumberGenerator.GetBytes(32));

    public static bool TryHash(string? token, out string digest)
    {
        digest = string.Empty;
        if (token is null || token.Length != 43 || token.Any(character =>
                !(char.IsAsciiLetterOrDigit(character) || character is '-' or '_')))
        {
            return false;
        }
        try
        {
            var bytes = Convert.FromBase64String(token.Replace('-', '+').Replace('_', '/') + "=");
            if (bytes.Length != 32 || Encode(bytes) != token) { return false; }
            digest = Convert.ToHexString(SHA256.HashData(Encoding.ASCII.GetBytes(token))).ToLowerInvariant();
            return true;
        }
        catch (FormatException) { return false; }
    }

    internal static bool IsDigest(string? value) => value is { Length: 64 } &&
        value.All(character => char.IsAsciiDigit(character) || character is >= 'a' and <= 'f');

    private static string Encode(byte[] bytes) => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
