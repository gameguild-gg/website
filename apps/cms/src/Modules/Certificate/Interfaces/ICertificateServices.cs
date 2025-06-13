namespace GameGuild.Modules.Certificate.Interfaces;

/// <summary>
/// Interface for certificate management services
/// </summary>
public interface ICertificateService
{
    Task<Models.Certificate> CreateCertificateAsync(Models.Certificate certificate);
    Task<Models.Certificate?> GetCertificateByIdAsync(Guid id);
    Task<IEnumerable<Models.Certificate>> GetCertificatesByProgramAsync(Guid programId);
    Task<IEnumerable<Models.Certificate>> GetCertificatesByProductAsync(Guid productId);
    Task<Models.Certificate> UpdateCertificateAsync(Models.Certificate certificate);
    Task<bool> DeleteCertificateAsync(Guid id);
    Task<bool> CheckEligibilityAsync(Guid certificateId, Guid programUserId);
    Task<IEnumerable<Models.Certificate>> GetActiveCertificatesAsync();
}

/// <summary>
/// Interface for user certificate issuance and management services
/// </summary>
public interface IUserCertificateService
{
    Task<Models.UserCertificate> IssueCertificateAsync(Guid certificateId, Guid userId, Guid? programUserId = null);
    Task<Models.UserCertificate?> GetUserCertificateByIdAsync(Guid id);
    Task<Models.UserCertificate?> GetUserCertificateByCodeAsync(string verificationCode);
    Task<IEnumerable<Models.UserCertificate>> GetUserCertificatesAsync(Guid userId);
    Task<bool> VerifyCertificateAsync(string verificationCode);
    Task<bool> RevokeCertificateAsync(Guid userCertificateId, string reason);
    Task<bool> IsValidCertificateAsync(Guid userCertificateId);
    Task<IEnumerable<Models.UserCertificate>> GetExpiringCertificatesAsync(DateTime thresholdDate);
    Task<string> GenerateVerificationCodeAsync();
}

/// <summary>
/// Interface for blockchain anchoring services
/// </summary>
public interface ICertificateBlockchainService
{
    Task<Models.CertificateBlockchainAnchor> AnchorCertificateAsync(Guid userCertificateId, string blockchainNetwork);
    Task<bool> VerifyBlockchainAnchorAsync(Guid anchorId);
    Task<Models.CertificateBlockchainAnchor?> GetAnchorByTransactionHashAsync(string transactionHash);
    Task<IEnumerable<Models.CertificateBlockchainAnchor>> GetAnchorsByCertificateAsync(Guid userCertificateId);
    Task<bool> UpdateAnchorStatusAsync(Guid anchorId, string status, string? transactionHash = null);
}
