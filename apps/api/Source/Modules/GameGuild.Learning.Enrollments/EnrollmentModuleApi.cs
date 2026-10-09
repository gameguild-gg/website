using Microsoft.AspNetCore.Authorization;
using GameGuild.CQRS;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace GameGuild.Learning.Enrollments;

public sealed record EnrollmentDto(
    Guid Id,
    Guid CourseId,
    Guid UserId,
    Guid? CohortId,
    EnrollmentStatus Status,
    DateTime EnrolledAt,
    DateTime? CompletedAt,
    DateTime? DroppedAt,
    int Progress,
    DateTime? LastActivityAt);

public sealed record EnrollUserRequest(Guid CourseId, Guid UserId, Guid? CohortId = null);

public sealed record UpdateEnrollmentProgressRequest(int Progress);

public sealed record EnrollUserCommand(Guid CourseId, Guid UserId, Guid? CohortId) : ICommand<EnrollmentDto>;

public sealed record UpdateEnrollmentProgressCommand(Guid EnrollmentId, int Progress) : ICommand<EnrollmentDto?>;

public sealed record SetEnrollmentStatusCommand(Guid EnrollmentId, EnrollmentStatus Status) : ICommand<EnrollmentDto?>;

public sealed record GetEnrollmentQuery(Guid EnrollmentId) : IQuery<EnrollmentDto?>;

public sealed record GetUserEnrollmentsQuery(Guid UserId, EnrollmentStatus? Status = null) : IQuery<IReadOnlyList<EnrollmentDto>>;

public sealed record GetCourseEnrollmentsQuery(Guid CourseId, EnrollmentStatus? Status = null) : IQuery<IReadOnlyList<EnrollmentDto>>;

public interface IEnrollmentRepository
{
    Task<Enrollment?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<Enrollment?> GetActiveAsync(Guid courseId, Guid userId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Enrollment>> GetByUserAsync(Guid userId, EnrollmentStatus? status, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Enrollment>> GetByCourseAsync(Guid courseId, EnrollmentStatus? status, CancellationToken cancellationToken = default);

    Task AddAsync(Enrollment enrollment, CancellationToken cancellationToken = default);

    Task UpdateAsync(Enrollment enrollment, CancellationToken cancellationToken = default);
}

public sealed class EnrollmentRepository(IApplicationDbContext context) : IEnrollmentRepository
{
    public Task<Enrollment?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => context.Set<Enrollment>().FirstOrDefaultAsync(enrollment => enrollment.Id == id, cancellationToken);

    public Task<Enrollment?> GetActiveAsync(Guid courseId, Guid userId, CancellationToken cancellationToken = default)
        => context.Set<Enrollment>()
            .FirstOrDefaultAsync(
                enrollment => enrollment.CourseId == courseId &&
                              enrollment.UserId == userId &&
                              enrollment.Status != EnrollmentStatus.Dropped &&
                              enrollment.Status != EnrollmentStatus.Expired,
                cancellationToken);

    public async Task<IReadOnlyList<Enrollment>> GetByUserAsync(Guid userId, EnrollmentStatus? status, CancellationToken cancellationToken = default)
    {
        var query = context.Set<Enrollment>().Where(enrollment => enrollment.UserId == userId);
        if (status.HasValue)
        {
            query = query.Where(enrollment => enrollment.Status == status.Value);
        }

        return await query.OrderByDescending(enrollment => enrollment.EnrolledAt).ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<Enrollment>> GetByCourseAsync(Guid courseId, EnrollmentStatus? status, CancellationToken cancellationToken = default)
    {
        var query = context.Set<Enrollment>().Where(enrollment => enrollment.CourseId == courseId);
        if (status.HasValue)
        {
            query = query.Where(enrollment => enrollment.Status == status.Value);
        }

        return await query.OrderByDescending(enrollment => enrollment.EnrolledAt).ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task AddAsync(Enrollment enrollment, CancellationToken cancellationToken = default)
    {
        context.Set<Enrollment>().Add(enrollment);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task UpdateAsync(Enrollment enrollment, CancellationToken cancellationToken = default)
    {
        context.Set<Enrollment>().Update(enrollment);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}

public interface IEnrollmentService
{
    Task<EnrollmentDto> EnrollAsync(EnrollUserCommand command, CancellationToken cancellationToken = default);

    Task<EnrollmentDto?> UpdateProgressAsync(Guid id, int progress, CancellationToken cancellationToken = default);

    Task<EnrollmentDto?> SetStatusAsync(Guid id, EnrollmentStatus status, CancellationToken cancellationToken = default);

    Task<EnrollmentDto?> GetAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<EnrollmentDto>> GetUserEnrollmentsAsync(Guid userId, EnrollmentStatus? status, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<EnrollmentDto>> GetCourseEnrollmentsAsync(Guid courseId, EnrollmentStatus? status, CancellationToken cancellationToken = default);
}

public sealed class EnrollmentService(IEnrollmentRepository repository) : IEnrollmentService
{
    public async Task<EnrollmentDto> EnrollAsync(EnrollUserCommand command, CancellationToken cancellationToken = default)
    {
        var existing = await repository.GetActiveAsync(command.CourseId, command.UserId, cancellationToken)
            .ConfigureAwait(false);
        if (existing is not null)
        {
            return ToDto(existing);
        }

        var enrollment = Enrollment.Create(command.CourseId, command.UserId, command.CohortId);
        await repository.AddAsync(enrollment, cancellationToken).ConfigureAwait(false);
        return ToDto(enrollment);
    }

    public async Task<EnrollmentDto?> UpdateProgressAsync(Guid id, int progress, CancellationToken cancellationToken = default)
    {
        var enrollment = await repository.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
        if (enrollment is null)
        {
            return null;
        }

        enrollment.UpdateProgress(progress);
        await repository.UpdateAsync(enrollment, cancellationToken).ConfigureAwait(false);
        return ToDto(enrollment);
    }

    public async Task<EnrollmentDto?> SetStatusAsync(Guid id, EnrollmentStatus status, CancellationToken cancellationToken = default)
    {
        var enrollment = await repository.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
        if (enrollment is null)
        {
            return null;
        }

        switch (status)
        {
            case EnrollmentStatus.Active:
                enrollment.Resume();
                break;
            case EnrollmentStatus.Paused:
                enrollment.Pause();
                break;
            case EnrollmentStatus.Completed:
                enrollment.Complete();
                break;
            case EnrollmentStatus.Dropped:
                enrollment.Drop();
                break;
            case EnrollmentStatus.Expired:
                enrollment.Drop();
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(status), status, null);
        }

        await repository.UpdateAsync(enrollment, cancellationToken).ConfigureAwait(false);
        return ToDto(enrollment);
    }

    public async Task<EnrollmentDto?> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var enrollment = await repository.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
        return enrollment is null ? null : ToDto(enrollment);
    }

    public async Task<IReadOnlyList<EnrollmentDto>> GetUserEnrollmentsAsync(Guid userId, EnrollmentStatus? status, CancellationToken cancellationToken = default)
        => (await repository.GetByUserAsync(userId, status, cancellationToken).ConfigureAwait(false)).Select(ToDto).ToList();

    public async Task<IReadOnlyList<EnrollmentDto>> GetCourseEnrollmentsAsync(Guid courseId, EnrollmentStatus? status, CancellationToken cancellationToken = default)
        => (await repository.GetByCourseAsync(courseId, status, cancellationToken).ConfigureAwait(false)).Select(ToDto).ToList();

    private static EnrollmentDto ToDto(Enrollment enrollment)
        => new(
            enrollment.Id,
            enrollment.CourseId,
            enrollment.UserId,
            enrollment.CohortId,
            enrollment.Status,
            enrollment.EnrolledAt,
            enrollment.CompletedAt,
            enrollment.DroppedAt,
            enrollment.Progress,
            enrollment.LastActivityAt);
}

public sealed class EnrollUserCommandHandler(IEnrollmentService service) : ICommandHandler<EnrollUserCommand, EnrollmentDto>
{
    public Task<EnrollmentDto> Handle(EnrollUserCommand request, CancellationToken cancellationToken)
        => service.EnrollAsync(request, cancellationToken);
}

public sealed class UpdateEnrollmentProgressCommandHandler(IEnrollmentService service) : ICommandHandler<UpdateEnrollmentProgressCommand, EnrollmentDto?>
{
    public Task<EnrollmentDto?> Handle(UpdateEnrollmentProgressCommand request, CancellationToken cancellationToken)
        => service.UpdateProgressAsync(request.EnrollmentId, request.Progress, cancellationToken);
}

public sealed class SetEnrollmentStatusCommandHandler(IEnrollmentService service) : ICommandHandler<SetEnrollmentStatusCommand, EnrollmentDto?>
{
    public Task<EnrollmentDto?> Handle(SetEnrollmentStatusCommand request, CancellationToken cancellationToken)
        => service.SetStatusAsync(request.EnrollmentId, request.Status, cancellationToken);
}

public sealed class GetEnrollmentQueryHandler(IEnrollmentService service) : IQueryHandler<GetEnrollmentQuery, EnrollmentDto?>
{
    public Task<EnrollmentDto?> Handle(GetEnrollmentQuery request, CancellationToken cancellationToken)
        => service.GetAsync(request.EnrollmentId, cancellationToken);
}

public sealed class GetUserEnrollmentsQueryHandler(IEnrollmentService service) : IQueryHandler<GetUserEnrollmentsQuery, IReadOnlyList<EnrollmentDto>>
{
    public Task<IReadOnlyList<EnrollmentDto>> Handle(GetUserEnrollmentsQuery request, CancellationToken cancellationToken)
        => service.GetUserEnrollmentsAsync(request.UserId, request.Status, cancellationToken);
}

public sealed class GetCourseEnrollmentsQueryHandler(IEnrollmentService service) : IQueryHandler<GetCourseEnrollmentsQuery, IReadOnlyList<EnrollmentDto>>
{
    public Task<IReadOnlyList<EnrollmentDto>> Handle(GetCourseEnrollmentsQuery request, CancellationToken cancellationToken)
        => service.GetCourseEnrollmentsAsync(request.CourseId, request.Status, cancellationToken);
}

[ApiController]
[Route("api/learning/enrollments")]
[Authorize]
public sealed class EnrollmentsController(ISender sender) : ControllerBase
{
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken cancellationToken)
    {
        var enrollment = await sender.Send(new GetEnrollmentQuery(id), cancellationToken).ConfigureAwait(false);
        return enrollment is null ? NotFound() : Ok(enrollment);
    }

    [HttpGet("users/{userId:guid}")]
    public Task<IReadOnlyList<EnrollmentDto>> GetByUser(Guid userId, [FromQuery] EnrollmentStatus? status, CancellationToken cancellationToken)
        => sender.Send(new GetUserEnrollmentsQuery(userId, status), cancellationToken);

    [HttpGet("courses/{courseId:guid}")]
    public Task<IReadOnlyList<EnrollmentDto>> GetByCourse(Guid courseId, [FromQuery] EnrollmentStatus? status, CancellationToken cancellationToken)
        => sender.Send(new GetCourseEnrollmentsQuery(courseId, status), cancellationToken);

    [HttpPost]
    public Task<EnrollmentDto> Enroll(EnrollUserRequest request, CancellationToken cancellationToken)
        => sender.Send(new EnrollUserCommand(request.CourseId, request.UserId, request.CohortId), cancellationToken);

    [HttpPatch("{id:guid}/progress")]
    public async Task<IActionResult> UpdateProgress(Guid id, UpdateEnrollmentProgressRequest request, CancellationToken cancellationToken)
    {
        var enrollment = await sender.Send(new UpdateEnrollmentProgressCommand(id, request.Progress), cancellationToken)
            .ConfigureAwait(false);
        return enrollment is null ? NotFound() : Ok(enrollment);
    }

    [HttpPost("{id:guid}/status/{status}")]
    public async Task<IActionResult> SetStatus(Guid id, EnrollmentStatus status, CancellationToken cancellationToken)
    {
        var enrollment = await sender.Send(new SetEnrollmentStatusCommand(id, status), cancellationToken)
            .ConfigureAwait(false);
        return enrollment is null ? NotFound() : Ok(enrollment);
    }
}

public sealed class EnrollmentsModelConfiguration : IModelConfiguration
{
    public void Configure(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new EnrollmentConfiguration());
    }
}

public sealed class EnrollmentConfiguration : IEntityTypeConfiguration<Enrollment>
{
    public void Configure(EntityTypeBuilder<Enrollment> builder)
    {
        builder.ToTable("learning_enrollments");
        builder.HasKey(enrollment => enrollment.Id);
        builder.Property(enrollment => enrollment.Status).HasConversion<string>().HasMaxLength(40);
        builder.HasIndex(enrollment => new { enrollment.CourseId, enrollment.UserId });
        builder.HasIndex(enrollment => enrollment.UserId);
        builder.HasIndex(enrollment => enrollment.CourseId);
        builder.HasIndex(enrollment => enrollment.Status);
    }
}

public static class EnrollmentsDependencyInjection
{
    public static IServiceCollection AddLearningEnrollmentsModule(this IServiceCollection services)
    {
        services.AddScoped<IEnrollmentRepository, EnrollmentRepository>();
        services.AddScoped<IEnrollmentService, EnrollmentService>();
        services.AddScoped<GameGuild.Learning.Courses.ICourseEnrollmentAccessReader, CourseEnrollmentAccessReader>();
        services.AddScoped<ICommandHandler<EnrollUserCommand, EnrollmentDto>, EnrollUserCommandHandler>();
        services.AddScoped<IRequestHandler<EnrollUserCommand, EnrollmentDto>>(sp => sp.GetRequiredService<ICommandHandler<EnrollUserCommand, EnrollmentDto>>());
        services.AddScoped<ICommandHandler<UpdateEnrollmentProgressCommand, EnrollmentDto?>, UpdateEnrollmentProgressCommandHandler>();
        services.AddScoped<IRequestHandler<UpdateEnrollmentProgressCommand, EnrollmentDto?>>(sp => sp.GetRequiredService<ICommandHandler<UpdateEnrollmentProgressCommand, EnrollmentDto?>>());
        services.AddScoped<ICommandHandler<SetEnrollmentStatusCommand, EnrollmentDto?>, SetEnrollmentStatusCommandHandler>();
        services.AddScoped<IRequestHandler<SetEnrollmentStatusCommand, EnrollmentDto?>>(sp => sp.GetRequiredService<ICommandHandler<SetEnrollmentStatusCommand, EnrollmentDto?>>());
        services.AddScoped<IQueryHandler<GetEnrollmentQuery, EnrollmentDto?>, GetEnrollmentQueryHandler>();
        services.AddScoped<IRequestHandler<GetEnrollmentQuery, EnrollmentDto?>>(sp => sp.GetRequiredService<IQueryHandler<GetEnrollmentQuery, EnrollmentDto?>>());
        services.AddScoped<IQueryHandler<GetUserEnrollmentsQuery, IReadOnlyList<EnrollmentDto>>, GetUserEnrollmentsQueryHandler>();
        services.AddScoped<IRequestHandler<GetUserEnrollmentsQuery, IReadOnlyList<EnrollmentDto>>>(sp => sp.GetRequiredService<IQueryHandler<GetUserEnrollmentsQuery, IReadOnlyList<EnrollmentDto>>>());
        services.AddScoped<IQueryHandler<GetCourseEnrollmentsQuery, IReadOnlyList<EnrollmentDto>>, GetCourseEnrollmentsQueryHandler>();
        services.AddScoped<IRequestHandler<GetCourseEnrollmentsQuery, IReadOnlyList<EnrollmentDto>>>(sp => sp.GetRequiredService<IQueryHandler<GetCourseEnrollmentsQuery, IReadOnlyList<EnrollmentDto>>>());
        return services;
    }
}

public sealed class LearningEnrollmentsModule : ModuleBase
{
    public override string Name => "Learning.Enrollments";
    public override int Order => 140;

    public override IServiceCollection ConfigureServices(IServiceCollection services, IConfiguration configuration)
        => services.AddLearningEnrollmentsModule();

    public override IEndpointRouteBuilder MapEndpoints(IEndpointRouteBuilder endpoints) => endpoints;
}
