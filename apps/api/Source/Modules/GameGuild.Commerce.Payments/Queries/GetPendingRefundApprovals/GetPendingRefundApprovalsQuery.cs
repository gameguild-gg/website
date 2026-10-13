using GameGuild.CQRS;
using GameGuild.Identity.Authorization;
using Microsoft.EntityFrameworkCore;

namespace GameGuild.Commerce.Payments;

/// <summary>
///     The pending refund-approval queue (issue #419): refunds in <see cref="RefundStatus.Pending"/>
///     that require a manual decision, oldest first. Requires the
///     <c>payments:refunds:approve</c> permission.
/// </summary>
/// <param name="TenantId">Optional tenant scope; enforced at the API boundary from the actor context.</param>
/// <param name="Page">Page number for pagination (1-based).</param>
/// <param name="PageSize">Page size (1-100).</param>
[AuthorizeRequest(PaymentsPermission.Keys.RefundApprovals)]
public sealed record GetPendingRefundApprovalsQuery(Guid? TenantId = null, int Page = 1, int PageSize = 20)
    : IQuery<RefundApprovalQueuePage>;

/// <summary>Paged read model for the pending refund-approval queue.</summary>
public sealed record RefundApprovalQueuePage
{
    /// <summary>Queue items for the requested page, oldest first.</summary>
    public required IReadOnlyList<RefundRecord> Items { get; init; }

    /// <summary>Total queue size across all pages.</summary>
    public required int TotalCount { get; init; }

    /// <summary>1-based page number.</summary>
    public required int Page { get; init; }

    /// <summary>Page size used for the query.</summary>
    public required int PageSize { get; init; }
}

/// <summary>Handler for <see cref="GetPendingRefundApprovalsQuery"/>.</summary>
public sealed class GetPendingRefundApprovalsQueryHandler(IApplicationDbContext context)
    : IQueryHandler<GetPendingRefundApprovalsQuery, RefundApprovalQueuePage>
{
    public async Task<RefundApprovalQueuePage> Handle(GetPendingRefundApprovalsQuery request, CancellationToken cancellationToken)
    {
        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, 100);

        var query = context.Set<Refund>()
            .AsNoTracking()
            .Where(refund => refund.Status == RefundStatus.Pending && refund.RequiresApproval);

        if (request.TenantId.HasValue)
        {
            query = query.Where(refund => refund.TenantId == request.TenantId.Value);
        }

        var totalCount = await query.CountAsync(cancellationToken).ConfigureAwait(false);
        var items = await query
            .OrderBy(refund => refund.RequestedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return new RefundApprovalQueuePage
        {
            Items = items.Select(RefundRecord.From).ToList(),
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }
}
