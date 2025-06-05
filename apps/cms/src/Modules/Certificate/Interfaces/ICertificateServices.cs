using cms.Common.Enums;

namespace cms.Modules.Certificate.Interfaces;

/// <summary>
/// Interface for certificate management services
/// </summary>
public interface ICertificateService
{
    Task<Models.Certificate> CreateCertificateAsync(Models.Certificate certificate);
    Task<Models.Certificate?> GetCertificateByIdAsync(int id);
    Task<IEnumerable<Models.Certificate>> GetCertificatesByProgramAsync(int programId);
    Task<IEnumerable<Models.Certificate>> GetCertificatesByProductAsync(int productId);
    Task<Models.Certificate> UpdateCertificateAsync(Models.Certificate certificate);
    Task<bool> DeleteCertificateAsync(int id);
    Task<bool> CheckEligibilityAsync(int certificateId, int programUserId);
    Task<IEnumerable<Models.Certificate>> GetActiveCertificatesAsync();
}

/// <summary>
/// Interface for user certificate issuance and management services
/// </summary>
public interface IUserCertificateService
{
    Task<Models.UserCertificate> IssueCertificateAsync(int certificateId, int userId, int? programUserId = null);
    Task<Models.UserCertificate?> GetUserCertificateByIdAsync(int id);
    Task<Models.UserCertificate?> GetUserCertificateByCodeAsync(string verificationCode);
    Task<IEnumerable<Models.UserCertificate>> GetUserCertificatesAsync(int userId);
    Task<bool> VerifyCertificateAsync(string verificationCode);
    Task<bool> RevokeCertificateAsync(int userCertificateId, string reason);
    Task<bool> IsValidCertificateAsync(int userCertificateId);
    Task<IEnumerable<Models.UserCertificate>> GetExpiringCertificatesAsync(DateTime thresholdDate);
    Task<string> GenerateVerificationCodeAsync();
}

/// <summary>
/// Interface for blockchain anchoring services
/// </summary>
public interface ICertificateBlockchainService
{
    Task<Models.CertificateBlockchainAnchor> AnchorCertificateAsync(int userCertificateId, string blockchainNetwork);
    Task<bool> VerifyBlockchainAnchorAsync(int anchorId);
    Task<Models.CertificateBlockchainAnchor?> GetAnchorByTransactionHashAsync(string transactionHash);
    Task<IEnumerable<Models.CertificateBlockchainAnchor>> GetAnchorsByCertificateAsync(int userCertificateId);
    Task<bool> UpdateAnchorStatusAsync(int anchorId, string status, string? transactionHash = null);
}
