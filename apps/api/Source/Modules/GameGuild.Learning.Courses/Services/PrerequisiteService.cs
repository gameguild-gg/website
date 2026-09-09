using GameGuild.Learning.Grading.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace GameGuild.Learning.Courses;

/// <summary>
/// Service implementation for managing course prerequisites
/// </summary>
public class PrerequisiteService : IPrerequisiteService
{
    private readonly IApplicationDbContext _context;
    private readonly ILogger<PrerequisiteService> _logger;

    public PrerequisiteService(IApplicationDbContext context, ILogger<PrerequisiteService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<Result<CoursePrerequisite>> CreatePrerequisiteAsync(CreatePrerequisiteRequest request)
    {
        // Validate courses exist
        var courseExists = await _context.Set<Program>()
            .AnyAsync(p => p.Id == request.CourseId && (request.TenantId == null || p.TenantId == request.TenantId)).ConfigureAwait(false);
        
        if (!courseExists)
        {
            return Result.Failure<CoursePrerequisite>(Error.NotFound("Course.NotFound", "Course not found."));
        }

        var prerequisiteCourseExists = await _context.Set<Program>()
            .AnyAsync(p => p.Id == request.PrerequisiteCourseId && (request.TenantId == null || p.TenantId == request.TenantId)).ConfigureAwait(false);
        
        if (!prerequisiteCourseExists)
        {
            return Result.Failure<CoursePrerequisite>(Error.NotFound("PrerequisiteCourse.NotFound", "Prerequisite course not found."));
        }

        // Check for duplicate
        var exists = await _context.Set<CoursePrerequisite>()
            .AnyAsync(cp => cp.CourseId == request.CourseId && cp.PrerequisiteCourseId == request.PrerequisiteCourseId).ConfigureAwait(false);
        
        if (exists)
        {
            return Result.Failure<CoursePrerequisite>(Error.Validation("Prerequisite.Duplicate", "This prerequisite already exists."));
        }

        // Check for circular dependency
        if (await WouldCreateCircularDependencyAsync(request.CourseId, request.PrerequisiteCourseId, request.TenantId))
        {
            return Result.Failure<CoursePrerequisite>(Error.Validation("Prerequisite.CircularDependency", 
                "Adding this prerequisite would create a circular dependency."));
        }

        var prerequisite = CoursePrerequisite.Create(
            request.CourseId,
            request.PrerequisiteCourseId,
            request.TenantId,
            request.Type,
            request.MinimumGrade,
            request.Description,
            request.DisplayOrder,
            request.PrerequisiteGroup);

        _context.Set<CoursePrerequisite>().Add(prerequisite);
        await _context.SaveChangesAsync().ConfigureAwait(false);

        _logger.LogInformation("Created prerequisite {PrerequisiteId} for course {CourseId}", prerequisite.Id, request.CourseId);

        return Result.Success(prerequisite);
    }

    public async Task<CoursePrerequisite?> GetPrerequisiteByIdAsync(Guid id)
    {
        return await _context.Set<CoursePrerequisite>()
            .Include(cp => cp.Course)
            .Include(cp => cp.PrerequisiteCourse)
            .FirstOrDefaultAsync(cp => cp.Id == id).ConfigureAwait(false);
    }

    public async Task<IEnumerable<CoursePrerequisite>> GetCoursePrerequisitesAsync(Guid courseId, Guid? tenantId)
    {
        return await _context.Set<CoursePrerequisite>()
            .Include(cp => cp.PrerequisiteCourse)
            .Where(cp => cp.CourseId == courseId && (tenantId == null || cp.TenantId == tenantId))
            .OrderBy(cp => cp.DisplayOrder)
            .ThenBy(cp => cp.CreatedAt)
            .ToListAsync().ConfigureAwait(false);
    }

    public async Task<IEnumerable<CoursePrerequisite>> GetDependentCoursesAsync(Guid prerequisiteCourseId, Guid? tenantId)
    {
        return await _context.Set<CoursePrerequisite>()
            .Include(cp => cp.Course)
            .Where(cp => cp.PrerequisiteCourseId == prerequisiteCourseId && (tenantId == null || cp.TenantId == tenantId))
            .OrderBy(cp => cp.Course!.Title)
            .ToListAsync().ConfigureAwait(false);
    }

    public async Task<Result<CoursePrerequisite>> UpdatePrerequisiteAsync(Guid id, UpdatePrerequisiteRequest request)
    {
        var prerequisite = await _context.Set<CoursePrerequisite>()
            .FirstOrDefaultAsync(cp => cp.Id == id).ConfigureAwait(false);

        if (prerequisite == null)
        {
            return Result.Failure<CoursePrerequisite>(Error.NotFound("Prerequisite.NotFound", "Prerequisite not found."));
        }

        prerequisite.Update(
            request.Type,
            request.MinimumGrade,
            request.Description,
            request.DisplayOrder,
            request.PrerequisiteGroup);

        await _context.SaveChangesAsync().ConfigureAwait(false);

        _logger.LogInformation("Updated prerequisite {PrerequisiteId}", id);

        return Result.Success(prerequisite);
    }

    public async Task<Result<bool>> DeletePrerequisiteAsync(Guid id)
    {
        var prerequisite = await _context.Set<CoursePrerequisite>()
            .FirstOrDefaultAsync(cp => cp.Id == id).ConfigureAwait(false);

        if (prerequisite == null)
        {
            return Result.Failure<bool>(Error.NotFound("Prerequisite.NotFound", "Prerequisite not found."));
        }

        _context.Set<CoursePrerequisite>().Remove(prerequisite);
        await _context.SaveChangesAsync().ConfigureAwait(false);

        _logger.LogInformation("Deleted prerequisite {PrerequisiteId}", id);

        return Result.Success(true);
    }

    public async Task<PrerequisiteCheckResult> CheckPrerequisitesAsync(Guid courseId, Guid userId, Guid? tenantId)
    {
        var prerequisites = await GetCoursePrerequisitesAsync(courseId, tenantId).ConfigureAwait(false);
        var statuses = new List<PrerequisiteStatus>();
        var allSatisfied = true;

        // Group prerequisites by group for OR logic
        var groupedPrereqs = prerequisites
            .GroupBy(p => p.PrerequisiteGroup ?? p.Id.ToString())
            .ToList();

        foreach (var group in groupedPrereqs)
        {
            var groupSatisfied = false;

            foreach (var prereq in group)
            {
                var enrollment = await _context.Set<ProgramEnrollment>()
                    .FirstOrDefaultAsync(e => e.ProgramId == prereq.PrerequisiteCourseId && e.UserId == userId).ConfigureAwait(false);

                var courseName = prereq.PrerequisiteCourse?.Title ?? "Unknown Course";
                bool isSatisfied;
                PercentValue? achievedGrade = null;
                string? reason = null;

                if (enrollment == null)
                {
                    isSatisfied = prereq.Type == PrerequisiteType.Recommended;
                    reason = "Not enrolled in prerequisite course.";
                }
                else if (enrollment.CompletionStatus != CompletionStatus.Completed)
                {
                    isSatisfied = prereq.Type == PrerequisiteType.Recommended || prereq.Type == PrerequisiteType.Corequisite;
                    reason = $"Prerequisite course is {enrollment.CompletionStatus}.";
                }
                else if (prereq.MinimumGrade.HasValue)
                {
                    achievedGrade = enrollment.FinalGrade ?? enrollment.ProgressPercentage;
                    isSatisfied = achievedGrade.Value.CompareTo(prereq.MinimumGrade.Value) >= 0;
                    reason = isSatisfied ? null : $"Grade {achievedGrade}% is below required {prereq.MinimumGrade}%.";
                }
                else
                {
                    isSatisfied = true;
                }

                statuses.Add(new PrerequisiteStatus(
                    prereq.Id,
                    prereq.PrerequisiteCourseId,
                    courseName,
                    prereq.Type,
                    isSatisfied,
                    prereq.MinimumGrade,
                    achievedGrade,
                    reason));

                if (isSatisfied)
                {
                    groupSatisfied = true;
                }
            }

            // For required prerequisites, at least one in the group must be satisfied
            if (!groupSatisfied && group.Any(p => p.Type == PrerequisiteType.Required))
            {
                allSatisfied = false;
            }
        }

        return new PrerequisiteCheckResult(allSatisfied, statuses);
    }

    public async Task<IEnumerable<CoursePrerequisite>> GetPrerequisiteChainAsync(Guid courseId, Guid? tenantId)
    {
        var visited = new HashSet<Guid>();
        var chain = new List<CoursePrerequisite>();

        await CollectPrerequisitesRecursiveAsync(courseId, tenantId, visited, chain).ConfigureAwait(false);

        return chain;
    }

    private async Task CollectPrerequisitesRecursiveAsync(
        Guid courseId, 
        Guid? tenantId, 
        HashSet<Guid> visited, 
        List<CoursePrerequisite> chain)
    {
        if (visited.Contains(courseId))
            return;

        visited.Add(courseId);

        var prerequisites = await GetCoursePrerequisitesAsync(courseId, tenantId).ConfigureAwait(false);

        foreach (var prereq in prerequisites)
        {
            chain.Add(prereq);
            await CollectPrerequisitesRecursiveAsync(prereq.PrerequisiteCourseId, tenantId, visited, chain).ConfigureAwait(false);
        }
    }

    public async Task<bool> WouldCreateCircularDependencyAsync(Guid courseId, Guid prerequisiteCourseId, Guid? tenantId)
    {
        // Check if adding this prerequisite would create a cycle
        // by checking if courseId is reachable from prerequisiteCourseId
        var visited = new HashSet<Guid>();
        return await IsReachableAsync(prerequisiteCourseId, courseId, tenantId, visited).ConfigureAwait(false);
    }

    private async Task<bool> IsReachableAsync(Guid from, Guid target, Guid? tenantId, HashSet<Guid> visited)
    {
        if (from == target)
            return true;

        if (visited.Contains(from))
            return false;

        visited.Add(from);

        var prerequisites = await _context.Set<CoursePrerequisite>()
            .Where(cp => cp.CourseId == from && (tenantId == null || cp.TenantId == tenantId))
            .Select(cp => cp.PrerequisiteCourseId)
            .ToListAsync().ConfigureAwait(false);

        foreach (var prereqId in prerequisites)
        {
            if (await IsReachableAsync(prereqId, target, tenantId, visited))
                return true;
        }

        return false;
    }

    public async Task<Result<bool>> ReorderPrerequisitesAsync(Guid courseId, IEnumerable<Guid> prerequisiteIds)
    {
        var prerequisites = await _context.Set<CoursePrerequisite>()
            .Where(cp => cp.CourseId == courseId)
            .ToListAsync().ConfigureAwait(false);

        var idList = prerequisiteIds.ToList();
        var order = 0;

        foreach (var id in idList)
        {
            var prereq = prerequisites.FirstOrDefault(p => p.Id == id);
            if (prereq != null)
            {
                prereq.Update(displayOrder: order++);
            }
        }

        await _context.SaveChangesAsync().ConfigureAwait(false);

        _logger.LogInformation("Reordered prerequisites for course {CourseId}", courseId);

        return Result.Success(true);
    }
}
