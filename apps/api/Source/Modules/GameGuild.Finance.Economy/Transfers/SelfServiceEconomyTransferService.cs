using GameGuild.Finance.Economy.Contracts;
using GameGuild.Finance.Economy.Ledger;
using GameGuild.Finance.Economy.Risk;
using GameGuild.Identity.Context.Actors;

namespace GameGuild.Finance.Economy.Transfers;

public enum SelfServiceEconomyTransferType
{
    Tip = 1,
    Gift = 2,
    CreatorSupport = 3
}

public sealed record SelfServiceEconomyTransferRequest(
    Guid RecipientUserId,
    SelfServiceEconomyTransferType TransferType,
    CurrencyCode Currency,
    long AmountUnits,
    string IdempotencyKey);

public sealed record SelfServiceEconomyTransferReceipt(
    Guid PostingId,
    SelfServiceEconomyTransferType TransferType,
    CurrencyCode Currency,
    long AmountUnits,
    Guid RecipientUserId,
    long JournalSequence,
    string JournalHash,
    bool IsDuplicate);

public sealed record SelfServiceEconomyTransferIntentDraft(
    Guid TenantId,
    Guid ActorId,
    Guid RecipientUserId,
    SelfServiceEconomyTransferType TransferType,
    CurrencyCode Currency,
    ProvenanceKind Provenance,
    long AmountUnits,
    IdempotencyKey IdempotencyKey,
    DateTimeOffset RequestedAt);

public sealed record PreparedSelfServiceEconomyTransferIntent(
    PostingId PostingId,
    Guid TenantId,
    Guid ActorId,
    Guid RecipientUserId,
    SelfServiceEconomyTransferType TransferType,
    CurrencyCode Currency,
    ProvenanceKind Provenance,
    long AmountUnits,
    IdempotencyKey IdempotencyKey,
    string RequestHash,
    string ProviderReferenceHash,
    string DestinationHash,
    DateTimeOffset RequestedAt);

public interface ISelfServiceEconomyTransferIntentStore
{
    ValueTask<PreparedSelfServiceEconomyTransferIntent> PrepareAsync(
        SelfServiceEconomyTransferIntentDraft draft,
        CancellationToken cancellationToken = default);
}

public interface ISelfServiceEconomyTransferService
{
    Task<SelfServiceEconomyTransferReceipt> TransferAsync(
        SelfServiceEconomyTransferRequest request,
        CancellationToken cancellationToken = default);
}

public sealed class SelfServiceEconomyTransferService(
    IActorContextAccessor actorContextAccessor,
    IEconomyWalletDirectory wallets,
    ISelfServiceEconomyTransferIntentStore intents,
    ISelfServiceEconomyTransferSourceRootPlanner sourceRoots,
    IEconomyProtectedOperationOrchestrator orchestrator,
    IEconomyProtectedOperationTransaction transaction,
    IRegisteredPostingCapabilityResolver postingAuthorities,
    IFifoTransferGateway transfers,
    TimeProvider timeProvider) : ISelfServiceEconomyTransferService
{
    private const string RegisteredCapabilityName = "fifo-transfer";

    public async Task<SelfServiceEconomyTransferReceipt> TransferAsync(
        SelfServiceEconomyTransferRequest request,
        CancellationToken cancellationToken = default)
    {
        Validate(request);
        var actor = RequiredActor();
        if (request.RecipientUserId == actor.ActorId)
            throw new SelfServiceEconomyTransferException(
                "A transfer recipient must differ from the authenticated actor.");

        var sourceWallet = await wallets.GetOwnerWalletAsync(
            actor.TenantId, actor.ActorId, cancellationToken).ConfigureAwait(false);
        var destinationWallet = await wallets.GetOwnerWalletAsync(
            actor.TenantId, request.RecipientUserId, cancellationToken).ConfigureAwait(false);
        if (sourceWallet.WalletId == destinationWallet.WalletId)
            throw new SelfServiceEconomyTransferException(
                "A transfer recipient must have a distinct Economy wallet.");

        return await transaction.ExecuteAsync(async token =>
        {
            var idempotencyKey = new IdempotencyKey(request.IdempotencyKey);
            var provenance = ProvenanceFor(request.Currency);
            var prepared = await intents.PrepareAsync(
                new SelfServiceEconomyTransferIntentDraft(
                    actor.TenantId,
                    actor.ActorId,
                    request.RecipientUserId,
                    request.TransferType,
                    request.Currency,
                    provenance,
                    request.AmountUnits,
                    idempotencyKey,
                    timeProvider.GetUtcNow()),
                token).ConfigureAwait(false);
            EnsurePreparedIntent(prepared, actor, request, provenance, idempotencyKey);
            var reservedRoots = await sourceRoots.ReserveAsync(
                new SelfServiceEconomyTransferSourceRootRequest(
                    prepared.PostingId,
                    actor.TenantId,
                    actor.ActorId,
                    sourceWallet.WalletId,
                    destinationWallet.WalletId),
                token).ConfigureAwait(false);
            if (reservedRoots.Count == 0)
                throw new SelfServiceEconomyTransferException(
                    "The transfer could not reserve an authorized source-root set.");

            var amount = new CoinAmount(request.Currency, request.AmountUnits);
            var intent = new EconomyProtectedOperationIntent(
                EconomyValueMovementCapability.Transfer,
                PostingTemplateKind.Spend,
                sourceWallet.WalletId,
                destinationWallet.WalletId,
                amount,
                [new RiskCurrencyLeg(amount.Currency, amount.Units)],
                reservedRoots,
                prepared.ProviderReferenceHash,
                prepared.DestinationHash,
                idempotencyKey,
                prepared.RequestedAt);

            return await orchestrator.ExecuteAsync(intent, async (authorization, operationToken) =>
            {
                EnsureAuthorization(authorization, actor);
                var authority = await postingAuthorities.ResolveAuthorityAsync(
                    RegisteredCapabilityName,
                    PostingTemplateKind.Spend,
                    authorization.Receipt,
                    operationToken).ConfigureAwait(false);
                var posting = transfers.Transfer(new PersistedFifoTransferRequest(
                    new TransferFragmentsCommand(
                        prepared.PostingId,
                        idempotencyKey,
                        sourceWallet.WalletId,
                        destinationWallet.WalletId,
                        amount,
                        provenance,
                        new ReserveVersion(authorization.Receipt.ReserveVersion),
                        new PolicyVersion(authorization.Receipt.PolicyVersion),
                        prepared.RequestedAt),
                    authority,
                    prepared.RequestHash));
                return new SelfServiceEconomyTransferReceipt(
                    posting.PostingId.Value,
                    request.TransferType,
                    request.Currency,
                    request.AmountUnits,
                    request.RecipientUserId,
                    posting.JournalSequence,
                    posting.JournalHash,
                    posting.IsDuplicate);
            }, token).ConfigureAwait(false);
        }, cancellationToken).ConfigureAwait(false);
    }

    private (Guid TenantId, Guid ActorId) RequiredActor()
    {
        var actor = actorContextAccessor.ActorContext;
        if (!actor.IsAuthenticated || actor.TenantId is not { } tenantId ||
            actor.SubjectIdAsGuid is not { } actorId)
            throw new UnauthorizedAccessException(
                "A self-service Economy transfer requires an authenticated tenant actor.");
        return (tenantId, actorId);
    }

    internal static void Validate(SelfServiceEconomyTransferRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.RecipientUserId == Guid.Empty)
            throw new ArgumentException("A transfer recipient is required.", nameof(request));
        if (!Enum.IsDefined(request.TransferType))
            throw new ArgumentOutOfRangeException(nameof(request));
        if (!Enum.IsDefined(request.Currency))
            throw new ArgumentOutOfRangeException(nameof(request));
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(request.AmountUnits);
        _ = new IdempotencyKey(request.IdempotencyKey);
    }

    internal static ProvenanceKind ProvenanceFor(CurrencyCode currency) => currency switch
    {
        CurrencyCode.HardCoin => ProvenanceKind.PurchasedHard,
        CurrencyCode.SoftCoin => ProvenanceKind.ConvertedSoft,
        _ => throw new ArgumentOutOfRangeException(nameof(currency))
    };

    internal static void EnsurePreparedIntent(
        PreparedSelfServiceEconomyTransferIntent prepared,
        (Guid TenantId, Guid ActorId) actor,
        SelfServiceEconomyTransferRequest request,
        ProvenanceKind provenance,
        IdempotencyKey idempotencyKey)
    {
        ArgumentNullException.ThrowIfNull(prepared);
        if (prepared.TenantId != actor.TenantId || prepared.ActorId != actor.ActorId ||
            prepared.RecipientUserId != request.RecipientUserId ||
            prepared.TransferType != request.TransferType || prepared.Currency != request.Currency ||
            prepared.Provenance != provenance || prepared.AmountUnits != request.AmountUnits ||
            prepared.IdempotencyKey != idempotencyKey)
            throw new SelfServiceEconomyTransferException(
                "The transfer idempotency key is already bound to another request.");
        ArgumentException.ThrowIfNullOrWhiteSpace(prepared.RequestHash);
        ArgumentException.ThrowIfNullOrWhiteSpace(prepared.ProviderReferenceHash);
        ArgumentException.ThrowIfNullOrWhiteSpace(prepared.DestinationHash);
    }

    internal static void EnsureAuthorization(
        EconomyProtectedOperationAuthorization authorization,
        (Guid TenantId, Guid ActorId) actor)
    {
        ArgumentNullException.ThrowIfNull(authorization);
        if (authorization.TenantId != actor.TenantId || authorization.ActorId != actor.ActorId ||
            authorization.Receipt.TenantId != actor.TenantId ||
            authorization.Receipt.ActorId != actor.ActorId ||
            authorization.Receipt.Capability != EconomyValueMovementCapability.Transfer ||
            authorization.RiskDecisionId != authorization.Receipt.RiskDecisionId ||
            authorization.OperationFingerprint != authorization.Receipt.OperationFingerprint)
            throw new SelfServiceEconomyTransferException(
                "The protected transfer authorization is not bound to the authenticated actor.");
    }
}

public sealed class SelfServiceEconomyTransferException : InvalidOperationException
{
    public SelfServiceEconomyTransferException(string message) : base(message) { }

    public SelfServiceEconomyTransferException(string message, Exception innerException)
        : base(message, innerException) { }
}
