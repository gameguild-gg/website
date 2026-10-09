using GameGuild.CQRS;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;


namespace GameGuild.Learning.Courses;

/// <summary>
/// Query handler for GetProgramByIdQuery
/// </summary>
public sealed class GetProgramByIdQueryHandler(IApplicationDbContext context, ILogger<GetProgramByIdQueryHandler> logger)
    : IQueryHandler<GetProgramByIdQuery, Program?>
{
    public async Task<Program?> Handle(GetProgramByIdQuery request, CancellationToken cancellationToken) {
    logger.LogInformation("Getting program by ID: {ProgramId}", request.Id);

    var query = context.Set<Program>()
      .AsNoTracking() // Read-only query optimization
      .Where(p => p.Id == request.Id && p.DeletedAt == null);

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
            logger.LogInformation("Found program: {ProgramId}", program.Id);
        }
        else
        {
            logger.LogWarning("Program not found: {ProgramId}", request.Id);
        }

        return program;
  }
}
