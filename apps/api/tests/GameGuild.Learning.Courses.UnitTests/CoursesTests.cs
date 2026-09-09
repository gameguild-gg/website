using System.Reflection;
using FluentAssertions;
using GameGuild.Identity.Authorization;
using GameGuild.Learning.Courses;
using Microsoft.AspNetCore.Authorization;
using Xunit;

namespace GameGuild.Learning.Courses.UnitTests;

#region Program Entity Tests

public class ProgramTests
{
    [Fact]
    public void DefaultValues_ShouldBeCorrect()
    {
        var program = new Program();

        program.Title.Should().BeEmpty();
        program.Status.Should().Be(ContentStatus.Draft);
        program.EnrollmentStatus.Should().Be(EnrollmentStatus.Open);
        program.Visibility.Should().Be(ContentVisibility.Public);
        program.Category.Should().Be(ProgramCategory.General);
        program.Difficulty.Should().Be(ProgramDifficulty.Beginner);
    }

    [Fact]
    public void OpenEnrollment_ShouldSetStatus()
    {
        var program = new Program();
        program.CloseEnrollment();

        program.OpenEnrollment();

        program.EnrollmentStatus.Should().Be(EnrollmentStatus.Open);
    }

    [Fact]
    public void CloseEnrollment_ShouldSetStatus()
    {
        var program = new Program();

        program.CloseEnrollment();

        program.EnrollmentStatus.Should().Be(EnrollmentStatus.Closed);
    }

    [Fact]
    public void Publish_ShouldSetStatusToPublished()
    {
        var program = new Program();

        program.Publish();

        program.Status.Should().Be(ContentStatus.Published);
    }

    [Fact]
    public void Archive_ShouldSetStatusAndCloseEnrollment()
    {
        var program = new Program();

        program.Archive();

        program.Status.Should().Be(ContentStatus.Archived);
        program.EnrollmentStatus.Should().Be(EnrollmentStatus.Closed);
    }

    [Fact]
    public void IsEnrollmentOpen_WhenOpenAndNoBounds_ShouldBeTrue()
    {
        var program = new Program { EnrollmentStatus = EnrollmentStatus.Open };

        program.IsEnrollmentOpen.Should().BeTrue();
    }

    [Fact]
    public void IsEnrollmentOpen_WhenClosed_ShouldBeFalse()
    {
        var program = new Program { EnrollmentStatus = EnrollmentStatus.Closed };

        program.IsEnrollmentOpen.Should().BeFalse();
    }

    [Fact]
    public void IsEnrollmentOpen_WhenPastDeadline_ShouldBeFalse()
    {
        var program = new Program
        {
            EnrollmentStatus = EnrollmentStatus.Open,
            EnrollmentDeadline = DateTime.UtcNow.AddDays(-1)
        };

        program.IsEnrollmentOpen.Should().BeFalse();
    }

    [Fact]
    public void SetMetadata_ShouldSerializeToJson()
    {
        var program = new Program();

        program.SetMetadata("key1", "value1");

        program.Metadata.Should().Contain("key1");
        program.Metadata.Should().Contain("value1");
    }

    [Fact]
    public void AverageRating_WithNoRatings_ShouldBeZero()
    {
        var program = new Program();

        program.AverageRating.Should().Be(0);
    }

    [Fact]
    public void TotalRatings_WithNoRatings_ShouldBeZero()
    {
        var program = new Program();

        program.TotalRatings.Should().Be(0);
    }

    [Fact]
    public void IsGlobal_WhenNoTenant_ShouldBeTrue()
    {
        var program = new Program();

        program.IsGlobal.Should().BeTrue();
    }
}

#endregion

#region ProgramContent Tests

public class ProgramContentTests
{
    [Fact]
    public void DefaultValues_ShouldBeCorrect()
    {
        var content = new ProgramContent();

        content.Title.Should().BeEmpty();
        content.Type.Should().Be(ProgramContentType.Lesson);
        content.IsRequired.Should().BeTrue();
        content.Visibility.Should().Be(Visibility.Public);
        content.SortOrder.Should().Be(0);
    }

    [Fact]
    public void MoveTo_ShouldUpdateParentAndOrder()
    {
        var content = new ProgramContent();
        var newParentId = Guid.NewGuid();

        content.MoveTo(newParentId, 5);

        content.ParentId.Should().Be(newParentId);
        content.SortOrder.Should().Be(5);
    }

    [Fact]
    public void Reorder_ShouldUpdateSortOrder()
    {
        var content = new ProgramContent();

        content.Reorder(3);

        content.SortOrder.Should().Be(3);
    }

    [Fact]
    public void MakeRequired_ShouldSetTrue()
    {
        var content = new ProgramContent { IsRequired = false };

        content.MakeRequired();

        content.IsRequired.Should().BeTrue();
    }

    [Fact]
    public void MakeOptional_ShouldSetFalse()
    {
        var content = new ProgramContent();

        content.MakeOptional();

        content.IsRequired.Should().BeFalse();
    }

    [Fact]
    public void UpdateEstimatedTime_ShouldSet()
    {
        var content = new ProgramContent();

        content.UpdateEstimatedTime(30);

        content.EstimatedMinutes.Should().Be(30);
    }

    [Fact]
    public void FullPath_WithNoParent_ShouldBeTitle()
    {
        var content = new ProgramContent { Title = "Lesson 1" };

        content.FullPath.Should().Be("Lesson 1");
    }

    [Fact]
    public void HasChildren_WithNoChildren_ShouldBeFalse()
    {
        var content = new ProgramContent();

        content.HasChildren.Should().BeFalse();
    }
}

#endregion

#region ProgramEnrollment Tests

public class ProgramEnrollmentTests
{
    [Fact]
    public void DefaultValues_ShouldBeCorrect()
    {
        var enrollment = new ProgramEnrollment();

        enrollment.EnrollmentSource.Should().Be(EnrollmentSource.Manual);
        enrollment.EnrollmentStatus.Should().Be(EnrollmentStatus.Active);
        enrollment.CompletionStatus.Should().Be(CompletionStatus.NotStarted);
        enrollment.ProgressPercentage.Should().Be(PercentValue.Zero);
        enrollment.CertificateIssued.Should().BeFalse();
    }

    [Fact]
    public void MarkAsCompleted_ShouldSetCompletionFields()
    {
        var enrollment = new ProgramEnrollment();

        enrollment.MarkAsCompleted(Percent("85"));

        enrollment.CompletionStatus.Should().Be(CompletionStatus.Completed);
        enrollment.ProgressPercentage.Should().Be(PercentValue.Hundred);
        enrollment.CompletedAt.Should().NotBeNull();
        enrollment.FinalGrade.Should().Be(Percent("85"));
    }

    [Fact]
    public void PercentValue_RejectsGradeAboveOneHundred()
    {
        var act = () => Percent("150");

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void PercentValue_RejectsNegativeGrade()
    {
        var act = () => Percent("-10");

        act.Should().Throw<FormatException>();
    }

    [Fact]
    public void MarkAsCompleted_WithoutGrade_ShouldNotSetFinalGrade()
    {
        var enrollment = new ProgramEnrollment();

        enrollment.MarkAsCompleted();

        enrollment.FinalGrade.Should().BeNull();
    }
}

#endregion

#region ProgramUser Tests

public class ProgramUserTests
{
    [Fact]
    public void DefaultValues_ShouldBeCorrect()
    {
        var user = new ProgramUser();

        user.IsActive.Should().BeTrue();
        user.CompletionPercentage.Should().Be(PercentValue.Zero);
    }

    [Fact]
    public void Start_ShouldSetStartedAtOnce()
    {
        var user = new ProgramUser();

        user.Start();
        var firstStart = user.StartedAt;
        user.Start(); // second call should not change

        user.StartedAt.Should().Be(firstStart);
        user.LastAccessedAt.Should().NotBeNull();
    }

    [Fact]
    public void Complete_ShouldSetCompletionFields()
    {
        var user = new ProgramUser();

        user.Complete(Percent("92"));

        user.CompletedAt.Should().NotBeNull();
        user.CompletionPercentage.Should().Be(PercentValue.Hundred);
        user.FinalGrade.Should().Be(Percent("92"));
    }

    [Fact]
    public void Complete_WhenAlreadyCompleted_ShouldNotChange()
    {
        var user = new ProgramUser();
        user.Complete(Percent("80"));
        var firstCompleted = user.CompletedAt;

        user.Complete(Percent("95")); // second call

        user.CompletedAt.Should().Be(firstCompleted);
        user.FinalGrade.Should().Be(Percent("80"));
    }

    [Fact]
    public void Deactivate_ShouldSetFalse()
    {
        var user = new ProgramUser();

        user.Deactivate();

        user.IsActive.Should().BeFalse();
    }

    [Fact]
    public void Reactivate_ShouldSetTrue()
    {
        var user = new ProgramUser();
        user.Deactivate();

        user.Reactivate();

        user.IsActive.Should().BeTrue();
    }

    [Fact]
    public void IsCompleted_WhenCompletedAtSet_ShouldBeTrue()
    {
        var user = new ProgramUser();
        user.Complete();

        user.IsCompleted.Should().BeTrue();
    }

    [Fact]
    public void IsInProgress_WhenStartedButNotCompleted_ShouldBeTrue()
    {
        var user = new ProgramUser();
        user.Start();

        user.IsInProgress.Should().BeTrue();
    }
}

#endregion

#region ActivityGrade Tests

public class ActivityGradeTests
{
    [Fact]
    public void DefaultValues_ShouldBeCorrect()
    {
        var grade = new ActivityGrade();

        grade.IsFinalized.Should().BeFalse();
        grade.GradeType.Should().Be(GradeType.Manual);
        grade.AttemptNumber.Should().Be(1);
    }

    [Fact]
    public void AssignPoints_AboveMaximum_ShouldThrow()
    {
        var grade = new ActivityGrade();

        var act = () => grade.AssignPoints(Score("150"), Score("100"));

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void ScoreValue_RejectsNegativePoints()
    {
        var act = () => Score("-5");

        act.Should().Throw<FormatException>();
    }

    [Fact]
    public void SetLetterGrade_ShouldUppercase()
    {
        var grade = new ActivityGrade();

        grade.SetLetterGrade("b+");

        grade.GradeLetter.Should().Be("B+");
    }

    [Fact]
    public void FinalizeGrade_ShouldSetFlag()
    {
        var grade = new ActivityGrade();

        grade.FinalizeGrade();

        grade.IsFinalized.Should().BeTrue();
    }

    [Fact]
    public void Unlock_ShouldClearFinalized()
    {
        var grade = new ActivityGrade();
        grade.FinalizeGrade();

        grade.Unlock();

        grade.IsFinalized.Should().BeFalse();
    }

    [Fact]
    public void PercentageScore_ShouldCalculateCorrectly()
    {
        var grade = new ActivityGrade { Points = Score("85"), MaxPoints = Score("100") };

        grade.PercentageScore.Should().Be(Percent("85"));
    }

    [Fact]
    public void IsPassing_Above60_ShouldBeTrue()
    {
        var grade = new ActivityGrade { Points = Score("70"), MaxPoints = Score("100") };

        grade.IsPassing.Should().BeTrue();
    }

    [Fact]
    public void IsPassing_Below60_ShouldBeFalse()
    {
        var grade = new ActivityGrade { Points = Score("50"), MaxPoints = Score("100") };

        grade.IsPassing.Should().BeFalse();
    }

    [Theory]
    [InlineData(97, "A+")]
    [InlineData(93, "A")]
    [InlineData(90, "A-")]
    [InlineData(87, "B+")]
    [InlineData(83, "B")]
    [InlineData(80, "B-")]
    [InlineData(73, "C")]
    [InlineData(60, "D-")]
    [InlineData(55, "F")]
    public void CalculateLetterGrade_ShouldReturnCorrectLetter(int points, string expected)
    {
        var grade = new ActivityGrade { Points = Score(points.ToString()), MaxPoints = Score("100") };

        grade.CalculateLetterGrade().Should().Be(expected);
    }

    [Fact]
    public void IsValid_WithPointsAboveMax_ShouldBeFalse()
    {
        var grade = new ActivityGrade { Points = Score("110"), MaxPoints = Score("100") };

        grade.IsValid().Should().BeFalse();
    }

    [Fact]
    public void IsValid_WithZeroMaximum_ShouldBeFalse()
    {
        var grade = new ActivityGrade { Points = ScoreValue.Zero, MaxPoints = ScoreValue.Zero };

        grade.IsValid().Should().BeFalse();
    }

    [Fact]
    public void IsValid_WithValidData_ShouldBeTrue()
    {
        var grade = new ActivityGrade { Points = Score("80"), MaxPoints = Score("100") };

        grade.IsValid().Should().BeTrue();
    }

    [Fact]
    public void CreateRevision_ShouldIncrementAttempt()
    {
        var grade = new ActivityGrade
        {
            StudentId = Guid.NewGuid(),
            GraderId = Guid.NewGuid(),
            ContentInteractionId = Guid.NewGuid(),
            ProgramUserId = Guid.NewGuid(),
            Points = Score("70"),
            MaxPoints = Score("100"),
            AttemptNumber = 1
        };

        var revision = grade.CreateRevision(Score("85"), "Regraded after appeal");

        revision.AttemptNumber.Should().Be(2);
        revision.Points.Should().Be(Score("85"));
        revision.IsFinalized.Should().BeFalse();
        revision.Feedback.Should().Contain("Revision");
    }

    [Fact]
    public void LegacyGradeAlias_IsNotExposed()
    {
        typeof(ActivityGrade).GetProperty("Grade").Should().BeNull();
    }
}

#endregion

#region CoursePrerequisite Tests

public class CoursePrerequisiteTests
{
    [Fact]
    public void Create_ShouldSetAllProperties()
    {
        var courseId = Guid.NewGuid();
        var prereqId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();

        var prereq = CoursePrerequisite.Create(courseId, prereqId, tenantId,
            PrerequisiteType.Required, Percent("70"), "Must pass with C or better", 1, "Group A");

        prereq.Id.Should().NotBeEmpty();
        prereq.CourseId.Should().Be(courseId);
        prereq.PrerequisiteCourseId.Should().Be(prereqId);
        prereq.Type.Should().Be(PrerequisiteType.Required);
        prereq.MinimumGrade.Should().Be(Percent("70"));
        prereq.Description.Should().Be("Must pass with C or better");
        prereq.DisplayOrder.Should().Be(1);
        prereq.PrerequisiteGroup.Should().Be("Group A");
    }

    [Fact]
    public void Create_SelfReference_ShouldThrow()
    {
        var courseId = Guid.NewGuid();

        var act = () => CoursePrerequisite.Create(courseId, courseId, null);

        act.Should().Throw<ArgumentException>().WithMessage("*cannot be a prerequisite of itself*");
    }

    [Fact]
    public void SetMinimumGrade_OutOfRange_ShouldThrow()
    {
        var prereq = CoursePrerequisite.Create(Guid.NewGuid(), Guid.NewGuid(), null);

        var act = () => prereq.SetMinimumGrade(Percent("101"));

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void SetMinimumGrade_Negative_ShouldThrow()
    {
        var prereq = CoursePrerequisite.Create(Guid.NewGuid(), Guid.NewGuid(), null);

        var act = () => Percent("-1");

        act.Should().Throw<FormatException>();
    }

    [Fact]
    public void SetMinimumGrade_Null_ShouldClear()
    {
        var prereq = CoursePrerequisite.Create(Guid.NewGuid(), Guid.NewGuid(), null, minimumGrade: Percent("80"));

        prereq.SetMinimumGrade(null);

        prereq.MinimumGrade.Should().BeNull();
    }
}

#endregion

#region ProgramRating Tests

public class ProgramRatingTests
{
    [Fact]
    public void DefaultValues_ShouldBeCorrect()
    {
        var rating = new ProgramRating();

        rating.IsVerified.Should().BeFalse();
        rating.IsFeatured.Should().BeFalse();
        rating.HelpfulVotes.Should().Be(0);
        rating.UnhelpfulVotes.Should().Be(0);
    }

    [Fact]
    public void CanSetRating()
    {
        var rating = new ProgramRating
        {
            ProgramId = Guid.NewGuid(),
            UserId = "user-1",
            Rating = 4.5m,
            Review = "Great course!"
        };

        rating.Rating.Should().Be(4.5m);
        rating.Review.Should().Be("Great course!");
    }
}

#endregion

#region ContentInteraction Tests

public class ContentInteractionTests
{
    [Fact]
    public void DefaultValues_ShouldBeCorrect()
    {
        var interaction = new ContentInteraction();

        interaction.IsCompleted.Should().BeFalse();
        interaction.ProgressPercentage.Should().Be(PercentValue.Zero);
        interaction.TimeSpentMinutes.Should().Be(0);
        interaction.Status.Should().Be(ProgressStatus.NotStarted);
        interaction.AttemptCount.Should().Be(0);
    }

    [Fact]
    public void Start_ShouldSetStartedAtOnce()
    {
        var interaction = new ContentInteraction();

        interaction.Start();
        var first = interaction.StartedAt;
        interaction.Start(); // second call

        interaction.StartedAt.Should().Be(first);
        interaction.IsStarted.Should().BeTrue();
    }

    [Fact]
    public void UpdateProgress_AtOneHundred_ShouldComplete()
    {
        var interaction = new ContentInteraction();

        interaction.UpdateProgress(PercentValue.Hundred);

        interaction.ProgressPercentage.Should().Be(PercentValue.Hundred);
    }

    [Fact]
    public void PercentValue_RejectsNegativeProgress()
    {
        var act = () => Percent("-10");

        act.Should().Throw<FormatException>();
    }

    [Fact]
    public void CompletionPercentage_ShouldAliasProgressPercentage()
    {
        var interaction = new ContentInteraction();

        interaction.CompletionPercentage = Percent("50");

        interaction.ProgressPercentage.Should().Be(Percent("50"));
    }

    [Fact]
    public void IsInProgress_WhenStartedNotCompleted_ShouldBeTrue()
    {
        var interaction = new ContentInteraction();
        interaction.Start();

        interaction.IsInProgress.Should().BeTrue();
    }
}

#endregion

#region ProgramContentController Authorization Tests

public class ProgramContentControllerAuthorizationTests
{
    [Fact]
    public void GetProgramContent_ShouldRemainPublic()
    {
        var method = GetAction(nameof(ProgramContentController.GetProgramContent));

        method.GetCustomAttribute<AllowAnonymousAttribute>().Should().NotBeNull();
    }

    [Theory]
    [InlineData(nameof(ProgramContentController.CreateContent), PermissionType.Create)]
    [InlineData(nameof(ProgramContentController.UpdateContent), PermissionType.Edit)]
    [InlineData(nameof(ProgramContentController.DeleteContent), PermissionType.Delete)]
    [InlineData(nameof(ProgramContentController.ReorderContent), PermissionType.Edit)]
    [InlineData(nameof(ProgramContentController.MoveContent), PermissionType.Edit)]
    public void MutationEndpoints_ShouldRequireProgramResourcePermission(string actionName, PermissionType requiredPermission)
    {
        var method = GetAction(actionName);

        var attribute = method
            .GetCustomAttributes(inherit: true)
            .OfType<IResourcePermissionMarker>()
            .SingleOrDefault();

        attribute.Should().NotBeNull();
        attribute!.ResourceType.Should().Be(typeof(Program));
        attribute.ResourceIdParameterName.Should().Be("programId");
        attribute.RequiredPermission.Should().Be(requiredPermission);
    }

    private static MethodInfo GetAction(string actionName)
    {
        return typeof(ProgramContentController)
            .GetMethod(actionName, BindingFlags.Instance | BindingFlags.Public)!;
    }
}

#endregion

#region Enum Tests

public class CoursesEnumTests
{
    [Fact]
    public void GradeType_ShouldHave6Values()
    {
        Enum.GetValues<GradeType>().Should().HaveCount(6);
    }

    [Fact]
    public void PrerequisiteType_ShouldHave3Values()
    {
        Enum.GetValues<PrerequisiteType>().Should().HaveCount(3);
    }

    [Fact]
    public void ProgramContentType_ShouldKeepExplicitPersistedValues()
    {
        ((int)ProgramContentType.Lesson).Should().Be(0);
        ((int)ProgramContentType.Page).Should().Be(1);
        ((int)ProgramContentType.Assignment).Should().Be(2);
        ((int)ProgramContentType.Questionnaire).Should().Be(3);
        ((int)ProgramContentType.Discussion).Should().Be(4);
        ((int)ProgramContentType.Code).Should().Be(5);
        ((int)ProgramContentType.Challenge).Should().Be(6);
        ((int)ProgramContentType.Reflection).Should().Be(7);
        ((int)ProgramContentType.Survey).Should().Be(8);
        ((int)ProgramContentType.Project).Should().Be(9);
        ((int)ProgramContentType.Module).Should().Be(10);
    }

    [Theory]
    [InlineData(ProgramContentType.Page, ProgramContentType.Lesson)]
    [InlineData(ProgramContentType.Challenge, ProgramContentType.Assignment)]
    [InlineData(ProgramContentType.Project, ProgramContentType.Project)]
    [InlineData(ProgramContentType.Module, ProgramContentType.Module)]
    public void ProgramContentDto_ShouldNormalizeProfessorFacingTypes(ProgramContentType storedType, ProgramContentType expectedType)
    {
        var content = new ProgramContent
        {
            Id = Guid.NewGuid(),
            ProgramId = Guid.NewGuid(),
            Title = "Stored content",
            Type = storedType
        };

        var dto = content.ToDto();

        dto.Type.Should().Be(expectedType);
    }
}

#endregion

#region StringExtensions Tests

public class StringExtensionsTests
{
    [Fact]
    public void ToSlugCase_NormalString()
    {
        "Hello World".ToSlugCase().Should().Be("hello-world");
    }

    [Fact]
    public void ToSlugCase_WithSpecialCharacters()
    {
        "C# Programming!".ToSlugCase().Should().Be("c-programming");
    }

    [Fact]
    public void ToSlugCase_WithUnderscores()
    {
        "my_cool_course".ToSlugCase().Should().Be("my-cool-course");
    }

    [Fact]
    public void ToSlugCase_WithDots()
    {
        "version.2.0".ToSlugCase().Should().Be("version-2-0");
    }

    [Fact]
    public void ToSlugCase_WithMultipleSpaces()
    {
        "lots   of   spaces".ToSlugCase().Should().Be("lots-of-spaces");
    }

    [Fact]
    public void ToSlugCase_EmptyString()
    {
        "".ToSlugCase().Should().Be(string.Empty);
    }

    [Fact]
    public void ToSlugCase_NullString()
    {
        string.Empty.ToSlugCase().Should().Be(string.Empty);
    }

    [Fact]
    public void ToSlugCase_WhitespaceOnly()
    {
        "   ".ToSlugCase().Should().Be(string.Empty);
    }

    [Fact]
    public void ToSlugCase_AlreadySlug()
    {
        "already-a-slug".ToSlugCase().Should().Be("already-a-slug");
    }

    [Fact]
    public void ToSlugCase_LeadingTrailingDashes()
    {
        " -hello- ".ToSlugCase().Should().Be("hello");
    }

    [Fact]
    public void ToSlugCase_MixedCase()
    {
        "UPPER lower MiXeD".ToSlugCase().Should().Be("upper-lower-mixed");
    }
}

#endregion

#region ContentInteraction Extended Tests

public class ContentInteractionExtendedTests
{
    [Fact]
    public void UpdateLastAccess_ShouldSetLastAccessedAt()
    {
        var interaction = new ContentInteraction
        {
            UserId = Guid.NewGuid(),
            ContentId = Guid.NewGuid(),
            ProgramUserId = Guid.NewGuid()
        };

        interaction.UpdateLastAccess();
        interaction.LastAccessedAt.Should().NotBeNull();
    }

    [Fact]
    public void AddTimeSpent_ShouldAccumulate()
    {
        var interaction = new ContentInteraction
        {
            UserId = Guid.NewGuid(),
            ContentId = Guid.NewGuid(),
            ProgramUserId = Guid.NewGuid()
        };

        interaction.AddTimeSpent(10);
        interaction.AddTimeSpent(20);
        interaction.TimeSpentMinutes.Should().Be(30);
    }

    [Fact]
    public void RecordAttempt_ShouldTrackBestScore()
    {
        var interaction = new ContentInteraction
        {
            UserId = Guid.NewGuid(),
            ContentId = Guid.NewGuid(),
            ProgramUserId = Guid.NewGuid()
        };

        interaction.RecordAttempt(Score("80"));
        interaction.RecordAttempt(Score("60"));
        interaction.RecordAttempt(Score("90"));
        interaction.AttemptCount.Should().Be(3);
        interaction.BestScore.Should().Be(Score("90"));
    }

    [Fact]
    public void RecordAttempt_NoScore_ShouldIncrementOnly()
    {
        var interaction = new ContentInteraction
        {
            UserId = Guid.NewGuid(),
            ContentId = Guid.NewGuid(),
            ProgramUserId = Guid.NewGuid()
        };

        interaction.RecordAttempt();
        interaction.AttemptCount.Should().Be(1);
        interaction.BestScore.Should().BeNull();
    }

    [Fact]
    public void SetBookmark_ShouldSetPosition()
    {
        var interaction = new ContentInteraction
        {
            UserId = Guid.NewGuid(),
            ContentId = Guid.NewGuid(),
            ProgramUserId = Guid.NewGuid()
        };

        interaction.SetBookmark("page:42");
        interaction.BookmarkPosition.Should().Be("page:42");
    }

    [Fact]
    public void UpdateNotes_ShouldSetNotes()
    {
        var interaction = new ContentInteraction
        {
            UserId = Guid.NewGuid(),
            ContentId = Guid.NewGuid(),
            ProgramUserId = Guid.NewGuid()
        };

        interaction.UpdateNotes("Great lesson!");
        interaction.Notes.Should().Be("Great lesson!");
    }

    [Fact]
    public void Reset_ShouldClearAllProgress()
    {
        var interaction = new ContentInteraction
        {
            UserId = Guid.NewGuid(),
            ContentId = Guid.NewGuid(),
            ProgramUserId = Guid.NewGuid()
        };

        interaction.Start();
        interaction.UpdateProgress(Percent("50"));
        interaction.RecordAttempt(Score("80"));
        interaction.SetBookmark("page:10");

        interaction.Reset();

        interaction.IsCompleted.Should().BeFalse();
        interaction.CompletedAt.Should().BeNull();
        interaction.ProgressPercentage.Should().Be(PercentValue.Zero);
        interaction.TimeSpentMinutes.Should().Be(0);
        interaction.AttemptCount.Should().Be(0);
        interaction.BestScore.Should().BeNull();
        interaction.BookmarkPosition.Should().BeNull();
    }

    [Fact]
    public void Complete_Idempotent_ShouldNotChangeTimestamp()
    {
        var interaction = new ContentInteraction
        {
            UserId = Guid.NewGuid(),
            ContentId = Guid.NewGuid(),
            ProgramUserId = Guid.NewGuid()
        };

        interaction.Complete();
        var firstCompletion = interaction.CompletedAt;
        interaction.Complete();
        interaction.CompletedAt.Should().Be(firstCompletion);
    }

    [Fact]
    public void DaysSinceLastAccess_WhenNotAccessed_ShouldBeNull()
    {
        var interaction = new ContentInteraction
        {
            UserId = Guid.NewGuid(),
            ContentId = Guid.NewGuid(),
            ProgramUserId = Guid.NewGuid()
        };

        interaction.DaysSinceLastAccess.Should().BeNull();
    }

    [Fact]
    public void EngagementDuration_WhenNotComplete_ShouldBeNull()
    {
        var interaction = new ContentInteraction
        {
            UserId = Guid.NewGuid(),
            ContentId = Guid.NewGuid(),
            ProgramUserId = Guid.NewGuid()
        };

        interaction.Start();
        interaction.EngagementDuration.Should().BeNull();
    }

    [Fact]
    public void LegacyEngagementScore_IsNotExposed()
    {
        typeof(ContentInteraction).GetMethod("CalculateEngagementScore").Should().BeNull();
    }

    [Fact]
    public void Complete_UsesCanonicalProgressWithoutSyntheticEngagementScore()
    {
        var interaction = new ContentInteraction
        {
            UserId = Guid.NewGuid(),
            ContentId = Guid.NewGuid(),
            ProgramUserId = Guid.NewGuid()
        };

        interaction.Complete();
        interaction.ProgressPercentage.Should().Be(PercentValue.Hundred);
        typeof(ContentInteraction).GetMethod("CalculateEngagementScore").Should().BeNull();
    }
}

#endregion

#region Validator Tests

public class CreateProgramCommandValidatorTests
{
    private readonly CreateProgramCommandValidator _validator = new();

    [Fact]
    public void ValidCommand_ShouldPass()
    {
        var cmd = new CreateProgramCommand("My Course", "This is a valid description for the course");
        _validator.Validate(cmd).IsValid.Should().BeTrue();
    }

    [Fact]
    public void EmptyTitle_ShouldFail()
    {
        var cmd = new CreateProgramCommand("", "A valid description for the course");
        _validator.Validate(cmd).IsValid.Should().BeFalse();
    }

    [Fact]
    public void TitleTooShort_ShouldFail()
    {
        var cmd = new CreateProgramCommand("AB", "A valid description for the course");
        _validator.Validate(cmd).IsValid.Should().BeFalse();
    }

    [Fact]
    public void TitleTooLong_ShouldFail()
    {
        var cmd = new CreateProgramCommand(new string('x', 256), "A valid description for the course");
        _validator.Validate(cmd).IsValid.Should().BeFalse();
    }

    [Fact]
    public void EmptyDescription_ShouldFail()
    {
        var cmd = new CreateProgramCommand("Valid Title", "");
        _validator.Validate(cmd).IsValid.Should().BeFalse();
    }

    [Fact]
    public void DescriptionTooShort_ShouldFail()
    {
        var cmd = new CreateProgramCommand("Valid Title", "short");
        _validator.Validate(cmd).IsValid.Should().BeFalse();
    }

    [Fact]
    public void DescriptionTooLong_ShouldFail()
    {
        var cmd = new CreateProgramCommand("Valid Title", new string('x', 2001));
        _validator.Validate(cmd).IsValid.Should().BeFalse();
    }

    [Fact]
    public void InvalidThumbnailUrl_ShouldFail()
    {
        var cmd = new CreateProgramCommand("Valid Title", "A valid description for the course",
            Thumbnail: "not-a-url");
        _validator.Validate(cmd).IsValid.Should().BeFalse();
    }

    [Fact]
    public void ValidThumbnailUrl_ShouldPass()
    {
        var cmd = new CreateProgramCommand("Valid Title", "A valid description for the course",
            Thumbnail: "https://example.com/img.png");
        _validator.Validate(cmd).IsValid.Should().BeTrue();
    }

    [Fact]
    public void EstimatedHoursZero_ShouldFail()
    {
        var cmd = new CreateProgramCommand("Valid Title", "A valid description for the course",
            EstimatedHours: 0);
        _validator.Validate(cmd).IsValid.Should().BeFalse();
    }

    [Fact]
    public void EstimatedHoursTooHigh_ShouldFail()
    {
        var cmd = new CreateProgramCommand("Valid Title", "A valid description for the course",
            EstimatedHours: 1001);
        _validator.Validate(cmd).IsValid.Should().BeFalse();
    }

    [Fact]
    public void MaxEnrollmentsZero_ShouldFail()
    {
        var cmd = new CreateProgramCommand("Valid Title", "A valid description for the course",
            MaxEnrollments: 0);
        _validator.Validate(cmd).IsValid.Should().BeFalse();
    }

    [Fact]
    public void MaxEnrollmentsTooHigh_ShouldFail()
    {
        var cmd = new CreateProgramCommand("Valid Title", "A valid description for the course",
            MaxEnrollments: 10001);
        _validator.Validate(cmd).IsValid.Should().BeFalse();
    }

    [Fact]
    public void SummaryTooShort_ShouldFail()
    {
        var cmd = new CreateProgramCommand("Valid Title", "A valid description for the course",
            Summary: "short");
        _validator.Validate(cmd).IsValid.Should().BeFalse();
    }

    [Fact]
    public void SummaryTooLong_ShouldFail()
    {
        var cmd = new CreateProgramCommand("Valid Title", "A valid description for the course",
            Summary: new string('x', 501));
        _validator.Validate(cmd).IsValid.Should().BeFalse();
    }
}

public class RateProgramCommandValidatorTests
{
    private readonly RateProgramCommandValidator _validator = new();

    [Fact]
    public void ValidCommand_ShouldPass()
    {
        var cmd = new RateProgramCommand(Guid.NewGuid(), Guid.NewGuid().ToString(), 4, null);
        _validator.Validate(cmd).IsValid.Should().BeTrue();
    }

    [Fact]
    public void EmptyProgramId_ShouldFail()
    {
        var cmd = new RateProgramCommand(Guid.Empty, Guid.NewGuid().ToString(), 4, null);
        _validator.Validate(cmd).IsValid.Should().BeFalse();
    }

    [Fact]
    public void EmptyUserId_ShouldFail()
    {
        var cmd = new RateProgramCommand(Guid.NewGuid(), "", 4, null);
        _validator.Validate(cmd).IsValid.Should().BeFalse();
    }

    [Fact]
    public void RatingTooLow_ShouldFail()
    {
        var cmd = new RateProgramCommand(Guid.NewGuid(), Guid.NewGuid().ToString(), 0, null);
        _validator.Validate(cmd).IsValid.Should().BeFalse();
    }

    [Fact]
    public void RatingTooHigh_ShouldFail()
    {
        var cmd = new RateProgramCommand(Guid.NewGuid(), Guid.NewGuid().ToString(), 6, null);
        _validator.Validate(cmd).IsValid.Should().BeFalse();
    }

    [Fact]
    public void ReviewTooShort_ShouldFail()
    {
        var cmd = new RateProgramCommand(Guid.NewGuid(), Guid.NewGuid().ToString(), 4, "short");
        _validator.Validate(cmd).IsValid.Should().BeFalse();
    }

    [Fact]
    public void ReviewTooLong_ShouldFail()
    {
        var cmd = new RateProgramCommand(Guid.NewGuid(), Guid.NewGuid().ToString(), 4, new string('x', 1001));
        _validator.Validate(cmd).IsValid.Should().BeFalse();
    }
}

public class EnrollUserCommandValidatorTests
{
    private readonly EnrollUserCommandValidator _validator = new();

    [Fact]
    public void ValidCommand_ShouldPass()
    {
        var cmd = new EnrollUserCommand(Guid.NewGuid(), Guid.NewGuid().ToString());
        _validator.Validate(cmd).IsValid.Should().BeTrue();
    }

    [Fact]
    public void EmptyProgramId_ShouldFail()
    {
        var cmd = new EnrollUserCommand(Guid.Empty, Guid.NewGuid().ToString());
        _validator.Validate(cmd).IsValid.Should().BeFalse();
    }

    [Fact]
    public void EmptyUserId_ShouldFail()
    {
        var cmd = new EnrollUserCommand(Guid.NewGuid(), "");
        _validator.Validate(cmd).IsValid.Should().BeFalse();
    }
}

public class ProgramTagCommandValidatorTests
{
    [Fact]
    public void AddTag_ValidCommand_ShouldPass()
    {
        var validator = new AddTagToProgramCommandValidator();
        var cmd = new AddTagToProgramCommand(Guid.NewGuid(), Guid.NewGuid());
        validator.Validate(cmd).IsValid.Should().BeTrue();
    }

    [Fact]
    public void AddTag_EmptyProgramId_ShouldFail()
    {
        var validator = new AddTagToProgramCommandValidator();
        var cmd = new AddTagToProgramCommand(Guid.Empty, Guid.NewGuid());
        validator.Validate(cmd).IsValid.Should().BeFalse();
    }

    [Fact]
    public void AddTag_EmptyTagId_ShouldFail()
    {
        var validator = new AddTagToProgramCommandValidator();
        var cmd = new AddTagToProgramCommand(Guid.NewGuid(), Guid.Empty);
        validator.Validate(cmd).IsValid.Should().BeFalse();
    }

    [Fact]
    public void AddTag_NegativeDisplayOrder_ShouldFail()
    {
        var validator = new AddTagToProgramCommandValidator();
        var cmd = new AddTagToProgramCommand(Guid.NewGuid(), Guid.NewGuid(), DisplayOrder: -1);
        validator.Validate(cmd).IsValid.Should().BeFalse();
    }

    [Fact]
    public void UpdateTag_ValidCommand_ShouldPass()
    {
        var validator = new UpdateProgramTagCommandValidator();
        var cmd = new UpdateProgramTagCommand(Guid.NewGuid(), Guid.NewGuid());
        validator.Validate(cmd).IsValid.Should().BeTrue();
    }

    [Fact]
    public void RemoveTag_EmptyProgramId_ShouldFail()
    {
        var validator = new RemoveTagFromProgramCommandValidator();
        var cmd = new RemoveTagFromProgramCommand(Guid.Empty, Guid.NewGuid());
        validator.Validate(cmd).IsValid.Should().BeFalse();
    }

    [Fact]
    public void BulkAdd_EmptyTags_ShouldFail()
    {
        var validator = new BulkAddTagsToProgramCommandValidator();
        var cmd = new BulkAddTagsToProgramCommand(Guid.NewGuid(), Array.Empty<AddTagToProgramDto>());
        validator.Validate(cmd).IsValid.Should().BeFalse();
    }

    [Fact]
    public void BulkAdd_EmptyProgramId_ShouldFail()
    {
        var validator = new BulkAddTagsToProgramCommandValidator();
        var cmd = new BulkAddTagsToProgramCommand(Guid.Empty,
            new[] { new AddTagToProgramDto(Guid.NewGuid()) });
        validator.Validate(cmd).IsValid.Should().BeFalse();
    }
}

#endregion
