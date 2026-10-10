// Regression tests for issue #287 (User Enumeration Protection).
// The anonymous email-verification boundary must return an identical public
// response for known and unknown accounts, including when the durable delivery
// boundary fails, while keeping token/recipient binding to the resolved account.

using FluentAssertions;
using GameGuild.CQRS;
using GameGuild.Identity.Users;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace GameGuild.Identity.Authentication.UnitTests.Handlers;

public sealed class SendEmailVerificationEnumerationProtectionTests
{
    private readonly Mock<IEmailVerificationService> _emailService = new();
    private readonly Mock<IUserRepository> _userRepository = new();
    private readonly Mock<IAuthenticationAuditEventSink> _auditSink = new();

    private SendEmailVerificationCommandHandler CreateSubject() => new(
        _emailService.Object,
        _userRepository.Object,
        Mock.Of<ILogger<SendEmailVerificationCommandHandler>>(),
        _auditSink.Object);

    private static SendEmailVerificationCommand Command(string email, Guid? userId = null) =>
        new() { Email = email, UserId = userId };

    private void SetupKnownUser(Guid userId, string email)
    {
        _userRepository
            .Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User { Id = userId, Email = email, Username = "known-user" });
        _userRepository
            .Setup(r => r.GetByEmailAsync(email, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User { Id = userId, Email = email, Username = "known-user" });
        _emailService
            .Setup(s => s.GenerateVerificationTokenAsync(userId, email))
            .ReturnsAsync("verification-token");
    }

    private void SetupUnknownUser(string email)
    {
        _userRepository
            .Setup(r => r.GetByEmailAsync(email, It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);
    }

    [Fact]
    public async Task Handle_KnownAndUnknownAccounts_ReturnIdenticalResponse()
    {
        var userId = Guid.NewGuid();
        const string email = "known@example.com";
        SetupKnownUser(userId, email);
        _emailService
            .Setup(s => s.SendVerificationEmailAsync(email, "verification-token", It.IsAny<string?>()))
            .Returns(Task.CompletedTask);
        const string unknownEmail = "unknown@example.com";
        SetupUnknownUser(unknownEmail);

        var known = await CreateSubject().Handle(Command(email), CancellationToken.None);
        var unknown = await CreateSubject().Handle(Command(unknownEmail), CancellationToken.None);

        known.Message.Should().Be(unknown.Message);
        known.Message.Should().Be(UserEnumerationProtectionService.GenericEmailVerificationMessage);
    }

    [Fact]
    public async Task Handle_UnknownAccount_DoesNotGenerateTokensOrTriggerDelivery()
    {
        SetupUnknownUser("absent@example.com");

        var result = await CreateSubject().Handle(Command("absent@example.com"), CancellationToken.None);

        result.Message.Should().Be(UserEnumerationProtectionService.GenericEmailVerificationMessage);
        _emailService.Verify(
            s => s.GenerateVerificationTokenAsync(It.IsAny<Guid>(), It.IsAny<string>()),
            Times.Never);
        _emailService.Verify(
            s => s.SendVerificationEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>()),
            Times.Never);
        _auditSink.Verify(
            s => s.RecordAsync(It.IsAny<AuthenticationAuditEvent>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_KnownAccount_BindsTokenAndRecipientToResolvedAccount()
    {
        var userId = Guid.NewGuid();
        const string email = "binding@example.com";
        SetupKnownUser(userId, email);
        _emailService
            .Setup(s => s.SendVerificationEmailAsync(email, "verification-token", "known-user"))
            .Returns(Task.CompletedTask);

        await CreateSubject().Handle(Command(email, userId), CancellationToken.None);

        _emailService.Verify(
            s => s.GenerateVerificationTokenAsync(userId, email),
            Times.Once);
        _emailService.Verify(
            s => s.SendVerificationEmailAsync(email, "verification-token", "known-user"),
            Times.Once);
    }

    [Fact]
    public async Task Handle_DeliveryFailsOnEveryAttempt_ReturnsGenericResponseWithoutThrowing()
    {
        var userId = Guid.NewGuid();
        const string email = "failing@example.com";
        SetupKnownUser(userId, email);
        _emailService
            .Setup(s => s.SendVerificationEmailAsync(email, It.IsAny<string>(), It.IsAny<string?>()))
            .ThrowsAsync(new InvalidOperationException("Authentication notification was not durably queued."));

        var subject = CreateSubject();
        var result = await subject.Handle(Command(email), CancellationToken.None);

        // Unknown accounts can never fail delivery; a thrown or divergent response here would be an
        // account-existence oracle, so the boundary must stay uniform (HTTP 200 + generic message).
        result.Message.Should().Be(UserEnumerationProtectionService.GenericEmailVerificationMessage);
        _emailService.Verify(
            s => s.SendVerificationEmailAsync(email, It.IsAny<string>(), It.IsAny<string?>()),
            Times.Exactly(2));

        // The lost delivery is not silent: a security audit failure event is recorded.
        _auditSink.Verify(
            s => s.RecordAsync(
                It.Is<AuthenticationAuditEvent>(e =>
                    e.ActionType == "Authentication.EmailVerificationDeliveryFailed" &&
                    e.UserId == userId &&
                    !e.Success),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_TransientDeliveryFailure_RetriesAndRecoversWithoutAuditFailure()
    {
        var userId = Guid.NewGuid();
        const string email = "transient@example.com";
        SetupKnownUser(userId, email);
        var attempts = 0;
        _emailService
            .Setup(s => s.SendVerificationEmailAsync(email, It.IsAny<string>(), It.IsAny<string?>()))
            .Returns(() => ++attempts == 1
                ? Task.FromException(new InvalidOperationException("transient queue failure"))
                : Task.CompletedTask);

        var result = await CreateSubject().Handle(Command(email), CancellationToken.None);

        result.Message.Should().Be(UserEnumerationProtectionService.GenericEmailVerificationMessage);
        attempts.Should().Be(2);
        _auditSink.Verify(
            s => s.RecordAsync(It.IsAny<AuthenticationAuditEvent>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task PasswordFacade_KnownAndUnknownAccounts_ReturnIdenticalResponse()
    {
        var userRepo = new Mock<IUserRepository>();
        var sender = new Mock<ISender>();
        sender.Setup(s => s.Send(It.IsAny<SendEmailVerificationCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EmailVerificationResponse { Message = UserEnumerationProtectionService.GenericEmailVerificationMessage });
        var facade = new PasswordService(
            Mock.Of<ILogger<PasswordService>>(),
            userRepo.Object,
            sender.Object);

        var userId = Guid.NewGuid();
        userRepo
            .Setup(r => r.GetByEmailAsync("known@example.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User { Id = userId, Email = "known@example.com", Username = "known-user" });
        userRepo
            .Setup(r => r.GetByEmailAsync("unknown@example.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        var known = await facade.SendEmailVerificationAsync(new SendEmailVerificationRequest { Email = "known@example.com" });
        var unknown = await facade.SendEmailVerificationAsync(new SendEmailVerificationRequest { Email = "unknown@example.com" });

        known.Success.Should().BeTrue();
        unknown.Success.Should().BeTrue();
        known.Message.Should().Be(unknown.Message);
        known.Message.Should().Be(UserEnumerationProtectionService.GenericEmailVerificationMessage);
        sender.Verify(
            s => s.Send(
                It.Is<SendEmailVerificationCommand>(c => c.UserId == userId),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public void GenericMessage_IsRegisteredAsTheEmailVerificationContextMessage()
    {
        var policy = new Microsoft.Extensions.Configuration.ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["PresentationLayer:Authentication:PasswordPolicy:BCryptWorkFactor"] = "10"
            })
            .Build();
        var protection = new UserEnumerationProtectionService(
            Mock.Of<ILogger<UserEnumerationProtectionService>>(),
            new Microsoft.Extensions.Caching.Memory.MemoryCache(Microsoft.Extensions.Options.Options.Create(
                new Microsoft.Extensions.Caching.Memory.MemoryCacheOptions())),
            new PasswordHasher(Mock.Of<ILogger<PasswordHasher>>(), policy));

        protection.GetGenericErrorMessage("email_verification")
            .Should().Be(UserEnumerationProtectionService.GenericEmailVerificationMessage);
    }
}
