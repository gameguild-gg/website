using System.Diagnostics;
using FluentAssertions;
using GameGuild.CQRS;
using GameGuild.Identity.Users;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace GameGuild.Identity.Authentication.UnitTests.Services;

/// <summary>
///     Executed regressions for the enumeration timing-compensation defects recorded in the
///     2026-10-07 baseline of issue #288: one server-owned monotonic origin captured BEFORE
///     account resolution, credential-work classification replacing user existence, and
///     equivalent dummy work for missing/passwordless/unusable credentials.
/// </summary>
public sealed class CredentialTimingCompensationRegressionTests
{
    private const int LookupDelayMs = 80;
    private const int CandidateResolutionDelayMs = 60;

    private readonly List<string> _order = [];
    private AuthenticationTimingOrigin? _originReturnedToCaller;
    private AuthenticationTimingOrigin? _compensatedScope;
    private RecordingTimeProvider? _clock;
    private CredentialWorkClassification? _compensatedClassification;
    private LocalSignInRequest? _capturedSignInRequest;

    private readonly Mock<IUserRepository> _userRepository = new();
    private readonly Mock<IAuthAttemptService> _authAttemptService = new();
    private readonly Mock<IAuthenticationAnomalyDetectionService> _anomalyDetection = new();
    private readonly Mock<IUserEnumerationProtectionService> _enumerationProtection = new();
    private readonly Mock<IHttpContextAccessor> _httpContextAccessor = new();

    private IPasswordHasher CreateRealHasher()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["PresentationLayer:Authentication:PasswordPolicy:BCryptWorkFactor"] = "10"
            })
            .Build();
        return new PasswordHasher(NullLogger<PasswordHasher>.Instance, configuration);
    }

    private LocalAuthService CreateSut(IPasswordHasher? passwordHasher = null)
    {
        _authAttemptService.Setup(service => service.GetClientIpAddress(It.IsAny<HttpContext>())).Returns("127.0.0.1");

        SetupInstrumentedTimingProtection();

        _anomalyDetection
            .Setup(service => service.AnalyzeLoginAttemptAsync(It.IsAny<AuthenticationAttemptContext>()))
            .ReturnsAsync(new AuthenticationAnomalyResult { RiskLevel = RiskLevel.Low, RiskScore = 0, DetectedAnomalies = new List<string>() });

        return new LocalAuthService(
            _userRepository.Object,
            new Mock<IRefreshTokenRepository>().Object,
            new Mock<IRefreshTokenLineageRepository>().Object,
            new Mock<IJwtTokenService>().Object,
            new Mock<IRefreshTokenHasher>().Object,
            new ConfigurationBuilder().Build(),
            _authAttemptService.Object,
            passwordHasher ?? CreateRealHasher(),
            _anomalyDetection.Object,
            _enumerationProtection.Object,
            _httpContextAccessor.Object,
            NullLogger<LocalAuthService>.Instance,
            new Mock<ISender>().Object,
            new Mock<ISessionManagementService>().Object,
            SignInMfaPreparationStub.Create(),
            timeProvider: _clock);
    }

    private void SetupInstrumentedLookup(User? user)
    {
        _userRepository
            .Setup(repository => repository.GetByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(async () =>
            {
                _originReturnedToCaller = AuthenticationTimingOrigin.GetOrStartForRequest(_httpContextAccessor.Object.HttpContext, _clock);
                _order.Add("lookup");
                await Task.Delay(LookupDelayMs);
                return user;
            });
    }

    private void SetupInstrumentedTimingProtection()
    {
        _clock = new RecordingTimeProvider(_order);
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers.UserAgent = "TimingRegression/1.0";
        _httpContextAccessor.Setup(accessor => accessor.HttpContext).Returns(httpContext);
        _enumerationProtection.As<IAuthenticationTimingProtection>()
            .Setup(protection => protection.CompleteAuthenticationTimingAsync(It.IsAny<AuthenticationTimingOrigin>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .Callback<AuthenticationTimingOrigin, bool, CancellationToken>((scope, workCompleted, _) =>
            {
                _order.Add("compensate");
                _compensatedScope = scope;
                _compensatedClassification = workCompleted ? CredentialWorkClassification.Completed : CredentialWorkClassification.None;
            })
            .Returns(Task.CompletedTask);
        _enumerationProtection
            .Setup(protection => protection.GetGenericErrorMessage(It.IsAny<string>()))
            .Returns("Invalid credentials. Please check your email and password.");
    }

    private sealed class RecordingTimeProvider(List<string> order) : TimeProvider
    {
        private bool started;

        public override long TimestampFrequency => TimeProvider.System.TimestampFrequency;

        public override long GetTimestamp()
        {
            var timestamp = TimeProvider.System.GetTimestamp();
            if (!started)
            {
                started = true;
                order.Add("origin");
            }
            return timestamp;
        }
    }

    private static User CreatePasswordAccount(IPasswordHasher hasher, string password = "CorrectPassword1!")
    {
        return User.CreateWithPassword("password-account@example.test", "password-account", hasher.HashPassword(password));
    }

    // ── Baseline regressions 1-3: the origin precedes account resolution and credential work ──

    [Fact]
    public async Task MissingAccount_OriginPrecedesAccountLookupInsideTheCompensatedWindow()
    {
        SetupInstrumentedLookup(null);
        var sut = CreateSut();

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            sut.LocalSignInAsync(new LocalSignInRequest { Email = "unknown@example.test", Password = "WrongPassword1!" }));

        _order.Should().Equal("origin", "lookup", "compensate");
        _compensatedScope.Should().BeSameAs(_originReturnedToCaller);
        _compensatedScope!.Elapsed.Should().BeGreaterThanOrEqualTo(TimeSpan.FromMilliseconds(LookupDelayMs - 10),
            "the server-owned origin must precede the account lookup so the lookup is compensated");
    }

    [Fact]
    public async Task PasswordlessAccount_OriginPrecedesAccountLookupInsideTheCompensatedWindow()
    {
        var passwordless = User.Create("passwordless@example.test", "Passwordless Account");
        SetupInstrumentedLookup(passwordless);
        var sut = CreateSut();

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            sut.LocalSignInAsync(new LocalSignInRequest { Email = passwordless.Email, Password = "WrongPassword1!" }));

        _order.Should().Equal("origin", "lookup", "compensate");
        _compensatedScope!.Elapsed.Should().BeGreaterThanOrEqualTo(TimeSpan.FromMilliseconds(LookupDelayMs - 10),
            "the server-owned origin must precede the account lookup so the lookup is compensated");
    }

    [Fact]
    public async Task PasswordAccount_OriginPrecedesLookupAndRealBcryptInsideTheCompensatedWindow()
    {
        var hasher = CreateRealHasher();
        var account = CreatePasswordAccount(hasher);
        SetupInstrumentedLookup(account);
        var order = _order;
        var instrumented = new OrderedHasherDecorator(hasher, order);
        var sut = CreateSut(instrumented);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            sut.LocalSignInAsync(new LocalSignInRequest { Email = account.Email, Password = "WrongPassword1!" }));

        _order.Should().Equal("origin", "lookup", "verify", "compensate");
        _compensatedScope!.Elapsed.Should().BeGreaterThanOrEqualTo(TimeSpan.FromMilliseconds(LookupDelayMs - 10),
            "the origin must precede the lookup and the real BCrypt verification");
    }

    // ── Baseline regression 4: public polymorphic candidate resolution is inside the window ──

    [Fact]
    public async Task PolymorphicSignIn_CandidateResolutionRunsAfterTheServerOwnedOrigin()
    {
        var account = User.CreateWithPassword("poly@example.test", "poly-account", CreateRealHasher().HashPassword("CorrectPassword1!"));
        SetupInstrumentedTimingProtection();
        var userRepository = new Mock<IUserRepository>();
        userRepository
            .Setup(repository => repository.FindSignInCandidatesAsync(It.IsAny<string>(), It.IsAny<SignInIdentifierType>(), It.IsAny<CancellationToken>()))
            .Returns(async () =>
            {
                _originReturnedToCaller = AuthenticationTimingOrigin.GetOrStartForRequest(_httpContextAccessor.Object.HttpContext, _clock);
                _order.Add("candidates");
                await Task.Delay(CandidateResolutionDelayMs);
                return (IReadOnlyList<User>)new[] { account };
            });
        var authService = new Mock<IAuthService>();
        authService
            .Setup(service => service.LocalSignInAsync(It.IsAny<LocalSignInRequest>(), It.IsAny<CancellationToken>()))
            .Callback<LocalSignInRequest, CancellationToken>((request, _) => _capturedSignInRequest = request)
            .ThrowsAsync(new UnauthorizedAccessException("Invalid credentials"));

        var handler = new PolymorphicSignInHandler(
            authService.Object,
            userRepository.Object,
            NullLogger<PolymorphicSignInHandler>.Instance,
            new PasswordSignInAdmissionStub(),
            timeProvider: _clock,
            httpContextAccessor: _httpContextAccessor.Object);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            handler.Handle(new PolymorphicSignInCommand { Credential = account.Email, CredentialType = CredentialType.Email, Password = "WrongPassword1!" }, CancellationToken.None));

        _order.Should().Equal("origin", "candidates");
        _capturedSignInRequest.Should().NotBeNull();
        _capturedSignInRequest!.TimingOrigin.Should().BeSameAs(_originReturnedToCaller,
            "the polymorphic entry point must hand its pre-resolution window to the local sign-in service");
        _capturedSignInRequest.TimingOrigin!.Elapsed.Should().BeGreaterThanOrEqualTo(TimeSpan.FromMilliseconds(CandidateResolutionDelayMs - 10),
            "public candidate resolution must run inside the compensated window");
    }

    // ── Baseline regressions 5-7: credential-work classification replacing user existence ──

    [Fact]
    public async Task PasswordlessAccount_RequestsDummyCredentialWork()
    {
        var passwordless = User.Create("passwordless-work@example.test", "Passwordless Work Account");
        SetupInstrumentedLookup(passwordless);
        var sut = CreateSut();

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            sut.LocalSignInAsync(new LocalSignInRequest { Email = passwordless.Email, Password = "WrongPassword1!" }));

        _compensatedClassification.Should().Be(CredentialWorkClassification.None,
            "an existing account without a local password completed no credential work and needs equivalent dummy work");
    }

    [Fact]
    public async Task MissingAccount_RequestsDummyCredentialWork()
    {
        SetupInstrumentedLookup(null);
        var sut = CreateSut();

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            sut.LocalSignInAsync(new LocalSignInRequest { Email = "unknown-work@example.test", Password = "WrongPassword1!" }));

        _compensatedClassification.Should().Be(CredentialWorkClassification.None);
    }

    [Fact]
    public async Task PasswordAccountWithWrongPassword_ClassifiesRealCredentialWork()
    {
        var hasher = CreateRealHasher();
        var account = CreatePasswordAccount(hasher);
        SetupInstrumentedLookup(account);
        var sut = CreateSut(hasher);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            sut.LocalSignInAsync(new LocalSignInRequest { Email = account.Email, Password = "WrongPassword1!" }));

        _compensatedClassification.Should().Be(CredentialWorkClassification.Completed,
            "the real BCrypt verification already performed the credential work inside the window");
    }

    // ── Baseline regressions 8-9 (unusable credentials): structural rejections are not work ──

    [Fact]
    public async Task MalformedStoredHash_RequestsDummyCredentialWork()
    {
        var account = User.CreateWithPassword("malformed@example.test", "malformed-account", "not-a-valid-bcrypt-hash");
        SetupInstrumentedLookup(account);
        var instrumented = new OrderedHasherDecorator(CreateRealHasher(), _order);
        var sut = CreateSut(instrumented);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            sut.LocalSignInAsync(new LocalSignInRequest { Email = account.Email, Password = "WrongPassword1!" }));

        _order.Should().Equal("origin", "lookup", "verify", "compensate");
        _compensatedClassification.Should().Be(CredentialWorkClassification.None,
            "the hasher rejects a malformed stored hash before expensive verification; the caller must not classify it as completed work");
    }

    [Fact]
    public async Task OverlongPasswordAgainstLegacyBcryptRow_RequestsDummyCredentialWork()
    {
        var hasher = CreateRealHasher();
        var account = CreatePasswordAccount(hasher);
        SetupInstrumentedLookup(account);
        var sut = CreateSut(hasher);
        var seventyThreeAsciiBytes = new string('a', 73);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            sut.LocalSignInAsync(new LocalSignInRequest { Email = account.Email, Password = seventyThreeAsciiBytes }));

        _compensatedClassification.Should().Be(CredentialWorkClassification.None,
            "a legacy BCrypt row cannot establish bytes after 72; the hasher returns before expensive verification");
    }

    // ── Sign-up duplicate-email path: origin precedes the existence check, no completed work ──

    [Fact]
    public async Task SignUpDuplicateEmail_OriginPrecedesExistenceCheckAndRequestsDummyWork()
    {
        _userRepository
            .Setup(repository => repository.ExistsByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(async () =>
            {
                _order.Add("exists-check");
                await Task.Delay(LookupDelayMs);
                return true;
            });
        var sut = CreateSut();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sut.LocalSignUpAsync(new LocalSignUpRequest { Email = "duplicate@example.test", Password = "Password1!", Username = "duplicate" }));

        _order.Should().Equal("origin", "exists-check", "compensate");
        _compensatedScope!.Elapsed.Should().BeGreaterThanOrEqualTo(TimeSpan.FromMilliseconds(LookupDelayMs - 10));
        _compensatedClassification.Should().Be(CredentialWorkClassification.None,
            "the duplicate-email path performed no credential work while the fresh path hashes the password");
    }

    /// <summary>Delegating hasher that records verification order for window-containment assertions.</summary>
    private sealed class OrderedHasherDecorator(IPasswordHasher inner, List<string> order) : IPasswordHasher, IPasswordVerificationWork
    {
        public string HashPassword(string password) => inner.HashPassword(password);

        public bool VerifyPassword(string hashedPassword, string providedPassword) => inner.VerifyPassword(hashedPassword, providedPassword);

        public PasswordVerificationResult VerifyPasswordWithWorkClassification(string hashedPassword, string providedPassword)
        {
            order.Add("verify");
            return inner.VerifyPasswordWithWorkClassification(hashedPassword, providedPassword);
        }

        public PasswordVerificationResult VerifyPasswordWithWork(string hashedPassword, string providedPassword) =>
            VerifyPasswordWithWorkClassification(hashedPassword, providedPassword);

        public bool NeedsUpgrade(string hashedPassword) => inner.NeedsUpgrade(hashedPassword);

        public PasswordStrengthResult ValidatePasswordStrength(string password) => inner.ValidatePasswordStrength(password);

        public Task PerformDummyVerificationAsync(CancellationToken cancellationToken) => inner.PerformDummyVerificationAsync(cancellationToken);
    }
}

/// <summary>
///     Real-service behavior of the compensation itself: dummy verification only for windows
///     without completed credential work, at the configured work factor, with the fixed target
///     floor retained and no existence-dependent jitter.
/// </summary>
public sealed class UserEnumerationProtectionCompensationTests
{
    private const int ConfiguredWorkFactor = 10;

    private static IPasswordHasher CreateRealHasher()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["PresentationLayer:Authentication:PasswordPolicy:BCryptWorkFactor"] = ConfiguredWorkFactor.ToString()
            })
            .Build();
        return new PasswordHasher(NullLogger<PasswordHasher>.Instance, configuration);
    }

    private static UserEnumerationProtectionService CreateSut(IPasswordHasher? hasher = null)
    {
        return new UserEnumerationProtectionService(
            NullLogger<UserEnumerationProtectionService>.Instance,
            new Microsoft.Extensions.Caching.Memory.MemoryCache(Microsoft.Extensions.Options.Options.Create(
                new Microsoft.Extensions.Caching.Memory.MemoryCacheOptions())),
            hasher ?? CreateRealHasher());
    }

    // A deterministic aged monotonic clock rules out the fixed top-up delay. Runner
    // scheduling cannot turn the presence/absence of dummy work into a duration ratio.
    private sealed class AgedClock : TimeProvider
    {
        private long timestamp;
        public override long TimestampFrequency => TimeSpan.TicksPerSecond;
        public override long GetTimestamp() => timestamp;
        public void Age() => timestamp += TimeSpan.FromMilliseconds(450).Ticks;
        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period) =>
            throw new InvalidOperationException("An already-aged compensation window must not create a timer.");
    }

    private sealed class DummyCostLogger : Microsoft.Extensions.Logging.ILogger<PasswordHasher>
    {
        public List<int> Costs { get; } = [];
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(Microsoft.Extensions.Logging.LogLevel level) => true;
        public void Log<TState>(Microsoft.Extensions.Logging.LogLevel level, Microsoft.Extensions.Logging.EventId eventId,
            TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (state is IEnumerable<KeyValuePair<string, object?>> values)
            {
                foreach (var pair in values)
                {
                    if (pair.Key == "WorkFactor" && pair.Value is int factor) { Costs.Add(factor); }
                }
            }
        }
    }

    private static AuthenticationTimingScope CreatePreAgedScope()
    {
        var clock = new AgedClock();
        var scope = new AuthenticationTimingScope(AuthenticationTimingOrigin.Start(clock));
        clock.Age();
        return scope;
    }

    [Fact]
    public async Task NoCredentialWork_RunsDummyVerificationAtTheConfiguredWorkFactor()
    {
        var logger = new DummyCostLogger();
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["PresentationLayer:Authentication:PasswordPolicy:BCryptWorkFactor"] = ConfiguredWorkFactor.ToString()
        }).Build();
        var hasher = new PasswordHasher(logger, configuration);
        var sut = CreateSut(hasher);

        await sut.AddTimingProtectionDelayAsync(CreatePreAgedScope(), CredentialWorkClassification.None);

        logger.Costs.Should().Equal(ConfiguredWorkFactor,
            "the real hasher must complete exactly one dummy operation at the configured factor");
    }

    [Fact]
    public async Task CompletedCredentialWork_RunsNoDummyVerification()
    {
        var hasher = new Mock<IPasswordHasher>(MockBehavior.Strict);
        var sut = CreateSut(hasher.Object);

        await sut.AddTimingProtectionDelayAsync(CreatePreAgedScope(), CredentialWorkClassification.Completed);

        hasher.Verify(item => item.PerformDummyVerificationAsync(It.IsAny<CancellationToken>()), Times.Never);
        hasher.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task FreshWindow_IsToppedUpToTheTargetProcessingTime()
    {
        var sut = CreateSut();
        var scope = new AuthenticationTimingScope();

        var stopwatch = Stopwatch.StartNew();
        await sut.AddTimingProtectionDelayAsync(scope, CredentialWorkClassification.Completed);
        stopwatch.Stop();

        stopwatch.Elapsed.Should().BeGreaterThanOrEqualTo(sut.GetBaseProcessingTime() - TimeSpan.FromMilliseconds(30),
            "the fixed target floor from the server-owned origin is preserved");
    }

    [Fact]
    public async Task DummyVerificationDelegatesToTheHasherExactlyOncePerUncompensatedWindow()
    {
        var hasher = new Mock<IPasswordHasher>();
        hasher.Setup(item => item.PerformDummyVerificationAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        var sut = CreateSut(hasher.Object);

        await sut.AddTimingProtectionDelayAsync(new AuthenticationTimingScope(), CredentialWorkClassification.None);

        hasher.Verify(item => item.PerformDummyVerificationAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}

/// <summary>
///     Work-classification contract of the hasher itself: every rejection taken before the
///     underlying KDF reports that no cryptographic work ran.
/// </summary>
public sealed class PasswordVerificationWorkClassificationTests
{
    private static IPasswordHasher CreateSut()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["PresentationLayer:Authentication:PasswordPolicy:BCryptWorkFactor"] = "10"
            })
            .Build();
        return new PasswordHasher(NullLogger<PasswordHasher>.Instance, configuration);
    }

    [Fact]
    public void MalformedStoredHash_PerformsNoCryptographicWork()
    {
        var outcome = CreateSut().VerifyPasswordWithWorkClassification("not-a-valid-bcrypt-hash", "WrongPassword1!");

        outcome.IsValid.Should().BeFalse();
        outcome.PerformedCryptographicWork.Should().BeFalse();
    }

    [Fact]
    public void OverlongInputAgainstLegacyBcryptRow_PerformsNoCryptographicWork()
    {
        var hash = BCrypt.Net.BCrypt.HashPassword("CorrectPassword1!", 10);
        var outcome = CreateSut().VerifyPasswordWithWorkClassification(hash, new string('a', 73));

        outcome.IsValid.Should().BeFalse();
        outcome.PerformedCryptographicWork.Should().BeFalse();
    }

    [Fact]
    public void WrongPasswordAgainstValidBcryptRow_PerformsCryptographicWork()
    {
        var hash = BCrypt.Net.BCrypt.HashPassword("CorrectPassword1!", 10);
        var outcome = CreateSut().VerifyPasswordWithWorkClassification(hash, "WrongPassword1!");

        outcome.IsValid.Should().BeFalse();
        outcome.PerformedCryptographicWork.Should().BeTrue();
    }

    [Fact]
    public void CorrectPasswordAgainstValidBcryptRow_PerformsCryptographicWorkAndSucceeds()
    {
        var hash = BCrypt.Net.BCrypt.HashPassword("CorrectPassword1!", 10);
        var outcome = CreateSut().VerifyPasswordWithWorkClassification(hash, "CorrectPassword1!");

        outcome.IsValid.Should().BeTrue();
        outcome.PerformedCryptographicWork.Should().BeTrue();
    }

    [Fact]
    public void EmptyInputs_PerformNoCryptographicWork()
    {
        var sut = CreateSut();
        sut.VerifyPasswordWithWorkClassification("", "password").PerformedCryptographicWork.Should().BeFalse();
        sut.VerifyPasswordWithWorkClassification(BCrypt.Net.BCrypt.HashPassword("x", 10), "").PerformedCryptographicWork.Should().BeFalse();
    }

    [Fact]
    public void VerifyPassword_PreservesItsBooleanContract()
    {
        var hash = BCrypt.Net.BCrypt.HashPassword("CorrectPassword1!", 10);
        var sut = CreateSut();
        sut.VerifyPassword(hash, "CorrectPassword1!").Should().BeTrue();
        sut.VerifyPassword(hash, "WrongPassword1!").Should().BeFalse();
        sut.VerifyPassword("malformed", "WrongPassword1!").Should().BeFalse();
        sut.VerifyPassword(hash, new string('a', 73)).Should().BeFalse();
    }

    [Fact]
    public async Task PerformDummyVerification_CompletesAtTheConfiguredWorkFactor()
    {
        var sut = (PasswordHasher)CreateSut();

        var hash = await sut.GenerateDummyCredentialHashAsync(CancellationToken.None);

        hash.Split('

    [Fact]
    public async Task PerformDummyVerification_ExplicitCancellationPreventsWork()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var sut = CreateSut();

        Func<Task> performWork = () => sut.PerformDummyVerificationAsync(cancellation.Token);

        await performWork.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task PerformDummyVerification_ConcreteNoArgumentOverloadCompletes()
    {
        var sut = (PasswordHasher)CreateSut();

        await sut.PerformDummyVerificationAsync();
    }
}
)[2].Should().Be("10");
        BCrypt.Net.BCrypt.Verify(PasswordHasher.DummyCredentialMaterial, hash).Should().BeTrue();
    }

    [Fact]
    public async Task DummyCredentialHash_UsesReloadedPolicyAndReturnsRealBcryptOutput()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["PresentationLayer:Authentication:PasswordPolicy:BCryptWorkFactor"] = "10"
        }).Build();
        var sut = new PasswordHasher(NullLogger<PasswordHasher>.Instance, configuration);
        var first = await sut.GenerateDummyCredentialHashAsync(CancellationToken.None);
        configuration["PresentationLayer:Authentication:PasswordPolicy:BCryptWorkFactor"] = "11";
        configuration.Reload();
        var second = await sut.GenerateDummyCredentialHashAsync(CancellationToken.None);

        first.Split('

    [Fact]
    public async Task PerformDummyVerification_ExplicitCancellationPreventsWork()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var sut = CreateSut();

        Func<Task> performWork = () => sut.PerformDummyVerificationAsync(cancellation.Token);

        await performWork.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task PerformDummyVerification_ConcreteNoArgumentOverloadCompletes()
    {
        var sut = (PasswordHasher)CreateSut();

        await sut.PerformDummyVerificationAsync();
    }
}
)[2].Should().Be("10"); second.Split('

    [Fact]
    public async Task PerformDummyVerification_ExplicitCancellationPreventsWork()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var sut = CreateSut();

        Func<Task> performWork = () => sut.PerformDummyVerificationAsync(cancellation.Token);

        await performWork.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task PerformDummyVerification_ConcreteNoArgumentOverloadCompletes()
    {
        var sut = (PasswordHasher)CreateSut();

        await sut.PerformDummyVerificationAsync();
    }
}
)[2].Should().Be("11");
        BCrypt.Net.BCrypt.Verify(PasswordHasher.DummyCredentialMaterial, first).Should().BeTrue();
        BCrypt.Net.BCrypt.Verify(PasswordHasher.DummyCredentialMaterial, second).Should().BeTrue();
    }

    [Fact]
    public async Task PerformDummyVerification_ExplicitCancellationPreventsWork()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var sut = CreateSut();

        Func<Task> performWork = () => sut.PerformDummyVerificationAsync(cancellation.Token);

        await performWork.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task PerformDummyVerification_ConcreteNoArgumentOverloadCompletes()
    {
        var sut = (PasswordHasher)CreateSut();

        await sut.PerformDummyVerificationAsync();
    }
}
