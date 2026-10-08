using FluentAssertions;
using GameGuild.CQRS.Models;
using GameGuild.Identity.Context.Actors;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace GameGuild.Identity.Authorization.UnitTests;

/// <summary>
///     Guard and lifecycle tests for the signed policy bundle registry commands:
///     signing is system-admin-only and approval/deployment fail closed without a valid signature.
/// </summary>
public class SignPolicyBundleCommandHandlerTests
{
    private static (string PublicPem, string PrivatePem) GenerateKeyPair()
    {
        using var key = System.Security.Cryptography.ECDsa.Create(
            System.Security.Cryptography.ECCurve.NamedCurves.nistP256);
        return (key.ExportSubjectPublicKeyInfoPem(), key.ExportPkcs8PrivateKeyPem());
    }

    private static PolicyBundleSigningOptions DefaultOptions()
    {
        var (publicPem, privatePem) = GenerateKeyPair();
        return new PolicyBundleSigningOptions
        {
            TrustedKeys = [new TrustedPolicySigningKey { KeyId = "key-1", PublicKeyPem = publicPem }],
            ActiveKey = new PolicyBundleSigningKeyOptions { KeyId = "key-1", PrivateKeyPem = privatePem }
        };
    }

    private static PolicyBundleSignatureService SignatureService(PolicyBundleSigningOptions? options = null) =>
        new(
            Options.Create(options ?? DefaultOptions()),
            NullLogger<PolicyBundleSignatureService>.Instance);

    private static ActorContext Actor(
        bool isAuthenticated,
        bool isSystemAdmin = false,
        bool isTenantAdmin = false,
        Guid? tenantId = null,
        Guid? subjectId = null)
    {
        var roles = new List<string>();
        if (isSystemAdmin) roles.Add("SystemAdmin");
        if (isTenantAdmin) roles.Add("TenantAdmin");

        return new ActorContext
        {
            ActorKind = ActorKind.User,
            SubjectId = (subjectId ?? Guid.NewGuid()).ToString(),
            TenantId = tenantId,
            Roles = new HashSet<string>(roles),
            Permissions = new HashSet<string>(),
            IsAuthenticated = isAuthenticated
        };
    }

    private static PolicyBundle DraftBundle(Action<PolicyBundle>? mutate = null)
    {
        var bundle = new PolicyBundle
        {
            Name = "baseline",
            Version = "1.0.0",
            IsGlobal = true,
            BundleType = PolicyBundleType.Composite,
            PolicyData = """[{"policyName":"p1","requiredPermissions":["content:read"]}]""",
            Status = PolicyBundleStatus.Draft,
            CreatedBy = Guid.NewGuid()
        };
        mutate?.Invoke(bundle);
        return bundle;
    }

    private static (Mock<IPolicyBundleRepository> BundleRepository,
        Mock<IPolicyRegistryAuditLogRepository> AuditRepository,
        Mock<IActorContextAccessor> Accessor) DefaultMocks(ActorContext actor)
    {
        var bundles = new Mock<IPolicyBundleRepository>();
        var audit = new Mock<IPolicyRegistryAuditLogRepository>();
        var accessor = new Mock<IActorContextAccessor>();
        accessor.SetupGet(a => a.ActorContext).Returns(actor);
        return (bundles, audit, accessor);
    }

    [Fact]
    public async Task Sign_Is_Denied_For_Anonymous_Actors()
    {
        var (bundles, audit, accessor) = DefaultMocks(Actor(isAuthenticated: false));
        var handler = new SignPolicyBundleCommandHandler(
            bundles.Object, audit.Object, SignatureService(), accessor.Object,
            NullLogger<SignPolicyBundleCommandHandler>.Instance);

        var act = () => handler.Handle(
            new SignPolicyBundleCommand { BundleId = Guid.NewGuid() }, CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
        bundles.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Sign_Is_Denied_For_TenantAdmins_SigningIsSystemAdminOnly()
    {
        var tenantId = Guid.NewGuid();
        var (bundles, audit, accessor) = DefaultMocks(
            Actor(isAuthenticated: true, isTenantAdmin: true, tenantId: tenantId));
        var handler = new SignPolicyBundleCommandHandler(
            bundles.Object, audit.Object, SignatureService(), accessor.Object,
            NullLogger<SignPolicyBundleCommandHandler>.Instance);

        var act = () => handler.Handle(
            new SignPolicyBundleCommand { BundleId = Guid.NewGuid() }, CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("*system administration*");
        bundles.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Sign_By_SystemAdmin_Signs_Persists_And_Audits()
    {
        var actorId = Guid.NewGuid();
        var (bundles, audit, accessor) = DefaultMocks(
            Actor(isAuthenticated: true, isSystemAdmin: true, subjectId: actorId));
        var bundle = DraftBundle();
        bundles.Setup(r => r.GetByIdAsync(bundle.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(bundle);

        var handler = new SignPolicyBundleCommandHandler(
            bundles.Object, audit.Object, SignatureService(), accessor.Object,
            NullLogger<SignPolicyBundleCommandHandler>.Instance);

        var result = await handler.Handle(
            new SignPolicyBundleCommand { BundleId = bundle.Id }, CancellationToken.None);

        result.BundleId.Should().Be(bundle.Id);
        result.KeyId.Should().Be("key-1");
        result.ContentHash.Should().HaveLength(64);
        bundle.DigitalSignature.Should().NotBeNullOrWhiteSpace();

        bundles.Verify(r => r.UpdateAsync(bundle, It.IsAny<CancellationToken>()), Times.Once);
        audit.Verify(a => a.CreateAsync(
            It.Is<PolicyRegistryAuditLog>(l =>
                l.BundleId == bundle.Id
                && l.Action == PolicyRegistryAction.Sign
                && l.PerformedBy == actorId),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Sign_Fails_Closed_When_Bundle_Inputs_Violate_The_Contract()
    {
        var (bundles, audit, accessor) = DefaultMocks(
            Actor(isAuthenticated: true, isSystemAdmin: true));
        var bundle = DraftBundle(b => b.PolicyData = "{invalid");
        bundles.Setup(r => r.GetByIdAsync(bundle.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(bundle);

        var handler = new SignPolicyBundleCommandHandler(
            bundles.Object, audit.Object, SignatureService(), accessor.Object,
            NullLogger<SignPolicyBundleCommandHandler>.Instance);

        var act = () => handler.Handle(
            new SignPolicyBundleCommand { BundleId = bundle.Id }, CancellationToken.None);

        await act.Should().ThrowAsync<PolicyBundleSignatureException>();
        bundles.Verify(r => r.UpdateAsync(It.IsAny<PolicyBundle>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Approve_Fails_Closed_When_Bundle_Is_Unsigned()
    {
        var (bundles, audit, accessor) = DefaultMocks(
            Actor(isAuthenticated: true, isSystemAdmin: true));
        var bundle = DraftBundle();
        bundles.Setup(r => r.GetByIdAsync(bundle.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(bundle);

        var handler = new ApprovePolicyBundleCommandHandler(
            bundles.Object, audit.Object, SignatureService(), accessor.Object,
            NullLogger<ApprovePolicyBundleCommandHandler>.Instance);

        var act = () => handler.Handle(
            new ApprovePolicyBundleCommand { BundleId = bundle.Id }, CancellationToken.None);

        await act.Should().ThrowAsync<PolicyBundleSignatureException>()
            .WithMessage("*cannot be approved*");
        bundles.Verify(r => r.UpdateAsync(It.IsAny<PolicyBundle>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Approve_Fails_Closed_When_Signature_No_Longer_Verifies()
    {
        var signatureService = SignatureService();
        var (bundles, audit, accessor) = DefaultMocks(
            Actor(isAuthenticated: true, isSystemAdmin: true));
        var bundle = DraftBundle();
        signatureService.SignBundle(bundle, Guid.NewGuid());
        // Tamper after signing.
        bundle.PolicyData = """[{"policyName":"p1","requiredPermissions":["admin:*"]}]""";
        bundles.Setup(r => r.GetByIdAsync(bundle.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(bundle);

        var handler = new ApprovePolicyBundleCommandHandler(
            bundles.Object, audit.Object, signatureService, accessor.Object,
            NullLogger<ApprovePolicyBundleCommandHandler>.Instance);

        var act = () => handler.Handle(
            new ApprovePolicyBundleCommand { BundleId = bundle.Id }, CancellationToken.None);

        await act.Should().ThrowAsync<PolicyBundleSignatureException>();
        bundles.Verify(r => r.UpdateAsync(It.IsAny<PolicyBundle>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Deploy_Fails_Closed_Through_The_SignedStore_For_Unsigned_Bundles()
    {
        var signatureService = SignatureService();
        var (bundles, audit, accessor) = DefaultMocks(
            Actor(isAuthenticated: true, isSystemAdmin: true));
        var bundle = DraftBundle(b => b.Status = PolicyBundleStatus.Approved);
        bundles.Setup(r => r.GetByIdAsync(bundle.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(bundle);

        var signedStore = new SignedPolicyBundleStore(
            bundles.Object, signatureService, NullLogger<SignedPolicyBundleStore>.Instance);
        var deployments = new Mock<IPolicyBundleDeploymentRepository>();
        var materializer = new Mock<IPolicyBundlePolicyMaterializer>();
        var versions = new Mock<ITenantSecurityVersionStore>();

        var handler = new DeployPolicyBundleCommandHandler(
            bundles.Object, deployments.Object, audit.Object, signedStore,
            materializer.Object, versions.Object, accessor.Object,
            NullLogger<DeployPolicyBundleCommandHandler>.Instance);

        var act = () => handler.Handle(
            new DeployPolicyBundleCommand { BundleId = bundle.Id }, CancellationToken.None);

        await act.Should().ThrowAsync<PolicyBundleSignatureException>()
            .WithMessage("*no signature*");
        materializer.VerifyNoOtherCalls();
        deployments.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Deploy_Rejects_Signed_But_Unapproved_Bundles()
    {
        var signatureService = SignatureService();
        var (bundles, audit, accessor) = DefaultMocks(
            Actor(isAuthenticated: true, isSystemAdmin: true));
        var bundle = DraftBundle(); // still Draft after signing
        signatureService.SignBundle(bundle, Guid.NewGuid());
        bundles.Setup(r => r.GetByIdAsync(bundle.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(bundle);

        var signedStore = new SignedPolicyBundleStore(
            bundles.Object, signatureService, NullLogger<SignedPolicyBundleStore>.Instance);
        var deployments = new Mock<IPolicyBundleDeploymentRepository>();
        var materializer = new Mock<IPolicyBundlePolicyMaterializer>();
        var versions = new Mock<ITenantSecurityVersionStore>();

        var handler = new DeployPolicyBundleCommandHandler(
            bundles.Object, deployments.Object, audit.Object, signedStore,
            materializer.Object, versions.Object, accessor.Object,
            NullLogger<DeployPolicyBundleCommandHandler>.Instance);

        var act = () => handler.Handle(
            new DeployPolicyBundleCommand { BundleId = bundle.Id }, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*only approved bundles can be deployed*");
        materializer.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Deploy_Materializes_Records_Deployment_Activates_And_Bumps_Versions()
    {
        var tenantId = Guid.NewGuid();
        var signatureService = SignatureService();
        var (bundles, audit, accessor) = DefaultMocks(
            Actor(isAuthenticated: true, isSystemAdmin: true));
        var bundle = DraftBundle(b =>
        {
            b.IsGlobal = false;
            b.TenantId = new TenantId(tenantId);
            b.Status = PolicyBundleStatus.Approved;
        });
        signatureService.SignBundle(bundle, Guid.NewGuid());
        bundles.Setup(r => r.GetByIdAsync(bundle.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(bundle);

        var signedStore = new SignedPolicyBundleStore(
            bundles.Object, signatureService, NullLogger<SignedPolicyBundleStore>.Instance);
        var deployments = new Mock<IPolicyBundleDeploymentRepository>();
        var materializer = new Mock<IPolicyBundlePolicyMaterializer>();
        materializer
            .Setup(m => m.MaterializeAsync(bundle, It.IsAny<CancellationToken>()))
            .ReturnsAsync(2);
        var versions = new Mock<ITenantSecurityVersionStore>();

        var handler = new DeployPolicyBundleCommandHandler(
            bundles.Object, deployments.Object, audit.Object, signedStore,
            materializer.Object, versions.Object, accessor.Object,
            NullLogger<DeployPolicyBundleCommandHandler>.Instance);

        var deploymentId = await handler.Handle(
            new DeployPolicyBundleCommand { BundleId = bundle.Id, Environment = "Staging" },
            CancellationToken.None);

        deploymentId.Should().NotBe(Guid.Empty);
        bundle.Status.Should().Be(PolicyBundleStatus.Active);
        bundle.DeploymentCount.Should().Be(1);

        deployments.Verify(d => d.CreateAsync(
            It.Is<PolicyBundleDeployment>(dep =>
                dep.BundleId == bundle.Id
                && dep.Environment == "Staging"
                && dep.Status == PolicyDeploymentStatus.Active
                && dep.VerificationPassed),
            It.IsAny<CancellationToken>()), Times.Once);

        // Tenant bundles bump both the tenant version and the shared global version.
        versions.Verify(v => v.IncrementVersionAsync(tenantId.ToString(), It.IsAny<CancellationToken>()), Times.Once);
        versions.Verify(v => v.IncrementVersionAsync(Guid.Empty.ToString(), It.IsAny<CancellationToken>()), Times.Once);

        audit.Verify(a => a.CreateAsync(
            It.Is<PolicyRegistryAuditLog>(l => l.Action == PolicyRegistryAction.Deploy),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Create_By_TenantAdmin_For_Foreign_Tenant_Is_Denied()
    {
        var (bundles, audit, accessor) = DefaultMocks(
            Actor(isAuthenticated: true, isTenantAdmin: true, tenantId: Guid.NewGuid()));

        var handler = new CreatePolicyBundleCommandHandler(
            bundles.Object, audit.Object, SignatureService(), accessor.Object,
            NullLogger<CreatePolicyBundleCommandHandler>.Instance);

        var act = () => handler.Handle(
            new CreatePolicyBundleCommand
            {
                Name = "x",
                Version = "1.0.0",
                TenantId = Guid.NewGuid(), // different tenant
                PolicyData = "[]"
            },
            CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
        bundles.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Create_By_TenantAdmin_For_Global_Scope_Is_Denied()
    {
        var (bundles, audit, accessor) = DefaultMocks(
            Actor(isAuthenticated: true, isTenantAdmin: true, tenantId: Guid.NewGuid()));

        var handler = new CreatePolicyBundleCommandHandler(
            bundles.Object, audit.Object, SignatureService(), accessor.Object,
            NullLogger<CreatePolicyBundleCommandHandler>.Instance);

        var act = () => handler.Handle(
            new CreatePolicyBundleCommand
            {
                Name = "x",
                Version = "1.0.0",
                TenantId = null, // global scope
                PolicyData = "[]"
            },
            CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("*Global policy bundle create requires system administration*");
        bundles.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Create_By_TenantAdmin_For_Own_Tenant_Persists_Draft_And_Audits()
    {
        var tenantId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        var (bundles, audit, accessor) = DefaultMocks(
            Actor(isAuthenticated: true, isTenantAdmin: true, tenantId: tenantId, subjectId: actorId));

        var handler = new CreatePolicyBundleCommandHandler(
            bundles.Object, audit.Object, SignatureService(), accessor.Object,
            NullLogger<CreatePolicyBundleCommandHandler>.Instance);

        var bundleId = await handler.Handle(
            new CreatePolicyBundleCommand
            {
                Name = "tenant-baseline",
                Version = "1.0.0",
                TenantId = tenantId,
                PolicyData = """[{"policyName":"p1","requiredPermissions":["content:read"]}]"""
            },
            CancellationToken.None);

        bundleId.Should().NotBe(Guid.Empty);
        bundles.Verify(r => r.CreateAsync(
            It.Is<PolicyBundle>(b =>
                b.Id == bundleId
                && b.Name == "tenant-baseline"
                && b.TenantId!.Value.Equals(tenantId)
                && !b.IsGlobal
                && b.Status == PolicyBundleStatus.Draft
                && b.CreatedBy == actorId),
            It.IsAny<CancellationToken>()), Times.Once);
        audit.Verify(a => a.CreateAsync(
            It.Is<PolicyRegistryAuditLog>(l =>
                l.BundleId == bundleId
                && l.Action == PolicyRegistryAction.Create
                && l.PerformedBy == actorId),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Create_Fails_Closed_Without_Persisting_When_Inputs_Violate_The_Contract()
    {
        var tenantId = Guid.NewGuid();
        var (bundles, audit, accessor) = DefaultMocks(
            Actor(isAuthenticated: true, isTenantAdmin: true, tenantId: tenantId));

        var handler = new CreatePolicyBundleCommandHandler(
            bundles.Object, audit.Object, SignatureService(), accessor.Object,
            NullLogger<CreatePolicyBundleCommandHandler>.Instance);

        var act = () => handler.Handle(
            new CreatePolicyBundleCommand
            {
                Name = "bad",
                Version = "1.0.0",
                TenantId = tenantId,
                PolicyData = "{not-json"
            },
            CancellationToken.None);

        await act.Should().ThrowAsync<PolicyBundleSignatureException>();
        bundles.Verify(r => r.CreateAsync(It.IsAny<PolicyBundle>(), It.IsAny<CancellationToken>()), Times.Never);
        audit.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task List_By_TenantAdmin_Is_Constrained_To_The_Own_Tenant_Scope()
    {
        var tenantId = Guid.NewGuid();
        var (bundles, audit, accessor) = DefaultMocks(
            Actor(isAuthenticated: true, isTenantAdmin: true, tenantId: tenantId));
        bundles.Setup(r => r.GetByTenantAsync(tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PolicyBundle>
            {
                DraftBundle(b =>
                {
                    b.IsGlobal = false;
                    b.TenantId = new TenantId(tenantId);
                    b.SignedBy = "key-1";
                    b.SignedAt = SystemClock.UtcNow;
                    b.DigitalSignature = """{"version":"1"}""";
                })
            });

        var handler = new ListPolicyBundlesQueryHandler(
            bundles.Object, accessor.Object, NullLogger<ListPolicyBundlesQueryHandler>.Instance);

        var summaries = await handler.Handle(
            new ListPolicyBundlesQuery { TenantId = tenantId }, CancellationToken.None);

        var summary = summaries.Should().ContainSingle().Subject;
        summary.TenantId.Should().Be(tenantId);
        summary.IsGlobal.Should().BeFalse();
        summary.Status.Should().Be(PolicyBundleStatus.Draft);
        summary.IsSigned.Should().BeTrue();
        summary.SignedBy.Should().Be("key-1");
        bundles.Verify(r => r.GetByTenantAsync(tenantId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task List_By_TenantAdmin_For_Foreign_Scope_Is_Denied()
    {
        var (bundles, audit, accessor) = DefaultMocks(
            Actor(isAuthenticated: true, isTenantAdmin: true, tenantId: Guid.NewGuid()));

        var handler = new ListPolicyBundlesQueryHandler(
            bundles.Object, accessor.Object, NullLogger<ListPolicyBundlesQueryHandler>.Instance);

        var act = () => handler.Handle(
            new ListPolicyBundlesQuery { TenantId = Guid.NewGuid() }, CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("*Listing policy bundles requires tenant administration*");
        bundles.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task List_By_SystemAdmin_Can_Inspect_The_Global_Registry()
    {
        var (bundles, audit, accessor) = DefaultMocks(
            Actor(isAuthenticated: true, isSystemAdmin: true));
        bundles.Setup(r => r.GetByTenantAsync(null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PolicyBundle>());

        var handler = new ListPolicyBundlesQueryHandler(
            bundles.Object, accessor.Object, NullLogger<ListPolicyBundlesQueryHandler>.Instance);

        var summaries = await handler.Handle(
            new ListPolicyBundlesQuery { TenantId = null }, CancellationToken.None);

        summaries.Should().BeEmpty();
        bundles.Verify(r => r.GetByTenantAsync(null, It.IsAny<CancellationToken>()), Times.Once);
    }
}
