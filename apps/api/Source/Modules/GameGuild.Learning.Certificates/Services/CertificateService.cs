using GameGuild.Identity.Users;
using GameGuild.Learning.Abstractions;
using GameGuild.Learning.Courses;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace GameGuild.Learning.Certificates;

/// <summary>
/// Service implementation for certificate issuance and management
/// </summary>
public class CertificateService : ICertificateService, ICertificateIssuanceService
{
    private readonly IApplicationDbContext _context;
    private readonly ILogger<CertificateService> _logger;
    private readonly ICertificateAnchoring _certificateAnchoring;

    public CertificateService(
        IApplicationDbContext context,
        ILogger<CertificateService> logger,
        ICertificateAnchoring? certificateAnchoring = null)
    {
        _context = context;
        _logger = logger;
        _certificateAnchoring = certificateAnchoring ?? NoOpCertificateAnchoring.Instance;
    }

    public async Task<Result<Certificate>> IssueCertificateAsync(
        Guid templateId,
        Guid enrollmentId,
        Guid userId,
        Guid courseId,
        Guid? tenantId = null)
    {
        try
        {
            // Check if certificate already exists for this enrollment
            var existing = await _context.Set<Certificate>()
                .FirstOrDefaultAsync(c => c.EnrollmentId == enrollmentId && c.TemplateId == templateId).ConfigureAwait(false);

            if (existing != null)
            {
                _logger.LogWarning("Certificate already exists for enrollment {EnrollmentId}", enrollmentId);
                return Result.Failure<Certificate>(Error.Conflict("Certificate", "Certificate already issued for this enrollment"));
            }

            // Get template to determine expiration
            var template = await _context.Set<CertificateTemplate>()
                .FirstOrDefaultAsync(t => t.Id == templateId).ConfigureAwait(false);

            if (template == null || !template.IsActive)
            {
                return Result.Failure<Certificate>(Error.NotFound("Template", "Certificate template not found or inactive"));
            }

            // Get recipient name from user
            var user = await _context.Set<User>()
                .FirstOrDefaultAsync(u => u.Id == userId).ConfigureAwait(false);
            var recipientName = user?.Name ?? user?.Username ?? "Unknown Recipient";

            // Get course/program name
            var program = await _context.Set<Program>()
                .FirstOrDefaultAsync(p => p.Id == courseId).ConfigureAwait(false);
            var courseName = program?.Title ?? "Unknown Course";

            _logger.LogDebug(
                "Issuing certificate for user {UserId} ({RecipientName}) for course {CourseId} ({CourseName})",
                userId, recipientName, courseId, courseName);

            var certificate = Certificate.Issue(
                templateId,
                enrollmentId,
                userId,
                courseId,
                recipientName,
                courseName);

            _context.Set<Certificate>().Add(certificate);
            await _context.SaveChangesAsync().ConfigureAwait(false);

            _logger.LogInformation(
                "Certificate issued: {CertificateNumber} to user {UserId} for course {CourseId}",
                certificate.CertificateNumber, userId, courseId);

            await AnchorIssuedCertificateSafelyAsync(certificate).ConfigureAwait(false);

            return Result.Success(certificate);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error issuing certificate for enrollment {EnrollmentId}", enrollmentId);
            return Result.Failure<Certificate>(Error.Failure("IssueCertificate", "Failed to issue certificate"));
        }
    }

    public async Task<Result<Guid>> IssueCertificateForEnrollmentAsync(
        Guid enrollmentId,
        Guid userId,
        Guid programId,
        Guid? tenantId = null)
    {
        var template = await _context.Set<CertificateTemplate>()
            .Where(candidate => candidate.CourseId == programId
                && candidate.IsActive
                && (candidate.TenantId == tenantId || candidate.TenantId == null))
            .OrderByDescending(candidate => candidate.TenantId == tenantId)
            .ThenByDescending(candidate => candidate.IsDefault)
            .ThenBy(candidate => candidate.Name)
            .FirstOrDefaultAsync()
            .ConfigureAwait(false);

        if (template == null)
        {
            return Result.Failure<Guid>(
                Error.NotFound("CertificateTemplate", "No active certificate template found for this enrollment"));
        }

        var result = await IssueCertificateAsync(template.Id, enrollmentId, userId, programId, tenantId)
            .ConfigureAwait(false);

        return result.IsSuccess
            ? Result.Success(result.Value.Id)
            : Result.Failure<Guid>(result.Error);
    }

    public async Task<bool> HasCertificateAsync(Guid enrollmentId)
    {
        return await _context.Set<Certificate>()
            .AnyAsync(certificate => certificate.EnrollmentId == enrollmentId)
            .ConfigureAwait(false);
    }

    public async Task<Certificate?> GetCertificateByIdAsync(Guid id)
    {
        return await _context.Set<Certificate>()
            .FirstOrDefaultAsync(c => c.Id == id).ConfigureAwait(false);
    }

    public async Task<Certificate?> GetCertificateByNumberAsync(string certificateNumber)
    {
        return await _context.Set<Certificate>()
            .FirstOrDefaultAsync(c => c.CertificateNumber == certificateNumber).ConfigureAwait(false);
    }

    public async Task<IEnumerable<Certificate>> GetUserCertificatesAsync(Guid userId, Guid? tenantId = null)
    {
        return await _context.Set<Certificate>()
            .Where(c => c.UserId == userId)
            .OrderByDescending(c => c.IssuedAt)
            .ToListAsync().ConfigureAwait(false);
    }

    public async Task<IEnumerable<Certificate>> GetCourseCertificatesAsync(Guid courseId, Guid? tenantId = null)
    {
        return await _context.Set<Certificate>()
            .Where(c => c.CourseId == courseId)
            .OrderByDescending(c => c.IssuedAt)
            .ToListAsync().ConfigureAwait(false);
    }

    public async Task<Result<CertificateVerificationResult>> VerifyCertificateAsync(string certificateNumber)
    {
        try
        {
            var certificate = await GetCertificateByNumberAsync(certificateNumber).ConfigureAwait(false);

            if (certificate == null)
            {
                return Result.Success(new CertificateVerificationResult(
                    IsValid: false,
                    CertificateNumber: certificateNumber,
                    RecipientName: null,
                    CourseName: null,
                    IssuedAt: DateTime.MinValue,
                    ExpiresAt: null,
                    Status: CertificateStatus.Revoked,
                    Message: "Certificate not found"
                ));
            }

            var isValid = certificate.IsValid();
            var message = isValid ? "Certificate is valid" :
                certificate.Status == CertificateStatus.Revoked ? "Certificate has been revoked" :
                "Certificate has expired";

            return Result.Success(new CertificateVerificationResult(
                IsValid: isValid,
                CertificateNumber: certificate.CertificateNumber,
                RecipientName: certificate.RecipientName,
                CourseName: certificate.CourseName,
                IssuedAt: certificate.IssuedAt,
                ExpiresAt: certificate.ExpiresAt,
                Status: certificate.Status,
                Message: message
            ));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error verifying certificate {CertificateNumber}", certificateNumber);
            return Result.Failure<CertificateVerificationResult>(
                Error.Failure("VerifyCertificate", "Failed to verify certificate"));
        }
    }

    public async Task<Result> RevokeCertificateAsync(Guid certificateId, string reason)
    {
        try
        {
            var certificate = await GetCertificateByIdAsync(certificateId).ConfigureAwait(false);

            if (certificate == null)
            {
                return Result.Failure(Error.NotFound("Certificate", "Certificate not found"));
            }

            certificate.Revoke(reason);
            _context.Set<Certificate>().Update(certificate);
            await _context.SaveChangesAsync().ConfigureAwait(false);

            _logger.LogInformation(
                "Certificate revoked: {CertificateNumber} - Reason: {Reason}",
                certificate.CertificateNumber, reason);

            await RecordRevocationSafelyAsync(certificate, reason).ConfigureAwait(false);

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error revoking certificate {CertificateId}", certificateId);
            return Result.Failure(Error.Failure("RevokeCertificate", "Failed to revoke certificate"));
        }
    }

    public async Task<IEnumerable<Certificate>> GetExpiringCertificatesAsync(int thresholdDays = 30)
    {
        var threshold = SystemClock.UtcNow.AddDays(thresholdDays);

        return await _context.Set<Certificate>()
            .Where(c => c.Status == CertificateStatus.Active
                && c.ExpiresAt != null
                && c.ExpiresAt <= threshold)
            .OrderBy(c => c.ExpiresAt)
            .ToListAsync().ConfigureAwait(false);
    }

    public async Task<Result<bool>> CheckEligibilityAsync(Guid enrollmentId, Guid templateId)
    {
        try
        {
            _logger.LogInformation("Checking certificate eligibility for enrollment {EnrollmentId}", enrollmentId);

            // Get enrollment to check completion status
            var enrollment = await _context.Set<ProgramEnrollment>()
                .FirstOrDefaultAsync(e => e.Id == enrollmentId).ConfigureAwait(false);

            if (enrollment == null)
            {
                _logger.LogWarning("Enrollment {EnrollmentId} not found for eligibility check", enrollmentId);
                return Result.Failure<bool>(Error.NotFound("Enrollment", "Enrollment not found"));
            }

            // Check if enrollment is completed
            if (enrollment.CompletionStatus != CompletionStatus.Completed &&
                enrollment.CompletionStatus != CompletionStatus.CompletedWithCertificate)
            {
                _logger.LogInformation(
                    "Enrollment {EnrollmentId} not eligible for certificate. Status: {Status}",
                    enrollmentId, enrollment.CompletionStatus);
                return Result.Success(false);
            }

            // Check if certificate was already issued
            if (enrollment.CertificateIssued)
            {
                _logger.LogInformation(
                    "Certificate already issued for enrollment {EnrollmentId}",
                    enrollmentId);
                return Result.Success(false);
            }

            // Get template to check if it's active and applicable
            var template = await _context.Set<CertificateTemplate>()
                .FirstOrDefaultAsync(t => t.Id == templateId).ConfigureAwait(false);

            if (template == null || !template.IsActive)
            {
                _logger.LogWarning(
                    "Certificate template {TemplateId} not found or inactive",
                    templateId);
                return Result.Failure<bool>(Error.NotFound("Template", "Certificate template not found or inactive"));
            }

            _logger.LogInformation(
                "Enrollment {EnrollmentId} is eligible for certificate",
                enrollmentId);
            return Result.Success(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking certificate eligibility for enrollment {EnrollmentId}", enrollmentId);
            return Result.Failure<bool>(Error.Failure("CheckEligibility", "Failed to check eligibility"));
        }
    }

    public Task<string> GenerateCertificateNumberAsync()
    {
        var number = $"CERT-{SystemClock.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()}";
        return Task.FromResult(number);
    }

    /// <summary>
    ///     Anchoring is auxiliary trust evidence: failures are logged but never break issuance,
    ///     verification, or revocation of the certificate itself.
    /// </summary>
    private async Task AnchorIssuedCertificateSafelyAsync(Certificate certificate)
    {
        try
        {
            await _certificateAnchoring.AnchorIssuedCertificateAsync(certificate).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Certificate anchoring failed for certificate {CertificateNumber} (non-fatal; certificate remains issued)",
                certificate.CertificateNumber);
        }
    }

    private async Task RecordRevocationSafelyAsync(Certificate certificate, string reason)
    {
        try
        {
            await _certificateAnchoring.RecordRevocationAsync(certificate, reason).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Certificate revocation anchoring failed for certificate {CertificateNumber} (non-fatal; certificate remains revoked)",
                certificate.CertificateNumber);
        }
    }
}
