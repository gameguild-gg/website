using GameGuild.Learning.Assessments.Grading.Contracts;
using GameGuild.Learning.Grading.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace GameGuild.Learning.Assessments.Grading.Persistence;

public sealed class GradingPersistenceModelConfiguration : IModelConfiguration
{
    private static readonly ValueConverter<ScoreValue, int> ScoreConverter =
        new(value => value.Units, value => ScoreValue.FromUnits(value));

    private static readonly ValueConverter<ScoreValue?, int?> NullableScoreConverter =
        new(value => value.HasValue ? value.Value.Units : null,
            value => value == null ? null : ScoreValue.FromUnits(value.Value));

    private static readonly ValueConverter<AssessmentTestRunStatus, string> TestRunStatusConverter =
        new(value => FormatTestRunStatus(value), value => ParseTestRunStatus(value));
    private static readonly ValueConverter<PersistedGradingExecutionStatus, string> ExecutionStatusConverter =
        new(value => FormatExecutionStatus(value), value => ParseExecutionStatus(value));
    private static readonly ValueConverter<PersistedGradeRoundStatus, string> RoundStatusConverter =
        new(value => FormatRoundStatus(value), value => ParseRoundStatus(value));
    private static readonly ValueConverter<PersistedReviewStageStatus, string> StageStatusConverter =
        new(value => FormatStageStatus(value), value => ParseStageStatus(value));
    private static readonly ValueConverter<PersistedGradeItemState, string> ItemStateConverter =
        new(value => FormatItemState(value), value => ParseItemState(value));
    private static readonly ValueConverter<ReviewMethod, string> ReviewMethodConverter =
        new(value => FormatReviewMethod(value), value => ParseReviewMethod(value));
    private static readonly ValueConverter<AcademicOutboxStatus, string> OutboxStatusConverter =
        new(value => FormatOutboxStatus(value), value => ParseOutboxStatus(value));
    private static readonly ValueConverter<AcademicOutboxDeliveryStatus, string> DeliveryStatusConverter =
        new(value => FormatDeliveryStatus(value), value => ParseDeliveryStatus(value));

    public void Configure(ModelBuilder modelBuilder)
    {
        ConfigureRevisions(modelBuilder);
        ConfigureTestRuns(modelBuilder);
        ConfigureExecutions(modelBuilder);
        ConfigureRounds(modelBuilder);
        ConfigureEvidence(modelBuilder);
        ConfigureReliability(modelBuilder);
    }

    private static void ConfigureRevisions(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AssessmentDefinitionRevision>(entity =>
        {
            entity.ToTable("AssessmentDefinitionRevisions", table =>
            {
                table.HasCheckConstraint("CK_AssessmentDefinitionRevisions_Number", "\"RevisionNumber\" > 0");
                table.HasCheckConstraint("CK_AssessmentDefinitionRevisions_Schema", "\"SchemaVersion\" = 1");
                table.HasCheckConstraint("CK_AssessmentDefinitionRevisions_AuthoringHash", "\"AuthoringSourceHash\" ~ '^[0-9a-f]{64}$'");
                table.HasCheckConstraint("CK_AssessmentDefinitionRevisions_ExecutionHash", "\"ExecutionSnapshotHash\" ~ '^[0-9a-f]{64}$'");
                table.HasCheckConstraint(
                    "CK_AssessmentDefinitionRevisions_HashVersions",
                    "\"AuthoringSourceHashVersion\" = 'sha256-jcs-v1' AND \"ExecutionSnapshotHashVersion\" = 'sha256-jcs-v1'");
                table.HasCheckConstraint("CK_AssessmentDefinitionRevisions_AuthoringSize", "octet_length(\"AuthoringSourceCanonicalJson\") <= 4194304");
                table.HasCheckConstraint("CK_AssessmentDefinitionRevisions_SnapshotSize", "octet_length(\"ExecutionSnapshotCanonicalJson\") <= 8388608");
            });
            entity.HasKey(value => value.Id);
            entity.HasAlternateKey(value => new { value.Id, value.AssessmentId });
            entity.HasIndex(value => new { value.AssessmentId, value.RevisionNumber }).IsUnique();
            entity.Property(value => value.AuthoringSourceCanonicalJson).HasColumnType("text").IsRequired();
            entity.Property(value => value.ExecutionSnapshotCanonicalJson).HasColumnType("text").IsRequired();
            entity.Property(value => value.AuthoringSourceHash).HasMaxLength(64).IsFixedLength().IsRequired();
            entity.Property(value => value.ExecutionSnapshotHash).HasMaxLength(64).IsFixedLength().IsRequired();
            entity.Property(value => value.AuthoringSourceHashVersion).HasMaxLength(32).IsRequired();
            entity.Property(value => value.ExecutionSnapshotHashVersion).HasMaxLength(32).IsRequired();
        });
    }

    private static void ConfigureTestRuns(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AssessmentTestRun>(entity =>
        {
            entity.ToTable("AssessmentTestRuns", table =>
            {
                table.HasCheckConstraint(
                    "CK_AssessmentTestRuns_Status",
                    "\"Status\" IN ('draft', 'running', 'completed', 'cancelled')");
                table.HasCheckConstraint(
                    "CK_AssessmentTestRuns_Completion",
                    "(\"Status\" IN ('completed', 'cancelled')) = (\"CompletedAt\" IS NOT NULL)");
            });
            entity.HasKey(value => value.Id);
            entity.Property(value => value.Status).HasConversion(TestRunStatusConverter).HasMaxLength(32);
            entity.HasAlternateKey(value => new { value.Id, value.AssessmentId });
            entity.HasIndex(value => value.AssessmentId);
            entity.HasIndex(value => value.DefinitionRevisionId);
            entity.HasOne<AssessmentDefinitionRevision>()
                .WithMany()
                .HasForeignKey(value => new { value.DefinitionRevisionId, value.AssessmentId })
                .HasPrincipalKey(value => new { value.Id, value.AssessmentId })
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<AssessmentTestRunSubject>(entity =>
        {
            entity.ToTable("AssessmentTestRunSubjects");
            entity.HasKey(value => value.Id);
            entity.Property(value => value.PersonaKey).HasMaxLength(128).IsRequired();
            entity.Property(value => value.DisplayName).HasMaxLength(200).IsRequired();
            entity.HasIndex(value => new { value.TestRunId, value.PersonaKey }).IsUnique();
            entity.HasOne<AssessmentTestRun>()
                .WithMany()
                .HasForeignKey(value => value.TestRunId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }

    private static void ConfigureExecutions(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<GradingExecution>(entity =>
        {
            entity.ToTable("GradingExecutions", table =>
            {
                table.HasCheckConstraint(
                    "CK_GradingExecutions_Owner",
                    "(\"ExecutionContext\" = 'author-test' AND \"TestRunSubjectId\" IS NOT NULL AND \"AssessmentSubmissionId\" IS NULL) OR " +
                    "(\"ExecutionContext\" = 'official-submission' AND \"TestRunSubjectId\" IS NULL AND \"AssessmentSubmissionId\" IS NOT NULL)");
                table.HasCheckConstraint(
                    "CK_GradingExecutions_DeliveryAllOrNone",
                    "num_nonnulls(\"DeliverySchemaVersion\", \"DeliveryCanonicalJson\", \"DeliveryHash\", \"DeliveryHashVersion\") IN (0, 4)");
                table.HasCheckConstraint(
                    "CK_GradingExecutions_ResponseAllOrNone",
                    "num_nonnulls(\"ResponseSchemaVersion\", \"ResponseContentType\", \"ResponsePayloadSchema\", \"ResponseEnvelopeCanonicalJson\", \"ResponseHash\", \"ResponseHashVersion\") IN (0, 6)");
                table.HasCheckConstraint("CK_GradingExecutions_DeliverySize", "\"DeliveryCanonicalJson\" IS NULL OR octet_length(\"DeliveryCanonicalJson\") <= 8388608");
                table.HasCheckConstraint("CK_GradingExecutions_ResponseSize", "\"ResponseEnvelopeCanonicalJson\" IS NULL OR octet_length(\"ResponseEnvelopeCanonicalJson\") <= 8388608");
                table.HasCheckConstraint(
                    "CK_GradingExecutions_Status",
                    "\"Status\" IN ('pending', 'running', 'awaiting-review', 'completed', 'failed')");
                table.HasCheckConstraint(
                    "CK_GradingExecutions_Hashes",
                    "(\"DeliveryHash\" IS NULL OR \"DeliveryHash\" ~ '^[0-9a-f]{64}$') AND " +
                    "(\"ResponseHash\" IS NULL OR \"ResponseHash\" ~ '^[0-9a-f]{64}$')");
                table.HasCheckConstraint(
                    "CK_GradingExecutions_HashVersions",
                    "(\"DeliveryHashVersion\" IS NULL OR \"DeliveryHashVersion\" = 'sha256-jcs-v1') AND " +
                    "(\"ResponseHashVersion\" IS NULL OR \"ResponseHashVersion\" = 'sha256-jcs-v1')");
                table.HasCheckConstraint(
                    "CK_GradingExecutions_SubmissionLifecycle",
                    "(\"Status\" = 'pending' AND \"SubmittedAt\" IS NULL) OR " +
                    "(\"Status\" <> 'pending' AND \"SubmittedAt\" IS NOT NULL)");
                table.HasCheckConstraint(
                    "CK_GradingExecutions_Finalization",
                    "(\"Status\" IN ('completed', 'failed')) = (\"FinalizedAt\" IS NOT NULL)");
            });
            entity.HasKey(value => value.Id);
            entity.Property(value => value.ExecutionContext)
                .HasConversion(value => value == ReviewExecutionContext.AuthorTest ? "author-test" : "official-submission",
                    value => value == "author-test" ? ReviewExecutionContext.AuthorTest : ReviewExecutionContext.OfficialSubmission)
                .HasMaxLength(32)
                .IsRequired();
            entity.Property(value => value.Status).HasConversion(ExecutionStatusConverter).HasMaxLength(48);
            entity.Property(value => value.DeliveryCanonicalJson).HasColumnType("text");
            entity.Property(value => value.ResponseEnvelopeCanonicalJson).HasColumnType("text");
            entity.Property(value => value.DeliverySchemaVersion).HasMaxLength(32);
            entity.Property(value => value.ResponseSchemaVersion).HasMaxLength(32);
            entity.Property(value => value.ResponseContentType).HasMaxLength(128);
            entity.Property(value => value.ResponsePayloadSchema).HasMaxLength(160);
            entity.Property(value => value.DeliveryHashVersion).HasMaxLength(32);
            entity.Property(value => value.ResponseHashVersion).HasMaxLength(32);
            entity.Property(value => value.DeliveryHash).HasMaxLength(64).IsFixedLength();
            entity.Property(value => value.ResponseHash).HasMaxLength(64).IsFixedLength();
            entity.HasIndex(value => value.TestRunSubjectId).IsUnique();
            entity.HasIndex(value => value.AssessmentSubmissionId).IsUnique();
            entity.HasOne<AssessmentDefinitionRevision>()
                .WithMany()
                .HasForeignKey(value => value.DefinitionRevisionId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<AssessmentTestRunSubject>()
                .WithMany()
                .HasForeignKey(value => value.TestRunSubjectId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<AssessmentSubmission>()
                .WithMany()
                .HasForeignKey(value => value.AssessmentSubmissionId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<GradeRound>()
                .WithMany()
                .HasForeignKey(value => new { value.ActiveGradeRoundId, value.Id })
                .HasPrincipalKey(value => new { value.Id, value.GradingExecutionId })
                .OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureRounds(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<GradeRound>(entity =>
        {
            entity.ToTable("GradeRounds", table =>
            {
                table.HasCheckConstraint("CK_GradeRounds_Number", "\"RoundNumber\" > 0");
                table.HasCheckConstraint(
                    "CK_GradeRounds_ScoreRange",
                    "\"MaxScore\" > 0 AND (\"Score\" IS NULL OR (\"Score\" >= 0 AND \"Score\" <= \"MaxScore\"))");
                table.HasCheckConstraint(
                    "CK_GradeRounds_Status",
                    "\"Status\" IN ('pending', 'running', 'awaiting-evidence', 'awaiting-instructor-resolution', 'failed', 'finalized')");
                table.HasCheckConstraint("CK_GradeRounds_Reason", "\"Reason\" IN ('initial', 'regrade')");
                table.HasCheckConstraint("CK_GradeRounds_ResultSchema", "\"ResultSchemaVersion\" = 1");
                table.HasCheckConstraint(
                    "CK_GradeRounds_Result",
                    "(\"ResultState\" IS NULL AND \"Score\" IS NULL) OR " +
                    "(\"ResultState\" = 'partial' AND \"Score\" IS NULL) OR " +
                    "(\"ResultState\" = 'final' AND \"Score\" IS NOT NULL)");
                table.HasCheckConstraint(
                    "CK_GradeRounds_Finalization",
                    "(\"Status\" = 'finalized') = (\"FinalizedAt\" IS NOT NULL)");
            });
            entity.HasKey(value => value.Id);
            entity.HasAlternateKey(value => new { value.Id, value.GradingExecutionId });
            entity.HasIndex(value => new { value.GradingExecutionId, value.RoundNumber }).IsUnique();
            entity.Property(value => value.Score).HasConversion(NullableScoreConverter).HasColumnType("integer");
            entity.Property(value => value.MaxScore).HasConversion(ScoreConverter).HasColumnType("integer");
            entity.Property(value => value.Status).HasConversion(RoundStatusConverter).HasMaxLength(48);
            entity.Property(value => value.Reason).HasMaxLength(64).IsRequired();
            entity.Property(value => value.Feedback).HasColumnType("text");
            entity.HasOne<GradingExecution>()
                .WithMany()
                .HasForeignKey(value => value.GradingExecutionId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<GradeRound>()
                .WithMany()
                .HasForeignKey(value => new { value.SupersedesGradeRoundId, value.GradingExecutionId })
                .HasPrincipalKey(value => new { value.Id, value.GradingExecutionId })
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ReviewStage>(entity =>
        {
            entity.ToTable("ReviewStages", table =>
            {
                table.HasCheckConstraint("CK_ReviewStages_Sequence", "\"Sequence\" > 0");
                table.HasCheckConstraint(
                    "CK_ReviewStages_Method",
                    "\"ReviewMethod\" IN ('peer-review', 'ai-review', 'automated-review', 'instructor-review', 'self-review')");
                table.HasCheckConstraint(
                    "CK_ReviewStages_Status",
                    "\"Status\" IN ('pending', 'running', 'awaiting-evidence', 'awaiting-instructor-resolution', 'completed', 'failed')");
                table.HasCheckConstraint(
                    "CK_ReviewStages_ProviderBinding",
                    "num_nonnulls(\"ProviderKey\", \"ProviderPolicyVersion\") IN (0, 2)");
                table.HasCheckConstraint(
                    "CK_ReviewStages_Lifecycle",
                    "(\"Status\" IN ('completed', 'failed')) = (\"CompletedAt\" IS NOT NULL) AND " +
                    "(\"StartedAt\" IS NULL OR \"CompletedAt\" IS NULL OR \"StartedAt\" <= \"CompletedAt\")");
            });
            entity.HasKey(value => value.Id);
            entity.HasIndex(value => new { value.GradeRoundId, value.Sequence }).IsUnique();
            entity.Property(value => value.ReviewMethod).HasConversion(ReviewMethodConverter).HasMaxLength(32);
            entity.Property(value => value.Status).HasConversion(StageStatusConverter).HasMaxLength(48);
            entity.Property(value => value.HandlerKey).HasMaxLength(128).IsRequired();
            entity.Property(value => value.HandlerVersion).HasMaxLength(64).IsRequired();
            entity.Property(value => value.ProviderKey).HasMaxLength(128);
            entity.Property(value => value.ProviderPolicyVersion).HasMaxLength(64);
            entity.HasOne<GradeRound>()
                .WithMany()
                .HasForeignKey(value => value.GradeRoundId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<GradeItemResult>(entity =>
        {
            entity.ToTable("GradeItemResults", table =>
            {
                table.HasCheckConstraint(
                    "CK_GradeItemResults_State",
                    "\"State\" IN ('graded', 'pending', 'unsupported')");
                table.HasCheckConstraint(
                    "CK_GradeItemResults_ScoreRange",
                    "\"MaxScore\" >= 0 AND " +
                    "(\"Score\" IS NULL OR (\"Score\" >= 0 AND \"Score\" <= \"MaxScore\")) AND " +
                    "((\"State\" = 'graded') = (\"Score\" IS NOT NULL))");
            });
            entity.HasKey(value => value.Id);
            entity.HasIndex(value => new { value.ReviewStageId, value.ItemId }).IsUnique();
            entity.Property(value => value.ItemId).HasMaxLength(200).IsRequired();
            entity.Property(value => value.State).HasConversion(ItemStateConverter).HasMaxLength(32);
            entity.Property(value => value.Score).HasConversion(NullableScoreConverter).HasColumnType("integer");
            entity.Property(value => value.MaxScore).HasConversion(ScoreConverter).HasColumnType("integer");
            entity.Property(value => value.Feedback).HasColumnType("text");
            entity.HasOne<ReviewStage>()
                .WithMany()
                .HasForeignKey(value => value.ReviewStageId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }

    private static void ConfigureEvidence(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ReviewEvidence>(entity =>
        {
            entity.ToTable("ReviewEvidence", table =>
            {
                table.HasCheckConstraint("CK_ReviewEvidence_Size", "octet_length(\"CanonicalJson\") <= 1048576");
                table.HasCheckConstraint("CK_ReviewEvidence_Hash", "\"PayloadHash\" ~ '^[0-9a-f]{64}$'");
                table.HasCheckConstraint("CK_ReviewEvidence_HashVersion", "\"HashVersion\" = 'sha256-jcs-v1'");
                table.HasCheckConstraint(
                    "CK_ReviewEvidence_Producer",
                    "num_nonnulls(\"ProducedByActorId\", \"ProducedByService\") = 1");
            });
            entity.HasKey(value => value.Id);
            entity.HasIndex(value => new { value.ReviewStageId, value.EvidenceKey }).IsUnique();
            entity.Property(value => value.EvidenceKey).HasMaxLength(160).IsRequired();
            entity.Property(value => value.ItemId).HasMaxLength(200);
            entity.Property(value => value.EvidenceType).HasMaxLength(128).IsRequired();
            entity.Property(value => value.CanonicalJson).HasColumnType("text").IsRequired();
            entity.Property(value => value.PayloadHash).HasMaxLength(64).IsFixedLength().IsRequired();
            entity.Property(value => value.SchemaVersion).HasMaxLength(64).IsRequired();
            entity.Property(value => value.HashVersion).HasMaxLength(32).IsRequired();
            entity.Property(value => value.ProducedByService).HasMaxLength(160);
            entity.HasOne<ReviewStage>()
                .WithMany()
                .HasForeignKey(value => value.ReviewStageId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }

    private static void ConfigureReliability(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<GradingCommandReceipt>(entity =>
        {
            entity.ToTable("GradingCommandReceipts", table =>
            {
                table.HasCheckConstraint("CK_GradingCommandReceipts_Hash", "\"RequestHash\" ~ '^[0-9a-f]{64}$'");
                table.HasCheckConstraint("CK_GradingCommandReceipts_OutcomeSize", "octet_length(\"OutcomeCanonicalJson\") <= 1048576");
                table.HasCheckConstraint("CK_GradingCommandReceipts_Retention", "\"ExpiresAt\" > \"CreatedAt\"");
            });
            entity.HasKey(value => value.Id);
            entity.HasIndex(value => new { value.TenantId, value.ResourceId, value.CommandType, value.ActorId, value.IdempotencyKey }).IsUnique();
            entity.Property(value => value.CommandType).HasMaxLength(128).IsRequired();
            entity.Property(value => value.IdempotencyKey).HasMaxLength(200).IsRequired();
            entity.Property(value => value.RequestHash).HasMaxLength(64).IsFixedLength().IsRequired();
            entity.Property(value => value.OutcomeSchemaVersion).HasMaxLength(64).IsRequired();
            entity.Property(value => value.OutcomeCanonicalJson).HasColumnType("text").IsRequired();
        });

        modelBuilder.Entity<AcademicOutboxMessage>(entity =>
        {
            entity.ToTable("AcademicOutboxMessages", table =>
            {
                table.HasCheckConstraint("CK_AcademicOutboxMessages_Hash", "\"PayloadHash\" ~ '^[0-9a-f]{64}$'");
                table.HasCheckConstraint("CK_AcademicOutboxMessages_PayloadSize", "octet_length(\"PayloadCanonicalJson\") <= 1048576");
                table.HasCheckConstraint("CK_AcademicOutboxMessages_Status", "\"Status\" IN ('pending', 'processing', 'completed', 'failed')");
                table.HasCheckConstraint(
                    "CK_AcademicOutboxMessages_Completion",
                    "(\"Status\" = 'completed') = (\"CompletedAt\" IS NOT NULL)");
            });
            entity.HasKey(value => value.Id);
            entity.Property(value => value.EventType).HasMaxLength(160).IsRequired();
            entity.Property(value => value.EventSchemaVersion).HasMaxLength(64).IsRequired();
            entity.Property(value => value.PayloadCanonicalJson).HasColumnType("text").IsRequired();
            entity.Property(value => value.PayloadHash).HasMaxLength(64).IsFixedLength().IsRequired();
            entity.Property(value => value.Status).HasConversion(OutboxStatusConverter).HasMaxLength(32).IsConcurrencyToken();
        });

        modelBuilder.Entity<AcademicOutboxDelivery>(entity =>
        {
            entity.ToTable("AcademicOutboxDeliveries", table =>
            {
                table.HasCheckConstraint("CK_AcademicOutboxDeliveries_Attempts", "\"AttemptCount\" >= 0");
                table.HasCheckConstraint("CK_AcademicOutboxDeliveries_Status", "\"Status\" IN ('pending', 'processing', 'confirmed', 'failed')");
                table.HasCheckConstraint(
                    "CK_AcademicOutboxDeliveries_Lifecycle",
                    "(\"Status\" = 'processing') = (\"ClaimedAt\" IS NOT NULL AND \"ClaimedBy\" IS NOT NULL) AND " +
                    "(\"Status\" = 'confirmed') = (\"ConfirmedAt\" IS NOT NULL) AND " +
                    "(\"Status\" = 'failed') = (\"NextAttemptAt\" IS NOT NULL)");
                table.HasCheckConstraint(
                    "CK_AcademicOutboxDeliveries_ErrorSize",
                    "\"LastError\" IS NULL OR octet_length(\"LastError\") <= 4096");
            });
            entity.HasKey(value => value.Id);
            entity.HasIndex(value => new { value.OutboxMessageId, value.ConsumerKey }).IsUnique();
            entity.HasIndex(value => new { value.Status, value.NextAttemptAt });
            entity.HasIndex(value => new { value.Status, value.ClaimedAt });
            entity.Property(value => value.ConsumerKey).HasMaxLength(160).IsRequired();
            entity.Property(value => value.Status).HasConversion(DeliveryStatusConverter).HasMaxLength(32).IsConcurrencyToken();
            entity.Property(value => value.ClaimedAt).IsConcurrencyToken();
            entity.Property(value => value.ClaimedBy).HasMaxLength(200);
            entity.Property(value => value.LastError).HasColumnType("text");
            entity.HasOne<AcademicOutboxMessage>()
                .WithMany()
                .HasForeignKey(value => value.OutboxMessageId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }

    private static string FormatTestRunStatus(AssessmentTestRunStatus value) => value switch
    {
        AssessmentTestRunStatus.Draft => "draft",
        AssessmentTestRunStatus.Running => "running",
        AssessmentTestRunStatus.Completed => "completed",
        AssessmentTestRunStatus.Cancelled => "cancelled",
        _ => throw new ArgumentOutOfRangeException(nameof(value)),
    };

    private static AssessmentTestRunStatus ParseTestRunStatus(string value) => value switch
    {
        "draft" => AssessmentTestRunStatus.Draft,
        "running" => AssessmentTestRunStatus.Running,
        "completed" => AssessmentTestRunStatus.Completed,
        "cancelled" => AssessmentTestRunStatus.Cancelled,
        _ => throw new InvalidOperationException($"Unknown test-run status '{value}'."),
    };

    private static string FormatExecutionStatus(PersistedGradingExecutionStatus value) => value switch
    {
        PersistedGradingExecutionStatus.Pending => "pending",
        PersistedGradingExecutionStatus.Running => "running",
        PersistedGradingExecutionStatus.AwaitingReview => "awaiting-review",
        PersistedGradingExecutionStatus.Completed => "completed",
        PersistedGradingExecutionStatus.Failed => "failed",
        _ => throw new ArgumentOutOfRangeException(nameof(value)),
    };

    private static PersistedGradingExecutionStatus ParseExecutionStatus(string value) => value switch
    {
        "pending" => PersistedGradingExecutionStatus.Pending,
        "running" => PersistedGradingExecutionStatus.Running,
        "awaiting-review" => PersistedGradingExecutionStatus.AwaitingReview,
        "completed" => PersistedGradingExecutionStatus.Completed,
        "failed" => PersistedGradingExecutionStatus.Failed,
        _ => throw new InvalidOperationException($"Unknown grading execution status '{value}'."),
    };

    private static string FormatRoundStatus(PersistedGradeRoundStatus value) => value switch
    {
        PersistedGradeRoundStatus.Pending => "pending",
        PersistedGradeRoundStatus.Running => "running",
        PersistedGradeRoundStatus.AwaitingEvidence => "awaiting-evidence",
        PersistedGradeRoundStatus.AwaitingInstructorResolution => "awaiting-instructor-resolution",
        PersistedGradeRoundStatus.Failed => "failed",
        PersistedGradeRoundStatus.Finalized => "finalized",
        _ => throw new ArgumentOutOfRangeException(nameof(value)),
    };

    private static PersistedGradeRoundStatus ParseRoundStatus(string value) => value switch
    {
        "pending" => PersistedGradeRoundStatus.Pending,
        "running" => PersistedGradeRoundStatus.Running,
        "awaiting-evidence" => PersistedGradeRoundStatus.AwaitingEvidence,
        "awaiting-instructor-resolution" => PersistedGradeRoundStatus.AwaitingInstructorResolution,
        "failed" => PersistedGradeRoundStatus.Failed,
        "finalized" => PersistedGradeRoundStatus.Finalized,
        _ => throw new InvalidOperationException($"Unknown grade-round status '{value}'."),
    };

    private static string FormatStageStatus(PersistedReviewStageStatus value) => value switch
    {
        PersistedReviewStageStatus.Pending => "pending",
        PersistedReviewStageStatus.Running => "running",
        PersistedReviewStageStatus.AwaitingEvidence => "awaiting-evidence",
        PersistedReviewStageStatus.AwaitingInstructorResolution => "awaiting-instructor-resolution",
        PersistedReviewStageStatus.Completed => "completed",
        PersistedReviewStageStatus.Failed => "failed",
        _ => throw new ArgumentOutOfRangeException(nameof(value)),
    };

    private static PersistedReviewStageStatus ParseStageStatus(string value) => value switch
    {
        "pending" => PersistedReviewStageStatus.Pending,
        "running" => PersistedReviewStageStatus.Running,
        "awaiting-evidence" => PersistedReviewStageStatus.AwaitingEvidence,
        "awaiting-instructor-resolution" => PersistedReviewStageStatus.AwaitingInstructorResolution,
        "completed" => PersistedReviewStageStatus.Completed,
        "failed" => PersistedReviewStageStatus.Failed,
        _ => throw new InvalidOperationException($"Unknown review-stage status '{value}'."),
    };

    private static string FormatItemState(PersistedGradeItemState value) => value switch
    {
        PersistedGradeItemState.Graded => "graded",
        PersistedGradeItemState.Pending => "pending",
        PersistedGradeItemState.Unsupported => "unsupported",
        _ => throw new ArgumentOutOfRangeException(nameof(value)),
    };

    private static PersistedGradeItemState ParseItemState(string value) => value switch
    {
        "graded" => PersistedGradeItemState.Graded,
        "pending" => PersistedGradeItemState.Pending,
        "unsupported" => PersistedGradeItemState.Unsupported,
        _ => throw new InvalidOperationException($"Unknown grade-item state '{value}'."),
    };

    private static string FormatReviewMethod(ReviewMethod value) => value switch
    {
        ReviewMethod.PeerReview => "peer-review",
        ReviewMethod.AIReview => "ai-review",
        ReviewMethod.AutomatedReview => "automated-review",
        ReviewMethod.InstructorReview => "instructor-review",
        ReviewMethod.SelfReview => "self-review",
        _ => throw new ArgumentOutOfRangeException(nameof(value)),
    };

    private static ReviewMethod ParseReviewMethod(string value) => value switch
    {
        "peer-review" => ReviewMethod.PeerReview,
        "ai-review" => ReviewMethod.AIReview,
        "automated-review" => ReviewMethod.AutomatedReview,
        "instructor-review" => ReviewMethod.InstructorReview,
        "self-review" => ReviewMethod.SelfReview,
        _ => throw new InvalidOperationException($"Unknown review method '{value}'."),
    };

    private static string FormatOutboxStatus(AcademicOutboxStatus value) => value.ToString().ToLowerInvariant();
    private static AcademicOutboxStatus ParseOutboxStatus(string value) => Enum.Parse<AcademicOutboxStatus>(value, true);
    private static string FormatDeliveryStatus(AcademicOutboxDeliveryStatus value) => value.ToString().ToLowerInvariant();
    private static AcademicOutboxDeliveryStatus ParseDeliveryStatus(string value) => Enum.Parse<AcademicOutboxDeliveryStatus>(value, true);
}
