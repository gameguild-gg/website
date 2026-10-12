using GameGuild.CQRS;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;


namespace GameGuild.Learning.Courses;

/// <summary>
/// Query handler for GetProgramBySlugQuery
/// </summary>
public sealed class GetProgramBySlugQueryHandler(IApplicationDbContext context, ILogger<GetProgramBySlugQueryHandler> logger)
    : IQueryHandler<GetProgramBySlugQuery, Program?>
{
    public async Task<Program?> Handle(GetProgramBySlugQuery request, CancellationToken cancellationToken) {
    logger.LogInformation("Getting program by slug: {Slug}", request.Slug);

    var query = context.Set<Program>()
      .AsNoTracking() // Read-only query optimization
      .Where(p => p.Slug == request.Slug && p.DeletedAt == null);

    if (request.IncludeContent)
        {
            query = query.Include(p => p.ProgramContents.Where(pc => pc.DeletedAt == null));
        }

        if (request.IncludeEnrollments)
        {
            query = query.Include(p => p.ProgramUsers.Where(pu => pu.DeletedAt == null));
        }

        if (request.IncludeRatings)
        {
            query = query.Include(p => p.ProgramRatings);
        }

        var program = await query.FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);

    if (program != null)
        {
            logger.LogInformation("Found program by slug: {Slug}", request.Slug);
        }
        else
        {
            logger.LogWarning("Program not found by slug: {Slug}", request.Slug);
        }

        return program;
  }
}
