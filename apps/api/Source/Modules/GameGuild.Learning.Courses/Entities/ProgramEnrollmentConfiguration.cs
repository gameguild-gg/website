using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GameGuild.Learning.Courses;

/// <summary>EF Core configuration for detailed program enrollment records.</summary>
public class ProgramEnrollmentConfiguration : IEntityTypeConfiguration<ProgramEnrollment>
{
    public void Configure(EntityTypeBuilder<ProgramEnrollment> builder)
    {
        builder.ToTable("program_enrollments");

        builder.HasKey(enrollment => enrollment.Id);

        builder.HasIndex(enrollment => new { enrollment.UserId, enrollment.ProgramId }).IsUnique();
        builder.HasIndex(enrollment => enrollment.UserId);
        builder.HasIndex(enrollment => enrollment.ProgramId);
        builder.HasIndex(enrollment => enrollment.EnrollmentStatus);
        builder.HasIndex(enrollment => enrollment.EnrolledAt);
        builder.HasIndex(enrollment => enrollment.CompletedAt);
        builder.HasIndex(enrollment => enrollment.TenantId);

        builder.HasOne(enrollment => enrollment.Program)
            .WithMany()
            .HasForeignKey(enrollment => enrollment.ProgramId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(enrollment => enrollment.User)
            .WithMany()
            .HasForeignKey(enrollment => enrollment.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
