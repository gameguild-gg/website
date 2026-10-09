using GameGuild.CQRS;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace GameGuild.Learning.Courses;

/// <summary>
/// Command handler for UpdateEnrollmentStatusCommand
/// </summary>
public sealed class UpdateEnrollmentStatusCommandHandler(IApplicationDbContext context, ILogger<UpdateEnrollmentStatusCommandHandler> logger)
    : ICommandHandler<UpdateEnrollmentStatusCommand, Program>
{
    public async Task<Program> Handle(UpdateEnrollmentStatusCommand request, CancellationToken cancellationToken) {
    logger.LogInformation("Updating enrollment status for program: {ProgramId}", request.ProgramId);

    var program = await context.Set<Program>().Where(p => p.Id == request.ProgramId && p.DeletedAt == null).FirstOrDefaultAsync(cancellationToken);

    if (program == null) { throw new InvalidOperationException($"Program with ID {request.ProgramId} not found"); }

    program.EnrollmentStatus = (EnrollmentStatus)request.Status;
    if (request.MaxEnrollments.HasValue)
        {
            program.MaxEnrollments = request.MaxEnrollments;
        }

        if (request.EnrollmentDeadline.HasValue)
        {
            program.EnrollmentDeadline = request.EnrollmentDeadline;
        }

        program.Touch();

    await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

    logger.LogInformation("Updated enrollment status for program: {ProgramId}", program.Id);

    return program;
  }
}
