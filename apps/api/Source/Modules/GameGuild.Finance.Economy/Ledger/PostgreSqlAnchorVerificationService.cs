using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using GameGuild.Finance.Economy.Persistence;
using GameGuild.Finance.Economy.Risk;
using Microsoft.EntityFrameworkCore;

namespace GameGuild.Finance.Economy.Ledger;

public sealed record AnchorVerificationRunResult(int Verified, int Failed);

public interface IEconomyAnchorVerificationService
{
    ValueTask<AnchorVerificationRunResult> VerifyPublishedAnchorsAsync(
        DateTimeOffset verifiedAt,
        CancellationToken cancellationToken);
}

public sealed class PostgreSqlAnchorVerificationService : IEconomyAnchorVerificationService
{
    private static readonly Guid SystemActorId = new("ac174f23-8f12-46dd-8da2-7e0df3dc2aba");
    private readonly DbContext _db;
    private readonly IWormAnchorVerifier _worm;
    private readonly ICapabilityPolicySignatureVerifier _signatures;
    private readonly IEconomyKillSwitchStore _killSwitches;

    public PostgreSqlAnchorVerificationService(
        IApplicationDbContext context,
        IWormAnchorVerifier worm,
        ICapabilityPolicySignatureVerifier signatures,
        IEconomyKillSwitchStore killSwitches)
    {
        _db = PostgreSqlEntityRiskGraphStore.RequireRelationalContext(context);
        ArgumentNullException.ThrowIfNull(worm);
        ArgumentNullException.ThrowIfNull(signatures);
        ArgumentNullException.ThrowIfNull(killSwitches);
        _worm = worm;
        _signatures = signatures;
        _killSwitches = killSwitches;
    }

    public async ValueTask<AnchorVerificationRunResult> VerifyPublishedAnchorsAsync(
        DateTimeOffset verifiedAt,
        CancellationToken cancellationToken)
    {
        var anchors = await _db.Set<EconomyExternalAnchorRow>().AsNoTracking()
            .OrderBy(row => row.JournalSequence).ToArrayAsync(cancellationToken);
        var verified = 0;
        var failed = 0;
        foreach (var anchor in anchors)
        {
            var previous = await _db.Set<EconomyAnchorVerificationRow>().AsNoTracking()
                .Where(row => row.ExternalAnchorId == anchor.Id)
                .OrderByDescending(row => row.VerifiedAt)
                .FirstOrDefaultAsync(cancellationToken);
            if (previous is null)
            {
                failed++;
                await ActivateKillSwitchAsync(anchor, "missing-verification-metadata", verifiedAt, cancellationToken);
                continue;
            }

            WormAnchorReadBack? readBack = null;
            var signatureValid = false;
            var objectMatches = false;
            var keyId = previous.KeyId;
            try
            {
                readBack = await _worm.ReadAndVerifyAsync(new WormAnchorReference(
                    anchor.WormReference,
                    previous.ObjectVersion,
                    previous.ETag,
                    previous.RetainUntil,
                    previous.ObjectHash), cancellationToken);
                objectMatches = readBack.ObjectMatches && readBack.RetainUntil > verifiedAt;
                using var document = JsonDocument.Parse(readBack.CanonicalPayload);
                var root = document.RootElement;
                var payload = root.GetProperty("payload");
                keyId = root.GetProperty("key_id").GetString() ?? string.Empty;
                var signature = root.GetProperty("signature").GetString() ?? string.Empty;
                var payloadMatches = payload.GetProperty("anchor_id").GetGuid() == anchor.Id &&
                                     payload.GetProperty("journal_sequence").GetInt64() == anchor.JournalSequence &&
                                     payload.GetProperty("journal_hash").GetString() == anchor.JournalHash &&
                                     signature == anchor.Signature;
                var canonicalPayload = EconomyCanonicalJson.Serialize(payload);
                signatureValid = payloadMatches && await _signatures.VerifyAsync(
                    canonicalPayload, keyId, signature, cancellationToken);
            }
            catch (Exception exception) when (exception is WormAnchorUnavailableException or
                                               WormAnchorVerificationException or
                                               JsonException or KeyNotFoundException or InvalidOperationException)
            {
                objectMatches = false;
                signatureValid = false;
            }

            _db.Set<EconomyAnchorVerificationRow>().Add(new EconomyAnchorVerificationRow
            {
                Id = Guid.NewGuid(),
                ExternalAnchorId = anchor.Id,
                KeyId = keyId,
                ObjectVersion = readBack?.ObjectVersion ?? previous.ObjectVersion,
                ETag = readBack?.ETag ?? previous.ETag,
                RetainUntil = readBack?.RetainUntil ?? previous.RetainUntil,
                ObjectHash = readBack?.ObjectHash ?? previous.ObjectHash,
                SignatureValid = signatureValid,
                ObjectMatches = objectMatches,
                VerifiedAt = verifiedAt
            });
            await _db.SaveChangesAsync(cancellationToken);
            if (signatureValid && objectMatches)
            {
                verified++;
                continue;
            }
            failed++;
            await ActivateKillSwitchAsync(anchor, "anchor-object-or-signature-invalid", verifiedAt, cancellationToken);
        }
        return new AnchorVerificationRunResult(verified, failed);
    }

    private async ValueTask ActivateKillSwitchAsync(
        EconomyExternalAnchorRow anchor,
        string reason,
        DateTimeOffset activatedAt,
        CancellationToken cancellationToken)
    {
        var seed = SHA256.HashData(Encoding.UTF8.GetBytes($"anchor|{anchor.Id:N}|{reason}"));
        var activationId = new Guid(seed.AsSpan(0, 16));
        try
        {
            await _killSwitches.ActivateAsync(
                activationId,
                EconomyKillSwitchScope.Global,
                $"External anchor verification failed: {reason}.",
                SystemActorId,
                activatedAt,
                cancellationToken);
        }
        catch (InvalidOperationException)
        {
            // A global switch is already active; value movement remains fail-closed.
        }
    }
}
