using FluentAssertions;
using Xunit;

namespace GameGuild.Learning.Assessments.Tests;

/// <summary>
/// Unit tests for AssessmentRubric / RubricCriterion entity domain logic.
/// </summary>
public class AssessmentRubricEntityTests
{
    [Fact]
    public void AssessmentRubric_Create_ShouldSetFieldsAndTrimTitle()
    {
        var rubric = AssessmentRubric.Create("  Essay rubric  ");

        rubric.Id.Should().NotBeEmpty();
        rubric.Title.Should().Be("Essay rubric");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void AssessmentRubric_Create_WithEmptyTitle_Throws(string title)
    {
        var action = () => AssessmentRubric.Create(title);

        action.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void RubricCriterion_Create_ShouldSetFields()
    {
        var rubricId = Guid.NewGuid();
        var criterion = RubricCriterion.Create(rubricId, "  Correctness  ", Score(10), 1);

        criterion.Id.Should().NotBeEmpty();
        criterion.RubricId.Should().Be(rubricId);
        criterion.Description.Should().Be("Correctness");
        criterion.Points.Should().Be(Score(10));
        criterion.Order.Should().Be(1);
    }

    [Fact]
    public void RubricCriterion_Create_WithZeroPoints_Throws()
    {
        var action = () => RubricCriterion.Create(Guid.NewGuid(), "Correctness", ScoreValue.Zero, 1);

        action.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void RubricCriterion_Create_WithEmptyDescription_Throws(string description)
    {
        var action = () => RubricCriterion.Create(Guid.NewGuid(), description, Score(10), 1);

        action.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Assessment_AssignRubric_ShouldSetRubricIdAndTouchUpdatedAt()
    {
        var assessment = Assessment.Create(Guid.NewGuid(), "Essay", AssessmentType.Assignment, Score(100));
        var originalUpdatedAt = assessment.UpdatedAt;
        Thread.Sleep(20);
        var rubricId = Guid.NewGuid();

        assessment.AssignRubric(rubricId);

        assessment.RubricId.Should().Be(rubricId);
        assessment.UpdatedAt.Should().BeAfter(originalUpdatedAt);
    }

    [Fact]
    public void Assessment_AssignRubric_WithNull_ShouldClearRubricId()
    {
        var assessment = Assessment.Create(Guid.NewGuid(), "Essay", AssessmentType.Assignment, Score(100));
        assessment.AssignRubric(Guid.NewGuid());

        assessment.AssignRubric(null);

        assessment.RubricId.Should().BeNull();
    }
}
