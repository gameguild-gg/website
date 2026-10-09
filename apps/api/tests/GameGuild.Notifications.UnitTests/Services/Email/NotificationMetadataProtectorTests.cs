using System.Security.Cryptography;
using GameGuild.Notifications.Services.Email;
using GameGuild.Notifications.Services.Email.Renderers;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace GameGuild.Notifications.UnitTests.Services.Email;

public sealed class NotificationMetadataProtectorTests
{
    private const string Payload = "{\"token\":\"native-credential\",\"email\":\"member@example.test\",\"userName\":\"Member\"}";
    private readonly NotificationMetadataProtector _protector = new(new EphemeralDataProtectionProvider());

    [Theory]
    [InlineData(NotificationType.EmailVerification)]
    [InlineData(NotificationType.PasswordReset)]
    [InlineData(NotificationType.MagicLink)]
    public void Protection_Is_Recoverable_And_Idempotent_Without_Plaintext_Reassignment(NotificationType type)
    {
        var notification = Create(type);
        _protector.ProtectForStorage(notification).Should().BeTrue();
        var envelope = notification.Metadata;
        envelope.Should().StartWith(NotificationMetadataProtector.ProtectedPrefix).And.NotContain("native-credential");
        _protector.GetForRendering(notification, type).Should().Be(Payload);
        _protector.ProtectForStorage(notification).Should().BeFalse();
        notification.Metadata.Should().Be(envelope);
    }

    [Theory]
    [InlineData("Id")]
    [InlineData("RecipientId")]
    [InlineData("TenantId")]
    [InlineData("RecipientEmail")]
    [InlineData("Type")]
    [InlineData("Channel")]
    public void Envelope_Cannot_Be_Rebound_To_Another_Record_Or_Recipient(string field)
    {
        var notification = Create();
        _protector.ProtectForStorage(notification);
        object replacement = field switch
        {
            "RecipientEmail" => "attacker@example.test",
            "Type" => NotificationType.MagicLink,
            "Channel" => NotificationChannel.InApp,
            _ => Guid.NewGuid()
        };
        typeof(Notification).GetProperty(field)!.SetValue(notification, replacement);
        var exception = Assert.Throws<CryptographicException>(() => _protector.GetForRendering(notification, notification.Type));
        AssertSanitized(exception);
    }

    [Theory]
    [InlineData("copy")]
    [InlineData("tamper")]
    [InlineData("foreign-key")]
    [InlineData("plaintext")]
    [InlineData("purpose")]
    public void Invalid_Envelope_Fails_Closed_With_No_Secret_In_The_Diagnostic(string attack)
    {
        var notification = Create();
        if (attack != "plaintext")
        {
            _protector.ProtectForStorage(notification);
        }

        if (attack == "copy")
        {
            var copy = Create();
            SetMetadata(copy, notification.Metadata!);
            notification = copy;
        }
        if (attack == "tamper")
        {
            var bytes = notification.Metadata!.ToCharArray();
            var position = NotificationMetadataProtector.ProtectedPrefix.Length + 30;
            bytes[position] = bytes[position] == 'A' ? 'B' : 'A';
            SetMetadata(notification, new string(bytes));
        }
        var subject = attack == "foreign-key" ? new NotificationMetadataProtector(new EphemeralDataProtectionProvider()) : _protector;
        var expected = attack == "purpose" ? NotificationType.PasswordReset : notification.Type;
        var exception = Assert.Throws<CryptographicException>(() => subject.GetForRendering(notification, expected));
        AssertSanitized(exception);
        notification.IsSent.Should().BeFalse();
    }

    [Theory]
    [InlineData(NotificationChannel.Email)]
    [InlineData(NotificationChannel.InApp)]
    [InlineData(NotificationChannel.Push)]
    public void Credential_Types_Are_Protected_On_Every_Channel(NotificationChannel channel)
    {
        var notification = Create(channel: channel);
        _protector.ProtectForStorage(notification).Should().BeTrue();
        _protector.GetForRendering(notification, notification.Type).Should().Be(Payload);
    }

    [Fact]
    public void Ordinary_Metadata_Is_Unchanged()
    {
        var notification = Create(NotificationType.System);
        _protector.ProtectForStorage(notification).Should().BeFalse();
        notification.Metadata.Should().Be(Payload);
    }

    [Fact]
    public void Maximum_Unicode_Input_RoundTrips_When_Ciphertext_Exceeds_Old_Column_Size()
    {
        var metadata = string.Concat(Enumerable.Repeat("\U0001F600", 2000));
        metadata.Length.Should().Be(Notification.MaximumMetadataInputLength);
        var notification = Create(metadata: metadata);
        _protector.ProtectForStorage(notification).Should().BeTrue();
        notification.Metadata!.Length.Should().BeGreaterThan(Notification.MaximumMetadataInputLength);
        _protector.GetForRendering(notification, notification.Type).Should().Be(metadata);
        var invalid = () => Create(metadata: metadata + "x");
        invalid.Should().Throw<ArgumentException>().WithMessage("Notification metadata exceeds the input limit.*");
    }

    [Theory]
    [InlineData(NotificationType.EmailVerification)]
    [InlineData(NotificationType.PasswordReset)]
    [InlineData(NotificationType.MagicLink)]
    public async Task Renderer_Rejects_Missing_Credentials_After_Valid_Decryption(NotificationType type)
    {
        var notification = Create(type, metadata: "{\"userName\":\"Member\"}");
        _protector.ProtectForStorage(notification);
        var renderer = Renderer(type, _protector);
        await Assert.ThrowsAsync<InvalidOperationException>(() => renderer.RenderAsync(notification));
        notification.IsSent.Should().BeFalse();
    }

    [Theory]
    [InlineData(NotificationDeliveryStatus.Pending)]
    [InlineData(NotificationDeliveryStatus.Sending)]
    [InlineData(NotificationDeliveryStatus.Sent)]
    [InlineData(NotificationDeliveryStatus.DeadLettered)]
    [InlineData(NotificationDeliveryStatus.HeldForDigest)]
    [InlineData(NotificationDeliveryStatus.Failed)]
    public async Task Legacy_History_Is_Protected_Without_Changing_Delivery_State(NotificationDeliveryStatus status)
    {
        await using var context = Context();
        var notification = Create(channel: NotificationChannel.InApp);
        typeof(Notification).GetProperty(nameof(Notification.DeliveryStatus))!.SetValue(notification, status);
        if (status == NotificationDeliveryStatus.Sent)
        {
            notification.MarkAsSent();
        }

        notification.Version = 1; // The InMemory test context does not perform production version stamping.
        notification.Delete();
        var state = (notification.IsSent, notification.SentAt, notification.UpdatedAt, notification.DeletedAt,
            notification.AttemptCount, notification.NextAttemptAt, notification.LastError);
        context.Add(notification);
        await context.SaveChangesAsync();
        var sender = new Mock<GameGuild.Email.IEmailSender>(MockBehavior.Strict);
        await Dispatcher(context, sender.Object, []).SweepOnceAsync();
        context.ChangeTracker.Clear();
        var stored = await context.Notifications.IgnoreQueryFilters().SingleAsync();
        stored.Metadata.Should().StartWith(NotificationMetadataProtector.ProtectedPrefix).And.NotContain("native-credential");
        stored.DeliveryStatus.Should().Be(status);
        (stored.IsSent, stored.SentAt, stored.UpdatedAt, stored.DeletedAt, stored.AttemptCount, stored.NextAttemptAt, stored.LastError).Should().Be(state);
        _protector.GetForRendering(stored, stored.Type).Should().Be(Payload);
        sender.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Legacy_Batch_Is_Bounded_And_Due_Row_Outside_Batch_Is_Protected_Before_Delivery()
    {
        await using var context = Context();
        var history = Create(channel: NotificationChannel.InApp);
        history.CreatedAt = SystemClock.UtcNow.AddDays(-1);
        var whitespace = Create(channel: NotificationChannel.InApp, metadata: "   ");
        whitespace.CreatedAt = history.CreatedAt.AddDays(-1);
        var due = Create();
        var later = Create(channel: NotificationChannel.InApp);
        later.CreatedAt = SystemClock.UtcNow.AddDays(1);
        context.AddRange(history, whitespace, due, later);
        await context.SaveChangesAsync();
        var sender = new Mock<GameGuild.Email.IEmailSender>();
        sender.Setup(s => s.SendAsync(It.IsAny<GameGuild.Email.EmailMessage>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("native-message");
        var processed = await Dispatcher(context, sender.Object, [Renderer(due.Type, _protector)], batchSize: 1).SweepOnceAsync();
        processed.Should().Be(1);
        history.Metadata.Should().StartWith(NotificationMetadataProtector.ProtectedPrefix);
        due.Metadata.Should().StartWith(NotificationMetadataProtector.ProtectedPrefix);
        later.Metadata.Should().Be(Payload);
        due.DeliveryStatus.Should().Be(NotificationDeliveryStatus.Sent);
        sender.Verify(s => s.SendAsync(It.Is<GameGuild.Email.EmailMessage>(m => m.PlainTextContent.Contains("native-credential")), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Send_Failure_And_Corruption_Do_Not_Acknowledge_Or_Persist_Plaintext(bool corrupt)
    {
        await using var context = Context();
        var notification = Create();
        _protector.ProtectForStorage(notification);
        if (corrupt)
        {
            SetMetadata(notification, NotificationMetadataProtector.ProtectedPrefix + "invalid");
        }

        context.Add(notification);
        await context.SaveChangesAsync();
        var sender = new Mock<GameGuild.Email.IEmailSender>();
        sender.Setup(s => s.SendAsync(It.IsAny<GameGuild.Email.EmailMessage>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("SMTP unavailable"));
        var dispatcher = Dispatcher(context, sender.Object, [Renderer(notification.Type, _protector)]);
        (await dispatcher.SweepOnceAsync()).Should().Be(0);
        notification.DeliveryStatus.Should().Be(NotificationDeliveryStatus.Pending);
        notification.IsSent.Should().BeFalse();
        notification.AttemptCount.Should().Be(1);
        notification.LastError.Should().NotContain("native-credential");
        notification.Metadata.Should().StartWith(NotificationMetadataProtector.ProtectedPrefix).And.NotContain("native-credential");
        sender.Verify(s => s.SendAsync(It.IsAny<GameGuild.Email.EmailMessage>(), It.IsAny<CancellationToken>()), corrupt ? Times.Never() : Times.Once());
    }

    private static Notification Create(NotificationType type = NotificationType.EmailVerification,
        NotificationChannel channel = NotificationChannel.Email, string metadata = Payload) =>
        Notification.Create(Guid.NewGuid(), type, channel, "Identity email", "Requested operation",
            tenantId: Guid.NewGuid(), recipientEmail: "member@example.test", metadata: metadata);

    [Fact]
    public async Task Unavailable_Key_Aborts_Legacy_Sweep_Before_Lifecycle_Changes_Or_Email()
    {
        await using var context = Context();
        var notification = Create();
        context.Add(notification);
        await context.SaveChangesAsync();
        var sender = new Mock<GameGuild.Email.IEmailSender>(MockBehavior.Strict);
        var dispatcher = Dispatcher(context, sender.Object, [],
            protector: new NotificationMetadataProtector(new UnavailableProvider()));
        await Assert.ThrowsAsync<CryptographicException>(() => dispatcher.SweepOnceAsync());
        context.ChangeTracker.Clear();
        var stored = await context.Notifications.SingleAsync();
        stored.DeliveryStatus.Should().Be(NotificationDeliveryStatus.Pending);
        stored.IsSent.Should().BeFalse();
        stored.AttemptCount.Should().Be(0);
        stored.Metadata.Should().Be(Payload);
        sender.VerifyNoOtherCalls();
    }

    private sealed class UnavailableProvider : IDataProtectionProvider, IDataProtector
    {
        public IDataProtector CreateProtector(string purpose) => this;
        public byte[] Protect(byte[] plaintext) => throw new CryptographicException("Fixture key unavailable.");
        public byte[] Unprotect(byte[] protectedData) => throw new CryptographicException("Fixture key unavailable.");
    }

    private static void SetMetadata(Notification notification, string metadata) =>
        typeof(Notification).GetProperty(nameof(Notification.Metadata))!.SetValue(notification, metadata);

    private static void AssertSanitized(Exception exception)
    {
        exception.Message.Should().Be("Notification credential metadata is invalid or unavailable.");
        exception.InnerException.Should().BeNull();
        exception.ToString().Should().NotContain("native-credential");
    }

    private static IEmailRenderer Renderer(NotificationType type, NotificationMetadataProtector protector)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["App:BaseUrl"] = "https://notification.example.test"
        }).Build();
        return type switch
        {
            NotificationType.EmailVerification => new EmailVerificationRenderer(Mock.Of<IEmailFooterService>(), configuration, protector),
            NotificationType.PasswordReset => new PasswordResetRenderer(Mock.Of<IEmailFooterService>(), configuration, protector),
            NotificationType.MagicLink => new MagicLinkRenderer(Mock.Of<IEmailFooterService>(), configuration, protector),
            _ => throw new ArgumentOutOfRangeException(nameof(type))
        };
    }

    private static NotificationsTestDbContext Context() => new(new DbContextOptionsBuilder<NotificationsTestDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private EmailDispatcherService Dispatcher(NotificationsTestDbContext context, GameGuild.Email.IEmailSender sender,
        IEmailRenderer[] renderers, int batchSize = 50, NotificationMetadataProtector? protector = null)
    {
        var resolver = new Mock<IRecipientEmailResolver>();
        resolver.Setup(r => r.ResolveAsync(It.IsAny<Notification>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("member@example.test");
        var preferences = new Mock<INotificationPreferenceService>();
#pragma warning disable CS0618 // Exercise the existing dispatcher preference seam.
        preferences.Setup(p => p.ShouldSendNotificationAsync(It.IsAny<Guid>(), It.IsAny<NotificationType>(),
            It.IsAny<NotificationChannel>(), It.IsAny<NotificationPriority>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
#pragma warning restore CS0618
        return new EmailDispatcherService(context, new EmailRendererRegistry(renderers), resolver.Object, preferences.Object,
            sender, Options.Create(new EmailDispatcherOptions { SweepBatchSize = batchSize }),
            NullLogger<EmailDispatcherService>.Instance, protector ?? _protector);
    }
}
