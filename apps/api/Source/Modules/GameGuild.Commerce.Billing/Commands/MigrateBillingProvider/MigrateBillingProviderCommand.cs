using FluentValidation;
using GameGuild.Commerce.Billing;
using GameGuild.Commerce.Subscriptions;
using GameGuild.Compliance.Audit;
using GameGuild.CQRS;
using GameGuild.Identity.Context.Actors;
using Microsoft.Extensions.Logging;

namespace GameGuild.Commerce.Billing;

/// <summary>
///     Report-only dry-run of a billing provider migration (issue #397): given a source
///     and a target provider key, classifies every subscription by its external-provider
///     binding and lists the ones bound to the source provider that lack a
///     target-provider external identifier. Executing a migration (state mutation) is
///     explicitly out of scope; gateway routing and failover are tracked in issue #413.
/// </summary>
public sealed record MigrateBillingProviderCommand(string SourceProvider, string TargetProvider)
    : ICommand<BillingProviderMigrationReport>;

/// <summary>
///     Validator for <see cref="MigrateBillingProviderCommand"/>: both provider keys must
///     be supported and distinct (fail-closed on unknown keys).
/// </summary>
public sealed class MigrateBillingProviderCommandValidator : AbstractValidator<MigrateBillingProviderCommand>
{
    public MigrateBillingProviderCommandValidator()
    {
        RuleFor(command => command.SourceProvider)
            .NotEmpty()
            .Must(provider => PaymentProviders.IsSupported(provider))
            .WithMessage("SourceProvider must be a supported external billing provider (stripe, paypal, applepay, apple_app_store, googlepay, google_play_store).");

        RuleFor(command => command.TargetProvider)
            .NotEmpty()
            .Must(provider => PaymentProviders.IsSupported(provider))
            .WithMessage("TargetProvider must be a supported external billing provider (stripe, paypal, applepay, apple_app_store, googlepay, google_play_store).");

        RuleFor(command => command)
            .Must(command => !string.Equals(
                PaymentProviders.Normalize(command.SourceProvider),
                PaymentProviders.Normalize(command.TargetProvider),
                StringComparison.Ordinal))
            .WithMessage("SourceProvider and TargetProvider must differ.")
            .WithName("TargetProvider");
    }
}

/// <summary>
///     Handler for <see cref="MigrateBillingProviderCommand"/>. Reads subscriptions
///     through <see cref="ISubscriptionRepository"/> and never mutates subscription or
///     provider state; the report itself is the deliverable.
/// </summary>
public sealed class MigrateBillingProviderCommandHandler(
    ISubscriptionRepository subscriptionRepository,
    IActorContextAccessor actorContextAccessor,
    IAuditService auditService,
    ILogger<MigrateBillingProviderCommandHandler> logger) : ICommandHandler<MigrateBillingProviderCommand, BillingProviderMigrationReport>
{
    private const int PageSize = 200;
    private const int MaxReportEntries = 200;

    /// <summary>Hard stop against pathological paging loops (defensive; 200 x 5,000 rows).</summary>
    private const int MaxPages = 5_000;

    public async Task<BillingProviderMigrationReport> Handle(MigrateBillingProviderCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var sourceProvider = PaymentProviders.Normalize(command.SourceProvider);
        var targetProvider = PaymentProviders.Normalize(command.TargetProvider);

        var boundToSource = new List<BillingProviderMigrationReportEntry>();
        var alreadyOnTarget = new List<BillingProviderMigrationReportEntry>();
        var unattributed = new List<BillingProviderMigrationReportEntry>();
        var notExternallyBound = new List<BillingProviderMigrationReportEntry>();
        var totalScanned = 0;

        // Read-only paging scan; no writes are issued against any store.
        var page = 1;
        while (!cancellationToken.IsCancellationRequested && page <= MaxPages)
        {
            var result = await subscriptionRepository
                .GetPagedAsync(page, PageSize, cancellationToken: cancellationToken)
                .ConfigureAwait(false);

            foreach (var subscription in result.Items)
            {
                totalScanned++;

                var entry = Classify(subscription, sourceProvider, targetProvider);

                switch (entry.Classification)
                {
                    case BillingProviderMigrationEntryClassification.BoundToSourceMissingTargetExternalId:
                        boundToSource.Add(entry);
                        break;
                    case BillingProviderMigrationEntryClassification.AlreadyOnTargetProvider:
                        alreadyOnTarget.Add(entry);
                        break;
                    case BillingProviderMigrationEntryClassification.UnattributedExternalId:
                        unattributed.Add(entry);
                        break;
                    default:
                        notExternallyBound.Add(entry);
                        break;
                }
            }

            var reachedEnd = result.Items.Count < PageSize
                || result.Items.Count == 0
                || (result.TotalCount > 0 && totalScanned >= result.TotalCount);

            if (reachedEnd)
            {
                break;
            }

            page++;
        }

        var report = new BillingProviderMigrationReport
        {
            SourceProvider = sourceProvider,
            TargetProvider = targetProvider,
            DryRun = true,
            MutationApplied = false,
            GeneratedAtUtc = DateTimeOffset.UtcNow,
            TotalSubscriptionsScanned = totalScanned,
            BoundToSourceMissingTargetExternalIdCount = boundToSource.Count,
            AlreadyOnTargetProviderCount = alreadyOnTarget.Count,
            UnattributedExternalIdCount = unattributed.Count,
            NotExternallyBoundCount = notExternallyBound.Count,
            Entries = boundToSource
                .Take(MaxReportEntries)
                .Concat(unattributed.Take(MaxReportEntries))
                .Concat(alreadyOnTarget.Take(MaxReportEntries))
                .Concat(notExternallyBound.Take(MaxReportEntries))
                .ToList()
        };

        logger.LogInformation(
            "Billing provider migration dry-run {SourceProvider} -> {TargetProvider}: {Total} scanned, {Bound} bound to source lacking target external id, {Already} already on target, {Unattributed} unattributed",
            sourceProvider,
            targetProvider,
            report.TotalSubscriptionsScanned,
            report.BoundToSourceMissingTargetExternalIdCount,
            report.AlreadyOnTargetProviderCount,
            report.UnattributedExternalIdCount);

        await AuditDryRunAsync(sourceProvider, targetProvider, report, cancellationToken).ConfigureAwait(false);

        return report;
    }

    private static BillingProviderMigrationReportEntry Classify(Subscription subscription, string sourceProvider, string targetProvider)
    {
        BillingProviderMigrationEntryClassification classification;

        if (string.IsNullOrWhiteSpace(subscription.ExternalId))
        {
            classification = BillingProviderMigrationEntryClassification.NotExternallyBound;
        }
        else if (BillingProviderExternalIdRecognizer.TryRecognize(subscription.ExternalId, out var recognizedProvider))
        {
            if (string.Equals(recognizedProvider, sourceProvider, StringComparison.Ordinal))
            {
                // The subscription stores a single external identifier namespace; an
                // identifier attributed to the source provider means the subscription
                // has no target-provider external identifier yet.
                classification = BillingProviderMigrationEntryClassification.BoundToSourceMissingTargetExternalId;
            }
            else if (string.Equals(recognizedProvider, targetProvider, StringComparison.Ordinal))
            {
                classification = BillingProviderMigrationEntryClassification.AlreadyOnTargetProvider;
            }
            else
            {
                classification = BillingProviderMigrationEntryClassification.UnattributedExternalId;
            }
        }
        else
        {
            classification = BillingProviderMigrationEntryClassification.UnattributedExternalId;
        }

        return new BillingProviderMigrationReportEntry
        {
            SubscriptionId = subscription.Id,
            TenantId = subscription.TenantId
                ?? throw new InvalidOperationException("TenantId is required for subscription entities but was null. This indicates a data integrity issue."),
            ExternalId = subscription.ExternalId,
            Status = subscription.Status.ToString(),
            Classification = classification
        };
    }

    private async Task AuditDryRunAsync(
        string sourceProvider,
        string targetProvider,
        BillingProviderMigrationReport report,
        CancellationToken cancellationToken)
    {
        var actor = actorContextAccessor.ActorContext;
        var actorUserId = actor.SubjectIdAsGuid;

        try
        {
            await auditService.LogAsync(new CreateAuditLogRequest
            {
                ActionType = AuditActionTypes.BillingProviderMigrationDryRun,
                ResourceType = "ExternalBillingProvider",
                ResourceId = $"{sourceProvider}->{targetProvider}",
                UserId = actorUserId,
                Description = $"Billing provider migration dry-run report generated for {sourceProvider} -> {targetProvider} (no state mutated).",
                Metadata = new Dictionary<string, object?>
                {
                    ["sourceProvider"] = sourceProvider,
                    ["targetProvider"] = targetProvider,
                    ["dryRun"] = true,
                    ["totalSubscriptionsScanned"] = report.TotalSubscriptionsScanned,
                    ["boundToSourceMissingTargetExternalId"] = report.BoundToSourceMissingTargetExternalIdCount,
                    ["alreadyOnTargetProvider"] = report.AlreadyOnTargetProviderCount,
                    ["unattributedExternalId"] = report.UnattributedExternalIdCount
                },
                Success = true,
                RiskLevel = AuditRiskLevel.Low,
                Category = AuditCategory.General
            }).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Failed to write the audit record for the billing provider migration dry-run {SourceProvider} -> {TargetProvider}; the report is still returned",
                sourceProvider,
                targetProvider);
        }
    }
}
