using GameGuild.CQRS;
using GameGuild.Learning.Grading.Contracts;



using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace GameGuild.Learning.Courses;

/// <summary>
/// Command handler for CreateProgramCommand
/// </summary>
public sealed class CreateProgramCommandHandler(IApplicationDbContext context, ILogger<CreateProgramCommandHandler> logger)
    : ICommandHandler<CreateProgramCommand, Program>
{
    public async Task<Program> Handle(CreateProgramCommand request, CancellationToken cancellationToken) {
    logger.LogInformation("Creating new program: {Title}", request.Title);

    // Generate slug from title
    var slug = request.Title.ToSlugCase();

    // Ensure slug uniqueness
    var existingSlug = await context.Set<Program>().Where(p => p.Slug == slug && p.DeletedAt == null).FirstOrDefaultAsync(cancellationToken);

    if (existingSlug != null) { slug = $"{slug}-{Guid.NewGuid().ToString("N")[..8]}"; }

    var program = new Program {
      Id = Guid.NewGuid(),
      Title = request.Title,
      Description = request.Description,
      Slug = slug,
      Thumbnail = request.Thumbnail,
      VideoShowcaseUrl = request.VideoShowcaseUrl,
      EstimatedHours = (int?)request.EstimatedHours,
      Category = request.Category,
      Difficulty = request.Difficulty,
      EnrollmentStatus = request.EnrollmentStatus,
      MaxEnrollments = request.MaxEnrollments,
      EnrollmentDeadline = request.EnrollmentDeadline,
      PassingScore = request.PassingScore == default ? PercentValue.FromPercentage("60") : request.PassingScore,
      Status = ContentStatus.Draft,
      Visibility = ContentVisibility.Private,
    };

    context.Set<Program>().Add(program);
    await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

    logger.LogInformation("Created program with ID: {ProgramId}", program.Id);

    return program;
  }
}
