using System.Text.Json;
using GameGuild.API.Core.Integration;
using GameGuild.Configuration.ApplicationLayer;
using GameGuild.Identity.Authentication;
using GameGuild.Learning.Certificates;
using Moq;

namespace GameGuild.API.UnitTests.Integration;

/// <summary>Reader policy only: journal lookup and chain verification are controlled boundaries here.</summary>
public sealed class CertificateEvmEvidenceReaderTests
{
    [Theory]
    [InlineData(EvmCertificatePublicationStatus.Queued)]
    [InlineData(EvmCertificatePublicationStatus.Confirmed)]
    [InlineData(EvmCertificatePublicationStatus.Revoked)]
    public async Task CachedStateNeverBecomesAFreshProof(EvmCertificatePublicationStatus status)
    {
        var h = new Harness(); h.Anchor = h.Operation(EvmCertificateOperationKind.Anchor, status, Harness.Transaction);
        var result = await h.Reader.ReadAsync(h.Document, false, CancellationToken.None);
        Assert.Equal(CertificateEvmValidity.Unknown, result.Validity);
        Assert.Equal(CertificateEvmEvidenceReadStatus.Cached, result.ReadStatus);
        Assert.Null(result.ChainProof); h.Proofs.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task MissingJournalDoesNotClaimTheCertificateWasNeverPublished()
    {
        var h = new Harness();
        var result = await h.Reader.ReadAsync(h.Document, true, CancellationToken.None);
        Assert.Equal(CertificateEvmEvidenceReadStatus.NoJournalEntry, result.ReadStatus);
        Assert.Equal(CertificateEvmValidity.Unknown, result.Validity); h.Proofs.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task AQueuedIntentHasNoInventedReceipt()
    {
        var h = new Harness(); h.Anchor = h.Operation(EvmCertificateOperationKind.Anchor, EvmCertificatePublicationStatus.Queued, null);
        var result = await h.Reader.ReadAsync(h.Document, true, CancellationToken.None);
        Assert.Equal(CertificateEvmEvidenceReadStatus.NoRecordedTransaction, result.ReadStatus);
        Assert.Null(result.Anchor!.TransactionHash); Assert.Null(result.ChainProof); h.Proofs.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task ACurrentBoundFinalizedAnchorCanProveAnActiveCertificateValid()
    {
        var h = Anchored(); h.AnchorProof(h.Proof(EvmCertificateProofStatus.Verified, false));
        var result = await h.Reader.ReadAsync(h.Document, true, CancellationToken.None);
        Assert.Equal(CertificateEvmValidity.Valid, result.Validity);
        Assert.Equal(CertificateEvmEvidenceReadStatus.Observed, result.ReadStatus);
        Assert.True(result.LocallyValid); Assert.NotNull(result.ChainProof!.Evidence);
    }

    [Theory]
    [InlineData(CertificateStatus.Revoked)]
    [InlineData(CertificateStatus.Expired)]
    public async Task AChainAnchorCannotMakeALocallyInvalidCertificateValid(CertificateStatus status)
    {
        var h = Anchored(); h.AnchorProof(h.Proof(EvmCertificateProofStatus.Verified, false));
        var result = await h.Reader.ReadAsync(h.Document with { Status = status }, true, CancellationToken.None);
        Assert.False(result.LocallyValid); Assert.Equal(CertificateEvmValidity.Invalid, result.Validity);
    }

    [Fact]
    public async Task ExpiryIsEvaluatedAtTheCurrentClockEvenWithAnActiveStoredStatus()
    {
        var h = Anchored(); h.AnchorProof(h.Proof(EvmCertificateProofStatus.Verified, false));
        var result = await h.Reader.ReadAsync(h.Document with { ExpiresAt = Harness.Now.UtcDateTime.AddSeconds(-1) }, true, CancellationToken.None);
        Assert.False(result.LocallyValid); Assert.Equal(CertificateEvmValidity.Invalid, result.Validity);
    }

    [Fact]
    public async Task AnOnChainRevocationInvalidatesAnOtherwiseActiveLocalCertificate()
    {
        var h = Anchored(); h.AnchorProof(h.Proof(EvmCertificateProofStatus.Revoked, true));
        var result = await h.Reader.ReadAsync(h.Document, true, CancellationToken.None);
        Assert.Equal(CertificateEvmValidity.Invalid, result.Validity);
    }

    [Fact]
    public async Task AnUnavailableOriginalProfileIsNeverQueriedThroughTheCurrentEndpoint()
    {
        var h = Anchored(); h.Anchor = h.Anchor! with { Network = CertificateEvmEvidenceProducerTests.Network("other-profile", '4') };
        var result = await h.Reader.ReadAsync(h.Document, true, CancellationToken.None);
        Assert.Equal(CertificateEvmEvidenceReadStatus.ProfileUnavailable, result.ReadStatus);
        Assert.Equal(CertificateEvmValidity.Unknown, result.Validity); h.Proofs.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData("digest")]
    [InlineData("transaction")]
    [InlineData("network")]
    [InlineData("signer")]
    public async Task EvidenceMustBeBoundToTheOriginalOperation(string mismatch)
    {
        var h = Anchored(); var proof = h.Proof(EvmCertificateProofStatus.Verified, false); var e = proof.Evidence!;
        e = mismatch switch
        {
            "digest" => e with { CertificateHash = "0x" + new string('7', 64) },
            "transaction" => e with { TransactionHash = "0x" + new string('8', 64) },
            "network" => e with { Network = e.Network with { ChainId = 1 } },
            _ => e with { Signer = "0x" + new string('9', 40) }
        };
        h.AnchorProof(proof with { Evidence = e });
        var result = await h.Reader.ReadAsync(h.Document, true, CancellationToken.None);
        Assert.Equal(EvmCertificateProofStatus.InvalidEvidence, result.ChainProof!.Status);
        Assert.Equal(CertificateEvmValidity.Unknown, result.Validity); Assert.Null(result.ChainProof.Evidence);
    }

    [Fact]
    public async Task AVerifiedLabelWithoutEvidenceDoesNotProveValidity()
    {
        var h = Anchored(); h.AnchorProof(new(EvmCertificateProofStatus.Verified));
        var result = await h.Reader.ReadAsync(h.Document, true, CancellationToken.None);
        Assert.Equal(CertificateEvmValidity.Unknown, result.Validity);
        Assert.Equal(EvmCertificateProofStatus.InvalidEvidence, result.ChainProof!.Status);
    }

    [Fact]
    public async Task ProviderFailuresRemainUnavailableWithoutLeakingTheirContents()
    {
        var h = Anchored(); h.Proofs.Setup(p => p.VerifyAnchorAsync(Harness.Digest, Harness.Transaction, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("credential-bearing-provider-detail"));
        var result = await h.Reader.ReadAsync(h.Document, true, CancellationToken.None);
        Assert.Equal(CertificateEvmEvidenceReadStatus.Unavailable, result.ReadStatus);
        Assert.Equal(CertificateEvmValidity.Unknown, result.Validity);
        Assert.DoesNotContain("credential-bearing", JsonSerializer.Serialize(result), StringComparison.Ordinal);
    }

    [Fact]
    public async Task AmbiguousOriginalIdentityDoesNotSelectAnArbitraryOperation()
    {
        var h = new Harness(); h.Lookup.Setup(l => l.FindAsync(h.Document.Id, h.Document.OwnerUserId, h.Document.TenantId,
            EvmCertificateOperationKind.Anchor, It.IsAny<CancellationToken>())).ThrowsAsync(new EvmCertificateJournalException(EvmCertificateJournalFailure.Conflict));
        var result = await h.Reader.ReadAsync(h.Document, true, CancellationToken.None);
        Assert.Equal(CertificateEvmEvidenceReadStatus.AmbiguousIdentity, result.ReadStatus); h.Proofs.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task ARevocationForAnotherDigestDoesNotReplaceTheOriginalAnchor()
    {
        var h = Anchored(); h.Revocation = h.Operation(EvmCertificateOperationKind.Revoke, EvmCertificatePublicationStatus.Submitted, Harness.Transaction)
            with { CertificateHash = "0x" + new string('7', 64) };
        var result = await h.Reader.ReadAsync(h.Document, true, CancellationToken.None);
        Assert.Equal(CertificateEvmEvidenceReadStatus.AmbiguousIdentity, result.ReadStatus); h.Proofs.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task FreshRevocationUsesTheOriginalRevocationTransactionAndVerifier()
    {
        var h = Anchored(); h.Revocation = h.Operation(EvmCertificateOperationKind.Revoke, EvmCertificatePublicationStatus.Submitted, Harness.Transaction);
        var proof = h.Proof(EvmCertificateProofStatus.Verified, true);
        h.Proofs.Setup(p => p.VerifyRevocationAsync(Harness.Digest, Harness.Transaction, It.IsAny<CancellationToken>()))
            .ReturnsAsync(proof with { Evidence = proof.Evidence! with { Operation = EvmCertificateOperationKind.Revoke } });
        var result = await h.Reader.ReadAsync(h.Document, true, CancellationToken.None);
        Assert.Equal(CertificateEvmValidity.Invalid, result.Validity);
        h.Proofs.Verify(p => p.VerifyRevocationAsync(Harness.Digest, Harness.Transaction, It.IsAny<CancellationToken>()), Times.Once);
        h.Proofs.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task LookupAlwaysReceivesThePersistedOwnerAndExactTenant()
    {
        var h = new Harness(); await h.Reader.ReadAsync(h.Document, false, CancellationToken.None);
        foreach (var kind in new[] { EvmCertificateOperationKind.Anchor, EvmCertificateOperationKind.Revoke })
        {
            h.Lookup.Verify(l => l.FindAsync(h.Document.Id, h.Document.OwnerUserId, h.Document.TenantId, kind, It.IsAny<CancellationToken>()), Times.Once);
        }
        h.Lookup.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task CachedProjectionDoesNotExposeSignerReferencesBudgetsOrRawTransactions()
    {
        var h = Anchored(); var result = await h.Reader.ReadAsync(h.Document, false, CancellationToken.None);
        var json = JsonSerializer.Serialize(result);
        foreach (var name in new[] { "secret-key-reference", "SignerReference", "RawTransaction", "LeaseToken", "MaximumFeePerGasWei", "OwnerUserId", "TenantId" })
        {
            Assert.DoesNotContain(name, json, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public async Task CallerCancellationIsNotConvertedToAProviderFailure()
    {
        var h = new Harness(); using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => h.Reader.ReadAsync(h.Document, true, cancellation.Token));
        h.Lookup.VerifyNoOtherCalls(); h.Proofs.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task DisabledEvidenceNeverClaimsAChainProof()
    {
        var h = new Harness();
        var result = await new DisabledCertificateEvmEvidenceReader(new FrozenClock()).ReadAsync(h.Document, true, CancellationToken.None);
        Assert.False(result.Configured); Assert.Equal(CertificateEvmEvidenceReadStatus.Disabled, result.ReadStatus);
        Assert.Equal(CertificateEvmValidity.Unknown, result.Validity); Assert.Null(result.ChainProof);
        Assert.True(result.FreshProofRequested);
    }

    [Fact]
    public async Task AnOriginalExternalProofCanBeVerifiedWithoutInventingALocalSigningIdentity()
    {
        var h = Anchored(); h.Anchor = h.Anchor! with { SignerAddress = null };
        h.AnchorProof(h.Proof(EvmCertificateProofStatus.Verified, false));
        var result = await h.Reader.ReadAsync(h.Document, true, CancellationToken.None);
        Assert.Equal(CertificateEvmValidity.Valid, result.Validity);
    }

    [Theory]
    [InlineData("document")]
    [InlineData("owner")]
    [InlineData("tenant")]
    [InlineData("producer")]
    [InlineData("kind")]
    public async Task ALookupScopeMismatchIsNotProjectedOrSentToRpc(string mismatch)
    {
        var h = Anchored(); h.Anchor = mismatch switch
        {
            "document" => h.Anchor! with { DocumentId = Guid.NewGuid() },
            "owner" => h.Anchor! with { OwnerUserId = Guid.NewGuid() },
            "tenant" => h.Anchor! with { TenantId = Guid.NewGuid() },
            "producer" => h.Anchor! with { Producer = "another-producer" },
            _ => h.Anchor! with { Kind = EvmCertificateOperationKind.Revoke }
        };
        var result = await h.Reader.ReadAsync(h.Document, true, CancellationToken.None);
        Assert.Equal(CertificateEvmEvidenceReadStatus.AmbiguousIdentity, result.ReadStatus);
        Assert.Null(result.Anchor); Assert.Null(result.Revocation); h.Proofs.VerifyNoOtherCalls();
    }

    private static Harness Anchored()
    {
        var h = new Harness(); h.Anchor = h.Operation(EvmCertificateOperationKind.Anchor, EvmCertificatePublicationStatus.Confirmed, Harness.Transaction); return h;
    }

    private sealed class FrozenClock : TimeProvider { public override DateTimeOffset GetUtcNow() => Harness.Now; }

    private sealed class Harness
    {
        public static DateTimeOffset Now => new(2026, 10, 20, 0, 0, 0, TimeSpan.Zero);
        public static string Digest => "0x" + new string('5', 64);
        public static string Transaction => "0x" + new string('6', 64);
        public static string Signer => "0x" + new string('4', 40);
        public CertificateEvmDocument Document { get; } = new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), CertificateStatus.Active, null);
        public EvmCertificateJournalSnapshot? Anchor { get; set; }
        public EvmCertificateJournalSnapshot? Revocation { get; set; }
        public Mock<ICertificateEvmPublicationLookup> Lookup { get; } = new(MockBehavior.Strict);
        public Mock<IEvmCertificateProofVerifier> Proofs { get; } = new(MockBehavior.Strict);
        public EvmCertificateEvidenceReader Reader { get; }
        public EvmCertificatePublicationNetwork Network { get; } = CertificateEvmEvidenceProducerTests.Network();

        public Harness()
        {
            Lookup.Setup(l => l.FindAsync(Document.Id, Document.OwnerUserId, Document.TenantId, EvmCertificateOperationKind.Anchor, It.IsAny<CancellationToken>())).ReturnsAsync(() => Anchor);
            Lookup.Setup(l => l.FindAsync(Document.Id, Document.OwnerUserId, Document.TenantId, EvmCertificateOperationKind.Revoke, It.IsAny<CancellationToken>())).ReturnsAsync(() => Revocation);
            Reader = new(Lookup.Object, Proofs.Object, Network, new FrozenClock());
        }

        public EvmCertificateJournalSnapshot Operation(EvmCertificateOperationKind kind, EvmCertificatePublicationStatus status, string? transaction) =>
            new(Guid.NewGuid(), Network, Document.Id, Document.OwnerUserId, Document.TenantId, CertificateEvmEvidenceProducer.ProducerName,
                Digest, kind, status, null, "secret-key-reference", Signer, null, transaction, Now, Now, 1, null);

        public EvmCertificateProofResult Proof(EvmCertificateProofStatus status, bool revoked) => new(status,
            new(EvmCertificateOperationKind.Anchor, Digest, Transaction, Signer, new(99, "0x" + new string('a', 64)),
                new(Network.ChainId, Network.ContractAddress, Network.IssuerId, Network.RuntimeCodeSha256, Network.FinalityMode,
                    Network.MinimumConfirmations, new(100, "0x" + new string('b', 64))),
                new(Signer, revoked ? Signer : "0x" + new string('0', 40), 99, revoked ? 100UL : 0UL)));

        public void AnchorProof(EvmCertificateProofResult proof) => Proofs.Setup(p => p.VerifyAnchorAsync(Digest, Transaction, It.IsAny<CancellationToken>())).ReturnsAsync(proof);
    }
}
