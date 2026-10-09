using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using GameGuild.CQRS.Models;

namespace GameGuild.Identity.Authorization;

/// <summary>
///     Represents a data masking rule enforced by the authorization layer
/// </summary>
public class DataMaskingRule
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public TenantId? TenantId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public string ResourceType { get; set; } = string.Empty;

    public string FieldName { get; set; } = string.Empty;

    public MaskingType MaskingType { get; set; }

    public string? MaskingPattern { get; set; }

    public int? ShowFirst { get; set; }

    public int? ShowLast { get; set; }

    public char MaskCharacter { get; set; } = '*';

    // Stored as JSON arrays
    public string? ExemptRoles { get; set; }

    public string? RequiredPermissions { get; set; }

    public string? ExemptUsers { get; set; }

    public bool IsEnabled { get; set; } = true;

    public int Priority { get; set; } = 0;

    public DateTime CreatedAt { get; set; } = SystemClock.UtcNow;

    public DateTime? UpdatedAt { get; set; }

    public Guid CreatedBy { get; set; }

    /// <summary>
    ///     Apply masking to a field value
    /// </summary>
    public string ApplyMasking(string value)
    {
        if (!IsEnabled || string.IsNullOrEmpty(value))
        {
            return value;
        }

        return MaskingType switch
        {
            MaskingType.Full => new string(MaskCharacter, value.Length),
            MaskingType.Partial => ApplyPartialMask(value),
            MaskingType.Hash => $"#{Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant()}",
            MaskingType.PatternMask => ApplyPatternMask(value),
            MaskingType.Redact => "[REDACTED]",
            _ => value
        };
    }

    private string ApplyPartialMask(string value)
    {
        var showFirstCount = ShowFirst ?? 0;
        var showLastCount = ShowLast ?? 0;

        if (showFirstCount + showLastCount >= value.Length)
        {
            return value;
        }

        var maskLength = value.Length - showFirstCount - showLastCount;
        var maskedPart = new string(MaskCharacter, maskLength);

        return value.Substring(0, showFirstCount) +
               maskedPart +
               value.Substring(value.Length - showLastCount);
    }

    private string ApplyPatternMask(string value)
    {
        if (string.IsNullOrEmpty(MaskingPattern))
        {
            return new string(MaskCharacter, value.Length);
        }

        return MaskingPattern;
    }

    /// <summary>
    ///     Check if user is exempt from masking
    /// </summary>
    public bool IsUserExempt(Guid userId)
    {
        if (string.IsNullOrEmpty(ExemptUsers))
        {
            return false;
        }

        if (Guid.TryParse(ExemptUsers, out var exemptUserId))
        {
            return exemptUserId == userId;
        }

        try
        {
            return JsonSerializer.Deserialize<Guid[]>(ExemptUsers)?.Contains(userId) == true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    /// <summary>
    ///     Enable the rule
    /// </summary>
    public void Enable()
    {
        IsEnabled = true;
        UpdatedAt = SystemClock.UtcNow;
    }

    /// <summary>
    ///     Disable the rule
    /// </summary>
    public void Disable()
    {
        IsEnabled = false;
        UpdatedAt = SystemClock.UtcNow;
    }
}
