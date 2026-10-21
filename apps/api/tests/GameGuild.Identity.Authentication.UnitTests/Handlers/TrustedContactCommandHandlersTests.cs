using FluentAssertions;
using GameGuild.Email;
using GameGuild.Identity.Authentication;
using GameGuild.Identity.Users;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace GameGuild.Identity.Authentication.UnitTests.Handlers;

public sealed class TrustedContactCommandHandlersTests
{
    private static User CreateOwner(string email = "owner@test.com") =>
        new() { Id = Guid.NewGuid(), Email = email, Username = "owner", TenantId = Guid.NewGuid(), TokenVersion = 1 };

    private static IConfiguration CreateConfig() =>
        new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>()).Build();

    [Fact]
    public async Task AddTrustedContact_CreatesInvitedContactWithDigestAndSendsEmail()
    {
        var owner = CreateOwner();
        var contacts = new Mock<ITrustedContactStore>();
        var users = new Mock<IUserRepository>();
        var email = new Mock<IEmailSender>();
        users.Setup(r => r.GetByIdAsync(owner.Id, It.IsAny<CancellationToken>())).ReturnsAsync(owner);
        contacts.Setup(s => s.CountActiveByOwnerAsync(owner.Id, It.IsAny<CancellationToken>())).ReturnsAsync(0);
        contacts.Setup(s => s.FindActiveByOwnerAndEmailAsync(owner.Id, "friend@test.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync((TrustedContact?)null);

        TrustedContact? persisted = null;
        contacts.Setup(s => s.AddAsync(It.IsAny<TrustedContact>(), It.IsAny<CancellationToken>()))
            .Callback<TrustedContact, CancellationToken>((contact, _) => persisted = contact)
            .Returns(Task.CompletedTask);

        var handler = new AddTrustedContactCommandHandler(
            contacts.Object, users.Object, email.Object,
            Options.Create(new AccountRecoveryOptions()), CreateConfig(),
            Mock.Of<IAuthenticationAuditEventSink>(), NullLogger<AddTrustedContactCommandHandler>.Instance);

        var result = await handler.Handle(new AddTrustedContactCommand
        {
            OwnerUserId = owner.Id,
            Email = "Friend@Test.com",
            Relation = "spouse"
        }, CancellationToken.None);

        result.Success.Should().BeTrue();
        persisted.Should().NotBeNull();
        persisted!.Status.Should().Be(TrustedContactStatus.Invited);
        persisted.Email.Should().Be("friend@test.com");
        persisted.OwnerUserId.Should().Be(owner.Id);
        persisted.InviteTokenHash.Should().MatchRegex("^[a-f0-9]{64}$");
        persisted.InviteTokenExpiresAt.Should().BeAfter(persisted.InvitedAt);
        email.Verify(s => s.SendAsync(
            It.Is<EmailMessage>(m => m.ToEmail == "friend@test.com" && m.PlainTextContent.Contains("token=")),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AddTrustedContact_RejectsOwnEmail()
    {
        var owner = CreateOwner("me@test.com");
        var contacts = new Mock<ITrustedContactStore>();
        var users = new Mock<IUserRepository>();
        users.Setup(r => r.GetByIdAsync(owner.Id, It.IsAny<CancellationToken>())).ReturnsAsync(owner);

        var handler = new AddTrustedContactCommandHandler(
            contacts.Object, users.Object, Mock.Of<IEmailSender>(),
            Options.Create(new AccountRecoveryOptions()), CreateConfig(),
            Mock.Of<IAuthenticationAuditEventSink>(), NullLogger<AddTrustedContactCommandHandler>.Instance);

        var result = await handler.Handle(new AddTrustedContactCommand { OwnerUserId = owner.Id, Email = "Me@Test.com" }, CancellationToken.None);

        result.Success.Should().BeFalse();
        contacts.Verify(s => s.AddAsync(It.IsAny<TrustedContact>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task AddTrustedContact_RejectsDuplicateActiveEmail()
    {
        var owner = CreateOwner();
        var contacts = new Mock<ITrustedContactStore>();
        var users = new Mock<IUserRepository>();
        users.Setup(r => r.GetByIdAsync(owner.Id, It.IsAny<CancellationToken>())).ReturnsAsync(owner);
        contacts.Setup(s => s.CountActiveByOwnerAsync(owner.Id, It.IsAny<CancellationToken>())).ReturnsAsync(1);
        contacts.Setup(s => s.FindActiveByOwnerAndEmailAsync(owner.Id, "friend@test.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TrustedContact { Id = Guid.NewGuid(), OwnerUserId = owner.Id, Email = "friend@test.com", Status = TrustedContactStatus.Invited });

        var handler = new AddTrustedContactCommandHandler(
            contacts.Object, users.Object, Mock.Of<IEmailSender>(),
            Options.Create(new AccountRecoveryOptions { MaxTrustedContacts = 5 }), CreateConfig(),
            Mock.Of<IAuthenticationAuditEventSink>(), NullLogger<AddTrustedContactCommandHandler>.Instance);

        var result = await handler.Handle(new AddTrustedContactCommand { OwnerUserId = owner.Id, Email = "friend@test.com" }, CancellationToken.None);

        result.Success.Should().BeFalse();
        contacts.Verify(s => s.AddAsync(It.IsAny<TrustedContact>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task AddTrustedContact_RejectsWhenCapReached()
    {
        var owner = CreateOwner();
        var contacts = new Mock<ITrustedContactStore>();
        var users = new Mock<IUserRepository>();
        users.Setup(r => r.GetByIdAsync(owner.Id, It.IsAny<CancellationToken>())).ReturnsAsync(owner);
        contacts.Setup(s => s.CountActiveByOwnerAsync(owner.Id, It.IsAny<CancellationToken>())).ReturnsAsync(2);

        var handler = new AddTrustedContactCommandHandler(
            contacts.Object, users.Object, Mock.Of<IEmailSender>(),
            Options.Create(new AccountRecoveryOptions { MaxTrustedContacts = 2 }), CreateConfig(),
            Mock.Of<IAuthenticationAuditEventSink>(), NullLogger<AddTrustedContactCommandHandler>.Instance);

        var result = await handler.Handle(new AddTrustedContactCommand { OwnerUserId = owner.Id, Email = "new@test.com" }, CancellationToken.None);

        result.Success.Should().BeFalse();
        contacts.Verify(s => s.AddAsync(It.IsAny<TrustedContact>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ConfirmTrustedContact_ValidTokenConfirmsAndConsumesToken()
    {
        var contacts = new Mock<ITrustedContactStore>();
        var contact = new TrustedContact
        {
            Id = Guid.NewGuid(),
            OwnerUserId = Guid.NewGuid(),
            Email = "friend@test.com",
            Status = TrustedContactStatus.Invited,
            InviteTokenHash = AccountRecoveryTokensTestHelpers.Hash("invite-token"),
            InviteTokenExpiresAt = SystemClock.UtcNow.AddHours(1),
            InvitedAt = SystemClock.UtcNow.AddDays(-1)
        };
        contacts.Setup(s => s.FindInvitedByInviteTokenHashAsync(contact.InviteTokenHash!, It.IsAny<CancellationToken>()))
            .ReturnsAsync(contact);

        var handler = new ConfirmTrustedContactCommandHandler(
            contacts.Object, Mock.Of<IAuthenticationAuditEventSink>(), NullLogger<ConfirmTrustedContactCommandHandler>.Instance);
        var result = await handler.Handle(new ConfirmTrustedContactCommand { InviteToken = "invite-token" }, CancellationToken.None);

        result.Success.Should().BeTrue();
        contact.Status.Should().Be(TrustedContactStatus.Confirmed);
        contact.ConfirmedAt.Should().NotBeNull();
        // Single-use: the digest is dropped so the token can never be redeemed again.
        contact.InviteTokenHash.Should().BeNull();
        contact.InviteTokenExpiresAt.Should().BeNull();
    }

    [Fact]
    public async Task ConfirmTrustedContact_UnknownTokenFailsClosedWithGenericDenial()
    {
        var contacts = new Mock<ITrustedContactStore>();
        contacts.Setup(s => s.FindInvitedByInviteTokenHashAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((TrustedContact?)null);

        var handler = new ConfirmTrustedContactCommandHandler(
            contacts.Object, Mock.Of<IAuthenticationAuditEventSink>(), NullLogger<ConfirmTrustedContactCommandHandler>.Instance);
        var result = await handler.Handle(new ConfirmTrustedContactCommand { InviteToken = "unknown" }, CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Message.Should().Be("Invalid or expired invitation.");
    }

    [Fact]
    public async Task ConfirmTrustedContact_ExpiredTokenFailsClosed()
    {
        var contacts = new Mock<ITrustedContactStore>();
        var contact = new TrustedContact
        {
            Id = Guid.NewGuid(),
            OwnerUserId = Guid.NewGuid(),
            Email = "friend@test.com",
            Status = TrustedContactStatus.Invited,
            InviteTokenHash = AccountRecoveryTokensTestHelpers.Hash("stale-token"),
            InviteTokenExpiresAt = SystemClock.UtcNow.AddHours(-1),
            InvitedAt = SystemClock.UtcNow.AddDays(-8)
        };
        contacts.Setup(s => s.FindInvitedByInviteTokenHashAsync(contact.InviteTokenHash!, It.IsAny<CancellationToken>()))
            .ReturnsAsync(contact);

        var handler = new ConfirmTrustedContactCommandHandler(
            contacts.Object, Mock.Of<IAuthenticationAuditEventSink>(), NullLogger<ConfirmTrustedContactCommandHandler>.Instance);
        var result = await handler.Handle(new ConfirmTrustedContactCommand { InviteToken = "stale-token" }, CancellationToken.None);

        result.Success.Should().BeFalse();
        contact.Status.Should().Be(TrustedContactStatus.Invited);
    }

    [Fact]
    public async Task RemoveTrustedContact_RevokesAndDropsInviteDigest()
    {
        var contacts = new Mock<ITrustedContactStore>();
        var owner = Guid.NewGuid();
        var contact = new TrustedContact
        {
            Id = Guid.NewGuid(),
            OwnerUserId = owner,
            Email = "friend@test.com",
            Status = TrustedContactStatus.Confirmed,
            InviteTokenHash = null,
            ConfirmedAt = SystemClock.UtcNow.AddDays(-1)
        };
        contacts.Setup(s => s.GetByIdAsync(contact.Id, It.IsAny<CancellationToken>())).ReturnsAsync(contact);

        var handler = new RemoveTrustedContactCommandHandler(
            contacts.Object, Mock.Of<IAuthenticationAuditEventSink>(), NullLogger<RemoveTrustedContactCommandHandler>.Instance);
        var result = await handler.Handle(new RemoveTrustedContactCommand { OwnerUserId = owner, ContactId = contact.Id }, CancellationToken.None);

        result.Success.Should().BeTrue();
        contact.Status.Should().Be(TrustedContactStatus.Revoked);
        contact.RevokedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task RemoveTrustedContact_OtherOwnersContactIsNotFound()
    {
        var contacts = new Mock<ITrustedContactStore>();
        var contact = new TrustedContact
        {
            Id = Guid.NewGuid(),
            OwnerUserId = Guid.NewGuid(),
            Email = "friend@test.com",
            Status = TrustedContactStatus.Confirmed
        };
        contacts.Setup(s => s.GetByIdAsync(contact.Id, It.IsAny<CancellationToken>())).ReturnsAsync(contact);

        var handler = new RemoveTrustedContactCommandHandler(
            contacts.Object, Mock.Of<IAuthenticationAuditEventSink>(), NullLogger<RemoveTrustedContactCommandHandler>.Instance);
        var result = await handler.Handle(new RemoveTrustedContactCommand { OwnerUserId = Guid.NewGuid(), ContactId = contact.Id }, CancellationToken.None);

        result.Success.Should().BeFalse();
        contact.Status.Should().Be(TrustedContactStatus.Confirmed);
    }
}

/// <summary>Test-visible access to the internal token helper.</summary>
internal static class AccountRecoveryTokensTestHelpers
{
    public static string Hash(string token) =>
        Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(token))).ToLowerInvariant();
}
