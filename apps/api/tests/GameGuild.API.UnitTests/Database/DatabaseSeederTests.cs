using FluentAssertions;
using GameGuild.API.Database;
using GameGuild.Identity.Authorization;
using GameGuild.Identity.Tenants;
using GameGuild.Identity.Users;
using GameGuild.Projects;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace GameGuild.API.UnitTests.Database;

public sealed class DatabaseSeederTests
{
    [Fact]
    public async Task SeedAsync_Should_Not_Log_Warnings_When_LegacyIdentityManagers_Are_Not_Registered()
    {
        var services = new ServiceCollection();
        var logger = new CapturingLogger<ApplicationDbContext>();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Seed:AdminPassword"] = "UnitTestAdmin123!"
            })
            .Build();

        _ = typeof(GameGuild.Identity.Users.User);

        services.AddSingleton<IConfiguration>(configuration);
        services.AddSingleton<ILogger<ApplicationDbContext>>(logger);
        services.AddDbContext<ApplicationDbContext>(options => options.UseInMemoryDatabase(Guid.NewGuid().ToString()));
        AddPolicySeeder(services);

        await using var provider = services.BuildServiceProvider();

        await DatabaseSeeder.SeedAsync(provider);

        logger.Messages.Where(message => message.Level >= LogLevel.Warning).Should().BeEmpty();
    }

    [Fact]
    public async Task SeedAsync_Should_Create_The_Canonical_SystemAdmin_Policy()
    {
        var services = CreateSeederServices("UnitTestAdmin123!");
        await using var provider = services.BuildServiceProvider();

        await DatabaseSeeder.SeedAsync(provider);

        var dbContext = provider.GetRequiredService<ApplicationDbContext>();
        var policy = await dbContext.Set<PolicyDefinitionEntity>()
            .SingleAsync(candidate => candidate.PolicyName == Policies.SystemAdmin && candidate.TenantId == null);

        policy.RequiredRolesJson.Should().Be("[\"SystemAdmin\"]");
        policy.PolicyVersion.Should().Be(PolicyDefinitionSeeder.CurrentPolicyVersion);
    }

    [Fact]
    public async Task SeedAsync_Should_Create_Default_Tenant_And_Admin_SuperAdmin_Membership()
    {
        var services = CreateSeederServices("UnitTestAdmin123!");
        await using var provider = services.BuildServiceProvider();

        await DatabaseSeeder.SeedAsync(provider);
        await DatabaseSeeder.SeedAsync(provider);

        var dbContext = provider.GetRequiredService<ApplicationDbContext>();
        var adminUser = await dbContext.Set<User>().SingleAsync(user => user.Email == "admin@game-guild.com");
        var tenant = await dbContext.Set<Tenant>().SingleAsync(tenant => tenant.IsDefault);
        var membership = await dbContext.Set<TenantMember>()
            .SingleAsync(member => member.UserId == adminUser.Id && member.TenantId == tenant.Id);

        tenant.Slug.Should().Be("gameguild-platform");
        tenant.IsActive.Should().BeTrue();
        membership.Role.Should().Be("SystemAdmin");
        membership.IsActive.Should().BeTrue();

        var settingsCount = await dbContext.Set<TenantSettings>()
            .CountAsync(settings => settings.TenantId == tenant.Id);
        var statisticsCount = await dbContext.Set<TenantStatistics>()
            .CountAsync(statistics => statistics.TenantId == tenant.Id);

        settingsCount.Should().Be(1);
        statisticsCount.Should().Be(1);
    }

    [Fact]
    public async Task SeedAsync_Should_Not_Create_Demo_Showcase_Content()
    {
        var services = CreateSeederServices("UnitTestAdmin123!");
        await using var provider = services.BuildServiceProvider();

        await DatabaseSeeder.SeedAsync(provider);
        await DatabaseSeeder.SeedAsync(provider);

        var dbContext = provider.GetRequiredService<ApplicationDbContext>();

        var showcaseProjects = await dbContext.Set<Project>()
            .IgnoreQueryFilters()
            .Where(project => project.Slug.StartsWith("gameguild-showcase-"))
            .ToListAsync();

        showcaseProjects.Should().BeEmpty(
            "production database seeding must not create product demonstration content");
    }

    private static ServiceCollection CreateSeederServices(string adminPassword)
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Seed:AdminPassword"] = adminPassword
            })
            .Build();

        services.AddSingleton<IConfiguration>(configuration);
        services.AddSingleton<ILogger<ApplicationDbContext>, CapturingLogger<ApplicationDbContext>>();
        services.AddDbContext<ApplicationDbContext>(options => options.UseInMemoryDatabase(Guid.NewGuid().ToString()));
        AddPolicySeeder(services);

        return services;
    }

    private static void AddPolicySeeder(IServiceCollection services)
    {
        services.AddLogging();
        services.AddScoped<IApplicationDbContext>(provider => provider.GetRequiredService<ApplicationDbContext>());
        services.AddScoped<IPolicyDefinitionRepository, PolicyDefinitionRepository>();
        services.AddScoped<PolicyDefinitionSeeder>();
    }

    private sealed class CapturingLogger<T> : ILogger<T>
    {
        public List<(LogLevel Level, string Text)> Messages { get; } = [];

        public IDisposable BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            Messages.Add((logLevel, formatter(state, exception)));
        }

        private sealed class NullScope : IDisposable
        {
            public static NullScope Instance { get; } = new();

            public void Dispose() { }
        }
    }
}
