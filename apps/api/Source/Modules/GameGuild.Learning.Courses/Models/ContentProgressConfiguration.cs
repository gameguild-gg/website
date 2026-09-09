using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GameGuild.Learning.Courses;

/// <summary>EF Core configuration for per-content progress inside a program enrollment.</summary>
public class ContentProgressConfiguration : IEntityTypeConfiguration<ContentProgress>
{
    public void Configure(EntityTypeBuilder<ContentProgress> builder)
    {
        builder.ToTable("content_progress");

        builder.HasKey(progress => progress.Id);

        builder.HasIndex(progress => new { progress.UserId, progress.ContentId }).IsUnique();
        builder.HasIndex(progress => progress.UserId);
        builder.HasIndex(progress => progress.ContentId);
        builder.HasIndex(progress => progress.CompletionStatus);
        builder.HasIndex(progress => progress.CompletedAt);

        builder.Property(progress => progress.ProgressData).HasColumnType("jsonb");

        builder.HasOne(progress => progress.Content)
            .WithMany()
            .HasForeignKey(progress => progress.ContentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(progress => progress.ProgramEnrollment)
            .WithMany()
            .HasForeignKey(progress => progress.ProgramEnrollmentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(progress => progress.User)
            .WithMany()
            .HasForeignKey(progress => progress.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
