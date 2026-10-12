using System.Buffers.Binary;
using System.Security.Cryptography;
using GameGuild.Configuration.ApplicationLayer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace GameGuild.Identity.Authentication.UnitTests.Services;

public sealed class TotpRecoverySecurityTests
{
    [Fact]
    public async Task SetupEncryptsRandomSecretAndConfirmationCompletesEnrollment()
    {
        UserMfaConfiguration? stored = null;
        var repository = new Mock<IUserMfaConfigurationRepository>();
        repository.Setup(value => value.GetByUserIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => stored);
        repository.Setup(value => value.CreateAsync(It.IsAny<UserMfaConfiguration>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserMfaConfiguration row, CancellationToken _) => stored = row);
        repository.Setup(value => value.UpdateAsync(It.IsAny<UserMfaConfiguration>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserMfaConfiguration row, CancellationToken _) => row);
        var encryption = CreateEncryption();
        var service = CreateService(repository.Object, encryption);
        var userId = Guid.NewGuid();
        var setup = await service.SetupTotpAsync(userId, "synthetic+recovery@example.test");
        Assert.Equal(32, setup.SecretKey.Length);
        Assert.All(setup.SecretKey, character => Assert.Contains(character, "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567"));
        Assert.NotEqual(setup.SecretKey, stored!.TotpSecretKey);
        Assert.Equal(setup.SecretKey, encryption.Decrypt(stored.TotpSecretKey!));
        Assert.Contains("algorithm=SHA1&digits=6&period=30", setup.QrCodeUri);
        Assert.Contains("synthetic%2Brecovery%40example.test", setup.QrCodeUri);
        Assert.False(stored.IsEnabled);
        Assert.False(stored.IsSetupComplete);
        Assert.NotNull(stored.SetupExpiresAt);
        Assert.True(await service.VerifyTotpAsync(userId, IndependentTotp(setup.SecretKey, 0)));
        Assert.True(stored.IsEnabled);
        Assert.True(stored.IsSetupComplete);
        Assert.Null(stored.SetupExpiresAt);
        Assert.NotNull(stored.EnabledAt);
        Assert.NotNull(stored.LastUsedAt);
    }

    [Theory]
    [InlineData(-1, true)]
    [InlineData(0, true)]
    [InlineData(1, true)]
    [InlineData(-5, false)]
    [InlineData(5, false)]
    public async Task IndependentAuthenticatorCodesFollowRecordedThirtySecondWindow(int offset, bool accepted)
    {
        var secret = CreateSyntheticTotpKey();
        var encryption = CreateEncryption();
        var row = new UserMfaConfiguration { UserId = Guid.NewGuid(), IsEnabled = true, TotpSecretKey = encryption.Encrypt(secret) };
        var repository = CreateRepository(row);
        Assert.Equal(accepted, await CreateService(repository.Object, encryption).VerifyTotpAsync(row.UserId, IndependentTotp(secret, offset)));
    }

    [Fact]
    public async Task ExpiredPendingSetupClearsSecretAndRecoveryCodes()
    {
        var secret = CreateSyntheticTotpKey();
        var encryption = CreateEncryption();
        var row = new UserMfaConfiguration
        {
            UserId = Guid.NewGuid(), TotpSecretKey = encryption.Encrypt(secret), BackupCodes = "legacy-code",
            UpdatedAt = DateTime.UtcNow.AddMinutes(-11)
        };
        Assert.False(await CreateService(CreateRepository(row).Object, encryption).VerifyTotpAsync(row.UserId, IndependentTotp(secret, 0)));
        Assert.Null(row.TotpSecretKey);
        Assert.Null(row.BackupCodes);
        Assert.False(row.IsEnabled);
    }

    [Fact]
    public async Task ProvisioningUriRendersPngWithoutClaimingExternalAuthenticatorAcceptance()
    {
        var service = CreateService(new Mock<IUserMfaConfigurationRepository>().Object, CreateEncryption());
        var provisioningKey = CreateSyntheticTotpKey();
        var image = await service.GenerateQrCodeAsync($"otpauth://totp/Test:synthetic%40example.test?secret={provisioningKey}&issuer=Test&algorithm=SHA1&digits=6&period=30");
        Assert.Equal(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }, image[..8]);
    }

    [Fact]
    public async Task RecentFailedAttemptCannotExtendAnExpiredEnrollment()
    {
        var secret = CreateSyntheticTotpKey();
        var encryption = CreateEncryption();
        var row = new UserMfaConfiguration
        {
            UserId = Guid.NewGuid(), TotpSecretKey = encryption.Encrypt(secret),
            UpdatedAt = DateTime.UtcNow, SetupExpiresAt = DateTime.UtcNow.AddSeconds(-1)
        };
        Assert.False(await CreateService(CreateRepository(row).Object, encryption).VerifyTotpAsync(row.UserId, IndependentTotp(secret, 0)));
        Assert.False(row.IsEnabled);
        Assert.Null(row.TotpSecretKey);
    }

    private static EncryptionService CreateEncryption() => new(NullLogger<EncryptionService>.Instance,
        new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Encryption:Key"] = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
        }).Build());

    private static TotpMfaService CreateService(IUserMfaConfigurationRepository repository, IEncryptionService encryption) =>
        new(NullLogger<TotpMfaService>.Instance, repository, new Mock<IMfaAttemptTrackingService>().Object,
            encryption, NativeTotpReplayStub.Create(), new MfaOptions());

    private static Mock<IUserMfaConfigurationRepository> CreateRepository(UserMfaConfiguration row)
    {
        var repository = new Mock<IUserMfaConfigurationRepository>();
        repository.Setup(value => value.GetByUserIdAsync(row.UserId, It.IsAny<CancellationToken>())).ReturnsAsync(row);
        repository.Setup(value => value.UpdateAsync(It.IsAny<UserMfaConfiguration>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserMfaConfiguration value, CancellationToken _) => value);
        return repository;
    }

    private static string IndependentTotp(string base32, int offset)
    {
        const string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
        var key = new List<byte>();
        var buffer = 0;
        var bits = 0;
        foreach (var character in base32)
        {
            buffer = (buffer << 5) | alphabet.IndexOf(character);
            bits += 5;
            if (bits >= 8) { bits -= 8; key.Add((byte)(buffer >> bits)); }
        }
        var counter = new byte[8];
        BinaryPrimitives.WriteInt64BigEndian(counter, DateTimeOffset.UtcNow.ToUnixTimeSeconds() / 30 + offset);
        var digest = HMACSHA1.HashData(key.ToArray(), counter);
        var start = digest[^1] & 15;
        var value = BinaryPrimitives.ReadInt32BigEndian(digest.AsSpan(start, 4)) & int.MaxValue;
        return (value % 1000000).ToString("D6", System.Globalization.CultureInfo.InvariantCulture);
    }

    private static string CreateSyntheticTotpKey()
    {
        const string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
        return new string(Enumerable.Range(0, 32)
            .Select(_ => alphabet[RandomNumberGenerator.GetInt32(alphabet.Length)]).ToArray());
    }
}
