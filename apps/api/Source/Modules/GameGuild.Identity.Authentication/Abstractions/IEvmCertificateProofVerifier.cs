namespace GameGuild.Identity.Authentication;

/// <summary>Independently reads configured registry identity and canonical operation proofs.</summary>
public interface IEvmCertificateProofVerifier
{
    Task<EvmCertificateNetworkIdentity> ValidateNetworkAsync(CancellationToken cancellationToken = default);
    Task<EvmCertificateProofResult> VerifyAnchorAsync(string certificateHash, string transactionHash, CancellationToken cancellationToken = default);
    Task<EvmCertificateProofResult> VerifyRevocationAsync(string certificateHash, string transactionHash, CancellationToken cancellationToken = default);
}
