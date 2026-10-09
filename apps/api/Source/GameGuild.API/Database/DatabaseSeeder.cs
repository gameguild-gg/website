using Microsoft.AspNetCore.Identity;
using GameGuild.Identity.Authorization;
using GameGuild.Identity.Tenants;

namespace GameGuild.API.Database;

// Type aliases for ASP.NET Core Identity
using Role = IdentityRole;
using LegacyIdentityUser = IdentityUser;

/// <summary>
///     Database seeder for default roles and admin user
/// </summary>
public static class DatabaseSeeder
{
    /// <summary>
    ///     Seeds the database with default roles and admin user.
    ///     Note: Database creation/migration is handled by DatabaseInitializationService.
    /// </summary>
    public static async Task SeedAsync(IServiceProvider serviceProvider)
    {
        // Ensure ApplicationDbContext is registered (validates DI configuration)
        var dbContext = serviceProvider.GetRequiredService<ApplicationDbContext>();
        var policyDefinitionSeeder = serviceProvider.GetRequiredService<PolicyDefinitionSeeder>();
        await policyDefinitionSeeder.SeedAsync().ConfigureAwait(false);

        // Try to get Identity managers - they may not be registered in all configurations
        var userManager = serviceProvider.GetService<UserManager<LegacyIdentityUser>>();
        var roleManager = serviceProvider.GetService<RoleManager<Role>>();
        var logger = serviceProvider.GetService<ILogger<ApplicationDbContext>>();
        var configuration = serviceProvider.GetService<IConfiguration>();

        // Seed roles if RoleManager is available
        if (roleManager != null)
        {
            await SeedRolesAsync(roleManager, logger).ConfigureAwait(false);
        }
        else
        {
            logger?.LogInformation("RoleManager not registered - skipping legacy role seeding");
        }

        await PlatformIdentitySeeder.SeedAsync(
                dbContext,
                logger,
                CreatePlatformIdentitySeedOptions(configuration))
            .ConfigureAwait(false);

        // Seed legacy ASP.NET Identity admin user if UserManager is available.
        // This is kept for compatibility with older identity surfaces.
        if (userManager != null)
        {
            await SeedLegacyIdentityAdminUserAsync(userManager, logger, configuration).ConfigureAwait(false);
        }
        else
        {
            logger?.LogInformation("UserManager not registered - skipping legacy admin user seeding");
        }
    }

    private static PlatformIdentitySeedOptions CreatePlatformIdentitySeedOptions(IConfiguration? configuration) => new(
        configuration?["Seed:AdminEmail"] ?? "admin@game-guild.com",
        configuration?["Seed:AdminName"] ?? "Game Guild Admin",
        configuration?["Seed:AdminUsername"] ?? "admin",
        configuration?["Seed:AdminPassword"] ?? "Admin123!",
        configuration?["Seed:DefaultTenantName"] ?? "GameGuild Platform",
        configuration?["Seed:DefaultTenantSlug"] ?? "gameguild-platform",
        configuration?["Seed:DefaultTenantDescription"] ?? "Default platform tenant for GameGuild administration.",
        configuration?["Seed:AdminTenantRole"] ?? "SystemAdmin",
        ForcePasswordReset: !string.IsNullOrWhiteSpace(configuration?["Seed:AdminPassword"]));

    private static async Task SeedRolesAsync(RoleManager<Role> roleManager, ILogger? logger)
    {
        var roles = new[] { "Admin", "User", "Manager" };

        foreach (var roleName in roles)
        {
            if (!await roleManager.RoleExistsAsync(roleName))
            {
                var role = new IdentityRole
                {
                    Id = Guid.NewGuid().ToString(),
                    Name = roleName,
                    NormalizedName = roleName.ToUpper()
                };

                var result = await roleManager.CreateAsync(role).ConfigureAwait(false);
                if (result.Succeeded)
                {
                    logger?.LogInformation("  Created role: {RoleName}", roleName);
                }
            }
        }
    }

    private static async Task SeedLegacyIdentityAdminUserAsync(UserManager<LegacyIdentityUser> userManager, ILogger? logger, IConfiguration? configuration = null)
    {
        const string adminEmail = "admin@game-guild.com";
        var adminPassword = configuration?["Seed:AdminPassword"] ?? "Admin123!";
        if (adminPassword == "Admin123!")
        {
            logger?.LogWarning("Using default admin password. Set 'Seed:AdminPassword' in configuration for production");
        }

        if (await userManager.FindByEmailAsync(adminEmail) == null)
        {
            var adminUser = new LegacyIdentityUser
            {
                Id = Guid.NewGuid().ToString(),
                UserName = adminEmail,
                Email = adminEmail,
                EmailConfirmed = true
            };

            var result = await userManager.CreateAsync(adminUser, adminPassword).ConfigureAwait(false);

            if (result.Succeeded)
            {
                await userManager.AddToRoleAsync(adminUser, "Admin").ConfigureAwait(false);
                logger?.LogInformation("  Created admin user: {Email}", LogRedaction.MaskEmail(adminEmail));
            }
            else
            {
                logger?.LogWarning("  Failed to create admin user: {Errors}",
                    string.Join(", ", result.Errors.Select(e => e.Description)));
            }
        }
    }
}
