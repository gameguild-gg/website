using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Xunit;

namespace GameGuild.Commerce.Billing.UnitTests.Services;

public class GooglePayWebhookVerificationServiceTests : IDisposable
{
    private readonly RSA _providerKey = RSA.Create(2048);
    private readonly RSA _otherKey = RSA.Create(2048);

    [Fact]
    public void Verify_Accepts_A_Validly_Signed_Fresh_JWT()
    {
        var settings = Settings(verificationKeys: true);
        var service = new GooglePayWebhookVerificationService(Options.Create(settings));
        var token = CreateSignedToken(_providerKey, audience: "gameguild-project");

        var result = service.Verify("""{"event_id":"evt-1","event_type":"SUBSCRIPTION_PURCHASED"}""", $"Bearer {token}", "gameguild-project");

        result.IsValid.Should().BeTrue();
        result.EventId.Should().Be("evt-1");
        result.EventType.Should().Be("SUBSCRIPTION_PURCHASED");
        result.Audience.Should().Be("gameguild-project");
    }

    [Fact]
    public void Verify_Rejects_A_JWT_Signed_By_An_Unknown_Key()
    {
        var settings = Settings(verificationKeys: true);
        var service = new GooglePayWebhookVerificationService(Options.Create(settings));
        var token = CreateSignedToken(_otherKey, audience: "gameguild-project");

        var result = service.Verify("""{"event_id":"evt-1"}""", $"Bearer {token}", "gameguild-project");

        result.IsValid.Should().BeFalse();
        result.ErrorMessage.Should().Contain("signature");
    }

    [Fact]
    public void Verify_Fails_Closed_When_No_Key_Is_Configured()
    {
        var settings = Settings(verificationKeys: false);
        var service = new GooglePayWebhookVerificationService(Options.Create(settings));
        var token = CreateSignedToken(_providerKey, audience: "gameguild-project");

        var result = service.Verify("""{"event_id":"evt-1"}""", $"Bearer {token}", "gameguild-project");

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Verify_Rejects_A_Mismatched_Project_Identifier()
    {
        var settings = Settings(verificationKeys: true);
        var service = new GooglePayWebhookVerificationService(Options.Create(settings));
        var token = CreateSignedToken(_providerKey, audience: "gameguild-project");

        var result = service.Verify("""{"event_id":"evt-1"}""", $"Bearer {token}", "another-project");

        result.IsValid.Should().BeFalse();
        result.ErrorMessage.Should().Contain("project");
    }

    [Fact]
    public void Verify_Rejects_A_Mismatched_Audience()
    {
        var settings = Settings(verificationKeys: true);
        var service = new GooglePayWebhookVerificationService(Options.Create(settings));
        var token = CreateSignedToken(_providerKey, audience: "somebody-else");

        var result = service.Verify("""{"event_id":"evt-1"}""", $"Bearer {token}", "gameguild-project");

        result.IsValid.Should().BeFalse();
        result.ErrorMessage.Should().Contain("audience");
    }

    [Fact]
    public void Verify_Rejects_An_Expired_JWT_Outside_The_Tolerance_Window()
    {
        var settings = Settings(verificationKeys: true);
        var service = new GooglePayWebhookVerificationService(Options.Create(settings));
        var token = CreateSignedToken(_providerKey, audience: "gameguild-project", expired: true);

        var result = service.Verify("""{"event_id":"evt-1"}""", $"Bearer {token}", "gameguild-project");

        result.IsValid.Should().BeFalse();
        result.ErrorMessage.Should().Contain("expired");
    }

    [Fact]
    public void Verify_Rejects_A_Malformed_Bearer_Token()
    {
        var settings = Settings(verificationKeys: true);
        var service = new GooglePayWebhookVerificationService(Options.Create(settings));

        var result = service.Verify("""{"event_id":"evt-1"}""", "Bearer not-a-jwt", "gameguild-project");

        result.IsValid.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Malformed");
    }

    [Fact]
    public void Verify_Rejects_A_Missing_Bearer_Prefix()
    {
        var settings = Settings(verificationKeys: true);
        var service = new GooglePayWebhookVerificationService(Options.Create(settings));
        var token = CreateSignedToken(_providerKey, audience: "gameguild-project");

        var result = service.Verify("""{"event_id":"evt-1"}""", token, "gameguild-project");

        result.IsValid.Should().BeFalse();
        result.ErrorMessage.Should().Contain("bearer");
    }

    [Fact]
    public void Verify_Rejects_Payloads_Without_An_Event_Identifier()
    {
        var settings = Settings(verificationKeys: true);
        var service = new GooglePayWebhookVerificationService(Options.Create(settings));
        var token = CreateSignedToken(_providerKey, audience: "gameguild-project");

        var result = service.Verify("""{"event_type":"SUBSCRIPTION_PURCHASED"}""", $"Bearer {token}", "gameguild-project");

        result.IsValid.Should().BeFalse();
        result.ErrorMessage.Should().Contain("event identifier");
    }

    [Fact]
    public void Verify_Fails_Closed_When_Not_Configured()
    {
        var settings = new BillingConfiguration
        {
            GooglePay = new GooglePaySettings() // ProjectId empty
        };
        var service = new GooglePayWebhookVerificationService(Options.Create(settings));

        var result = service.Verify("{}", "Bearer x", "gameguild-project");

        result.IsValid.Should().BeFalse();
        result.ErrorMessage.Should().Contain("not configured");
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void IsValidVerificationKey_Accepts_Pem_And_Base64_Spki(bool pem)
    {
        var key = pem ? _providerKey.ExportSubjectPublicKeyInfoPem() : Convert.ToBase64String(_providerKey.ExportSubjectPublicKeyInfo());

        GooglePayWebhookVerificationService.IsValidVerificationKey(key).Should().BeTrue();
    }

    [Theory]
    [InlineData("not a key")]
    [InlineData("")]
    public void IsValidVerificationKey_Rejects_Garbage(string candidate)
    {
        GooglePayWebhookVerificationService.IsValidVerificationKey(candidate).Should().BeFalse();
    }

    private BillingConfiguration Settings(bool verificationKeys) => new()
    {
        GooglePay = new GooglePaySettings
        {
            ProjectId = "gameguild-project",
            VerificationKeys = verificationKeys
                ? [_providerKey.ExportSubjectPublicKeyInfoPem()]
                : []
        }
    };

    private static string CreateSignedToken(RSA key, string audience, bool expired = false)
    {
        var now = DateTimeOffset.UtcNow;
        var header = JsonSerializer.Serialize(new { alg = "RS256", typ = "JWT" });
        var payload = JsonSerializer.Serialize(new
        {
            aud = audience,
            iat = now.ToUnixTimeSeconds(),
            nbf = now.ToUnixTimeSeconds(),
            exp = (now + (expired ? TimeSpan.FromHours(-2) : TimeSpan.FromMinutes(5))).ToUnixTimeSeconds()
        });

        var encodedHeader = Base64Url(Encoding.UTF8.GetBytes(header));
        var encodedPayload = Base64Url(Encoding.UTF8.GetBytes(payload));
        var signature = Base64Url(key.SignData(
            Encoding.ASCII.GetBytes($"{encodedHeader}.{encodedPayload}"),
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1));

        return $"{encodedHeader}.{encodedPayload}.{signature}";
    }

    private static string Base64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    public void Dispose()
    {
        _providerKey.Dispose();
        _otherKey.Dispose();
    }
}
