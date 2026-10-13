namespace GameGuild.Commerce.Payments;

/// <summary>
///     Contributes fraud signals for a payment attempt (issue #409). Implementations are
///     tenant-scoped: the <see cref="FraudSignalContext"/> carries the tenant id and every
///     query an implementation performs must be filtered by it.
/// </summary>
public interface IFraudSignalProvider
{
    /// <summary>Stable provider identifier used in diagnostics.</summary>
    string Name { get; }

    /// <summary>
    ///     Evaluates the provider's signals for the given attempt using the tenant-resolved
    ///     options. Providers must only return bounded, evidence-grade data — never provider
    ///     tokens or raw credentials.
    /// </summary>
    /// <param name="context">The payment attempt being assessed.</param>
    /// <param name="options">Effective (tenant-resolved) assessment options.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Detected signals; empty when nothing fired.</returns>
    Task<IReadOnlyList<FraudSignal>> EvaluateAsync(
        FraudSignalContext context,
        FraudAssessmentOptions options,
        CancellationToken cancellationToken = default);
}
