using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;

namespace GameGuild.Configuration.ApplicationLayer;

/// <summary>
///     Configuration options for Encryption Service
/// </summary>
public sealed class EncryptionOptions : BaseOptions
{
    public const string SectionName = "Encryption";

    /// <summary>
    ///     Encryption key for symmetric encryption (must be base64 encoded, 256-bit key)
    /// </summary>
    [Required]
    [MinLength(32)]
    public string EncryptionKey { get; set; } = string.Empty;

    /// <summary>
    ///     Algorithm to use for encryption (AES256, AES128, etc.)
    /// </summary>
    [Required]
    public string Algorithm { get; set; } = "AES256";

    /// <summary>
    ///     Cipher mode for encryption
    /// </summary>
    public CipherMode CipherMode { get; set; } = CipherMode.CBC;

    /// <summary>
    ///     Padding mode for encryption
    /// </summary>
    public PaddingMode PaddingMode { get; set; } = PaddingMode.PKCS7;

    /// <summary>
    ///     Whether to enable key rotation
    /// </summary>
    public bool EnableKeyRotation { get; set; } = false;

    /// <summary>
    ///     Key rotation interval in days
    /// </summary>
    [Range(1, 365)]
    public int KeyRotationIntervalDays { get; set; } = 90;

    /// <summary>
    ///     Previous encryption keys for decrypting old data (comma-separated base64 strings)
    /// </summary>
    public string? PreviousKeys { get; set; }

    public bool IsValid
    {
        get => Validate().IsValid;
    }

    public new (bool IsValid, string[] Errors) Validate()
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(EncryptionKey))
        {
            errors.Add("EncryptionKey is required");
        }
        else if (EncryptionKey.Length < 32)
        {
            errors.Add("EncryptionKey must be at least 32 characters (256 bits)");
        }
        else
        {
            // Validate that it's a valid base64 string or hexadecimal
            try
            {
                if (EncryptionKey.Length % 2 == 0 && EncryptionKey.All(c => "0123456789ABCDEFabcdef".Contains(c)))
                {
                    // Hexadecimal format
                    if (EncryptionKey.Length < 64) // 256 bits = 64 hex characters
                    {
                        errors.Add("EncryptionKey in hexadecimal format must be at least 64 characters (256 bits)");
                    }
                }
                else
                {
                    // Try base64
                    _ = Convert.FromBase64String(EncryptionKey);
                }
            }
            catch (FormatException)
            {
                errors.Add("EncryptionKey must be a valid base64 or hexadecimal string");
            }
        }

        if (string.IsNullOrWhiteSpace(Algorithm))
        {
            errors.Add("Algorithm is required");
        }

        if (EnableKeyRotation && (KeyRotationIntervalDays < 1 || KeyRotationIntervalDays > 365))
        {
            errors.Add("KeyRotationIntervalDays must be between 1 and 365");
        }

        return (errors.Count == 0, errors.ToArray());
    }
}