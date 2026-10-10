using GameGuild;
using GameGuild.Identity.Authentication;
using GameGuild.Identity.Users;
using Moq;
using Xunit;

public sealed class SignInMfaMappingOutcomeTests
{
    [Theory]
    [InlineData(SignInMfaPurpose.VerifyFactor, false)]
    [InlineData(SignInMfaPurpose.EnrollFactor, false)]
    [InlineData(SignInMfaPurpose.VerifyFactor, true)]
    [InlineData(SignInMfaPurpose.EnrollFactor, true)]
    public async Task PendingMappingPreservesTheServerOutcomeAndVerifiedProfileWithoutPhoneOrCredentials(SignInMfaPurpose purpose, bool risk)
    {
        var bearer = SignInMfaChallengeToken.Create();
        var expiry = DateTimeOffset.UtcNow.AddMinutes(5);
        var user = new User
        {
            Id = Guid.NewGuid(), Email = "verified-mfa@example.test", Username = "verified-mfa",
            Name = "Ana Maria Silva", PhoneNumber = "+15550001000",
            CreatedAt = new DateTime(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc),
            LastLoginAt = new DateTime(2026, 1, 3, 4, 5, 6, DateTimeKind.Utc)
        };
        user.VerifyEmail();
        var tenantId = Guid.NewGuid();
        var pending = new SignInMfaPendingResponse(bearer, expiry, purpose, risk, user, tenantId);
        var repository = new Mock<IUserRepository>(MockBehavior.Strict);
        var mapped = await pending.ToDto(repository.Object, default);
        Assert.Same(pending, mapped);
        Assert.IsType<SignInMfaPendingResponse>(mapped);
        Assert.False(CommandOutcome.ShouldRollback(mapped));
        Assert.False(mapped.Success);
        Assert.True(mapped.RequiresMfa);
        Assert.Equal(bearer, mapped.MfaToken);
        Assert.Equal(risk, mapped.RequiresStepUp);
        Assert.Equal(risk ? bearer : null, mapped.StepUpToken);
        Assert.Equal(expiry.UtcDateTime, mapped.StepUpExpiresAt);
        Assert.Empty(mapped.AccessToken);
        Assert.Empty(mapped.RefreshToken);
        Assert.Equal(user.Id, mapped.UserId);
        Assert.Equal(user.Email, mapped.Email);
        Assert.Equal(Guid.Empty, mapped.SessionId);
        Assert.Equal(user.Id, mapped.User.Id);
        Assert.Equal(user.Email, mapped.User.Email);
        Assert.Equal(user.Username, mapped.User.Username);
        Assert.Equal("Ana", mapped.User.FirstName);
        Assert.Equal("Maria Silva", mapped.User.LastName);
        Assert.Equal(user.CreatedAt, mapped.User.CreatedAt);
        Assert.Equal(user.LastLoginAt, mapped.User.LastLoginAt);
        Assert.True(mapped.User.EmailVerified);
        Assert.Null(mapped.User.PhoneNumber);
        Assert.False(mapped.User.PhoneNumberVerified);
        Assert.Equal(tenantId, mapped.TenantId);
        Assert.Null(mapped.AvailableTenants);
        repository.VerifyNoOtherCalls();
        user.Email = "changed-mfa@example.test";
        user.UpdateName("Changed Name");
        user.UpdatePhoneNumber("+15550002000");
        Assert.Equal("verified-mfa@example.test", mapped.User.Email);
        Assert.Equal("Ana", mapped.User.FirstName);
        Assert.Null(mapped.User.PhoneNumber);
    }

    [Fact]
    public async Task TrustedCommittedDenialKeepsFailedProofAccountingWithoutProfileOrCredentials()
    {
        var denial = new SignInMfaCommittedDenial();
        var repository = new Mock<IUserRepository>(MockBehavior.Strict);
        var mapped = await denial.ToDto(repository.Object, default);
        Assert.Same(denial, mapped);
        Assert.IsType<SignInMfaCommittedDenial>(mapped);
        Assert.False(CommandOutcome.ShouldRollback(mapped));
        Assert.False(mapped.Success);
        Assert.Empty(mapped.AccessToken);
        Assert.Empty(mapped.RefreshToken);
        AssertNoAuthenticatedProfile(mapped);
        Assert.Equal(Guid.Empty, mapped.UserId);
        repository.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task PublicMfaFlagsCannotCreateTheServerOnlyCommitOutcome()
    {
        var response = new SignInResponse { Success = false, RequiresMfa = true, MfaToken = SignInMfaChallengeToken.Create() };
        var repository = new Mock<IUserRepository>(MockBehavior.Strict);
        repository.Setup(port => port.GetByIdAsync(Guid.Empty, It.IsAny<CancellationToken>())).ReturnsAsync((User?)null);
        var mapped = await response.ToDto(repository.Object, default);
        Assert.NotSame(response, mapped);
        Assert.IsType<SignInResponse>(mapped);
        Assert.True(CommandOutcome.ShouldRollback(mapped));
        Assert.True(mapped.RequiresMfa);
        Assert.Equal(response.MfaToken, mapped.MfaToken);
        repository.Verify(port => port.GetByIdAsync(Guid.Empty, It.IsAny<CancellationToken>()), Times.Once);
        repository.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task CancellationIsObservedBeforeReturningThePreservedPendingOutcome()
    {
        var pending = new SignInMfaPendingResponse(SignInMfaChallengeToken.Create(), DateTimeOffset.UtcNow.AddMinutes(5),
            SignInMfaPurpose.VerifyFactor, false, new User { Id = Guid.NewGuid() }, Guid.NewGuid());
        var repository = new Mock<IUserRepository>(MockBehavior.Strict);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending.ToDto(repository.Object, cancellation.Token));
        repository.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData("null-subject")]
    [InlineData("empty-subject")]
    [InlineData("empty-tenant")]
    public void PendingResponseRequiresBoundSubjectAndTenant(string fault)
    {
        var user = fault == "null-subject" ? null : new User { Id = fault == "empty-subject" ? Guid.Empty : Guid.NewGuid() };
        var tenantId = fault == "empty-tenant" ? Guid.Empty : Guid.NewGuid();
        Assert.ThrowsAny<ArgumentException>(() => new SignInMfaPendingResponse(SignInMfaChallengeToken.Create(),
            DateTimeOffset.UtcNow.AddMinutes(5), SignInMfaPurpose.VerifyFactor, false, user!, tenantId));
    }

    private static void AssertNoAuthenticatedProfile(SignInResponse response)
    {
        // The existing public contract requires User; preserve it with only the empty DTO.
        Assert.NotNull(response.User);
        Assert.Equal(Guid.Empty, response.User.Id);
        Assert.Empty(response.User.Email);
        Assert.Empty(response.User.Username);
        Assert.Null(response.User.FirstName);
        Assert.Null(response.User.LastName);
        Assert.Null(response.User.PhoneNumber);
        Assert.Null(response.User.LastLoginAt);
        Assert.False(response.User.EmailVerified);
        Assert.False(response.User.PhoneNumberVerified);
        Assert.Equal(default, response.User.CreatedAt);
    }
}
