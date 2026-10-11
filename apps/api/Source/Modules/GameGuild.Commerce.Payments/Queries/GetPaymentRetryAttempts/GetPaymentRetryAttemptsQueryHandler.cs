using GameGuild.CQRS;
using Microsoft.EntityFrameworkCore;

namespace GameGuild.Commerce.Payments;

/// <summary>
///     Handler for <see cref="GetPaymentRetryAttemptsQuery" />: returns the persisted attempt
///     history ordered by attempt number (issue #415).
/// </summary>
public sealed class GetPaymentRetryAttemptsQueryHandler(IApplicationDbContext context)
    : IQueryHandler<GetPaymentRetryAttemptsQuery, IReadOnlyList<PaymentRetryAttemptDto>>
{
    public async Task<IReadOnlyList<PaymentRetryAttemptDto>> Handle(
        GetPaymentRetryAttemptsQuery request,
        CancellationToken cancellationToken)
    {
        var attempts = await context.Set<PaymentRetryAttempt>()
            .AsNoTracking()
            .Where(attempt => attempt.PaymentId == request.PaymentId)
            .OrderBy(attempt => attempt.AttemptNumber)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return attempts
            .Select(attempt => new PaymentRetryAttemptDto(
                attempt.PaymentId,
                attempt.AttemptNumber,
                attempt.ScheduledForUtc,
                attempt.ExecutedAtUtc,
                attempt.Outcome,
                attempt.FailureCode,
                attempt.FailureMessage,
                attempt.Gateway,
                attempt.GatewayTransactionId))
            .ToList();
    }
}
