using GameGuild.CQRS;
using GameGuild.Learning.Grading.Contracts;




using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace GameGuild.Learning.Courses;

/// <summary>
/// Query handlers for Program enrollment, progress, content, ratings, and wishlist operations.
/// </summary>
public sealed class ProgramEnrollmentAndProgressQueryHandlers(IApplicationDbContext context, ILogger<ProgramEnrollmentAndProgressQueryHandlers> logger) : IRequestHandler<GetUserEnrolledProgramsQuery, IEnumerable<Program>>,
                                                                                                                                                    IRequestHandler<GetProgramEnrollmentsQuery, IEnumerable<ProgramUser>>,
                                                                                                                                                    IRequestHandler<CheckUserEnrollmentQuery, ProgramUser?>,
                                                                                                                                                    IRequestHandler<GetProgramContentQuery, IEnumerable<ProgramContent>>,
                                                                                                                                                    IRequestHandler<GetUserProgramProgressQuery, ProgramUserProgress?>,
                                                                                                                                                    IRequestHandler<GetProgramRatingsQuery, IEnumerable<ProgramRating>>,
                                                                                                                                                    IRequestHandler<GetUserProgramRatingQuery, ProgramRating?>,
                                                                                                                                                    IRequestHandler<GetUserWishlistQuery, IEnumerable<Program>>,
                                                                                                                                                    IRequestHandler<CheckProgramInWishlistQuery, bool> {
  public async Task<ProgramUser?> Handle(CheckUserEnrollmentQuery request, CancellationToken cancellationToken) {
    logger.LogInformation("Checking enrollment for user {UserId} in program {ProgramId}", request.UserId, request.ProgramId);

    var userGuid = Guid.Parse(request.UserId); // Convert string UserId to Guid
    var enrollment = await context.Set<ProgramUser>().Where(pu => pu.ProgramId == request.ProgramId && pu.UserId == userGuid).FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);

    return enrollment;
  }

  public async Task<bool> Handle(CheckProgramInWishlistQuery request, CancellationToken cancellationToken) {
    logger.LogInformation("Checking if program {ProgramId} is in wishlist for user {UserId}", request.ProgramId, request.UserId);

    var userGuid = Guid.Parse(request.UserId); // Convert string UserId to Guid
    var exists = await context.Set<ProgramWishlist>().AnyAsync(pw => pw.ProgramId == request.ProgramId && pw.UserId == userGuid, cancellationToken).ConfigureAwait(false); // Fixed: use converted Guid

    return exists;
  }

  public async Task<IEnumerable<ProgramContent>> Handle(GetProgramContentQuery request, CancellationToken cancellationToken) {
    logger.LogInformation("Getting content for program: {ProgramId}", request.ProgramId);

    var query = context.Set<ProgramContent>()
      .AsNoTracking() // Read-only query optimization
      .Where(pc => pc.ProgramId == request.ProgramId && pc.DeletedAt == null);

    if (request.OnlyVisible) {
      query = query.Where(pc => pc.Visibility == Visibility.Public); // Filter by public visibility
    }

    var content = await query.OrderBy(pc => pc.SortOrder) // Fixed property name from Order to SortOrder
                             .ToListAsync(cancellationToken).ConfigureAwait(false);

    logger.LogInformation("Found {Count} content items for program {ProgramId}", content.Count, request.ProgramId);

    return content;
  }

  public async Task<IEnumerable<ProgramUser>> Handle(GetProgramEnrollmentsQuery request, CancellationToken cancellationToken) {
    logger.LogInformation("Getting enrollments for program: {ProgramId}", request.ProgramId);

    var query = context.Set<ProgramUser>().Where(pu => pu.ProgramId == request.ProgramId);

    if (request.OnlyActive) query = query.Where(pu => pu.IsActive);

    var enrollments = await query.OrderByDescending(pu => pu.JoinedAt) // Fixed property name
                                 .Skip(request.Skip)
                                 .Take(request.Take)
                                 .ToListAsync(cancellationToken).ConfigureAwait(false);

    logger.LogInformation("Found {Count} enrollments for program {ProgramId}", enrollments.Count, request.ProgramId);

    return enrollments;
  }

  public async Task<IEnumerable<ProgramRating>> Handle(GetProgramRatingsQuery request, CancellationToken cancellationToken) {
    logger.LogInformation("Getting ratings for program: {ProgramId}", request.ProgramId);

    var ratings = await context.Set<ProgramRating>().Where(pr => pr.ProgramId == request.ProgramId).OrderByDescending(pr => pr.CreatedAt).Skip(request.Skip).Take(request.Take).ToListAsync(cancellationToken).ConfigureAwait(false);

    logger.LogInformation("Found {Count} ratings for program {ProgramId}", ratings.Count, request.ProgramId);

    return ratings;
  }

  public async Task<ProgramRating?> Handle(GetUserProgramRatingQuery request, CancellationToken cancellationToken) {
    logger.LogInformation("Getting rating for program {ProgramId} by user {UserId}", request.ProgramId, request.UserId);

    var rating = await context.Set<ProgramRating>().Where(pr => pr.ProgramId == request.ProgramId && pr.UserId == request.UserId) // UserId is string in ProgramRating model
                              .FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);

    return rating;
  }

  public async Task<IEnumerable<Program>> Handle(GetUserEnrolledProgramsQuery request, CancellationToken cancellationToken) {
    logger.LogInformation("Getting enrolled programs for user: {UserId}", request.UserId);

    var userGuid = Guid.Parse(request.UserId); // Convert string UserId to Guid
    var query = context.Set<Program>().Where(p => p.DeletedAt == null && p.ProgramUsers.Any(pu => pu.UserId == userGuid && (!request.OnlyActive || pu.IsActive)));

    var programs = await query.OrderByDescending(p => p.ProgramUsers.First(pu => pu.UserId == userGuid).JoinedAt) // Fixed property name
                              .Skip(request.Skip)
                              .Take(request.Take)
                              .ToListAsync(cancellationToken).ConfigureAwait(false);

    logger.LogInformation("Found {Count} enrolled programs for user {UserId}", programs.Count, request.UserId);

    return programs;
  }

  public async Task<ProgramUserProgress?> Handle(GetUserProgramProgressQuery request, CancellationToken cancellationToken) {
    logger.LogInformation("Getting progress for user {UserId} in program {ProgramId}", request.UserId, request.ProgramId);

    var enrollment = await context.Set<ProgramUser>().Where(pu => pu.ProgramId == request.ProgramId && pu.UserId == request.UserId && pu.IsActive).FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);

    if (enrollment == null) return null;

    var totalContent = await context.Set<ProgramContent>().Where(pc => pc.ProgramId == request.ProgramId && pc.DeletedAt == null).CountAsync(cancellationToken).ConfigureAwait(false);

    // Note: This is a simplified implementation. You might want to track actual content completion
    var completedContent = 0; // This would need to be calculated from actual progress tracking
    var timeSpent = TimeSpan.Zero; // This would need to be calculated from actual time tracking
    var lastActivity = enrollment.UpdatedAt;
    var isCompleted = enrollment.CompletedAt.HasValue; // Fixed: use CompletedAt instead of IsCompleted
    var completedAt = enrollment.CompletedAt; // Fixed: use CompletedAt directly

    var progressPercentage = totalContent > 0
      ? PercentValue.FromRatio(completedContent, totalContent)
      : PercentValue.Zero;
    var progress = new ProgramUserProgress(request.ProgramId, request.UserId, completedContent, totalContent, progressPercentage, timeSpent, lastActivity, isCompleted, completedAt);

    return progress;
  }

  public async Task<IEnumerable<Program>> Handle(GetUserWishlistQuery request, CancellationToken cancellationToken) {
    logger.LogInformation("Getting wishlist for user: {UserId}", request.UserId);

    var userGuid = Guid.Parse(request.UserId); // Convert string UserId to Guid

    var programs = await context.Set<Program>().Where(p => p.DeletedAt == null && p.ProgramWishlists.Any(pw => pw.UserId == userGuid) // Fixed: use converted Guid
                                )
                                .OrderByDescending(p => p.ProgramWishlists.First(pw => pw.UserId == userGuid).CreatedAt) // Fixed: use converted Guid
                                .Skip(request.Skip)
                                .Take(request.Take)
                                .ToListAsync(cancellationToken).ConfigureAwait(false);

    logger.LogInformation("Found {Count} programs in wishlist for user {UserId}", programs.Count, request.UserId);

    return programs;
  }
}
