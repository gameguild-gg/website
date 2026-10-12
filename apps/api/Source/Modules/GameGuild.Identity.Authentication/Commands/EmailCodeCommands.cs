using GameGuild.CQRS;

namespace GameGuild.Identity.Authentication;

/// <summary>
///     Requests a one-time six-digit email sign-in code for an email address.
/// </summary>
public sealed class RequestEmailCodeCommand : ICommand<EmailCodeRequestResult>
{
    public string Email { get; init; } = string.Empty;

    public Guid? TenantId { get; init; }

    public string? IpAddress { get; init; }

    public string? UserAgent { get; init; }
}

/// <summary>
///     Consumes a one-time email sign-in code and issues authentication tokens.
/// </summary>
public sealed class ConsumeEmailCodeCommand : ICommand<SignInResponse>
{
    public string Email { get; init; } = string.Empty;

    public string Code { get; init; } = string.Empty;

    public Guid? TenantId { get; init; }

    public string? DeviceFingerprint { get; init; }

    public string? IpAddress { get; init; }

    public string? UserAgent { get; init; }
}

public sealed class EmailCodeRequestResult
{
    public bool Success { get; init; } = true;

    public string Message { get; init; } = "If an account with that email exists, a one-time sign-in code has been sent.";

    public int ExpiresInMinutes { get; init; } = 10;
}
