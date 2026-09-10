using System.Data.Common;
using System.Security.Cryptography;
using System.Text;
using GameGuild.Commerce.Payments;
using GameGuild.Finance.Economy.Contracts;
using GameGuild.Finance.Economy.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace GameGuild.Finance.Economy.Funding;

public sealed class PostgreSqlEconomyTopUpIntentStore :
    IEconomyTopUpIntentStore,
    IEconomyTopUpReader,
    IEconomyTopUpSettlementStore
{
    private readonly DbContext _db;

    public PostgreSqlEconomyTopUpIntentStore(IApplicationDbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        _db = context as DbContext ?? throw new InvalidOperationException(
            "Persistent Economy top-up intents require the application's DbContext.");
    }

    public async ValueTask<PreparedEconomyTopUpIntent> PrepareAsync(
        EconomyTopUpIntentDraft draft,
        CancellationToken cancellationToken)
    {
        Validate(draft);
        var requestHash = RequestHash(draft);
        if (_db.Database.IsRelational())
            return await PrepareRelationalAsync(draft, requestHash, cancellationToken).ConfigureAwait(false);
        var existing = await _db.Set<EconomyTopUpIntentRow>().AsNoTracking()
            .SingleOrDefaultAsync(row =>
                    row.TenantId == draft.TenantId && row.ActorId == draft.ActorId &&
                    row.IdempotencyKey == draft.IdempotencyKey.Value,
                cancellationToken).ConfigureAwait(false);
        if (existing is not null)
            return MapReplay(existing, requestHash);

        var payment = Payment.Create(
            draft.TenantId,
            draft.UsdMinorUnits / 100m,
            "USD",
            PaymentIdempotencyKey(draft),
            draft.Provider,
            description: "Economy HardCoin top-up");
        var row = new EconomyTopUpIntentRow
        {
            Id = DeterministicGuid(requestHash),
            PaymentId = payment.Id,
            TenantId = draft.TenantId,
            ActorId = draft.ActorId,
            WalletId = draft.WalletId.Value,
            HardCoinUnits = draft.HardCoinUnits,
            UsdMinorUnits = draft.UsdMinorUnits,
            JurisdictionCode = draft.JurisdictionCode,
            PolicyVersion = draft.PolicyVersion,
            PolicyHash = draft.PolicyHash,
            Provider = draft.Provider,
            IdempotencyKey = draft.IdempotencyKey.Value,
            RequestHash = requestHash,
            Status = EconomyTopUpProviderStatus.Prepared,
            RequestedAt = draft.RequestedAt,
            UpdatedAt = draft.RequestedAt,
            Version = 1
        };
        _db.Set<Payment>().Add(payment);
        _db.Set<EconomyTopUpIntentRow>().Add(row);
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Map(row, isDuplicate: false);
    }

    public async ValueTask BindProviderAsync(
        EconomyTopUpProviderBinding binding,
        CancellationToken cancellationToken)
    {
        Validate(binding);
        if (_db.Database.IsRelational())
        {
            await BindProviderRelationalAsync(binding, cancellationToken).ConfigureAwait(false);
            return;
        }
        var row = await _db.Set<EconomyTopUpIntentRow>()
            .SingleOrDefaultAsync(item => item.Id == binding.TopUpId, cancellationToken)
            .ConfigureAwait(false) ?? throw new KeyNotFoundException("Economy top-up intent was not found.");
        var payment = await _db.Set<Payment>()
            .SingleAsync(item => item.Id == row.PaymentId, cancellationToken)
            .ConfigureAwait(false);

        if (row.ProviderObjectId is not null)
        {
            if (!Matches(row, binding))
                throw new EconomyTopUpReplayConflictException(
                    "The Economy top-up provider object cannot be rebound.");
            return;
        }
        if (!string.Equals(row.Provider, binding.Provider, StringComparison.Ordinal))
            throw new EconomySelfServiceCommandRejectedException(
                "The Economy top-up provider does not match the signed quote.");

        payment.BindProviderMapping(
            binding.Provider,
            binding.ProviderEnvironment,
            binding.ProviderAccountId,
            binding.ProviderObjectId,
            binding.ProviderObjectType,
            binding.ProviderMonetaryLeg);
        payment.MarkAsProcessing(binding.ProviderObjectId);
        if (binding.Status == EconomyTopUpProviderStatus.RequiresAction)
            payment.MarkAsRequiresAction(binding.ProviderObjectId);

        row.ProviderEnvironment = binding.ProviderEnvironment;
        row.ProviderAccountId = binding.ProviderAccountId;
        row.ProviderObjectId = binding.ProviderObjectId;
        row.ProviderObjectType = binding.ProviderObjectType;
        row.ProviderMonetaryLeg = binding.ProviderMonetaryLeg;
        row.Status = binding.Status;
        row.ProviderBoundAt = binding.BoundAt;
        row.Version = checked(row.Version + 1);
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask<EconomyTopUpStatusDto?> GetAsync(
        Guid tenantId,
        Guid actorId,
        Guid topUpId,
        CancellationToken cancellationToken)
    {
        ValidateReaderAuthority(tenantId, actorId);
        if (topUpId == Guid.Empty)
            throw new ArgumentException("Top-up ID is required.", nameof(topUpId));
        var row = await _db.Set<EconomyTopUpIntentRow>().AsNoTracking()
            .SingleOrDefaultAsync(item =>
                    item.Id == topUpId && item.TenantId == tenantId && item.ActorId == actorId,
                cancellationToken).ConfigureAwait(false);
        return row is null ? null : MapStatus(row);
    }

    public async ValueTask<IReadOnlyList<EconomyTopUpStatusDto>> ListAsync(
        Guid tenantId,
        Guid actorId,
        int take,
        CancellationToken cancellationToken)
    {
        ValidateReaderAuthority(tenantId, actorId);
        if (take is < 1 or > 100)
            throw new ArgumentOutOfRangeException(nameof(take));
        return await _db.Set<EconomyTopUpIntentRow>().AsNoTracking()
            .Where(item => item.TenantId == tenantId && item.ActorId == actorId)
            .OrderByDescending(item => item.RequestedAt)
            .ThenByDescending(item => item.Id)
            .Take(take)
            .Select(item => new EconomyTopUpStatusDto(
                item.Id,
                item.HardCoinUnits,
                item.UsdMinorUnits,
                "USD",
                item.Status,
                item.ProviderObjectId,
                item.RequestedAt,
                item.ProviderBoundAt))
            .ToArrayAsync(cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask<EconomyTopUpSettlementContext?> FindAsync(
        EconomyTopUpProviderIdentity identity,
        CancellationToken cancellationToken)
    {
        Validate(identity);
        var row = await _db.Set<EconomyTopUpIntentRow>().AsNoTracking()
            .SingleOrDefaultAsync(item =>
                    item.Provider == identity.Provider &&
                    item.ProviderEnvironment == identity.ProviderEnvironment &&
                    item.ProviderAccountId == identity.ProviderAccountId &&
                    item.ProviderObjectId == identity.ProviderObjectId &&
                    item.ProviderObjectType == identity.ProviderObjectType &&
                    item.ProviderMonetaryLeg == identity.ProviderMonetaryLeg,
                cancellationToken).ConfigureAwait(false);
        if (row is null)
            return null;
        var payment = _db.Database.IsRelational()
            ? await ReadPaymentFactAsync(identity, cancellationToken).ConfigureAwait(false)
            : await ReadPaymentFactFromModelAsync(row.PaymentId, cancellationToken).ConfigureAwait(false);
        EnsurePaymentBinding(row, payment);
        return new EconomyTopUpSettlementContext(Map(row, isDuplicate: false), payment);
    }

    public async ValueTask<EconomyTopUpProviderEventResult> ApplyAsync(
        EconomyTopUpProviderEvent providerEvent,
        CancellationToken cancellationToken)
    {
        Validate(providerEvent);
        if (_db.Database.IsRelational())
        {
            var result = await _db.Database.SqlQuery<EconomyTopUpProviderEventSqlResult>($"""
                    SELECT * FROM economy_private.apply_economy_top_up_provider_event_v1(
                        {providerEvent.Identity.Provider}, {providerEvent.Identity.ProviderEnvironment},
                        {providerEvent.Identity.ProviderAccountId}, {providerEvent.Identity.ProviderObjectId},
                        {providerEvent.Identity.ProviderObjectType}, {providerEvent.Identity.ProviderMonetaryLeg},
                        {providerEvent.EventId}, {providerEvent.OccurredAt}, {(int)providerEvent.Status},
                        {providerEvent.EvidenceHash}, {providerEvent.ProviderUsdMinorUnits},
                        {providerEvent.Currency}, {providerEvent.PostingGroupId}, {providerEvent.FailureCode})
                    """)
                .SingleAsync(cancellationToken).ConfigureAwait(false);
            return new EconomyTopUpProviderEventResult(
                result.Applied,
                result.Duplicate,
                (EconomyTopUpProviderStatus)result.Status);
        }

        var row = await _db.Set<EconomyTopUpIntentRow>()
            .SingleOrDefaultAsync(item =>
                    item.Provider == providerEvent.Identity.Provider &&
                    item.ProviderEnvironment == providerEvent.Identity.ProviderEnvironment &&
                    item.ProviderAccountId == providerEvent.Identity.ProviderAccountId &&
                    item.ProviderObjectId == providerEvent.Identity.ProviderObjectId &&
                    item.ProviderObjectType == providerEvent.Identity.ProviderObjectType &&
                    item.ProviderMonetaryLeg == providerEvent.Identity.ProviderMonetaryLeg,
                cancellationToken).ConfigureAwait(false) ?? throw new KeyNotFoundException(
                "Economy top-up provider binding was not found.");
        if (row.LastProviderEventId == providerEvent.EventId && row.Status == providerEvent.Status)
            return new EconomyTopUpProviderEventResult(false, true, row.Status);
        if (row.LastProviderEventAt > providerEvent.OccurredAt || IsTerminalRegression(row.Status, providerEvent.Status))
            return new EconomyTopUpProviderEventResult(false, false, row.Status);

        var payment = await _db.Set<Payment>()
            .SingleAsync(item => item.Id == row.PaymentId, cancellationToken).ConfigureAwait(false);
        ApplyPaymentState(payment, providerEvent);
        row.Status = providerEvent.Status;
        row.LastProviderEventId = providerEvent.EventId;
        row.LastProviderEventAt = providerEvent.OccurredAt;
        row.LastProviderEvidenceHash = providerEvent.EvidenceHash;
        row.PostingGroupId = providerEvent.PostingGroupId;
        row.FailureCode = providerEvent.FailureCode;
        row.UpdatedAt = providerEvent.OccurredAt;
        row.Version = checked(row.Version + 1);
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return new EconomyTopUpProviderEventResult(true, false, row.Status);
    }

    internal static string RequestHash(EconomyTopUpIntentDraft draft)
    {
        ArgumentNullException.ThrowIfNull(draft);
        return Hash(string.Join('|',
            "economy-hard-coin-top-up-v1",
            draft.TenantId.ToString("N"),
            draft.ActorId.ToString("N"),
            draft.WalletId.Value.ToString("N"),
            draft.HardCoinUnits,
            draft.UsdMinorUnits,
            draft.JurisdictionCode,
            draft.PolicyVersion,
            draft.PolicyHash,
            draft.Provider,
            draft.IdempotencyKey.Value));
    }

    internal static void Validate(EconomyTopUpIntentDraft draft)
    {
        ArgumentNullException.ThrowIfNull(draft);
        if (draft.TenantId == Guid.Empty || draft.ActorId == Guid.Empty || draft.WalletId.Value == Guid.Empty)
            throw new ArgumentException("Top-up tenant, actor, and wallet are required.", nameof(draft));
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(draft.HardCoinUnits);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(draft.UsdMinorUnits);
        if (draft.HardCoinUnits != draft.UsdMinorUnits)
            throw new ArgumentException("HardCoin units must match authoritative USD minor units.", nameof(draft));
        _ = Risk.EconomyJurisdictionCode.Require(draft.JurisdictionCode, nameof(draft));
        if (draft.PolicyVersion <= 0)
            throw new ArgumentOutOfRangeException(nameof(draft));
        ArgumentException.ThrowIfNullOrWhiteSpace(draft.PolicyHash);
        if (!string.Equals(draft.Provider, "stripe", StringComparison.Ordinal))
            throw new ArgumentException("Only the signed Stripe top-up provider is supported.", nameof(draft));
        if (draft.RequestedAt == default)
            throw new ArgumentException("A server request time is required.", nameof(draft));
    }

    internal static void Validate(EconomyTopUpProviderBinding binding)
    {
        ArgumentNullException.ThrowIfNull(binding);
        if (binding.TopUpId == Guid.Empty)
            throw new ArgumentException("Top-up ID is required.", nameof(binding));
        ArgumentException.ThrowIfNullOrWhiteSpace(binding.Provider);
        ArgumentException.ThrowIfNullOrWhiteSpace(binding.ProviderEnvironment);
        ArgumentException.ThrowIfNullOrWhiteSpace(binding.ProviderAccountId);
        ArgumentException.ThrowIfNullOrWhiteSpace(binding.ProviderObjectId);
        if (!string.Equals(binding.ProviderObjectType, "payment_intent", StringComparison.Ordinal) ||
            !string.Equals(binding.ProviderMonetaryLeg, "capture", StringComparison.Ordinal))
            throw new ArgumentException("Top-up provider binding must reference a payment-intent capture.", nameof(binding));
        if (binding.Status is not (EconomyTopUpProviderStatus.RequiresAction or EconomyTopUpProviderStatus.Processing))
            throw new ArgumentOutOfRangeException(nameof(binding));
        if (binding.BoundAt == default)
            throw new ArgumentException("Provider binding time is required.", nameof(binding));
    }

    internal static void Validate(EconomyTopUpProviderIdentity identity)
    {
        ArgumentNullException.ThrowIfNull(identity);
        if (!string.Equals(identity.Provider, "stripe", StringComparison.Ordinal) ||
            string.IsNullOrWhiteSpace(identity.ProviderEnvironment) ||
            string.IsNullOrWhiteSpace(identity.ProviderAccountId) ||
            string.IsNullOrWhiteSpace(identity.ProviderObjectId) ||
            !string.Equals(identity.ProviderObjectType, "payment_intent", StringComparison.Ordinal) ||
            !string.Equals(identity.ProviderMonetaryLeg, "capture", StringComparison.Ordinal))
            throw new ArgumentException("A canonical Stripe payment-intent capture identity is required.", nameof(identity));
    }

    internal static void Validate(EconomyTopUpProviderEvent providerEvent)
    {
        ArgumentNullException.ThrowIfNull(providerEvent);
        Validate(providerEvent.Identity);
        ArgumentException.ThrowIfNullOrWhiteSpace(providerEvent.EventId);
        if (providerEvent.OccurredAt == default)
            throw new ArgumentException("Provider event time is required.", nameof(providerEvent));
        if (providerEvent.Status is not (EconomyTopUpProviderStatus.RequiresAction or
            EconomyTopUpProviderStatus.Processing or EconomyTopUpProviderStatus.Posted or
            EconomyTopUpProviderStatus.Failed or EconomyTopUpProviderStatus.Cancelled or
            EconomyTopUpProviderStatus.Held))
            throw new ArgumentOutOfRangeException(nameof(providerEvent));
        if (providerEvent.EvidenceHash.Length != 64 ||
            !providerEvent.EvidenceHash.All(Uri.IsHexDigit))
            throw new ArgumentException("Provider evidence must be a SHA-256 hash.", nameof(providerEvent));
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(providerEvent.ProviderUsdMinorUnits);
        if (!string.Equals(providerEvent.Currency, "USD", StringComparison.Ordinal))
            throw new ArgumentException("HardCoin top-up events require authoritative USD amounts.", nameof(providerEvent));
        if ((providerEvent.Status == EconomyTopUpProviderStatus.Posted) != providerEvent.PostingGroupId.HasValue)
            throw new ArgumentException("Only a posted top-up may bind a posting group.", nameof(providerEvent));
        if (providerEvent.Status is EconomyTopUpProviderStatus.Failed or EconomyTopUpProviderStatus.Cancelled &&
            string.IsNullOrWhiteSpace(providerEvent.FailureCode))
            throw new ArgumentException("Terminal provider events require a failure code.", nameof(providerEvent));
    }

    private static PreparedEconomyTopUpIntent MapReplay(EconomyTopUpIntentRow row, string requestHash)
    {
        if (!CryptographicOperations.FixedTimeEquals(
                Encoding.ASCII.GetBytes(row.RequestHash),
                Encoding.ASCII.GetBytes(requestHash)))
            throw new EconomyTopUpReplayConflictException(
                "The top-up idempotency key is already bound to another request.");
        return Map(row, isDuplicate: true);
    }

    private static PreparedEconomyTopUpIntent Map(EconomyTopUpIntentRow row, bool isDuplicate) => new(
        row.Id,
        row.PaymentId,
        row.TenantId,
        row.ActorId,
        new WalletId(row.WalletId),
        row.HardCoinUnits,
        row.UsdMinorUnits,
        row.JurisdictionCode,
        row.PolicyVersion,
        row.PolicyHash,
        row.Provider,
        new IdempotencyKey(row.IdempotencyKey),
        row.RequestHash,
        row.ProviderEnvironment,
        row.ProviderAccountId,
        row.ProviderObjectId,
        row.Status,
        row.RequestedAt,
        isDuplicate);

    private static bool Matches(EconomyTopUpIntentRow row, EconomyTopUpProviderBinding binding) =>
        string.Equals(row.Provider, binding.Provider, StringComparison.Ordinal) &&
        string.Equals(row.ProviderEnvironment, binding.ProviderEnvironment, StringComparison.Ordinal) &&
        string.Equals(row.ProviderAccountId, binding.ProviderAccountId, StringComparison.Ordinal) &&
        string.Equals(row.ProviderObjectId, binding.ProviderObjectId, StringComparison.Ordinal) &&
        string.Equals(row.ProviderObjectType, binding.ProviderObjectType, StringComparison.Ordinal) &&
        string.Equals(row.ProviderMonetaryLeg, binding.ProviderMonetaryLeg, StringComparison.Ordinal) &&
        row.Status == binding.Status;

    private static EconomyTopUpStatusDto MapStatus(EconomyTopUpIntentRow row) => new(
        row.Id,
        row.HardCoinUnits,
        row.UsdMinorUnits,
        "USD",
        row.Status,
        row.ProviderObjectId,
        row.RequestedAt,
        row.ProviderBoundAt);

    private static void EnsurePaymentBinding(EconomyTopUpIntentRow row, EconomyTopUpPaymentFact payment)
    {
        if (payment.TenantId != row.TenantId || payment.Amount != row.UsdMinorUnits / 100m ||
            !string.Equals(payment.Currency, "USD", StringComparison.Ordinal) ||
            !string.Equals(payment.Provider, row.Provider, StringComparison.Ordinal) ||
            !string.Equals(payment.ProviderEnvironment, row.ProviderEnvironment, StringComparison.Ordinal) ||
            !string.Equals(payment.ProviderAccountId, row.ProviderAccountId, StringComparison.Ordinal) ||
            !string.Equals(payment.ProviderObjectId, row.ProviderObjectId, StringComparison.Ordinal) ||
            !string.Equals(payment.ProviderObjectType, row.ProviderObjectType, StringComparison.Ordinal) ||
            !string.Equals(payment.ProviderMonetaryLeg, row.ProviderMonetaryLeg, StringComparison.Ordinal))
            throw new InvalidOperationException("The Economy top-up and Payment provider bindings diverged.");
    }

    private async ValueTask<EconomyTopUpPaymentFact> ReadPaymentFactAsync(
        EconomyTopUpProviderIdentity identity,
        CancellationToken cancellationToken)
    {
        var result = await _db.Database.SqlQuery<EconomyTopUpPaymentFactSqlResult>($"""
                SELECT * FROM economy_private.read_economy_top_up_payment_fact_v1(
                    {identity.Provider}, {identity.ProviderEnvironment}, {identity.ProviderAccountId},
                    {identity.ProviderObjectId}, {identity.ProviderObjectType}, {identity.ProviderMonetaryLeg})
                """)
            .SingleOrDefaultAsync(cancellationToken).ConfigureAwait(false) ?? throw new InvalidOperationException(
                "The Economy top-up payment binding is missing.");
        return result.ToContract();
    }

    private async ValueTask<EconomyTopUpPaymentFact> ReadPaymentFactFromModelAsync(
        Guid paymentId,
        CancellationToken cancellationToken)
    {
        var payment = await _db.Set<Payment>().AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == paymentId, cancellationToken)
            .ConfigureAwait(false) ?? throw new InvalidOperationException(
                "The Economy top-up payment binding is missing.");
        return new EconomyTopUpPaymentFact(
            payment.Id,
            payment.TenantId,
            payment.Amount,
            payment.Currency,
            payment.Provider,
            payment.ProviderEnvironment ?? string.Empty,
            payment.ProviderAccountId ?? string.Empty,
            payment.ProviderObjectId ?? string.Empty,
            payment.ProviderObjectType ?? string.Empty,
            payment.ProviderMonetaryLeg ?? string.Empty);
    }

    private static bool IsTerminalRegression(
        EconomyTopUpProviderStatus current,
        EconomyTopUpProviderStatus requested) =>
        current is EconomyTopUpProviderStatus.Posted or EconomyTopUpProviderStatus.Cancelled ||
        current == EconomyTopUpProviderStatus.Held && requested != EconomyTopUpProviderStatus.Posted;

    private static void ApplyPaymentState(Payment payment, EconomyTopUpProviderEvent providerEvent)
    {
        if (providerEvent.Status == EconomyTopUpProviderStatus.Processing)
        {
            if (payment.Status != PaymentStatus.Processing)
                payment.MarkAsProcessing(providerEvent.Identity.ProviderObjectId);
            return;
        }
        if (providerEvent.Status == EconomyTopUpProviderStatus.RequiresAction)
        {
            if (payment.Status != PaymentStatus.RequiresAction)
                payment.MarkAsRequiresAction(providerEvent.Identity.ProviderObjectId);
            return;
        }
        if (providerEvent.Status is EconomyTopUpProviderStatus.Posted or EconomyTopUpProviderStatus.Held)
        {
            if (payment.Status == PaymentStatus.Failed)
                payment.PrepareForRetry();
            if (payment.Status == PaymentStatus.Pending)
                payment.MarkAsProcessing(providerEvent.Identity.ProviderObjectId);
            if (payment.Status != PaymentStatus.Succeeded)
                payment.MarkAsSucceeded(providerEvent.Identity.ProviderObjectId, providerEvent.Identity.ProviderObjectId);
            return;
        }
        if (providerEvent.Status == EconomyTopUpProviderStatus.Failed)
        {
            if (payment.Status != PaymentStatus.Failed)
                payment.MarkAsFailed("Stripe reported that the top-up payment failed.", providerEvent.FailureCode);
            return;
        }
        if (payment.Status != PaymentStatus.Cancelled)
            payment.Cancel("Stripe cancelled the top-up payment.");
    }

    private static void ValidateReaderAuthority(Guid tenantId, Guid actorId)
    {
        if (tenantId == Guid.Empty || actorId == Guid.Empty)
            throw new ArgumentException("Top-up reader tenant and actor are required.");
    }

    private static string PaymentIdempotencyKey(EconomyTopUpIntentDraft draft) =>
        $"economy-top-up:{draft.TenantId:N}:{draft.ActorId:N}:{draft.IdempotencyKey.Value}";

    private static Guid DeterministicGuid(string value)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        Span<byte> bytes = stackalloc byte[16];
        hash.AsSpan(0, 16).CopyTo(bytes);
        bytes[6] = (byte)((bytes[6] & 0x0F) | 0x50);
        bytes[8] = (byte)((bytes[8] & 0x3F) | 0x80);
        return new Guid(bytes);
    }

    private static string Hash(string value) => Convert.ToHexStringLower(
        SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    private async ValueTask<PreparedEconomyTopUpIntent> PrepareRelationalAsync(
        EconomyTopUpIntentDraft draft,
        string requestHash,
        CancellationToken cancellationToken)
    {
        var existing = await FindAsync(draft, cancellationToken).ConfigureAwait(false);
        if (existing is not null)
            return MapReplay(existing, requestHash);

        await using var transaction = await BeginOwnedTransactionAsync(cancellationToken).ConfigureAwait(false);
        var lockKey = $"economy-top-up:{draft.TenantId:N}:{draft.ActorId:N}:{draft.IdempotencyKey.Value}";
        _ = await _db.Database.SqlQuery<int>($"""
                SELECT 1 AS "Value"
                FROM (SELECT pg_advisory_xact_lock(hashtextextended({lockKey}, 0))) AS top_up_lock
                """)
            .SingleAsync(cancellationToken).ConfigureAwait(false);
        existing = await FindAsync(draft, cancellationToken).ConfigureAwait(false);
        if (existing is not null)
        {
            if (transaction is not null)
                await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return MapReplay(existing, requestHash);
        }

        var paymentId = DeterministicGuid($"economy-top-up-payment:{requestHash}");
        var row = await _db.Set<EconomyTopUpIntentRow>()
            .FromSqlInterpolated($"""
                SELECT * FROM economy_private.prepare_economy_top_up_intent_v1(
                    {DeterministicGuid(requestHash)}, {paymentId}, {draft.TenantId}, {draft.ActorId},
                    {draft.WalletId.Value}, {draft.HardCoinUnits}, {draft.UsdMinorUnits},
                    {draft.JurisdictionCode}, {draft.PolicyVersion}, {draft.PolicyHash}, {draft.Provider},
                    {draft.IdempotencyKey.Value}, {requestHash}, {draft.RequestedAt})
                """)
            .AsNoTracking()
            .SingleAsync(cancellationToken).ConfigureAwait(false);

        if (transaction is not null)
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return Map(row, isDuplicate: false);
    }

    private async ValueTask BindProviderRelationalAsync(
        EconomyTopUpProviderBinding binding,
        CancellationToken cancellationToken)
    {
        var current = await _db.Set<EconomyTopUpIntentRow>().AsNoTracking()
            .SingleOrDefaultAsync(row => row.Id == binding.TopUpId, cancellationToken)
            .ConfigureAwait(false) ?? throw new KeyNotFoundException("Economy top-up intent was not found.");
        if (current.ProviderObjectId is not null)
        {
            if (!Matches(current, binding))
                throw new EconomyTopUpReplayConflictException(
                    "The Economy top-up provider object cannot be rebound.");
            return;
        }

        await using var transaction = await BeginOwnedTransactionAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            _ = await _db.Set<EconomyTopUpIntentRow>()
                .FromSqlInterpolated($"""
                    SELECT * FROM economy_private.bind_economy_top_up_provider_v1(
                        {binding.TopUpId}, {binding.Provider}, {binding.ProviderEnvironment},
                        {binding.ProviderAccountId}, {binding.ProviderObjectId}, {binding.ProviderObjectType},
                        {binding.ProviderMonetaryLeg}, {(int)binding.Status}, {binding.BoundAt})
                    """)
                .AsNoTracking()
                .SingleAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbException exception) when (exception.SqlState == "23505")
        {
            throw new EconomyTopUpReplayConflictException(
                "The Economy top-up provider object cannot be rebound.", exception);
        }
        if (transaction is not null)
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
    }

    private Task<EconomyTopUpIntentRow?> FindAsync(
        EconomyTopUpIntentDraft draft,
        CancellationToken cancellationToken) => _db.Set<EconomyTopUpIntentRow>().AsNoTracking()
        .SingleOrDefaultAsync(row =>
                row.TenantId == draft.TenantId && row.ActorId == draft.ActorId &&
                row.IdempotencyKey == draft.IdempotencyKey.Value,
            cancellationToken);

    private async ValueTask<IDbContextTransaction?> BeginOwnedTransactionAsync(
        CancellationToken cancellationToken) => _db.Database.CurrentTransaction is null
        ? await _db.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false)
        : null;
}

internal sealed class EconomyTopUpProviderEventSqlResult
{
    public bool Applied { get; set; }
    public bool Duplicate { get; set; }
    public int Status { get; set; }
}

internal sealed class EconomyTopUpPaymentFactSqlResult
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = string.Empty;
    public string Provider { get; set; } = string.Empty;
    public string ProviderEnvironment { get; set; } = string.Empty;
    public string ProviderAccountId { get; set; } = string.Empty;
    public string ProviderObjectId { get; set; } = string.Empty;
    public string ProviderObjectType { get; set; } = string.Empty;
    public string ProviderMonetaryLeg { get; set; } = string.Empty;

    public EconomyTopUpPaymentFact ToContract() => new(
        Id,
        TenantId,
        Amount,
        Currency,
        Provider,
        ProviderEnvironment,
        ProviderAccountId,
        ProviderObjectId,
        ProviderObjectType,
        ProviderMonetaryLeg);
}
