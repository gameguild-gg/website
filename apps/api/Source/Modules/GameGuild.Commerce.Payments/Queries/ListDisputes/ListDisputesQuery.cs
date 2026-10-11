using GameGuild.CQRS;
using Microsoft.EntityFrameworkCore;

namespace GameGuild.Commerce.Payments;

/// <summary>Read model for one dispute (issue #419).</summary>
public sealed record DisputeRecord
{
    /// <summary>Dispute identifier.</summary>
    public required Guid DisputeId { get; init; }

    /// <summary>Payment the dispute was raised against.</summary>
    public required Guid PaymentId { get; init; }

    /// <summary>User associated with the dispute.</summary>
    public required Guid UserId { get; init; }

    /// <summary>Dispute type.</summary>
    public required DisputeType Type { get; init; }

    /// <summary>Current status.</summary>
    public required DisputeStatus Status { get; init; }

    /// <summary>Disputed amount.</summary>
    public required decimal Amount { get; init; }

    /// <summary>Dispute reason.</summary>
    public required string Reason { get; init; }

    /// <summary>Detailed description.</summary>
    public string? Description { get; init; }

    /// <summary>Gateway dispute id, when reconciled from a webhook.</summary>
    public string? GatewayDisputeId { get; init; }

    /// <summary>Gateway that owns the dispute.</summary>
    public string? GatewayName { get; init; }

    /// <summary>Deadline for evidence submission, when provided by the gateway.</summary>
    public DateTime? EvidenceDueBy { get; init; }

    /// <summary>Response due date.</summary>
    public DateTime? DueDate { get; init; }

    /// <summary>Resolution, when decided.</summary>
    public DisputeResolution? Resolution { get; init; }

    /// <summary>Resolution notes.</summary>
    public string? ResolutionNotes { get; init; }

    /// <summary>When the dispute was resolved.</summary>
    public DateTime? ResolvedAt { get; init; }

    /// <summary>Number of attached evidence files.</summary>
    public required int EvidenceCount { get; init; }

    /// <summary>When the dispute was created.</summary>
    public required DateTime CreatedAt { get; init; }
}

/// <summary>
///     Lists disputes with optional filters (issue #419), newest first.
/// </summary>
public sealed record ListDisputesQuery(
    DisputeStatus? Status = null,
    Guid? PaymentId = null,
    Guid? UserId = null,
    int Page = 1,
    int PageSize = 20) : IQuery<DisputeListPage>;

/// <summary>Paged read model for dispute listings.</summary>
public sealed record DisputeListPage
{
    /// <summary>Disputes for the requested page, newest first.</summary>
    public required IReadOnlyList<DisputeRecord> Items { get; init; }

    /// <summary>Total matches across all pages.</summary>
    public required int TotalCount { get; init; }

    /// <summary>1-based page number.</summary>
    public required int Page { get; init; }

    /// <summary>Page size used for the query.</summary>
    public required int PageSize { get; init; }
}

/// <summary>Handler for <see cref="ListDisputesQuery"/>.</summary>
public sealed class ListDisputesQueryHandler(IApplicationDbContext context)
    : IQueryHandler<ListDisputesQuery, DisputeListPage>
{
    public async Task<DisputeListPage> Handle(ListDisputesQuery request, CancellationToken cancellationToken)
    {
        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, 100);

        var query = context.Set<PaymentDispute>().AsNoTracking();
        if (request.Status.HasValue)
        {
            query = query.Where(dispute => dispute.Status == request.Status.Value);
        }

        if (request.PaymentId.HasValue)
        {
            query = query.Where(dispute => dispute.PaymentId == request.PaymentId.Value);
        }

        if (request.UserId.HasValue)
        {
            query = query.Where(dispute => dispute.UserId == request.UserId.Value);
        }

        var totalCount = await query.CountAsync(cancellationToken).ConfigureAwait(false);
        var disputes = await query
            .OrderByDescending(dispute => dispute.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(dispute => new DisputeRecord
            {
                DisputeId = dispute.Id,
                PaymentId = dispute.PaymentId,
                UserId = dispute.UserId,
                Type = dispute.Type,
                Status = dispute.Status,
                Amount = dispute.Amount,
                Reason = dispute.Reason,
                Description = dispute.Description,
                GatewayDisputeId = dispute.GatewayDisputeId,
                GatewayName = dispute.GatewayName,
                EvidenceDueBy = dispute.EvidenceDueBy,
                DueDate = dispute.DueDate,
                Resolution = dispute.Resolution,
                ResolutionNotes = dispute.ResolutionNotes,
                ResolvedAt = dispute.ResolvedAt,
                EvidenceCount = dispute.Evidence.Count,
                CreatedAt = dispute.CreatedAt
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return new DisputeListPage
        {
            Items = disputes,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }
}
