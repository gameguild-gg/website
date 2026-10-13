using FluentAssertions;
using GameGuild.Identity.Users;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace GameGuild.Identity.Authentication.UnitTests.Handlers;

public sealed class MagicLinkMfaPolicyTests
{
    [Fact]
    public async Task VerifiedLinkPreservesMfaChallengeBoundToSubjectVersionTenantAndDevice()
    {
        var user = new User { Id = Guid.NewGuid(), Email = "mfa-link@example.test", TokenVersion = 7 };
        var repository = new Mock<IUserRepository>();
        var verification = new Mock<IEmailVerificationService>();
        var tokens = new Mock<IJwtTokenService>(MockBehavior.Strict);
        var issuer = new Mock<IAuthenticatedSessionIssuer>(MockBehavior.Strict);
        var mfa = new Mock<ISignInMfaService>(MockBehavior.Strict);
        var tenantId = Guid.NewGuid();
        using var cancellation = new CancellationTokenSource();
        repository.Setup(r => r.GetByIdAsync(user.Id, cancellation.Token)).ReturnsAsync(user);
        verification.Setup(s => s.VerifyMagicLinkTokenAsync("verified-link"))
            .ReturnsAsync(new TokenValidationResult(true, user.Id, user.Email));
        var pending = new SignInResponse { Success = false, RequiresMfa = true,
            MfaToken = "limited-bearer", Message = "Authenticator enrollment required" };
        mfa.Setup(s => s.BeginAsync(user.Id, user.TokenVersion, tenantId,
                It.Is<DeviceInfo>(device => device.DeviceName == "Magic Link" && device.DeviceType == "Web"
                    && device.Fingerprint == "synthetic-device" && device.IpAddress == "192.0.2.1"
                    && device.UserAgent == "Test browser"),
                SignInFirstFactor.MagicLink, false, cancellation.Token)).ReturnsAsync(pending);
        var handler = new ConsumeMagicLinkCommandHandler(repository.Object, verification.Object, tokens.Object,
            new ConfigurationBuilder().Build(), NullLogger<ConsumeMagicLinkCommandHandler>.Instance,
            jwtOptions: null, sessionIssuer: issuer.Object, signInMfa: mfa.Object);

        var result = await handler.Handle(new ConsumeMagicLinkCommand { Token = "verified-link", TenantId = tenantId,
            DeviceFingerprint = "synthetic-device", IpAddress = "192.0.2.1", UserAgent = "Test browser" }, cancellation.Token);

        result.Should().BeSameAs(pending);
        result.Success.Should().BeFalse();
        result.RequiresMfa.Should().BeTrue();
        result.Message.Should().Be("Authenticator enrollment required");
        result.AccessToken.Should().BeEmpty();
        result.RefreshToken.Should().BeEmpty();
        mfa.VerifyAll();
        mfa.VerifyNoOtherCalls();
        issuer.VerifyNoOtherCalls();
        tokens.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task LegacyConstructorWithoutMfaPolicyFailsClosedEvenWithSessionIssuer()
    {
        var user = new User { Id = Guid.NewGuid(), Email = "mfa-missing@example.test", TokenVersion = 7 };
        var repository = new Mock<IUserRepository>();
        var verification = new Mock<IEmailVerificationService>();
        var tokens = new Mock<IJwtTokenService>(MockBehavior.Strict);
        var issuer = new Mock<IAuthenticatedSessionIssuer>(MockBehavior.Strict);
        repository.Setup(r => r.GetByIdAsync(user.Id, CancellationToken.None)).ReturnsAsync(user);
        verification.Setup(s => s.VerifyMagicLinkTokenAsync("verified-link"))
            .ReturnsAsync(new TokenValidationResult(true, user.Id, user.Email));
        var handler = new ConsumeMagicLinkCommandHandler(repository.Object, verification.Object, tokens.Object,
            new ConfigurationBuilder().Build(), NullLogger<ConsumeMagicLinkCommandHandler>.Instance,
            jwtOptions: null, sessionIssuer: issuer.Object);

        var action = () => handler.Handle(new ConsumeMagicLinkCommand { Token = "verified-link" }, CancellationToken.None);
        await action.Should().ThrowAsync<InvalidOperationException>();
        issuer.VerifyNoOtherCalls();
        tokens.VerifyNoOtherCalls();
    }
}
