using GameGuild.CQRS;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;


namespace GameGuild.Learning.Courses;

/// <summary>
/// Query handler for GetPublishedProgramBySlugQuery
/// </summary>
public sealed class GetPublishedProgramBySlugQueryHandler(IApplicationDbContext context, ILogger<GetPublishedProgramBySlugQueryHandler> logger)
    : IQueryHandler<GetPublishedProgramBySlugQuery, Program?>
{
    public async Task<Program?> Handle(GetPublishedProgramBySlugQuery request, CancellationToken cancellationToken) {
    logger.LogInformation("Getting published program by slug: {Slug}", request.Slug);

    var query = context.Set<Program>().Where(p => p.Slug == request.Slug && p.DeletedAt == null && p.Status == ContentStatus.Published && p.Visibility == ContentVisibility.Public);

    if (request.IncludeContent)
        {
            query = query.Include(p => p.ProgramContents.Where(pc => pc.DeletedAt == null));
        }

        var program = await query.FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);

    if (program != null)
        {
            logger.LogInformation("Found published program by slug: {Slug}", request.Slug);
        }
        else
        {
            logger.LogWarning("Published program not found by slug: {Slug}", request.Slug);
        }

        return program;
  }
}
