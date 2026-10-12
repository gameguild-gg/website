using FluentAssertions;
using GameGuild.Identity.Users;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace GameGuild.Identity.Authentication.UnitTests.Handlers;

public sealed class ProviderRefreshIssuanceSafetyTests
{
    [Theory]
    [InlineData(false, false, false)]
    [InlineData(true, true, false)]
    [InlineData(true, false, true)]
    public async Task MagicLink_UnavailableAccountCannotIssueRefreshCredentials(bool active, bool suspended, bool deleted)
    {
        var user = CreateUser(active, suspended, deleted);
        var repository = new Mock<IUserRepository>();
        repository.Setup(service => service.GetByIdAsync(user.Id, It.IsAny<CancellationToken>())).ReturnsAsync(user);
        var verification = new Mock<IEmailVerificationService>();
        verification.Setup(service => service.VerifyMagicLinkTokenAsync("verified-magic-link"))
            .ReturnsAsync(new TokenValidationResult(true, user.Id, user.Email));
        var tokens = new Mock<IJwtTokenService>();
        var handler = new ConsumeMagicLinkCommandHandler(repository.Object, verification.Object, tokens.Object,
            new ConfigurationBuilder().Build(), NullLogger<ConsumeMagicLinkCommandHandler>.Instance);

        var act = () => handler.Handle(new ConsumeMagicLinkCommand { Token = "verified-magic-link" }, CancellationToken.None);

        await act.Should().ThrowAsync<AuthenticationRequiredException>();
        tokens.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task WebAuthn_MissingAccountCannotReportSuccessfulAuthentication()
    {
        var webAuthn = new Mock<IWebAuthnService>();
        webAuthn.Setup(service => service.CompleteAuthenticationAsync("verified-assertion", null, "test-agent", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WebAuthnAuthenticationResult { Success = true, UserId = Guid.NewGuid() });
        var tokens = new Mock<IJwtTokenService>();
        var handler = new WebAuthnMutationCommandHandler(webAuthn.Object, tokens.Object, Mock.Of<IUserRepository>(),
            new ConfigurationBuilder().Build());

        var act = () => handler.Handle(new CompleteWebAuthnAuthenticationCommand("verified-assertion", null, "test-agent"), CancellationToken.None);

        await act.Should().ThrowAsync<AuthenticationRequiredException>();
        tokens.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData(false, false, false)]
    [InlineData(true, true, false)]
    [InlineData(true, false, true)]
    public async Task WebAuthn_UnavailableAccountCannotIssueRefreshCredentials(bool active, bool suspended, bool deleted)
    {
        var user = CreateUser(active, suspended, deleted);
        var repository = new Mock<IUserRepository>();
        repository.Setup(service => service.GetByIdAsync(user.Id, It.IsAny<CancellationToken>())).ReturnsAsync(user);
        var webAuthn = new Mock<IWebAuthnService>();
        webAuthn.Setup(service => service.CompleteAuthenticationAsync("verified-assertion", null, "test-agent", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WebAuthnAuthenticationResult { Success = true, UserId = user.Id });
        var tokens = new Mock<IJwtTokenService>();
        var handler = new WebAuthnMutationCommandHandler(webAuthn.Object, tokens.Object, repository.Object,
            new ConfigurationBuilder().Build());

        var act = () => handler.Handle(new CompleteWebAuthnAuthenticationCommand("verified-assertion", null, "test-agent"), CancellationToken.None);

        await act.Should().ThrowAsync<AuthenticationRequiredException>();
        tokens.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task MagicLink_CancelledRequestDoesNotConsumeCredential()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var user = CreateUser(true, false, false);
        var repository = new Mock<IUserRepository>();
        repository.Setup(service => service.GetByIdAsync(user.Id, It.IsAny<CancellationToken>())).ReturnsAsync(user);
        var verification = new Mock<IEmailVerificationService>();
        verification.Setup(service => service.VerifyMagicLinkTokenAsync("verified-magic-link"))
            .ReturnsAsync(new TokenValidationResult(true, user.Id, user.Email));
        var tokens = new Mock<IJwtTokenService>();
        var handler = new ConsumeMagicLinkCommandHandler(repository.Object, verification.Object, tokens.Object,
            new ConfigurationBuilder().Build(), NullLogger<ConsumeMagicLinkCommandHandler>.Instance);

        var act = () => handler.Handle(new ConsumeMagicLinkCommand { Token = "verified-magic-link" }, cancellation.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
        verification.VerifyNoOtherCalls();
        repository.VerifyNoOtherCalls();
        tokens.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task WebAuthn_CancelledRequestDoesNotConsumeAssertion()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var webAuthn = new Mock<IWebAuthnService>();
        webAuthn.Setup(service => service.CompleteAuthenticationAsync("verified-assertion", null, "test-agent", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WebAuthnAuthenticationResult { Success = false });
        var tokens = new Mock<IJwtTokenService>();
        var repository = new Mock<IUserRepository>();
        var handler = new WebAuthnMutationCommandHandler(webAuthn.Object, tokens.Object, repository.Object,
            new ConfigurationBuilder().Build());

        var act = () => handler.Handle(new CompleteWebAuthnAuthenticationCommand("verified-assertion", null, "test-agent"), cancellation.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
        webAuthn.VerifyNoOtherCalls();
        repository.VerifyNoOtherCalls();
        tokens.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task WebAuthn_MissingVerifiedIdentityCannotReportSuccess()
    {
        var webAuthn = new Mock<IWebAuthnService>();
        webAuthn.Setup(service => service.CompleteAuthenticationAsync("verified-assertion", null, "test-agent", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WebAuthnAuthenticationResult { Success = true });
        var tokens = new Mock<IJwtTokenService>();
        var repository = new Mock<IUserRepository>();
        var handler = new WebAuthnMutationCommandHandler(webAuthn.Object, tokens.Object, repository.Object,
            new ConfigurationBuilder().Build());

        var act = () => handler.Handle(new CompleteWebAuthnAuthenticationCommand("verified-assertion", null, "test-agent"), CancellationToken.None);

        await act.Should().ThrowAsync<AuthenticationRequiredException>();
        repository.VerifyNoOtherCalls();
        tokens.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task MagicLink_CancellationDuringVerificationCannotIssueCredentials()
    {
        using var cancellation = new CancellationTokenSource();
        var user = CreateUser(true, false, false);
        var repository = new Mock<IUserRepository>();
        repository.Setup(service => service.GetByIdAsync(user.Id, It.IsAny<CancellationToken>())).ReturnsAsync(user);
        var verification = new Mock<IEmailVerificationService>();
        verification.Setup(service => service.VerifyMagicLinkTokenAsync("verified-magic-link"))
            .Callback(() => cancellation.Cancel())
            .ReturnsAsync(new TokenValidationResult(true, user.Id, user.Email));
        var tokens = new Mock<IJwtTokenService>();
        var handler = new ConsumeMagicLinkCommandHandler(repository.Object, verification.Object, tokens.Object,
            new ConfigurationBuilder().Build(), NullLogger<ConsumeMagicLinkCommandHandler>.Instance);

        var act = () => handler.Handle(new ConsumeMagicLinkCommand { Token = "verified-magic-link" }, cancellation.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
        verification.Verify(service => service.VerifyMagicLinkTokenAsync("verified-magic-link"), Times.Once);
        verification.VerifyNoOtherCalls();
        repository.VerifyNoOtherCalls();
        tokens.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task WebAuthn_CancellationDuringVerificationCannotIssueCredentials()
    {
        using var cancellation = new CancellationTokenSource();
        var user = CreateUser(true, false, false);
        var repository = new Mock<IUserRepository>();
        repository.Setup(service => service.GetByIdAsync(user.Id, It.IsAny<CancellationToken>())).ReturnsAsync(user);
        var webAuthn = new Mock<IWebAuthnService>();
        webAuthn.Setup(service => service.CompleteAuthenticationAsync("verified-assertion", null, "test-agent", It.IsAny<CancellationToken>()))
            .Callback(() => cancellation.Cancel())
            .ReturnsAsync(new WebAuthnAuthenticationResult { Success = true, UserId = user.Id });
        var tokens = new Mock<IJwtTokenService>();
        var handler = new WebAuthnMutationCommandHandler(webAuthn.Object, tokens.Object, repository.Object,
            new ConfigurationBuilder().Build());

        var act = () => handler.Handle(new CompleteWebAuthnAuthenticationCommand("verified-assertion", null, "test-agent"), cancellation.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
        webAuthn.Verify(service => service.CompleteAuthenticationAsync("verified-assertion", null, "test-agent", cancellation.Token), Times.Once);
        webAuthn.VerifyNoOtherCalls();
        repository.VerifyNoOtherCalls();
        tokens.VerifyNoOtherCalls();
    }

    private static User CreateUser(bool active, bool suspended, bool deleted)
    {
        var user = new User
        {
            Id = Guid.NewGuid(), Email = "provider-issuance@example.test", TokenVersion = 4, Version = 1,
            IsActive = active, IsSuspended = suspended
        };
        if (deleted)
        {
            user.SoftDelete();
        }
        return user;
    }
}
