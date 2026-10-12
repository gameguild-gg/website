using System.Security.Cryptography;
using System.Text;
using GameGuild.Configuration.ApplicationLayer;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;
using Microsoft.AspNetCore.Http;

namespace GameGuild.Identity.Authentication.UnitTests.Services;

public sealed class BackupCodeRecoverySecurityTests
{
    [Fact]
    public void DefaultCodesHonorRecordedTwelveCharacterCriterion()
    {
        var service = CreateService(new UserMfaConfiguration { IsEnabled = true });
        Assert.Equal(12, service.GenerateBackupCode().Length);
    }

    [Fact]
    public async Task NewHashesAreSaltedVersionedPbkdf2()
    {
        var service = CreateService(new UserMfaConfiguration { IsEnabled = true });
        const string syntheticCode = "ABCDEFGH2345";
        var first = await service.HashBackupCodeAsync(syntheticCode);
        var second = await service.HashBackupCodeAsync(syntheticCode);
        Assert.StartsWith("pbkdf2-sha256$600000$", first);
        Assert.NotEqual(first, second);
        var parts = first.Split('$');
        var computed = Rfc2898DeriveBytes.Pbkdf2(syntheticCode, Convert.FromBase64String(parts[2]),
            600000, HashAlgorithmName.SHA256, 32);
        Assert.Equal(Convert.FromBase64String(parts[3]), computed);
    }

    [Fact]
    public async Task PendingSetupCannotUseBackupCodeToEnableOrBypassMfa()
    {
        const string syntheticCode = "ABCDEFGH2345";
        var configuration = new UserMfaConfiguration
        {
            UserId = Guid.NewGuid(), IsEnabled = false,
            BackupCodes = Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(syntheticCode)))
        };
        Assert.False(await CreateService(configuration).VerifyBackupCodeAsync(configuration.UserId, syntheticCode));
        Assert.False(configuration.IsEnabled);
    }

    [Fact]
    public async Task LegacySha256CodeRemainsUsableExactlyOnce()
    {
        const string syntheticCode = "ABCDEFGH2345";
        var configuration = new UserMfaConfiguration
        {
            UserId = Guid.NewGuid(), IsEnabled = true,
            BackupCodes = Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(syntheticCode)))
        };
        var service = CreateService(configuration);
        Assert.True(await service.VerifyBackupCodeAsync(configuration.UserId, syntheticCode));
        Assert.False(await service.VerifyBackupCodeAsync(configuration.UserId, syntheticCode));
    }

    [Fact]
    public async Task CancelledHashingDoesNotDeriveOrReturnHash()
    {
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            CreateService(new UserMfaConfiguration()).HashBackupCodeAsync("ABCDEFGH2345", cancelled.Token));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(4)]
    [InlineData(12)]
    [InlineData(20)]
    public async Task IssuedCountSurvivesConsumptionAndConfigurationChanges(int count)
    {
        var configuration = new UserMfaConfiguration { UserId = Guid.NewGuid(), IsEnabled = true };
        var repository = CreateRepository(configuration);
        var options = new MfaOptions { BackupCodesCount = count };
        var service = new BackupCodeMfaService(NullLogger<BackupCodeMfaService>.Instance,
            repository.Object, new Mock<IMfaAttemptTrackingService>().Object, options);
        var codes = await service.GenerateBackupCodesAsync(configuration.UserId);
        Assert.Equal(count, codes.Distinct(StringComparer.Ordinal).Count());
        Assert.All(codes, code => Assert.Equal(12, code.Length));
        Assert.True(configuration.BackupCodes!.Length <= 2000);
        Assert.All(codes, code => Assert.DoesNotContain(code, configuration.BackupCodes));
        options.BackupCodesCount = count == 20 ? 1 : 20;
        Assert.True(await service.VerifyBackupCodeAsync(configuration.UserId, codes[0]));
        var tracking = CreateTracking(repository.Object);
        var status = await tracking.GetMfaConfigurationAsync(configuration.UserId);
        Assert.Equal(count, status.BackupCodesIssued);
        Assert.Equal(count - 1, status.BackupCodesRemaining);
        Assert.False(await service.VerifyBackupCodeAsync(configuration.UserId, codes[0]));
    }

    [Fact]
    public async Task RegenerationInvalidatesAllPreviousCodes()
    {
        var configuration = new UserMfaConfiguration { UserId = Guid.NewGuid(), IsEnabled = true };
        var service = CreateService(configuration);
        var previous = await service.GenerateBackupCodesAsync(configuration.UserId);
        var current = await service.GenerateBackupCodesAsync(configuration.UserId);
        Assert.False(await service.VerifyBackupCodeAsync(configuration.UserId, previous[0]));
        Assert.True(await service.VerifyBackupCodeAsync(configuration.UserId, current[0]));
    }

    [Fact]
    public async Task LegacyStatusDoesNotGuessTotalOrUsedCount()
    {
        var configuration = new UserMfaConfiguration { UserId = Guid.NewGuid(), IsEnabled = true, BackupCodes = "legacy-a,legacy-b" };
        var status = await CreateTracking(CreateRepository(configuration).Object).GetMfaConfigurationAsync(configuration.UserId);
        Assert.Null(status.BackupCodesIssued);
        Assert.Equal(2, status.BackupCodesRemaining);
    }

    [Theory]
    [InlineData("pbkdf2-sha256$1$c2FsdA==$aGFzaA==")]
    [InlineData("pbkdf2-sha256$600000$invalid$invalid")]
    [InlineData("not-a-base64-hash")]
    public async Task MalformedOrDowngradedHashesFailClosed(string hash)
    {
        var configuration = new UserMfaConfiguration { UserId = Guid.NewGuid(), IsEnabled = true, BackupCodes = hash };
        Assert.False(await CreateService(configuration).VerifyBackupCodeAsync(configuration.UserId, "ABCDEFGH2345"));
        Assert.Equal(hash, configuration.BackupCodes);
    }

    private static MfaAttemptTrackingService CreateTracking(IUserMfaConfigurationRepository repository) =>
        new(NullLogger<MfaAttemptTrackingService>.Instance, repository, new Mock<IMfaAttemptRepository>().Object,
            new HttpContextAccessor());

    private static BackupCodeMfaService CreateService(UserMfaConfiguration configuration)
    {
        var repository = CreateRepository(configuration);
        return new BackupCodeMfaService(NullLogger<BackupCodeMfaService>.Instance, repository.Object,
            new Mock<IMfaAttemptTrackingService>().Object, new MfaOptions());
    }

    private static Mock<IUserMfaConfigurationRepository> CreateRepository(UserMfaConfiguration configuration)
    {
        var repository = new Mock<IUserMfaConfigurationRepository>();
        repository.Setup(value => value.GetByUserIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(configuration);
        repository.Setup(value => value.UpdateAsync(It.IsAny<UserMfaConfiguration>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserMfaConfiguration value, CancellationToken _) => value);
        return repository;
    }
}
