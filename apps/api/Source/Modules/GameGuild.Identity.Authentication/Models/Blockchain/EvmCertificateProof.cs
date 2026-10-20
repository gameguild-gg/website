using GameGuild.Configuration.ApplicationLayer;

namespace GameGuild.Identity.Authentication;

public enum EvmCertificateOperationKind
{
    Anchor,
    Revoke
}

public enum EvmCertificateProofStatus
{
    InvalidEvidence,
    Pending,
    Reverted,
    NotFinalized,
    Reorged,
    Revoked,
    Verified
}

public sealed record EvmCertificateBlockReference(ulong Number, string Hash);

public sealed record EvmCertificateNetworkIdentity(
    long ChainId,
    string ContractAddress,
    string IssuerId,
    string RuntimeCodeSha256,
    EvmCertificateFinalityMode FinalityMode,
    int MinimumConfirmations,
    EvmCertificateBlockReference VerificationBlock);

public sealed record EvmCertificateRegistryState(
    string AnchorSigner,
    string RevokeSigner,
    ulong AnchoredAtBlock,
    ulong RevokedAtBlock);

public sealed record EvmCertificateEvidence(
    EvmCertificateOperationKind Operation,
    string CertificateHash,
    string TransactionHash,
    string Signer,
    EvmCertificateBlockReference TransactionBlock,
    EvmCertificateNetworkIdentity Network,
    EvmCertificateRegistryState State);

public sealed record EvmCertificateProofResult(EvmCertificateProofStatus Status, EvmCertificateEvidence? Evidence = null);

/// <summary>Safe identity/protocol failure; never includes untrusted RPC values.</summary>
public sealed class EvmCertificateEvidenceException(EvmCertificateEvidenceFailure failure) : Exception($"EVM certificate evidence rejected ({failure}).")
{
    public EvmCertificateEvidenceFailure Failure { get; } = failure;
}

public enum EvmCertificateEvidenceFailure
{
    NetworkMismatch,
    RuntimeCodeMismatch,
    ProtocolVersionMismatch,
    IssuerMismatch,
    MalformedRpcData,
    InsufficientChainHistory
}
