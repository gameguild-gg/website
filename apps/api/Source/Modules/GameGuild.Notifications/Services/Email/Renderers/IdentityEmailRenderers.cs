using System.Net;
using System.Text.Json;
using GameGuild.Email;
using GameGuild.Notifications.Services.Email;
using Microsoft.Extensions.Configuration;

namespace GameGuild.Notifications.Services.Email.Renderers;

/// <summary>
/// Renders the welcome (onboarding) email. SUPPRESSIBLE: account onboarding is not security-critical,
/// so it carries the unsubscribe footer and is gated by user preferences. Metadata JSON:
/// <c>{ "userName": string, "displayName": string }</c>.
/// </summary>
public sealed class WelcomeEmailRenderer(
    IEmailFooterService footerService,
    IConfiguration configuration) : EmailRendererBase, IEmailRenderer
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public NotificationType Type => NotificationType.Onboarding;

    public Task<EmailMessage?> RenderAsync(Notification notification, CancellationToken cancellationToken = default)
    {
        var meta = Deserialize(notification.Metadata);
        var displayName = string.IsNullOrWhiteSpace(meta.DisplayName) ? meta.UserName : meta.DisplayName;
        var appBaseUrl = ResolveBaseUrl();

        var plain =
            $"Hi {displayName},\n\nWelcome to GameGuild. Your account is ready to use.\n\nSign in: {appBaseUrl}\n\nSee you there.";
        var html =
            $"<p>Hi {WebUtility.HtmlEncode(displayName)},</p><p>Welcome to GameGuild. Your account is ready to use.</p><p><a href=\"{WebUtility.HtmlEncode(appBaseUrl)}\">Open GameGuild</a></p><p>See you there.</p>";

        var (finalPlain, finalHtml) = MergeFooter(plain, html, footerService.Build(notification));
        var message = new EmailMessage(string.Empty, "Welcome to GameGuild", finalPlain, finalHtml, displayName);
        return Task.FromResult<EmailMessage?>(message);
    }

    private string ResolveBaseUrl()
    {
        var appBaseUrl = configuration["App:BaseUrl"];
        return string.IsNullOrWhiteSpace(appBaseUrl) ? "http://localhost:3000" : appBaseUrl.TrimEnd('/');
    }

    private static WelcomeMetadata Deserialize(string? metadata)
    {
        if (string.IsNullOrWhiteSpace(metadata))
        {
            return WelcomeMetadata.Empty;
        }

        try
        {
            return JsonSerializer.Deserialize<WelcomeMetadata>(metadata, JsonOptions) ?? WelcomeMetadata.Empty;
        }
        catch (JsonException)
        {
            return WelcomeMetadata.Empty;
        }
    }

    private sealed record WelcomeMetadata(string? UserName, string? DisplayName)
    {
        public static WelcomeMetadata Empty { get; } = new(null, null);
    }
}

/// <summary>
/// Renders the email-address verification email. TRANSACTIONAL: never unsubscribable, no footer.
/// Metadata JSON: <c>{ "token": string, "email": string, "userName": string }</c>.
/// </summary>
public sealed class EmailVerificationRenderer(
    IEmailFooterService footerService,
    IConfiguration configuration,
    NotificationMetadataProtector metadataProtector) : EmailRendererBase, IEmailRenderer
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public NotificationType Type => NotificationType.EmailVerification;

    public Task<EmailMessage?> RenderAsync(Notification notification, CancellationToken cancellationToken = default)
    {
        var meta = Deserialize(metadataProtector.GetForRendering(notification, Type));
        if (string.IsNullOrWhiteSpace(meta.Token) || string.IsNullOrWhiteSpace(meta.Email))
        {
            throw new InvalidOperationException("Notification credential metadata is invalid or unavailable.");
        }
        var appBaseUrl = ResolveBaseUrl();
        var verificationLink = $"{appBaseUrl}/verify-email?token={meta.Token}";
        var recipientName = string.IsNullOrWhiteSpace(meta.UserName) ? meta.Email : meta.UserName;

        var plain =
            $"Hi {recipientName},\n\nPlease verify your GameGuild email address by visiting:\n{verificationLink}\n\nIf you did not request this, you can ignore this email.";
        var html =
            $"<p>Hi {WebUtility.HtmlEncode(recipientName)},</p><p>Please verify your GameGuild email address by visiting the link below:</p><p><a href=\"{WebUtility.HtmlEncode(verificationLink)}\">Verify your email address</a></p><p>If you did not request this, you can ignore this email.</p>";

        var (finalPlain, finalHtml) = MergeFooter(plain, html, footerService.Build(notification));
        var message = new EmailMessage(string.Empty, "Verify your GameGuild email address", finalPlain, finalHtml, recipientName);
        return Task.FromResult<EmailMessage?>(message);
    }

    private string ResolveBaseUrl()
    {
        var appBaseUrl = configuration["App:BaseUrl"];
        return string.IsNullOrWhiteSpace(appBaseUrl) ? "http://localhost:3000" : appBaseUrl.TrimEnd('/');
    }

    private static EmailVerificationMetadata Deserialize(string? metadata)
    {
        if (string.IsNullOrWhiteSpace(metadata))
        {
            return EmailVerificationMetadata.Empty;
        }

        try
        {
            return JsonSerializer.Deserialize<EmailVerificationMetadata>(metadata, JsonOptions) ?? EmailVerificationMetadata.Empty;
        }
        catch (JsonException)
        {
            return EmailVerificationMetadata.Empty;
        }
    }

    private sealed record EmailVerificationMetadata(string? Token, string? Email, string? UserName)
    {
        public static EmailVerificationMetadata Empty { get; } = new(null, null, null);
    }
}

/// <summary>
/// Renders the password-reset email. TRANSACTIONAL: never unsubscribable, no footer.
/// Metadata JSON: <c>{ "token": string, "email": string, "userName": string }</c>.
/// </summary>
public sealed class PasswordResetRenderer(
    IEmailFooterService footerService,
    IConfiguration configuration,
    NotificationMetadataProtector metadataProtector) : EmailRendererBase, IEmailRenderer
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public NotificationType Type => NotificationType.PasswordReset;

    public Task<EmailMessage?> RenderAsync(Notification notification, CancellationToken cancellationToken = default)
    {
        var meta = Deserialize(metadataProtector.GetForRendering(notification, Type));
        if (string.IsNullOrWhiteSpace(meta.Token) || string.IsNullOrWhiteSpace(meta.Email))
        {
            throw new InvalidOperationException("Notification credential metadata is invalid or unavailable.");
        }
        var appBaseUrl = ResolveBaseUrl();
        var resetLink = $"{appBaseUrl}/reset-password?token={meta.Token}";
        var recipientName = string.IsNullOrWhiteSpace(meta.UserName) ? meta.Email : meta.UserName;
        var encodedName = WebUtility.HtmlEncode(recipientName);
        var encodedLink = WebUtility.HtmlEncode(resetLink);

        var plain =
            $"Hi {recipientName},\n\nReset your GameGuild password by visiting:\n{resetLink}\n\nIf you did not request this, you can ignore this email.";
        var html =
            $"<p>Hi {encodedName},</p><p>Reset your GameGuild password by visiting the link below:</p><p><a href=\"{encodedLink}\">Reset your password</a></p><p>If you did not request this, you can ignore this email.</p>";

        var (finalPlain, finalHtml) = MergeFooter(plain, html, footerService.Build(notification));
        var message = new EmailMessage(string.Empty, "Reset your GameGuild password", finalPlain, finalHtml, recipientName);
        return Task.FromResult<EmailMessage?>(message);
    }

    private string ResolveBaseUrl()
    {
        var appBaseUrl = configuration["App:BaseUrl"];
        return string.IsNullOrWhiteSpace(appBaseUrl) ? "http://localhost:3000" : appBaseUrl.TrimEnd('/');
    }

    private static PasswordResetMetadata Deserialize(string? metadata)
    {
        if (string.IsNullOrWhiteSpace(metadata))
        {
            return PasswordResetMetadata.Empty;
        }

        try
        {
            return JsonSerializer.Deserialize<PasswordResetMetadata>(metadata, JsonOptions) ?? PasswordResetMetadata.Empty;
        }
        catch (JsonException)
        {
            return PasswordResetMetadata.Empty;
        }
    }

    private sealed record PasswordResetMetadata(string? Token, string? Email, string? UserName)
    {
        public static PasswordResetMetadata Empty { get; } = new(null, null, null);
    }
}

/// <summary>
/// Renders the magic sign-in link email. TRANSACTIONAL: never unsubscribable, no footer.
/// Metadata JSON: <c>{ "token": string, "email": string, "userName": string }</c>.
/// </summary>
public sealed class MagicLinkRenderer(
    IEmailFooterService footerService,
    IConfiguration configuration,
    NotificationMetadataProtector metadataProtector) : EmailRendererBase, IEmailRenderer
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public NotificationType Type => NotificationType.MagicLink;

    public Task<EmailMessage?> RenderAsync(Notification notification, CancellationToken cancellationToken = default)
    {
        var meta = Deserialize(metadataProtector.GetForRendering(notification, Type));
        if (string.IsNullOrWhiteSpace(meta.Token) || string.IsNullOrWhiteSpace(meta.Email))
        {
            throw new InvalidOperationException("Notification credential metadata is invalid or unavailable.");
        }
        var appBaseUrl = ResolveBaseUrl();
        var magicLink = $"{appBaseUrl}/magic-link?token={meta.Token}";
        var recipientName = string.IsNullOrWhiteSpace(meta.UserName) ? meta.Email : meta.UserName;

        var plain =
            $"Hi {recipientName},\n\nSign in to GameGuild with this magic link:\n{magicLink}\n\nIf you did not request this, you can ignore this email.";
        var html =
            $"<p>Hi {WebUtility.HtmlEncode(recipientName)},</p><p>Sign in to GameGuild with the link below:</p><p><a href=\"{WebUtility.HtmlEncode(magicLink)}\">Sign in to GameGuild</a></p><p>If you did not request this, you can ignore this email.</p>";

        var (finalPlain, finalHtml) = MergeFooter(plain, html, footerService.Build(notification));
        var message = new EmailMessage(string.Empty, "Your GameGuild sign-in link", finalPlain, finalHtml, recipientName);
        return Task.FromResult<EmailMessage?>(message);
    }

    private string ResolveBaseUrl()
    {
        var appBaseUrl = configuration["App:BaseUrl"];
        return string.IsNullOrWhiteSpace(appBaseUrl) ? "http://localhost:3000" : appBaseUrl.TrimEnd('/');
    }

    private static MagicLinkMetadata Deserialize(string? metadata)
    {
        if (string.IsNullOrWhiteSpace(metadata))
        {
            return MagicLinkMetadata.Empty;
        }

        try
        {
            return JsonSerializer.Deserialize<MagicLinkMetadata>(metadata, JsonOptions) ?? MagicLinkMetadata.Empty;
        }
        catch (JsonException)
        {
            return MagicLinkMetadata.Empty;
        }
    }

    private sealed record MagicLinkMetadata(string? Token, string? Email, string? UserName)
    {
        public static MagicLinkMetadata Empty { get; } = new(null, null, null);
    }
}

/// <summary>
/// Renders the one-time email sign-in code email. TRANSACTIONAL: never unsubscribable, no footer.
/// Metadata JSON: <c>{ "code": string, "email": string, "userName": string }</c>.
/// </summary>
public sealed class EmailCodeRenderer(
    IEmailFooterService footerService,
    NotificationMetadataProtector metadataProtector) : EmailRendererBase, IEmailRenderer
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public NotificationType Type => NotificationType.EmailCode;

    public Task<EmailMessage?> RenderAsync(Notification notification, CancellationToken cancellationToken = default)
    {
        var meta = Deserialize(metadataProtector.GetForRendering(notification, Type));
        if (string.IsNullOrWhiteSpace(meta.Code) || string.IsNullOrWhiteSpace(meta.Email))
        {
            throw new InvalidOperationException("Notification credential metadata is invalid or unavailable.");
        }

        var recipientName = string.IsNullOrWhiteSpace(meta.UserName) ? meta.Email : meta.UserName;
        var encodedName = WebUtility.HtmlEncode(recipientName);
        var encodedCode = WebUtility.HtmlEncode(meta.Code);

        var plain =
            $"Hi {recipientName},\n\nUse this one-time code to sign in to GameGuild:\n{meta.Code}\n\nThe code expires in 10 minutes and can only be used once. If you did not request it, you can ignore this email.";
        var html =
            $"<p>Hi {encodedName},</p><p>Use this one-time code to sign in to GameGuild:</p><p style=\"font-size:1.5em;letter-spacing:0.3em;\">{encodedCode}</p><p>The code expires in 10 minutes and can only be used once. If you did not request it, you can ignore this email.</p>";

        var (finalPlain, finalHtml) = MergeFooter(plain, html, footerService.Build(notification));
        var message = new EmailMessage(string.Empty, "Your GameGuild sign-in code", finalPlain, finalHtml, recipientName);
        return Task.FromResult<EmailMessage?>(message);
    }

    private static EmailCodeMetadata Deserialize(string? metadata)
    {
        if (string.IsNullOrWhiteSpace(metadata))
        {
            return EmailCodeMetadata.Empty;
        }

        try
        {
            return JsonSerializer.Deserialize<EmailCodeMetadata>(metadata, JsonOptions) ?? EmailCodeMetadata.Empty;
        }
        catch (JsonException)
        {
            return EmailCodeMetadata.Empty;
        }
    }

    private sealed record EmailCodeMetadata(string? Code, string? Email, string? UserName)
    {
        public static EmailCodeMetadata Empty { get; } = new(null, null, null);
    }
}
