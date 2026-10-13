using GameGuild.CQRS;
using GameGuild.Commerce;
using Microsoft.EntityFrameworkCore;

namespace GameGuild.Commerce.Payments;

/// <summary>
///     Lists refunds with optional tenant, status and period filters (issue #419), newest first.
/// </summary>
/// <param name="TenantId">Optional tenant scope; enforced at the API boundary from the actor context.</param>
/// <param name="Status">Optional refund-status filter.</param>
/// <param name="FromUtc">Optional inclusive window start.</param>
/// <param name="ToUtc">Optional inclusive window end.</param>
/// <param name="Page">Page number for pagination (1-based).</param>
/// <param name="PageSize">Page size (1-100).</param>
public sealed record ListRefundsQuery(
    Guid? TenantId = null,
    RefundStatus? Status = null,
    DateTime? FromUtc = null,
    DateTime? ToUtc = null,
    int Page = 1,
    int PageSize = 20) : IQuery<RefundListPage>;

/// <summary>Paged read model for refund listings.</summary>
public sealed record RefundListPage
{
    /// <summary>Refunds for the requested page, newest first.</summary>
    public required IReadOnlyList<RefundRecord> Items { get; init; }

    /// <summary>Total matches across all pages.</summary>
    public required int TotalCount { get; init; }

    /// <summary>1-based page number.</summary>
    public required int Page { get; init; }

    /// <summary>Page size used for the query.</summary>
    public required int PageSize { get; init; }
}

/// <summary>Handler for <see cref="ListRefundsQuery"/>.</summary>
public sealed class ListRefundsQueryHandler(IApplicationDbContext context)
    : IQueryHandler<ListRefundsQuery, RefundListPage>
{
    public async Task<RefundListPage> Handle(ListRefundsQuery request, CancellationToken cancellationToken)
    {
        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, 100);

        var query = context.Set<Refund>().AsNoTracking();
        if (request.TenantId.HasValue)
        {
            query = query.Where(refund => refund.TenantId == request.TenantId.Value);
        }

        if (request.Status.HasValue)
        {
            query = query.Where(refund => refund.Status == request.Status.Value);
        }

        if (request.FromUtc.HasValue)
        {
            query = query.Where(refund => refund.CreatedAt >= request.FromUtc.Value);
        }

        if (request.ToUtc.HasValue)
        {
            query = query.Where(refund => refund.CreatedAt <= request.ToUtc.Value);
        }

        var totalCount = await query.CountAsync(cancellationToken).ConfigureAwait(false);
        var items = await query
            .OrderByDescending(refund => refund.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return new RefundListPage
        {
            Items = items.Select(RefundRecord.From).ToList(),
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }
}

/// <summary>Query for a single refund by id (issue #419).</summary>
public sealed record GetRefundByIdQuery(Guid RefundId) : IQuery<RefundRecord?>;

/// <summary>Handler for <see cref="GetRefundByIdQuery"/>.</summary>
public sealed class GetRefundByIdQueryHandler(IRefundRepository refundRepository)
    : IQueryHandler<GetRefundByIdQuery, RefundRecord?>
{
    public async Task<RefundRecord?> Handle(GetRefundByIdQuery request, CancellationToken cancellationToken)
    {
        var refund = await refundRepository.GetByIdAsync(request.RefundId, cancellationToken).ConfigureAwait(false);
        return refund is null ? null : RefundRecord.From(refund);
    }
}
