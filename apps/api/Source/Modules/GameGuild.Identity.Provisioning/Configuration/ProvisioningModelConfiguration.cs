using Microsoft.EntityFrameworkCore;

namespace GameGuild.Identity.Provisioning;

/// <summary>
///     EF Core model configuration for the Provisioning module. Discovered by the
///     thin-shell <c>ApplicationDbContext</c> through <c>IModelConfiguration</c> assembly scanning.
/// </summary>
public sealed class ProvisioningModelConfiguration : IModelConfiguration
{
    public void Configure(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(
            typeof(ScimProvisioningToken).Assembly,
            type => type.Namespace?.StartsWith("GameGuild.Identity.Provisioning", StringComparison.Ordinal) == true);
    }
}
