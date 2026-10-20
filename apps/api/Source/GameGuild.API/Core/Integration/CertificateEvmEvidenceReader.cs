using GameGuild.Identity.Authentication;
using GameGuild.Learning.Certificates;

namespace GameGuild.API.Core.Integration;

public sealed class DisabledCertificateEvmEvidenceReader(TimeProvider clock) : ICertificateEvmEvidenceReader
{
    public Task<CertificateEvmEvidenceResponse> ReadAsync(CertificateEvmDocument document, bool freshProof, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(document); cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(CertificateEvmEvidenceProjection.Build(document, clock.GetUtcNow(),
            freshProof, CertificateEvmEvidenceReadStatus.Disabled, null, null, null));
    }
}

/// <summary>Reads original owner-scoped intents. Proof requests never sign, publish, rehash or change the journal.</summary>
public sealed class EvmCertificateEvidenceReader(ICertificateEvmPublicationLookup lookup, IEvmCertificateProofVerifier verifier,
    EvmCertificatePublicationNetwork configuredNetwork, TimeProvider clock) : ICertificateEvmEvidenceReader
{
    public async Task<CertificateEvmEvidenceResponse> ReadAsync(CertificateEvmDocument document, bool freshProof, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(document); cancellationToken.ThrowIfCancellationRequested();
        EvmCertificateJournalSnapshot? anchor = null, revocation = null;
        try
        {
            anchor = await lookup.FindAsync(document.Id, document.OwnerUserId, document.TenantId,
                EvmCertificateOperationKind.Anchor, cancellationToken).ConfigureAwait(false);
            revocation = await lookup.FindAsync(document.Id, document.OwnerUserId, document.TenantId,
                EvmCertificateOperationKind.Revoke, cancellationToken).ConfigureAwait(false);
            if (anchor is not null && !HasScope(anchor, document, EvmCertificateOperationKind.Anchor) ||
                revocation is not null && !HasScope(revocation, document, EvmCertificateOperationKind.Revoke))
            {
                anchor = null; revocation = null;
                return Result(CertificateEvmEvidenceReadStatus.AmbiguousIdentity, null);
            }
            if (anchor is null && revocation is null)
            {
                return Result(CertificateEvmEvidenceReadStatus.NoJournalEntry, null);
            }
            if (anchor is null || revocation is not null &&
                (anchor.CertificateHash != revocation.CertificateHash || !SameNetwork(anchor.Network, revocation.Network)))
            {
                return Result(CertificateEvmEvidenceReadStatus.AmbiguousIdentity, null);
            }
            if (!freshProof)
            {
                return Result(CertificateEvmEvidenceReadStatus.Cached, null);
            }
            var operation = revocation?.TransactionHash is not null ? revocation : anchor;
            if (!SameNetwork(operation.Network, configuredNetwork))
            {
                return Result(CertificateEvmEvidenceReadStatus.ProfileUnavailable, null);
            }
            if (operation.TransactionHash is null)
            {
                return Result(CertificateEvmEvidenceReadStatus.NoRecordedTransaction, null);
            }
            var proof = operation.Kind == EvmCertificateOperationKind.Anchor
                ? await verifier.VerifyAnchorAsync(operation.CertificateHash, operation.TransactionHash, cancellationToken).ConfigureAwait(false)
                : await verifier.VerifyRevocationAsync(operation.CertificateHash, operation.TransactionHash, cancellationToken).ConfigureAwait(false);
            if (proof.Evidence is not null && !IsBound(proof.Evidence, operation) ||
                proof.Evidence is null && proof.Status is EvmCertificateProofStatus.Verified or EvmCertificateProofStatus.Revoked)
            {
                proof = new(EvmCertificateProofStatus.InvalidEvidence);
            }
            return Result(CertificateEvmEvidenceReadStatus.Observed, proof);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw new OperationCanceledException(cancellationToken);
        }
        catch (EvmCertificateJournalException exception) when (exception.Failure == EvmCertificateJournalFailure.Conflict)
        {
            return Result(CertificateEvmEvidenceReadStatus.AmbiguousIdentity, null);
        }
        catch (Exception)
        {
            // Provider/database failures can contain credentials. Preserve availability, never the exception or its message.
            return Result(CertificateEvmEvidenceReadStatus.Unavailable, null);
        }

        CertificateEvmEvidenceResponse Result(CertificateEvmEvidenceReadStatus status, EvmCertificateProofResult? proof) =>
            CertificateEvmEvidenceProjection.Build(document, clock.GetUtcNow(), freshProof, status, anchor, revocation, proof);
    }

    private static bool HasScope(EvmCertificateJournalSnapshot operation, CertificateEvmDocument document, EvmCertificateOperationKind kind) =>
        operation.DocumentId == document.Id && operation.OwnerUserId == document.OwnerUserId && operation.TenantId == document.TenantId &&
        operation.Producer == CertificateEvmEvidenceProducer.ProducerName && operation.Kind == kind;

    private static bool IsBound(EvmCertificateEvidence evidence, EvmCertificateJournalSnapshot operation) =>
        evidence.Operation == operation.Kind && string.Equals(evidence.CertificateHash, operation.CertificateHash, StringComparison.OrdinalIgnoreCase) &&
        string.Equals(evidence.TransactionHash, operation.TransactionHash, StringComparison.OrdinalIgnoreCase) &&
        (operation.SignerAddress is null || string.Equals(evidence.Signer, operation.SignerAddress, StringComparison.OrdinalIgnoreCase)) &&
        string.Equals(evidence.Signer, operation.Kind == EvmCertificateOperationKind.Anchor ? evidence.State.AnchorSigner : evidence.State.RevokeSigner,
            StringComparison.OrdinalIgnoreCase) &&
        evidence.Network.ChainId == operation.Network.ChainId && evidence.Network.ContractAddress == operation.Network.ContractAddress &&
        evidence.Network.IssuerId == operation.Network.IssuerId && evidence.Network.RuntimeCodeSha256 == operation.Network.RuntimeCodeSha256 &&
        evidence.Network.FinalityMode == operation.Network.FinalityMode && evidence.Network.MinimumConfirmations == operation.Network.MinimumConfirmations;

    private static bool SameNetwork(EvmCertificatePublicationNetwork left, EvmCertificatePublicationNetwork right) =>
        left.Profile == right.Profile && left.ChainId == right.ChainId && left.ContractAddress == right.ContractAddress &&
        left.IssuerId == right.IssuerId && left.RuntimeCodeSha256 == right.RuntimeCodeSha256 &&
        left.FinalityMode == right.FinalityMode && left.MinimumConfirmations == right.MinimumConfirmations;
}

internal static class CertificateEvmEvidenceProjection
{
    internal static CertificateEvmEvidenceResponse Build(CertificateEvmDocument document, DateTimeOffset now, bool freshProof,
        CertificateEvmEvidenceReadStatus status, EvmCertificateJournalSnapshot? anchor, EvmCertificateJournalSnapshot? revocation,
        EvmCertificateProofResult? proof)
    {
        var localValid = document.Status == CertificateStatus.Active && (!document.ExpiresAt.HasValue || document.ExpiresAt.Value > now.UtcDateTime);
        var validity = CertificateEvmValidity.Unknown;
        if (!localValid || proof is { Status: EvmCertificateProofStatus.Verified or EvmCertificateProofStatus.Revoked,
            Evidence.State.RevokedAtBlock: > 0 })
        {
            validity = CertificateEvmValidity.Invalid;
        }
        else if (proof is { Status: EvmCertificateProofStatus.Verified, Evidence: { Operation: EvmCertificateOperationKind.Anchor } evidence } &&
            evidence.State.AnchoredAtBlock > 0 && evidence.State.RevokedAtBlock == 0)
        {
            validity = CertificateEvmValidity.Valid;
        }
        return new(document.Id, document.Status, localValid, status != CertificateEvmEvidenceReadStatus.Disabled,
            freshProof, status, validity, View(anchor), View(revocation), proof);
    }

    private static CertificateEvmOperationView? View(EvmCertificateJournalSnapshot? operation) => operation is null ? null :
        new(operation.Id, operation.Network.Profile, operation.Network.ChainId, operation.Network.ContractAddress,
            operation.Network.IssuerId, operation.Network.RuntimeCodeSha256, operation.Network.FinalityMode,
            operation.Network.MinimumConfirmations, operation.CertificateHash, operation.Kind, operation.Status,
            operation.TransactionHash, operation.LastFailure, operation.CreatedAt);
}
