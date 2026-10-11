using GameGuild.CQRS;
using GameGuild.Identity.Users;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace GameGuild.Identity.Authentication.UnitTests.Services;

public sealed class AuthenticationTimingBoundaryTests
{
    [Fact]
    public async Task AlreadyCancelledRequestDoesNotLookupRecordOrCompensate()
    {
        var fixture = new Fixture();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            fixture.Service.LocalSignInAsync(Request(), new CancellationToken(true)));
        Assert.Empty(fixture.Events);
        fixture.Users.Verify(service => service.GetByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task LookupCancellationPropagatesWithoutASecondAttemptOrCompensation()
    {
        var fixture = new Fixture();
        using var cancellation = new CancellationTokenSource();
        fixture.Users.Setup(service => service.GetByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns((string _, CancellationToken token) =>
            {
                cancellation.Cancel();
                return Task.FromCanceled<User?>(token);
            });
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            fixture.Service.LocalSignInAsync(Request(), cancellation.Token));
        Assert.Empty(fixture.Events);
    }

    [Fact]
    public async Task CancelledLookupReturningAnAccountCannotStartCredentialVerification()
    {
        using var cancellation = new CancellationTokenSource();
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["PasswordPolicy:BCryptWorkFactor"] = "10"
        }).Build();
        var hasher = new PasswordHasher(NullLogger<PasswordHasher>.Instance, configuration);
        var provider = new CancellingPasswordProvider(hasher, cancellation);
        var fixture = new Fixture(provider);
        var user = User.CreateWithPassword("synthetic@example.test", "Synthetic", hasher.HashPassword("Synthetic-Correct-1!"));
        fixture.Users.Setup(service => service.GetByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns((string _, CancellationToken _) =>
            {
                cancellation.Cancel();
                return Task.FromResult<User?>(user);
            });
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => fixture.Service.LocalSignInAsync(Request(), cancellation.Token));
        Assert.False(provider.Result.WorkPerformed);
        Assert.Empty(fixture.Events);
    }

    [Fact]
    public async Task CancellationAfterActualCredentialWorkPropagatesBeforeFurtherAuthenticationWork()
    {
        using var cancellation = new CancellationTokenSource();
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["PasswordPolicy:BCryptWorkFactor"] = "10"
        }).Build();
        var hasher = new PasswordHasher(NullLogger<PasswordHasher>.Instance, configuration);
        var provider = new CancellingPasswordProvider(hasher, cancellation);
        var fixture = new Fixture(provider);
        fixture.Users.Setup(service => service.GetByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(User.CreateWithPassword("synthetic@example.test", "Synthetic", hasher.HashPassword("Synthetic-Correct-1!")));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => fixture.Service.LocalSignInAsync(Request(), cancellation.Token));

        Assert.True(provider.Result.WorkPerformed);
        Assert.False(provider.Result.IsValid);
        Assert.Empty(fixture.Events);
    }

    [Fact]
    public async Task CancellationInCompensationIsNotConvertedToCredentialDenial()
    {
        var fixture = new Fixture();
        using var cancellation = new CancellationTokenSource();
        fixture.Protection.Completion = (_, token) =>
        {
            cancellation.Cancel();
            return Task.FromCanceled(token);
        };
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            fixture.Service.LocalSignInAsync(Request(), cancellation.Token));
        Assert.Equal(new[] { "attempt:InvalidCredentials", "risk", "timing" }, fixture.Events);
        Assert.Equal(1, fixture.Protection.CallCount);
    }

    [Fact]
    public async Task FailedAttemptStoreDoesNotBypassRiskAnalysisOrCompensation()
    {
        var fixture = new Fixture();
        fixture.Attempts.Setup(service => service.RecordFailedAttemptAsync(It.IsAny<string>(), It.IsAny<Guid?>(),
                It.IsAny<string>(), It.IsAny<string?>(), "InvalidCredentials", It.IsAny<TimeSpan>()))
            .Callback(() => fixture.Events.Add("attempt:failed-store"))
            .ThrowsAsync(new InvalidOperationException("Synthetic attempt store unavailable"));
        var denial = await Assert.ThrowsAsync<UnauthorizedAccessException>(() => fixture.Service.LocalSignInAsync(Request()));
        Assert.Equal(ObservingProtection.GenericDenial, denial.Message);
        Assert.Equal(new[] { "attempt:failed-store", "risk", "timing" }, fixture.Events);
        Assert.False(fixture.Protection.WorkPerformed);
    }

    [Fact]
    public async Task RiskAnalysisFailureStillCompletesTheGenericDenialTiming()
    {
        var fixture = new Fixture();
        fixture.Risk.Setup(service => service.AnalyzeLoginAttemptAsync(It.IsAny<AuthenticationAttemptContext>()))
            .Callback(() => fixture.Events.Add("risk:unavailable"))
            .ThrowsAsync(new InvalidOperationException("Synthetic classifier unavailable"));
        var denial = await Assert.ThrowsAsync<UnauthorizedAccessException>(() => fixture.Service.LocalSignInAsync(Request()));
        Assert.Equal(ObservingProtection.GenericDenial, denial.Message);
        Assert.Equal(new[] { "attempt:InvalidCredentials", "risk:unavailable", "timing" }, fixture.Events);
    }

    [Fact]
    public async Task ThreatAuditPrecedesDenialCompensation()
    {
        var fixture = new Fixture();
        fixture.Risk.Setup(service => service.AnalyzeLoginAttemptAsync(It.IsAny<AuthenticationAttemptContext>()))
            .Callback(() => fixture.Events.Add("risk"))
            .ReturnsAsync(new AuthenticationAnomalyResult
            {
                IsSuspicious = true, RiskLevel = RiskLevel.High, DetectedAnomalies = ["SyntheticRisk"]
            });
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => fixture.Service.LocalSignInAsync(Request()));
        Assert.Equal(new[] { "attempt:InvalidCredentials", "risk", "siem", "audit", "timing" }, fixture.Events);
        Assert.Equal("Authentication.ThreatDetected", Assert.Single(fixture.AuditEvents).ActionType);
    }

    [Fact]
    public async Task UnexpectedLookupFailureReceivesOneCompensationFromBeforeLookup()
    {
        var fixture = new Fixture();
        DateTime? lookupAt = null;
        fixture.Users.Setup(service => service.GetByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback(() => lookupAt = DateTime.UtcNow)
            .ThrowsAsync(new InvalidOperationException("Synthetic account lookup unavailable"));
        var denial = await Assert.ThrowsAsync<UnauthorizedAccessException>(() => fixture.Service.LocalSignInAsync(Request()));
        Assert.Equal(ObservingProtection.GenericDenial, denial.Message);
        Assert.Equal(new[] { "attempt:SystemError", "timing" }, fixture.Events);
        Assert.Equal(1, fixture.Protection.CallCount);
        Assert.False(fixture.Protection.WorkPerformed);
        Assert.True(fixture.Protection.Origin!.StartedAtUtc <= lookupAt);
    }

    [Fact]
    public async Task LookupAndAttemptStoreFailuresStillCompensateAndReturnTheGenericDenial()
    {
        var fixture = new Fixture();
        fixture.Users.Setup(service => service.GetByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Synthetic account lookup unavailable"));
        fixture.Attempts.Setup(service => service.RecordFailedAttemptAsync(It.IsAny<string>(), It.IsAny<Guid?>(),
                It.IsAny<string>(), It.IsAny<string?>(), "SystemError", It.IsAny<TimeSpan>()))
            .Callback(() => fixture.Events.Add("attempt:failed-store"))
            .ThrowsAsync(new InvalidOperationException("Synthetic attempt store unavailable"));
        var denial = await Assert.ThrowsAsync<UnauthorizedAccessException>(() => fixture.Service.LocalSignInAsync(Request()));
        Assert.Equal(ObservingProtection.GenericDenial, denial.Message);
        Assert.Equal(new[] { "attempt:failed-store", "timing" }, fixture.Events);
        Assert.Equal(1, fixture.Protection.CallCount);
    }

    [Fact]
    public async Task FailedTimingCompensationIsNotRepeatedOrReportedAsCompletedWork()
    {
        var fixture = new Fixture();
        fixture.Protection.Completion = (_, _) => Task.FromException(new InvalidOperationException("Synthetic hash policy failure"));
        var denial = await Assert.ThrowsAsync<UnauthorizedAccessException>(() => fixture.Service.LocalSignInAsync(Request()));
        Assert.Equal(ObservingProtection.GenericDenial, denial.Message);
        Assert.Equal(new[] { "attempt:InvalidCredentials", "risk", "timing", "attempt:SystemError" }, fixture.Events);
        Assert.Equal(1, fixture.Protection.CallCount);
        Assert.False(fixture.Protection.WorkPerformed);
    }

    [Fact]
    public async Task OpaqueLegacyPasswordProviderCannotClaimThatCredentialWorkCompleted()
    {
        var fixture = new Fixture();
        fixture.Users.Setup(service => service.GetByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(User.CreateWithPassword("synthetic@example.test", "Synthetic", "opaque-provider-record"));
        fixture.Hasher.Setup(service => service.VerifyPassword("opaque-provider-record", "Synthetic-Wrong-1!"))
            .Returns(false);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => fixture.Service.LocalSignInAsync(Request()));
        fixture.Hasher.Verify(service => service.VerifyPassword("opaque-provider-record", "Synthetic-Wrong-1!"), Times.Once);
        Assert.False(fixture.Protection.WorkPerformed);
        Assert.Equal(1, fixture.Protection.CallCount);
    }

    private static LocalSignInRequest Request() => new()
    {
        Email = "synthetic@example.test", Password = "Synthetic-Wrong-1!"
    };

    private sealed class Fixture
    {
        public List<string> Events { get; } = [];
        public List<AuthenticationAuditEvent> AuditEvents { get; } = [];
        public Mock<IUserRepository> Users { get; } = new();
        public Mock<IAuthAttemptService> Attempts { get; } = new();
        public Mock<IAuthenticationAnomalyDetectionService> Risk { get; } = new();
        public Mock<IPasswordHasher> Hasher { get; } = new();
        public ObservingProtection Protection { get; }
        public LocalAuthService Service { get; }

        public Fixture(IPasswordHasher? passwordProvider = null)
        {
            Users.Setup(service => service.GetByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync((User?)null);
            Attempts.Setup(service => service.GetClientIpAddress(It.IsAny<HttpContext>())).Returns("127.0.0.1");
            Attempts.Setup(service => service.RecordFailedAttemptAsync(It.IsAny<string>(), It.IsAny<Guid?>(),
                    It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string>(), It.IsAny<TimeSpan>()))
                .Callback((string _, Guid? _, string _, string? _, string reason, TimeSpan _) => Events.Add("attempt:" + reason))
                .Returns(Task.CompletedTask);
            Risk.Setup(service => service.AnalyzeLoginAttemptAsync(It.IsAny<AuthenticationAttemptContext>()))
                .Callback(() => Events.Add("risk"))
                .ReturnsAsync(new AuthenticationAnomalyResult { RiskLevel = RiskLevel.Low });
            Risk.Setup(service => service.RecordSuspiciousActivityAsync(It.IsAny<SuspiciousActivity>()))
                .Callback(() => Events.Add("siem"))
                .Returns(Task.CompletedTask);
            var audit = new Mock<IAuthenticationAuditEventSink>();
            audit.Setup(service => service.RecordAsync(It.IsAny<AuthenticationAuditEvent>(), It.IsAny<CancellationToken>()))
                .Callback((AuthenticationAuditEvent value, CancellationToken _) => { AuditEvents.Add(value); Events.Add("audit"); })
                .Returns(Task.CompletedTask);
            var context = new HttpContextAccessor { HttpContext = new DefaultHttpContext() };
            Protection = new ObservingProtection(Events);
            Service = new LocalAuthService(Users.Object, Mock.Of<IRefreshTokenRepository>(), Mock.Of<IRefreshTokenLineageRepository>(),
                Mock.Of<IJwtTokenService>(), Mock.Of<IRefreshTokenHasher>(), new ConfigurationBuilder().Build(), Attempts.Object,
                passwordProvider ?? Hasher.Object, Risk.Object, Protection, context, NullLogger<LocalAuthService>.Instance,
                Mock.Of<ISender>(), Mock.Of<ISessionManagementService>(), SignInMfaPreparationStub.Create(), auditEventSink: audit.Object);
        }
    }

    private sealed class CancellingPasswordProvider(PasswordHasher hasher, CancellationTokenSource cancellation) : IPasswordHasher, IPasswordVerificationWork
    {
        public PasswordVerificationResult Result { get; private set; }
        public PasswordVerificationResult VerifyPasswordWithWork(string hashedPassword, string providedPassword)
        {
            Result = hasher.VerifyPasswordWithWork(hashedPassword, providedPassword);
            cancellation.Cancel();
            return Result;
        }

        public bool VerifyPassword(string hashedPassword, string providedPassword) => VerifyPasswordWithWork(hashedPassword, providedPassword).IsValid;
        public string HashPassword(string password) => hasher.HashPassword(password);
        public bool NeedsUpgrade(string hashedPassword) => hasher.NeedsUpgrade(hashedPassword);
        public PasswordStrengthResult ValidatePasswordStrength(string password) => hasher.ValidatePasswordStrength(password);
    }

    private sealed class ObservingProtection(List<string> events) : IUserEnumerationProtectionService, IAuthenticationTimingProtection
    {
        public const string GenericDenial = "Synthetic generic denial";
        public AuthenticationTimingOrigin? Origin { get; private set; }
        public bool WorkPerformed { get; private set; }
        public int CallCount { get; private set; }
        public Func<AuthenticationTimingOrigin, CancellationToken, Task>? Completion { get; set; }

        public Task CompleteAuthenticationTimingAsync(AuthenticationTimingOrigin origin, bool credentialWorkCompleted, CancellationToken cancellationToken = default)
        {
            Origin = origin;
            WorkPerformed = credentialWorkCompleted;
            CallCount++;
            events.Add("timing");
            return Completion?.Invoke(origin, cancellationToken) ?? Task.CompletedTask;
        }

        string IUserEnumerationProtectionService.GetGenericErrorMessage(string context)
        {
            _ = context;
            return GenericDenial;
        }
        Task IUserEnumerationProtectionService.AddTimingProtectionDelayAsync(bool userExists, DateTime startTime)
        {
            _ = userExists;
            _ = startTime;
            throw new InvalidOperationException("The active host must use the monotonic capability.");
        }
        Task<ThrottleDecision> IUserEnumerationProtectionService.ShouldThrottleAsync(string identifier)
        {
            _ = identifier;
            return Task.FromResult(new ThrottleDecision());
        }
        Task IUserEnumerationProtectionService.RecordEnumerationAttemptAsync(string identifier, string ipAddress)
        {
            _ = identifier;
            _ = ipAddress;
            return Task.CompletedTask;
        }
    }
}
