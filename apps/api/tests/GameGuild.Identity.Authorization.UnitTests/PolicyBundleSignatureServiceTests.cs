using System.Security.Cryptography;
using FluentAssertions;
using GameGuild.CQRS.Models;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace GameGuild.Identity.Authorization.UnitTests;

/// <summary>
///     Focused tests for the ECDSA P-256 policy bundle signature service:
///     signing contract, trusted-key rotation/revocation and fail-closed verification.
/// </summary>
public class PolicyBundleSignatureServiceTests
{
    private static (string PublicPem, string PrivatePem) GenerateKeyPair()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        return (key.ExportSubjectPublicKeyInfoPem(), key.ExportPkcs8PrivateKeyPem());
    }

    private static PolicyBundleSigningOptions BuildOptions(
        string activeKeyId = "key-2026-01",
        Action<PolicyBundleSigningOptions>? mutate = null)
    {
        var (publicPem, privatePem) = GenerateKeyPair();
        var options = new PolicyBundleSigningOptions
        {
            TrustedKeys =
            [
                new TrustedPolicySigningKey
                {
                    KeyId = activeKeyId,
                    PublicKeyPem = publicPem
                }
            ],
            ActiveKey = new PolicyBundleSigningKeyOptions
            {
                KeyId = activeKeyId,
                PrivateKeyPem = privatePem
            }
        };
        mutate?.Invoke(options);
        return options;
    }

    private static PolicyBundle BuildValidBundle(Action<PolicyBundle>? mutate = null)
    {
        var bundle = new PolicyBundle
        {
            Name = "tenant-baseline",
            Version = "1.0.0",
            TenantId = new TenantId(Guid.NewGuid()),
            IsGlobal = false,
            BundleType = PolicyBundleType.Composite,
            PolicyData = """[{"policyName":"baseline.read","requiredPermissions":["content:read"]}]""",
            Metadata = """{"source":"unit-test"}""",
            CreatedBy = Guid.NewGuid()
        };
        mutate?.Invoke(bundle);
        return bundle;
    }

    private static PolicyBundleSignatureService CreateService(PolicyBundleSigningOptions options) =>
        new(
            Options.Create(options),
            NullLogger<PolicyBundleSignatureService>.Instance);

    [Fact]
    public void Sign_And_Verify_RoundTrip_Succeeds()
    {
        var options = BuildOptions();
        var service = CreateService(options);
        var bundle = BuildValidBundle();

        service.SignBundle(bundle, signerUserId: Guid.NewGuid());

        bundle.DigitalSignature.Should().NotBeNullOrWhiteSpace();
        bundle.SignedBy.Should().Be("key-2026-01");
        bundle.ContentHash.Should().HaveLength(64);

        var result = service.VerifyBundle(bundle);
        result.IsValid.Should().BeTrue(result.Reason);
        result.KeyId.Should().Be("key-2026-01");
        result.ContentHash.Should().Be(bundle.ContentHash);
    }

    [Fact]
    public void Verify_FailsClosed_When_PolicyData_Was_Tampered_After_Signing()
    {
        var service = CreateService(BuildOptions());
        var bundle = BuildValidBundle();
        service.SignBundle(bundle, Guid.NewGuid());

        bundle.PolicyData = """[{"policyName":"baseline.read","requiredPermissions":["admin:*"]}]""";

        var result = service.VerifyBundle(bundle);
        result.IsValid.Should().BeFalse();
        result.Reason.Should().Contain("Content hash mismatch");
    }

    [Fact]
    public void Verify_FailsClosed_When_Signature_Key_Is_Not_Trusted()
    {
        var options = BuildOptions();
        var service = CreateService(options);
        var bundle = BuildValidBundle();
        service.SignBundle(bundle, Guid.NewGuid());

        // Replace the trusted registry after signing: the recorded key must no longer verify.
        options.TrustedKeys.Clear();
        options.TrustedKeys.Add(new TrustedPolicySigningKey
        {
            KeyId = "other-key",
            PublicKeyPem = GenerateKeyPair().PublicPem
        });

        var result = service.VerifyBundle(bundle);
        result.IsValid.Should().BeFalse();
        result.Reason.Should().Contain("not trusted");
    }

    [Fact]
    public void Verify_FailsClosed_When_Trusted_Key_Was_Revoked()
    {
        var options = BuildOptions();
        var service = CreateService(options);
        var bundle = BuildValidBundle();
        service.SignBundle(bundle, Guid.NewGuid());

        // Revoke the trusted key only after signing: existing signatures must stop verifying.
        options.TrustedKeys[0] = options.TrustedKeys[0] with { RevokedAt = DateTimeOffset.UtcNow.AddMinutes(-1) };

        var result = service.VerifyBundle(bundle);
        result.IsValid.Should().BeFalse();
        result.Reason.Should().Contain("revoked");
    }

    [Fact]
    public void Sign_FailsClosed_When_Active_Key_Is_Not_Yet_Valid()
    {
        var options = BuildOptions(mutate: o =>
            o.TrustedKeys[0] = o.TrustedKeys[0] with { NotBefore = DateTimeOffset.UtcNow.AddHours(1) });

        var service = CreateService(options);
        var bundle = BuildValidBundle();

        // Signing itself must fail closed: the active key is outside its validity window.
        var signAction = () => service.SignBundle(bundle, Guid.NewGuid());
        signAction.Should().Throw<PolicyBundleSignatureException>()
            .WithMessage("*expired or revoked*");
    }

    [Fact]
    public void Sign_FailsClosed_When_Active_Private_Key_Does_Not_Match_Any_Trusted_Public_Key()
    {
        var options = BuildOptions();
        var (otherPublic, otherPrivate) = GenerateKeyPair();
        options.ActiveKey = new PolicyBundleSigningKeyOptions { KeyId = "key-2026-01", PrivateKeyPem = otherPrivate };
        options.TrustedKeys[0] = options.TrustedKeys[0] with { PublicKeyPem = GenerateKeyPair().PublicPem };
        _ = otherPublic;

        var service = CreateService(options);
        var bundle = BuildValidBundle();

        var signAction = () => service.SignBundle(bundle, Guid.NewGuid());
        signAction.Should().Throw<PolicyBundleSignatureException>()
            .WithMessage("*does not match any trusted public key*");
    }

    [Fact]
    public void Sign_FailsClosed_When_No_Active_Key_Is_Configured()
    {
        var options = BuildOptions();
        options.ActiveKey = null;

        var service = CreateService(options);
        var signAction = () => service.SignBundle(BuildValidBundle(), Guid.NewGuid());
        signAction.Should().Throw<PolicyBundleSignatureException>()
            .WithMessage("*No active policy bundle signing key*");
    }

    [Fact]
    public void Sign_FailsClosed_When_PolicyData_Is_Not_Valid_Json()
    {
        var service = CreateService(BuildOptions());
        var bundle = BuildValidBundle(b => b.PolicyData = "{not-json");

        var signAction = () => service.SignBundle(bundle, Guid.NewGuid());
        signAction.Should().Throw<PolicyBundleSignatureException>()
            .WithMessage("*PolicyData*");
    }

    [Fact]
    public void Sign_FailsClosed_When_Json_Contains_Duplicate_Object_Properties()
    {
        var service = CreateService(BuildOptions());
        var bundle = BuildValidBundle(b => b.Metadata = """{"a":1,"a":2}""");

        var signAction = () => service.SignBundle(bundle, Guid.NewGuid());
        signAction.Should().Throw<PolicyBundleSignatureException>()
            .WithMessage("*duplicate*");
    }

    [Fact]
    public void Sign_FailsClosed_When_Scope_Is_Inconsistent_Global_With_Tenant()
    {
        var service = CreateService(BuildOptions());
        var bundle = BuildValidBundle(b => b.IsGlobal = true);

        var signAction = () => service.SignBundle(bundle, Guid.NewGuid());
        signAction.Should().Throw<PolicyBundleSignatureException>()
            .WithMessage("*global bundle must not carry a tenant id*");
    }

    [Fact]
    public void Sign_FailsClosed_When_Scope_Is_Inconsistent_Tenant_Without_TenantId()
    {
        var service = CreateService(BuildOptions());
        var bundle = BuildValidBundle(b =>
        {
            b.TenantId = null;
            b.IsGlobal = false;
        });

        var signAction = () => service.SignBundle(bundle, Guid.NewGuid());
        signAction.Should().Throw<PolicyBundleSignatureException>()
            .WithMessage("*tenant-scoped bundle must carry a tenant id*");
    }

    [Fact]
    public void Sign_FailsClosed_When_EffectiveDates_Are_Inverted()
    {
        var service = CreateService(BuildOptions());
        var bundle = BuildValidBundle(b =>
        {
            b.EffectiveFrom = DateTime.UtcNow.AddDays(10);
            b.EffectiveUntil = DateTime.UtcNow.AddDays(1);
        });

        var signAction = () => service.SignBundle(bundle, Guid.NewGuid());
        signAction.Should().Throw<PolicyBundleSignatureException>()
            .WithMessage("*EffectiveFrom*");
    }

    [Fact]
    public void Sign_FailsClosed_When_Combined_Size_Exceeds_The_Configured_Limit()
    {
        var options = BuildOptions(mutate: o => o.MaxBundleSizeBytes = 128);
        var service = CreateService(options);

        var bundle = BuildValidBundle(b => b.PolicyData = "[" + new string('x', 300) + "]");

        var signAction = () => service.SignBundle(bundle, Guid.NewGuid());
        signAction.Should().Throw<PolicyBundleSignatureException>()
            .WithMessage("*exceeds the limit*");
    }

    [Fact]
    public void Verify_FailsClosed_When_Bundle_Is_Unsigned()
    {
        var service = CreateService(BuildOptions());
        var result = service.VerifyBundle(BuildValidBundle());

        result.IsValid.Should().BeFalse();
        result.Reason.Should().Contain("no signature");
    }

    [Fact]
    public void Verify_FailsClosed_When_Envelope_Version_Is_Unknown()
    {
        var service = CreateService(BuildOptions());
        var bundle = BuildValidBundle();
        service.SignBundle(bundle, Guid.NewGuid());

        // Tamper the envelope version; the unknown version must be rejected before any
        // hash or signature comparison runs.
        bundle.DigitalSignature = bundle.DigitalSignature!.Replace("\"version\":\"1\"", "\"version\":\"99\"");

        var result = service.VerifyBundle(bundle);
        result.IsValid.Should().BeFalse();
        result.Reason.Should().Contain("Unsupported signature envelope version");
    }

    [Fact]
    public void Verify_Supports_Key_Rotation_By_Verifying_With_The_Key_Recorded_In_The_Envelope()
    {
        var (rotatedPublic, rotatedPrivate) = GenerateKeyPair();
        var options = BuildOptions();
        var bundle = BuildValidBundle();

        // Sign with the original key, then rotate: new trusted key + new active key.
        var originalService = CreateService(options);
        originalService.SignBundle(bundle, Guid.NewGuid());

        options.TrustedKeys.Add(new TrustedPolicySigningKey
        {
            KeyId = "key-2026-02",
            PublicKeyPem = rotatedPublic
        });
        options.ActiveKey = new PolicyBundleSigningKeyOptions { KeyId = "key-2026-02", PrivateKeyPem = rotatedPrivate };

        var rotatedService = CreateService(options);
        rotatedService.VerifyBundle(bundle).IsValid.Should().BeTrue("the old key remains trusted and unrevoked");

        // New signatures are produced with the rotated key.
        var next = BuildValidBundle(b => b.Version = "1.1.0");
        rotatedService.SignBundle(next, Guid.NewGuid());
        next.SignedBy.Should().Be("key-2026-02");
        rotatedService.VerifyBundle(next).IsValid.Should().BeTrue();
    }

    [Fact]
    public void Verify_Allows_Explicit_Disable_For_Local_Development()
    {
        var options = BuildOptions(mutate: o => o.EnforceSignatureVerification = false);
        var service = CreateService(options);

        var result = service.VerifyBundle(BuildValidBundle());
        result.IsValid.Should().BeTrue();
        result.Reason.Should().Be("signature-enforcement-disabled");
    }

    [Fact]
    public void ValidateBundleInputs_Returns_All_Errors()
    {
        var service = CreateService(BuildOptions());
        var bundle = BuildValidBundle(b =>
        {
            b.Name = " ";
            b.Version = "";
            b.PolicyData = "{bad";
            b.IsGlobal = true; // inconsistent with the tenant id
            b.CreatedBy = Guid.Empty;
        });

        var errors = service.ValidateBundleInputs(bundle);
        errors.Should().Contain(e => e.Contains("name", StringComparison.OrdinalIgnoreCase));
        errors.Should().Contain(e => e.Contains("version", StringComparison.OrdinalIgnoreCase));
        errors.Should().Contain(e => e.Contains("PolicyData"));
        errors.Should().Contain(e => e.Contains("tenant id"));
        errors.Should().Contain(e => e.Contains("CreatedBy"));
    }
}
