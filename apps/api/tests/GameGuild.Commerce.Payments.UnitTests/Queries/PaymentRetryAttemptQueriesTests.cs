using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace GameGuild.Commerce.Payments.UnitTests.PaymentRecoveryQueries;

/// <summary>
///     Retry-attempt history query (issue #415): attempts are returned ordered by attempt
///     number for exactly the requested payment.
/// </summary>
public class PaymentRetryAttemptQueriesTests
{
    [Fact]
    public async Task GetRetryAttempts_ReturnsThePaymentsHistoryOrderedByAttemptNumber()
    {
        await using var context = await CreateContextAsync();
        var paymentId = Guid.NewGuid();
        var otherPaymentId = Guid.NewGuid();
        var executedAt = SystemClock.UtcNow;

        context.Set<PaymentRetryAttempt>().AddRange(
            PaymentRetryAttempt.Create(paymentId, 2, SystemClock.UtcNow.AddHours(-2), executedAt, PaymentStatus.Failed, "stripe", "insufficient_funds", "Not enough funds"),
            PaymentRetryAttempt.Create(paymentId, 1, SystemClock.UtcNow.AddHours(-3), executedAt, PaymentStatus.Failed, "stripe", "card_declined", "Declined"),
            PaymentRetryAttempt.Create(paymentId, 3, SystemClock.UtcNow.AddHours(-1), executedAt, PaymentStatus.Succeeded, "stripe", gatewayTransactionId: "tx_3"),
            PaymentRetryAttempt.Create(otherPaymentId, 1, null, executedAt, PaymentStatus.Failed, "stripe", "card_declined", "Declined"));
        await context.SaveChangesAsync();

        var handler = new GetPaymentRetryAttemptsQueryHandler(context);
        var attempts = await handler.Handle(new GetPaymentRetryAttemptsQuery(paymentId), CancellationToken.None);

        attempts.Should().HaveCount(3);
        attempts.Select(attempt => attempt.AttemptNumber).Should().ContainInOrder(1, 2, 3);
        attempts.Should().OnlyContain(attempt => attempt.PaymentId == paymentId);
        attempts.Last().Outcome.Should().Be(PaymentStatus.Succeeded);
        attempts.Last().GatewayTransactionId.Should().Be("tx_3");
        attempts.First().FailureCode.Should().Be("card_declined");
    }

    [Fact]
    public async Task GetRetryAttempts_UnknownPayment_ReturnsEmptyHistory()
    {
        await using var context = await CreateContextAsync();

        var handler = new GetPaymentRetryAttemptsQueryHandler(context);
        var attempts = await handler.Handle(new GetPaymentRetryAttemptsQuery(Guid.NewGuid()), CancellationToken.None);

        attempts.Should().BeEmpty();
    }

    private static async Task<PaymentsPersistenceTestDbContext> CreateContextAsync()
    {
        var context = new PaymentsPersistenceTestDbContext(
            new DbContextOptionsBuilder<PaymentsPersistenceTestDbContext>()
                .UseInMemoryDatabase($"retry-attempts-{Guid.NewGuid():N}")
                .Options);
        await context.Database.EnsureCreatedAsync();
        return context;
    }
}
