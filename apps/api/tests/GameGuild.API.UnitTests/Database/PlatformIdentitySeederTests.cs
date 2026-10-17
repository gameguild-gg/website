using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using GameGuild.API.Database;
using GameGuild.Identity.Tenants;
using GameGuild.Identity.Users;
using GameGuild.Identity.Authentication;
using Moq;

namespace GameGuild.API.UnitTests.Database;

public sealed class PlatformIdentitySeederTests
{
    [Fact]
    public async Task SeedAsync_CreatesOneUsableAdministratorAndDefaultTenantIdempotently()
    {
        await using var dbContext = CreateDbContext();
        var options = CreateOptions();

        var first = await PlatformIdentitySeeder.SeedAsync(
            dbContext,
            NullLogger<ApplicationDbContext>.Instance,
            options);
        var second = await PlatformIdentitySeeder.SeedAsync(
            dbContext,
            NullLogger<ApplicationDbContext>.Instance,
            options);

        second.AdminUser.Id.Should().Be(first.AdminUser.Id);
        second.PlatformTenant.Id.Should().Be(first.PlatformTenant.Id);
        (await dbContext.Set<User>().CountAsync()).Should().Be(1);
        (await dbContext.Set<Tenant>().CountAsync()).Should().Be(1);
        (await dbContext.Set<TenantMember>().CountAsync()).Should().Be(1);
        (await dbContext.Set<TenantSettings>().CountAsync()).Should().Be(1);
        (await dbContext.Set<TenantStatistics>().CountAsync()).Should().Be(1);

        first.AdminUser.Email.Should().Be("admin@product.example");
        first.AdminUser.IsActive.Should().BeTrue();
        first.AdminUser.IsEmailVerified.Should().BeTrue();
        BCrypt.Net.BCrypt.Verify("StrongAdminPassword123!", first.AdminUser.PasswordHash).Should().BeTrue();
        first.PlatformTenant.Slug.Should().Be("product-platform");
        first.AdminMembership.Role.Should().Be("SystemAdmin");
        first.AdminMembership.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task SeedAsync_RepairsExistingIdentityAndDemotesOtherDefaultTenants()
    {
        await using var dbContext = CreateDbContext();
        var options = CreateOptions();
        var initial = await PlatformIdentitySeeder.SeedAsync(
            dbContext,
            NullLogger<ApplicationDbContext>.Instance,
            options);

        initial.AdminUser.SoftDelete();
        initial.AdminUser.SetPasswordHash(BCrypt.Net.BCrypt.HashPassword("OutdatedPassword123!"));
        initial.AdminUser.IsEmailVerified = false;
        initial.AdminUser.Deactivate();
        initial.AdminUser.Suspend();
        initial.AdminUser.Username = " ";
        initial.AdminUser.Name = " ";

        initial.PlatformTenant.SoftDelete();
        initial.PlatformTenant.Archive("test repair");
        initial.PlatformTenant.Name = " ";
        initial.PlatformTenant.Slug = " ";
        initial.PlatformTenant.Description = " ";
        initial.PlatformTenant.AdminEmail = " ";

        initial.AdminMembership.SoftDelete();
        initial.AdminMembership.Deactivate("test repair");
        initial.AdminMembership.Role = "Owner";

        var competingDefault = new Tenant
        {
            Id = Guid.NewGuid(),
            Name = "Competing default",
            Slug = "competing-default",
            Description = "Should be demoted.",
            AdminEmail = "other@product.example",
            IsActive = true,
            IsDefault = true
        };
        dbContext.Set<Tenant>().Add(competingDefault);
        await dbContext.SaveChangesAsync();

        var repaired = await PlatformIdentitySeeder.SeedAsync(
            dbContext,
            NullLogger<ApplicationDbContext>.Instance,
            options);

        repaired.AdminUser.IsDeleted.Should().BeFalse();
        repaired.AdminUser.IsActive.Should().BeTrue();
        repaired.AdminUser.IsSuspended.Should().BeFalse();
        repaired.AdminUser.IsEmailVerified.Should().BeTrue();
        repaired.AdminUser.Username.Should().Be("admin");
        repaired.AdminUser.Name.Should().Be("Product Administrator");
        BCrypt.Net.BCrypt.Verify(options.AdminPassword, repaired.AdminUser.PasswordHash).Should().BeTrue();

        repaired.PlatformTenant.IsDeleted.Should().BeFalse();
        repaired.PlatformTenant.IsArchived.Should().BeFalse();
        repaired.PlatformTenant.IsActive.Should().BeTrue();
        repaired.PlatformTenant.IsDefault.Should().BeTrue();
        repaired.PlatformTenant.Name.Should().Be("Product Platform");
        repaired.PlatformTenant.Slug.Should().Be("product-platform");
        repaired.PlatformTenant.Description.Should().Be("Default administration tenant.");
        repaired.PlatformTenant.AdminEmail.Should().Be("admin@product.example");
        competingDefault.IsDefault.Should().BeFalse();

        repaired.AdminMembership.IsDeleted.Should().BeFalse();
        repaired.AdminMembership.IsActive.Should().BeTrue();
        repaired.AdminMembership.LeftAt.Should().BeNull();
        repaired.AdminMembership.LeaveReason.Should().BeNull();
        repaired.AdminMembership.Role.Should().Be("SystemAdmin");
        (await dbContext.Set<TenantSettings>().CountAsync(item => item.TenantId == repaired.PlatformTenant.Id)).Should().Be(1);
        (await dbContext.Set<TenantStatistics>().CountAsync(item => item.TenantId == repaired.PlatformTenant.Id)).Should().Be(1);
    }

    [Fact]
    public async Task SeedAsync_RepairsInactivePasswordlessAdministratorWithoutLogger()
    {
        await using var dbContext = CreateDbContext();
        var options = CreateOptions();
        var initial = await PlatformIdentitySeeder.SeedAsync(dbContext, null, options);
        initial.AdminUser.PasswordHash = null;
        initial.AdminUser.Deactivate();
        await dbContext.SaveChangesAsync();

        var repaired = await PlatformIdentitySeeder.SeedAsync(dbContext, null, options);

        repaired.AdminUser.IsActive.Should().BeTrue();
        repaired.AdminUser.HasPassword.Should().BeTrue();
        BCrypt.Net.BCrypt.Verify(options.AdminPassword, repaired.AdminUser.PasswordHash).Should().BeTrue();
    }

    [Theory]
    [MemberData(nameof(InvalidOptions))]
    public async Task SeedAsync_RejectsMissingRequiredOptions(string optionName)
    {
        await using var dbContext = CreateDbContext();
        var options = optionName switch
        {
            nameof(PlatformIdentitySeedOptions.AdminEmail) => CreateOptions() with { AdminEmail = " " },
            nameof(PlatformIdentitySeedOptions.AdminName) => CreateOptions() with { AdminName = " " },
            nameof(PlatformIdentitySeedOptions.AdminUsername) => CreateOptions() with { AdminUsername = " " },
            nameof(PlatformIdentitySeedOptions.AdminPassword) => CreateOptions() with { AdminPassword = " " },
            nameof(PlatformIdentitySeedOptions.TenantName) => CreateOptions() with { TenantName = " " },
            nameof(PlatformIdentitySeedOptions.TenantSlug) => CreateOptions() with { TenantSlug = " " },
            nameof(PlatformIdentitySeedOptions.TenantDescription) => CreateOptions() with { TenantDescription = " " },
            nameof(PlatformIdentitySeedOptions.AdminTenantRole) => CreateOptions() with { AdminTenantRole = " " },
            _ => throw new ArgumentOutOfRangeException(nameof(optionName), optionName, null)
        };

        var action = () => PlatformIdentitySeeder.SeedAsync(dbContext, null, options);

        await action.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task SeedAsync_RejectsNullDependencies()
    {
        await using var dbContext = CreateDbContext();
        var options = CreateOptions();

        var nullContext = () => PlatformIdentitySeeder.SeedAsync(null!, null, options);
        var nullOptions = () => PlatformIdentitySeeder.SeedAsync(dbContext, null, null!);

        await nullContext.Should().ThrowAsync<ArgumentNullException>();
        await nullOptions.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task SeedAsync_ProvisionsTotpMfaConfiguration_WhenSecretIsProvided()
    {
        await using var dbContext = CreateDbContext();
        var options = CreateOptions() with { AdminTotpSecret = "SECRET123" };

        var mockEncryption = new Mock<IEncryptionService>();
        mockEncryption.Setup(x => x.Encrypt("SECRET123")).Returns("ENCRYPTED_SECRET");

        var mockMfaRepo = new Mock<IUserMfaConfigurationRepository>();
        mockMfaRepo.Setup(x => x.GetByUserIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserMfaConfiguration)null!);

        var result = await PlatformIdentitySeeder.SeedAsync(
            dbContext,
            NullLogger<ApplicationDbContext>.Instance,
            options,
            mockEncryption.Object,
            mockMfaRepo.Object);

        mockMfaRepo.Verify(x => x.CreateAsync(It.Is<UserMfaConfiguration>(c =>
            c.UserId == result.AdminUser.Id &&
            c.TotpSecretKey == "ENCRYPTED_SECRET" &&
            c.IsEnabled == true &&
            c.IsSetupComplete == true &&
            c.PreferredMethod == MfaMethod.Totp
        ), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SeedAsync_DoesNotProvisionMfa_WhenSecretIsMissing()
    {
        await using var dbContext = CreateDbContext();
        var options = CreateOptions(); // AdminTotpSecret is null

        var mockEncryption = new Mock<IEncryptionService>();
        var mockMfaRepo = new Mock<IUserMfaConfigurationRepository>();

        await PlatformIdentitySeeder.SeedAsync(
            dbContext,
            NullLogger<ApplicationDbContext>.Instance,
            options,
            mockEncryption.Object,
            mockMfaRepo.Object);

        mockMfaRepo.Verify(x => x.CreateAsync(It.IsAny<UserMfaConfiguration>(), It.IsAny<CancellationToken>()), Times.Never);
        mockMfaRepo.Verify(x => x.UpdateAsync(It.IsAny<UserMfaConfiguration>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    public static TheoryData<string> InvalidOptions => new()
    {
        nameof(PlatformIdentitySeedOptions.AdminEmail),
        nameof(PlatformIdentitySeedOptions.AdminName),
        nameof(PlatformIdentitySeedOptions.AdminUsername),
        nameof(PlatformIdentitySeedOptions.AdminPassword),
        nameof(PlatformIdentitySeedOptions.TenantName),
        nameof(PlatformIdentitySeedOptions.TenantSlug),
        nameof(PlatformIdentitySeedOptions.TenantDescription),
        nameof(PlatformIdentitySeedOptions.AdminTenantRole)
    };

    private static ApplicationDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"platform-identity-seeder-{Guid.NewGuid()}")
            .Options;
        return new ApplicationDbContext(options);
    }

    private static PlatformIdentitySeedOptions CreateOptions() => new(
        "admin@product.example",
        "Product Administrator",
        "admin",
        "StrongAdminPassword123!",
        "Product Platform",
        "product-platform",
        "Default administration tenant.",
        "SystemAdmin");
}
