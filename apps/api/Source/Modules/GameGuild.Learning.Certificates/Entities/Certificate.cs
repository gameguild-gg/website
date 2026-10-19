
namespace GameGuild.Learning.Certificates;

/// <summary>
/// Represents a certificate template for a course
/// </summary>
public class CertificateTemplate : EntityBase
{
    public Guid CourseId { get; private set; }
    public new Guid? TenantId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public string TemplateHtml { get; private set; } = string.Empty;
    public string? TemplateStyles { get; private set; }
    public bool IsDefault { get; private set; }
    public bool IsActive { get; private set; }

    private CertificateTemplate() { } // EF Core

    public static CertificateTemplate Create(Guid courseId, string name, string templateHtml, Guid? tenantId = null)
    {
        return new CertificateTemplate
        {
            Id = Guid.NewGuid(),
            CourseId = courseId,
            TenantId = tenantId,
            Name = name,
            TemplateHtml = templateHtml,
            IsDefault = false,
            IsActive = true
        };
    }

    public void Update(string name, string? description, string templateHtml, string? templateStyles, bool isActive)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(templateHtml);

        Name = name.Trim();
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        TemplateHtml = templateHtml.Trim();
        TemplateStyles = string.IsNullOrWhiteSpace(templateStyles) ? null : templateStyles.Trim();
        IsActive = isActive;
        UpdatedAt = SystemClock.UtcNow;
    }

    public void SetDefault(bool isDefault)
    {
        IsDefault = isDefault;
        UpdatedAt = SystemClock.UtcNow;
    }
}

/// <summary>
/// Represents an issued certificate to a student
/// </summary>
public class Certificate : EntityBase
{
    public Guid TemplateId { get; private set; }
    public Guid EnrollmentId { get; private set; }
    public Guid UserId { get; private set; }
    public Guid CourseId { get; private set; }
    public string CertificateNumber { get; private set; } = string.Empty;
    public string RecipientName { get; private set; } = string.Empty;
    public string CourseName { get; private set; } = string.Empty;
    public DateTime IssuedAt { get; private set; }
    public DateTime? ExpiresAt { get; private set; }
    public DateTime? RevokedAt { get; private set; }
    public string? RevocationReason { get; private set; }
    public string? VerificationUrl { get; private set; }
    public string? DigitalSignature { get; private set; }
    public CertificateStatus Status { get; private set; }

    private Certificate() { } // EF Core

    public static Certificate Issue(
        Guid templateId,
        Guid enrollmentId,
        Guid userId,
        Guid courseId,
        string recipientName,
        string courseName,
        DateTime? expiresAt = null,
        Guid? tenantId = null)
    {
        return new Certificate
        {
            Id = Guid.NewGuid(),
            TemplateId = templateId,
            EnrollmentId = enrollmentId,
            UserId = userId,
            TenantId = tenantId,
            CourseId = courseId,
            CertificateNumber = GenerateCertificateNumber(),
            RecipientName = recipientName,
            CourseName = courseName,
            IssuedAt = SystemClock.UtcNow,
            ExpiresAt = expiresAt,
            Status = CertificateStatus.Active
        };
    }

    private static string GenerateCertificateNumber()
    {
        return $"CERT-{SystemClock.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()}";
    }

    public bool IsValid()
    {
        if (Status != CertificateStatus.Active) return false;
        if (ExpiresAt.HasValue && SystemClock.UtcNow > ExpiresAt.Value) return false;
        return true;
    }

    public void Revoke(string reason)
    {
        Status = CertificateStatus.Revoked;
        RevokedAt = SystemClock.UtcNow;
        RevocationReason = reason;
        UpdatedAt = SystemClock.UtcNow;
    }
}

public enum CertificateStatus
{
    Active,
    Expired,
    Revoked
}
