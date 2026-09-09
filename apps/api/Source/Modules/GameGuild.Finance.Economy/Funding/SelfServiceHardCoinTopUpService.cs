using GameGuild.Finance.Economy.Contracts;
using GameGuild.Finance.Economy.Ledger;
using GameGuild.Identity.Context.Actors;

namespace GameGuild.Finance.Economy.Funding;

public enum EconomyTopUpProviderStatus
{
    Prepared = 1,
    RequiresAction = 2,
    Processing = 3,
    ProviderSucceeded = 4,
    Posted = 5,
    Failed = 6,
    Cancelled = 7,
    Ambiguous = 8,
    Held = 9,
    Reversed = 10
}

public sealed record SelfServiceHardCoinTopUpRequest(
    long HardCoinUnits,
    string IdempotencyKey);

public sealed record SelfServiceHardCoinTopUpReceipt(
    Guid TopUpId,
    Guid PaymentId,
    long HardCoinUnits,
    long UsdMinorUnits,
    string Currency,
    EconomyTopUpProviderStatus Status,
    string ClientSecret,
    string PublishableKey,
    bool IsDuplicate)
{
    public string ProviderObjectId { get; init; } = string.Empty;
}

public sealed record EconomyTopUpIntentDraft(
    Guid TenantId,
    Guid ActorId,
    WalletId WalletId,
    long HardCoinUnits,
    long UsdMinorUnits,
    string JurisdictionCode,
    long PolicyVersion,
    string PolicyHash,
    string Provider,
    IdempotencyKey IdempotencyKey,
    DateTimeOffset RequestedAt);

public sealed record PreparedEconomyTopUpIntent(
    Guid Id,
    Guid PaymentId,
    Guid TenantId,
    Guid ActorId,
    WalletId WalletId,
    long HardCoinUnits,
    long UsdMinorUnits,
    string JurisdictionCode,
    long PolicyVersion,
    string PolicyHash,
    string Provider,
    IdempotencyKey IdempotencyKey,
    string RequestHash,
    string? ProviderEnvironment,
    string? ProviderAccountId,
    string? ProviderObjectId,
    EconomyTopUpProviderStatus Status,
    DateTimeOffset RequestedAt,
    bool IsDuplicate);

public sealed record EconomyTopUpProviderCreateRequest(
    Guid TopUpId,
    Guid TenantId,
    long UsdMinorUnits,
    string Currency,
    string IdempotencyKey);

public sealed record EconomyTopUpProviderResult(
    string Provider,
    string ProviderEnvironment,
    string ProviderAccountId,
    string ProviderObjectId,
    string ProviderObjectType,
    string ProviderMonetaryLeg,
    EconomyTopUpProviderStatus Status,
    string ClientSecret,
    string PublishableKey);

public sealed record EconomyTopUpProviderBinding(
    Guid TopUpId,
    string Provider,
    string ProviderEnvironment,
    string ProviderAccountId,
    string ProviderObjectId,
    string ProviderObjectType,
    string ProviderMonetaryLeg,
    EconomyTopUpProviderStatus Status,
    DateTimeOffset BoundAt);

public sealed record EconomyTopUpStatusDto(
    Guid TopUpId,
    long HardCoinUnits,
    long UsdMinorUnits,
    string Currency,
    EconomyTopUpProviderStatus Status,
    string? ProviderObjectId,
    DateTimeOffset RequestedAt,
    DateTimeOffset? ProviderBoundAt);

public sealed record EconomyTopUpProviderIdentity(
    string Provider,
    string ProviderEnvironment,
    string ProviderAccountId,
    string ProviderObjectId,
    string ProviderObjectType,
    string ProviderMonetaryLeg);

public sealed record EconomyTopUpSettlementContext(
    PreparedEconomyTopUpIntent TopUp,
    EconomyTopUpPaymentFact Payment);

public sealed record EconomyTopUpPaymentFact(
    Guid Id,
    Guid TenantId,
    decimal Amount,
    string Currency,
    string Provider,
    string ProviderEnvironment,
    string ProviderAccountId,
    string ProviderObjectId,
    string ProviderObjectType,
    string ProviderMonetaryLeg);

public sealed record EconomyTopUpProviderEvent(
    EconomyTopUpProviderIdentity Identity,
    string EventId,
    DateTimeOffset OccurredAt,
    EconomyTopUpProviderStatus Status,
    string EvidenceHash,
    long ProviderUsdMinorUnits,
    string Currency,
    Guid? PostingGroupId = null,
    string? FailureCode = null);

public sealed record EconomyTopUpProviderEventResult(
    bool Applied,
    bool Duplicate,
    EconomyTopUpProviderStatus Status);

public interface IEconomyTopUpIntentStore
{
    ValueTask<PreparedEconomyTopUpIntent> PrepareAsync(
        EconomyTopUpIntentDraft draft,
        CancellationToken cancellationToken);

    ValueTask BindProviderAsync(
        EconomyTopUpProviderBinding binding,
        CancellationToken cancellationToken);
}

public interface IEconomyTopUpReader
{
    ValueTask<EconomyTopUpStatusDto?> GetAsync(
        Guid tenantId,
        Guid actorId,
        Guid topUpId,
        CancellationToken cancellationToken);

    ValueTask<IReadOnlyList<EconomyTopUpStatusDto>> ListAsync(
        Guid tenantId,
        Guid actorId,
        int take,
        CancellationToken cancellationToken);
}

public interface IEconomyTopUpSettlementStore
{
    ValueTask<EconomyTopUpSettlementContext?> FindAsync(
        EconomyTopUpProviderIdentity identity,
        CancellationToken cancellationToken);

    ValueTask<EconomyTopUpProviderEventResult> ApplyAsync(
        EconomyTopUpProviderEvent providerEvent,
        CancellationToken cancellationToken);
}

public interface IEconomyTopUpProvider
{
    ValueTask<EconomyTopUpProviderResult> CreateAsync(
        EconomyTopUpProviderCreateRequest request,
        CancellationToken cancellationToken);

    ValueTask<EconomyTopUpProviderResult> RetrieveAsync(
        string providerObjectId,
        CancellationToken cancellationToken);
}

public interface ISelfServiceHardCoinTopUpService
{
    Task<SelfServiceHardCoinTopUpReceipt> CreateAsync(
        SelfServiceHardCoinTopUpRequest request,
        CancellationToken cancellationToken);
}

public sealed class EconomyTopUpReplayConflictException(string message, Exception? innerException = null)
    : InvalidOperationException(message, innerException);

public sealed class SelfServiceHardCoinTopUpService(
    IActorContextAccessor actorContextAccessor,
    IEconomyWalletDirectory wallets,
    IHardCoinTopUpPolicyResolver policies,
    IEconomyTopUpIntentStore intents,
    IEconomyTopUpProvider provider,
    TimeProvider timeProvider) : ISelfServiceHardCoinTopUpService
{
    private const string ProviderCurrency = "USD";

    public async Task<SelfServiceHardCoinTopUpReceipt> CreateAsync(
        SelfServiceHardCoinTopUpRequest request,
        CancellationToken cancellationToken)
    {
        Validate(request);
        cancellationToken.ThrowIfCancellationRequested();
        var actor = RequiredActor();
        var requestedAt = timeProvider.GetUtcNow();
        var wallet = await wallets.GetOwnerWalletAsync(
            actor.TenantId, actor.ActorId, cancellationToken).ConfigureAwait(false);
        var policy = await policies.ResolveAsync(
            actor.TenantId,
            actor.ActorId,
            request.HardCoinUnits,
            requestedAt,
            cancellationToken).ConfigureAwait(false);
        var key = new IdempotencyKey(request.IdempotencyKey);
        var prepared = await intents.PrepareAsync(
            new EconomyTopUpIntentDraft(
                actor.TenantId,
                actor.ActorId,
                wallet.WalletId,
                policy.HardCoinUnits,
                policy.UsdMinorUnits,
                policy.JurisdictionCode,
                policy.PolicyVersion,
                policy.PolicyHash,
                policy.Provider,
                key,
                requestedAt),
            cancellationToken).ConfigureAwait(false);
        EnsurePrepared(prepared, actor, wallet, policy, key);

        var providerResult = prepared.ProviderObjectId is { Length: > 0 } providerObjectId
            ? await provider.RetrieveAsync(providerObjectId, cancellationToken).ConfigureAwait(false)
            : await provider.CreateAsync(
                new EconomyTopUpProviderCreateRequest(
                    prepared.Id,
                    actor.TenantId,
                    prepared.UsdMinorUnits,
                    ProviderCurrency,
                    key.Value),
                cancellationToken).ConfigureAwait(false);
        EnsureProviderResult(prepared, providerResult);

        if (prepared.ProviderObjectId is null)
        {
            await intents.BindProviderAsync(
                new EconomyTopUpProviderBinding(
                    prepared.Id,
                    providerResult.Provider,
                    providerResult.ProviderEnvironment,
                    providerResult.ProviderAccountId,
                    providerResult.ProviderObjectId,
                    providerResult.ProviderObjectType,
                    providerResult.ProviderMonetaryLeg,
                    providerResult.Status,
                    requestedAt),
                cancellationToken).ConfigureAwait(false);
        }

        return new SelfServiceHardCoinTopUpReceipt(
            prepared.Id,
            prepared.PaymentId,
            prepared.HardCoinUnits,
            prepared.UsdMinorUnits,
            ProviderCurrency,
            providerResult.Status,
            providerResult.ClientSecret,
            providerResult.PublishableKey,
            prepared.IsDuplicate)
        {
            ProviderObjectId = providerResult.ProviderObjectId
        };
    }

    private (Guid TenantId, Guid ActorId) RequiredActor()
    {
        var actor = actorContextAccessor.ActorContext;
        if (!actor.IsAuthenticated || actor.TenantId is not { } tenantId ||
            actor.SubjectIdAsGuid is not { } actorId)
            throw new UnauthorizedAccessException(
                "An Economy top-up requires an authenticated tenant actor.");
        return (tenantId, actorId);
    }

    internal static void Validate(SelfServiceHardCoinTopUpRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(request.HardCoinUnits);
        _ = new IdempotencyKey(request.IdempotencyKey);
    }

    internal static void EnsurePrepared(
        PreparedEconomyTopUpIntent prepared,
        (Guid TenantId, Guid ActorId) actor,
        EconomyWalletIdentity wallet,
        HardCoinTopUpPolicyAuthorization policy,
        IdempotencyKey key)
    {
        ArgumentNullException.ThrowIfNull(prepared);
        if (prepared.TenantId != actor.TenantId || prepared.ActorId != actor.ActorId ||
            prepared.WalletId != wallet.WalletId || prepared.HardCoinUnits != policy.HardCoinUnits ||
            prepared.UsdMinorUnits != policy.UsdMinorUnits || prepared.JurisdictionCode != policy.JurisdictionCode ||
            prepared.PolicyVersion != policy.PolicyVersion || prepared.PolicyHash != policy.PolicyHash ||
            prepared.Provider != policy.Provider || prepared.IdempotencyKey != key)
            throw new EconomyTopUpReplayConflictException(
                "The top-up idempotency key is already bound to another request.");
        ArgumentException.ThrowIfNullOrWhiteSpace(prepared.RequestHash);
    }

    internal static void EnsureProviderResult(
        PreparedEconomyTopUpIntent prepared,
        EconomyTopUpProviderResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        if (!string.Equals(result.Provider, prepared.Provider, StringComparison.Ordinal) ||
            string.IsNullOrWhiteSpace(result.ProviderEnvironment) ||
            string.IsNullOrWhiteSpace(result.ProviderAccountId) ||
            string.IsNullOrWhiteSpace(result.ProviderObjectId) ||
            !string.Equals(result.ProviderObjectType, "payment_intent", StringComparison.Ordinal) ||
            !string.Equals(result.ProviderMonetaryLeg, "capture", StringComparison.Ordinal) ||
            string.IsNullOrWhiteSpace(result.ClientSecret) ||
            string.IsNullOrWhiteSpace(result.PublishableKey) ||
            result.Status is not (EconomyTopUpProviderStatus.RequiresAction or
                EconomyTopUpProviderStatus.Processing))
            throw new EconomySelfServiceCommandRejectedException(
                "The top-up provider returned an invalid payment intent binding.");
        if (prepared.ProviderObjectId is not null &&
            !string.Equals(prepared.ProviderObjectId, result.ProviderObjectId, StringComparison.Ordinal))
            throw new EconomySelfServiceCommandRejectedException(
                "The top-up provider object cannot be rebound.");
    }
}
