
namespace GameGuild.Learning.Experience.LearningPaths;

/// <summary>
/// Represents a curated sequence of courses forming a learning path
/// </summary>
public class LearningPath : EntityBase
{
    public new Guid? TenantId { get; private set; }
    public Guid CreatorId { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string Slug { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public string? ImageUrl { get; private set; }
    public int EstimatedHours { get; private set; }
    public LearningPathDifficulty Difficulty { get; private set; }
    public bool IsPublished { get; private set; }
    public bool IsFeatured { get; private set; }
    public int EnrollmentCount { get; private set; }
    public int CompletionCount { get; private set; }

    private readonly List<LearningPathCourse> _courses = new();
    public IReadOnlyCollection<LearningPathCourse> Courses => _courses.AsReadOnly();

    private LearningPath() { } // EF Core

    public static LearningPath Create(
        Guid creatorId,
        string title,
        string slug,
        LearningPathDifficulty difficulty = LearningPathDifficulty.Beginner,
        Guid? tenantId = null,
        string? description = null,
        string? imageUrl = null,
        int estimatedHours = 0)
    {
        return new LearningPath
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            CreatorId = creatorId,
            Title = title,
            Slug = slug,
            Description = description,
            ImageUrl = imageUrl,
            EstimatedHours = estimatedHours,
            Difficulty = difficulty,
            IsPublished = false,
            IsFeatured = false,
            EnrollmentCount = 0,
            CompletionCount = 0
        };
    }

    public void Update(
        string? title,
        string? description,
        string? imageUrl,
        int? estimatedHours,
        LearningPathDifficulty? difficulty,
        bool? isFeatured)
    {
        if (title is not null)
        {
            Title = title;
        }

        if (description is not null)
        {
            Description = description;
        }

        if (imageUrl is not null)
        {
            ImageUrl = imageUrl;
        }

        if (estimatedHours.HasValue)
        {
            EstimatedHours = estimatedHours.Value;
        }

        if (difficulty.HasValue)
        {
            Difficulty = difficulty.Value;
        }

        if (isFeatured.HasValue)
        {
            IsFeatured = isFeatured.Value;
        }

        UpdatedAt = SystemClock.UtcNow;
    }

    public void AddCourse(Guid courseId, int order, bool isRequired = true)
    {
        var item = new LearningPathCourse(Id, courseId, order, isRequired);
        _courses.Add(item);
        UpdatedAt = SystemClock.UtcNow;
    }

    public void Publish()
    {
        IsPublished = true;
        UpdatedAt = SystemClock.UtcNow;
    }

    public void Unpublish()
    {
        IsPublished = false;
        UpdatedAt = SystemClock.UtcNow;
    }
}

/// <summary>
/// Junction entity linking courses to learning paths with ordering
/// </summary>
public class LearningPathCourse
{
    public Guid LearningPathId { get; private set; }
    public Guid CourseId { get; private set; }
    public int Order { get; private set; }
    public bool IsRequired { get; private set; }

    private LearningPathCourse() { } // EF Core

    public LearningPathCourse(Guid learningPathId, Guid courseId, int order, bool isRequired)
    {
        LearningPathId = learningPathId;
        CourseId = courseId;
        Order = order;
        IsRequired = isRequired;
    }

    public void SetOrder(int order) { Order = order; }
}

/// <summary>
/// Tracks a user's progress through a learning path
/// </summary>
public class LearningPathEnrollment : EntityBase
{
    public Guid LearningPathId { get; private set; }
    public Guid UserId { get; private set; }
    public int Progress { get; private set; } // 0-100
    public int CoursesCompleted { get; private set; }
    public int TotalCourses { get; private set; }
    public DateTime EnrolledAt { get; private set; }
    public DateTime? CompletedAt { get; private set; }
    public LearningPathEnrollmentStatus Status { get; private set; }

    private LearningPathEnrollment() { } // EF Core

    public static LearningPathEnrollment Create(Guid learningPathId, Guid userId, int totalCourses)
    {
        return new LearningPathEnrollment
        {
            Id = Guid.NewGuid(),
            LearningPathId = learningPathId,
            UserId = userId,
            Progress = 0,
            CoursesCompleted = 0,
            TotalCourses = totalCourses,
            EnrolledAt = SystemClock.UtcNow,
            Status = LearningPathEnrollmentStatus.InProgress
        };
    }

    public void UpdateProgress(int coursesCompleted)
    {
        if (coursesCompleted < 0 || coursesCompleted > TotalCourses)
        {
            throw new ArgumentOutOfRangeException(nameof(coursesCompleted), coursesCompleted, $"Courses completed must be between 0 and {TotalCourses}.");
        }

        CoursesCompleted = coursesCompleted;
        Progress = TotalCourses > 0 ? (int)((double)coursesCompleted / TotalCourses * 100) : 0;
        UpdatedAt = SystemClock.UtcNow;

        if (CoursesCompleted >= TotalCourses)
        {
            Complete();
        }
    }

    public void Complete()
    {
        Status = LearningPathEnrollmentStatus.Completed;
        CompletedAt = SystemClock.UtcNow;
        Progress = 100;
        UpdatedAt = SystemClock.UtcNow;
    }

    public void Abandon()
    {
        Status = LearningPathEnrollmentStatus.Abandoned;
        CompletedAt = null;
        UpdatedAt = SystemClock.UtcNow;
    }
}

public enum LearningPathDifficulty
{
    Beginner,
    Intermediate,
    Advanced,
    Expert
}

public enum LearningPathEnrollmentStatus
{
    InProgress,
    Completed,
    Abandoned
}
