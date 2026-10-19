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
    public async Task PartialCredit_MatchesTheSharedVersionedVectorsAndRejectsInvalidAnswers()
    {
        using var fixture = JsonDocument.Parse(ReadFixture("quiz-partial-credit-v1.json"));
        fixture.RootElement.GetProperty("schemaVersion").GetInt32().Should().Be(1);
        var projector = new QuizItemProjector();
        var projections = fixture.RootElement.GetProperty("items")
            .EnumerateArray()
            .ToDictionary(
                item => item.GetProperty("itemId").GetString()!,
                item => projector.Project(
                    item.GetProperty("itemId").GetString()!,
                    item.GetProperty("entry")),
                StringComparer.Ordinal);

        projections["matching-partial"].GetProperty("partialCreditAlgorithm").GetString()
            .Should().Be(QuizAdapterContracts.MatchingPartialCreditAlgorithm);
        projections["ordering-partial"].GetProperty("partialCreditAlgorithm").GetString()
            .Should().Be(QuizAdapterContracts.OrderingPartialCreditAlgorithm);
        projections["matching-all-or-nothing"].TryGetProperty("partialCreditAlgorithm", out _)
            .Should().BeFalse();

        var decoder = new QuizAnswerDecoder();
        var algorithm = new QuizDeterministicReviewAlgorithm();
        foreach (var testCase in fixture.RootElement.GetProperty("scoreCases").EnumerateArray())
        {
            var itemId = testCase.GetProperty("itemId").GetString()!;
            var envelope = EnvelopeFor(itemId, testCase.GetProperty("answer"));
            var decoded = decoder.Decode(envelope, [projections[itemId]]);
            var result = await algorithm.EvaluateAsync(
                new DeterministicReviewRequest(
                    [projections[itemId]],
                    EmptyDelivery(),
                    decoded,
                    QuizAdapterContracts.AutomatedReviewHandlerKey,
                    QuizAdapterContracts.Version),
                CancellationToken.None);

            result.Score.Should().Be(
                ScoreValue.FromUnits(testCase.GetProperty("expectedScore").GetInt32()),
                testCase.GetProperty("name").GetString());
        }

        foreach (var testCase in fixture.RootElement.GetProperty("invalidCases").EnumerateArray())
        {
            var itemId = testCase.GetProperty("itemId").GetString()!;
            var decode = () => decoder.Decode(
                EnvelopeFor(itemId, testCase.GetProperty("answer")),
                [projections[itemId]]);

            decode.Should().Throw<JsonException>(testCase.GetProperty("name").GetString());
        }
    }

    [Fact]
    public async Task ValidationMatrix_MatchesCapabilitiesScoresAndUnresolvedStatesAcrossAllQuizTypes()
    {
        using var fixture = JsonDocument.Parse(ReadFixture("quiz-validation-cases-v1.json"));
        fixture.RootElement.GetProperty("schemaVersion").GetInt32().Should().Be(1);
        var cases = fixture.RootElement.GetProperty("cases").EnumerateArray().ToArray();
        cases.Should().HaveCount(18);
        cases.Where(testCase => testCase.GetProperty("baseType").GetBoolean())
            .Select(testCase => testCase.GetProperty("quizEntryType").GetString())
            .Should().OnlyHaveUniqueItems().And.HaveCount(14);

        var projector = new QuizItemProjector();
        var deliveryGenerator = new QuizDeliveryGenerator();
        var decoder = new QuizAnswerDecoder();
        var algorithm = new QuizDeterministicReviewAlgorithm();
        var adapter = new QuizAssessmentTypeAdapter(
            new QuizAuthoringAdapter(projector),
            deliveryGenerator,
            decoder,
            algorithm);

        foreach (var testCase in cases)
        {
            var caseId = testCase.GetProperty("id").GetString();
            var entry = testCase.GetProperty("entry");
            var projection = projector.Project("q1", entry);
            var automated = testCase.GetProperty("officialCapability").GetString() == "automated-review";
            adapter.CanEvaluateDeterministically(projection).Should().Be(automated, caseId);

            var delivery = deliveryGenerator.Generate(projection).GetRawText();
            foreach (var privateField in PrivateAuthoringFields(testCase.GetProperty("quizEntryType").GetString()!))
            {
                delivery.Should().NotContain($"\"{privateField}\"", $"{caseId} learner delivery must be redacted");
            }

            if (testCase.GetProperty("quizEntryType").GetString() == "NUMERIC")
            {
                delivery.Should().Contain("\"formula\"", $"{caseId} formula is part of the numeric learner prompt");
            }

            await AssertVector(
                testCase,
                "correct",
                projection,
                decoder,
                algorithm,
                caseId!);
            await AssertVector(
                testCase,
                "omitted",
                projection,
                decoder,
                algorithm,
                caseId!);
            if (testCase.GetProperty("answers").GetProperty("incorrect").ValueKind != JsonValueKind.Null)
            {
                await AssertVector(
                    testCase,
                    "incorrect",
                    projection,
                    decoder,
                    algorithm,
                    caseId!);
            }
            if (testCase.GetProperty("answers").GetProperty("partial").ValueKind != JsonValueKind.Null)
            {
                await AssertVector(
                    testCase,
                    "partial",
                    projection,
                    decoder,
                    algorithm,
                    caseId!);
            }
        }
    }

    [Fact]
    public void Runtime_RegistersQuizAdapterAndAutomatedReviewForBothContexts()
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
            .Should().NotBeNull();
        registry.ResolveReview(ReviewMethod.AutomatedReview, "quiz-automated-review", "1", ReviewExecutionContext.OfficialSubmission)
            .Should().NotBeNull();
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
        services.Should().ContainSingle(descriptor =>
            descriptor.ServiceType == typeof(IProgramContentDeleteParticipant) &&
            descriptor.ImplementationType == typeof(QuizProgramContentDeleteParticipant) &&
            descriptor.Lifetime == ServiceLifetime.Scoped);
        services.Should().ContainSingle(descriptor =>
            descriptor.ServiceType == typeof(IProgramContentPublicationParticipant) &&
            descriptor.ImplementationType == typeof(QuizProgramContentPublicationParticipant) &&
            descriptor.Lifetime == ServiceLifetime.Scoped);
        using var provider = services.BuildServiceProvider();
        var resolver = provider.GetRequiredService<IAssessmentTypeAdapterResolver>();

        resolver.ResolveForAuthoring(ProgramContentType.Questionnaire)
            .Should().BeOfType<QuizAssessmentTypeAdapter>()
            .Which.IsCurrentForAuthoring.Should().BeTrue();
        resolver.Resolve("quiz", QuizAdapterContracts.AdapterKey, "1", ReviewExecutionContext.AuthorTest)
            .Should().BeOfType<QuizAssessmentTypeAdapter>();
        resolver.Resolve("quiz", QuizAdapterContracts.AdapterKey, "1", ReviewExecutionContext.OfficialSubmission)
            .Should().BeOfType<QuizAssessmentTypeAdapter>();
        provider.GetRequiredService<IReviewStageHandlerResolver>()
            .Resolve(ReviewMethod.AutomatedReview, "quiz-automated-review", "1", ReviewExecutionContext.AuthorTest)
            .Should().BeOfType<QuizAutomatedReviewStageHandler>();
        provider.GetRequiredService<IReviewStageHandlerResolver>()
            .Resolve(ReviewMethod.AutomatedReview, "quiz-automated-review", "1", ReviewExecutionContext.OfficialSubmission)
            .Should().BeOfType<QuizAutomatedReviewStageHandler>();
        var wrongVersion = () => resolver.Resolve(
            "quiz", QuizAdapterContracts.AdapterKey, "2", ReviewExecutionContext.AuthorTest);
        wrongVersion.Should().Throw<InvalidOperationException>().WithMessage("*unavailable*");
    }

    private static AssessmentResponseEnvelopeV1 Envelope(string payload)
    {
        using var document = JsonDocument.Parse(payload);
        return new AssessmentResponseEnvelopeV1(1, "quiz", "quiz-answer/v1", document.RootElement.Clone());
    }

    private static AssessmentResponseEnvelopeV1 EnvelopeFor(string itemId, JsonElement answer) =>
        new(
            1,
            QuizAdapterContracts.ContentType,
            QuizAdapterContracts.AnswerPayloadSchema,
            JsonSerializer.SerializeToElement(new
            {
                answers = new Dictionary<string, JsonElement>(StringComparer.Ordinal)
                {
                    [itemId] = answer.Clone(),
                },
            }, GradingJson.Options));

    private static AssessmentExecutionDeliveryV1 EmptyDelivery() => new(
        1,
        Guid.NewGuid(),
        "snapshot",
        [],
        new Dictionary<string, AssessmentExecutionDeliveryItemV1>());

    private static async Task AssertVector(
        JsonElement testCase,
        string vector,
        JsonElement projection,
        QuizAnswerDecoder decoder,
        QuizDeterministicReviewAlgorithm algorithm,
        string caseId)
    {
        var answers = testCase.GetProperty("answers");
        var answer = vector == "omitted" ? (JsonElement?)null : answers.GetProperty(vector);
        var envelope = EnvelopeForAnswers(answer);
        var decoded = decoder.Decode(envelope, [projection]);
        var result = await algorithm.EvaluateAsync(
            new DeterministicReviewRequest(
                [projection],
                EmptyDelivery(),
                decoded,
                QuizAdapterContracts.AutomatedReviewHandlerKey,
                QuizAdapterContracts.Version),
            CancellationToken.None);
        var expected = testCase.GetProperty("expected").GetProperty(vector);
        result.State.Should().Be(expected.GetProperty("resultState").GetString(), $"{caseId}/{vector}");
        result.Items.Should().ContainSingle();
        result.Items[0].State.ToString().ToLowerInvariant()
            .Should().Be(expected.GetProperty("itemState").GetString(), $"{caseId}/{vector}");
        var expectedScore = expected.GetProperty("score");
        if (expectedScore.ValueKind == JsonValueKind.Null)
        {
            result.Score.Should().BeNull($"{caseId}/{vector}");
            result.Items[0].Score.Should().BeNull($"{caseId}/{vector}");
        }
        else
        {
            result.Score.Should().Be(ScoreValue.FromUnits(expectedScore.GetInt32()), $"{caseId}/{vector}");
            result.Items[0].Score.Should().Be(ScoreValue.FromUnits(expectedScore.GetInt32()), $"{caseId}/{vector}");
        }
    }

    private static AssessmentResponseEnvelopeV1 EnvelopeForAnswers(JsonElement? answer) =>
        new(
            1,
            QuizAdapterContracts.ContentType,
            QuizAdapterContracts.AnswerPayloadSchema,
            JsonSerializer.SerializeToElement(new
            {
                answers = answer.HasValue
                    ? new Dictionary<string, JsonElement>(StringComparer.Ordinal) { ["q1"] = answer.Value.Clone() }
                    : new Dictionary<string, JsonElement>(StringComparer.Ordinal),
            }, GradingJson.Options));

    private static string[] PrivateAuthoringFields(string itemType) => itemType switch
    {
        "SINGLE_CHOICE" => ["correctOptionId"],
        "MULTIPLE_CHOICE" => ["correctOptionIds"],
        "TRUE_FALSE" => ["correctAnswer"],
        "FILL_IN_THE_BLANK" => ["acceptedAnswers", "correctValue"],
        "SHORT_ANSWER" => ["acceptedAnswers"],
        "ESSAY" => ["correctAnswer", "correctAnswerPlain"],
        "MATCHING" => ["right"],
        "ORDERING" => ["correctPosition"],
        "CATEGORIZATION" => ["correctCategoryIds"],
        "RATING" => ["correctRating"],
        "NUMERIC" => ["toleranceType", "tolerance"],
        "FORMULA" => ["formula", "toleranceType", "tolerance"],
        "HOTSPOT" => ["hotspots"],
        "HIGHLIGHT" => ["sourceText", "highlights"],
        _ => [],
    };

    private static string ReadFixture(string name = "quiz-answer-envelope-v1.json")
    {
        using var stream = Assembly.GetExecutingAssembly()
            .GetManifestResourceStream($"GameGuild.Grading.Fixtures.{name}")
            ?? throw new InvalidOperationException("Shared quiz answer fixture was not embedded.");
        using var reader = new StreamReader(stream, Encoding.UTF8);
        return reader.ReadToEnd();
    }
}
