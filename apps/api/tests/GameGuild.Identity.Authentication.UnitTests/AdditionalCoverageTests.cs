using System.Text.Json;
using FluentAssertions;
using FluentValidation.TestHelper;
using GameGuild.Identity.Authentication;
using Xunit;

namespace GameGuild.Identity.Authentication.UnitTests;

#region PolymorphicCredentialConverter Tests

public class PolymorphicCredentialConverterTests
{
    private readonly JsonSerializerOptions _options;

    public PolymorphicCredentialConverterTests()
    {
        _options = new JsonSerializerOptions();
        _options.Converters.Add(new PolymorphicCredentialConverter());
        _options.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    }

    [Fact]
    public void Deserialize_EmailType_ReturnsEmailCredentialData()
    {
        var json = """{"type":"email","email":"test@test.com","password":"pass"}""";
        var result = JsonSerializer.Deserialize<ICredentialData>(json, _options);
        result.Should().BeOfType<EmailCredentialData>();
    }

    [Fact]
    public void Deserialize_PhoneType_ReturnsPhoneCredentialData()
    {
        var json = """{"type":"phone","phoneNumber":"+1234","password":"pass"}""";
        var result = JsonSerializer.Deserialize<ICredentialData>(json, _options);
        result.Should().BeOfType<PhoneCredentialData>();
    }

    [Fact]
    public void Deserialize_UsernameType_ReturnsUsernameCredentialData()
    {
        var json = """{"type":"username","username":"user1","password":"pass"}""";
        var result = JsonSerializer.Deserialize<ICredentialData>(json, _options);
        result.Should().BeOfType<UsernameCredentialData>();
    }

    [Fact]
    public void Deserialize_OAuthType_ReturnsOAuthCredentialData()
    {
        var json = """{"type":"oauth","provider":"google","token":"tok123"}""";
        var result = JsonSerializer.Deserialize<ICredentialData>(json, _options);
        result.Should().BeOfType<OAuthCredentialData>();
    }

    [Fact]
    public void Deserialize_Web3Type_ReturnsWeb3CredentialData()
    {
        var json = """{"type":"web3","walletAddress":"0x123","signature":"sig","message":"msg"}""";
        var result = JsonSerializer.Deserialize<ICredentialData>(json, _options);
        result.Should().BeOfType<Web3CredentialData>();
    }

    [Fact]
    public void Deserialize_UnknownType_ThrowsJsonException()
    {
        var json = """{"type":"biometric"}""";
        var act = () => JsonSerializer.Deserialize<ICredentialData>(json, _options);
        act.Should().Throw<JsonException>();
    }

    // Auto-detection by properties
    [Fact]
    public void Deserialize_AutoDetect_Email()
    {
        var json = """{"email":"test@test.com","password":"pass"}""";
        var result = JsonSerializer.Deserialize<ICredentialData>(json, _options);
        result.Should().BeOfType<EmailCredentialData>();
    }

    [Fact]
    public void Deserialize_AutoDetect_Phone()
    {
        var json = """{"phoneNumber":"+1234","password":"pass"}""";
        var result = JsonSerializer.Deserialize<ICredentialData>(json, _options);
        result.Should().BeOfType<PhoneCredentialData>();
    }

    [Fact]
    public void Deserialize_AutoDetect_Username()
    {
        var json = """{"username":"user1","password":"pass"}""";
        var result = JsonSerializer.Deserialize<ICredentialData>(json, _options);
        result.Should().BeOfType<UsernameCredentialData>();
    }

    [Fact]
    public void Deserialize_AutoDetect_OAuth()
    {
        var json = """{"provider":"google","token":"tok"}""";
        var result = JsonSerializer.Deserialize<ICredentialData>(json, _options);
        result.Should().BeOfType<OAuthCredentialData>();
    }

    [Fact]
    public void Deserialize_AutoDetect_Web3()
    {
        var json = """{"walletAddress":"0x1","signature":"sig","message":"msg"}""";
        var result = JsonSerializer.Deserialize<ICredentialData>(json, _options);
        result.Should().BeOfType<Web3CredentialData>();
    }

    [Fact]
    public void Deserialize_NoTypeOrKnownProps_ThrowsJsonException()
    {
        var json = """{"foo":"bar"}""";
        var act = () => JsonSerializer.Deserialize<ICredentialData>(json, _options);
        act.Should().Throw<JsonException>();
    }

    [Fact]
    public void Serialize_WriteRoundTrip()
    {
        var data = new EmailCredentialData { Email = "test@test.com", Password = "pass" };
        var json = JsonSerializer.Serialize<ICredentialData>(data, _options);
        json.Should().Contain("test@test.com");
        using var document = JsonDocument.Parse(json);
        document.RootElement.GetProperty("type").GetString().Should().Be("email");
        var roundTrip = Assert.IsType<EmailCredentialData>(JsonSerializer.Deserialize<ICredentialData>(json, _options));
        Assert.Equal(data.Email, roundTrip.Email);
        Assert.Equal(data.Password, roundTrip.Password);
        Assert.NotSame(data, roundTrip);
    }

    [Fact]
    public void Deserialize_CertificateType_PreservesCertificateProperties()
    {
        var json = """{"type":"certificate","thumbprint":"synthetic-thumbprint","spkiSha256":"synthetic-key-pin"}""";
        var result = Assert.IsType<CertificateCredentialData>(JsonSerializer.Deserialize<ICredentialData>(json, _options));
        Assert.Equal("synthetic-thumbprint", result.Thumbprint);
        Assert.Equal("synthetic-key-pin", result.SpkiSha256);
    }

    [Fact]
    public void Deserialize_AutoDetect_Certificate()
    {
        var json = """{"thumbprint":"synthetic-thumbprint","spkiSha256":"synthetic-key-pin"}""";
        var result = Assert.IsType<CertificateCredentialData>(JsonSerializer.Deserialize<ICredentialData>(json, _options));
        Assert.Equal("synthetic-thumbprint", result.Thumbprint);
        Assert.Equal("synthetic-key-pin", result.SpkiSha256);
    }

    [Theory]
    [InlineData("email")]
    [InlineData("phone")]
    [InlineData("username")]
    [InlineData("oauth")]
    [InlineData("web3")]
    [InlineData("certificate")]
    public void Serialize_AllDeclaredTypes_PreserveDiscriminatorAndRoundTripProperties(string type)
    {
        var original = CreateSyntheticCredential(type);
        var json = JsonSerializer.Serialize<ICredentialData>(original, _options);
        using var document = JsonDocument.Parse(json);
        Assert.Equal(type, document.RootElement.GetProperty("type").GetString());
        var roundTrip = JsonSerializer.Deserialize<ICredentialData>(json, _options);
        Assert.NotNull(roundTrip);
        Assert.NotSame(original, roundTrip);
        Assert.Equal(original.GetType(), roundTrip.GetType());

        switch ((original, roundTrip))
        {
            case (EmailCredentialData expectedEmail, EmailCredentialData actualEmail):
                Assert.Equal(expectedEmail.Email, actualEmail.Email);
                Assert.Equal(expectedEmail.Password, actualEmail.Password);
                break;
            case (PhoneCredentialData expectedPhone, PhoneCredentialData actualPhone):
                Assert.Equal(expectedPhone.PhoneNumber, actualPhone.PhoneNumber);
                Assert.Equal(expectedPhone.Password, actualPhone.Password);
                break;
            case (UsernameCredentialData expectedUsername, UsernameCredentialData actualUsername):
                Assert.Equal(expectedUsername.Username, actualUsername.Username);
                Assert.Equal(expectedUsername.Password, actualUsername.Password);
                break;
            case (OAuthCredentialData expectedOAuth, OAuthCredentialData actualOAuth):
                Assert.Equal(expectedOAuth.Provider, actualOAuth.Provider);
                Assert.Equal(expectedOAuth.Token, actualOAuth.Token);
                break;
            case (Web3CredentialData expectedWeb3, Web3CredentialData actualWeb3):
                Assert.Equal(expectedWeb3.WalletAddress, actualWeb3.WalletAddress);
                Assert.Equal(expectedWeb3.Signature, actualWeb3.Signature);
                Assert.Equal(expectedWeb3.Message, actualWeb3.Message);
                break;
            case (CertificateCredentialData expectedCertificate, CertificateCredentialData actualCertificate):
                Assert.Equal(expectedCertificate.Thumbprint, actualCertificate.Thumbprint);
                Assert.Equal(expectedCertificate.SpkiSha256, actualCertificate.SpkiSha256);
                break;
            default:
                throw new InvalidOperationException("The declared credential contract was not exercised.");
        }
    }

    [Theory]
    [InlineData("[]")]
    [InlineData("42")]
    [InlineData("true")]
    [InlineData("\"credential\"")]
    public void Deserialize_NonObjectRoots_RejectsWithJsonException(string json)
    {
        var act = () => JsonSerializer.Deserialize<ICredentialData>(json, _options);
        act.Should().Throw<JsonException>();
    }

    [Theory]
    [InlineData("42")]
    [InlineData("true")]
    [InlineData("{}")]
    [InlineData("[]")]
    [InlineData("null")]
    public void Deserialize_NonTextDiscriminators_RejectsWithoutLegacyEmailFallback(string discriminatorJson)
    {
        var json = "{\"type\":" + discriminatorJson + ",\"email\":\"synthetic@example.test\"}";
        var act = () => JsonSerializer.Deserialize<ICredentialData>(json, _options);
        act.Should().Throw<JsonException>();
    }

    [Fact]
    public void NullCredentialPayloadKeepsDefaultSerializerNullContract()
    {
        Assert.Null(JsonSerializer.Deserialize<ICredentialData>("null", _options));
        Assert.Equal("null", JsonSerializer.Serialize<ICredentialData?>(null, _options));
    }

    private static ICredentialData CreateSyntheticCredential(string type) => type switch
    {
        "email" => new EmailCredentialData { Email = "roundtrip@example.test", Password = Guid.NewGuid().ToString("N") },
        "phone" => new PhoneCredentialData { PhoneNumber = "+15550001000", Password = Guid.NewGuid().ToString("N") },
        "username" => new UsernameCredentialData { Username = "roundtrip-user", Password = Guid.NewGuid().ToString("N") },
        "oauth" => new OAuthCredentialData { Provider = "synthetic-provider", Token = Guid.NewGuid().ToString("N") },
        "web3" => new Web3CredentialData { WalletAddress = "synthetic-wallet", Signature = "synthetic-signature", Message = "synthetic-message" },
        "certificate" => new CertificateCredentialData { Thumbprint = "synthetic-thumbprint", SpkiSha256 = "synthetic-key-pin" },
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Unknown synthetic credential type.")
    };
}

#endregion

#region CredentialData DTOs Tests

public class PhoneCredentialDataTests
{
    [Fact]
    public void Type_ReturnsPhone()
    {
        new PhoneCredentialData().Type.Should().Be("phone");
    }

    [Fact]
    public void Defaults_AreEmpty()
    {
        var d = new PhoneCredentialData();
        d.PhoneNumber.Should().BeEmpty();
        d.Password.Should().BeEmpty();
    }
}

public class UsernameCredentialDataTests
{
    [Fact]
    public void Type_ReturnsUsername()
    {
        new UsernameCredentialData().Type.Should().Be("username");
    }

    [Fact]
    public void Defaults_AreEmpty()
    {
        var d = new UsernameCredentialData();
        d.Username.Should().BeEmpty();
        d.Password.Should().BeEmpty();
    }
}

public class OAuthCredentialDataTests
{
    [Fact]
    public void Type_ReturnsOAuth()
    {
        new OAuthCredentialData().Type.Should().Be("oauth");
    }

    [Fact]
    public void Defaults_AreEmpty()
    {
        var d = new OAuthCredentialData();
        d.Provider.Should().BeEmpty();
        d.Token.Should().BeEmpty();
    }
}

public class Web3CredentialDataTests
{
    [Fact]
    public void Type_ReturnsWeb3()
    {
        new Web3CredentialData().Type.Should().Be("web3");
    }

    [Fact]
    public void Defaults_AreEmpty()
    {
        var d = new Web3CredentialData();
        d.WalletAddress.Should().BeEmpty();
        d.Signature.Should().BeEmpty();
        d.Message.Should().BeEmpty();
    }
}

#endregion

#region AuthenticationAttemptContext Tests

public class AuthenticationAttemptContextTests
{
    [Fact]
    public void DeviceInfo_Alias_SyncsWithDevice()
    {
        var ctx = new AuthenticationAttemptContext();
        var device = new DeviceInfo { Fingerprint = "fp123" };
        ctx.DeviceInfo = device;
        ctx.Device.Should().BeSameAs(device);
    }

    [Fact]
    public void Device_SetsDeviceInfo_Alias()
    {
        var ctx = new AuthenticationAttemptContext();
        var device = new DeviceInfo { Fingerprint = "fp456" };
        ctx.Device = device;
        ctx.DeviceInfo.Should().BeSameAs(device);
    }

    [Fact]
    public void LocationInfo_Alias_SyncsWithLocation()
    {
        var ctx = new AuthenticationAttemptContext();
        var loc = new LocationInfo { City = "NYC" };
        ctx.LocationInfo = loc;
        ctx.Location.Should().BeSameAs(loc);
    }

    [Fact]
    public void Timestamp_Alias_SyncsWithAttemptedAt()
    {
        var ctx = new AuthenticationAttemptContext();
        var dt = new DateTime(2025, 6, 15, 14, 30, 0, DateTimeKind.Utc);
        ctx.Timestamp = dt;
        ctx.AttemptedAt.Should().Be(dt);
    }

    [Fact]
    public void TimeOfDay_ReturnsAttemptedAtTimeOfDay()
    {
        var dt = new DateTime(2025, 6, 15, 14, 30, 0, DateTimeKind.Utc);
        var ctx = new AuthenticationAttemptContext { AttemptedAt = dt };
        ctx.TimeOfDay.Should().Be(new TimeSpan(14, 30, 0));
    }

    [Fact]
    public void DayOfWeek_ReturnsCorrectDay()
    {
        var ctx = new AuthenticationAttemptContext { AttemptedAt = new DateTime(2025, 6, 15, 0, 0, 0, DateTimeKind.Utc) }; // Sunday
        ctx.DayOfWeek.Should().Be(DayOfWeek.Sunday);
    }

    [Fact]
    public void IsWeekend_OnSaturday_ReturnsTrue()
    {
        var ctx = new AuthenticationAttemptContext { AttemptedAt = new DateTime(2025, 6, 14, 0, 0, 0, DateTimeKind.Utc) }; // Saturday
        ctx.IsWeekend.Should().BeTrue();
    }

    [Fact]
    public void IsWeekend_OnMonday_ReturnsFalse()
    {
        var ctx = new AuthenticationAttemptContext { AttemptedAt = new DateTime(2025, 6, 16, 0, 0, 0, DateTimeKind.Utc) }; // Monday
        ctx.IsWeekend.Should().BeFalse();
    }
}

#endregion

#region LocationInfo Tests

public class LocationInfoTests
{
    [Fact]
    public void DisplayLocation_WithCityAndCountry_ReturnsCityCountry()
    {
        var loc = new LocationInfo { City = "NYC", Country = "US" };
        loc.DisplayLocation.Should().Be("NYC, US");
    }

    [Fact]
    public void DisplayLocation_WithCountryOnly_ReturnsCountry()
    {
        var loc = new LocationInfo { Country = "US" };
        loc.DisplayLocation.Should().Be("US");
    }

    [Fact]
    public void DisplayLocation_NoData_ReturnsUnknownLocation()
    {
        var loc = new LocationInfo();
        loc.DisplayLocation.Should().Be("Unknown Location");
    }

    [Fact]
    public void Default_IpAddress_Empty()
    {
        new LocationInfo().IpAddress.Should().BeEmpty();
    }
}

#endregion

#region DeviceInfo Tests

public class DeviceInfoTests
{
    [Fact]
    public void DeviceId_ReturnsFingerprint()
    {
        var d = new DeviceInfo { Fingerprint = "test-fp" };
        d.DeviceId.Should().Be("test-fp");
    }

    [Fact]
    public void Default_Fingerprint_Empty()
    {
        new DeviceInfo().Fingerprint.Should().BeEmpty();
    }

    [Fact]
    public void Default_IsMobile_False()
    {
        new DeviceInfo().IsMobile.Should().BeFalse();
    }

    [Fact]
    public void Default_IsBot_False()
    {
        new DeviceInfo().IsBot.Should().BeFalse();
    }
}

#endregion

#region SignInResponse & RoleDto Tests

public class SignInResponseTests
{
    [Fact]
    public void Defaults_AreCorrect()
    {
        var r = new SignInResponse();
        r.Success.Should().BeFalse();
        r.Message.Should().BeEmpty();
        r.AccessToken.Should().BeEmpty();
        r.RefreshToken.Should().BeEmpty();
        r.Email.Should().BeEmpty();
        r.RequiresMfa.Should().BeFalse();
        r.RequiresStepUp.Should().BeFalse();
        r.User.Should().NotBeNull();
    }
}

public class RoleDtoTests
{
    [Fact]
    public void Defaults_AreCorrect()
    {
        var d = new RoleDto();
        d.Name.Should().BeEmpty();
        d.Description.Should().BeEmpty();
        d.Permissions.Should().BeEmpty();
    }
}

public class CreateRoleRequestTests
{
    [Fact]
    public void Defaults_AreCorrect()
    {
        var r = new CreateRoleRequest();
        r.Name.Should().BeEmpty();
        r.Permissions.Should().BeEmpty();
    }
}

public class UserRoleDtoTests
{
    [Fact]
    public void AllProperties_CanBeSet()
    {
        var d = new UserRoleDto
        {
            Id = Guid.NewGuid(), UserId = Guid.NewGuid(),
            RoleId = Guid.NewGuid(), AssignedBy = Guid.NewGuid(),
            IsExpired = true
        };
        d.IsExpired.Should().BeTrue();
    }
}

#endregion

#region PublicAttribute Tests

public class PublicAttributeTests
{
    [Fact]
    public void Default_IsPublic_True()
    {
        new PublicAttribute().IsPublic.Should().BeTrue();
    }

    [Fact]
    public void ExplicitFalse_IsPublic_False()
    {
        new PublicAttribute(false).IsPublic.Should().BeFalse();
    }
}

#endregion

#region OAuthUserProfile Tests

public class OAuthUserProfileTests
{
    [Fact]
    public void Defaults_AreCorrect()
    {
        var p = new OAuthUserProfile();
        p.ProviderId.Should().BeEmpty();
        p.Provider.Should().BeEmpty();
        p.Email.Should().BeNull();
        p.EmailVerified.Should().BeFalse();
        p.Name.Should().BeNull();
        p.AvatarUrl.Should().BeNull();
    }

    [Fact]
    public void AllProperties_CanBeSet()
    {
        var p = new OAuthUserProfile
        {
            ProviderId = "123", Provider = "google",
            Email = "t@t.com", EmailVerified = true,
            Name = "Test", FirstName = "T", LastName = "U",
            Username = "testuser", AvatarUrl = "http://img",
            Locale = "en", AccessToken = "at", RefreshToken = "rt",
            TokenExpiresAt = DateTime.UtcNow,
            AdditionalClaims = new() { { "key", "val" } }
        };
        p.AdditionalClaims.Should().ContainKey("key");
    }
}

#endregion

#region ActivityTimelineEntry Tests

public class ActivityTimelineEntryTests
{
    [Fact]
    public void Defaults_AreCorrect()
    {
        var e = new ActivityTimelineEntry();
        e.ActivityType.Should().BeEmpty();
        e.Description.Should().BeEmpty();
        e.IsSuspicious.Should().BeFalse();
        e.RiskLevel.Should().Be(RiskLevel.Low);
    }

    [Fact]
    public void AllProperties_CanBeSet()
    {
        var e = new ActivityTimelineEntry
        {
            Id = Guid.NewGuid(), Timestamp = DateTime.UtcNow,
            ActivityType = "Login", Description = "desc",
            IpAddress = "1.2.3.4", UserAgent = "ua",
            DeviceFingerprint = "fp", Location = "NYC",
            IsSuspicious = true, RiskLevel = RiskLevel.High,
            SessionId = Guid.NewGuid(),
            Metadata = new() { { "k", "v" } }
        };
        e.Location.Should().Be("NYC");
    }
}

#endregion

#region SiemEvent Tests

public class SiemEventTests
{
    [Fact]
    public void Defaults_AreCorrect()
    {
        var e = new SiemEvent { EventType = "test", Description = "desc" };
        e.EventId.Should().NotBeEmpty();
        e.Source.Should().Be("GameGuild.Authentication");
        e.Severity.Should().Be(SiemSeverity.Info);
    }

    [Fact]
    public void AllProperties_CanBeSet()
    {
        var e = new SiemEvent
        {
            EventType = "auth.login", Description = "User logged in",
            Severity = SiemSeverity.Critical,
            UserId = Guid.NewGuid(), IpAddress = "1.2.3.4",
            UserAgent = "ua", RiskScore = 85,
            TenantId = Guid.NewGuid(), CorrelationId = Guid.NewGuid(),
            Metadata = new() { { "k", "v" } }
        };
        e.RiskScore.Should().Be(85);
    }

    [Theory]
    [InlineData(SiemSeverity.Info)]
    [InlineData(SiemSeverity.Low)]
    [InlineData(SiemSeverity.Medium)]
    [InlineData(SiemSeverity.High)]
    [InlineData(SiemSeverity.Critical)]
    public void SiemSeverity_AllValuesValid(SiemSeverity s)
    {
        Enum.IsDefined(s).Should().BeTrue();
    }
}

#endregion

#region AuthenticationAttemptAnalysis & Response Tests

public class AuthenticationAttemptAnalysisTests
{
    [Fact]
    public void Defaults_AreCorrect()
    {
        var a = new AuthenticationAttemptAnalysis();
        a.IsSuspicious.Should().BeFalse();
        a.RiskScore.Should().Be(0);
        a.RiskFactors.Should().BeEmpty();
    }
}

public class AuthenticationAttemptResponseTests
{
    [Fact]
    public void Defaults_AreCorrect()
    {
        var r = new AuthenticationAttemptResponse();
        r.Email.Should().BeEmpty();
        r.IpAddress.Should().BeEmpty();
        r.IsSuccessful.Should().BeFalse();
        r.IsSuspicious.Should().BeFalse();
    }
}

public class ChangePasswordRequestTests
{
    [Fact]
    public void Defaults_AreCorrect()
    {
        var r = new ChangePasswordRequest();
        r.CurrentPassword.Should().BeEmpty();
        r.NewPassword.Should().BeEmpty();
    }
}

#endregion

#region CreateApiKeyValidator Tests

public class CreateApiKeyValidatorTests
{
    private readonly CreateApiKeyValidator _validator = new();

    [Fact]
    public void ValidCommand_ShouldPass()
    {
        var cmd = new CreateApiKeyCommand { Name = "Test Key", Scopes = ["read", "write"] };
        _validator.TestValidate(cmd).ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void EmptyName_ShouldFail()
    {
        var cmd = new CreateApiKeyCommand { Name = "", Scopes = ["read"] };
        _validator.TestValidate(cmd).ShouldHaveValidationErrorFor(x => x.Name);
    }

    [Fact]
    public void NameTooLong_ShouldFail()
    {
        var cmd = new CreateApiKeyCommand { Name = new string('A', 101), Scopes = ["read"] };
        _validator.TestValidate(cmd).ShouldHaveValidationErrorFor(x => x.Name);
    }

    [Fact]
    public void EmptyScopes_ShouldFail()
    {
        var cmd = new CreateApiKeyCommand { Name = "Test", Scopes = [] };
        _validator.TestValidate(cmd).ShouldHaveValidationErrorFor(x => x.Scopes);
    }

    [Fact]
    public void PastExpiresAt_ShouldFail()
    {
        var cmd = new CreateApiKeyCommand
        {
            Name = "Test", Scopes = ["read"],
            ExpiresAt = DateTime.UtcNow.AddDays(-1)
        };
        _validator.TestValidate(cmd).ShouldHaveValidationErrorFor(x => x.ExpiresAt);
    }

    [Fact]
    public void FutureExpiresAt_ShouldPass()
    {
        var cmd = new CreateApiKeyCommand
        {
            Name = "Test", Scopes = ["read"],
            ExpiresAt = DateTime.UtcNow.AddDays(30)
        };
        _validator.TestValidate(cmd).ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void NullExpiresAt_ShouldPass()
    {
        var cmd = new CreateApiKeyCommand { Name = "Test", Scopes = ["read"], ExpiresAt = null };
        _validator.TestValidate(cmd).ShouldNotHaveAnyValidationErrors();
    }
}

#endregion

#region ApiKey Factory Method Tests

public class CreateApiKeyResponseFactoryTests
{
    [Fact]
    public void FromEntity_MapsAllProperties()
    {
        var (entity, plaintext) = ApiKey.Create(
            Guid.NewGuid(), Guid.NewGuid(), "Test Key",
            new[] { "read", "write" }, DateTime.UtcNow.AddDays(30));

        var response = CreateApiKeyResponse.FromEntity(entity, plaintext);

        response.Id.Should().Be(entity.Id);
        response.Name.Should().Be("Test Key");
        response.ApiKey.Should().Be(plaintext);
        response.KeyPrefix.Should().Be(entity.KeyPrefix);
        response.Scopes.Should().Contain("read");
        response.Scopes.Should().Contain("write");
        response.ExpiresAt.Should().NotBeNull();
    }

    [Fact]
    public void DefaultValues_AreCorrect()
    {
        var r = new CreateApiKeyResponse();
        r.Name.Should().BeEmpty();
        r.ApiKey.Should().BeEmpty();
        r.KeyPrefix.Should().BeEmpty();
        r.Scopes.Should().BeEmpty();
    }
}

public class ApiKeyDtoFactoryTests
{
    [Fact]
    public void FromEntity_MapsAllProperties()
    {
        var (entity, _) = ApiKey.Create(
            Guid.NewGuid(), Guid.NewGuid(), "Test Key",
            new[] { "admin" });

        var dto = ApiKeyDto.FromEntity(entity);

        dto.Id.Should().Be(entity.Id);
        dto.Name.Should().Be("Test Key");
        dto.KeyPrefix.Should().Be(entity.KeyPrefix);
        dto.Scopes.Should().Contain("admin");
        dto.IsActive.Should().BeTrue();
    }

    [Fact]
    public void DefaultValues_AreCorrect()
    {
        var dto = new ApiKeyDto();
        dto.Name.Should().BeEmpty();
        dto.KeyPrefix.Should().BeEmpty();
        dto.Scopes.Should().BeEmpty();
    }
}

#endregion

#region JwtKeyInfoDto Tests

public class JwtKeyInfoDtoTests
{
    [Fact]
    public void DefaultValues_AreCorrect()
    {
        var dto = new JwtKeyInfoDto();
        dto.KeyId.Should().BeEmpty();
        dto.Algorithm.Should().BeEmpty();
        dto.IsActive.Should().BeFalse();
    }
}

#endregion

#region ServiceAccount DTOs Tests

public class ClientCredentialsRequestTests
{
    [Fact]
    public void Defaults_AreCorrect()
    {
        var r = new ClientCredentialsRequest();
        r.GrantType.Should().BeEmpty();
        r.ClientId.Should().BeEmpty();
        r.ClientSecret.Should().BeEmpty();
    }
}

public class ClientCredentialsTokenResponseTests
{
    [Fact]
    public void Defaults_AreCorrect()
    {
        var r = new ClientCredentialsTokenResponse();
        r.AccessToken.Should().BeEmpty();
        r.TokenType.Should().Be("Bearer");
    }
}

public class OAuth2ErrorResponseTests
{
    [Fact]
    public void Defaults_AreCorrect()
    {
        var r = new OAuth2ErrorResponse();
        r.Error.Should().BeEmpty();
        r.ErrorDescription.Should().BeNull();
    }
}

public class CreateServiceAccountRequestTests
{
    [Fact]
    public void Defaults_AreCorrect()
    {
        var r = new CreateServiceAccountRequest();
        r.Name.Should().BeEmpty();
        r.Description.Should().BeNull();
    }
}

public class ServiceAccountCreatedResponseTests
{
    [Fact]
    public void Defaults_AreCorrect()
    {
        var r = new ServiceAccountCreatedResponse();
        r.ClientId.Should().BeEmpty();
        r.ClientSecret.Should().BeEmpty();
        r.Name.Should().BeEmpty();
        r.Scopes.Should().BeEmpty();
        r.Warning.Should().BeEmpty();
    }
}

public class ServiceAccountResponseTests
{
    [Fact]
    public void Defaults_AreCorrect()
    {
        var r = new ServiceAccountResponse();
        r.ClientId.Should().BeEmpty();
        r.Name.Should().BeEmpty();
        r.CreatedBy.Should().BeEmpty();
        r.IsActive.Should().BeFalse();
    }
}

public class SecretRotationResponseTests
{
    [Fact]
    public void Defaults_AreCorrect()
    {
        var r = new SecretRotationResponse();
        r.ClientSecret.Should().BeEmpty();
        r.Warning.Should().BeEmpty();
    }
}

public class ServiceAccountAuditEntryTests
{
    [Fact]
    public void Defaults_AreCorrect()
    {
        var r = new ServiceAccountAuditEntry();
        r.Action.Should().BeEmpty();
    }
}

public class ServiceAccountAuditLogResponseTests
{
    [Fact]
    public void Defaults_AreCorrect()
    {
        var r = new ServiceAccountAuditLogResponse();
        r.Entries.Should().BeEmpty();
    }
}

public class PagedAuditResultTests
{
    [Fact]
    public void Constructor_SetsProperties()
    {
        var items = Array.Empty<ServiceAccountAuditEntry>();
        var result = new PagedAuditResult(items, 42);
        result.Items.Should().BeSameAs(items);
        result.TotalCount.Should().Be(42);
    }
}

#endregion
