using System.Reflection;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using GameGuild.Learning.Assessments;
using GameGuild.Learning.Assessments.Grading.Abstractions;
using GameGuild.Learning.Assessments.Grading.Capabilities;
using GameGuild.Learning.Assessments.Grading.Contracts;
using GameGuild.Learning.Assessments.QuizAdapter;
using GameGuild.Learning.Courses;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace GameGuild.Learning.Assessments.Tests;

public sealed class QuizGradingAdapterContractTests
{
    [Fact]
    public void Decoder_AcceptsTheSharedFixtureWithAllFourteenAnswerVariants()
    {
        var envelope = JsonSerializer.Deserialize<AssessmentResponseEnvelopeV1>(ReadFixture(), GradingJson.Options)!;
        var decoded = new QuizAnswerDecoder().Decode(envelope);

        decoded.GetProperty("answers").EnumerateObject().Should().HaveCount(14);
        decoded.GetProperty("answers").GetProperty("matching").GetProperty("matches")
            .GetProperty("left").GetString().Should().Be("right");
    }

    [Fact]
    public void Decoder_RejectsUnknownFieldsAndTextEncodedStructures()
    {
        var envelope = Envelope("""
            {"answers":{"matching":{"type":"MATCHING","matches":"left:right"}}}
            """);

        Action action = () => new QuizAnswerDecoder().Decode(envelope);

        action.Should().Throw<JsonException>();
    }

    [Fact]
    public void DeliveryGenerator_RemovesPrivateAnswerMaterial()
    {
        using var entry = JsonDocument.Parse("""
            {
              "type":"MATCHING",
              "stem":"Match",
              "points":200,
              "settings":{"allowRetry":false},
              "pairs":[{"id":"a","left":"A","right":"secret"}],
              "allowPartialCredit":true,
              "feedback":{"correct":"private","general":"visible"},
              "attachments":{"learnerVisible":[],"authorOnly":[{"assetUri":"asset:private","role":"answer"}]}
            }
            """);
        var projection = new QuizItemProjector().Project("q1", entry.RootElement);

        var delivery = new QuizDeliveryGenerator().Generate(projection);
        var serialized = delivery.GetRawText();

        var learnerEntry = delivery.GetProperty("entry");
        learnerEntry.GetProperty("pairs")[0].TryGetProperty("right", out _).Should().BeFalse();
        learnerEntry.GetProperty("attachments").TryGetProperty("authorOnly", out _).Should().BeFalse();
        learnerEntry.GetProperty("feedback").TryGetProperty("correct", out _).Should().BeFalse();
        serialized.Should().Contain("visible");
    }

    [Fact]
    public async Task Algorithm_PreservesPartialResultsAndExactPartialCredit()
    {
        using var trueFalse = JsonDocument.Parse("""
            {"type":"TRUE_FALSE","stem":"True","points":200,"correctAnswer":true,"settings":{"allowRetry":false}}
            """);
        using var matching = JsonDocument.Parse("""
            {"type":"MATCHING","stem":"Match","points":300,"pairs":[{"id":"a","left":"A","right":"1"},{"id":"b","left":"B","right":"2"},{"id":"c","left":"C","right":"3"}],"allowPartialCredit":true,"settings":{"allowRetry":false}}
            """);
        using var essay = JsonDocument.Parse("""
            {"type":"ESSAY","stem":"Explain","points":400,"settings":{"allowRetry":false}}
            """);
        using var answers = JsonDocument.Parse("""
            {"answers":{"true-false":{"type":"TRUE_FALSE","value":true},"matching":{"type":"MATCHING","matches":{"a":"1","b":"wrong","c":"3"}},"essay":{"type":"ESSAY","richText":null,"plainText":"Response"}}}
            """);
        var projector = new QuizItemProjector();
        var projections = new[]
        {
            projector.Project("true-false", trueFalse.RootElement),
            projector.Project("matching", matching.RootElement),
            projector.Project("essay", essay.RootElement),
        };
        var request = new DeterministicReviewRequest(
            projections,
            EmptyDelivery(),
            answers.RootElement.Clone(),
            "quiz-automated-review",
            "1");

        var result = await new QuizDeterministicReviewAlgorithm().EvaluateAsync(request, CancellationToken.None);

        result.State.Should().Be("partial");
        result.Score.Should().BeNull();
        result.MaxScore.Should().Be(ScoreValue.FromUnits(900));
        result.Items[0].Score.Should().Be(ScoreValue.FromUnits(200));
        result.Items[1].Score.Should().Be(ScoreValue.FromUnits(200));
        result.Items[2].State.Should().Be(GradeItemState.Pending);
    }

    [Fact]
    public void Foundation_RegistersStructuralComponentsButNoReviewHandler()
    {
        var registry = new ReviewCapabilityRegistry();
        new QuizCapabilityRegistration().Register(registry);

        registry.Resolve(
                ExecutableComponentKind.AssessmentTypeAdapter,
                QuizAdapterContracts.AdapterKey,
                QuizAdapterContracts.Version,
                ReviewExecutionContext.AuthorTest)
            .Should().NotBeNull();
        registry.ResolveReview(ReviewMethod.AutomatedReview, "quiz-automated-review", "1", ReviewExecutionContext.AuthorTest)
            .Should().BeNull();
        registry.ResolveReview(ReviewMethod.AutomatedReview, "quiz-automated-review", "1", ReviewExecutionContext.OfficialSubmission)
            .Should().BeNull();
    }

    [Fact]
    public void CoreAssembly_DoesNotReferenceTheQuizAdapter()
    {
        typeof(ReviewCapabilityRegistry).Assembly.GetReferencedAssemblies()
            .Select(reference => reference.Name)
            .Should().NotContain("GameGuild.Learning.Assessments.QuizAdapter");
        typeof(QuizItemProjector).Assembly.GetReferencedAssemblies()
            .Select(reference => reference.Name)
            .Should().Contain(typeof(ReviewCapabilityRegistry).Assembly.GetName().Name);
    }

    [Fact]
    public void Projector_RejectsAnIncompleteAuthoringEntryBeforeSnapshotPreparation()
    {
        using var entry = JsonDocument.Parse("""
            {"type":"TRUE_FALSE","stem":"Incomplete","settings":{"allowRetry":false}}
            """);

        var project = () => new QuizItemProjector().Project("q1", entry.RootElement);

        project.Should().Throw<JsonException>();
    }

    [Fact]
    public void AdapterResolver_UsesExactManifestKeyVersionAndContext()
    {
        var services = new ServiceCollection();
        services.AddAssessmentsModule();
        services.AddQuizGradingAdapter();
        using var provider = services.BuildServiceProvider();
        var resolver = provider.GetRequiredService<IAssessmentTypeAdapterResolver>();

        resolver.ResolveForAuthoring(ProgramContentType.Questionnaire)
            .Should().BeOfType<QuizAssessmentTypeAdapter>()
            .Which.IsCurrentForAuthoring.Should().BeTrue();
        resolver.Resolve("quiz", QuizAdapterContracts.AdapterKey, "1", ReviewExecutionContext.AuthorTest)
            .Should().BeOfType<QuizAssessmentTypeAdapter>();
        var absentHandler = () => provider.GetRequiredService<IReviewStageHandlerResolver>()
            .Resolve(ReviewMethod.AutomatedReview, "quiz-automated-review", "1", ReviewExecutionContext.AuthorTest);
        var wrongVersion = () => resolver.Resolve(
            "quiz", QuizAdapterContracts.AdapterKey, "2", ReviewExecutionContext.AuthorTest);
        var wrongContext = () => resolver.Resolve(
            "quiz", QuizAdapterContracts.AdapterKey, "1", ReviewExecutionContext.OfficialSubmission);
        wrongVersion.Should().Throw<InvalidOperationException>().WithMessage("*unavailable*");
        wrongContext.Should().Throw<InvalidOperationException>().WithMessage("*unavailable*");
        absentHandler.Should().Throw<InvalidOperationException>().WithMessage("*unavailable*");
    }

    private static AssessmentResponseEnvelopeV1 Envelope(string payload)
    {
        using var document = JsonDocument.Parse(payload);
        return new AssessmentResponseEnvelopeV1(1, "quiz", "quiz-answer/v1", document.RootElement.Clone());
    }

    private static AssessmentExecutionDeliveryV1 EmptyDelivery() => new(
        1,
        Guid.NewGuid(),
        "snapshot",
        [],
        new Dictionary<string, AssessmentExecutionDeliveryItemV1>());

    private static string ReadFixture()
    {
        using var stream = Assembly.GetExecutingAssembly()
            .GetManifestResourceStream("GameGuild.Grading.Fixtures.quiz-answer-envelope-v1.json")
            ?? throw new InvalidOperationException("Shared quiz answer fixture was not embedded.");
        using var reader = new StreamReader(stream, Encoding.UTF8);
        return reader.ReadToEnd();
    }
}
