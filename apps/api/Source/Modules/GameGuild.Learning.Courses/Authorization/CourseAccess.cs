using GameGuild.Identity.Authorization;
using GameGuild.Identity.Context.Actors;

namespace GameGuild.Learning.Courses;

/// <summary>
///     Contextual actions that can be performed on a course. These are evaluated
///     capabilities, not persisted product roles.
/// </summary>
public enum CourseCapability
{
    Learn,
    Edit,
    Publish,
    StaffReview,
    AccessWorkspace
}

public sealed record CourseAccessCapabilities(
    Guid CourseId,
    bool CourseExists,
    bool IsTenantMember,
    bool IsOwner,
    bool HasActiveEnrollment,
    bool CanLearn,
    bool CanEdit,
    bool CanPublish,
    bool CanReviewAsStaff)
{
    public bool CanAccessWorkspace => CanEdit || CanPublish || CanReviewAsStaff;

    public bool Has(CourseCapability capability) => capability switch
    {
        CourseCapability.Learn => CanLearn,
        CourseCapability.Edit => CanEdit,
        CourseCapability.Publish => CanPublish,
        CourseCapability.StaffReview => CanReviewAsStaff,
        CourseCapability.AccessWorkspace => CanAccessWorkspace,
        _ => false
    };

    public static CourseAccessCapabilities Denied(Guid courseId) =>
        new(courseId, false, false, false, false, false, false, false, false);
}

/// <summary>
///     Enrollment-side port. The Enrollments module supplies the implementation,
///     keeping Courses independent from the enrollment persistence module.
/// </summary>
public interface ICourseEnrollmentAccessReader
{
    Task<bool> HasActiveEnrollmentAsync(
        Guid courseId,
        Guid userId,
        CancellationToken cancellationToken = default);
}

public interface ICourseAccessEvaluator
{
    Task<CourseAccessCapabilities> GetCapabilitiesAsync(
        Guid courseId,
        CancellationToken cancellationToken = default);

    Task<CourseAccessCapabilities> GetCapabilitiesAsync(
        Program program,
        CancellationToken cancellationToken = default);

    async Task<bool> HasCapabilityAsync(
        Guid courseId,
        CourseCapability capability,
        CancellationToken cancellationToken = default)
        => (await GetCapabilitiesAsync(courseId, cancellationToken).ConfigureAwait(false)).Has(capability);
}

public static class CoursePermissionNames
{
    public static string For(Guid courseId, CourseCapability capability) => capability switch
    {
        CourseCapability.Edit => $"{nameof(Program)}.{courseId}.{PermissionType.Edit}",
        CourseCapability.Publish => $"{nameof(Program)}.{courseId}.{PermissionType.Publish}",
        // Keep the persisted permission name stable. StaffReview clarifies the
        // evaluated capability without changing the Authorization contract.
        CourseCapability.StaffReview => $"{nameof(Program)}.{courseId}.{PermissionType.Review}",
        _ => throw new ArgumentOutOfRangeException(
            nameof(capability),
            capability,
            "Only explicitly grantable course capabilities have permission names.")
    };
}

public sealed class CourseAccessEvaluator(
    IProgramReadService programReadService,
    ICourseEnrollmentAccessReader enrollmentReader,
    IActorContextAccessor actorContextAccessor,
    IPermissionQueryService permissionQueryService) : ICourseAccessEvaluator
{
    public async Task<CourseAccessCapabilities> GetCapabilitiesAsync(
        Guid courseId,
        CancellationToken cancellationToken = default)
    {
        var program = await programReadService.GetProgramByIdAsync(courseId).ConfigureAwait(false);
        return program is null
            ? CourseAccessCapabilities.Denied(courseId)
            : await GetCapabilitiesAsync(program, cancellationToken).ConfigureAwait(false);
    }

    public async Task<CourseAccessCapabilities> GetCapabilitiesAsync(
        Program program,
        CancellationToken cancellationToken = default)
    {
        var actor = actorContextAccessor.ActorContext;
        if (!actor.IsAuthenticated || actor.SubjectIdAsGuid is not Guid userId || actor.TenantId is not Guid tenantId)
        {
            return CourseAccessCapabilities.Denied(program.Id);
        }

        if (program.TenantId is Guid programTenantId && programTenantId != tenantId)
        {
            return CourseAccessCapabilities.Denied(program.Id);
        }

        var isTenantMember = actor.IsSystemAdmin ||
                             await permissionQueryService
                                 .IsUserInTenantAsync(userId, tenantId, cancellationToken)
                                 .ConfigureAwait(false);
        if (!isTenantMember)
        {
            return new CourseAccessCapabilities(
                program.Id,
                true,
                false,
                false,
                false,
                false,
                false,
                false,
                false);
        }

        var isOwner = program.TenantId == tenantId && program.CreatorId == userId;
        var hasActiveEnrollment = await enrollmentReader
            .HasActiveEnrollmentAsync(program.Id, userId, cancellationToken)
            .ConfigureAwait(false);

        if (actor.IsSystemAdmin)
        {
            return new CourseAccessCapabilities(
                program.Id,
                true,
                true,
                isOwner,
                hasActiveEnrollment,
                hasActiveEnrollment,
                true,
                true,
                true);
        }

        var canEdit = isOwner || await HasExplicitPermissionAsync(
            userId,
            tenantId,
            program.Id,
            CourseCapability.Edit,
            cancellationToken).ConfigureAwait(false);
        var canPublish = isOwner || await HasExplicitPermissionAsync(
            userId,
            tenantId,
            program.Id,
            CourseCapability.Publish,
            cancellationToken).ConfigureAwait(false);
        var canReviewAsStaff = isOwner || await HasExplicitPermissionAsync(
            userId,
            tenantId,
            program.Id,
            CourseCapability.StaffReview,
            cancellationToken).ConfigureAwait(false);

        return new CourseAccessCapabilities(
            program.Id,
            true,
            true,
            isOwner,
            hasActiveEnrollment,
            hasActiveEnrollment,
            canEdit,
            canPublish,
            canReviewAsStaff);
    }

    private Task<bool> HasExplicitPermissionAsync(
        Guid userId,
        Guid tenantId,
        Guid courseId,
        CourseCapability capability,
        CancellationToken cancellationToken)
        => permissionQueryService.HasTenantPermissionAsync(
            userId,
            tenantId,
            CoursePermissionNames.For(courseId, capability),
            cancellationToken);
}

internal sealed class FailClosedCourseEnrollmentAccessReader : ICourseEnrollmentAccessReader
{
    public Task<bool> HasActiveEnrollmentAsync(
        Guid courseId,
        Guid userId,
        CancellationToken cancellationToken = default)
        => Task.FromResult(false);
}
