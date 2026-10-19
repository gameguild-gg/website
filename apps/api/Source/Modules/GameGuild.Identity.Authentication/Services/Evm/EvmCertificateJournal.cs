using System.Data.Common;
using Microsoft.EntityFrameworkCore;

namespace GameGuild.Identity.Authentication;

/// <summary>Durable intents, fenced worker leases, signer-global nonces and immutable attempt/event history.</summary>
/// <remarks>Every call owns a fresh context. It never signs, broadcasts, activates a provider or invents chain evidence.</remarks>
public sealed class EvmCertificateJournal(IDbContextFactory<EvmCertificateJournalDbContext> contexts, TimeProvider clock)
{
    public Task<EvmCertificateJournalSnapshot> EnqueueAsync(EvmCertificateJournalRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Network);
        EvmCertificatePublicationValues.Key(request.Producer, nameof(request.Producer));
        var digest = EvmCertificateProtocol.NormalizeHashArgument(request.CertificateHash, nameof(request.CertificateHash));
        if (request.DocumentId == Guid.Empty || request.OwnerUserId == Guid.Empty || request.TenantId == Guid.Empty || !Enum.IsDefined(request.Kind))
        {
            throw new ArgumentException("A document, authenticated owner and supported operation are required.", nameof(request));
        }
        return RetryAsync(async db =>
        {
            var rows = db.Set<EvmCertificateJournalOperationRow>();
            var network = request.Network;
            var existing = await rows.AsNoTracking().FirstOrDefaultAsync(o => o.ChainId == network.ChainId &&
                o.ContractAddress == network.ContractAddress && o.Kind == request.Kind &&
                ((o.Producer == request.Producer && o.DocumentId == request.DocumentId) || o.CertificateHash == digest), cancellationToken).ConfigureAwait(false);
            if (existing is not null)
            {
                if (!SameIntent(existing, request, digest))
                {
                    throw Failure(EvmCertificateJournalFailure.Conflict);
                }
                return Snapshot(existing);
            }
            var now = clock.GetUtcNow();
            var op = new EvmCertificateJournalOperationRow
            {
                Id = Guid.NewGuid(), Profile = network.Profile, ChainId = network.ChainId,
                ContractAddress = network.ContractAddress, IssuerId = network.IssuerId,
                RuntimeCodeSha256 = network.RuntimeCodeSha256, FinalityMode = network.FinalityMode,
                MinimumConfirmations = network.MinimumConfirmations, DocumentId = request.DocumentId,
                OwnerUserId = request.OwnerUserId, TenantId = request.TenantId, Producer = request.Producer,
                CertificateHash = digest, Kind = request.Kind, Status = EvmCertificatePublicationStatus.Queued,
                CreatedAt = now, UpdatedAt = now, NextAttemptAt = now, Revision = 1
            };
            rows.Add(op);
            Event(db, op, "enqueued", now, null);
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return Snapshot(op);
        }, cancellationToken);
    }

    public async Task<EvmCertificateJournalSnapshot?> FindAsync(Guid operationId, CancellationToken cancellationToken)
    {
        await using var db = await contexts.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var op = await db.Set<EvmCertificateJournalOperationRow>().AsNoTracking().SingleOrDefaultAsync(o => o.Id == operationId, cancellationToken).ConfigureAwait(false);
        return op is null ? null : Snapshot(op);
    }

    public async Task<IReadOnlyList<Guid>> FindDueAsync(string profile, int limit, CancellationToken cancellationToken)
    {
        EvmCertificatePublicationValues.Key(profile, nameof(profile));
        if (limit is < 1 or > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(limit));
        }
        await using var db = await contexts.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var now = clock.GetUtcNow();
        return await Eligible(db, now).Where(o => o.Profile == profile).OrderBy(o => o.NextAttemptAt).ThenBy(o => o.CreatedAt)
            .Select(o => o.Id).Take(limit).ToListAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<EvmCertificateJournalLease?> TryAcquireAsync(Guid operationId, TimeSpan duration, CancellationToken cancellationToken)
    {
        Delay(duration);
        await using var db = await contexts.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var now = clock.GetUtcNow();
        var token = Guid.NewGuid();
        var count = await Eligible(db, now).Where(o => o.Id == operationId).ExecuteUpdateAsync(setters => setters
            .SetProperty(o => o.LeaseToken, token).SetProperty(o => o.LeaseUntil, now + duration)
            .SetProperty(o => o.UpdatedAt, now).SetProperty(o => o.Revision, o => o.Revision + 1), cancellationToken).ConfigureAwait(false);
        return count == 1 ? new(operationId, token) : null;
    }

    public Task<EvmCertificateJournalSnapshot> ReserveNonceAsync(EvmCertificateJournalLease lease, string signerReference,
        string signerAddress, ulong nodePendingNonce, EvmCertificateFeeBudget budget, CancellationToken cancellationToken)
    {
        EvmCertificatePublicationValues.Key(signerReference, nameof(signerReference));
        var signer = EvmCertificatePublicationValues.Address(signerAddress, nameof(signerAddress));
        ArgumentNullException.ThrowIfNull(budget);
        return RetryAsync(async db =>
        {
            var op = await OwnedAsync(db, lease, cancellationToken).ConfigureAwait(false);
            if (op.Nonce.HasValue)
            {
                if (op.SignerAddress != signer || op.SignerReference != signerReference)
                {
                    throw Failure(EvmCertificateJournalFailure.Conflict);
                }
                return Snapshot(op);
            }
            if (op.Status is not EvmCertificatePublicationStatus.Queued and not EvmCertificatePublicationStatus.Reorged)
            {
                throw Failure(EvmCertificateJournalFailure.InvalidState);
            }
            var accounts = db.Set<EvmCertificateJournalNonceRow>();
            var account = await accounts.SingleOrDefaultAsync(a => a.ChainId == op.ChainId && a.SignerAddress == signer, cancellationToken).ConfigureAwait(false);
            if (account is null)
            {
                account = new() { ChainId = op.ChainId, SignerAddress = signer };
                accounts.Add(account);
            }
            var nonce = Math.Max(checked((ulong)account.NextNonce), nodePendingNonce);
            if (nonce == ulong.MaxValue)
            {
                throw Failure(EvmCertificateJournalFailure.NonceExhausted);
            }
            account.NextNonce = nonce + 1;
            account.Revision++;
            op.Nonce = nonce;
            op.SignerReference = signerReference;
            op.SignerAddress = signer;
            op.GasLimit = budget.GasLimit;
            op.MaximumFeePerGasWei = EvmCertificatePublicationValues.Wei(budget.MaximumFeePerGasWei);
            op.MaximumTransactionFeeWei = EvmCertificatePublicationValues.Wei(budget.MaximumTransactionFeeWei);
            op.Status = EvmCertificatePublicationStatus.NonceReserved;
            Changed(db, op, "nonce-reserved");
            RequireLease(op, lease);
            // One SaveChanges transaction commits both rows or neither; nonce revision is a CAS token.
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return Snapshot(op);
        }, cancellationToken);
    }

    public Task<EvmCertificateJournalAttempt> StoreSignedAttemptAsync(EvmCertificateJournalLease lease, string rawTransaction,
        CancellationToken cancellationToken) => RetryAsync(async db =>
    {
        var op = await OwnedAsync(db, lease, cancellationToken).ConfigureAwait(false);
        var signed = EvmCertificateSignedEnvelope.Validate(rawTransaction, Snapshot(op).PreparedTransaction);
        var attempts = db.Set<EvmCertificateJournalAttemptRow>();
        var existing = await attempts.AsNoTracking().SingleOrDefaultAsync(a => a.TransactionHash == signed.TransactionHash, cancellationToken).ConfigureAwait(false);
        if (existing is not null)
        {
            if (existing.OperationId != op.Id || existing.RawTransaction != signed.RawTransaction)
            {
                throw Failure(EvmCertificateJournalFailure.Conflict);
            }
            return Attempt(existing);
        }
        if (op.Status is not EvmCertificatePublicationStatus.NonceReserved and not EvmCertificatePublicationStatus.Prepared and
            not EvmCertificatePublicationStatus.Submitted and not EvmCertificatePublicationStatus.Reorged)
        {
            throw Failure(EvmCertificateJournalFailure.InvalidState);
        }
        var last = await attempts.Where(a => a.OperationId == op.Id).OrderByDescending(a => a.Sequence).FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
        if (last is not null && (signed.MaximumFeePerGasWei <= EvmCertificatePublicationValues.Wei(last.MaximumFeePerGasWei) ||
            signed.PriorityFeePerGasWei < EvmCertificatePublicationValues.Wei(last.PriorityFeePerGasWei)))
        {
            throw Failure(EvmCertificateJournalFailure.InvalidTransaction);
        }
        var attempt = new EvmCertificateJournalAttemptRow
        {
            Id = Guid.NewGuid(), OperationId = op.Id, Sequence = (last?.Sequence ?? 0) + 1,
            TransactionHash = signed.TransactionHash, RawTransaction = signed.RawTransaction,
            MaximumFeePerGasWei = EvmCertificatePublicationValues.Wei(signed.MaximumFeePerGasWei),
            PriorityFeePerGasWei = EvmCertificatePublicationValues.Wei(signed.PriorityFeePerGasWei), PreparedAt = clock.GetUtcNow()
        };
        attempts.Add(attempt);
        op.TransactionHash = attempt.TransactionHash;
        op.Status = EvmCertificatePublicationStatus.Prepared;
        Changed(db, op, "prepared");
        RequireLease(op, lease);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Attempt(attempt);
    }, cancellationToken);

    public Task<EvmCertificateJournalSnapshot> RecordBroadcastAsync(EvmCertificateJournalLease lease, string transactionHash,
        CancellationToken cancellationToken) => RetryAsync(async db =>
    {
        var op = await OwnedAsync(db, lease, cancellationToken).ConfigureAwait(false);
        var hash = EvmCertificateProtocol.NormalizeHashArgument(transactionHash, nameof(transactionHash));
        if (op.Status is not EvmCertificatePublicationStatus.Prepared and not EvmCertificatePublicationStatus.Submitted and not EvmCertificatePublicationStatus.Reorged)
        {
            throw Failure(EvmCertificateJournalFailure.InvalidState);
        }
        var attempt = await db.Set<EvmCertificateJournalAttemptRow>().SingleOrDefaultAsync(a => a.OperationId == op.Id && a.TransactionHash == hash, cancellationToken).ConfigureAwait(false);
        if (attempt is null)
        {
            throw Failure(EvmCertificateJournalFailure.InvalidState);
        }
        attempt.BroadcastAt ??= clock.GetUtcNow();
        op.Status = EvmCertificatePublicationStatus.Submitted;
        Changed(db, op, "broadcast");
        RequireLease(op, lease);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Snapshot(op);
    }, cancellationToken);

    public Task<EvmCertificateJournalSnapshot> RecordObservationAsync(EvmCertificateJournalLease lease, string transactionHash,
        EvmCertificateProofResult proof, TimeSpan nextCheck, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(proof);
        Delay(nextCheck);
        var hash = EvmCertificateProtocol.NormalizeHashArgument(transactionHash, nameof(transactionHash));
        return RetryAsync(async db =>
        {
            var op = await OwnedAsync(db, lease, cancellationToken).ConfigureAwait(false);
            var knownAttempt = await db.Set<EvmCertificateJournalAttemptRow>().AsNoTracking()
                .AnyAsync(a => a.OperationId == op.Id && a.TransactionHash == hash, cancellationToken).ConfigureAwait(false);
            var verified = proof.Status is EvmCertificateProofStatus.Verified or EvmCertificateProofStatus.Revoked;
            if (verified)
            {
                RequireEvidence(op, hash, proof, knownAttempt);
            }
            else if (!knownAttempt && op.TransactionHash != hash)
            {
                throw Failure(EvmCertificateJournalFailure.InvalidEvidence);
            }
            var wasConfirmed = op.Status is EvmCertificatePublicationStatus.Confirmed or EvmCertificatePublicationStatus.Revoked;
            op.Status = proof.Status switch
            {
                EvmCertificateProofStatus.Verified => EvmCertificatePublicationStatus.Confirmed,
                EvmCertificateProofStatus.Revoked => EvmCertificatePublicationStatus.Revoked,
                EvmCertificateProofStatus.Reverted => EvmCertificatePublicationStatus.Rejected,
                EvmCertificateProofStatus.Reorged => EvmCertificatePublicationStatus.Reorged,
                EvmCertificateProofStatus.Pending or EvmCertificateProofStatus.NotFinalized => wasConfirmed ? EvmCertificatePublicationStatus.Reorged : EvmCertificatePublicationStatus.Submitted,
                _ => EvmCertificatePublicationStatus.NeedsAttention
            };
            op.LastFailure = proof.Status == EvmCertificateProofStatus.InvalidEvidence ? EvmCertificateJournalFailure.InvalidEvidence : null;
            if (verified)
            {
                // A prior replacement may be mined first. Bind completion to its actual verified hash.
                op.TransactionHash = hash;
            }
            op.NextAttemptAt = clock.GetUtcNow() + nextCheck;
            Changed(db, op, "observation", proof);
            RequireLease(op, lease);
            Release(op);
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return Snapshot(op);
        }, cancellationToken);
    }

    public Task<EvmCertificateJournalSnapshot> ScheduleRetryAsync(EvmCertificateJournalLease lease, EvmCertificateJournalFailure failure,
        TimeSpan delay, CancellationToken cancellationToken)
    {
        Delay(delay);
        if (!Enum.IsDefined(failure))
        {
            throw new ArgumentOutOfRangeException(nameof(failure));
        }
        return RetryAsync(async db =>
        {
            var op = await OwnedAsync(db, lease, cancellationToken).ConfigureAwait(false);
            op.LastFailure = failure;
            op.NextAttemptAt = clock.GetUtcNow() + delay;
            Changed(db, op, "retry");
            RequireLease(op, lease);
            Release(op);
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return Snapshot(op);
        }, cancellationToken);
    }

    /// <summary>Release a fenced lease for a normal proof check, without recording a transport failure.</summary>
    public Task<EvmCertificateJournalSnapshot> ScheduleCheckAsync(EvmCertificateJournalLease lease,
        TimeSpan delay, CancellationToken cancellationToken)
    {
        Delay(delay);
        return RetryAsync(async db =>
        {
            var op = await OwnedAsync(db, lease, cancellationToken).ConfigureAwait(false);
            op.LastFailure = null;
            op.NextAttemptAt = clock.GetUtcNow() + delay;
            Changed(db, op, "check-scheduled");
            RequireLease(op, lease);
            Release(op);
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return Snapshot(op);
        }, cancellationToken);
    }

    /// <summary>Quarantine an operation, including failures that occurred before a transaction hash exists.</summary>
    public Task<EvmCertificateJournalSnapshot> MarkNeedsAttentionAsync(EvmCertificateJournalLease lease,
        EvmCertificateJournalFailure failure, CancellationToken cancellationToken)
    {
        if (!Enum.IsDefined(failure)) throw new ArgumentOutOfRangeException(nameof(failure));
        return RetryAsync(async db =>
        {
            var op = await OwnedAsync(db, lease, cancellationToken).ConfigureAwait(false);
            op.LastFailure = failure;
            op.Status = EvmCertificatePublicationStatus.NeedsAttention;
            Changed(db, op, "attention");
            RequireLease(op, lease);
            Release(op);
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return Snapshot(op);
        }, cancellationToken);
    }

    public async Task<IReadOnlyList<EvmCertificateJournalAttempt>> ListAttemptsAsync(Guid operationId, CancellationToken cancellationToken)
    {
        await using var db = await contexts.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var rows = await db.Set<EvmCertificateJournalAttemptRow>().AsNoTracking().Where(a => a.OperationId == operationId)
            .OrderBy(a => a.Sequence).ToListAsync(cancellationToken).ConfigureAwait(false);
        return rows.Select(Attempt).ToArray();
    }

    public async Task<IReadOnlyList<EvmCertificateJournalEvent>> ListEventsAsync(Guid operationId, CancellationToken cancellationToken)
    {
        await using var db = await contexts.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var rows = await db.Set<EvmCertificateJournalEventRow>().AsNoTracking().Where(e => e.OperationId == operationId)
            .OrderBy(e => e.Revision).ToListAsync(cancellationToken).ConfigureAwait(false);
        return rows.Select(e => new EvmCertificateJournalEvent(e.Revision, e.Code, e.Status, e.TransactionHash,
            e.ProofStatus, e.BlockHash, e.BlockNumber.HasValue ? checked((ulong)e.BlockNumber.Value) : null, e.At)).ToArray();
    }

    private IQueryable<EvmCertificateJournalOperationRow> Eligible(EvmCertificateJournalDbContext db, DateTimeOffset now)
    {
        var rows = db.Set<EvmCertificateJournalOperationRow>();
        return rows.Where(o => o.Status != EvmCertificatePublicationStatus.Rejected && o.Status != EvmCertificatePublicationStatus.NeedsAttention &&
            o.NextAttemptAt <= now && (!o.LeaseUntil.HasValue || o.LeaseUntil <= now) &&
            (o.Kind == EvmCertificateOperationKind.Anchor || rows.Any(a => a.ChainId == o.ChainId && a.ContractAddress == o.ContractAddress &&
                a.CertificateHash == o.CertificateHash && a.Kind == EvmCertificateOperationKind.Anchor &&
                (a.Status == EvmCertificatePublicationStatus.Confirmed || a.Status == EvmCertificatePublicationStatus.Revoked))));
    }

    private async Task<EvmCertificateJournalOperationRow> OwnedAsync(EvmCertificateJournalDbContext db, EvmCertificateJournalLease lease, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(lease);
        var op = await db.Set<EvmCertificateJournalOperationRow>().SingleOrDefaultAsync(o => o.Id == lease.OperationId, ct).ConfigureAwait(false);
        if (op is null)
        {
            throw Failure(EvmCertificateJournalFailure.LeaseLost);
        }
        RequireLease(op, lease);
        return op;
    }

    private void RequireLease(EvmCertificateJournalOperationRow op, EvmCertificateJournalLease lease)
    {
        if (lease.Token == Guid.Empty || op.LeaseToken != lease.Token || !op.LeaseUntil.HasValue || op.LeaseUntil <= clock.GetUtcNow())
        {
            throw Failure(EvmCertificateJournalFailure.LeaseLost);
        }
    }

    private void Changed(EvmCertificateJournalDbContext db, EvmCertificateJournalOperationRow op, string code)
    {
        Changed(db, op, code, null);
    }

    private void Changed(EvmCertificateJournalDbContext db, EvmCertificateJournalOperationRow op, string code, EvmCertificateProofResult? proof)
    {
        op.Revision++;
        op.UpdatedAt = clock.GetUtcNow();
        Event(db, op, code, op.UpdatedAt, proof);
    }

    private static void Event(EvmCertificateJournalDbContext db, EvmCertificateJournalOperationRow op, string code, DateTimeOffset now, EvmCertificateProofResult? proof)
    {
        db.Set<EvmCertificateJournalEventRow>().Add(new()
        {
            Id = Guid.NewGuid(), OperationId = op.Id, Revision = op.Revision, Code = code, Status = op.Status,
            TransactionHash = proof?.Evidence?.TransactionHash ?? op.TransactionHash, ProofStatus = proof?.Status,
            BlockHash = proof?.Evidence?.TransactionBlock.Hash, BlockNumber = proof?.Evidence?.TransactionBlock.Number, At = now
        });
    }

    private static void Release(EvmCertificateJournalOperationRow op)
    {
        op.LeaseToken = null;
        op.LeaseUntil = null;
    }

    private static bool SameIntent(EvmCertificateJournalOperationRow op, EvmCertificateJournalRequest request, string digest) =>
        op.DocumentId == request.DocumentId && op.OwnerUserId == request.OwnerUserId && op.TenantId == request.TenantId &&
        op.Producer == request.Producer && op.CertificateHash == digest && op.Profile == request.Network.Profile &&
        op.ChainId == request.Network.ChainId && op.ContractAddress == request.Network.ContractAddress && op.IssuerId == request.Network.IssuerId &&
        op.RuntimeCodeSha256 == request.Network.RuntimeCodeSha256 && op.FinalityMode == request.Network.FinalityMode &&
        op.MinimumConfirmations == request.Network.MinimumConfirmations;

    private static void RequireEvidence(EvmCertificateJournalOperationRow op, string hash, EvmCertificateProofResult result, bool ownAttempt)
    {
        var e = result.Evidence;
        if (e is null || e.Network is null || e.TransactionBlock is null || e.Network.VerificationBlock is null || e.State is null ||
            e.Operation != op.Kind || e.CertificateHash != op.CertificateHash || e.TransactionHash != hash ||
            e.Network.ChainId != op.ChainId || e.Network.ContractAddress != op.ContractAddress || e.Network.IssuerId != op.IssuerId ||
            e.Network.RuntimeCodeSha256 != op.RuntimeCodeSha256 || e.Network.FinalityMode != op.FinalityMode || e.Network.MinimumConfirmations != op.MinimumConfirmations ||
            e.TransactionBlock.Number == 0 || e.TransactionBlock.Number > e.Network.VerificationBlock.Number ||
            (ownAttempt && !string.Equals(e.Signer, op.SignerAddress, StringComparison.Ordinal)) ||
            !StateMatches(e, result.Status) ||
            (op.FinalityMode == Configuration.ApplicationLayer.EvmCertificateFinalityMode.Confirmations &&
                e.Network.VerificationBlock.Number - e.TransactionBlock.Number < (ulong)(op.MinimumConfirmations - 1)))
        {
            throw Failure(EvmCertificateJournalFailure.InvalidEvidence);
        }
        try
        {
            EvmCertificatePublicationValues.Address(e.Signer, nameof(e.Signer));
            EvmCertificateProtocol.NormalizeHashArgument(e.TransactionBlock.Hash, nameof(e.TransactionBlock.Hash));
            EvmCertificateProtocol.NormalizeHashArgument(e.Network.VerificationBlock.Hash, nameof(e.Network.VerificationBlock.Hash));
        }
        catch (ArgumentException)
        {
            throw Failure(EvmCertificateJournalFailure.InvalidEvidence);
        }
    }

    private static bool StateMatches(EvmCertificateEvidence evidence, EvmCertificateProofStatus status)
    {
        var state = evidence.State;
        if (state.AnchoredAtBlock == 0 || state.AnchoredAtBlock > evidence.Network.VerificationBlock.Number ||
            state.RevokedAtBlock > evidence.Network.VerificationBlock.Number)
        {
            return false;
        }
        if (evidence.Operation == EvmCertificateOperationKind.Revoke)
        {
            return status == EvmCertificateProofStatus.Verified && state.RevokedAtBlock == evidence.TransactionBlock.Number &&
                state.RevokedAtBlock >= state.AnchoredAtBlock && state.RevokeSigner == evidence.Signer;
        }
        return state.AnchoredAtBlock == evidence.TransactionBlock.Number && state.AnchorSigner == evidence.Signer &&
            (status == EvmCertificateProofStatus.Verified ? state.RevokedAtBlock == 0 :
                state.RevokedAtBlock >= state.AnchoredAtBlock && state.RevokedAtBlock > 0);
    }

    private static EvmCertificateJournalSnapshot Snapshot(EvmCertificateJournalOperationRow o) => new(o.Id,
        new(o.Profile, o.ChainId, o.ContractAddress, o.IssuerId, o.RuntimeCodeSha256, o.FinalityMode, o.MinimumConfirmations),
        o.DocumentId, o.OwnerUserId, o.TenantId, o.Producer, o.CertificateHash, o.Kind, o.Status,
        o.Nonce.HasValue ? checked((ulong)o.Nonce.Value) : null, o.SignerReference, o.SignerAddress,
        o.GasLimit.HasValue ? EvmCertificateFeeBudget.Create(checked((ulong)o.GasLimit.Value),
            EvmCertificatePublicationValues.Wei(o.MaximumFeePerGasWei!), EvmCertificatePublicationValues.Wei(o.MaximumTransactionFeeWei!)) : null,
        o.TransactionHash, o.CreatedAt, o.NextAttemptAt, o.Revision, o.LastFailure);

    private static EvmCertificateJournalAttempt Attempt(EvmCertificateJournalAttemptRow a) => new(a.Id, a.OperationId, a.Sequence,
        a.TransactionHash, a.RawTransaction, EvmCertificatePublicationValues.Wei(a.MaximumFeePerGasWei),
        EvmCertificatePublicationValues.Wei(a.PriorityFeePerGasWei), a.PreparedAt, a.BroadcastAt);

    private static void Delay(TimeSpan delay)
    {
        if (delay < TimeSpan.FromSeconds(1) || delay > TimeSpan.FromDays(1))
        {
            throw new ArgumentOutOfRangeException(nameof(delay));
        }
    }

    private async Task<T> RetryAsync<T>(Func<EvmCertificateJournalDbContext, Task<T>> action, CancellationToken cancellationToken)
    {
        for (var i = 0; i < 32; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await using var db = await contexts.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                return await action(db).ConfigureAwait(false);
            }
            catch (DbUpdateConcurrencyException)
            {
                if (i == 31)
                {
                    throw Failure(EvmCertificateJournalFailure.Conflict);
                }
                await Task.Delay(TimeSpan.FromMilliseconds(3 * (i + 1)), cancellationToken).ConfigureAwait(false);
            }
            catch (DbUpdateException ex) when (ex.InnerException is DbException { SqlState: "23505" or "40001" or "40P01" })
            {
                if (i == 31)
                {
                    throw Failure(EvmCertificateJournalFailure.Conflict);
                }
                await Task.Delay(TimeSpan.FromMilliseconds(3 * (i + 1)), cancellationToken).ConfigureAwait(false);
            }
        }
        throw Failure(EvmCertificateJournalFailure.Conflict);
    }

    private static EvmCertificateJournalException Failure(EvmCertificateJournalFailure failure) => new(failure);
}
