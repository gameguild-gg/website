using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using GameGuild.Compliance.Audit;
using GameGuild.Identity.Context.Actors;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace GameGuild.Tests.Audit.Unit.Services;

public abstract class AuditCustodyServiceTestBase : ValueSnapshotServiceTestBase
{
    protected readonly EcdsaCryptographicSigningService Signing;
    protected Guid TenantId => Actors.Object.ActorContext.TenantId!.Value;
    protected Guid UserId => Actors.Object.ActorContext.SubjectIdAsGuid!.Value;

    protected AuditCustodyServiceTestBase()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var options = new AuditSigningOptions { ActiveKeyId = "custody-test-key" };
        options.Keys[options.ActiveKeyId] = new AuditSigningKeyOptions { PrivateKeyPem = key.ExportECPrivateKeyPem() };
        Signing = new EcdsaCryptographicSigningService(Options.Create(options));
    }

    protected void SetActor(Guid tenant, Guid user) => Actors.SetupGet(accessor => accessor.ActorContext).Returns(new ActorContext
    {
        ActorKind = ActorKind.User, SubjectId = user.ToString(), TenantId = tenant,
        IsAuthenticated = true, Roles = new HashSet<string> { "TenantAdmin" }, Permissions = new HashSet<string>()
    });

    protected EvidenceCustodianService Custodians() => new(
        Actors.Object, Context, TimeProvider.System, NullLogger<EvidenceCustodianService>.Instance);

    protected ChainOfCustodyService Custody() => new(
        Actors.Object, Context, Custodians(), TimeProvider.System, NullLogger<ChainOfCustodyService>.Instance);

    protected Task<EvidenceCustodianResponse> RegisterAsync(Guid user, string name = "Test custodian") =>
        Custodians().RegisterAsync(new RegisterCustodianRequest
        {
            UserId = user, DisplayName = name, Scopes = CustodianScopes.All
        }, CancellationToken.None);

    protected Task<CustodyEvidenceResponse> PreserveAsync() => Custody().PreserveAsync(new PreserveEvidenceRequest
    {
        SourceKind = "RawContent", SourceId = "inline", RawContent = "preserved test evidence", Purpose = "Unit coverage"
    }, CancellationToken.None);

    protected void BecomeAnonymous() => Actors.SetupGet(accessor => accessor.ActorContext).Returns(ActorContext.Anonymous);
}

public sealed class EvidenceCustodianServiceTests : AuditCustodyServiceTestBase
{
    [Fact]
    public async Task RegisteredScopesAreDeniedAfterRevocation()
    {
        var user = UserId;
        var registration = await RegisterAsync(user);
        Assert.True(await Custodians().HasScopeAsync(user, CustodianScopes.Handle, CancellationToken.None));
        Assert.Equal(user, (await Custodians().RequireScopeAsync(user, CustodianScopes.Handle, CancellationToken.None)).UserId);

        var revoked = await Custodians().RevokeAsync(registration.Id, "Assignment ended", CancellationToken.None);

        Assert.False(revoked.IsActive);
        Assert.False(await Custodians().HasScopeAsync(user, CustodianScopes.Handle, CancellationToken.None));
        await Assert.ThrowsAsync<CustodyUnauthorizedException>(() =>
            Custodians().RequireScopeAsync(user, CustodianScopes.Handle, CancellationToken.None));
    }

    [Fact]
    public async Task RegistryAndScopesAreIsolatedByTenant()
    {
        var user = UserId;
        await RegisterAsync(user);
        SetActor(Guid.NewGuid(), user);

        Assert.Empty(await Custodians().ListAsync(CancellationToken.None));
        Assert.False(await Custodians().HasScopeAsync(user, CustodianScopes.Access, CancellationToken.None));
        BecomeAnonymous();
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => Custodians().ListAsync(CancellationToken.None));
    }
}

public sealed class ChainOfCustodyServiceTests : AuditCustodyServiceTestBase
{
    [Fact]
    public async Task PreservationRecordsContentAndDetectsTampering()
    {
        await RegisterAsync(UserId);
        var evidence = await PreserveAsync();
        Assert.True(evidence.IntegrityVerified);
        Assert.Equal("Collected", Assert.Single(evidence.Timeline).EventType);
        Assert.Equal(1, evidence.Timeline[0].SequenceNumber);
        Assert.Equal(UserId, evidence.CurrentCustodianId);
        var stored = await Context.Set<CustodyEvidenceItem>().SingleAsync(item => item.Id == evidence.Id);
        Assert.Equal("preserved test evidence", Encoding.UTF8.GetString(stored.Content));
        stored.Content = Encoding.UTF8.GetBytes("tampered evidence");
        await Context.SaveChangesAsync();

        Assert.False(await Custody().VerifyIntegrityAsync(evidence.Id, CancellationToken.None));
    }

    [Fact]
    public async Task EvidenceReadRequiresTenantAndActiveCustodian()
    {
        var user = UserId;
        var tenant = TenantId;
        var registration = await RegisterAsync(user);
        var evidence = await PreserveAsync();
        SetActor(Guid.NewGuid(), user);
        Assert.Null(await Custody().GetAsync(evidence.Id, "Other tenant", CancellationToken.None));
        SetActor(tenant, user);
        await Custodians().RevokeAsync(registration.Id, "Assignment ended", CancellationToken.None);
        await Assert.ThrowsAsync<CustodyUnauthorizedException>(() => Custody().GetAsync(evidence.Id, "Revoked", CancellationToken.None));
        BecomeAnonymous();
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => Custody().ListAsync(0, 10, CancellationToken.None));
    }
}

public sealed class EvidenceTransferServiceTests : AuditCustodyServiceTestBase
{
    private EvidenceTransferService Service() => new(
        Actors.Object, Context, Custodians(), Signing, TimeProvider.System, NullLogger<EvidenceTransferService>.Instance);

    [Fact]
    public async Task SignedTransferRequiresTheDesignatedReceiverAndMovesCustodyOnce()
    {
        var sender = UserId;
        var tenant = TenantId;
        var receiver = Guid.NewGuid();
        await RegisterAsync(sender);
        await RegisterAsync(receiver, "Receiving custodian");
        var evidence = await PreserveAsync();
        var transfer = await Service().InitiateAsync(new InitiateTransferRequest
        {
            EvidenceId = evidence.Id, ToCustodianId = receiver, Purpose = "Handoff"
        }, CancellationToken.None);
        Assert.True(transfer.ProofVerified);
        Assert.True(await Service().VerifyProofAsync(transfer.Id, CancellationToken.None));
        await Assert.ThrowsAsync<CustodyUnauthorizedException>(() =>
            Service().AcknowledgeAsync(transfer.Id, new AcknowledgeTransferRequest(), CancellationToken.None));

        SetActor(tenant, receiver);
        var acknowledged = await Service().AcknowledgeAsync(transfer.Id, new AcknowledgeTransferRequest(), CancellationToken.None);
        Assert.Equal("Acknowledged", acknowledged.State);
        Assert.Equal(receiver, acknowledged.AcknowledgedByUserId);
        Assert.Equal(receiver, (await Context.Set<CustodyEvidenceItem>().SingleAsync(item => item.Id == evidence.Id)).CurrentCustodianId);
        await Assert.ThrowsAsync<ValueSnapshotValidationException>(() =>
            Service().AcknowledgeAsync(transfer.Id, new AcknowledgeTransferRequest(), CancellationToken.None));
    }

    [Fact]
    public async Task AnonymousActorCannotListTransfers()
    {
        BecomeAnonymous();
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => Service().ListAsync(null, 0, 10, CancellationToken.None));
    }
}

public sealed class LegalHoldServiceTests : AuditCustodyServiceTestBase
{
    private LegalHoldService Service() => new(
        Actors.Object, Context, Custody(), Mock.Of<IAuditService>(), TimeProvider.System, NullLogger<LegalHoldService>.Instance);

    [Fact]
    public async Task ActiveHoldPreservesEvidenceAndBlocksReleaseUntilTheHoldEnds()
    {
        await RegisterAsync(UserId);
        var hold = await Service().CreateAsync(new CreateLegalHoldRequest
        {
            CaseNumber = "test-case-177",
            PreservationTargets = [new PreservationTarget { SourceKind = "RawContent", SourceId = "inline", RawContent = "held evidence" }],
            NotificationTargets = [new NotificationTarget { Kind = "Stakeholder", Target = "reviewer@example.test" }]
        }, CancellationToken.None);
        var evidenceId = Assert.Single(hold.PreservedEvidenceIds);
        Assert.Equal("Pending", Assert.Single(hold.Notifications).Status);
        await Assert.ThrowsAsync<ValueSnapshotValidationException>(() => Custody().ReleaseAsync(evidenceId, "Premature", CancellationToken.None));

        var released = await Service().ReleaseAsync(hold.Id, "Case ended", CancellationToken.None);
        Assert.Equal("Released", released.Status);
        Assert.Equal("Released", (await Custody().ReleaseAsync(evidenceId, "Hold ended", CancellationToken.None)).CustodyState);
        Assert.True(await Context.Set<CustodyEvidenceItem>().AnyAsync(item => item.Id == evidenceId));
    }

    [Fact]
    public async Task AnonymousActorCannotReadHolds()
    {
        BecomeAnonymous();
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => Service().ListAsync(0, 10, CancellationToken.None));
    }
}

public sealed class CourtAdmissiblePackagingServiceTests : AuditCustodyServiceTestBase
{
    private CourtAdmissiblePackagingService Service() => new(
        Actors.Object, Context, Custodians(), Signing, TimeProvider.System, NullLogger<CourtAdmissiblePackagingService>.Instance);

    [Fact]
    public async Task SignedManifestContainsTheTenantCustodianAuthorizationRecord()
    {
        var tenant = TenantId;
        var user = UserId;
        var registration = await RegisterAsync(user, "Current tenant custodian");
        Assert.NotEqual(registration.Id, user);
        var evidence = await PreserveAsync();
        SetActor(Guid.NewGuid(), user);
        await RegisterAsync(user, "Other tenant custodian");
        SetActor(tenant, user);

        var package = await Service().BuildAsync(new BuildCustodyPackageRequest { EvidenceId = evidence.Id }, CancellationToken.None);

        Assert.True(package.Verification.ContentHashVerified);
        Assert.True(package.Verification.ChainSequenceGapless);
        Assert.True(Signing.VerifySignature(package.ManifestJson, package.Signature, package.SigningKeyId));
        using var manifest = JsonDocument.Parse(package.ManifestJson);
        var custodian = Assert.Single(manifest.RootElement.GetProperty("custodians").EnumerateArray());
        Assert.Equal(user, custodian.GetProperty("userId").GetGuid());
        Assert.Equal(registration.Id, custodian.GetProperty("id").GetGuid());
        Assert.Equal("Current tenant custodian", custodian.GetProperty("displayName").GetString());
        Assert.True(await Context.Set<ComplianceSealedPackage>().AnyAsync(item => item.Id == package.PackageId));
    }

    [Fact]
    public async Task AnonymousActorCannotPackageEvidence()
    {
        BecomeAnonymous();
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            Service().BuildAsync(new BuildCustodyPackageRequest { EvidenceId = Guid.NewGuid() }, CancellationToken.None));
    }
}
