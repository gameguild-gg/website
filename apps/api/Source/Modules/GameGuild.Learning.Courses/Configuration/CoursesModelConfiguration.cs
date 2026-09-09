using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using GameGuild.Learning.Grading.Contracts;

namespace GameGuild.Learning.Courses;

/// <summary>
///     EF Core model configuration for the Learning.Courses module.
///     Discovered by the main API database context via assembly scanning.
/// </summary>
public sealed class CoursesModelConfiguration : IModelConfiguration
{
    private static readonly ValueConverter<ScoreValue, int> ScoreConverter =
        new(value => value.Units, value => ScoreValue.FromUnits(value));
    private static readonly ValueConverter<ScoreValue?, int?> NullableScoreConverter =
        new(value => value.HasValue ? value.Value.Units : null,
            value => value == null ? null : ScoreValue.FromUnits(value.Value));
    private static readonly ValueConverter<PercentValue, int> PercentConverter =
        new(value => value.Units, value => PercentValue.FromUnits(value));
    private static readonly ValueConverter<PercentValue?, int?> NullablePercentConverter =
        new(value => value.HasValue ? value.Value.Units : null,
            value => value == null ? null : PercentValue.FromUnits(value.Value));

    public void Configure(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(
            typeof(Program).Assembly,
            type => type.Namespace?.StartsWith("GameGuild.Learning.Courses", StringComparison.Ordinal) == true);

        ConfigurePercent(modelBuilder.Entity<Program>().Property(value => value.PassingScore));
        ConfigurePercent(modelBuilder.Entity<ProgramUser>().Property(value => value.CompletionPercentage));
        ConfigureNullablePercent(modelBuilder.Entity<ProgramUser>().Property(value => value.FinalGrade));
        ConfigurePercent(modelBuilder.Entity<ProgramEnrollment>().Property(value => value.ProgressPercentage));
        ConfigureNullablePercent(modelBuilder.Entity<ProgramEnrollment>().Property(value => value.FinalGrade));
        ConfigureNullablePercent(modelBuilder.Entity<ContentInteraction>().Property(value => value.ProgressPercentage));
        ConfigureNullableScore(modelBuilder.Entity<ContentInteraction>().Property(value => value.BestScore));
        ConfigureNullablePercent(modelBuilder.Entity<ContentInteractionEvent>().Property(value => value.ProgressPercentage));
        ConfigurePercent(modelBuilder.Entity<ContentProgress>().Property(value => value.ProgressPercentage));
        ConfigureNullableScore(modelBuilder.Entity<ContentProgress>().Property(value => value.Score));
        ConfigureNullableScore(modelBuilder.Entity<ContentProgress>().Property(value => value.MaxScore));
        ConfigureNullablePercent(modelBuilder.Entity<CoursePrerequisite>().Property(value => value.MinimumGrade));
        ConfigureNullableScore(modelBuilder.Entity<ActivityGrade>().Property(value => value.Points));
        ConfigureNullableScore(modelBuilder.Entity<ActivityGrade>().Property(value => value.MaxPoints));

        ConfigureAcademicValueConstraints(modelBuilder);
    }

    private static void ConfigureScore(Microsoft.EntityFrameworkCore.Metadata.Builders.PropertyBuilder<ScoreValue> property) =>
        property.HasConversion(ScoreConverter).HasColumnType("integer");

    private static void ConfigureNullableScore(Microsoft.EntityFrameworkCore.Metadata.Builders.PropertyBuilder<ScoreValue?> property) =>
        property.HasConversion(NullableScoreConverter).HasColumnType("integer");

    private static void ConfigurePercent(Microsoft.EntityFrameworkCore.Metadata.Builders.PropertyBuilder<PercentValue> property) =>
        property.HasConversion(PercentConverter).HasColumnType("integer");

    private static void ConfigureNullablePercent(Microsoft.EntityFrameworkCore.Metadata.Builders.PropertyBuilder<PercentValue?> property) =>
        property.HasConversion(NullablePercentConverter).HasColumnType("integer");

    private static void ConfigureAcademicValueConstraints(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Program>().ToTable(table => table.HasCheckConstraint(
            "CK_programs_PassingScore_Canonical",
            PercentConstraint("PassingScore")));
        modelBuilder.Entity<ProgramUser>().ToTable(table =>
        {
            table.HasCheckConstraint(
                "CK_program_users_CompletionPercentage_Canonical",
                PercentConstraint("CompletionPercentage"));
            table.HasCheckConstraint(
                "CK_program_users_FinalGrade_Canonical",
                PercentConstraint("FinalGrade", nullable: true));
        });
        modelBuilder.Entity<ProgramEnrollment>().ToTable(table =>
        {
            table.HasCheckConstraint(
                "CK_program_enrollments_ProgressPercentage_Canonical",
                PercentConstraint("ProgressPercentage"));
            table.HasCheckConstraint(
                "CK_program_enrollments_FinalGrade_Canonical",
                PercentConstraint("FinalGrade", nullable: true));
        });
        modelBuilder.Entity<ContentInteraction>().ToTable(table =>
        {
            table.HasCheckConstraint(
                "CK_content_interactions_ProgressPercentage_Canonical",
                PercentConstraint("ProgressPercentage", nullable: true));
            table.HasCheckConstraint(
                "CK_content_interactions_BestScore_Canonical",
                ScoreConstraint("BestScore", nullable: true));
        });
        modelBuilder.Entity<ContentInteractionEvent>().ToTable(table => table.HasCheckConstraint(
            "CK_content_interaction_events_ProgressPercentage_Canonical",
            PercentConstraint("ProgressPercentage", nullable: true)));
        modelBuilder.Entity<ContentProgress>().ToTable(table =>
        {
            table.HasCheckConstraint(
                "CK_content_progress_ProgressPercentage_Canonical",
                PercentConstraint("ProgressPercentage"));
            table.HasCheckConstraint(
                "CK_content_progress_Score_Canonical",
                ScoreConstraint("Score", nullable: true));
            table.HasCheckConstraint(
                "CK_content_progress_MaxScore_Canonical",
                ScoreConstraint("MaxScore", nullable: true));
            table.HasCheckConstraint(
                "CK_content_progress_ScoreRange",
                "\"Score\" IS NULL OR \"MaxScore\" IS NULL OR \"Score\" <= \"MaxScore\"");
        });
        modelBuilder.Entity<CoursePrerequisite>().ToTable(table => table.HasCheckConstraint(
            "CK_course_prerequisites_MinimumGrade_Canonical",
            PercentConstraint("MinimumGrade", nullable: true)));
        modelBuilder.Entity<ActivityGrade>().ToTable(table =>
        {
            table.HasCheckConstraint(
                "CK_activity_grades_Points_Canonical",
                ScoreConstraint("Points", nullable: true));
            table.HasCheckConstraint(
                "CK_activity_grades_MaxPoints_Canonical",
                ScoreConstraint("MaxPoints", nullable: true));
            table.HasCheckConstraint(
                "CK_activity_grades_ScoreRange",
                "\"Points\" IS NULL OR \"MaxPoints\" IS NULL OR \"Points\" <= \"MaxPoints\"");
        });
    }

    private static string ScoreConstraint(string column, bool nullable = false)
    {
        var canonical = $"\"{column}\" >= 0";
        return nullable ? $"\"{column}\" IS NULL OR ({canonical})" : canonical;
    }

    private static string PercentConstraint(string column, bool nullable = false)
    {
        var canonical = $"\"{column}\" >= 0 AND \"{column}\" <= 10000";
        return nullable ? $"\"{column}\" IS NULL OR ({canonical})" : canonical;
    }
}
