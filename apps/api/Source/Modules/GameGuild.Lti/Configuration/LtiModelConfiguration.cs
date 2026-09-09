using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using GameGuild.Learning.Grading.Contracts;

namespace GameGuild.Lti;

/// <summary>
///     EF Core model configuration for the LTI module.
///     Discovered by ApplicationDbContext via assembly scanning.
/// </summary>
public sealed class LtiModelConfiguration : IModelConfiguration
{
    private static readonly ValueConverter<ScoreValue, int> ScoreConverter =
        new(value => value.Units, value => ScoreValue.FromUnits(value));

    public void Configure(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<LtiDeployment>(entity =>
        {
            entity.ToTable("LtiDeployments");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Issuer).HasMaxLength(512).IsRequired();
            entity.Property(e => e.ClientId).HasMaxLength(256).IsRequired();
            entity.Property(e => e.DeploymentId).HasMaxLength(256).IsRequired();
            entity.Property(e => e.AuthTokenUrl).HasMaxLength(1024).IsRequired();
            entity.Property(e => e.PlatformJwksUrl).HasMaxLength(1024).IsRequired();
            entity.Property(e => e.AuthorizationUrl).HasMaxLength(1024).IsRequired();
            entity.Property(e => e.KeyId).HasMaxLength(128).IsRequired();
            entity.Property(e => e.PrivateKeyPem).HasColumnType("text").IsRequired();
            entity.HasIndex(e => new { e.Issuer, e.ClientId, e.DeploymentId })
                .IsUnique()
                .HasDatabaseName("UX_LtiDeployments_Issuer_Client_Deployment");
        });

        modelBuilder.Entity<LtiLineItemMapping>(entity =>
        {
            entity.ToTable("LtiLineItemMappings");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.LineItemId).HasMaxLength(512).IsRequired();
            entity.Property(e => e.LineItemUrl).HasMaxLength(1024).IsRequired();
            entity.Property(e => e.MaxScore).HasConversion(ScoreConverter).HasColumnType("integer");
            entity.ToTable(table => table.HasCheckConstraint(
                "CK_LtiLineItemMappings_MaxScoreCanonical",
                "\"MaxScore\" > 0"));
            entity.HasIndex(e => e.AssessmentId).IsUnique();
            entity.HasIndex(e => e.DeploymentId);
        });

        modelBuilder.Entity<LtiUserMapping>(entity =>
        {
            entity.ToTable("LtiUserMappings");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Sub).HasMaxLength(256).IsRequired();
            entity.HasIndex(e => new { e.DeploymentId, e.Sub })
                .IsUnique()
                .HasDatabaseName("UX_LtiUserMappings_Deployment_Sub");
            entity.HasIndex(e => e.UserId);
        });
    }
}
