using FluentAssertions;
using GameGuild.Learning.Assessments.QuizAdapter;
using GameGuild.Learning.Courses;
using Xunit;

namespace GameGuild.Learning.Assessments.Tests;

public sealed class QuizProgramContentBoundarySecurityTests
{
    private const string GradedDocument = """
        {"schemaVersion":1,"order":[["q1","quiz"]],"blocks":{"q1":{"type":"TRUE_FALSE","stem":"Question","points":200,"correctAnswer":true,"settings":{"allowRetry":false}}},"grading":{"schemaVersion":2,"items":{"q1":{}}}}
        """;

    [Theory]
    [InlineData(ProgramContentType.Questionnaire)]
    [InlineData(ProgramContentType.Code)]
    [InlineData(ProgramContentType.Assignment)]
    [InlineData(ProgramContentType.Lesson)]
    public void GradedPayload_CannotOptOutOfTheAcademicWorkflowByChangingItsType(ProgramContentType type)
    {
        var content = new ProgramContent { Type = type, JsonBody = GradedDocument };

        CreateBoundary().GetRejection(content, ProgramContentAcademicMutation.Authoring)
            .Should().NotBeNull();
    }

    [Fact]
    public void OrdinaryStructuredLesson_StillAllowsGenericAuthoring()
    {
        var content = new ProgramContent { Type = ProgramContentType.Lesson, JsonBody = """{"root":{"children":[]}}""" };

        CreateBoundary().GetRejection(content, ProgramContentAcademicMutation.Authoring)
            .Should().BeNull();
    }

    [Fact]
    public void TypeMaskedQuizShapedDocument_WithoutGrading_StillAllowsGenericAuthoring()
    {
        var content = new ProgramContent
        {
            Type = ProgramContentType.Assignment,
            JsonBody = """{"schemaVersion":1,"order":[["q1","quiz"]],"blocks":{"q1":{"type":"TRUE_FALSE","stem":"Q","points":100,"correctAnswer":true,"settings":{"allowRetry":false}}}}"""
        };

        CreateBoundary().GetRejection(content, ProgramContentAcademicMutation.Authoring)
            .Should().BeNull("a valid ungraded quiz document is not reserved by the grading workflow");
    }

    [Theory]
    [InlineData(ProgramContentType.Assignment)]
    [InlineData(ProgramContentType.Code)]
    [InlineData(ProgramContentType.Lesson)]
    public void TypeMaskedQuizDocument_WithCaseVariants_IsNotQuizShaped(ProgramContentType type)
    {
        var content = new ProgramContent { Type = type, JsonBody = """{"SchemaVersion":1,"Order":[],"Blocks":{},"Grading":{}}""" };

        CreateBoundary().GetRejection(content, ProgramContentAcademicMutation.Authoring)
            .Should().BeNull("PascalCase keys are not the quiz contract");
    }

    [Fact]
    public void InvalidAuthoringDocument_OnQuestionnaire_FailsClosed()
    {
        var content = new ProgramContent { Type = ProgramContentType.Questionnaire, JsonBody = """{"schemaVersion":1}""" };

        CreateBoundary().GetRejection(content, ProgramContentAcademicMutation.Authoring)
            .Should().NotBeNull("a Questionnaire with an invalid body cannot use generic academic mutations");
    }

    [Fact]
    public void QuizShapedDocument_WithInvalidContract_FailsClosedEvenWhenTypeMasked()
    {
        var content = new ProgramContent
        {
            Type = ProgramContentType.Assignment,
            JsonBody = """{"schemaVersion":2,"order":[["q1","quiz"]],"blocks":{"q1":{}}}"""
        };

        CreateBoundary().GetRejection(content, ProgramContentAcademicMutation.Authoring)
            .Should().NotBeNull("a quiz-shaped document with an invalid contract cannot use generic academic mutations");
    }

    private static QuizProgramContentBoundary CreateBoundary() => new(new QuizAssessmentTypeAdapter(
        new QuizAuthoringAdapter(new QuizItemProjector()),
        new QuizDeliveryGenerator(),
        new QuizAnswerDecoder(),
        new QuizDeterministicReviewAlgorithm()));
}
