using FluentAssertions;
using Xunit;

namespace GameGuild.Learning.Assessments.Tests;

/// <summary>
/// Unit tests for CourseGroupSet / CourseGroup / CourseGroupMember entity domain logic.
/// </summary>
public class CourseGroupEntityTests
{
    [Fact]
    public void CourseGroupSet_Create_ShouldSetFieldsAndTrimName()
    {
        var courseId = Guid.NewGuid();
        var set = CourseGroupSet.Create(courseId, "  Project groups  ");

        set.Id.Should().NotBeEmpty();
        set.CourseId.Should().Be(courseId);
        set.Name.Should().Be("Project groups");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void CourseGroupSet_Create_WithEmptyName_Throws(string name)
    {
        var action = () => CourseGroupSet.Create(Guid.NewGuid(), name);

        action.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void CourseGroup_Create_ShouldSetFieldsAndTrimName()
    {
        var groupSetId = Guid.NewGuid();
        var group = CourseGroup.Create(groupSetId, "  Team Alpha  ", 4);

        group.Id.Should().NotBeEmpty();
        group.GroupSetId.Should().Be(groupSetId);
        group.Name.Should().Be("Team Alpha");
        group.Capacity.Should().Be(4);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void CourseGroup_Create_WithEmptyName_Throws(string name)
    {
        var action = () => CourseGroup.Create(Guid.NewGuid(), name, 4);

        action.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(-3)]
    public void CourseGroup_Create_WithCapacityBelowTwo_Throws(int capacity)
    {
        var action = () => CourseGroup.Create(Guid.NewGuid(), "Team Alpha", capacity);

        action.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void CourseGroupMember_Create_ShouldSetFields()
    {
        var groupId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var member = CourseGroupMember.Create(groupId, userId);

        member.Id.Should().NotBeEmpty();
        member.GroupId.Should().Be(groupId);
        member.UserId.Should().Be(userId);
        member.JoinedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(2));
    }

    [Fact]
    public void Assessment_AssignToGroupSet_ShouldSetGroupSetIdAndTouchUpdatedAt()
    {
        var assessment = Assessment.Create(Guid.NewGuid(), "Group project", AssessmentType.Project, Score(100));
        var originalUpdatedAt = assessment.UpdatedAt;
        Thread.Sleep(20);
        var groupSetId = Guid.NewGuid();

        assessment.AssignToGroupSet(groupSetId);

        assessment.GroupSetId.Should().Be(groupSetId);
        assessment.UpdatedAt.Should().BeAfter(originalUpdatedAt);
    }

    [Fact]
    public void Assessment_AssignToGroupSet_WithNull_ShouldClearGroupSetId()
    {
        var assessment = Assessment.Create(Guid.NewGuid(), "Group project", AssessmentType.Project, Score(100));
        assessment.AssignToGroupSet(Guid.NewGuid());

        assessment.AssignToGroupSet(null);

        assessment.GroupSetId.Should().BeNull();
    }
}
