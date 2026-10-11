using GameGuild.CQRS;

namespace GameGuild.Commerce.Payments;

/// <summary>
///     Query for the retry-attempt history of one payment (issue #415): when each attempt was
///     scheduled and executed, its outcome, and the gateway failure code when it failed.
/// </summary>
/// <param name="PaymentId">Payment whose attempt history is requested.</param>
public sealed record GetPaymentRetryAttemptsQuery(Guid PaymentId) : IQuery<IReadOnlyList<PaymentRetryAttemptDto>>;

/// <summary>Read model for one persisted <see cref="PaymentRetryAttempt" /> (issue #415).</summary>
/// <param name="PaymentId">Payment the attempt belongs to.</param>
/// <param name="AttemptNumber">1-based retry attempt number.</param>
/// <param name="ScheduledForUtc">When the attempt was scheduled to run; null for immediate manual retries.</param>
/// <param name="ExecutedAtUtc">When the attempt was executed.</param>
/// <param name="Outcome">Outcome status of the attempt.</param>
/// <param name="FailureCode">Gateway failure code when the attempt failed.</param>
/// <param name="FailureMessage">Gateway failure message when the attempt failed.</param>
/// <param name="Gateway">Gateway that executed the attempt.</param>
/// <param name="GatewayTransactionId">Gateway transaction reference when one exists.</param>
public sealed record PaymentRetryAttemptDto(
    Guid PaymentId,
    int AttemptNumber,
    DateTime? ScheduledForUtc,
    DateTime ExecutedAtUtc,
    PaymentStatus Outcome,
    string? FailureCode,
    string? FailureMessage,
    string Gateway,
    string? GatewayTransactionId);
