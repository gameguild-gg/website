using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using GameGuild.Learning.Assessments.Grading.Contracts;
using GameGuild.Learning.Grading.Contracts;
using GameGuild.Learning.Assessments.Grading.Persistence;

namespace GameGuild.Learning.Assessments;

/// <summary>
///     EF Core model configuration for the Learning.Assessments module.
///     Discovered by ApplicationDbContext via assembly scanning.
/// </summary>
public sealed class AssessmentsModelConfiguration : IModelConfiguration
{
    private static readonly ValueConverter<ScoreValue, int> ScoreConverter =
        new(value => value.Units, value => ScoreValue.FromUnits(value));
    private static readonly ValueConverter<ScoreValue?, int?> NullableScoreConverter =
        new(value => value.HasValue ? value.Value.Units : null,
            value => value == null ? null : ScoreValue.FromUnits(value.Value));
    private static readonly ValueConverter<PercentValue, int> PercentConverter =
        new(value => value.Units, value => PercentValue.FromUnits(value));
    private static readonly ValueConverter<AttemptContributionMode?, string?> AttemptContributionConverter =
        new(value => value.HasValue ? FormatAttemptContribution(value.Value) : null,
            value => value == null ? null : ParseAttemptContribution(value));
    private static readonly ValueConverter<ContentCompletionMode, string> CompletionConverter =
        new(value => FormatCompletion(value), value => ParseCompletion(value));
    private static readonly ValueConverter<ResultReleaseMode, string> ReleaseConverter =
        new(value => FormatRelease(value), value => ParseRelease(value));

    public void Configure(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Assessment>(entity =>
        {
            entity.ToTable("Assessments");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Title).HasMaxLength(500).IsRequired();
            entity.Property(e => e.Slug).HasMaxLength(220).IsRequired();
            entity.Property(e => e.Description).HasMaxLength(2000);
            entity.Property(e => e.SubmissionModalities).HasConversion<int>();
            entity.Property(e => e.PresentationMode).HasConversion<int>();
            entity.Property(e => e.ReviewMethods).HasConversion<int>();
            entity.Property(e => e.MaxScore).HasConversion(ScoreConverter).HasColumnType("integer");
            entity.Property(e => e.PassingScore).HasConversion(ScoreConverter).HasColumnType("integer");
            entity.Property(e => e.ReviewConfigurationCanonicalJson).HasColumnType("text").HasMaxLength(65536);
            entity.Property(e => e.AttemptContributionMode).HasConversion(AttemptContributionConverter).HasMaxLength(32);
            entity.Property(e => e.ContentCompletionMode).HasConversion(CompletionConverter).HasMaxLength(32);
            entity.Property(e => e.ResultReleaseMode).HasConversion(ReleaseConverter).HasMaxLength(16);
            entity.Property(e => e.MaxAttempts).HasDefaultValue(1);
            entity.ToTable(table =>
            {
                table.HasCheckConstraint(
                    "CK_Assessments_SubmissionModalities",
                    "\"SubmissionModalities\" > 0 AND (\"SubmissionModalities\" & ~127) = 0");
                table.HasCheckConstraint(
                    "CK_Assessments_PresentationMode",
                    "\"PresentationMode\" IN (0, 1)");
                table.HasCheckConstraint(
                    "CK_Assessments_ReviewMethods",
                    "\"ReviewMethods\" IN (0, 1, 2, 4, 8, 9, 10, 12, 16, 24)");
                table.HasCheckConstraint(
                    "CK_Assessments_ScoreRange",
                    "\"MaxScore\" > 0 AND \"PassingScore\" >= 0 AND \"PassingScore\" <= \"MaxScore\"");
                table.HasCheckConstraint("CK_Assessments_MaxAttempts", "\"MaxAttempts\" = 1");
                table.HasCheckConstraint(
                    "CK_Assessments_ResultRelease",
                    "(\"ResultReleaseMode\" = 'scheduled' AND \"ResultReleaseScheduledFor\" IS NOT NULL) OR " +
                    "(\"ResultReleaseMode\" <> 'scheduled' AND \"ResultReleaseScheduledFor\" IS NULL)");
                table.HasCheckConstraint(
                    "CK_Assessments_ReviewConfiguration",
                    "\"ReviewConfigurationCanonicalJson\" IS NULL OR " +
                    "(octet_length(\"ReviewConfigurationCanonicalJson\") <= 65536 AND " +
                    "jsonb_typeof(\"ReviewConfigurationCanonicalJson\"::jsonb) = 'object' AND " +
                    "(\"ReviewConfigurationCanonicalJson\"::jsonb ->> 'schemaVersion') = '1')");
                table.HasCheckConstraint(
                    "CK_Assessments_DeliverySchedule",
                    "(\"AvailableFrom\" IS NULL OR \"AvailableUntil\" IS NULL OR \"AvailableFrom\" <= \"AvailableUntil\") AND " +
                    "(\"DueAt\" IS NULL OR \"AvailableFrom\" IS NULL OR \"DueAt\" >= \"AvailableFrom\") AND " +
                    "(\"DueAt\" IS NULL OR \"AvailableUntil\" IS NULL OR \"DueAt\" <= \"AvailableUntil\") AND " +
                    "(NOT \"AllowLateSubmissions\" OR (\"DueAt\" IS NOT NULL AND \"LateSubmissionDeadline\" IS NOT NULL AND \"LateSubmissionDeadline\" > \"DueAt\" AND (\"AvailableUntil\" IS NULL OR \"LateSubmissionDeadline\" <= \"AvailableUntil\"))) AND " +
                    "(\"AllowLateSubmissions\" OR \"LateSubmissionDeadline\" IS NULL)");
            });
            entity.HasIndex(e => e.CourseId);
            entity.HasIndex(e => e.AssessmentGroupId);
            entity.HasIndex(e => e.Slug);
            entity.HasIndex(e => e.ContentId)
                .IsUnique()
                .HasFilter("\"ContentId\" IS NOT NULL AND \"DeletedAt\" IS NULL");
            entity.HasOne(e => e.AssessmentGroup)
                .WithMany()
                .HasForeignKey(e => e.AssessmentGroupId)
                .OnDelete(DeleteBehavior.SetNull);
            entity.HasOne<AssessmentDefinitionRevision>()
                .WithMany()
                .HasForeignKey(e => new { e.PublishedDefinitionRevisionId, e.Id })
                .HasPrincipalKey(e => new { e.Id, e.AssessmentId })
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<AssessmentGroup>(entity =>
        {
            entity.ToTable("AssessmentGroups");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).HasMaxLength(160).IsRequired();
            entity.Property(e => e.Description).HasMaxLength(1000);
            entity.Property(e => e.WeightPercent).HasConversion(PercentConverter).HasColumnType("integer");
            entity.ToTable(table => table.HasCheckConstraint(
                "CK_AssessmentGroups_WeightPercent",
                "\"WeightPercent\" >= 0 AND \"WeightPercent\" <= 10000"));
            entity.HasIndex(e => e.CourseId);
            entity.HasIndex(e => new { e.CourseId, e.Order });
        });

        modelBuilder.Entity<AssessmentSubmission>(entity =>
        {
            entity.ToTable("AssessmentSubmissions");
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.AssessmentId);
            entity.HasIndex(e => e.EnrollmentId);
            entity.HasIndex(e => e.UserId);
            entity.HasIndex(e => new { e.AssessmentId, e.EnrollmentId, e.AttemptNumber })
                .IsUnique()
                .HasDatabaseName("UX_AssessmentSubmissions_Assessment_Enrollment_Attempt");
            entity.Property(e => e.SubmittedModalities).HasConversion<int>();
            entity.Property(e => e.TextPayload).HasColumnType("text");
            entity.Property(e => e.FilePayload).HasMaxLength(2048);
            entity.Property(e => e.UrlPayload).HasMaxLength(2048);
            entity.Property(e => e.CodePayload).HasColumnType("text");
            entity.Property(e => e.MediaPayload).HasMaxLength(2048);
            entity.Property(e => e.ProjectPayload).HasMaxLength(2048);
            entity.Property(e => e.Score).HasConversion(NullableScoreConverter).HasColumnType("integer");
            entity.Property(e => e.RubricScoresPayload).HasColumnType("jsonb");
            entity.ToTable(table =>
            {
                table.HasCheckConstraint(
                    "CK_AssessmentSubmissions_SubmittedModalities",
                    "\"SubmittedModalities\" >= 0 AND (\"SubmittedModalities\" & ~127) = 0");
                table.HasCheckConstraint(
                    "CK_AssessmentSubmissions_ScoreCanonical",
                    "\"Score\" IS NULL OR \"Score\" >= 0");
                table.HasCheckConstraint(
                    "CK_AssessmentSubmissions_AttemptNumberPositive",
                    "\"AttemptNumber\" > 0");
                table.HasCheckConstraint(
                    "CK_AssessmentSubmissions_PayloadConsistency",
                    "((\"SubmittedModalities\" & 1) = 0 OR \"TextPayload\" IS NOT NULL) AND " +
                    "((\"SubmittedModalities\" & 2) = 0 OR \"FilePayload\" IS NOT NULL) AND " +
                    "((\"SubmittedModalities\" & 4) = 0 OR \"UrlPayload\" IS NOT NULL) AND " +
                    "((\"SubmittedModalities\" & 8) = 0 OR \"CodePayload\" IS NOT NULL) AND " +
                    "((\"SubmittedModalities\" & 16) = 0 OR \"MediaPayload\" IS NOT NULL) AND " +
                    "((\"SubmittedModalities\" & 32) = 0 OR \"ProjectPayload\" IS NOT NULL) AND " +
                    "(\"TextPayload\" IS NULL OR (\"SubmittedModalities\" & 1) <> 0) AND " +
                    "(\"FilePayload\" IS NULL OR (\"SubmittedModalities\" & 2) <> 0) AND " +
                    "(\"UrlPayload\" IS NULL OR (\"SubmittedModalities\" & 4) <> 0) AND " +
                    "(\"CodePayload\" IS NULL OR (\"SubmittedModalities\" & 8) <> 0) AND " +
                    "(\"MediaPayload\" IS NULL OR (\"SubmittedModalities\" & 16) <> 0) AND " +
                    "(\"ProjectPayload\" IS NULL OR (\"SubmittedModalities\" & 32) <> 0)");
            });
        });

        modelBuilder.Entity<CourseGroupSet>(entity =>
        {
            entity.ToTable("CourseGroupSets");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).HasMaxLength(160).IsRequired();
            entity.HasIndex(e => new { e.CourseId, e.Name }).IsUnique();
        });

        modelBuilder.Entity<CourseGroup>(entity =>
        {
            entity.ToTable("CourseGroups");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).HasMaxLength(160).IsRequired();
            entity.HasIndex(e => e.GroupSetId);
        });

        modelBuilder.Entity<CourseGroupMember>(entity =>
        {
            entity.ToTable("CourseGroupMembers");
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.GroupId, e.UserId }).IsUnique();
            entity.HasIndex(e => e.UserId);
        });

        modelBuilder.Entity<AssessmentRubric>(entity =>
        {
            entity.ToTable("AssessmentRubrics");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Title).HasMaxLength(160).IsRequired();
        });

        modelBuilder.Entity<RubricCriterion>(entity =>
        {
            entity.ToTable("RubricCriteria");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Description).IsRequired();
            entity.Property(e => e.Points).HasConversion(ScoreConverter).HasColumnType("integer");
            entity.ToTable(table => table.HasCheckConstraint(
                "CK_RubricCriteria_PointsCanonical",
                "\"Points\" >= 0"));
            entity.HasIndex(e => e.RubricId);
        });

        modelBuilder.Entity<AssessmentPeerReview>(entity =>
        {
            entity.ToTable("AssessmentPeerReviews");
            entity.HasKey(e => e.Id);
            // Unique (ReviewerUserId, SubmissionId): race protection for peer-review claim (todo 7).
            entity.HasIndex(e => new { e.ReviewerUserId, e.SubmissionId }).IsUnique();
            entity.HasIndex(e => e.SubmissionId);
            entity.HasIndex(e => e.AssessmentId);
            entity.HasIndex(e => e.ReviewerUserId);
            entity.Property(e => e.Feedback).HasColumnType("text");
            entity.Property(e => e.RubricScoresPayload).HasColumnType("jsonb");
            entity.Property(e => e.Score).HasConversion(NullableScoreConverter).HasColumnType("integer");
            entity.ToTable(table => table.HasCheckConstraint(
                "CK_AssessmentPeerReviews_ScoreCanonical",
                "\"Score\" IS NULL OR \"Score\" >= 0"));
        });

        modelBuilder.Entity<InteractiveVideoAssessmentCue>(entity =>
        {
            entity.ToTable("InteractiveVideoAssessmentCues");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.CueId).HasMaxLength(128).IsRequired();
            entity.Property(e => e.CuePositionSeconds).HasPrecision(12, 3);
            entity.HasIndex(e => new { e.AssessmentId, e.ContentId, e.CueId }).IsUnique();
            entity.HasIndex(e => e.ContentId);
            entity.HasOne(e => e.Assessment)
                .WithMany(e => e.InteractiveVideoCues)
                .HasForeignKey(e => e.AssessmentId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }

    private static string FormatAttemptContribution(AttemptContributionMode value) => value switch
    {
        AttemptContributionMode.FirstFinalized => "first-finalized",
        AttemptContributionMode.LastFinalized => "last-finalized",
        AttemptContributionMode.HighestFinalized => "highest-finalized",
        _ => throw new ArgumentOutOfRangeException(nameof(value)),
    };

    private static AttemptContributionMode ParseAttemptContribution(string value) => value switch
    {
        "first-finalized" => AttemptContributionMode.FirstFinalized,
        "last-finalized" => AttemptContributionMode.LastFinalized,
        "highest-finalized" => AttemptContributionMode.HighestFinalized,
        _ => throw new InvalidOperationException($"Unknown attempt contribution mode '{value}'."),
    };

    private static string FormatCompletion(ContentCompletionMode value) => value switch
    {
        ContentCompletionMode.OnSubmit => "on-submit",
        ContentCompletionMode.OnFinalize => "on-finalize",
        ContentCompletionMode.OnRelease => "on-release",
        ContentCompletionMode.OnReleaseAndPass => "on-release-and-pass",
        _ => throw new ArgumentOutOfRangeException(nameof(value)),
    };

    private static ContentCompletionMode ParseCompletion(string value) => value switch
    {
        "on-submit" => ContentCompletionMode.OnSubmit,
        "on-finalize" => ContentCompletionMode.OnFinalize,
        "on-release" => ContentCompletionMode.OnRelease,
        "on-release-and-pass" => ContentCompletionMode.OnReleaseAndPass,
        _ => throw new InvalidOperationException($"Unknown content completion mode '{value}'."),
    };

    private static string FormatRelease(ResultReleaseMode value) => value switch
    {
        ResultReleaseMode.Immediate => "immediate",
        ResultReleaseMode.Manual => "manual",
        ResultReleaseMode.Scheduled => "scheduled",
        _ => throw new ArgumentOutOfRangeException(nameof(value)),
    };

    private static ResultReleaseMode ParseRelease(string value) => value switch
    {
        "immediate" => ResultReleaseMode.Immediate,
        "manual" => ResultReleaseMode.Manual,
        "scheduled" => ResultReleaseMode.Scheduled,
        _ => throw new InvalidOperationException($"Unknown result release mode '{value}'."),
    };
}
