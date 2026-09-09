
using GameGuild.Learning.Assessments.Grading.Contracts;
using GameGuild.Learning.Grading.Contracts;

namespace GameGuild.Learning.Assessments;

/// <summary>
/// Represents a scoring rubric that can be attached to an assessment.
/// </summary>
public class AssessmentRubric : EntityBase
{
    public string Title { get; private set; } = string.Empty;

    public void Replace(string title)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            throw new ArgumentException("Title is required.", nameof(title));
        }

        Title = title.Trim();
        UpdatedAt = SystemClock.UtcNow;
    }

    private AssessmentRubric() { } // EF Core

    public static AssessmentRubric Create(string title)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            throw new ArgumentException("Title is required.", nameof(title));
        }

        return new AssessmentRubric
        {
            Id = Guid.NewGuid(),
            Title = title.Trim()
        };
    }
}

/// <summary>
/// Represents a single criterion (points bucket) within an assessment rubric.
/// </summary>
public class RubricCriterion : EntityBase
{
    public Guid RubricId { get; private set; }
    public string Description { get; private set; } = string.Empty;
    public ScoreValue Points { get; private set; }
    public int Order { get; private set; }

    private RubricCriterion() { } // EF Core

    public static RubricCriterion Create(Guid rubricId, string description, ScoreValue points, int order)
    {
        if (string.IsNullOrWhiteSpace(description))
        {
            throw new ArgumentException("Description is required.", nameof(description));
        }

        if (points.CompareTo(ScoreValue.Zero) <= 0)
        {
            throw new ArgumentException("Points must be positive.", nameof(points));
        }

        return new RubricCriterion
        {
            Id = Guid.NewGuid(),
            RubricId = rubricId,
            Description = description.Trim(),
            Points = points,
            Order = order
        };
    }
}
