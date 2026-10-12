using GameGuild.Identity.Users;
using Moq;
using Xunit;

namespace GameGuild.Identity.Authentication.UnitTests.Mappings;

public sealed class AuthenticationMappingsContractTests
{
    private static readonly DateTime Now = new(2030, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    [Theory]
    [InlineData("Matheus Luiz Martins", "Matheus", "Luiz Martins")]
    [InlineData("  Matheus   Luiz\tMartins  ", "Matheus", "Luiz Martins")]
    [InlineData("Ana\u00a0Maria da Silva", "Ana", "Maria da Silva")]
    [InlineData("María del Carmen", "María", "del Carmen")]
    [InlineData("Jean-Luc O'Neill", "Jean-Luc", "O'Neill")]
    [InlineData("Madonna", "Madonna", null)]
    [InlineData("   ", null, null)]
    [InlineData("", null, null)]
    public async Task SignInKeepsAllNameComponents(string name, string? first, string? last)
    {
        var user = CreateUser();
        user.Name = name;
        var mapped = await SuccessfulResponse(user).ToDto(Repository(user).Object);
        Assert.Equal(first, mapped.User.FirstName);
        Assert.Equal(last, mapped.User.LastName);
        Assert.Equal(name, user.Name);
    }

    [Theory]
    [InlineData(true, false, false, "synthetic-access", "+15550001000")]
    [InlineData(false, false, false, "synthetic-access", null)]
    [InlineData(true, true, false, "synthetic-access", null)]
    [InlineData(true, false, true, "synthetic-access", null)]
    [InlineData(true, false, false, "", null)]
    [InlineData(true, false, false, "  ", null)]
    public async Task PhoneRequiresCompletedAuthentication(bool success, bool mfa, bool stepUp, string token, string? expected)
    {
        var user = CreateUser();
        var source = SuccessfulResponse(user);
        source.Success = success;
        source.RequiresMfa = mfa;
        source.RequiresStepUp = stepUp;
        source.AccessToken = token;
        var mapped = await source.ToDto(Repository(user).Object);
        Assert.Equal(expected, mapped.User.PhoneNumber);
        Assert.False(mapped.User.PhoneNumberVerified);
        Assert.Equal(source.Success, mapped.Success);
        Assert.Equal(source.RequiresMfa, mapped.RequiresMfa);
        Assert.Equal(source.RequiresStepUp, mapped.RequiresStepUp);
        Assert.Equal(token, mapped.AccessToken);
    }

    [Fact]
    public async Task SignInPreservesServerFieldsAndUsesRepositoryProfile()
    {
        var user = CreateUser();
        var source = SuccessfulResponse(user);
        source.Message = "server-result";
        source.Email = "old-server-address@example.test";
        source.SessionId = Guid.NewGuid();
        source.TempToken = "synthetic-temp";
        source.MfaToken = "synthetic-mfa";
        source.TenantId = Guid.NewGuid();
        source.AvailableTenants = [new global::GameGuild.TenantInfo(source.TenantId.Value, "Tenant", "tenant", true)];
        source.MfaSessionId = "synthetic-mfa-session";
        source.StepUpToken = "synthetic-step-up";
        source.StepUpExpiresAt = Now.AddMinutes(5);
        source.RiskLevel = RiskLevel.Low;
        source.RiskFactors = ["server-risk"];
        source.AvailableMethods = ["TOTP"];
        source.User = new UserDto { Id = Guid.NewGuid(), Email = "unrelated@example.test", PhoneNumberVerified = true };
        var repository = Repository(user);

        var mapped = await source.ToDto(repository.Object);

        Assert.NotSame(source, mapped);
        Assert.Equal(source.Success, mapped.Success);
        Assert.Equal(source.Message, mapped.Message);
        Assert.Equal(source.AccessToken, mapped.AccessToken);
        Assert.Equal(source.RefreshToken, mapped.RefreshToken);
        Assert.Equal(source.ExpiresAt, mapped.ExpiresAt);
        Assert.Equal(source.AccessTokenExpiresAt, mapped.AccessTokenExpiresAt);
        Assert.Equal(source.RefreshTokenExpiresAt, mapped.RefreshTokenExpiresAt);
        Assert.Equal(source.ExpiresIn, mapped.ExpiresIn);
        Assert.Equal(user.Id, mapped.UserId);
        Assert.Equal(source.Email, mapped.Email);
        Assert.Equal(source.SessionId, mapped.SessionId);
        Assert.Equal(source.TempToken, mapped.TempToken);
        Assert.Equal(source.MfaToken, mapped.MfaToken);
        Assert.Equal(source.TenantId, mapped.TenantId);
        Assert.Equal(source.AvailableTenants, mapped.AvailableTenants);
        Assert.Equal(source.RequiresMfa, mapped.RequiresMfa);
        Assert.Equal(source.MfaSessionId, mapped.MfaSessionId);
        Assert.Equal(source.RequiresStepUp, mapped.RequiresStepUp);
        Assert.Equal(source.StepUpToken, mapped.StepUpToken);
        Assert.Equal(source.StepUpExpiresAt, mapped.StepUpExpiresAt);
        Assert.Equal(source.RiskLevel, mapped.RiskLevel);
        Assert.Equal(source.RiskFactors, mapped.RiskFactors);
        Assert.Equal(source.AvailableMethods, mapped.AvailableMethods);
        Assert.Equal(user.Id, mapped.User.Id);
        Assert.Equal(user.Email, mapped.User.Email);
        Assert.Equal(user.Username, mapped.User.Username);
        Assert.Equal("Matheus", mapped.User.FirstName);
        Assert.Equal("Luiz Martins", mapped.User.LastName);
        Assert.Equal(user.PhoneNumber, mapped.User.PhoneNumber);
        Assert.Equal(user.IsEmailVerified, mapped.User.EmailVerified);
        Assert.False(mapped.User.PhoneNumberVerified);
        Assert.Equal(user.CreatedAt, mapped.User.CreatedAt);
        Assert.Equal(user.LastLoginAt, mapped.User.LastLoginAt);
        repository.Verify(repo => repo.GetByIdAsync(user.Id, CancellationToken.None), Times.Once);
        repository.VerifyNoOtherCalls();
        Assert.True(source.User.PhoneNumberVerified);
    }

    [Fact]
    public async Task SignInDoesNotShareMutableCollectionContainers()
    {
        var user = CreateUser();
        var source = SuccessfulResponse(user);
        var tenants = new List<global::GameGuild.TenantInfo> { new(Guid.NewGuid(), "Tenant", "tenant", true) };
        source.AvailableTenants = tenants;
        source.RiskFactors = ["server-risk"];
        source.AvailableMethods = ["TOTP"];
        var mapped = await source.ToDto(Repository(user).Object);
        Assert.NotSame(tenants, mapped.AvailableTenants);
        mapped.RiskFactors!.Clear();
        mapped.AvailableMethods!.Clear();
        tenants.Clear();
        Assert.Single(source.RiskFactors);
        Assert.Single(source.AvailableMethods);
        Assert.Single(mapped.AvailableTenants!);
    }

    [Theory]
    [InlineData(600)]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task SignInOnlyDerivesMissingAccessExpiry(int seconds)
    {
        SystemClock.SetProvider(new FixedTimeProvider());
        try
        {
            var user = CreateUser();
            var source = SuccessfulResponse(user);
            source.AccessTokenExpiresAt = default;
            source.RefreshTokenExpiresAt = default;
            source.ExpiresIn = seconds;
            var mapped = await source.ToDto(Repository(user).Object);
            Assert.Equal(seconds > 0 ? Now.AddSeconds(seconds) : source.ExpiresAt, mapped.AccessTokenExpiresAt);
            Assert.Equal(source.ExpiresAt, mapped.RefreshTokenExpiresAt);
            Assert.Equal(source.ExpiresAt, mapped.ExpiresAt);
            Assert.Equal(seconds, mapped.ExpiresIn);
        }
        finally
        {
            SystemClock.Reset();
        }
    }

    [Fact]
    public async Task MissingRepositoryUserRetainsLegacyFallbackWithoutTrustingEmbeddedProfile()
    {
        SystemClock.SetProvider(new FixedTimeProvider());
        try
        {
            var source = SuccessfulResponse(CreateUser());
            source.User = new UserDto { Id = source.UserId, PhoneNumber = "+15550009999", EmailVerified = true };
            var repository = new Mock<IUserRepository>(MockBehavior.Strict);
            repository.Setup(repo => repo.GetByIdAsync(source.UserId, CancellationToken.None)).ReturnsAsync((User?)null);
            var mapped = await source.ToDto(repository.Object);
            Assert.Equal(source.UserId, mapped.User.Id);
            Assert.Equal(source.Email, mapped.User.Email);
            Assert.Equal(source.Email, mapped.User.Username);
            Assert.Equal(Now, mapped.User.CreatedAt);
            Assert.Null(mapped.User.PhoneNumber);
            Assert.Null(mapped.User.FirstName);
            Assert.Null(mapped.User.LastName);
            Assert.Null(mapped.User.LastLoginAt);
            Assert.False(mapped.User.EmailVerified);
            Assert.False(mapped.User.PhoneNumberVerified);
        }
        finally
        {
            SystemClock.Reset();
        }
    }

    [Fact]
    public async Task SignInForwardsCancellationAndPropagatesRepositoryFailure()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var source = SuccessfulResponse(CreateUser());
        var repository = new Mock<IUserRepository>(MockBehavior.Strict);
        repository.Setup(repo => repo.GetByIdAsync(source.UserId, cancellation.Token))
            .ThrowsAsync(new OperationCanceledException(cancellation.Token));
        var error = await Assert.ThrowsAsync<OperationCanceledException>(() => source.ToDto(repository.Object, cancellation.Token));
        Assert.Equal(cancellation.Token, error.CancellationToken);
        repository.VerifyAll();
    }

    [Theory]
    [InlineData(-86400)]
    [InlineData(86400)]
    public void RefreshPreservesSuppliedExpiryEvenWhenExpired(int offset)
    {
        SystemClock.SetProvider(new FixedTimeProvider());
        try
        {
            var source = new RefreshTokenResponse { ExpiresAt = Now.AddSeconds(offset), ExpiresIn = 600 };
            Assert.Equal(offset < 0, source.ExpiresAt < SystemClock.UtcNow);
            var mapped = source.ToDto();
            Assert.Equal(source.ExpiresAt, mapped.ExpiresAt);
            Assert.Equal(600, mapped.ExpiresIn);
        }
        finally
        {
            SystemClock.Reset();
        }
    }

    [Theory]
    [InlineData(120)]
    [InlineData(0)]
    [InlineData(-1)]
    public void RefreshOnlyDerivesMissingExpiryFromPositiveDuration(int seconds)
    {
        SystemClock.SetProvider(new FixedTimeProvider());
        try
        {
            var mapped = new RefreshTokenResponse { ExpiresIn = seconds }.ToDto();
            Assert.Equal(seconds > 0 ? Now.AddSeconds(seconds) : default, mapped.ExpiresAt);
            Assert.Equal(seconds, mapped.ExpiresIn);
        }
        finally
        {
            SystemClock.Reset();
        }
    }

    [Fact]
    public void RefreshCopiesEveryUserFieldWithoutSharingMutableObjects()
    {
        var source = new RefreshTokenResponse
        {
            AccessToken = "synthetic-access", RefreshToken = "synthetic-refresh", ExpiresAt = Now.AddMinutes(10), ExpiresIn = 600,
            User = new UserDto
            {
                Id = Guid.NewGuid(), Email = "dto@example.test", Username = "dto", FirstName = "Ana", LastName = "Maria Silva",
                PhoneNumber = "+15550001000", EmailVerified = true, PhoneNumberVerified = true,
                CreatedAt = Now.AddYears(-1), LastLoginAt = Now.AddHours(-1)
            }
        };
        var mapped = source.ToDto();
        Assert.NotSame(source, mapped);
        Assert.NotSame(source.User, mapped.User);
        Assert.Equal(source.AccessToken, mapped.AccessToken);
        Assert.Equal(source.RefreshToken, mapped.RefreshToken);
        Assert.Equal(source.ExpiresAt, mapped.ExpiresAt);
        Assert.Equal(source.ExpiresIn, mapped.ExpiresIn);
        Assert.Equal(source.User.Id, mapped.User.Id);
        Assert.Equal(source.User.Email, mapped.User.Email);
        Assert.Equal(source.User.Username, mapped.User.Username);
        Assert.Equal(source.User.FirstName, mapped.User.FirstName);
        Assert.Equal(source.User.LastName, mapped.User.LastName);
        Assert.Equal(source.User.PhoneNumber, mapped.User.PhoneNumber);
        Assert.Equal(source.User.EmailVerified, mapped.User.EmailVerified);
        Assert.Equal(source.User.PhoneNumberVerified, mapped.User.PhoneNumberVerified);
        Assert.Equal(source.User.CreatedAt, mapped.User.CreatedAt);
        Assert.Equal(source.User.LastLoginAt, mapped.User.LastLoginAt);
        mapped.User.Email = "changed@example.test";
        Assert.Equal("dto@example.test", source.User.Email);
    }

    [Fact]
    public async Task SignInRejectsNullResponse()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() => ((SignInResponse)null!).ToDto(new Mock<IUserRepository>().Object));
    }

    [Fact]
    public async Task SignInRejectsNullRepository()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() => SuccessfulResponse(CreateUser()).ToDto(null!));
    }

    [Fact]
    public void RefreshRejectsNullResponse()
    {
        Assert.Throws<ArgumentNullException>(() => ((RefreshTokenResponse)null!).ToDto());
    }

    private static User CreateUser() => new()
    {
        Id = Guid.NewGuid(), Email = "mapping@example.test", Username = "mapping", Name = "Matheus Luiz Martins",
        PhoneNumber = "+15550001000", IsEmailVerified = true, CreatedAt = Now.AddYears(-1), LastLoginAt = Now.AddHours(-1)
    };

    private static SignInResponse SuccessfulResponse(User user) => new()
    {
        Success = true, UserId = user.Id, Email = user.Email, AccessToken = "synthetic-access", RefreshToken = "synthetic-refresh",
        ExpiresAt = Now.AddDays(7), AccessTokenExpiresAt = Now.AddMinutes(10), RefreshTokenExpiresAt = Now.AddDays(7), ExpiresIn = 600
    };

    private static Mock<IUserRepository> Repository(User user)
    {
        var repository = new Mock<IUserRepository>(MockBehavior.Strict);
        repository.Setup(repo => repo.GetByIdAsync(user.Id, It.IsAny<CancellationToken>())).ReturnsAsync(user);
        return repository;
    }

    private sealed class FixedTimeProvider : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(Now);
    }
}
