
namespace GameGuild.Learning.Certificates;

/// <summary>
/// Service interface for certificate issuance and management
/// </summary>
public interface ICertificateService
{
    /// <summary>
    /// Issues a certificate to a user upon course completion
    /// </summary>
    /// <param name="templateId">The certificate template to use</param>
    /// <param name="enrollmentId">The enrollment this certificate is for</param>
    /// <param name="userId">The user receiving the certificate</param>
    /// <param name="courseId">The course the certificate is for</param>
    /// <param name="tenantId">Optional tenant ID for multi-tenant scenarios</param>
    /// <returns>Result containing the issued certificate or error information</returns>
    Task<Result<Certificate>> IssueCertificateAsync(
        Guid templateId,
        Guid enrollmentId,
        Guid userId,
        Guid courseId,
        Guid? tenantId = null);

    /// <summary>
    /// Gets a certificate by its ID
    /// </summary>
    Task<Certificate?> GetCertificateByIdAsync(Guid id);

    /// <summary>
    /// Gets a certificate by its unique certificate number
    /// </summary>
    Task<Certificate?> GetCertificateByNumberAsync(string certificateNumber);

    /// <summary>
    /// Gets all certificates for a user
    /// </summary>
    Task<IEnumerable<Certificate>> GetUserCertificatesAsync(Guid userId, Guid? tenantId = null);

    /// <summary>
    /// Gets all certificates for a course
    /// </summary>
    Task<IEnumerable<Certificate>> GetCourseCertificatesAsync(Guid courseId, Guid? tenantId = null);

    /// <summary>
    /// Verifies if a certificate is valid
    /// </summary>
    /// <param name="certificateNumber">The unique certificate number to verify</param>
    /// <returns>Result containing verification status and certificate details</returns>
    Task<Result<CertificateVerificationResult>> VerifyCertificateAsync(string certificateNumber);

    /// <summary>
    /// Verifies the blockchain anchor of a certificate by querying the anchoring backend for its
    /// on-chain record (recomputed canonical digest, digest match, chain confirmation).
    /// </summary>
    /// <param name="certificateNumber">The unique certificate number whose anchor should be verified.</param>
    /// <returns>
    /// Result containing the on-chain anchor verification (verified / digest mismatch / revoked /
    /// not anchored / unreachable — fail-closed on backend errors).
    /// </returns>
    Task<Result<CertificateAnchorVerification>> VerifyCertificateAnchoringAsync(string certificateNumber);

    /// <summary>
    /// Revokes a certificate
    /// </summary>
    /// <param name="certificateId">The ID of the certificate to revoke</param>
    /// <param name="reason">The reason for revocation</param>
    /// <returns>Result indicating success or failure</returns>
    Task<Result> RevokeCertificateAsync(Guid certificateId, string reason);

    /// <summary>
    /// Gets certificates that are about to expire
    /// </summary>
    /// <param name="thresholdDays">Number of days before expiration</param>
    Task<IEnumerable<Certificate>> GetExpiringCertificatesAsync(int thresholdDays = 30);

    /// <summary>
    /// Checks if a user is eligible for a certificate based on enrollment status
    /// </summary>
    Task<Result<bool>> CheckEligibilityAsync(Guid enrollmentId, Guid templateId);

    /// <summary>
    /// Generates a unique certificate number
    /// </summary>
    Task<string> GenerateCertificateNumberAsync();
}

/// <summary>
/// Result of certificate verification
/// </summary>
public sealed record CertificateVerificationResult(
    bool IsValid,
    string CertificateNumber,
    string? RecipientName,
    string? CourseName,
    DateTime IssuedAt,
    DateTime? ExpiresAt,
    CertificateStatus Status,
    string? Message
);
