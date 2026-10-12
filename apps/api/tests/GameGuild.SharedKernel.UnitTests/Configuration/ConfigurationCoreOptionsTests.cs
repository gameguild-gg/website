using FluentAssertions;
using GameGuild.Configuration;
using GameGuild.Configuration.ApplicationLayer;


namespace GameGuild.Tests.SharedKernel.Unit.Configuration;

public class BaseOptionsTests
{
    [Fact]
    public void IsEnabled_ShouldDefaultToTrue()
    {
        var options = new TestBaseOptions();

        options.IsEnabled.Should().BeTrue();
    }

    [Fact]
    public void IsEnabled_ShouldBeSettable()
    {
        var options = new TestBaseOptions { IsEnabled = false };

        options.IsEnabled.Should().BeFalse();
    }

    [Fact]
    public void Validate_ShouldNotThrow()
    {
        var options = new TestBaseOptions();
        var act = () => options.Validate();

        act.Should().NotThrow();
    }

    private sealed class TestBaseOptions : BaseOptions;
}

public class ModuleOptionsTests
{
    [Fact]
    public void ModuleName_ShouldReturnAssemblyName()
    {
        var options = new TestModuleOptions();

        options.ModuleName.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void IsEnabled_ShouldDefaultToTrue()
    {
        var options = new TestModuleOptions();

        options.IsEnabled.Should().BeTrue();
    }

    private sealed class TestModuleOptions : ModuleOptions;
}

public class ApplicationLayerOptionsTests
{
    [Fact]
    public void SectionName_ShouldBeApplication()
    {
        ApplicationLayerOptions.SectionName.Should().Be("Application");
    }

    [Fact]
    public void Defaults_ShouldEnableCqrsAndFluentValidation()
    {
        var options = new ApplicationLayerOptions();

        options.EnableCqrs.Should().BeTrue();
        options.EnableFluentValidation.Should().BeTrue();
    }

    [Fact]
    public void CreateDefault_ShouldReturnInstance()
    {
        var options = ApplicationLayerOptions.CreateDefault();

        options.Should().NotBeNull();
        options.EnableCqrs.Should().BeTrue();
        options.EnableFluentValidation.Should().BeTrue();
    }
}

public class JwtOptionsTests
{
    [Fact]
    public void SectionName_ShouldBeJwt()
    {
        JwtOptions.SectionName.Should().Be("Jwt");
    }

    [Fact]
    public void Defaults_ShouldBeReasonable()
    {
        var options = new JwtOptions();

        options.Issuer.Should().Be("GameGuild");
        options.Audience.Should().Be("GameGuild.Users");
        options.SecretKey.Should().BeEmpty();
        options.AccessTokenExpirationMinutes.Should().Be(60);
        options.RefreshTokenExpirationDays.Should().Be(30);
        options.ClockSkewSeconds.Should().Be(0);
        options.ValidateIssuer.Should().BeTrue();
        options.ValidateAudience.Should().BeTrue();
        options.ValidateLifetime.Should().BeTrue();
        options.ValidateIssuerSigningKey.Should().BeTrue();
    }

    [Fact]
    public void Validate_WithDefaults_ShouldReturnSecretKeyError()
    {
        var options = new JwtOptions();

        var errors = options.Validate();

        errors.Should().NotBeEmpty();
        errors.Should().Contain(error => error.Contains("SecretKey"));
    }

    [Fact]
    public void Validate_WithValidConfig_ShouldBeEmpty()
    {
        var options = new JwtOptions
        {
            Issuer = "TestIssuer",
            Audience = "TestAudience",
            SecretKey = "ThisIsALongEnoughSecretKeyForHS256AlgorithmAtLeast32"
        };

        var errors = options.Validate();

        errors.Should().BeEmpty();
    }

    [Fact]
    public void Validate_WithShortSecretKey_ShouldReportError()
    {
        var options = new JwtOptions { SecretKey = "short" };

        var errors = options.Validate();

        errors.Should().Contain(error => error.Contains("32 characters"));
    }

    [Fact]
    public void IsValid_WithValidConfig_ShouldBeTrue()
    {
        var options = new JwtOptions
        {
            SecretKey = "ThisIsALongEnoughSecretKeyForHS256AlgorithmAtLeast32"
        };

        options.IsValid.Should().BeTrue();
    }

    [Fact]
    public void IsValid_WithEmptySecretKey_ShouldBeFalse()
    {
        var options = new JwtOptions();

        options.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validate_ZeroExpiration_ShouldReportError()
    {
        var options = new JwtOptions
        {
            SecretKey = "ThisIsALongEnoughSecretKeyForHS256AlgorithmAtLeast32",
            AccessTokenExpirationMinutes = 0
        };

        var errors = options.Validate();

        errors.Should().Contain(error => error.Contains("AccessTokenExpirationMinutes"));
    }

    [Fact]
    public void Validate_NegativeClockSkew_ShouldReportError()
    {
        var options = new JwtOptions
        {
            SecretKey = "ThisIsALongEnoughSecretKeyForHS256AlgorithmAtLeast32",
            ClockSkewSeconds = -1
        };

        var errors = options.Validate();

        errors.Should().Contain(error => error.Contains("ClockSkewSeconds"));
    }
}

public class AuthenticationSecurityOptionsTests
{
    [Fact]
    public void SectionName_ShouldBeAuthenticationSecurity()
    {
        AuthenticationSecurityOptions.SectionName.Should().Be("AuthenticationSecurity");
    }

    [Fact]
    public void Defaults_ShouldBeValid()
    {
        var options = new AuthenticationSecurityOptions();

        var (isValid, errors) = options.Validate();

        isValid.Should().BeTrue();
        errors.Should().BeEmpty();
    }

    [Fact]
    public void Defaults_ShouldHaveReasonableValues()
    {
        var options = new AuthenticationSecurityOptions();

        options.MaxFailedAttemptsPerHour.Should().Be(5);
        options.MaxFailedAttemptsPerDay.Should().Be(20);
        options.MaxAttemptsPerIpPerHour.Should().Be(50);
        options.AccountLockoutDurationMinutes.Should().Be(30);
        options.EnableIpThrottling.Should().BeTrue();
        options.EnableUserEnumerationProtection.Should().BeTrue();
        options.EnableAnomalyDetection.Should().BeTrue();
        options.RequireEmailVerification.Should().BeTrue();
        options.EmailVerificationTokenValidityHours.Should().Be(24);
        options.PasswordResetTokenValidityHours.Should().Be(1);
        options.EnableCaptchaOnSuspiciousActivity.Should().BeFalse();
        options.SuspiciousThreshold.Should().Be(3);
    }

    [Fact]
    public void Validate_HourlyExceedsDaily_ShouldReportError()
    {
        var options = new AuthenticationSecurityOptions
        {
            MaxFailedAttemptsPerHour = 50,
            MaxFailedAttemptsPerDay = 10
        };

        var (isValid, errors) = options.Validate();

        isValid.Should().BeFalse();
        errors.Should().Contain(error => error.Contains("MaxFailedAttemptsPerHour cannot exceed"));
    }

    [Fact]
    public void Validate_OutOfRangeValues_ShouldReportErrors()
    {
        var options = new AuthenticationSecurityOptions
        {
            MaxFailedAttemptsPerHour = 0,
            EmailVerificationTokenValidityHours = 200,
            SuspiciousThreshold = 99
        };

        var (isValid, _) = options.Validate();

        isValid.Should().BeFalse();
    }

    [Fact]
    public void IsValid_WithDefaults_ShouldBeTrue()
    {
        var options = new AuthenticationSecurityOptions();

        options.IsValid.Should().BeTrue();
    }
}

public class SessionOptionsTests
{
    [Fact]
    public void SectionName_ShouldBeSession()
    {
        SessionOptions.SectionName.Should().Be("Session");
    }

    [Fact]
    public void Defaults_ShouldBeValid()
    {
        var options = new SessionOptions();

        var (isValid, _) = options.Validate();

        isValid.Should().BeTrue();
    }

    [Fact]
    public void Defaults_ShouldHaveReasonableValues()
    {
        var options = new SessionOptions();

        options.IdleTimeoutMinutes.Should().Be(30);
        options.AbsoluteTimeoutMinutes.Should().Be(1440);
        options.MaxConcurrentSessions.Should().Be(5);
        options.TrustedDeviceDurationDays.Should().Be(30);
        options.MaxTrustedDevices.Should().Be(10);
        options.TerminateSessionsOnPasswordChange.Should().BeTrue();
        options.TerminateSessionsOnMfaDisable.Should().BeTrue();
        options.EnableDeviceFingerprinting.Should().BeTrue();
        options.EnableLocationTracking.Should().BeTrue();
        options.RequireTrustedDeviceForSensitiveOps.Should().BeFalse();
    }

    [Fact]
    public void Validate_IdleExceedsAbsolute_ShouldReportError()
    {
        var options = new SessionOptions
        {
            IdleTimeoutMinutes = 2000,
            AbsoluteTimeoutMinutes = 1000
        };

        var (isValid, errors) = options.Validate();

        isValid.Should().BeFalse();
        errors.Should().Contain(error => error.Contains("IdleTimeoutMinutes cannot be greater"));
    }

    [Fact]
    public void IsValid_WithDefaults_ShouldBeTrue()
    {
        var options = new SessionOptions();

        options.IsValid.Should().BeTrue();
    }
}

public class MfaOptionsTests
{
    [Fact]
    public void SectionName_ShouldBeMfa()
    {
        MfaOptions.SectionName.Should().Be("Mfa");
    }

    [Fact]
    public void Defaults_ShouldBeValid()
    {
        var options = new MfaOptions();

        var (isValid, _) = options.Validate();

        isValid.Should().BeTrue();
    }

    [Fact]
    public void Defaults_ShouldHaveReasonableValues()
    {
        var options = new MfaOptions();

        options.MaxFailedAttempts.Should().Be(5);
        options.LockoutDurationMinutes.Should().Be(15);
        options.BackupCodesCount.Should().Be(10);
        options.BackupCodeLength.Should().Be(12);
        options.TotpTimeStepSeconds.Should().Be(30);
        options.TotpClockSkew.Should().Be(1);
        options.SetupSessionDurationMinutes.Should().Be(10);
        options.RequireMfaByDefault.Should().BeFalse();
        options.TotpIssuer.Should().Be("GameGuild");
        options.Enabled.Should().BeTrue();
    }

    [Fact]
    public void Validate_EmptyTotpIssuer_ShouldReportError()
    {
        var options = new MfaOptions { TotpIssuer = "" };

        var (isValid, errors) = options.Validate();

        isValid.Should().BeFalse();
        errors.Should().Contain(error => error.Contains("TotpIssuer"));
    }

    [Fact]
    public void Validate_OutOfRangeValues_ShouldReportErrors()
    {
        var options = new MfaOptions
        {
            MaxFailedAttempts = 0,
            BackupCodeLength = 3,
            TotpTimeStepSeconds = 10
        };

        var (isValid, errors) = options.Validate();

        isValid.Should().BeFalse();
        errors.Length.Should().BeGreaterThan(0);
    }
}

public class EncryptionOptionsTests
{
    [Fact]
    public void SectionName_ShouldBeEncryption()
    {
        EncryptionOptions.SectionName.Should().Be("Encryption");
    }

    [Fact]
    public void Defaults_ShouldHaveReasonableValues()
    {
        var options = new EncryptionOptions();

        options.EncryptionKey.Should().BeEmpty();
        options.Algorithm.Should().Be("AES256");
        options.CipherMode.Should().Be(System.Security.Cryptography.CipherMode.CBC);
        options.PaddingMode.Should().Be(System.Security.Cryptography.PaddingMode.PKCS7);
        options.EnableKeyRotation.Should().BeFalse();
        options.KeyRotationIntervalDays.Should().Be(90);
        options.PreviousKeys.Should().BeNull();
    }

    [Fact]
    public void Validate_EmptyKey_ShouldReportError()
    {
        var options = new EncryptionOptions();

        var (isValid, errors) = options.Validate();

        isValid.Should().BeFalse();
        errors.Should().Contain(error => error.Contains("EncryptionKey is required"));
    }

    [Fact]
    public void Validate_ShortKey_ShouldReportError()
    {
        var options = new EncryptionOptions { EncryptionKey = "short" };

        var (isValid, errors) = options.Validate();

        isValid.Should().BeFalse();
        errors.Should().Contain(error => error.Contains("at least 32 characters"));
    }

    [Fact]
    public void Validate_ValidBase64Key_ShouldPass()
    {
        var key = Convert.ToBase64String(new byte[32]);
        var options = new EncryptionOptions { EncryptionKey = key };

        var (isValid, _) = options.Validate();

        isValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_EmptyAlgorithm_ShouldReportError()
    {
        var key = Convert.ToBase64String(new byte[32]);
        var options = new EncryptionOptions { EncryptionKey = key, Algorithm = "" };

        var (isValid, errors) = options.Validate();

        isValid.Should().BeFalse();
        errors.Should().Contain(error => error.Contains("Algorithm"));
    }

    [Fact]
    public void IsValid_WithValidConfig_ShouldBeTrue()
    {
        var key = Convert.ToBase64String(new byte[32]);
        var options = new EncryptionOptions { EncryptionKey = key };

        options.IsValid.Should().BeTrue();
    }
}
