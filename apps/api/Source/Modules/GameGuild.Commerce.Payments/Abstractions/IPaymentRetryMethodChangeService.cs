namespace GameGuild.Commerce.Payments;

/// <summary>
///     Pauses and resumes automatic payment retries around a payment-method change
///     (issue #415). While a customer swaps their payment instrument mid-dunning, queued
///     retries are paused so no attempt charges the failed instrument; once the new method
///     is confirmed, the paused payments are re-armed — pointed at the new method — and the
///     next retry-queue pass picks them up.
/// </summary>
public interface IPaymentRetryMethodChangeService
{
    /// <summary>
    ///     Pauses automatic retries for the subscription's failed payments: scheduled
    ///     next-retry times are cleared so the retry queue stops dispatching them. Payments
    ///     without retry budget or with non-retriable failures are left untouched.
    /// </summary>
    /// <param name="tenantId">Tenant that owns the subscription.</param>
    /// <param name="subscriptionId">Subscription whose failed payments are paused.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Number of payments whose retry schedule was paused.</returns>
    Task<int> PauseForMethodChangeAsync(
        Guid tenantId,
        Guid subscriptionId,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Resumes previously paused retries after the new payment method was confirmed at the
    ///     gateway: paused payments become eligible on the next retry-queue pass and are
    ///     re-pointed at the confirmed method so retries charge the new instrument.
    /// </summary>
    /// <param name="tenantId">Tenant that owns the subscription.</param>
    /// <param name="subscriptionId">Subscription whose paused retries resume.</param>
    /// <param name="confirmedPaymentMethodId">Gateway payment-method id confirmed for future charges.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Number of payments whose retry schedule was resumed.</returns>
    Task<int> ResumeAfterMethodChangeAsync(
        Guid tenantId,
        Guid subscriptionId,
        string confirmedPaymentMethodId,
        CancellationToken cancellationToken = default);
}
