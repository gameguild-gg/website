using System.Text.Json;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;
using GameGuild.Learning.Grading.Contracts;

namespace GameGuild.Learning.Assessments.Grading.Contracts;

public static class GradingContractValidator
{
    public static void Validate(ContentGradingDefinitionV2 definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        Require(definition.SchemaVersion == GradingContractVersions.ContentGrading,
            $"Content grading schemaVersion must be {GradingContractVersions.ContentGrading}.");
        Require(definition.Items is not null, "Content grading items are required.");
        foreach (var (itemId, item) in definition.Items)
        {
            RequireText(itemId, "Content grading item ID");
            Require(item is not null, $"Content grading item {itemId} is required.");
            if (item.RubricRef is not null) RequireText(item.RubricRef, $"Content grading item {itemId} rubricRef");
        }
    }

    public static void Validate(AssessmentExecutionPolicyV1 policy)
    {
        ArgumentNullException.ThrowIfNull(policy);
        Require(policy.SchemaVersion == GradingContractVersions.ExecutionPolicy,
            $"Execution policy schemaVersion must be {GradingContractVersions.ExecutionPolicy}.");
        PositiveWhenPresent(policy.MaxAttempts, "maxAttempts");
        PositiveWhenPresent(policy.TimeLimitMinutes, "timeLimitMinutes");
        if (policy.MaxAttempts.GetValueOrDefault(1) > 1)
        {
            Require(policy.AttemptContribution is not null,
                "attemptContribution is required when maxAttempts is greater than 1.");
        }

        Require(policy.Availability is not null, "availability is required.");
        Require(policy.Completion is not null, "completion is required.");
        Require(policy.ResultRelease is not null, "resultRelease is required.");
        Require(policy.Presentation is not null, "presentation is required.");
        Require(policy.Review is not null, "review is required.");

        ValidateUtc(policy.Availability.AvailableFrom, "availableFrom");
        ValidateUtc(policy.Availability.AvailableUntil, "availableUntil");
        ValidateUtc(policy.Availability.DueAt, "dueAt");
        ValidateUtc(policy.Availability.LateSubmissionDeadline, "lateSubmissionDeadline");
        Require(policy.Availability.AllowLateSubmissions || policy.Availability.LateSubmissionDeadline is null,
            "lateSubmissionDeadline requires allowLateSubmissions.");

        if (policy.ResultRelease.Mode == ResultReleaseMode.Scheduled)
        {
            Require(policy.ResultRelease.ScheduledFor is not null, "scheduled result release requires scheduledFor.");
            ValidateUtc(policy.ResultRelease.ScheduledFor, "scheduledFor");
        }
        else
        {
            Require(policy.ResultRelease.ScheduledFor is null,
                "scheduledFor is only valid for scheduled result release.");
        }

        Require(policy.Presentation.Mode is "continuous" or "single-step",
            "presentation mode is unsupported.");
        Validate(policy.Review);
    }

    public static void Validate(AssessmentReviewPolicyV1 review)
    {
        ArgumentNullException.ThrowIfNull(review);
        Require(review.SchemaVersion == 1, "Review policy schemaVersion must be 1.");
        review.Methods.EnsureValid();

        RequireConfig(review.Methods, ReviewMethods.PeerReview, review.Peer, "peer");
        RequireConfig(review.Methods, ReviewMethods.AIReview, review.Ai, "ai");
        RequireConfig(review.Methods, ReviewMethods.SelfReview, review.Self, "self");
        RequireConfig(review.Methods, ReviewMethods.InstructorReview, review.Instructor, "instructor");

        if (review.Peer is not null)
        {
            Positive(review.Peer.ReviewsPerReviewer, "reviewsPerReviewer");
            Positive(review.Peer.ReviewsRequiredPerSubmission, "reviewsRequiredPerSubmission");
            Positive(review.Peer.MinimumReviewsToFinalize, "minimumReviewsToFinalize");
            Positive(review.Peer.ClaimLeaseMinutes, "claimLeaseMinutes");
            Positive(review.Peer.EvidenceWindowMinutes, "evidenceWindowMinutes");
            Require(review.Peer.MinimumReviewsToFinalize <= review.Peer.ReviewsRequiredPerSubmission,
                "minimumReviewsToFinalize cannot exceed reviewsRequiredPerSubmission.");
            Require(review.Peer.ReviewsPerReviewer <= review.Peer.ReviewsRequiredPerSubmission,
                "reviewsPerReviewer cannot exceed reviewsRequiredPerSubmission.");
            Require(review.Peer.Aggregation is "mean" or "median", "Peer aggregation is unsupported.");
            Require(review.Peer.OnInsufficientEvidence == "await-instructor-resolution",
                "Peer insufficient-evidence policy is unsupported.");
        }

        if (review.Ai is not null)
        {
            RequireText(review.Ai.ProviderKey, "AI providerKey");
            RequireText(review.Ai.PolicyVersion, "AI policyVersion");
        }
        if (review.Self?.Instructions is not null) RequireText(review.Self.Instructions, "Self review instructions");
    }

    public static string? NormalizeReviewConfiguration(ReviewMethods methods, string? canonicalJson)
    {
        methods.EnsureValid(allowDraft: true);
        if (string.IsNullOrWhiteSpace(canonicalJson)) return null;
        if (Encoding.UTF8.GetByteCount(canonicalJson) > 65536)
            throw new ArgumentException("Review configuration cannot exceed 64 KiB.", nameof(canonicalJson));

        var configuration = JsonSerializer.Deserialize<AssessmentReviewConfigurationV1>(canonicalJson, GradingJson.Options)
            ?? throw new JsonException("Review configuration is required.");
        Require(configuration.SchemaVersion == 1, "Review configuration schemaVersion must be 1.");

        if (methods == ReviewMethods.None)
        {
            Require(configuration.Peer is null && configuration.Ai is null && configuration.Self is null && configuration.Instructor is null,
                "A draft without reviews cannot contain review configuration.");
        }
        else
        {
            Validate(new AssessmentReviewPolicyV1(
                configuration.SchemaVersion,
                methods,
                configuration.Peer,
                configuration.Ai,
                configuration.Self,
                configuration.Instructor));
        }

        var element = JsonSerializer.SerializeToElement(configuration, GradingJson.Options);
        return CanonicalJson.Serialize(element);
    }

    public static void Validate(AssessmentExecutionManifestV1 manifest)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        Require(manifest.SchemaVersion == GradingContractVersions.ExecutionManifest,
            $"Execution manifest schemaVersion must be {GradingContractVersions.ExecutionManifest}.");
        Require(manifest.Items is not null, "Manifest items are required.");
        Require(manifest.Stages is not null, "Manifest stages are required.");
        Require(manifest.Policies is not null, "Manifest policies are required.");

        var itemIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var item in manifest.Items)
        {
            Require(item is not null, "Manifest item is required.");
            RequireText(item.ItemId, "Manifest item ID");
            Require(itemIds.Add(item.ItemId), $"Manifest contains duplicate item ID {item.ItemId}.");
            RequireText(item.ItemType, $"Manifest item {item.ItemId} type");
            RequireText(item.AdapterKey, $"Manifest item {item.ItemId} adapter key");
            RequireText(item.AdapterVersion, $"Manifest item {item.ItemId} adapter version");
        }

        foreach (var stage in manifest.Stages)
        {
            Require(stage is not null, "Manifest review stage is required.");
            stage.Method.EnsureValid();
            RequireText(stage.HandlerKey, "Manifest review handler key");
            RequireText(stage.HandlerVersion, "Manifest review handler version");
            RequirePair(stage.ProviderKey, stage.ProviderPolicyVersion, "provider");
        }
        SequenceToMethods(manifest.Stages.Select(stage => stage.Method).ToArray());

        var policyIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var policy in manifest.Policies)
        {
            Require(policy is not null, "Manifest policy is required.");
            RequireText(policy.PolicyKey, "Manifest policy key");
            RequireText(policy.PolicyVersion, "Manifest policy version");
            Require(policyIds.Add($"{policy.PolicyKey}@{policy.PolicyVersion}"),
                $"Manifest contains duplicate policy {policy.PolicyKey}@{policy.PolicyVersion}.");
        }
    }

    public static void Validate(AssessmentExecutionSnapshotV1 snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        Require(snapshot.SchemaVersion == 1, "Execution snapshot schemaVersion must be 1.");
        Require(snapshot.AuthoringSource is not null, "Snapshot authoringSource is required.");
        Require(snapshot.AuthoringSource.SchemaVersion == 1, "Authoring source schemaVersion must be 1.");
        RequireText(snapshot.AuthoringSource.ContentType, "Authoring contentType");
        Require(snapshot.AuthoringSource.Content.ValueKind != JsonValueKind.Undefined, "Authoring content is required.");
        Validate(snapshot.AuthoringSource.Grading);
        Validate(snapshot.AuthoringSource.Policy);
        Validate(snapshot.Manifest);
        Require(snapshot.ItemProjections is not null, "Snapshot itemProjections are required.");

        var manifestIds = snapshot.Manifest.Items.Select(item => item.ItemId).ToHashSet(StringComparer.Ordinal);
        RequireSameKeys(manifestIds, snapshot.AuthoringSource.Grading.Items.Keys,
            "Manifest items and authoring grading items");
        RequireSameKeys(manifestIds, snapshot.ItemProjections.Keys,
            "Snapshot projections and manifest items");

        var configuredStages = snapshot.AuthoringSource.Policy.Review.Methods.ToSequence();
        Require(configuredStages.SequenceEqual(snapshot.Manifest.Stages.Select(stage => stage.Method)),
            "Manifest stages must exactly match review policy methods in canonical order.");

        foreach (var item in snapshot.Manifest.Items)
        {
            ValidateProjection(
                snapshot.ItemProjections[item.ItemId],
                item.ItemId,
                item.ItemType,
                snapshot.AuthoringSource.ContentType);
        }
        ValidateStageBindings(snapshot.Manifest, snapshot.AuthoringSource.Policy);
    }

    public static void Validate(AssessmentExecutionDeliveryV1 delivery)
    {
        ArgumentNullException.ThrowIfNull(delivery);
        Require(delivery.SchemaVersion == GradingContractVersions.ExecutionDelivery,
            $"Execution delivery schemaVersion must be {GradingContractVersions.ExecutionDelivery}.");
        Require(delivery.DefinitionRevisionId != Guid.Empty, "Delivery definitionRevisionId is required.");
        RequireHash(delivery.ExecutionSnapshotHash, "Delivery executionSnapshotHash");
        Require(delivery.ItemOrder is not null, "Delivery itemOrder is required.");
        Require(delivery.Items is not null, "Delivery items are required.");
        RequireSameKeys(delivery.ItemOrder.ToHashSet(StringComparer.Ordinal), delivery.Items.Keys,
            "Delivery order and items");
        Require(delivery.ItemOrder.Count == delivery.ItemOrder.Distinct(StringComparer.Ordinal).Count(),
            "Delivery itemOrder cannot contain duplicates.");
        foreach (var (itemId, item) in delivery.Items)
        {
            RequireText(itemId, "Delivery item ID");
            Require(item is not null, $"Delivery item {itemId} is required.");
            RequireText(item.AdapterKey, $"Delivery item {itemId} adapter key");
            RequireText(item.AdapterVersion, $"Delivery item {itemId} adapter version");
            Require(item.LearnerPayload.ValueKind != JsonValueKind.Undefined,
                $"Delivery item {itemId} learner payload is required.");
        }
    }

    public static void Validate(AssessmentResponseEnvelopeV1 response)
    {
        ArgumentNullException.ThrowIfNull(response);
        Require(response.SchemaVersion == GradingContractVersions.ResponseEnvelope,
            $"Response envelope schemaVersion must be {GradingContractVersions.ResponseEnvelope}.");
        RequireText(response.ContentType, "Response contentType");
        RequireText(response.PayloadSchema, "Response payloadSchema");
        Require(response.Payload.ValueKind != JsonValueKind.Undefined, "Response payload is required.");
    }

    public static void ValidateBindings(
        string executionSnapshotHash,
        AssessmentExecutionSnapshotV1 snapshot,
        AssessmentExecutionDeliveryV1 delivery,
        AssessmentResponseEnvelopeV1 response)
    {
        RequireHash(executionSnapshotHash, "Execution snapshot hash");
        Validate(snapshot);
        Validate(delivery);
        Validate(response);
        Require(string.Equals(snapshot.AuthoringSource.ContentType, response.ContentType, StringComparison.Ordinal),
            "Response contentType must match the execution snapshot.");
        Require(string.Equals(executionSnapshotHash, delivery.ExecutionSnapshotHash, StringComparison.Ordinal),
            "Delivery executionSnapshotHash must match the grading execution snapshot hash.");

        var manifestItems = snapshot.Manifest.Items.ToDictionary(item => item.ItemId, StringComparer.Ordinal);
        RequireSameKeys(manifestItems.Keys.ToHashSet(StringComparer.Ordinal), delivery.Items.Keys,
            "Delivery and manifest items");
        foreach (var (itemId, deliveryItem) in delivery.Items)
        {
            var manifestItem = manifestItems[itemId];
            Require(string.Equals(deliveryItem.AdapterKey, manifestItem.AdapterKey, StringComparison.Ordinal) &&
                    string.Equals(deliveryItem.AdapterVersion, manifestItem.AdapterVersion, StringComparison.Ordinal),
                $"Delivery item {itemId} adapter must match the execution manifest.");
        }
    }

    private static void ValidateProjection(JsonElement projection, string itemId, string itemType, string contentType)
    {
        Require(projection.ValueKind == JsonValueKind.Object, $"Projection {itemId} must be an object.");
        RequirePositiveInteger(projection, "schemaVersion", $"Projection {itemId} schemaVersion");
        RequirePropertyString(projection, "itemId", $"Projection {itemId} itemId", itemId);
        RequirePropertyString(projection, "itemType", $"Projection {itemId} itemType", itemType);
        var maxScore = RequireProperty(projection, "maxScore", $"Projection {itemId} maxScore");
        Require(maxScore.TryGetInt32(out var maxScoreUnits), $"Projection {itemId} maxScore must be an integer.");
        ScoreValue.FromUnits(maxScoreUnits);

        var source = RequireProperty(projection, "source", $"Projection {itemId} source");
        Require(source.ValueKind == JsonValueKind.Object, $"Projection {itemId} source must be an object.");
        Require(source.EnumerateObject().Select(property => property.Name)
                .ToHashSet(StringComparer.Ordinal).SetEquals(["contentType", "itemId"]),
            $"Projection {itemId} source contains unknown or missing fields.");
        RequirePropertyString(source, "contentType", $"Projection {itemId} source contentType", contentType);
        RequirePropertyString(source, "itemId", $"Projection {itemId} source itemId", itemId);
    }

    private static void ValidateStageBindings(AssessmentExecutionManifestV1 manifest, AssessmentExecutionPolicyV1 policy)
    {
        foreach (var stage in manifest.Stages)
        {
            if (stage.Method == ReviewMethod.AIReview)
            {
                Require(policy.Review.Ai is not null &&
                        string.Equals(stage.ProviderKey, policy.Review.Ai.ProviderKey, StringComparison.Ordinal) &&
                        string.Equals(stage.ProviderPolicyVersion, policy.Review.Ai.PolicyVersion, StringComparison.Ordinal),
                    "AIReview provider must exactly match the review policy.");
            }
            else
            {
                Require(stage.ProviderKey is null && stage.ProviderPolicyVersion is null,
                    "Only AIReview may fix a provider.");
            }
        }
    }

    private static ReviewMethods SequenceToMethods(IReadOnlyList<ReviewMethod> sequence)
    {
        Require(sequence.Count <= 2, "A review workflow supports at most two stages.");
        if (sequence.Count == 0) return ReviewMethods.None;
        Require(sequence.Count == 1 || sequence[1] == ReviewMethod.InstructorReview,
            "Only InstructorReview may be the final review stage.");
        Require(sequence[0] != ReviewMethod.InstructorReview || sequence.Count == 1,
            "InstructorReview cannot be followed by another stage.");

        var methods = ToFlag(sequence[0]);
        if (sequence.Count == 2) methods |= ReviewMethods.InstructorReview;
        return methods.EnsureValid(allowDraft: true);
    }

    private static ReviewMethods ToFlag(ReviewMethod method) => method switch
    {
        ReviewMethod.PeerReview => ReviewMethods.PeerReview,
        ReviewMethod.AIReview => ReviewMethods.AIReview,
        ReviewMethod.AutomatedReview => ReviewMethods.AutomatedReview,
        ReviewMethod.InstructorReview => ReviewMethods.InstructorReview,
        ReviewMethod.SelfReview => ReviewMethods.SelfReview,
        _ => throw new JsonException("Review method is unsupported."),
    };

    private static void RequireConfig(ReviewMethods methods, ReviewMethods flag, object? value, string label)
    {
        Require(methods.HasFlag(flag) == (value is not null),
            $"Review policy {label} configuration must exist exactly when its method is selected.");
    }

    private static void RequireSameKeys(HashSet<string> expected, IEnumerable<string> actual, string label)
    {
        Require(expected.SetEquals(actual), $"{label} must contain the same item IDs exactly once.");
    }

    private static JsonElement RequireProperty(JsonElement owner, string property, string label)
    {
        Require(owner.TryGetProperty(property, out var value), $"{label} is required.");
        return value;
    }

    private static void RequirePropertyString(
        JsonElement owner,
        string property,
        string label,
        string expected)
    {
        var value = RequireProperty(owner, property, label);
        Require(value.ValueKind == JsonValueKind.String, $"{label} must be a string.");
        Require(string.Equals(value.GetString(), expected, StringComparison.Ordinal), $"{label} does not match its manifest binding.");
    }

    private static void RequirePositiveInteger(JsonElement owner, string property, string label)
    {
        var value = RequireProperty(owner, property, label);
        Require(value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var integer) && integer > 0,
            $"{label} must be a positive integer.");
    }

    private static void RequirePair(string? first, string? second, string label)
    {
        Require((first is null) == (second is null), $"Manifest {label} key and version must be provided together.");
        if (first is not null)
        {
            RequireText(first, $"Manifest {label} key");
            RequireText(second!, $"Manifest {label} version");
        }
    }

    private static void RequireHash([NotNull] string? value, string label)
    {
        RequireText(value, label);
        Require(value.Length == 64 && value.All(character =>
                character is >= '0' and <= '9' or >= 'a' and <= 'f'),
            $"{label} must be a lowercase SHA-256 hash.");
    }

    private static void ValidateUtc(string? value, string label)
    {
        if (value is null) return;
        Require(DateTimeOffset.TryParseExact(
                value,
                "yyyy-MM-dd'T'HH:mm:ss.fff'Z'",
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out _),
            $"{label} must be a canonical UTC instant with millisecond precision.");
    }

    private static void PositiveWhenPresent(int? value, string label)
    {
        if (value is not null) Positive(value.Value, label);
    }

    private static void Positive(int value, string label) => Require(value > 0, $"{label} must be positive.");

    private static void RequireText([NotNull] string? value, string label) =>
        Require(!string.IsNullOrWhiteSpace(value), $"{label} must be a non-empty string.");

    private static void Require([DoesNotReturnIf(false)] bool condition, string message)
    {
        if (!condition) throw new JsonException(message);
    }
}
