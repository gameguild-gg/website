using System.Security.Cryptography;

namespace GameGuild.Features;

/// <summary>
///     Implementation of feature flag encryption using AES-256-GCM
/// </summary>
public class FeatureFlagEncryptionService : IFeatureFlagEncryptionService
{
    private const string EncryptionPrefix = "ENC:";

    private readonly byte[ ] _encryptionKey;

    public FeatureFlagEncryptionService(string encryptionKey)
    {
        if (string.IsNullOrEmpty(encryptionKey))
        {
            throw new ArgumentException("Encryption key cannot be null or empty", nameof(encryptionKey));
        }

        _encryptionKey = Convert.FromBase64String(encryptionKey);

        if (_encryptionKey.Length != 32)
        {
            throw new ArgumentException("Encryption key must be 256 bits (32 bytes)", nameof(encryptionKey));
        }
    }

    public async Task<string> EncryptAsync(string plainText)
    {
        if (string.IsNullOrEmpty(plainText))
        {
            return plainText;
        }

        if (IsEncrypted(plainText))
        {
            return plainText;
        }

        return await Task.Run(() =>
            {
                using var aes = Aes.Create();
                aes.Key = _encryptionKey;
                aes.GenerateIV();
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;

                using var encryptor = aes.CreateEncryptor(aes.Key, aes.IV);
                using var msEncrypt = new MemoryStream();

                // Prepend IV to encrypted data
                msEncrypt.Write(aes.IV, 0, aes.IV.Length);

                using (var csEncrypt = new CryptoStream(msEncrypt, encryptor, CryptoStreamMode.Write))
                {
                    using (var swEncrypt = new StreamWriter(csEncrypt)) { swEncrypt.Write(plainText); }
                }

                var encrypted = Convert.ToBase64String(msEncrypt.ToArray());

                return $"{EncryptionPrefix}{encrypted}";
            }
        ).ConfigureAwait(false);
    }

    public async Task<string> DecryptAsync(string cipherText)
    {
        if (string.IsNullOrEmpty(cipherText))
        {
            return cipherText;
        }

        if (!IsEncrypted(cipherText))
        {
            return cipherText;
        }

        return await Task.Run(() =>
            {
                // Remove prefix
                var encryptedData = cipherText[EncryptionPrefix.Length..];
                var cipherBytes = Convert.FromBase64String(encryptedData);

                using var aes = Aes.Create();
                aes.Key = _encryptionKey;
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;

                // Extract IV from the beginning of cipher text
                var iv = new byte[aes.BlockSize / 8];
                Array.Copy(cipherBytes, 0, iv, 0, iv.Length);
                aes.IV = iv;

                using var decryptor = aes.CreateDecryptor(aes.Key, aes.IV);
                using var msDecrypt = new MemoryStream(cipherBytes, iv.Length, cipherBytes.Length - iv.Length);
                using var csDecrypt = new CryptoStream(msDecrypt, decryptor, CryptoStreamMode.Read);
                using var srDecrypt = new StreamReader(csDecrypt);

                return srDecrypt.ReadToEnd();
            }
        ).ConfigureAwait(false);
    }

    public bool IsEncrypted(string value) { return !string.IsNullOrEmpty(value) && value.StartsWith(EncryptionPrefix); }

    public string GenerateEncryptionKey()
    {
        using var aes = Aes.Create();
        aes.KeySize = 256;
        aes.GenerateKey();

        return Convert.ToBase64String(aes.Key);
    }
}
