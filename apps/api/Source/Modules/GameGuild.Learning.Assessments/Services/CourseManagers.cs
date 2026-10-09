using GameGuild.Identity.Authorization;
using GameGuild.Learning.Courses;
using Microsoft.EntityFrameworkCore;

namespace GameGuild.Learning.Assessments;

/// <summary>
/// Reviewer resolution for grading notifications. Review is independent from content
/// editing and publishing: Program.{courseId}.Review plus the course creator.
/// </summary>
internal static class CourseManagers
{
    // ponytail: loads ALL active direct grants then filters in memory (O(grants) per submit);
    // push the array-containment into SQL if notification volume ever matters. Tenant/global
    // DEFAULT grants (UserId == null) are deliberately not enumerated — defaults carry generic
    // operations permissions, not per-course resource grants, and fanning out to every tenant
    // member would spam.
    public static async Task<IReadOnlyList<Guid>> GetManagerUserIdsAsync(IApplicationDbContext context, Guid courseId)
    {
        var creatorId = await context.Set<Program>()
            .Where(p => p.Id == courseId)
            .Select(p => p.CreatorId)
            .FirstOrDefaultAsync().ConfigureAwait(false);

        var permissionName = $"{nameof(Program)}.{courseId}.{PermissionType.Review}";

        var grants = await context.Set<TenantPermission>()
            .Where(tp => tp.UserId != null && tp.IsActive && tp.DeletedAt == null)
            .ToListAsync().ConfigureAwait(false);

        var managers = grants
            .Where(tp => !tp.IsExpired() && tp.HasEffectivePermission(permissionName))
            .Select(tp => tp.UserId!.Value)
            .ToList();

        if (creatorId is { } creator)
        {
            managers.Add(creator);
        }

        return managers.Distinct().ToList();
    }
}
