using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GameGuild.Commerce.Payments;

/// <summary>
///     Default <see cref="IRevenueReconciliationService" /> (issue #404). Matching is
///     reference-based: an external line matches the first unconsumed internal revenue
///     event with the same reference in the period. Every mismatch — missing sides,
///     amount and currency differences, repeated external references — is recorded as an
///     immutable discrepancy on the run.
/// </summary>
public sealed class RevenueReconciliationService(
    IRevenueEventRepository revenueEventRepository,
    IRevenueReconciliationRepository reconciliationRepository,
    IExternalRevenueStatementSource externalStatementSource,
    IOptions<RevenueAuditingOptions> options,
    ILogger<RevenueReconciliationService> logger) : IRevenueReconciliationService
{
    private static readonly JsonSerializerOptions SummarySerializerOptions = new(JsonSerializerDefaults.Web);

    /// <inheritdoc />
    public async Task<RevenueReconciliationRun> ReconcileAsync(RevenueReconciliationRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var maxLines = options.Value.MaxStatementLinesPerRun;
        var lines = request.Lines ?? await externalStatementSource
            .GetLinesAsync(request.PeriodStartUtc, request.PeriodEndUtc, cancellationToken)
            .ConfigureAwait(false);

        if (lines.Count > maxLines)
        {
            throw new InvalidOperationException(
                $"The statement has {lines.Count} lines, which exceeds the maximum of {maxLines} lines per reconciliation run. Split the statement into smaller periods.");
        }

        var run = new RevenueReconciliationRun
        {
            TenantId = request.TenantId,
            Source = request.Source,
            ExternalStatementId = request.ExternalStatementId,
            PeriodStartUtc = request.PeriodStartUtc,
            PeriodEndUtc = request.PeriodEndUtc,
            StatementLineCount = lines.Count,
            InitiatedByUserId = request.InitiatedByUserId,
            StartedAtUtc = SystemClock.UtcNow
        };

        await reconciliationRepository.AddRunAsync(run, cancellationToken).ConfigureAwait(false);
        await reconciliationRepository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            var internalEvents = await revenueEventRepository
                .GetInPeriodAsync(request.PeriodStartUtc, request.PeriodEndUtc, request.TenantId, cancellationToken)
                .ConfigureAwait(false);

            // Only definitive rows participate: cancelled events never reconcile.
            var candidatesByReference = internalEvents
                .Where(revenueEvent => revenueEvent.Status != RevenueEventStatus.Cancelled)
                .GroupBy(revenueEvent => revenueEvent.ReferenceId, StringComparer.Ordinal)
                .ToDictionary(
                    group => group.Key,
                    group => new Queue<RevenueEvent>(group.OrderBy(revenueEvent => revenueEvent.Timestamp)),
                    StringComparer.Ordinal);

            var matched = 0;
            var discrepancies = new List<RevenueReconciliationDiscrepancy>();
            var seenExternalReferences = new HashSet<string>(StringComparer.Ordinal);

            foreach (var line in lines)
            {
                if (!seenExternalReferences.Add(line.ReferenceId))
                {
                    discrepancies.Add(CreateDiscrepancy(
                        run.Id,
                        request.TenantId,
                        RevenueDiscrepancyKind.DuplicateExternalReference,
                        line,
                        message: "The external statement repeats this reference."));
                    continue;
                }

                if (!candidatesByReference.TryGetValue(line.ReferenceId, out var candidates) || candidates.Count == 0)
                {
                    discrepancies.Add(CreateDiscrepancy(
                        run.Id,
                        request.TenantId,
                        RevenueDiscrepancyKind.MissingInternal,
                        line,
                        message: "No internal revenue event exists for this reference in the period."));
                    continue;
                }

                var internalEvent = candidates.Dequeue();
                if (!string.Equals(internalEvent.Currency, line.Currency, StringComparison.OrdinalIgnoreCase))
                {
                    discrepancies.Add(CreateDiscrepancy(
                        run.Id,
                        request.TenantId,
                        RevenueDiscrepancyKind.CurrencyMismatch,
                        line,
                        internalEvent,
                        message: $"Currency mismatch: internal '{internalEvent.Currency}' versus external '{line.Currency}'."));
                    continue;
                }

                if (decimal.Round(internalEvent.Amount, 2) != decimal.Round(line.Amount, 2))
                {
                    discrepancies.Add(CreateDiscrepancy(
                        run.Id,
                        request.TenantId,
                        RevenueDiscrepancyKind.AmountMismatch,
                        line,
                        internalEvent,
                        message: $"Amount mismatch: internal {internalEvent.Amount.ToString("0.00", CultureInfo.InvariantCulture)} versus external {line.Amount.ToString("0.00", CultureInfo.InvariantCulture)}."));
                    continue;
                }

                matched++;
            }

            // Internal events that the statement never claimed.
            foreach (var remaining in candidatesByReference.Values.SelectMany(queue => queue))
            {
                discrepancies.Add(new RevenueReconciliationDiscrepancy
                {
                    TenantId = request.TenantId,
                    RunId = run.Id,
                    Kind = RevenueDiscrepancyKind.MissingExternal,
                    ExternalReference = remaining.ReferenceId,
                    RevenueEventId = remaining.Id,
                    InternalAmount = remaining.Amount,
                    InternalCurrency = remaining.Currency,
                    Message = "An internal revenue event in the period was not present on the external statement."
                });
            }

            foreach (var discrepancy in discrepancies)
            {
                await reconciliationRepository.AddDiscrepancyAsync(discrepancy, cancellationToken).ConfigureAwait(false);
            }

            var summary = JsonSerializer.Serialize(
                new RevenueReconciliationSummary(
                    Matched: matched,
                    MissingInternal: discrepancies.Count(d => d.Kind == RevenueDiscrepancyKind.MissingInternal),
                    MissingExternal: discrepancies.Count(d => d.Kind == RevenueDiscrepancyKind.MissingExternal),
                    AmountMismatch: discrepancies.Count(d => d.Kind == RevenueDiscrepancyKind.AmountMismatch),
                    CurrencyMismatch: discrepancies.Count(d => d.Kind == RevenueDiscrepancyKind.CurrencyMismatch),
                    DuplicateExternalReference: discrepancies.Count(d => d.Kind == RevenueDiscrepancyKind.DuplicateExternalReference)),
                SummarySerializerOptions);

            run.Complete(matched, discrepancies.Count, summary);
            await reconciliationRepository.UpdateRunAsync(run, cancellationToken).ConfigureAwait(false);
            await reconciliationRepository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            logger.LogInformation(
                "Revenue reconciliation {RunId} for source {Source} completed: {Matched} matched, {Discrepancies} discrepancies.",
                run.Id,
                run.Source,
                matched,
                discrepancies.Count);

            return run;
        }
        catch (Exception exception)
        {
            if (run.Status == RevenueReconciliationStatus.Running)
            {
                run.Fail(exception.Message);
                await reconciliationRepository.UpdateRunAsync(run, cancellationToken).ConfigureAwait(false);
                await reconciliationRepository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            }

            throw;
        }
    }

    private static RevenueReconciliationDiscrepancy CreateDiscrepancy(
        Guid runId,
        Guid? tenantId,
        RevenueDiscrepancyKind kind,
        ExternalRevenueStatementLine line,
        string message) => new()
    {
        TenantId = tenantId,
        RunId = runId,
        Kind = kind,
        ExternalReference = line.ReferenceId,
        ExternalAmount = decimal.Round(line.Amount, 2),
        ExternalCurrency = line.Currency.ToUpperInvariant(),
        ExternalOccurredAtUtc = line.OccurredAtUtc,
        Message = message
    };

    private static RevenueReconciliationDiscrepancy CreateDiscrepancy(
        Guid runId,
        Guid? tenantId,
        RevenueDiscrepancyKind kind,
        ExternalRevenueStatementLine line,
        RevenueEvent internalEvent,
        string message)
    {
        var discrepancy = CreateDiscrepancy(runId, tenantId, kind, line, message);
        discrepancy.RevenueEventId = internalEvent.Id;
        discrepancy.InternalAmount = decimal.Round(internalEvent.Amount, 2);
        discrepancy.InternalCurrency = internalEvent.Currency;
        return discrepancy;
    }

    private sealed record RevenueReconciliationSummary(
        int Matched,
        int MissingInternal,
        int MissingExternal,
        int AmountMismatch,
        int CurrencyMismatch,
        int DuplicateExternalReference);
}
