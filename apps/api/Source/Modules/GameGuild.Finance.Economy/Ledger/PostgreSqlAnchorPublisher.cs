using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using GameGuild.Finance.Economy.Persistence;
using GameGuild.Finance.Economy.Risk;
using Microsoft.EntityFrameworkCore;

namespace GameGuild.Finance.Economy.Ledger;

public sealed record EconomyAnchorPublicationResult(
    Guid AnchorId,
    long JournalSequence,
    string JournalHash,
    string KeyId,
    string ObjectKey,
    string ObjectVersion,
    string ETag,
    DateTimeOffset RetainUntil,
    string ObjectHash);

public interface IEconomyAnchorPublisher
{
    ValueTask<EconomyAnchorPublicationResult?> PublishIfDueAsync(
        DateTimeOffset now,
        bool onDemand,
        string? dispatchSnapshotHash,
        CancellationToken cancellationToken);
}

public sealed class PostgreSqlEconomyAnchorPublisher : IEconomyAnchorPublisher
{
    internal const long EntryInterval = 1_000;
    internal static readonly TimeSpan TimeInterval = TimeSpan.FromMinutes(5);

    private readonly DbContext _db;
    private readonly ICapabilityReceiptSigner _signer;
    private readonly ICapabilityPolicySignatureVerifier _signatureVerifier;
    private readonly IWormAnchorStore _worm;

    public PostgreSqlEconomyAnchorPublisher(
        IApplicationDbContext context,
        ICapabilityReceiptSigner signer,
        ICapabilityPolicySignatureVerifier signatureVerifier,
        IWormAnchorStore worm)
    {
        _db = PostgreSqlEntityRiskGraphStore.RequireRelationalContext(context);
        ArgumentNullException.ThrowIfNull(signer);
        ArgumentNullException.ThrowIfNull(signatureVerifier);
        ArgumentNullException.ThrowIfNull(worm);
        _signer = signer;
        _signatureVerifier = signatureVerifier;
        _worm = worm;
    }

    public async ValueTask<EconomyAnchorPublicationResult?> PublishIfDueAsync(
        DateTimeOffset now,
        bool onDemand,
        string? dispatchSnapshotHash,
        CancellationToken cancellationToken)
    {
        var head = await _db.Set<EconomyChainHeadRow>().AsNoTracking().SingleOrDefaultAsync(cancellationToken)
            ?? throw new AnchorPublicationBlockedException("The journal chain head is unavailable.");
        var checkpoint = await _db.Set<EconomyJournalVerificationCheckpointRow>().AsNoTracking()
            .Where(row => row.IsValid && row.ToSequence == head.Sequence && row.CurrentHash == head.Hash)
            .OrderByDescending(row => row.CompletedAt)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new AnchorPublicationBlockedException("The journal head has not passed integrity verification.");

        var latest = await _db.Set<EconomyExternalAnchorRow>().AsNoTracking()
            .OrderByDescending(row => row.JournalSequence)
            .ThenByDescending(row => row.AnchoredAt)
            .FirstOrDefaultAsync(cancellationToken);
        if (!onDemand && latest is not null &&
            head.Sequence - latest.JournalSequence < EntryInterval &&
            now - latest.AnchoredAt < TimeInterval)
            return null;

        var anchorId = Guid.NewGuid();
        var payload = Canonicalize(new
        {
            anchor_id = anchorId,
            journal_sequence = head.Sequence,
            journal_hash = head.Hash,
            checkpoint_id = checkpoint.Id,
            checkpoint_fencing_token = checkpoint.FencingToken,
            dispatch_snapshot_hash = NormalizeOptionalHash(dispatchSnapshotHash),
            anchored_at = now
        });
        var signature = await _signer.SignAsync(payload, cancellationToken);
        if (!await _signatureVerifier.VerifyAsync(
                payload, signature.KeyId, signature.Signature, cancellationToken))
            throw new AnchorPublicationBlockedException("The KMS anchor signature could not be verified.");

        var signedEnvelope = Canonicalize(new
        {
            payload = JsonDocument.Parse(payload).RootElement,
            key_id = signature.KeyId,
            signature = signature.Signature
        });
        var publication = await _worm.PublishAsync(
            new WormAnchorObject(anchorId, signedEnvelope, now), cancellationToken);
        if (!publication.ReadBackVerified)
            throw new AnchorPublicationBlockedException("The WORM anchor did not pass mandatory read-back verification.");

        var row = new EconomyExternalAnchorRow
        {
            Id = anchorId,
            JournalSequence = head.Sequence,
            JournalHash = head.Hash,
            Signature = signature.Signature,
            WormReference = publication.ObjectKey,
            DispatchSnapshotHash = NormalizeOptionalHash(dispatchSnapshotHash),
            Provider = "s3-object-lock",
            ProviderReference = publication.ObjectVersion,
            AnchoredAt = now
        };
        _db.Set<EconomyExternalAnchorRow>().Add(row);
        _db.Set<EconomyAnchorVerificationRow>().Add(new EconomyAnchorVerificationRow
        {
            Id = Guid.NewGuid(),
            ExternalAnchorId = anchorId,
            KeyId = signature.KeyId,
            ObjectVersion = publication.ObjectVersion,
            ETag = publication.ETag,
            RetainUntil = publication.RetainUntil,
            ObjectHash = publication.ObjectHash,
            SignatureValid = true,
            ObjectMatches = true,
            VerifiedAt = now
        });
        await _db.SaveChangesAsync(cancellationToken);
        return new EconomyAnchorPublicationResult(
            anchorId,
            head.Sequence,
            head.Hash,
            signature.KeyId,
            publication.ObjectKey,
            publication.ObjectVersion,
            publication.ETag,
            publication.RetainUntil,
            publication.ObjectHash);
    }

    private static string? NormalizeOptionalHash(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        return value.Trim();
    }

    private static string Canonicalize<T>(T value)
    {
        using var document = JsonDocument.Parse(JsonSerializer.Serialize(value));
        return EconomyCanonicalJson.Serialize(document.RootElement);
    }
}

public sealed class AnchorPublicationBlockedException(string message) : InvalidOperationException(message);
