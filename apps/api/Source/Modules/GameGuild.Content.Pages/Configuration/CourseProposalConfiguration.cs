using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GameGuild.Content.Pages;

/// <summary>EF Core configuration for <see cref="CourseProposal"/>.</summary>
public class CourseProposalConfiguration : IEntityTypeConfiguration<CourseProposal>
{
    public void Configure(EntityTypeBuilder<CourseProposal> builder)
    {
        builder.Property(proposal => proposal.Slug)
            .HasMaxLength(160)
            .IsRequired();

        builder.Property(proposal => proposal.Status)
            .HasMaxLength(40)
            .HasDefaultValue(CourseProposalStatuses.Submitted)
            .IsRequired();

        builder.Property(proposal => proposal.CourseTitle).HasMaxLength(200).IsRequired();
        builder.Property(proposal => proposal.Pitch).HasMaxLength(4000);
        builder.Property(proposal => proposal.Audience).HasMaxLength(2000);
        builder.Property(proposal => proposal.ProposerName).HasMaxLength(120);
        builder.Property(proposal => proposal.ProposerEmail).HasMaxLength(200);
        builder.Property(proposal => proposal.Metadata).HasMaxLength(4000);

        builder.HasIndex(proposal => proposal.Slug);
        builder.HasIndex(proposal => new { proposal.Status, proposal.CreatedAt });
    }
}
