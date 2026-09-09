using GameGuild.Finance.Economy.Contracts;
using Microsoft.EntityFrameworkCore;

namespace GameGuild.Finance.Economy.Payouts;

/// <summary>
/// Uses database-owned procedures so application roles cannot insert or mutate payout requests
/// outside their constrained state transitions.
/// </summary>
public sealed class PostgreSqlPayoutRequestStore : IPayoutRequestStore
{
    private readonly DbContext _db;

    public PostgreSqlPayoutRequestStore(IApplicationDbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        _db = context as DbContext
            ?? throw new InvalidOperationException(
                "PostgreSQL payout request persistence requires the application's relational DbContext.");
    }

    public PayoutRequest? FindReplay(Guid tenantId, Guid payeeId, string idempotencyKey, string requestHash)
    {
        if (tenantId == Guid.Empty)
            throw new ArgumentException("Tenant ID is required.", nameof(tenantId));
        if (payeeId == Guid.Empty)
        {
            throw new ArgumentException("Payee ID is required.", nameof(payeeId));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(idempotencyKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(requestHash);

        var row = Read(
            "SELECT * FROM economy_private.read_payout_request_by_idempotency_v3({0}, {1}, {2})",
            tenantId,
            payeeId,
            idempotencyKey.Trim()).SingleOrDefault();
        if (row is null)
        {
            return null;
        }

        if (!string.Equals(row.RequestHash, requestHash, StringComparison.Ordinal))
        {
            throw new PayoutRequestReplayConflictException(
                "Payout request idempotency key was reused with different inputs.");
        }

        return ToContract(row);
    }

    public void Add(PayoutRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.TenantId == Guid.Empty)
            throw new ArgumentException("Tenant ID is required.", nameof(request));
        Execute($"""
            SELECT economy_private.create_payout_request_v3(
                {request.Id},
                {request.TenantId},
                {request.IdempotencyKey.Value},
                {request.RequestHash},
                {request.PayeeId},
                {request.WalletId.Value},
                {request.Amount.Units},
                {(int)request.State},
                {request.Version},
                {request.CreatedAt},
                {request.UpdatedAt});
            """);
    }

    public PayoutRequest GetForPayee(Guid tenantId, Guid requestId, Guid payeeId)
    {
        if (tenantId == Guid.Empty)
            throw new ArgumentException("Tenant ID is required.", nameof(tenantId));
        if (requestId == Guid.Empty)
        {
            throw new ArgumentException("Payout request ID is required.", nameof(requestId));
        }

        if (payeeId == Guid.Empty)
        {
            throw new ArgumentException("Payee ID is required.", nameof(payeeId));
        }

        var row = Read(
            "SELECT * FROM economy_private.read_payout_request_by_id_for_payee_v3({0}, {1}, {2})",
            tenantId,
            requestId,
            payeeId).SingleOrDefault();
        return row is null
            ? throw new KeyNotFoundException($"Payout request {requestId:N} was not found.")
            : ToContract(row);
    }

    public PayoutRequest GetForReview(Guid requestId, Guid tenantId)
    {
        if (requestId == Guid.Empty)
        {
            throw new ArgumentException("Payout request ID is required.", nameof(requestId));
        }
        if (tenantId == Guid.Empty)
        {
            throw new ArgumentException("Tenant ID is required.", nameof(tenantId));
        }

        var row = Read(
            "SELECT * FROM economy_private.read_payout_request_for_review_v2({0}, {1})",
            tenantId,
            requestId).SingleOrDefault();
        return row is null
            ? throw new KeyNotFoundException($"Payout request {requestId:N} was not found.")
            : ToContract(row);
    }

    public IReadOnlyList<PayoutRequest> ListForPayee(Guid tenantId, Guid payeeId, int take)
    {
        if (tenantId == Guid.Empty)
            throw new ArgumentException("Tenant ID is required.", nameof(tenantId));
        if (payeeId == Guid.Empty)
        {
            throw new ArgumentException("Payee ID is required.", nameof(payeeId));
        }

        if (take is < 1 or > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(take), "Take must be between 1 and 100.");
        }

        return Read(
                "SELECT * FROM economy_private.read_payout_requests_by_payee_v3({0}, {1}, {2})",
                tenantId,
                payeeId,
                take)
            .OrderByDescending(row => row.CreatedAt)
            .ThenByDescending(row => row.Id)
            .Select(ToContract)
            .ToArray();
    }

    public IReadOnlyList<PayoutRequest> ListForReview(Guid tenantId, int take)
    {
        if (tenantId == Guid.Empty)
        {
            throw new ArgumentException("Tenant ID is required.", nameof(tenantId));
        }
        if (take is < 1 or > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(take), "Take must be between 1 and 100.");
        }

        return Read(
                "SELECT * FROM economy_private.read_payout_requests_for_review_v2({0}, {1})",
                tenantId,
                take)
            .OrderBy(row => row.CreatedAt)
            .ThenBy(row => row.Id)
            .Select(ToContract)
            .ToArray();
    }

    public IReadOnlyList<PayoutRequestReviewAuditEvent> ListReviewAudit(Guid requestId, Guid tenantId)
    {
        if (requestId == Guid.Empty)
        {
            throw new ArgumentException("Payout request ID is required.", nameof(requestId));
        }
        if (tenantId == Guid.Empty)
        {
            throw new ArgumentException("Tenant ID is required.", nameof(tenantId));
        }

        return _db.Database.SqlQueryRaw<PayoutRequestReviewAuditEventRow>(
                "SELECT * FROM economy_private.read_payout_request_review_audit_v2({0}, {1})",
                tenantId,
                requestId)
            .AsNoTracking()
            .OrderBy(item => item.OccurredAt)
            .ThenBy(item => item.Id)
            .Select(item => new PayoutRequestReviewAuditEvent(
                item.Id,
                item.RequestId,
                item.TenantId,
                item.ActorId,
                item.Outcome,
                item.Reason,
                item.OccurredAt))
            .ToArray();
    }

    public PayoutRequest Update(PayoutRequest request, long expectedVersion)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (expectedVersion <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(expectedVersion));
        }

        Execute($"""
            SELECT economy_private.transition_payout_request_v3(
                {request.TenantId},
                {request.Id},
                {request.PayeeId},
                {expectedVersion},
                {(int)request.State},
                {request.UpdatedAt});
            """);
        return request;
    }

    public PayoutRequest Review(
        PayoutRequest request,
        long expectedVersion,
        Guid tenantId,
        Guid reviewerId,
        PayoutRequestState outcome,
        string reason)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (expectedVersion <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(expectedVersion));
        }
        if (tenantId == Guid.Empty)
        {
            throw new ArgumentException("Tenant ID is required.", nameof(tenantId));
        }
        if (reviewerId == Guid.Empty)
        {
            throw new ArgumentException("Reviewer ID is required.", nameof(reviewerId));
        }
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);

        try
        {
            var row = Read(
                "SELECT * FROM economy_private.review_payout_request_v2({0}, {1}, {2}, {3}, {4}, {5}, {6})",
                tenantId,
                request.Id,
                expectedVersion,
                reviewerId,
                (int)outcome,
                reason.Trim(),
                request.UpdatedAt).Single();
            return ToContract(row);
        }
        catch (Exception exception) when (ContainsPayoutRequestFailure(exception))
        {
            throw Translate(exception);
        }
    }

    private IQueryable<PayoutRequestRow> Read(string sql, params object[] parameters) =>
        _db.Database.SqlQueryRaw<PayoutRequestRow>(sql, parameters).AsNoTracking();

    private void Execute(FormattableString sql)
    {
        try
        {
            _db.Database.ExecuteSqlInterpolated(sql);
        }
        catch (Exception exception) when (ContainsPayoutRequestFailure(exception))
        {
            throw Translate(exception);
        }
    }

    private static bool ContainsPayoutRequestFailure(Exception exception)
    {
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            if (current.Message.Contains("payout request", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static Exception Translate(Exception exception)
    {
        var message = exception.Message;
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            if (current.Message.Contains("payout request", StringComparison.OrdinalIgnoreCase))
            {
                message = current.Message;
                break;
            }
        }

        return message.Contains("idempotency", StringComparison.OrdinalIgnoreCase)
            ? new PayoutRequestReplayConflictException(message)
            : new PayoutRequestStaleCommandException(message);
    }

    private static PayoutRequest ToContract(PayoutRequestRow row) => new(
        row.Id,
        new IdempotencyKey(row.IdempotencyKey),
        row.RequestHash,
        row.PayeeId,
        new WalletId(row.WalletId),
        new CoinAmount(CurrencyCode.HardCoin, row.AmountUnits),
        row.State,
        row.Version,
        row.CreatedAt,
        row.UpdatedAt,
        row.FirstApprovalActorId,
        row.TenantId);
}

public sealed class PayoutRequestRow
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string IdempotencyKey { get; set; } = string.Empty;
    public string RequestHash { get; set; } = string.Empty;
    public Guid PayeeId { get; set; }
    public Guid WalletId { get; set; }
    public long AmountUnits { get; set; }
    public PayoutRequestState State { get; set; }
    public long Version { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public Guid? FirstApprovalActorId { get; set; }
}

public sealed class PayoutRequestReviewAuditEventRow
{
    public Guid Id { get; set; }
    public Guid RequestId { get; set; }
    public Guid TenantId { get; set; }
    public Guid ActorId { get; set; }
    public PayoutRequestState Outcome { get; set; }
    public string Reason { get; set; } = string.Empty;
    public DateTimeOffset OccurredAt { get; set; }
}
