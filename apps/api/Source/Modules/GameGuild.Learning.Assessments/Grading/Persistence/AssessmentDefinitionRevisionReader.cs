using System.Text.Json;
using GameGuild.Learning.Assessments.Grading.Contracts;

namespace GameGuild.Learning.Assessments.Grading.Persistence;

internal static class AssessmentDefinitionRevisionReader
{
    public static AssessmentExecutionSnapshotV1 ReadValidated(AssessmentDefinitionRevision revision)
    {
        ArgumentNullException.ThrowIfNull(revision);
        if (revision.SchemaVersion != 1)
            throw new InvalidOperationException($"Executable assessment revision {revision.Id} uses an unsupported schema version.");
        if (!string.Equals(revision.AuthoringSourceHashVersion, GradingContractVersions.Hash, StringComparison.Ordinal) ||
            !string.Equals(revision.ExecutionSnapshotHashVersion, GradingContractVersions.Hash, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Executable assessment revision {revision.Id} uses an unsupported hash version.");
        }

        var authoringCanonical = CanonicalPayload.Require(
            revision.AuthoringSourceCanonicalJson,
            4 * 1024 * 1024,
            "authoring source");
        var snapshotCanonical = CanonicalPayload.Require(
            revision.ExecutionSnapshotCanonicalJson,
            8 * 1024 * 1024,
            "execution snapshot");
        if (!string.Equals(CanonicalPayload.Hash(authoringCanonical), revision.AuthoringSourceHash, StringComparison.Ordinal) ||
            !string.Equals(CanonicalPayload.Hash(snapshotCanonical), revision.ExecutionSnapshotHash, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Executable assessment revision {revision.Id} failed its persisted hash check.");
        }

        var snapshot = JsonSerializer.Deserialize<AssessmentExecutionSnapshotV1>(snapshotCanonical, GradingJson.Options)
            ?? throw new JsonException("Execution snapshot is required.");
        GradingContractValidator.Validate(snapshot);
        var snapshotAuthoringSource = CanonicalJson.Serialize(
            JsonSerializer.SerializeToElement(snapshot.AuthoringSource, GradingJson.Options));
        if (!string.Equals(snapshotAuthoringSource, authoringCanonical, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Executable assessment revision {revision.Id} does not bind its authoring source to its execution snapshot.");
        }

        return snapshot;
    }
}
