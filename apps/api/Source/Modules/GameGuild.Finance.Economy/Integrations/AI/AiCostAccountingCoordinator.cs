using GameGuild.Finance.Economy.Contracts;
using GameGuild.Finance.Economy.Ledger;

namespace GameGuild.Finance.Economy.Integrations.AI;

public sealed class AiCostAccountingCoordinator
{
    private readonly object _gate = new();
    private readonly AiServiceRateCardCatalog _catalog;
    private readonly AiCostRiskGate _risk;
    private readonly RootReversalFenceRegistry _fences;
    private readonly IAiSoftChargeGateway _charges;
    private readonly IAiProviderCostFactStore _costFacts;
    private readonly IAiTreasuryCostPublisher _treasury;
    private readonly Dictionary<Guid, AiServiceAuthorization> _authorizations = [];
    private readonly Dictionary<string, AiServiceAuthorization> _authorizationKeys = new(StringComparer.Ordinal);
    private readonly Dictionary<string, AiProviderCostFact> _providerUsage = new(StringComparer.Ordinal);
    private readonly Dictionary<string, AiServiceAuthorization> _failureKeys = new(StringComparer.Ordinal);

    public AiCostAccountingCoordinator(
        AiServiceRateCardCatalog catalog,
        AiCostRiskGate risk,
        RootReversalFenceRegistry fences,
        IAiSoftChargeGateway charges,
        IAiProviderCostFactStore costFacts,
        IAiTreasuryCostPublisher treasury)
    {
        _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        _risk = risk ?? throw new ArgumentNullException(nameof(risk));
        _fences = fences ?? throw new ArgumentNullException(nameof(fences));
        _charges = charges ?? throw new ArgumentNullException(nameof(charges));
        _costFacts = costFacts ?? throw new ArgumentNullException(nameof(costFacts));
        _treasury = treasury ?? throw new ArgumentNullException(nameof(treasury));
    }

    public AiServiceAuthorization Authorize(AuthorizeAiServiceCommand command)
    {
        ValidateAuthorization(command);
        lock (_gate)
        {
            if (_authorizationKeys.TryGetValue(command.IdempotencyKey.Value, out var duplicate))
            {
                if (duplicate.RequestId == command.RequestId) return duplicate;
                throw new AiCostAccountingIdempotencyException(
                    "An AI authorization idempotency key cannot identify another request.");
            }
            if (_authorizations.ContainsKey(command.RequestId))
                throw new AiCostAccountingIdempotencyException("The AI request is already authorized.");
            if (command.AvailableFundingLots.GroupBy(lot => lot.Id).Any(group => group.Count() > 1))
                throw new ArgumentException("Available funding lots must have unique identities.", nameof(command));

            var price = _catalog.Resolve(command.ServiceCode, command.Provider, command.Model, command.AuthorizedAt);
            var eligible = command.AvailableFundingLots.Where(lot =>
                lot.WalletId == command.SourceWalletId &&
                lot.Amount.Currency == CurrencyCode.SoftCoin &&
                lot.State == CreditLotState.Active &&
                lot.ConfirmedAt <= command.AuthorizedAt).ToArray();
            var parents = eligible.ToDictionary(lot => lot.Id);
            var selection = FifoFragmentSelector.Select(
                eligible,
                new CoinAmount(CurrencyCode.SoftCoin, price.PriceSoftUnits));
            var funding = selection.Selections
                .Select(item => new AiFundingFragment(parents[item.ParentLotId], item)).ToArray();
            var roots = funding.SelectMany(fragment => fragment.SelectedRanges)
                .Select(range => range.Root).Distinct().ToArray();
            var fence = _fences.Capture(roots);

            return _fences.WithAllocationFence(fence, roots, () =>
            {
                _risk.Authorize(command, price, funding);
                var reservation = _charges.Reserve(new AiSoftChargeReservationRequest(
                    command.RequestId,
                    new CoinAmount(CurrencyCode.SoftCoin, price.PriceSoftUnits),
                    funding,
                    command.AuthorizedAt));
                ValidateReservation(command.RequestId, price.PriceSoftUnits, funding, reservation);

                var authorization = new AiServiceAuthorization(
                    command.RequestId, command, price, funding, reservation);
                _authorizations.Add(authorization.Id, authorization);
                _authorizationKeys.Add(command.IdempotencyKey.Value, authorization);
                return authorization;
            });
        }
    }

    public AiProviderCostFact Complete(CompleteAiServiceCommand command)
    {
        ValidateCompletion(command);
        lock (_gate)
        {
            if (_providerUsage.TryGetValue(command.ProviderUsageId.Trim(), out var duplicate))
            {
                if (duplicate.AuthorizationId == command.AuthorizationId) return duplicate;
                throw new AiProviderUsageReplayException("Provider usage cannot be replayed across authorizations.");
            }
            var authorization = GetAuthorization(command.AuthorizationId);
            if (authorization.Status != AiServiceChargeStatus.Reserved)
                throw new InvalidOperationException("Only a reserved AI charge can be completed.");
            if (authorization.Provider != command.Provider ||
                !string.Equals(authorization.Model, command.Model.Trim(), StringComparison.Ordinal))
                throw new AiProviderUsageReplayException("Provider usage is not bound to the authorization rate card.");
            if (command.InputTokens > authorization.Price.MaximumInputTokens ||
                command.OutputTokens > authorization.Price.MaximumOutputTokens)
                throw new AiProviderUsageExceededReservationException(
                    "Provider usage exceeded the reserved token envelope.");
            if (command.TotalTokens != checked(command.InputTokens + command.OutputTokens))
                throw new ArgumentException("Total tokens must equal input plus output tokens.", nameof(command));

            var cost = authorization.Price.RateCard.CalculateCost(command.InputTokens, command.OutputTokens);
            _charges.Finalize(authorization.Reservation, command.CompletedAt);
            var fact = new AiProviderCostFact(
                Guid.NewGuid(),
                authorization.Id,
                authorization.RequestId,
                authorization.TenantId,
                authorization.ActorId,
                authorization.ServiceCode,
                command.Provider,
                command.Model.Trim(),
                command.ProviderUsageId.Trim(),
                command.InputTokens,
                command.OutputTokens,
                command.TotalTokens,
                cost.InputCostUsdNanos,
                cost.OutputCostUsdNanos,
                cost.TotalCostUsdNanos,
                authorization.Price.PriceSoftUnits,
                authorization.Price.RateCard.Version,
                command.CompletedAt);

            try
            {
                _costFacts.Save(fact);
            }
            catch (Exception exception)
            {
                _charges.Compensate(authorization.Reservation, command.CompletedAt);
                authorization.ChangeStatus(AiServiceChargeStatus.Compensated);
                throw new AiChargeCompensationException(
                    "The finalized AI charge was compensated because its provider cost fact could not be persisted.",
                    exception);
            }

            _treasury.Publish(new AiTreasuryServiceCostFact(
                fact.Id,
                authorization.ServiceCode,
                authorization.Price.PriceSoftUnits,
                authorization.Price.CurrentProviderCostUsdNanos,
                authorization.Price.TrailingHighPercentileCostUsdNanos,
                authorization.Price.ProviderFxStressCostUsdNanos,
                command.CompletedAt,
                authorization.Price.ExpiresAt));
            authorization.ChangeStatus(AiServiceChargeStatus.Completed);
            _providerUsage.Add(fact.ProviderUsageId, fact);
            return fact;
        }
    }

    public AiServiceAuthorization Fail(FailAiServiceCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (command.AuthorizationId == Guid.Empty)
            throw new ArgumentException("Authorization ID cannot be empty.", nameof(command));
        ArgumentException.ThrowIfNullOrWhiteSpace(command.Reason);

        lock (_gate)
        {
            if (_failureKeys.TryGetValue(command.IdempotencyKey.Value, out var duplicate))
            {
                if (duplicate.Id == command.AuthorizationId) return duplicate;
                throw new AiCostAccountingIdempotencyException(
                    "An AI failure idempotency key cannot identify another authorization.");
            }
            var authorization = GetAuthorization(command.AuthorizationId);
            if (authorization.Status != AiServiceChargeStatus.Reserved)
                throw new InvalidOperationException("Only a reserved AI charge can be released.");
            _charges.Release(authorization.Reservation, command.Reason.Trim(), command.FailedAt);
            authorization.ChangeStatus(AiServiceChargeStatus.Released);
            _failureKeys.Add(command.IdempotencyKey.Value, authorization);
            return authorization;
        }
    }

    public AiServiceAuthorization Get(Guid authorizationId)
    {
        if (authorizationId == Guid.Empty)
            throw new ArgumentException("Authorization ID cannot be empty.", nameof(authorizationId));
        lock (_gate) return GetAuthorization(authorizationId);
    }

    private AiServiceAuthorization GetAuthorization(Guid authorizationId) =>
        _authorizations.TryGetValue(authorizationId, out var authorization)
            ? authorization
            : throw new KeyNotFoundException("AI service authorization was not found.");

    private static void ValidateAuthorization(AuthorizeAiServiceCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(command.AvailableFundingLots);
        ArgumentNullException.ThrowIfNull(command.Risk);
        if (command.RequestId == Guid.Empty || command.TenantId == Guid.Empty || command.ActorId == Guid.Empty)
            throw new ArgumentException("Request, tenant, and actor identities are required.", nameof(command));
        ArgumentException.ThrowIfNullOrWhiteSpace(command.ServiceCode);
        if (!Enum.IsDefined(command.Provider)) throw new ArgumentOutOfRangeException(nameof(command.Provider));
        ArgumentException.ThrowIfNullOrWhiteSpace(command.Model);
        if (command.AvailableFundingLots.Count == 0)
            throw new ArgumentException("At least one funding lot is required.", nameof(command));
    }

    private static void ValidateCompletion(CompleteAiServiceCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (command.AuthorizationId == Guid.Empty)
            throw new ArgumentException("Authorization ID cannot be empty.", nameof(command));
        ArgumentException.ThrowIfNullOrWhiteSpace(command.ProviderUsageId);
        if (!Enum.IsDefined(command.Provider)) throw new ArgumentOutOfRangeException(nameof(command.Provider));
        ArgumentException.ThrowIfNullOrWhiteSpace(command.Model);
        ArgumentOutOfRangeException.ThrowIfNegative(command.InputTokens);
        ArgumentOutOfRangeException.ThrowIfNegative(command.OutputTokens);
        ArgumentOutOfRangeException.ThrowIfNegative(command.TotalTokens);
    }

    private static void ValidateReservation(
        Guid authorizationId,
        long priceSoftUnits,
        IReadOnlyList<AiFundingFragment> funding,
        AiSoftChargeReservation reservation)
    {
        if (reservation is null || reservation.Id == Guid.Empty || reservation.AuthorizationId != authorizationId ||
            reservation.Amount != new CoinAmount(CurrencyCode.SoftCoin, priceSoftUnits) ||
            !reservation.FundingFragments.SequenceEqual(funding))
            throw new InvalidOperationException("AI charge reservation is not bound to its authorization.");
    }
}
