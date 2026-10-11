using System.Security.Cryptography;
using System.Text;

namespace GameGuild.Identity.Authentication;

/// <summary>Versioned full-length password hashing for inputs outside BCrypt's 72-byte input boundary.</summary>
internal static class LongPasswordHash
{
    internal const string Prefix = "pbkdf2-sha256$";
    private const int Iterations = 600_000;
    private const int SaltLength = 16;
    private const int HashLength = 32;

    internal static string Create(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltLength);
        var passwordBytes = Encoding.UTF8.GetBytes(password);
        byte[]? derived = null;
        try
        {
            derived = Rfc2898DeriveBytes.Pbkdf2(passwordBytes, salt, Iterations, HashAlgorithmName.SHA256, HashLength);
            return $"{Prefix}{Iterations}${Convert.ToBase64String(salt)}${Convert.ToBase64String(derived)}";
        }
        finally
        {
            CryptographicOperations.ZeroMemory(passwordBytes);
            if (derived is not null) { CryptographicOperations.ZeroMemory(derived); }
        }
    }

    internal static bool Verify(string storedHash, string password) => VerifyWithWork(storedHash, password).IsValid;

    internal static PasswordVerificationResult VerifyWithWork(string storedHash, string password)
    {
        if (!TryParse(storedHash, out var salt, out var expected)) { return default; }
        var passwordBytes = Encoding.UTF8.GetBytes(password);
        byte[]? actual = null;
        try
        {
            actual = Rfc2898DeriveBytes.Pbkdf2(passwordBytes, salt, Iterations, HashAlgorithmName.SHA256, HashLength);
            return new PasswordVerificationResult(CryptographicOperations.FixedTimeEquals(actual, expected), true);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(passwordBytes);
            if (actual is not null) { CryptographicOperations.ZeroMemory(actual); }
        }
    }

    internal static bool IsValid(string storedHash) => TryParse(storedHash, out _, out _);

    private static bool TryParse(string storedHash, out byte[] salt, out byte[] hash)
    {
        salt = [];
        hash = [];
        var parts = storedHash.Split('$');
        if (parts.Length != 4 || parts[0] != "pbkdf2-sha256" || parts[1] != "600000") { return false; }
        try
        {
            salt = Convert.FromBase64String(parts[2]);
            hash = Convert.FromBase64String(parts[3]);
            return salt.Length == SaltLength && hash.Length == HashLength
                && Convert.ToBase64String(salt) == parts[2] && Convert.ToBase64String(hash) == parts[3];
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
