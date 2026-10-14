using GameGuild.CQRS;

namespace GameGuild.Identity.Authentication;

// ── SMS OTP factor (issue #244) ──────────────────────────────────────────────

/// <summary>Starts SMS MFA enrollment by sending a verification code to the given phone.</summary>
public sealed record StartSmsMfaEnrollmentCommand(Guid UserId, string PhoneNumber) : ICommand<SmsMfaChallengeResult>;

/// <summary>Completes SMS MFA enrollment by verifying the code sent to the pending phone.</summary>
public sealed record CompleteSmsMfaEnrollmentCommand(Guid UserId, string Code) : ICommand<MfaVerificationResult>;

/// <summary>Sends a fresh SMS sign-in challenge to the user's enrolled phone (throttled).</summary>
public sealed record SendSmsMfaChallengeCommand(Guid UserId) : ICommand<SmsMfaChallengeResult>;

// ── Push approval factor, v1 contract (issue #259) ───────────────────────────

/// <summary>Registers (or replaces) the mobile device token for push MFA.</summary>
public sealed record RegisterPushMfaDeviceCommand(Guid UserId, string DeviceToken) : ICommand<bool>;

/// <summary>Issues a push approval challenge and queues its notification.</summary>
public sealed record IssuePushMfaChallengeCommand(Guid UserId) : ICommand<PushMfaChallengeResult>;

/// <summary>Records the device's approve/deny decision for a pending challenge.</summary>
public sealed record RespondPushMfaChallengeCommand(string ChallengeId, bool Approved) : ICommand<MfaVerificationResult>;

/// <summary>Handlers for the SMS and push factor commands.</summary>
public sealed class SmsPushMfaCommandHandler(
    ISmsMfaService smsMfaService,
    IPushMfaService pushMfaService) :
    ICommandHandler<StartSmsMfaEnrollmentCommand, SmsMfaChallengeResult>,
    ICommandHandler<CompleteSmsMfaEnrollmentCommand, MfaVerificationResult>,
    ICommandHandler<SendSmsMfaChallengeCommand, SmsMfaChallengeResult>,
    ICommandHandler<RegisterPushMfaDeviceCommand, bool>,
    ICommandHandler<IssuePushMfaChallengeCommand, PushMfaChallengeResult>,
    ICommandHandler<RespondPushMfaChallengeCommand, MfaVerificationResult>
{
    public Task<SmsMfaChallengeResult> Handle(StartSmsMfaEnrollmentCommand command, CancellationToken cancellationToken) =>
        smsMfaService.StartEnrollmentAsync(command.UserId, command.PhoneNumber, cancellationToken);

    public Task<MfaVerificationResult> Handle(CompleteSmsMfaEnrollmentCommand command, CancellationToken cancellationToken) =>
        smsMfaService.CompleteEnrollmentAsync(command.UserId, command.Code, cancellationToken);

    public Task<SmsMfaChallengeResult> Handle(SendSmsMfaChallengeCommand command, CancellationToken cancellationToken) =>
        smsMfaService.SendChallengeAsync(command.UserId, cancellationToken);

    public Task<bool> Handle(RegisterPushMfaDeviceCommand command, CancellationToken cancellationToken) =>
        pushMfaService.RegisterDeviceAsync(command.UserId, command.DeviceToken, cancellationToken);

    public Task<PushMfaChallengeResult> Handle(IssuePushMfaChallengeCommand command, CancellationToken cancellationToken) =>
        pushMfaService.IssueChallengeAsync(command.UserId, cancellationToken);

    public Task<MfaVerificationResult> Handle(RespondPushMfaChallengeCommand command, CancellationToken cancellationToken)
    {
        if (!Guid.TryParseExact(command.ChallengeId?.Trim(), "N", out var challengeId))
        {
            return Task.FromResult(new MfaVerificationResult { Success = false, Message = "Invalid challenge id" });
        }

        return pushMfaService.RespondAsync(challengeId, command.Approved, cancellationToken);
    }
}
