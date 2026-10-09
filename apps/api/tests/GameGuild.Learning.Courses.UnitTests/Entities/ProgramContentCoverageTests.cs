using FluentAssertions;
using Xunit;

namespace GameGuild.Learning.Courses.UnitTests.Entities;

public sealed class ProgramContentCoverageTests
{
    [Fact]
    public void ComputedHierarchyProperties_HandleTenantChildrenAndParents()
    {
        var root = new ProgramContent { Title = "Module" };
        var child = new ProgramContent { Title = "Lesson", Parent = root };

        root.IsGlobal.Should().BeTrue();
        root.ChildCount.Should().Be(0);
        root.HasChildren.Should().BeFalse();
        child.FullPath.Should().Be("Module > Lesson");

        root.Children = [child];
        root.ChildCount.Should().Be(1);
        root.HasChildren.Should().BeTrue();

        root.Children = null!;
        root.ChildCount.Should().Be(0);
        root.TenantId = Guid.NewGuid();
        root.IsGlobal.Should().BeFalse();
    }

    [Theory]
    [InlineData(ProgramContentType.Page, ProgramContentType.Lesson)]
    [InlineData(ProgramContentType.Challenge, ProgramContentType.Assignment)]
    public void NormalizeLearningContract_MapsLegacyTypesAndCreatesSlug(
        ProgramContentType legacyType,
        ProgramContentType expectedType)
    {
        var content = new ProgramContent
        {
            Title = "Legacy Content",
            Slug = " ",
            Type = legacyType,
            Body = "A short body",
            JsonBody = "{\"root\":{}}"
        };

        content.NormalizeLearningContract();

        content.Type.Should().Be(expectedType);
        content.Slug.Should().Be("legacy-content");
        if (expectedType == ProgramContentType.Lesson)
        {
            content.JsonBody.Should().BeNull();
        }
        else
        {
            content.JsonBody.Should().BeNull();
        }
    }

    [Fact]
    public void NormalizeLearningContract_WhenLessonFormatIsMissing_InfersAndRoutesBody()
    {
        var content = new ProgramContent
        {
            Title = "Markdown lesson",
            Type = ProgramContentType.Lesson,
            LessonFormat = null,
            Body = "# Heading",
            JsonBody = "{\"stale\":true}"
        };

        content.NormalizeLearningContract();

        content.LessonFormat.Should().Be(LessonContentFormat.Markdown);
        content.Body.Should().Be("# Heading");
        content.JsonBody.Should().BeNull();
    }

    [Fact]
    public void NormalizeLearningContract_ForLexicalLesson_PreservesStructuredBodyOnly()
    {
        var content = new ProgramContent
        {
            Title = "Structured lesson",
            Type = ProgramContentType.Lesson,
            LessonFormat = LessonContentFormat.Lexical,
            Body = "stale markdown",
            JsonBody = "{\"root\":{}}"
        };

        content.NormalizeLearningContract();

        content.Body.Should().BeNull();
        content.JsonBody.Should().Be("{\"root\":{}}");
    }

    [Theory]
    [InlineData(ProgramContentType.Questionnaire)]
    [InlineData(ProgramContentType.Code)]
    [InlineData(ProgramContentType.Project)]
    public void NormalizeLearningContract_ForStructuredContent_PreservesJsonBodyOnly(ProgramContentType type)
    {
        var content = new ProgramContent
        {
            Title = "Structured content",
            Type = type,
            Body = "stale text",
            JsonBody = "{\"content\":true}"
        };

        content.NormalizeLearningContract();

        content.Body.Should().BeNull();
        content.JsonBody.Should().Be("{\"content\":true}");
        content.ActivitySettingsData.Should().BeNull();
    }

    [Fact]
    public void NormalizeLearningContract_ForActivity_InitializesAndRoundTripsDefaultSettings()
    {
        var content = new ProgramContent
        {
            Title = "Discussion",
            Type = ProgramContentType.Discussion,
            Body = "Discuss this topic"
        };

        content.NormalizeLearningContract();

        content.GetActivitySettings().Should().Be(new DiscussionActivitySettings());
        content.ActivitySettingsData.Should().NotBeNullOrWhiteSpace();
        content.JsonBody.Should().BeNull();
        content.UpdatedAt.Should().NotBe(default);
    }

    [Fact]
    public void NormalizeLearningContract_WhenLessonFormatIsInvalid_RejectsContent()
    {
        var content = new ProgramContent
        {
            Type = ProgramContentType.Lesson,
            LessonFormat = (LessonContentFormat)int.MaxValue
        };

        var act = () => content.NormalizeLearningContract();

        act.Should().Throw<ArgumentOutOfRangeException>().WithParameterName(nameof(ProgramContent.LessonFormat));
    }

    [Fact]
    public void IsAccessibleBy_HandlesPublicInternalAndRestrictedContent()
    {
        var userId = Guid.NewGuid();
        var content = new ProgramContent { Visibility = Visibility.Public };
        content.IsAccessibleBy(userId).Should().BeTrue();

        content.Visibility = Visibility.Internal;
        content.Program = null!;
        content.IsAccessibleBy(userId).Should().BeFalse();

        content.Program = new Program { ProgramUsers = null! };
        content.IsAccessibleBy(userId).Should().BeFalse();

        content.Program.ProgramUsers =
        [
            new ProgramUser { UserId = userId, IsActive = false },
            new ProgramUser { UserId = Guid.NewGuid(), IsActive = true }
        ];
        content.IsAccessibleBy(userId).Should().BeFalse();

        content.Program.ProgramUsers.Add(new ProgramUser { UserId = userId, IsActive = true });
        content.IsAccessibleBy(userId).Should().BeTrue();

        content.Visibility = Visibility.Private;
        content.IsAccessibleBy(userId).Should().BeFalse();
    }

    [Fact]
    public void GetCompletionPercentage_WhenInteractionsAreUnavailable_ReturnsZero()
    {
        var content = new ProgramContent { ContentInteractions = null! };
        content.GetCompletionPercentage(Guid.NewGuid()).Should().Be(PercentValue.Zero);

        content.ContentInteractions = [];
        content.GetCompletionPercentage(Guid.NewGuid()).Should().Be(PercentValue.Zero);
    }

    [Fact]
    public void GetCompletionPercentage_ForLeaf_UsesLatestMatchingInteraction()
    {
        var userId = Guid.NewGuid();
        var content = new ProgramContent
        {
            Children = null!,
            ContentInteractions =
            [
                new ContentInteraction
                {
                    UserId = userId,
                    ProgressPercentage = Percent(25),
                    UpdatedAt = SystemClock.UtcNow.AddMinutes(-2)
                },
                new ContentInteraction
                {
                    UserId = userId,
                    ProgressPercentage = null,
                    UpdatedAt = SystemClock.UtcNow.AddMinutes(-1)
                },
                new ContentInteraction { UserId = Guid.NewGuid(), ProgressPercentage = Percent(100) }
            ]
        };

        content.GetCompletionPercentage(userId).Should().Be(PercentValue.Zero);

        content.ContentInteractions.Add(new ContentInteraction
        {
            UserId = userId,
            IsCompleted = true,
            UpdatedAt = SystemClock.UtcNow
        });
        content.GetCompletionPercentage(userId).Should().Be(PercentValue.Hundred);
    }

    [Fact]
    public void GetCompletionPercentage_ForParent_AveragesChildren()
    {
        var userId = Guid.NewGuid();
        var completed = new ProgramContent
        {
            ContentInteractions = [new ContentInteraction { UserId = userId, IsCompleted = true }]
        };
        var halfDone = new ProgramContent
        {
            ContentInteractions = [new ContentInteraction { UserId = userId, ProgressPercentage = Percent(50) }]
        };
        var parent = new ProgramContent
        {
            ContentInteractions = [new ContentInteraction { UserId = userId }],
            Children = [completed, halfDone]
        };

        parent.GetCompletionPercentage(userId).Should().Be(Percent(75));
    }

    [Fact]
    public void RecalculateEstimatedReadingTime_WhenEstimateIsManual_PreservesStoredValue()
    {
        var content = new ProgramContent
        {
            Type = ProgramContentType.Lesson,
            LessonFormat = LessonContentFormat.Markdown,
            Body = string.Join(' ', Enumerable.Repeat("word", 600)),
            EstimatedMinutes = 42,
            EstimatedMinutesSource = EstimatedMinutesSource.Manual
        };

        content.RecalculateEstimatedReadingTime();

        content.EstimatedMinutes.Should().Be(42);
    }
}
