using FluentAssertions;
using FluentValidation.TestHelper;
using GameGuild.Identity.Authentication;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace GameGuild.Identity.Authentication.UnitTests.Validators;

public class LocalSignUpCommandValidatorTests
{
    private readonly LocalSignUpCommandValidator _validator;

    public LocalSignUpCommandValidatorTests()
    {
        var passwordHasher = new PasswordHasher(
            NullLogger<PasswordHasher>.Instance,
            new ConfigurationBuilder().Build());
        _validator = new LocalSignUpCommandValidator(passwordHasher);
    }

    // ── Email ─────────────────────────────────────────────────

    [Fact]
    public void Should_HaveError_When_EmailIsEmpty()
    {
        var command = new LocalSignUpCommand { Email = "", Password = "Password1!", Username = "user1" };
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.Email);
    }

    [Fact]
    public void Should_HaveError_When_EmailIsInvalid()
    {
        var command = new LocalSignUpCommand { Email = "not-an-email", Password = "Password1!", Username = "user1" };
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.Email);
    }

    [Fact]
    public void Should_HaveError_When_EmailIsTooLong()
    {
        var longEmail = new string('a', 246) + "@test.com"; // > 254 chars
        var command = new LocalSignUpCommand { Email = longEmail, Password = "Password1!", Username = "user1" };
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.Email);
    }

    [Fact]
    public void Should_NotHaveError_When_EmailIsValid()
    {
        var command = new LocalSignUpCommand { Email = "test@example.com", Password = "Password1!", Username = "user1" };
        var result = _validator.TestValidate(command);
        result.ShouldNotHaveValidationErrorFor(x => x.Email);
    }

    // ── Password ──────────────────────────────────────────────

    [Fact]
    public void Should_HaveError_When_PasswordIsEmpty()
    {
        var command = new LocalSignUpCommand { Email = "test@example.com", Password = "", Username = "user1" };
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.Password);
    }

    [Fact]
    public void Should_HaveError_When_PasswordIsTooShort()
    {
        var command = new LocalSignUpCommand { Email = "test@example.com", Password = "Abc1!", Username = "user1" };
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.Password);
    }

    [Fact]
    public void Should_HaveError_When_PasswordIsTooLong()
    {
        var command = new LocalSignUpCommand { Email = "test@example.com", Password = new string('A', 129) + "a1!", Username = "user1" };
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.Password);
    }

    [Fact]
    public void Should_HaveError_When_PasswordMissingUppercase()
    {
        var command = new LocalSignUpCommand { Email = "test@example.com", Password = "password1!", Username = "user1" };
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.Password);
    }

    [Fact]
    public void Should_HaveError_When_PasswordMissingLowercase()
    {
        var command = new LocalSignUpCommand { Email = "test@example.com", Password = "PASSWORD1!", Username = "user1" };
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.Password);
    }

    [Fact]
    public void Should_HaveError_When_PasswordMissingDigit()
    {
        var command = new LocalSignUpCommand { Email = "test@example.com", Password = "Password!", Username = "user1" };
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.Password);
    }

    [Fact]
    public void Should_HaveError_When_PasswordMissingSpecialChar()
    {
        var command = new LocalSignUpCommand { Email = "test@example.com", Password = "Password1", Username = "user1" };
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.Password);
    }

    [Fact]
    public void Should_NotHaveError_When_PasswordIsValid()
    {
        var command = new LocalSignUpCommand { Email = "test@example.com", Password = "Password1!", Username = "user1" };
        var result = _validator.TestValidate(command);
        result.ShouldNotHaveValidationErrorFor(x => x.Password);
    }

    [Fact]
    public void Should_UseConfiguredPasswordRequirements()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["PresentationLayer:Authentication:PasswordPolicy:MinPasswordLength"] = "8",
                ["PresentationLayer:Authentication:PasswordPolicy:MaxPasswordLength"] = "128",
                ["PresentationLayer:Authentication:PasswordPolicy:RequireUppercase"] = "false",
                ["PresentationLayer:Authentication:PasswordPolicy:RequireLowercase"] = "true",
                ["PresentationLayer:Authentication:PasswordPolicy:RequireDigit"] = "false",
                ["PresentationLayer:Authentication:PasswordPolicy:RequireSpecialChar"] = "false"
            })
            .Build();
        var validator = new LocalSignUpCommandValidator(
            new PasswordHasher(NullLogger<PasswordHasher>.Instance, configuration));
        var command = new LocalSignUpCommand
        {
            Email = "user@example.com",
            Password = "lowercase-only",
            Username = "valid.user"
        };

        var result = validator.TestValidate(command);

        result.ShouldNotHaveValidationErrorFor(x => x.Password);
    }

    [Fact]
    public void Should_NotIncludePasswordInValidationFailureDetails()
    {
        var command = new LocalSignUpCommand
        {
            Email = "user@example.com",
            Password = "weak",
            Username = "valid.user"
        };

        var result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.Password);
        result.Errors.Should().OnlyContain(failure => failure.AttemptedValue == null);
    }

    // ── Username ──────────────────────────────────────────────

    [Fact]
    public void Should_HaveError_When_UsernameIsEmpty()
    {
        var command = new LocalSignUpCommand { Email = "test@example.com", Password = "Password1!", Username = "" };
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.Username);
    }

    [Fact]
    public void Should_HaveError_When_UsernameIsTooShort()
    {
        var command = new LocalSignUpCommand { Email = "test@example.com", Password = "Password1!", Username = "ab" };
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.Username);
    }

    [Fact]
    public void Should_HaveError_When_UsernameIsTooLong()
    {
        var command = new LocalSignUpCommand { Email = "test@example.com", Password = "Password1!", Username = new string('a', 51) };
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.Username);
    }

    [Fact]
    public void Should_HaveError_When_UsernameHasInvalidChars()
    {
        var command = new LocalSignUpCommand { Email = "test@example.com", Password = "Password1!", Username = "---" };
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.Username);
    }

    [Theory]
    [InlineData("user_name")]
    [InlineData("user.name")]
    [InlineData("user-name")]
    [InlineData("user123")]
    [InlineData("Matheus Martins")]
    [InlineData("MátHeus Martíns")]
    [InlineData("  User Name  ")]
    public void Should_NotHaveError_When_UsernameIsValid(string username)
    {
        var command = new LocalSignUpCommand { Email = "test@example.com", Password = "Password1!", Username = username };
        var result = _validator.TestValidate(command);
        result.ShouldNotHaveValidationErrorFor(x => x.Username);
    }

    // ── TenantId ──────────────────────────────────────────────

    [Theory]
    [InlineData("...")]
    [InlineData("東京")]
    [InlineData("  ab  ")]
    [InlineData("a\u0000b")]
    [InlineData("\ud800")]
    public void Should_RejectUsernameWithoutUsableCanonicalHandle(string username)
    {
        var command = new LocalSignUpCommand { Email = "test@example.com", Password = "Password1!", Username = username };
        _validator.TestValidate(command).ShouldHaveValidationErrorFor(x => x.Username);
    }

    [Fact]
    public void Should_HaveError_When_TenantIdIsEmptyGuid()
    {
        var command = new LocalSignUpCommand { Email = "test@example.com", Password = "Password1!", Username = "user1", TenantId = Guid.Empty };
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.TenantId);
    }

    [Fact]
    public void Should_NotHaveError_When_TenantIdIsNull()
    {
        var command = new LocalSignUpCommand { Email = "test@example.com", Password = "Password1!", Username = "user1", TenantId = null };
        var result = _validator.TestValidate(command);
        result.ShouldNotHaveValidationErrorFor(x => x.TenantId);
    }

    [Fact]
    public void Should_NotHaveError_When_TenantIdIsValid()
    {
        var command = new LocalSignUpCommand { Email = "test@example.com", Password = "Password1!", Username = "user1", TenantId = Guid.NewGuid() };
        var result = _validator.TestValidate(command);
        result.ShouldNotHaveValidationErrorFor(x => x.TenantId);
    }

    // ── Full validation ───────────────────────────────────────

    [Fact]
    public void Should_PassValidation_When_AllFieldsAreValid()
    {
        var command = new LocalSignUpCommand
        {
            Email = "valid@example.com",
            Password = "StrongPass1!",
            Username = "validuser",
            TenantId = Guid.NewGuid()
        };
        var result = _validator.TestValidate(command);
        result.ShouldNotHaveAnyValidationErrors();
    }
}
