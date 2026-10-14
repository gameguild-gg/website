using Microsoft.EntityFrameworkCore;

namespace GameGuild.Content.Pages;

/// <summary>EF Core implementation of <see cref="IMarketingLeadService"/>.</summary>
public sealed class MarketingLeadService(
    IApplicationDbContext db,
    IContentPagesAuditEventSink auditSink) : IMarketingLeadService
{
    public async Task<MarketingLead> CreateAsync(CreateMarketingLeadDto dto, CancellationToken ct = default)
    {
        var lead = new MarketingLead
        {
            Source = NormalizeRequired(dto.Source),
            Status = MarketingLeadStatuses.New,
            Name = NormalizeOptional(dto.Name),
            Email = NormalizeRequired(dto.Email),
            Company = NormalizeOptional(dto.Company),
            Topic = NormalizeOptional(dto.Topic),
            Plan = NormalizeOptional(dto.Plan),
            Message = NormalizeOptional(dto.Message),
            Locale = NormalizeOptional(dto.Locale),
            PagePath = NormalizeOptional(dto.PagePath),
            Referrer = NormalizeOptional(dto.Referrer),
            UserAgent = NormalizeOptional(dto.UserAgent),
        };

        db.Set<MarketingLead>().Add(lead);
        // The submission payload may carry personal data (name, email, company), so the audit
        // event records only the shape of the submission, never the submitted values.
        await db.SaveWithRequiredAuditAsync(
            token => auditSink.RecordAsync(new ContentPagesAuditEvent(
                "Content.Pages.MarketingLeadCreated", "MarketingLead", lead.Id.ToString(), null, lead.TenantId,
                "Marketing lead captured", Metadata: new { lead.Source, lead.Topic, lead.Plan, lead.Locale, lead.PagePath }), token),
            ct).ConfigureAwait(false);
        return lead;
    }

    public async Task<MarketingLead?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        await db.Set<MarketingLead>()
            .AsNoTracking()
            .FirstOrDefaultAsync(lead => lead.Id == id, ct)
            .ConfigureAwait(false);

    public async Task<IReadOnlyList<MarketingLead>> ListAsync(
        string? source,
        string? status,
        string? topic,
        string? search,
        int skip,
        int take,
        CancellationToken ct = default)
    {
        var query = db.Set<MarketingLead>()
            .AsNoTracking()
            .AsQueryable();

        var normalizedSource = NormalizeOptional(source)?.ToLowerInvariant();
        var normalizedStatus = NormalizeOptional(status)?.ToLowerInvariant();
        var normalizedTopic = NormalizeOptional(topic)?.ToLowerInvariant();
        var normalizedSearch = NormalizeOptional(search);

        if (normalizedSource is not null)
        {
            query = query.Where(lead => lead.Source == normalizedSource);
        }

        if (normalizedStatus is not null)
        {
            query = query.Where(lead => lead.Status == normalizedStatus);
        }

        if (normalizedTopic is not null)
        {
            query = query.Where(lead => lead.Topic == normalizedTopic);
        }

        if (normalizedSearch is not null)
        {
            var term = normalizedSearch.ToLowerInvariant();
            query = query.Where(lead =>
                lead.Email.ToLower().Contains(term) ||
                (lead.Name != null && lead.Name.ToLower().Contains(term)) ||
                (lead.Company != null && lead.Company.ToLower().Contains(term)) ||
                (lead.Plan != null && lead.Plan.ToLower().Contains(term)));
        }

        return await query
            .OrderByDescending(lead => lead.CreatedAt)
            .Skip(skip)
            .Take(take)
            .ToListAsync(ct)
            .ConfigureAwait(false);
    }

    private static string NormalizeRequired(string value) =>
        value.Trim().ToLowerInvariant();

    private static string? NormalizeOptional(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value.Trim();
    }
}
