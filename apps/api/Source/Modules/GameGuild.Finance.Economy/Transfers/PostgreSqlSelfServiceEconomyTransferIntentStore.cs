using System.Data.Common;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using GameGuild.Finance.Economy.Contracts;
using GameGuild.Finance.Economy.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GameGuild.Finance.Economy.Transfers;

public sealed class PostgreSqlSelfServiceEconomyTransferIntentStore(
    IApplicationDbContext context) : ISelfServiceEconomyTransferIntentStore
{
    private const string ProviderReference = "internal-economy-transfer-v1";
    private readonly DbContext _db = context as DbContext ?? throw new InvalidOperationException(
        "Self-service Economy transfer intents require the application's relational DbContext.");

    public async ValueTask<PreparedSelfServiceEconomyTransferIntent> PrepareAsync(
        SelfServiceEconomyTransferIntentDraft draft,
        CancellationToken cancellationToken = default)
    {
        Validate(draft);
        var requestHash = RequestHash(draft);
        var postingId = DeterministicGuid(requestHash);
        var providerHash = Hash(ProviderReference);
        var destinationHash = Hash(string.Join('|',
            "economy-transfer-destination-v1",
            draft.TenantId.ToString("N"),
            draft.RecipientUserId.ToString("N"),
            ((int)draft.TransferType).ToString(CultureInfo.InvariantCulture)));

        EconomySelfServiceTransferIntentRow row;
        try
        {
            row = await _db.Set<EconomySelfServiceTransferIntentRow>()
                .FromSqlInterpolated($"""
                    SELECT * FROM economy_private.prepare_self_service_transfer_intent_v1(
                        {postingId}, {draft.TenantId}, {draft.ActorId}, {draft.RecipientUserId},
                        {(int)draft.TransferType}, {(int)draft.Currency}, {(int)draft.Provenance},
                        {draft.AmountUnits}, {draft.IdempotencyKey.Value}, {requestHash}, {providerHash},
                        {destinationHash}, {draft.RequestedAt})
                    """)
                .AsNoTracking()
                .SingleAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbException exception) when (exception.SqlState == "23505")
        {
            throw new SelfServiceEconomyTransferException(
                "The transfer idempotency key is already bound to another request.", exception);
        }
        EnsureRequestHash(row.RequestHash, requestHash);

        return new PreparedSelfServiceEconomyTransferIntent(
            new PostingId(row.Id),
            row.TenantId,
            row.ActorId,
            row.RecipientUserId,
            row.TransferType,
            row.Currency,
            row.Provenance,
            row.AmountUnits,
            new IdempotencyKey(row.IdempotencyKey),
            row.RequestHash,
            row.ProviderReferenceHash,
            row.DestinationHash,
            row.RequestedAt);
    }

    internal static string RequestHash(SelfServiceEconomyTransferIntentDraft draft)
    {
        ArgumentNullException.ThrowIfNull(draft);
        return Hash(string.Join('|',
            "economy-self-service-transfer-v1",
            draft.TenantId.ToString("N"),
            draft.ActorId.ToString("N"),
            draft.RecipientUserId.ToString("N"),
            ((int)draft.TransferType).ToString(CultureInfo.InvariantCulture),
            ((int)draft.Currency).ToString(CultureInfo.InvariantCulture),
            ((int)draft.Provenance).ToString(CultureInfo.InvariantCulture),
            draft.AmountUnits.ToString(CultureInfo.InvariantCulture),
            draft.IdempotencyKey.Value));
    }

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

    internal static void EnsureRequestHash(string actual, string expected)
    {
        ArgumentNullException.ThrowIfNull(actual);
        ArgumentNullException.ThrowIfNull(expected);
        if (!CryptographicOperations.FixedTimeEquals(
                Encoding.ASCII.GetBytes(actual),
                Encoding.ASCII.GetBytes(expected)))
            throw new SelfServiceEconomyTransferException(
                "The transfer idempotency key is already bound to another request.");
    }

    internal static void Validate(SelfServiceEconomyTransferIntentDraft draft)
    {
        ArgumentNullException.ThrowIfNull(draft);
        if (draft.TenantId == Guid.Empty || draft.ActorId == Guid.Empty ||
            draft.RecipientUserId == Guid.Empty || draft.ActorId == draft.RecipientUserId)
            throw new ArgumentException("A transfer intent requires distinct tenant actors.", nameof(draft));
        if (!Enum.IsDefined(draft.TransferType) || !Enum.IsDefined(draft.Currency) ||
            !Enum.IsDefined(draft.Provenance))
            throw new ArgumentOutOfRangeException(nameof(draft));
        if ((draft.Currency == CurrencyCode.HardCoin && draft.Provenance != ProvenanceKind.PurchasedHard) ||
            (draft.Currency == CurrencyCode.SoftCoin && draft.Provenance != ProvenanceKind.ConvertedSoft))
            throw new ArgumentException(
                "Self-service transfer currency and provenance are incompatible.", nameof(draft));
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(draft.AmountUnits);
        if (draft.RequestedAt == default)
            throw new ArgumentException("A server request time is required.", nameof(draft));
    }
}

public sealed record SelfServiceEconomyTransferSourceRootRequest(
    PostingId PostingId,
    Guid TenantId,
    Guid ActorId,
    WalletId SourceWalletId,
    WalletId DestinationWalletId);

public interface ISelfServiceEconomyTransferSourceRootPlanner
{
    ValueTask<IReadOnlyList<SourceStampId>> ReserveAsync(
        SelfServiceEconomyTransferSourceRootRequest request,
        CancellationToken cancellationToken = default);
}

public sealed class PostgreSqlSelfServiceEconomyTransferSourceRootPlanner(
    IApplicationDbContext context) : ISelfServiceEconomyTransferSourceRootPlanner
{
    private readonly DbContext _db = context as DbContext ?? throw new InvalidOperationException(
        "Self-service Economy transfer root planning requires the application's relational DbContext.");

    public async ValueTask<IReadOnlyList<SourceStampId>> ReserveAsync(
        SelfServiceEconomyTransferSourceRootRequest request,
        CancellationToken cancellationToken = default)
    {
        Validate(request);
        try
        {
            var roots = await _db.Database.SqlQuery<Guid>($"""
                    SELECT source_root_id AS "Value"
                    FROM economy_private.reserve_self_service_transfer_roots_v1(
                        {request.PostingId.Value}, {request.TenantId}, {request.ActorId},
                        {request.SourceWalletId.Value}, {request.DestinationWalletId.Value})
                    """)
                .ToArrayAsync(cancellationToken).ConfigureAwait(false);
            return NormalizeRoots(roots);
        }
        catch (DbException exception)
        {
            throw new SelfServiceEconomyTransferException(
                "The persistent Economy transfer planner rejected the request.", exception);
        }
    }

    internal static IReadOnlyList<SourceStampId> NormalizeRoots(IReadOnlyCollection<Guid> roots)
    {
        ArgumentNullException.ThrowIfNull(roots);
        if (roots.Count == 0 || roots.Any(root => root == Guid.Empty) ||
            roots.Distinct().Count() != roots.Count)
            throw new SelfServiceEconomyTransferException(
                "The transfer could not reserve an authorized source-root set.");
        return Array.AsReadOnly(roots.OrderBy(root => root).Select(root => new SourceStampId(root)).ToArray());
    }

    internal static void Validate(SelfServiceEconomyTransferSourceRootRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.PostingId.Value == Guid.Empty || request.TenantId == Guid.Empty ||
            request.ActorId == Guid.Empty || request.SourceWalletId.Value == Guid.Empty ||
            request.DestinationWalletId.Value == Guid.Empty)
            throw new ArgumentException("A transfer root reservation requires complete server authority.", nameof(request));
        if (request.SourceWalletId == request.DestinationWalletId)
            throw new ArgumentException("Transfer root reservation wallets must differ.", nameof(request));
    }
}
