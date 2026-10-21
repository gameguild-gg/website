using System.Numerics;

namespace GameGuild.Identity.Authentication;

/// <summary>Processes durable intents using independently verified receipts and persist-before-send attempts.</summary>
/// <remarks>Explicit composition only. Never returns a hash as a substitute for finalized proof.</remarks>
public sealed class EvmCertificatePublicationWorker(EvmCertificateJournal journal, IEvmCertificatePublicationRpc rpc,
    IEvmCertificateTransactionSigner signer, IEvmCertificateProofVerifier verifier, EvmCertificatePublicationPolicy policy, TimeProvider clock)
{
    public async Task<int> ProcessDueAsync(int limit, CancellationToken cancellationToken)
    {
        var due = await journal.FindDueAsync(policy.Network.Profile, limit, cancellationToken).ConfigureAwait(false);
        var processed = 0;
        foreach (var id in due)
        {
            if (await ProcessAsync(id, cancellationToken).ConfigureAwait(false) is not null)
            {
                processed++;
            }
        }
        return processed;
    }

    public async Task<EvmCertificateJournalSnapshot?> ProcessAsync(Guid operationId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var candidate = await journal.FindAsync(operationId, cancellationToken).ConfigureAwait(false);
        if (candidate is null || candidate.Network.Profile != policy.Network.Profile)
        {
            return null;
        }
        var lease = await journal.TryAcquireAsync(operationId, policy.LeaseDuration, cancellationToken).ConfigureAwait(false);
        if (lease is null)
        {
            return null;
        }
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(policy.LeaseDuration - TimeSpan.FromSeconds(2));
        try
        {
            return await AdvanceAsync(lease, deadline.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return await RetryAsync(lease, EvmCertificateJournalFailure.RpcUnavailable, cancellationToken).ConfigureAwait(false);
        }
        catch (EvmCertificateRpcException error)
        {
            return error.Failure is EvmCertificateRpcFailure.InvalidResponse or EvmCertificateRpcFailure.ResponseTooLarge
                ? await AttentionAsync(lease, EvmCertificateJournalFailure.InvalidEvidence, cancellationToken).ConfigureAwait(false)
                : await RetryAsync(lease, EvmCertificateJournalFailure.RpcUnavailable, cancellationToken).ConfigureAwait(false);
        }
        catch (EvmCertificateEvidenceException error) when (error.Failure == EvmCertificateEvidenceFailure.InsufficientChainHistory)
        {
            return await RetryAsync(lease, EvmCertificateJournalFailure.RpcUnavailable, cancellationToken).ConfigureAwait(false);
        }
        catch (EvmCertificateEvidenceException)
        {
            return await AttentionAsync(lease, EvmCertificateJournalFailure.InvalidEvidence, cancellationToken).ConfigureAwait(false);
        }
        catch (EvmCertificateJournalException error) when (error.Failure == EvmCertificateJournalFailure.LeaseLost)
        {
            return null;
        }
        catch (EvmCertificateJournalException error) when (error.Failure == EvmCertificateJournalFailure.SignerUnavailable)
        {
            return await AttentionAsync(lease, EvmCertificateJournalFailure.SignerUnavailable, cancellationToken).ConfigureAwait(false);
        }
        catch (EvmCertificateJournalException error) when (error.Failure is EvmCertificateJournalFailure.InvalidTransaction or
            EvmCertificateJournalFailure.InvalidState or EvmCertificateJournalFailure.NonceExhausted)
        {
            return await AttentionAsync(lease, error.Failure, cancellationToken).ConfigureAwait(false);
        }
        catch (EvmCertificateJournalException error) when (error.Failure == EvmCertificateJournalFailure.Conflict)
        {
            return await RetryAsync(lease, error.Failure, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task<EvmCertificateJournalSnapshot> AdvanceAsync(EvmCertificateJournalLease lease, CancellationToken ct)
    {
        var op = await journal.FindAsync(lease.OperationId, ct).ConfigureAwait(false)
            ?? throw new EvmCertificateJournalException(EvmCertificateJournalFailure.LeaseLost);
        if (!policy.Matches(op.Network))
        {
            return await journal.MarkNeedsAttentionAsync(lease, EvmCertificateJournalFailure.InvalidEvidence, ct).ConfigureAwait(false);
        }
        var actual = await verifier.ValidateNetworkAsync(ct).ConfigureAwait(false);
        if (actual.ChainId != op.Network.ChainId || actual.ContractAddress != op.Network.ContractAddress || actual.IssuerId != op.Network.IssuerId ||
            actual.RuntimeCodeSha256 != op.Network.RuntimeCodeSha256 || actual.FinalityMode != op.Network.FinalityMode ||
            actual.MinimumConfirmations != op.Network.MinimumConfirmations)
        {
            return await journal.MarkNeedsAttentionAsync(lease, EvmCertificateJournalFailure.InvalidEvidence, ct).ConfigureAwait(false);
        }

        var attempts = await journal.ListAttemptsAsync(op.Id, ct).ConfigureAwait(false);
        var observation = await ObserveAsync(lease, op, attempts, ct).ConfigureAwait(false);
        if (observation is not null)
        {
            return observation;
        }
        if (attempts.Count == 0 && op.TransactionHash is not null)
        {
            return await journal.MarkNeedsAttentionAsync(lease, EvmCertificateJournalFailure.InvalidEvidence, ct).ConfigureAwait(false);
        }
        return await PublishAsync(lease, op, attempts, ct).ConfigureAwait(false);
    }

    private async Task<EvmCertificateJournalSnapshot> PublishAsync(EvmCertificateJournalLease lease,
        EvmCertificateJournalSnapshot op, IReadOnlyList<EvmCertificateJournalAttempt> attempts, CancellationToken ct)
    {
        EvmCertificateJournalAttempt? attempt = attempts.Count == 0 ? null : attempts[^1];
        if (attempt is not null && clock.GetUtcNow() - attempt.PreparedAt >= policy.ReplacementDelay)
        {
            attempt = await ReplaceAsync(lease, op, attempt, attempts.Count, ct).ConfigureAwait(false);
        }
        else if (attempt?.BroadcastAt is not null)
        {
            return await journal.ScheduleCheckAsync(lease, policy.CheckDelay, ct).ConfigureAwait(false);
        }
        else if (attempt is null)
        {
            (op, attempt) = await PrepareAsync(lease, op, ct).ConfigureAwait(false);
        }

        // An unacknowledged send reuses exactly the durable payload, never another nonce or fee candidate.
        var hash = await rpc.SendRawTransactionAsync(op.PreparedTransaction, attempt.RawTransaction, ct).ConfigureAwait(false);
        if (hash != attempt.TransactionHash)
        {
            return await journal.MarkNeedsAttentionAsync(lease, EvmCertificateJournalFailure.InvalidEvidence, ct).ConfigureAwait(false);
        }
        await journal.RecordBroadcastAsync(lease, hash, ct).ConfigureAwait(false);
        return await journal.ScheduleCheckAsync(lease, policy.CheckDelay, ct).ConfigureAwait(false);
    }

    private async Task<EvmCertificateJournalAttempt> ReplaceAsync(EvmCertificateJournalLease lease,
        EvmCertificateJournalSnapshot op, EvmCertificateJournalAttempt previous, int count, CancellationToken ct)
    {
        if (count >= policy.MaximumAttempts)
        {
            throw new EvmCertificateJournalException(EvmCertificateJournalFailure.InvalidState);
        }
        var fee = Bump(previous.MaximumFeePerGasWei);
        var priority = Bump(previous.PriorityFeePerGasWei);
        if (fee > op.Budget!.MaximumFeePerGasWei || priority > fee)
        {
            throw new EvmCertificateJournalException(EvmCertificateJournalFailure.InvalidState);
        }
        var raw = await signer.SignAsync(op.SignerReference!, op.PreparedTransaction, fee, priority, ct).ConfigureAwait(false);
        return await journal.StoreSignedAttemptAsync(lease, raw, ct).ConfigureAwait(false);
    }

    private async Task<(EvmCertificateJournalSnapshot Operation, EvmCertificateJournalAttempt Attempt)> PrepareAsync(
        EvmCertificateJournalLease lease, EvmCertificateJournalSnapshot op, CancellationToken ct)
    {
        if (!op.Nonce.HasValue)
        {
            var address = await signer.GetAddressAsync(policy.SignerReference, ct).ConfigureAwait(false);
            var pending = await rpc.GetPendingNonceAsync(op.Network, address, ct).ConfigureAwait(false);
            op = await journal.ReserveNonceAsync(lease, policy.SignerReference, address, pending, policy.Budget, ct).ConfigureAwait(false);
        }
        if (policy.InitialFeePerGasWei > op.Budget!.MaximumFeePerGasWei)
        {
            throw new EvmCertificateJournalException(EvmCertificateJournalFailure.InvalidState);
        }
        var raw = await signer.SignAsync(op.SignerReference!, op.PreparedTransaction,
            policy.InitialFeePerGasWei, policy.InitialPriorityFeePerGasWei, ct).ConfigureAwait(false);
        var attempt = await journal.StoreSignedAttemptAsync(lease, raw, ct).ConfigureAwait(false);
        return (op, attempt);
    }

    private async Task<EvmCertificateJournalSnapshot?> ObserveAsync(EvmCertificateJournalLease lease,
        EvmCertificateJournalSnapshot op, IReadOnlyList<EvmCertificateJournalAttempt> attempts, CancellationToken ct)
    {
        (string Hash, EvmCertificateProofResult Proof)? deferred = null;
        var hashes = attempts.Reverse().Select(a => a.TransactionHash).ToList();
        if (op.TransactionHash is not null && !hashes.Contains(op.TransactionHash))
        {
            hashes.Add(op.TransactionHash);
        }
        foreach (var hash in hashes)
        {
            var proof = op.Kind == EvmCertificateOperationKind.Anchor
                ? await verifier.VerifyAnchorAsync(op.CertificateHash, hash, ct).ConfigureAwait(false)
                : await verifier.VerifyRevocationAsync(op.CertificateHash, hash, ct).ConfigureAwait(false);
            if (proof.Status is EvmCertificateProofStatus.Verified or EvmCertificateProofStatus.Revoked)
            {
                return await journal.RecordObservationAsync(lease, hash, proof, policy.CheckDelay, ct).ConfigureAwait(false);
            }
            if (proof.Status is not EvmCertificateProofStatus.Pending &&
                (deferred is null || proof.Status == EvmCertificateProofStatus.NotFinalized))
            {
                deferred = (hash, proof);
            }
        }
        if (deferred.HasValue && (deferred.Value.Proof.Status != EvmCertificateProofStatus.Reorged || op.Status != EvmCertificatePublicationStatus.Reorged))
        {
            return await journal.RecordObservationAsync(lease, deferred.Value.Hash, deferred.Value.Proof, policy.CheckDelay, ct).ConfigureAwait(false);
        }
        if (hashes.Count > 0 && op.Status is EvmCertificatePublicationStatus.Confirmed or EvmCertificatePublicationStatus.Revoked)
        {
            return await journal.RecordObservationAsync(lease, op.TransactionHash!, new(EvmCertificateProofStatus.Pending), policy.CheckDelay, ct).ConfigureAwait(false);
        }
        return null;
    }

    private async Task<EvmCertificateJournalSnapshot?> RetryAsync(EvmCertificateJournalLease lease, EvmCertificateJournalFailure failure, CancellationToken ct)
    {
        try { return await journal.ScheduleRetryAsync(lease, failure, policy.CheckDelay, ct).ConfigureAwait(false); }
        catch (EvmCertificateJournalException error) when (error.Failure == EvmCertificateJournalFailure.LeaseLost) { return null; }
    }

    private async Task<EvmCertificateJournalSnapshot?> AttentionAsync(EvmCertificateJournalLease lease, EvmCertificateJournalFailure failure, CancellationToken ct)
    {
        try { return await journal.MarkNeedsAttentionAsync(lease, failure, ct).ConfigureAwait(false); }
        catch (EvmCertificateJournalException error) when (error.Failure == EvmCertificateJournalFailure.LeaseLost) { return null; }
    }

    private static BigInteger Bump(BigInteger previous) => BigInteger.Max(previous + 1, (previous * 9 + 7) / 8);
}
