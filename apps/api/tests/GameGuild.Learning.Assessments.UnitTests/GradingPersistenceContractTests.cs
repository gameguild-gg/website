using System.Text.Json;
using FluentAssertions;
using GameGuild.Learning.Assessments.Grading.Contracts;
using GameGuild.Learning.Assessments.Grading.Persistence;
using GameGuild.Learning.Grading.Contracts;
using Xunit;

namespace GameGuild.Learning.Assessments.Tests;

public sealed class GradingPersistenceContractTests
{
    private const string SnapshotHash = "1111111111111111111111111111111111111111111111111111111111111111";

    [Fact]
    public void DefinitionRevision_RequiresCanonicalBytesAndDerivesBothHashes()
    {
        var assessmentId = Guid.NewGuid();
        var actorId = Guid.NewGuid();

        var revision = AssessmentDefinitionRevision.Create(
            Guid.NewGuid(),
            assessmentId,
            1,
            "{\"content\":{},\"schemaVersion\":1}",
            "{\"manifest\":{},\"schemaVersion\":1}",
            actorId);

        revision.AssessmentId.Should().Be(assessmentId);
        revision.AuthoringSourceHash.Should().Be(CanonicalJson.Sha256(Parse("{\"content\":{},\"schemaVersion\":1}")));
        revision.ExecutionSnapshotHash.Should().Be(CanonicalJson.Sha256(Parse("{\"manifest\":{},\"schemaVersion\":1}")));

        var nonCanonical = () => AssessmentDefinitionRevision.Create(
            Guid.NewGuid(),
            assessmentId,
            2,
            "{ \"schemaVersion\": 1, \"content\": {} }",
            "{\"manifest\":{},\"schemaVersion\":1}",
            actorId);
        nonCanonical.Should().Throw<ArgumentException>().WithMessage("*canonical JSON representation*");
    }

    [Fact]
    public void Execution_OwnershipMatchesItsContext()
    {
        var revisionId = Guid.NewGuid();
        var subjectId = Guid.NewGuid();
        var submissionId = Guid.NewGuid();

        var authorTest = GradingExecution.CreateAuthorTest(Guid.NewGuid(), revisionId, subjectId);
        var official = GradingExecution.CreateOfficial(Guid.NewGuid(), revisionId, submissionId);

        authorTest.ExecutionContext.Should().Be(ReviewExecutionContext.AuthorTest);
        authorTest.TestRunSubjectId.Should().Be(subjectId);
        authorTest.AssessmentSubmissionId.Should().BeNull();
        official.ExecutionContext.Should().Be(ReviewExecutionContext.OfficialSubmission);
        official.TestRunSubjectId.Should().BeNull();
        official.AssessmentSubmissionId.Should().Be(submissionId);
    }

    [Fact]
    public void Delivery_IsBoundToTheRevisionAndImmutableByteForByte()
    {
        var revisionId = Guid.NewGuid();
        var execution = GradingExecution.CreateAuthorTest(Guid.NewGuid(), revisionId, Guid.NewGuid());
        var delivery = Delivery(revisionId, "Prompt");
        var canonical = Serialize(delivery);

        execution.MaterializeDelivery(delivery, canonical);
        execution.MaterializeDelivery(delivery, canonical);

        execution.DeliveryCanonicalJson.Should().Be(canonical);
        execution.DeliveryHash.Should().Be(CanonicalJson.Sha256(Parse(canonical)));
        execution.DeliveryHashVersion.Should().Be(GradingContractVersions.Hash);

        var divergent = Delivery(revisionId, "Changed prompt");
        var rewrite = () => execution.MaterializeDelivery(divergent, Serialize(divergent));
        rewrite.Should().Throw<InvalidOperationException>().WithMessage("*immutable*");

        var otherRevision = Delivery(Guid.NewGuid(), "Prompt");
        var wrongOwner = () => GradingExecution.CreateAuthorTest(Guid.NewGuid(), revisionId, Guid.NewGuid())
            .MaterializeDelivery(otherRevision, Serialize(otherRevision));
        wrongOwner.Should().Throw<InvalidOperationException>().WithMessage("*definition revision*");
    }

    [Fact]
    public void DeliveryAndResponse_RejectCanonicalBytesFromAnotherContract()
    {
        var revisionId = Guid.NewGuid();
        var execution = GradingExecution.CreateAuthorTest(Guid.NewGuid(), revisionId, Guid.NewGuid());
        var delivery = Delivery(revisionId, "Prompt");

        var mismatchedDelivery = () => execution.MaterializeDelivery(
            delivery,
            Serialize(Delivery(revisionId, "Different")));
        mismatchedDelivery.Should().Throw<ArgumentException>().WithMessage("*does not match*");

        var response = Response("first");
        var mismatchedResponse = () => execution.SaveResponseDraft(response, Serialize(Response("second")));
        mismatchedResponse.Should().Throw<ArgumentException>().WithMessage("*does not match*");
    }

    [Fact]
    public void SubmittedResponse_IsImmutableWhileIdenticalDraftWritesAreStable()
    {
        var execution = GradingExecution.CreateAuthorTest(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        var response = Response("answer");
        var canonical = Serialize(response);

        execution.SaveResponseDraft(response, canonical);
        var originalHash = execution.ResponseHash;
        execution.SaveResponseDraft(response, canonical);
        execution.Submit(DateTime.UtcNow);

        execution.ResponseEnvelopeCanonicalJson.Should().Be(canonical);
        execution.ResponseHash.Should().Be(originalHash);
        execution.Status.Should().Be(PersistedGradingExecutionStatus.Running);

        var rewrite = () => execution.SaveResponseDraft(Response("changed"), Serialize(Response("changed")));
        rewrite.Should().Throw<InvalidOperationException>().WithMessage("*immutable*");
    }

    private static AssessmentExecutionDeliveryV1 Delivery(Guid revisionId, string prompt) => new(
        GradingContractVersions.ExecutionDelivery,
        revisionId,
        SnapshotHash,
        ["q1"],
        new Dictionary<string, AssessmentExecutionDeliveryItemV1>
        {
            ["q1"] = new("quiz-delivery-generator", "1", Parse($"{{\"prompt\":{JsonSerializer.Serialize(prompt)}}}")),
        });

    private static AssessmentResponseEnvelopeV1 Response(string value) => new(
        GradingContractVersions.ResponseEnvelope,
        "quiz",
        "quiz-answer/v1",
        Parse($"{{\"answers\":{{\"q1\":{JsonSerializer.Serialize(value)}}}}}"));

    private static JsonElement Parse(string value)
    {
        using var document = JsonDocument.Parse(value);
        return document.RootElement.Clone();
    }

    private static string Serialize<T>(T value) =>
        CanonicalJson.Serialize(JsonSerializer.SerializeToElement(value, GradingJson.Options));
}
