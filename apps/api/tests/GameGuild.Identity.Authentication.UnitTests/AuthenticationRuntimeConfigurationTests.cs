using FluentAssertions;
using GameGuild.Configuration.ApplicationLayer;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace GameGuild.Identity.Authentication.UnitTests;

public sealed class AuthenticationRuntimeConfigurationTests
{
    [Fact]
    public void AddAuthenticationData_BindsAndRegistersValidatedMfaAndSessionOptions()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Mfa:MaxFailedAttempts"] = "3",
                ["Mfa:LockoutDurationMinutes"] = "22",
                ["Mfa:RequireMfaByDefault"] = "true",
                ["Session:IdleTimeoutMinutes"] = "45",
                ["Session:MaxConcurrentSessions"] = "2"
            })
            .Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAuthenticationData(configuration);

        using var serviceProvider = services.BuildServiceProvider();

        var mfaOptions = serviceProvider.GetRequiredService<MfaOptions>();
        mfaOptions.MaxFailedAttempts.Should().Be(3);
        mfaOptions.LockoutDurationMinutes.Should().Be(22);
        mfaOptions.RequireMfaByDefault.Should().BeTrue();

        var sessionOptions = serviceProvider.GetRequiredService<SessionOptions>();
        sessionOptions.IdleTimeoutMinutes.Should().Be(45);
        sessionOptions.MaxConcurrentSessions.Should().Be(2);
    }

    [Fact]
    public void AddAuthenticationData_RejectsInvalidMfaConfigurationAtStartup()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Mfa:MaxFailedAttempts"] = "0" })
            .Build();

        var act = () => new ServiceCollection().AddAuthenticationData(configuration);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("Invalid Mfa configuration: *MaxFailedAttempts*");
    }

    [Fact]
    public async Task MfaAttemptTracking_UsesConfiguredDefaultPolicyAndLockoutThreshold()
    {
        var configurationRepository = new Mock<IUserMfaConfigurationRepository>();
        var attemptRepository = new Mock<IMfaAttemptRepository>();
        configurationRepository
            .Setup(repository => repository.UpdateAsync(It.IsAny<UserMfaConfiguration>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserMfaConfiguration configuration, CancellationToken _) => configuration);
        attemptRepository
            .Setup(repository => repository.CreateAsync(It.IsAny<MfaAttempt>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((MfaAttempt attempt, CancellationToken _) => attempt);

        var options = new MfaOptions { MaxFailedAttempts = 2, LockoutDurationMinutes = 17, RequireMfaByDefault = true };
        var service = new MfaAttemptTrackingService(
            NullLogger<MfaAttemptTrackingService>.Instance,
            configurationRepository.Object,
            attemptRepository.Object,
            new HttpContextAccessor(),
            options);
        var userId = Guid.NewGuid();
        var configuration = new UserMfaConfiguration { UserId = userId, FailedAttempts = 1 };

        (await service.IsMfaRequiredByPolicyAsync(userId)).Should().BeTrue();
        (await service.IsUserLockedOutAsync(userId)).Should().BeFalse();

        await service.RecordFailedMfaAttemptAsync(configuration, MfaMethod.Totp, "Invalid code", null);

        configuration.FailedAttempts.Should().Be(2);
        configuration.LockedOutUntil.Should().BeCloseTo(SystemClock.UtcNow.AddMinutes(17), TimeSpan.FromSeconds(2));
        service.IsLockedOut(configuration).Should().BeTrue();
        configurationRepository.Verify(
            repository => repository.UpdateAsync(configuration, It.IsAny<CancellationToken>()),
            Times.Once);
        attemptRepository.Verify(
            repository => repository.CreateAsync(It.Is<MfaAttempt>(attempt => !attempt.IsSuccessful), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task MfaAttemptTracking_DoesNotRequireMfaWhenGloballyDisabledEvenForAdmins()
    {
        var context = new DefaultHttpContext();
        context.User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Role, "SystemAdmin")], "test"));
        var service = new MfaAttemptTrackingService(
            NullLogger<MfaAttemptTrackingService>.Instance,
            Mock.Of<IUserMfaConfigurationRepository>(),
            Mock.Of<IMfaAttemptRepository>(),
            new HttpContextAccessor { HttpContext = context },
            new MfaOptions { Enabled = false });

        (await service.IsMfaRequiredByPolicyAsync(Guid.NewGuid())).Should().BeFalse();
        (await service.GetMfaStatusAsync(Guid.NewGuid())).Should().BeFalse();
    }

    [Fact]
    public async Task TotpVerification_ExpiresPendingSetupUsingConfiguredDuration()
    {
        var userId = Guid.NewGuid();
        var configuration = new UserMfaConfiguration
        {
            UserId = userId,
            TotpSecretKey = "encrypted-secret",
            BackupCodes = "hashed-codes",
            UpdatedAt = SystemClock.UtcNow.AddMinutes(-11)
        };
        var repository = new Mock<IUserMfaConfigurationRepository>();
        repository.Setup(x => x.GetByUserIdAsync(userId, It.IsAny<CancellationToken>())).ReturnsAsync(configuration);
        repository.Setup(x => x.UpdateAsync(configuration, It.IsAny<CancellationToken>())).ReturnsAsync(configuration);
        var attempts = new Mock<IMfaAttemptTrackingService>();
        attempts.Setup(x => x.RecordMfaAttemptAsync(userId, MfaMethod.Totp, false, "Setup expired", null, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var encryption = new Mock<IEncryptionService>();
        var service = new TotpMfaService(
            NullLogger<TotpMfaService>.Instance,
            repository.Object,
            attempts.Object,
            encryption.Object,
            NativeTotpReplayStub.Create(),
            new MfaOptions { SetupSessionDurationMinutes = 10 });

        var result = await service.VerifyTotpAsync(userId, "123456");

        result.Should().BeFalse();
        configuration.TotpSecretKey.Should().BeNull();
        configuration.BackupCodes.Should().BeNull();
        repository.Verify(x => x.UpdateAsync(configuration, It.IsAny<CancellationToken>()), Times.Once);
        encryption.Verify(x => x.Decrypt(It.IsAny<string>()), Times.Never);
    }
}
