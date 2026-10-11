using GameGuild.CQRS;
using GameGuild.Identity.Authentication;
using GameGuild.Identity.Users;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace GameGuild.Identity.Authentication.UnitTests.Services;

public sealed class TimingOriginBaselineTests
{
    private static readonly Lazy<string> PasswordHash = new(() => BCrypt.Net.BCrypt.HashPassword("Synthetic-Correct-1!", 10));

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task CompensationOriginMustIncludeAccountLookupAndCredentialWork(bool accountExists, bool hasPassword)
    {
        var fixture = new Fixture(accountExists, hasPassword);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => fixture.Service.LocalSignInAsync(
            new LocalSignInRequest { Email = "synthetic@example.test", Password = "Synthetic-Wrong-1!" }));
        Assert.NotNull(fixture.FirstLookupAt);
        Assert.NotNull(fixture.CompensationOrigin);
        Assert.True(fixture.CompensationOrigin <= fixture.FirstLookupAt,
            $"Compensation excludes {fixture.CompensationOrigin - fixture.FirstLookupAt} of account-dependent work.");
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task AccountsWithoutUsableLocalPasswordsMustReceiveDummyCredentialWork(bool accountExists, bool hasPassword)
    {
        var fixture = new Fixture(accountExists, hasPassword);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => fixture.Service.LocalSignInAsync(
            new LocalSignInRequest { Email = "synthetic@example.test", Password = "Synthetic-Wrong-1!" }));
        Assert.Equal(accountExists && hasPassword, fixture.RealCredentialWorkWasPerformed);
    }

    [Fact]
    public async Task PolymorphicOriginMustIncludeCandidateResolutionBeforeLocalAuthentication()
    {
        var fixture = new Fixture(true, true);
        fixture.Repository.Setup(repo => repo.FindSignInCandidatesAsync(
            "synthetic", SignInIdentifierType.Username, It.IsAny<CancellationToken>()))
            .Returns(async () =>
            {
                fixture.FirstLookupAt ??= DateTime.UtcNow;
                await Task.Delay(80);
                return (IReadOnlyList<User>)[fixture.Account!];
            });
        var facade = new Mock<IAuthService>();
        facade.Setup(service => service.LocalSignInAsync(It.IsAny<LocalSignInRequest>(), It.IsAny<CancellationToken>()))
            .Returns((LocalSignInRequest request, CancellationToken cancellation) => fixture.Service.LocalSignInAsync(request, cancellation));
        var handler = new PolymorphicSignInHandler(facade.Object, fixture.Repository.Object,
            NullLogger<PolymorphicSignInHandler>.Instance, new PasswordSignInAdmissionStub());
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => handler.Handle(new PolymorphicSignInCommand
        {
            Credential = "synthetic", CredentialType = CredentialType.Username, Password = "Synthetic-Wrong-1!"
        }, CancellationToken.None));
        Assert.NotNull(fixture.FirstLookupAt);
        Assert.NotNull(fixture.CompensationOrigin);
        Assert.True(fixture.CompensationOrigin <= fixture.FirstLookupAt,
            "Public polymorphic resolution is outside the timing origin.");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task UnusableLocalCredentialMustNotBeReportedAsCompletedPasswordWork(bool legacyOversizedInput)
    {
        var fixture = new Fixture(true, true, legacyOversizedInput ? null : "malformed-legacy-hash");
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => fixture.Service.LocalSignInAsync(
            new LocalSignInRequest
            {
                Email = "synthetic@example.test",
                Password = legacyOversizedInput ? new string('p', 73) : "Synthetic-Wrong-1!"
            }));
        Assert.Equal(false, fixture.RealCredentialWorkWasPerformed);
    }

    private sealed class Fixture
    {
        public Mock<IUserRepository> Repository { get; } = new();
        public LocalAuthService Service { get; }
        public User? Account { get; }
        public DateTime? FirstLookupAt { get; set; }
        public DateTime? CompensationOrigin { get; private set; }
        public bool? RealCredentialWorkWasPerformed { get; private set; }

        public Fixture(bool accountExists, bool hasPassword, string? storedHash = null)
        {
            Account = !accountExists ? null : hasPassword
                ? User.CreateWithPassword("synthetic@example.test", "Synthetic", storedHash ?? PasswordHash.Value, "synthetic")
                : User.CreateOAuthUser("synthetic@example.test", "Synthetic");
            Repository.Setup(repo => repo.GetByEmailAsync("synthetic@example.test", It.IsAny<CancellationToken>()))
                .Returns(Lookup);
            Repository.Setup(repo => repo.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .Returns(Lookup);
            var protection = new Mock<IUserEnumerationProtectionService>();
            protection.Setup(service => service.GetGenericErrorMessage("login")).Returns("Synthetic generic credential denial");
            protection.Setup(service => service.AddTimingProtectionDelayAsync(It.IsAny<bool>(), It.IsAny<DateTime>()))
                .Callback((bool work, DateTime origin) => { RealCredentialWorkWasPerformed = work; CompensationOrigin = origin; })
                .Returns(Task.CompletedTask);
            var context = new Mock<IHttpContextAccessor>();
            context.Setup(accessor => accessor.HttpContext).Returns(new DefaultHttpContext());
            var attempts = new Mock<IAuthAttemptService>();
            attempts.Setup(service => service.GetClientIpAddress(It.IsAny<HttpContext>())).Returns("127.0.0.1");
            var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["PasswordPolicy:BCryptWorkFactor"] = "10"
            }).Build();
            Service = new LocalAuthService(Repository.Object, Mock.Of<IRefreshTokenRepository>(),
                Mock.Of<IRefreshTokenLineageRepository>(), Mock.Of<IJwtTokenService>(), Mock.Of<IRefreshTokenHasher>(),
                configuration, attempts.Object, new PasswordHasher(NullLogger<PasswordHasher>.Instance, configuration),
                Mock.Of<IAuthenticationAnomalyDetectionService>(), protection.Object, context.Object,
                NullLogger<LocalAuthService>.Instance, Mock.Of<ISender>(), Mock.Of<ISessionManagementService>(), SignInMfaPreparationStub.Create());
        }

        private async Task<User?> Lookup()
        {
            FirstLookupAt ??= DateTime.UtcNow;
            await Task.Delay(80);
            return Account;
        }
    }
}
