using Microsoft.EntityFrameworkCore;
using GameGuild.Identity.Tenants;

namespace GameGuild.API.Database;

using AppUser = GameGuild.Identity.Users.User;

internal sealed record PlatformIdentitySeedOptions(
    string AdminEmail,
    string AdminName,
    string AdminUsername,
    string AdminPassword,
    string TenantName,
    string TenantSlug,
    string TenantDescription,
    string AdminTenantRole,
    bool ForcePasswordReset = true);

internal sealed record PlatformIdentitySeedResult(
    AppUser AdminUser,
    Tenant PlatformTenant,
    TenantMember AdminMembership);

internal static class PlatformIdentitySeeder
{
    public static async Task<PlatformIdentitySeedResult> SeedAsync(
        ApplicationDbContext dbContext,
        ILogger? logger,
        PlatformIdentitySeedOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(options);
        Validate(options);

        var adminUser = await EnsureAdminUserAsync(dbContext, logger, options, cancellationToken)
            .ConfigureAwait(false);
        var (tenant, membership) = await EnsurePlatformTenantAsync(
                dbContext,
                adminUser,
                logger,
                options,
                cancellationToken)
            .ConfigureAwait(false);

        return new PlatformIdentitySeedResult(adminUser, tenant, membership);
    }

    private static async Task<AppUser> EnsureAdminUserAsync(
        ApplicationDbContext dbContext,
        ILogger? logger,
        PlatformIdentitySeedOptions options,
        CancellationToken cancellationToken)
    {
        var normalizedEmail = options.AdminEmail.Trim().ToLowerInvariant();
        var adminUser = await dbContext.Set<AppUser>()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(user => user.Email == normalizedEmail, cancellationToken)
            .ConfigureAwait(false);

        if (adminUser is null)
        {
            adminUser = AppUser.CreateWithPassword(
                normalizedEmail,
                options.AdminName.Trim(),
                BCrypt.Net.BCrypt.HashPassword(options.AdminPassword),
                options.AdminUsername.Trim());
            adminUser.VerifyEmail();
            dbContext.Set<AppUser>().Add(adminUser);
            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            logger?.LogInformation("Created application administrator: {Email}", LogRedaction.MaskEmail(normalizedEmail));
            return adminUser;
        }

        var changed = false;
        if (adminUser.IsDeleted)
        {
            adminUser.RestoreUser();
            changed = true;
        }

        if (!adminUser.HasPassword ||
            (options.ForcePasswordReset && !BCrypt.Net.BCrypt.Verify(options.AdminPassword, adminUser.PasswordHash!)))
        {
            adminUser.SetPasswordHash(BCrypt.Net.BCrypt.HashPassword(options.AdminPassword));
            changed = true;
        }

        if (!adminUser.IsEmailVerified)
        {
            adminUser.VerifyEmail();
            changed = true;
        }

        if (!adminUser.IsActive)
        {
            adminUser.Activate();
            changed = true;
        }

        if (adminUser.IsSuspended)
        {
            adminUser.Unsuspend();
            changed = true;
        }

        if (string.IsNullOrWhiteSpace(adminUser.Username))
        {
            adminUser.Username = options.AdminUsername.Trim();
            changed = true;
        }

        if (string.IsNullOrWhiteSpace(adminUser.Name))
        {
            adminUser.Name = options.AdminName.Trim();
            changed = true;
        }

        if (changed)
        {
            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            logger?.LogInformation("Repaired application administrator: {Email}", LogRedaction.MaskEmail(normalizedEmail));
        }

        return adminUser;
    }

    private static async Task<(Tenant Tenant, TenantMember Membership)> EnsurePlatformTenantAsync(
        ApplicationDbContext dbContext,
        AppUser adminUser,
        ILogger? logger,
        PlatformIdentitySeedOptions options,
        CancellationToken cancellationToken)
    {
        var tenantSlug = options.TenantSlug.Trim().ToLowerInvariant();
        var tenants = dbContext.Set<Tenant>();
        var tenant = await tenants
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(item => item.Slug == tenantSlug || item.IsDefault, cancellationToken)
            .ConfigureAwait(false);

        if (tenant is null)
        {
            tenant = new Tenant
            {
                Id = Guid.NewGuid(),
                Name = options.TenantName.Trim(),
                Slug = tenantSlug,
                Description = options.TenantDescription.Trim(),
                AdminEmail = adminUser.Email,
                IsActive = true,
                IsDefault = true
            };
            tenants.Add(tenant);
            logger?.LogInformation("Created default platform tenant: {TenantSlug}", tenantSlug);
        }
        else
        {
            if (tenant.IsDeleted)
            {
                tenant.Restore();
            }

            if (tenant.IsArchived)
            {
                tenant.Unarchive();
            }

            tenant.Name = string.IsNullOrWhiteSpace(tenant.Name) ? options.TenantName.Trim() : tenant.Name;
            tenant.Slug = string.IsNullOrWhiteSpace(tenant.Slug) ? tenantSlug : tenant.Slug;
            tenant.Description = string.IsNullOrWhiteSpace(tenant.Description)
                ? options.TenantDescription.Trim()
                : tenant.Description;
            tenant.AdminEmail = string.IsNullOrWhiteSpace(tenant.AdminEmail) ? adminUser.Email : tenant.AdminEmail;
            tenant.IsActive = true;
            tenant.IsDefault = true;
            tenant.Touch();
        }

        var otherDefaultTenants = await tenants
            .IgnoreQueryFilters()
            .Where(item => item.Id != tenant.Id && item.IsDefault)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        foreach (var otherTenant in otherDefaultTenants)
        {
            otherTenant.IsDefault = false;
            otherTenant.Touch();
        }

        var memberships = dbContext.Set<TenantMember>();
        var membership = await memberships
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(
                item => item.TenantId == tenant.Id && item.UserId == adminUser.Id,
                cancellationToken)
            .ConfigureAwait(false);

        if (membership is null)
        {
            membership = new TenantMember
            {
                Id = Guid.NewGuid(),
                TenantId = tenant.Id,
                UserId = adminUser.Id,
                Role = options.AdminTenantRole.Trim(),
                IsActive = true,
                JoinedAt = SystemClock.UtcNow,
                Metadata = """{"bootstrap":true,"scope":"platform"}"""
            };
            memberships.Add(membership);
            logger?.LogInformation("Created platform administrator membership: {Email}", adminUser.Email);
        }
        else
        {
            if (membership.IsDeleted)
            {
                membership.Restore();
            }

            membership.Role = options.AdminTenantRole.Trim();
            membership.Activate();
        }

        if (!await dbContext.Set<TenantSettings>()
                .IgnoreQueryFilters()
                .AnyAsync(item => item.TenantId == tenant.Id, cancellationToken)
                .ConfigureAwait(false))
        {
            dbContext.Set<TenantSettings>().Add(TenantSettings.CreateDefault(tenant.Id));
        }

        if (!await dbContext.Set<TenantStatistics>()
                .IgnoreQueryFilters()
                .AnyAsync(item => item.TenantId == tenant.Id, cancellationToken)
                .ConfigureAwait(false))
        {
            dbContext.Set<TenantStatistics>().Add(new TenantStatistics
            {
                Id = Guid.NewGuid(),
                TenantId = tenant.Id,
                StatisticDate = SystemClock.UtcNow.Date,
                TotalMembers = 1,
                ActiveMembers = 1,
                NewMembers = 1
            });
        }

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return (tenant, membership);
    }

    private static void Validate(PlatformIdentitySeedOptions options)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(options.AdminEmail);
        ArgumentException.ThrowIfNullOrWhiteSpace(options.AdminName);
        ArgumentException.ThrowIfNullOrWhiteSpace(options.AdminUsername);
        ArgumentException.ThrowIfNullOrWhiteSpace(options.AdminPassword);
        ArgumentException.ThrowIfNullOrWhiteSpace(options.TenantName);
        ArgumentException.ThrowIfNullOrWhiteSpace(options.TenantSlug);
        ArgumentException.ThrowIfNullOrWhiteSpace(options.TenantDescription);
        ArgumentException.ThrowIfNullOrWhiteSpace(options.AdminTenantRole);
    }
}
