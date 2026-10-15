using System.Globalization;
using System.Text;
using Microsoft.EntityFrameworkCore;

namespace GameGuild.Content.Pages;

/// <summary>EF Core implementation of <see cref="ICourseProposalService"/>.</summary>
public sealed class CourseProposalService(
    IApplicationDbContext db,
    IContentPagesAuditEventSink auditSink) : ICourseProposalService
{
    public async Task<CourseProposal> CreateAsync(CreateCourseProposalDto dto, CancellationToken ct = default)
    {
        var proposal = new CourseProposal
        {
            Slug = GenerateSlug(dto.CourseTitle),
            Status = CourseProposalStatuses.Submitted,
            CourseTitle = dto.CourseTitle.Trim(),
            Pitch = NormalizeOptional(dto.Pitch),
            Audience = NormalizeOptional(dto.Audience),
            ProposerName = dto.ProposerName.Trim(),
            ProposerEmail = dto.ProposerEmail.Trim().ToLowerInvariant(),
            Metadata = NormalizeOptional(dto.Metadata),
        };

        db.Set<CourseProposal>().Add(proposal);
        // The submission carries personal data (name, email); the audit event
        // records only the shape of the submission, never the submitted values.
        await db.SaveWithRequiredAuditAsync(
            token => auditSink.RecordAsync(new ContentPagesAuditEvent(
                "Content.Pages.CourseProposalCreated", "CourseProposal", proposal.Id.ToString(), null, proposal.TenantId,
                "Course proposal submitted",
                Metadata: new { proposal.Slug, proposal.Status }), token),
            ct).ConfigureAwait(false);
        return proposal;
    }

    public async Task<CourseProposal?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        await db.Set<CourseProposal>()
            .AsNoTracking()
            .FirstOrDefaultAsync(proposal => proposal.Id == id, ct)
            .ConfigureAwait(false);

    public async Task<CourseProposal?> GetPublishedBySlugAsync(string slug, CancellationToken ct = default)
    {
        var normalized = slug.Trim().ToLowerInvariant();
        return await db.Set<CourseProposal>()
            .AsNoTracking()
            .Where(proposal => proposal.Slug == normalized && proposal.Status == CourseProposalStatuses.Published)
            .OrderByDescending(proposal => proposal.CreatedAt)
            .FirstOrDefaultAsync(ct)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<CourseProposal>> ListAsync(
        string? status,
        string? search,
        int skip,
        int take,
        CancellationToken ct = default)
    {
        var query = db.Set<CourseProposal>()
            .AsNoTracking()
            .AsQueryable();

        var normalizedStatus = NormalizeOptional(status)?.ToLowerInvariant();
        if (normalizedStatus is not null)
        {
            query = query.Where(proposal => proposal.Status == normalizedStatus);
        }

        var normalizedSearch = NormalizeOptional(search);
        if (normalizedSearch is not null)
        {
            var term = normalizedSearch.ToLowerInvariant();
            query = query.Where(proposal =>
                proposal.CourseTitle.ToLower().Contains(term) ||
                (proposal.ProposerName != null && proposal.ProposerName.ToLower().Contains(term)) ||
                (proposal.ProposerEmail != null && proposal.ProposerEmail.ToLower().Contains(term)));
        }

        return await query
            .OrderByDescending(proposal => proposal.CreatedAt)
            .Skip(skip)
            .Take(take)
            .ToListAsync(ct)
            .ConfigureAwait(false);
    }

    public async Task<CourseProposal?> UpdateAsync(Guid id, UpdateCourseProposalDto dto, CancellationToken ct = default)
    {
        var proposal = await db.Set<CourseProposal>()
            .FirstOrDefaultAsync(proposal => proposal.Id == id, ct)
            .ConfigureAwait(false);
        if (proposal is null)
        {
            return null;
        }

        var previousStatus = proposal.Status;

        if (dto.Status is not null)
        {
            proposal.Status = dto.Status.Trim().ToLowerInvariant();
        }
        if (dto.CourseTitle is not null)
        {
            proposal.CourseTitle = dto.CourseTitle.Trim();
        }
        if (dto.Pitch is not null)
        {
            proposal.Pitch = NormalizeOptional(dto.Pitch);
        }
        if (dto.Audience is not null)
        {
            proposal.Audience = NormalizeOptional(dto.Audience);
        }
        if (dto.Metadata is not null)
        {
            proposal.Metadata = NormalizeOptional(dto.Metadata);
        }

        if (proposal.Status == CourseProposalStatuses.Published && proposal.PublishedAt is null)
        {
            proposal.PublishedAt = DateTime.UtcNow;
        }
        if (previousStatus == CourseProposalStatuses.Published && proposal.Status != CourseProposalStatuses.Published)
        {
            proposal.PublishedAt = null;
        }

        await db.SaveWithRequiredAuditAsync(
            token => auditSink.RecordAsync(new ContentPagesAuditEvent(
                "Content.Pages.CourseProposalUpdated", "CourseProposal", proposal.Id.ToString(), null, proposal.TenantId,
                "Course proposal updated",
                Metadata: new { proposal.Slug, PreviousStatus = previousStatus, proposal.Status }), token),
            ct).ConfigureAwait(false);
        return proposal;
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var proposal = await db.Set<CourseProposal>()
            .FirstOrDefaultAsync(proposal => proposal.Id == id, ct)
            .ConfigureAwait(false);
        if (proposal is null)
        {
            return false;
        }

        db.Set<CourseProposal>().Remove(proposal);
        await db.SaveWithRequiredAuditAsync(
            token => auditSink.RecordAsync(new ContentPagesAuditEvent(
                "Content.Pages.CourseProposalDeleted", "CourseProposal", proposal.Id.ToString(), null, proposal.TenantId,
                "Course proposal deleted",
                Metadata: new { proposal.Slug }), token),
            ct).ConfigureAwait(false);
        return true;
    }

    /// <summary>
    ///     Slugifies the course title and appends a short random suffix so two
    ///     proposals with identical titles can never share a public URL.
    /// </summary>
    public static string GenerateSlug(string courseTitle)
    {
        var builder = new StringBuilder();
        var lastWasSeparator = true;
        foreach (var ch in courseTitle.Trim().ToLowerInvariant())
        {
            if (char.IsAsciiLetterOrDigit(ch))
            {
                builder.Append(ch);
                lastWasSeparator = false;
            }
            else if (!lastWasSeparator)
            {
                builder.Append('-');
                lastWasSeparator = true;
            }
        }

        while (builder.Length > 0 && builder[^1] == '-')
        {
            builder.Length -= 1;
        }

        var baseSlug = builder.Length > 0 ? builder.ToString() : "proposal";
        if (baseSlug.Length > 120)
        {
            baseSlug = baseSlug[..120].TrimEnd('-');
        }

        var suffix = Guid.NewGuid().ToString("N")[..8];
        return string.Create(CultureInfo.InvariantCulture, $"{baseSlug}-{suffix}");
    }

    private static string? NormalizeOptional(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value.Trim();
    }
}
