using FluentValidation;
using GameGuild.CQRS;

namespace GameGuild.Commerce.Payments;

/// <summary>
///     Creates a refund request for a payment (issue #419). Amounts at or above the configured
///     manual-approval threshold (<c>Payments:Refunds:ManualApprovalThresholdAmount</c>) are
///     queued as <see cref="RefundStatus.Pending"/> for an administrator holding
///     <c>payments:refunds:approve</c>; amounts below the threshold (or all refunds when no
///     threshold is configured) execute immediately. The requester identity comes from the actor
///     context, never from the request body.
/// </summary>
/// <param name="PaymentId">Payment to refund.</param>
/// <param name="Amount">Refund amount; null refunds the full remaining amount.</param>
/// <param name="Reason">Short machine-readable reason (e.g. <c>duplicate</c>).</param>
/// <param name="ReasonDetails">Optional human-readable details.</param>
/// <param name="IdempotencyKey">Optional client idempotency key; replays return the recorded refund.</param>
/// <param name="Kind">
///     How the amount is determined (issue #419): Full (default) uses the remaining balance,
///     Partial the explicit amount, and Prorated computes the unused-time credit of the
///     subscription billing period (requires a subscription-linked payment).
/// </param>
public sealed record RequestRefundCommand(
    Guid PaymentId,
    decimal? Amount,
    string Reason,
    string? ReasonDetails = null,
    string? IdempotencyKey = null,
    RefundKind Kind = RefundKind.Full) : ICommand<RequestRefundResult>;

/// <summary>Validator for <see cref="RequestRefundCommand"/>.</summary>
public sealed class RequestRefundCommandValidator : AbstractValidator<RequestRefundCommand>
{
    public RequestRefundCommandValidator()
    {
        RuleFor(command => command.PaymentId).NotEmpty().WithMessage("Payment ID is required");
        RuleFor(command => command.Reason).NotEmpty().MaximumLength(100)
            .WithMessage("A refund reason is required (max 100 characters)");
        RuleFor(command => command.ReasonDetails).MaximumLength(1000);
        RuleFor(command => command.Amount).GreaterThan(0)
            .When(command => command.Amount.HasValue)
            .WithMessage("Refund amount must be greater than zero");
        RuleFor(command => command.IdempotencyKey).MaximumLength(200)
            .Matches("^[a-zA-Z0-9_\\-\\.]+$")
            .When(command => !string.IsNullOrWhiteSpace(command.IdempotencyKey))
            .WithMessage("Idempotency key may only contain letters, digits, '-', '_' and '.'");
        RuleFor(command => command.Kind).IsInEnum()
            .WithMessage("Refund kind must be a known value (full, partial or prorated)");
    }
}

/// <summary>Result of creating a refund request.</summary>
public sealed record RequestRefundResult
{
    /// <summary>Whether the request was recorded (or replayed) successfully.</summary>
    public required bool Success { get; init; }

    /// <summary>Whether the refund is queued for manual approval.</summary>
    public required bool RequiresApproval { get; init; }

    /// <summary>The recorded refund row, when successful.</summary>
    public RefundRecord? Refund { get; init; }

    /// <summary>Failure reason when <see cref="Success"/> is false.</summary>
    public string? FailureReason { get; init; }
}
