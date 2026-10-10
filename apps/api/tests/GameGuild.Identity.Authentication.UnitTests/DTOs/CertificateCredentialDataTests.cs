using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace GameGuild.Identity.Authentication.UnitTests.DTOs;

public sealed class CertificateCredentialDataTests
{
    private static JsonSerializerOptions CreateOptions()
    {
        // Mirror the API's contract JSON options (camelCase naming, case-insensitive
        // binding): the polymorphic converter discriminates on the camelCase "type"
        // property the platform serializer emits, so raw PascalCase options cannot
        // round-trip any credential DTO.
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true
        };
        options.Converters.Add(new PolymorphicCredentialConverter());
        return options;
    }

    [Fact]
    public void Type_IsCertificate()
    {
        new CertificateCredentialData().Type.Should().Be("certificate");
    }

    [Fact]
    public void Converter_DeserializesCertificateCredentialByExplicitType()
    {
        var credential = JsonSerializer.Deserialize<ICredentialData>(
            """{"type":"certificate","thumbprint":"ABCDEF0123456789ABCDEF0123456789ABCDEF01","spkiSha256":"aabbcc"}""",
            CreateOptions());

        var certificate = credential.Should().BeOfType<CertificateCredentialData>().Subject;
        certificate.Thumbprint.Should().Be("ABCDEF0123456789ABCDEF0123456789ABCDEF01");
        certificate.SpkiSha256.Should().Be("aabbcc");
    }

    [Fact]
    public void Converter_AutoDetectsCertificateCredentialFromThumbprintProperty()
    {
        var credential = JsonSerializer.Deserialize<ICredentialData>(
            """{"thumbprint":"ABCDEF0123456789ABCDEF0123456789ABCDEF01"}""",
            CreateOptions());

        credential.Should().BeOfType<CertificateCredentialData>();
    }

    [Fact]
    public void Converter_RoundTripsCertificateCredential()
    {
        var original = new CertificateCredentialData
        {
            Thumbprint = "0123456789ABCDEF0123456789ABCDEF01234567",
            SpkiSha256 = "EEFF"
        };

        var json = JsonSerializer.Serialize<ICredentialData>(original, CreateOptions());
        var parsed = JsonSerializer.Deserialize<ICredentialData>(json, CreateOptions());

        var certificate = parsed.Should().BeOfType<CertificateCredentialData>().Subject;
        certificate.Thumbprint.Should().Be(original.Thumbprint);
        certificate.SpkiSha256.Should().Be(original.SpkiSha256);
    }

    [Fact]
    public void CredentialType_IncludesCertificateValue()
    {
        Enum.IsDefined(typeof(CredentialType), CredentialType.Certificate).Should().BeTrue();
    }
}
