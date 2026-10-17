namespace GameGuild.Content.Pages;

/// <summary>
///     Course-proposal pipeline for the public creator flow (issue #105):
///     public submissions, published-only slug lookups, and CRM-side lifecycle management.
/// </summary>
public interface ICourseProposalService
{
    Task<CourseProposal> CreateAsync(CreateCourseProposalDto dto, CancellationToken ct = default);
    Task<CourseProposal?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<CourseProposal?> GetPublishedBySlugAsync(string slug, CancellationToken ct = default);
    Task<IReadOnlyList<CourseProposal>> ListAsync(
        string? status,
        string? search,
        int skip,
        int take,
        CancellationToken ct = default);
    Task<CourseProposal?> UpdateAsync(Guid id, UpdateCourseProposalDto dto, CancellationToken ct = default);
    Task<bool> DeleteAsync(Guid id, CancellationToken ct = default);
}
