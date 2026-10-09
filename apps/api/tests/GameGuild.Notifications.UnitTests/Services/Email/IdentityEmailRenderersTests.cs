using System.Text.Json;
using FluentAssertions;
using GameGuild.Notifications.Services.Email;
using GameGuild.Notifications.Services.Email.Renderers;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace GameGuild.Notifications.UnitTests.Services.Email;

public sealed class IdentityEmailRenderersTests
{
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly NotificationMetadataProtector MetadataProtector = new(new EphemeralDataProtectionProvider());

    private static IEmailFooterService CreateFooterService() =>
        new EmailFooterService(
            new UnsubscribeTokenService(new EphemeralDataProtectionProvider()),
            new ConfigurationBuilder().AddInMemoryCollection(
                new Dictionary<string, string?> { ["App:BaseUrl"] = "https://app.example.com" }).Build());

    private static IConfiguration CreateConfiguration() =>
        new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?> { ["App:BaseUrl"] = "https://app.example.com" }).Build();

    private static Notification CreateNotification(NotificationType type, string metadata, Guid? recipientId = null)
    {
        var notification = Notification.Create(
            recipientId ?? UserId,
            type,
            NotificationChannel.Email,
            "Title",
            "Message",
            metadata: metadata);
        MetadataProtector.ProtectForStorage(notification);
        return notification;
    }

    private static string Metadata(object value) => JsonSerializer.Serialize(value);

    [Fact]
    public async Task Welcome_Renders_Subject_And_Body_From_Metadata()
    {
        var renderer = new WelcomeEmailRenderer(CreateFooterService(), CreateConfiguration());
        var notification = CreateNotification(
            NotificationType.Onboarding,
            Metadata(new { userName = "alice", displayName = "Alice" }));

        var message = await renderer.RenderAsync(notification);

        message.Should().NotBeNull();
        message!.Subject.Should().Be("Welcome to GameGuild");
        message.PlainTextContent.Should().Contain("Hi Alice,");
        message.PlainTextContent.Should().Contain("https://app.example.com");
        message.HtmlContent.Should().Contain("Open GameGuild");
    }

    [Fact]
    public async Task Welcome_Is_Suppressible_And_Includes_Footer()
    {
        var renderer = new WelcomeEmailRenderer(CreateFooterService(), CreateConfiguration());
        var notification = CreateNotification(
            NotificationType.Onboarding,
            Metadata(new { userName = "alice", displayName = "Alice" }));

        var message = await renderer.RenderAsync(notification);

        message!.PlainTextContent.Should().Contain("/unsubscribe?token=");
        message.HtmlContent.Should().Contain("/workspace/settings/notifications");
    }

    [Fact]
    public async Task EmailVerification_Renders_Link_From_Token_Metadata()
    {
        var renderer = new EmailVerificationRenderer(CreateFooterService(), CreateConfiguration(), MetadataProtector);
        var notification = CreateNotification(
            NotificationType.EmailVerification,
            Metadata(new { token = "tok-1", email = "user@example.com", userName = "Alice" }));

        var message = await renderer.RenderAsync(notification);

        message.Should().NotBeNull();
        message!.Subject.Should().Be("Verify your GameGuild email address");
        message.PlainTextContent.Should().Contain("https://app.example.com/verify-email?token=tok-1");
        message.HtmlContent.Should().Contain("Verify your email address");
    }

    [Fact]
    public async Task EmailVerification_Is_Transactional_And_Has_No_Footer()
    {
        var renderer = new EmailVerificationRenderer(CreateFooterService(), CreateConfiguration(), MetadataProtector);
        var notification = CreateNotification(
            NotificationType.EmailVerification,
            Metadata(new { token = "tok-1", email = "user@example.com", userName = "Alice" }));

        var message = await renderer.RenderAsync(notification);

        message!.PlainTextContent.Should().NotContain("/unsubscribe");
        message.HtmlContent.Should().NotContain("/unsubscribe");
    }

    [Fact]
    public async Task PasswordReset_Renders_Link_From_Token_Metadata()
    {
        var renderer = new PasswordResetRenderer(CreateFooterService(), CreateConfiguration(), MetadataProtector);
        var notification = CreateNotification(
            NotificationType.PasswordReset,
            Metadata(new { token = "reset-1", email = "user@example.com", userName = "Alice" }));

        var message = await renderer.RenderAsync(notification);

        message.Should().NotBeNull();
        message!.Subject.Should().Be("Reset your GameGuild password");
        message.PlainTextContent.Should().Contain("https://app.example.com/reset-password?token=reset-1");
        message.HtmlContent.Should().Contain("Reset your password");
    }

    [Fact]
    public async Task PasswordReset_Is_Transactional_And_Has_No_Footer()
    {
        var renderer = new PasswordResetRenderer(CreateFooterService(), CreateConfiguration(), MetadataProtector);
        var notification = CreateNotification(
            NotificationType.PasswordReset,
            Metadata(new { token = "reset-1", email = "user@example.com", userName = "Alice" }));

        var message = await renderer.RenderAsync(notification);

        message!.PlainTextContent.Should().NotContain("/unsubscribe");
        message.HtmlContent.Should().NotContain("/unsubscribe");
    }

    [Fact]
    public async Task MagicLink_Renders_Link_From_Token_Metadata()
    {
        var renderer = new MagicLinkRenderer(CreateFooterService(), CreateConfiguration(), MetadataProtector);
        var notification = CreateNotification(
            NotificationType.MagicLink,
            Metadata(new { token = "magic-1", email = "user@example.com", userName = "Alice" }));

        var message = await renderer.RenderAsync(notification);

        message.Should().NotBeNull();
        message!.Subject.Should().Be("Your GameGuild sign-in link");
        message.PlainTextContent.Should().Contain("https://app.example.com/magic-link?token=magic-1");
        message.HtmlContent.Should().Contain("Sign in to GameGuild");
    }

    [Fact]
    public async Task MagicLink_Is_Transactional_And_Has_No_Footer()
    {
        var renderer = new MagicLinkRenderer(CreateFooterService(), CreateConfiguration(), MetadataProtector);
        var notification = CreateNotification(
            NotificationType.MagicLink,
            Metadata(new { token = "magic-1", email = "user@example.com", userName = "Alice" }));

        var message = await renderer.RenderAsync(notification);

        message!.PlainTextContent.Should().NotContain("/unsubscribe");
        message.HtmlContent.Should().NotContain("/unsubscribe");
    }
}
