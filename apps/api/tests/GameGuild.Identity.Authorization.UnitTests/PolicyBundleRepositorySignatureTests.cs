using FluentAssertions;
using GameGuild.CQRS.Models;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace GameGuild.Identity.Authorization.UnitTests;

/// <summary>
///     Fail-closed behaviour of the signed policy bundle store (published-bundle reads) and the
///     policy materializer that connects verified bundles to the dynamic authorization policy provider.
/// </summary>
public class PolicyBundleRepositorySignatureTests
{
    private static (string PublicPem, string PrivatePem) GenerateKeyPair()
    {
        using var key = System.Security.Cryptography.ECDsa.Create(
            System.Security.Cryptography.ECCurve.NamedCurves.nistP256);
        return (key.ExportSubjectPublicKeyInfoPem(), key.ExportPkcs8PrivateKeyPem());
    }

    private static PolicyBundleSignatureService SignatureService()
    {
        var (publicPem, privatePem) = GenerateKeyPair();
        return new PolicyBundleSignatureService(
            Options.Create(new PolicyBundleSigningOptions
            {
                TrustedKeys = [new TrustedPolicySigningKey { KeyId = "key-1", PublicKeyPem = publicPem }],
                ActiveKey = new PolicyBundleSigningKeyOptions { KeyId = "key-1", PrivateKeyPem = privatePem }
            }),
            NullLogger<PolicyBundleSignatureService>.Instance);
    }

    private static PolicyBundle ActiveBundle(Action<PolicyBundle>? mutate = null)
    {
        var bundle = new PolicyBundle
        {
            Name = "baseline",
            Version = "1.0.0",
            IsGlobal = true,
            BundleType = PolicyBundleType.Composite,
            PolicyData = """[{"policyName":"p1","requiredPermissions":["content:read"]}]""",
            Status = PolicyBundleStatus.Active,
            CreatedBy = Guid.NewGuid()
        };
        mutate?.Invoke(bundle);
        return bundle;
    }

    private static SignedPolicyBundleStore CreateStore(
        Mock<IPolicyBundleRepository> bundles,
        PolicyBundleSignatureService? signatureService = null) =>
        new(
            bundles.Object,
            signatureService ?? SignatureService(),
            NullLogger<SignedPolicyBundleStore>.Instance);

    [Fact]
    public async Task PublishedRead_FailsClosed_When_Active_Bundle_Has_No_Signature()
    {
        var bundle = ActiveBundle();
        var bundles = new Mock<IPolicyBundleRepository>();
        bundles
            .Setup(r => r.GetPublishedByNameAsync(bundle.Name, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(bundle);

        var store = CreateStore(bundles);

        var act = () => store.GetVerifiedPublishedBundleAsync(bundle.Name, null);
        await act.Should().ThrowAsync<PolicyBundleSignatureException>()
            .WithMessage("*invalid signature*");
    }

    [Fact]
    public async Task PublishedRead_FailsClosed_When_Active_Bundle_Content_Was_Tampered()
    {
        var service = SignatureService();
        var bundle = ActiveBundle();
        service.SignBundle(bundle, Guid.NewGuid());
        // Tamper post-signing: content hash no longer matches the envelope.
        bundle.PolicyData = """[{"policyName":"p1","requiredPermissions":["admin:*"]}]""";

        var bundles = new Mock<IPolicyBundleRepository>();
        bundles
            .Setup(r => r.GetPublishedByNameAsync(bundle.Name, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(bundle);

        var store = CreateStore(bundles, service);

        var act = () => store.GetVerifiedPublishedBundleAsync(bundle.Name, null);
        await act.Should().ThrowAsync<PolicyBundleSignatureException>()
            .WithMessage("*Content hash mismatch*");
    }

    [Fact]
    public async Task PublishedRead_FailsClosed_When_Nothing_Was_Published()
    {
        var bundles = new Mock<IPolicyBundleRepository>();
        bundles
            .Setup(r => r.GetPublishedByNameAsync("missing", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PolicyBundle?)null);

        var store = CreateStore(bundles);

        var act = () => store.GetVerifiedPublishedBundleAsync("missing", null);
        await act.Should().ThrowAsync<PolicyBundleSignatureException>()
            .WithMessage("*No published policy bundle*");
    }

    [Fact]
    public async Task PublishedRead_Returns_The_Bundle_When_The_Signature_Verifies()
    {
        var service = SignatureService();
        var tenantId = Guid.NewGuid();
        var bundle = ActiveBundle(b =>
        {
            b.IsGlobal = false;
            b.TenantId = new TenantId(tenantId);
        });
        service.SignBundle(bundle, Guid.NewGuid());

        var bundles = new Mock<IPolicyBundleRepository>();
        bundles
            .Setup(r => r.GetPublishedByNameAsync(bundle.Name, tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(bundle);

        var store = CreateStore(bundles, service);
        var loaded = await store.GetVerifiedPublishedBundleAsync(bundle.Name, tenantId);

        loaded.Id.Should().Be(bundle.Id);
    }

    [Fact]
    public async Task VerifiedBundleRead_Allows_Unsigned_Drafts_Only_When_Explicitly_Requested()
    {
        var service = SignatureService();
        var bundle = ActiveBundle(b => b.Status = PolicyBundleStatus.Draft);

        var bundles = new Mock<IPolicyBundleRepository>();
        bundles
            .Setup(r => r.GetByIdAsync(bundle.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(bundle);

        var store = CreateStore(bundles, service);

        var draft = await store.GetVerifiedBundleAsync(bundle.Id, allowUnsignedDraft: true);
        draft.Id.Should().Be(bundle.Id);

        var act = () => store.GetVerifiedBundleAsync(bundle.Id, allowUnsignedDraft: false);
        await act.Should().ThrowAsync<PolicyBundleSignatureException>()
            .WithMessage("*carries no signature*");
    }

    [Fact]
    public async Task Materializer_Upserts_PolicyDefinitions_With_Bundle_Marker_And_BumpedVersion()
    {
        var tenantId = Guid.NewGuid();
        var bundle = ActiveBundle(b =>
        {
            b.IsGlobal = false;
            b.TenantId = new TenantId(tenantId);
            b.PolicyData =
                """[{"policyName":"baseline.read","requiredPermissions":["content:read"],"requireAuthentication":true}]""";
        });

        var policies = new Mock<IPolicyDefinitionRepository>();
        policies
            .Setup(r => r.GetByNameAsync("baseline.read", tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PolicyDefinitionEntity?)null);

        var materializer = new PolicyBundlePolicyMaterializer(
            policies.Object, NullLogger<PolicyBundlePolicyMaterializer>.Instance);

        var written = await materializer.MaterializeAsync(bundle);

        written.Should().Be(1);
        policies.Verify(r => r.AddAsync(
            It.Is<PolicyDefinitionEntity>(e =>
                e.PolicyName == "baseline.read"
                && e.TenantId == tenantId
                && e.IsActive
                && e.Description == $"{PolicyBundlePolicyMaterializer.SourceMarkerPrefix}{bundle.Id:N}"
                && e.RequiredPermissionsJson.Contains("content:read")),
            It.IsAny<CancellationToken>()), Times.Once);
        policies.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Materializer_Bumps_Existing_Definition_Versions_For_Cache_Invalidation()
    {
        var bundle = ActiveBundle();
        var existing = new PolicyDefinitionEntity
        {
            PolicyName = "p1",
            TenantId = null,
            PolicyVersion = 7,
            Description = PolicyBundlePolicyMaterializer.SourceMarkerPrefix + bundle.Id.ToString("N")
        };

        var policies = new Mock<IPolicyDefinitionRepository>();
        policies
            .Setup(r => r.GetByNameAsync("p1", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        var materializer = new PolicyBundlePolicyMaterializer(
            policies.Object, NullLogger<PolicyBundlePolicyMaterializer>.Instance);

        await materializer.MaterializeAsync(bundle);

        existing.PolicyVersion.Should().Be(8, "redeploying a bundle must invalidate version-keyed policy caches");
        policies.Verify(r => r.UpdateAsync(existing, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Materializer_Rejects_Empty_PolicyData_FailClosed()
    {
        var bundle = ActiveBundle(b => b.PolicyData = "[]");
        var policies = new Mock<IPolicyDefinitionRepository>();

        var materializer = new PolicyBundlePolicyMaterializer(
            policies.Object, NullLogger<PolicyBundlePolicyMaterializer>.Instance);

        var act = () => materializer.MaterializeAsync(bundle);
        await act.Should().ThrowAsync<PolicyBundleSignatureException>()
            .WithMessage("*no policy definitions*");
        policies.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Dematerializer_Removes_Only_Definitions_Marked_With_The_Bundle()
    {
        var tenantId = Guid.NewGuid();
        var bundle = ActiveBundle(b =>
        {
            b.IsGlobal = false;
            b.TenantId = new TenantId(tenantId);
        });
        var marker = PolicyBundlePolicyMaterializer.SourceMarkerPrefix + bundle.Id.ToString("N");
        var owned = new PolicyDefinitionEntity { PolicyName = "owned", Description = marker };
        var foreign = new PolicyDefinitionEntity { PolicyName = "foreign", Description = "seeded" };

        var policies = new Mock<IPolicyDefinitionRepository>();
        policies
            .Setup(r => r.GetByTenantAsync(tenantId, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PolicyDefinitionEntity> { owned, foreign });

        var materializer = new PolicyBundlePolicyMaterializer(
            policies.Object, NullLogger<PolicyBundlePolicyMaterializer>.Instance);

        var removed = await materializer.DematerializeAsync(bundle);

        removed.Should().Be(1);
        policies.Verify(r => r.DeleteAsync(owned, It.IsAny<CancellationToken>()), Times.Once);
        policies.Verify(r => r.DeleteAsync(foreign, It.IsAny<CancellationToken>()), Times.Never);
        policies.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
