namespace GameGuild.Identity.Authentication;

/// <summary>Independently reads configured registry identity and canonical operation proofs.</summary>
public interface IEvmCertificateProofVerifier
{
    Task<EvmCertificateNetworkIdentity> ValidateNetworkAsync();
    Task<EvmCertificateNetworkIdentity> ValidateNetworkAsync(CancellationToken cancellationToken);
    Task<EvmCertificateProofResult> VerifyAnchorAsync(string certificateHash, string transactionHash);
    Task<EvmCertificateProofResult> VerifyAnchorAsync(string certificateHash, string transactionHash, CancellationToken cancellationToken);
    Task<EvmCertificateProofResult> VerifyRevocationAsync(string certificateHash, string transactionHash);
    Task<EvmCertificateProofResult> VerifyRevocationAsync(string certificateHash, string transactionHash, CancellationToken cancellationToken);
}
