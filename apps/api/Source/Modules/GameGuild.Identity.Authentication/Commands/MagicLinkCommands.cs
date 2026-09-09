using GameGuild.CQRS;

namespace GameGuild.Identity.Authentication;

/// <summary>
///     Requests a one-time magic-link sign-in token for an email address.
/// </summary>
public sealed class RequestMagicLinkCommand : ICommand<MagicLinkRequestResult>
{
    public string Email { get; init; } = string.Empty;

    public Guid? TenantId { get; init; }

    public string? IpAddress { get; init; }

    public string? UserAgent { get; init; }
}

/// <summary>
///     Consumes a one-time magic-link token and issues authentication tokens.
/// </summary>
public sealed class ConsumeMagicLinkCommand : ICommand<SignInResponse>
{
    public string Token { get; init; } = string.Empty;

    public Guid? TenantId { get; init; }

    public string? DeviceFingerprint { get; init; }

    public string? IpAddress { get; init; }

    public string? UserAgent { get; init; }
}

public sealed class MagicLinkRequestResult
{
    public bool Success { get; init; } = true;

    public string Message { get; init; } = "If an account with that email exists, a magic sign-in link has been sent.";

    public int ExpiresInMinutes { get; init; } = 15;

    public string? DevelopmentPreviewToken { get; init; }
}
