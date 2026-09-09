using GameGuild.API.Authorization;
using GameGuild.API.Controllers;
using GameGuild.API.Database;
using GameGuild.API.HealthChecks;
using GameGuild.API.HostedServices;
using GameGuild.Commerce.Billing;
using GameGuild.Commerce.Payments;
using GameGuild.Compliance.FERPA;
using GameGuild.Compliance.KYC;
using GameGuild.Economy;
using GameGuild.Economy.AdRewards;
using GameGuild.Economy.Bounties;
using GameGuild.Economy.Marketplace;
using GameGuild.Economy.Payouts;
using GameGuild.Economy.Treasury;
using GameGuild.GameJams;
using GameGuild.Identity.Authorization;
using GameGuild.LaunchPad;
using GameGuild.Learning.Assessments;
using GameGuild.Learning.Certificates;
using GameGuild.Learning.Cohorts;
using GameGuild.Learning.Courses;
using GameGuild.Learning.Enrollments;
using GameGuild.Learning.Experience.Discovery;
using GameGuild.Learning.Experience.LearningPaths;
using GameGuild.Learning.Experience.Recommendations;
using GameGuild.Learning.Workspaces;
using GameGuild.Lti;
using GameGuild.ProjectWork;
using GameGuild.Projects;
using GameGuild.Social.Blog;
using GameGuild.Social.Follows;
using GameGuild.Social.Feed;
using GameGuild.Social.Groups;
using GameGuild.Social.Profiles;
using GameGuild.Social.Reactions;
using GameGuild.Teams;
using GameGuild.TestingLab;
using Microsoft.Extensions.Options;
using Swashbuckle.AspNetCore.SwaggerGen;
using LearningSocialModule = GameGuild.Learning.Experience.Social.SocialModule;

namespace GameGuild.API.Setup;

internal sealed class ApiProductComposition : IApiProductComposition
{
    public static ApiProductComposition Instance { get; } = new();

    private ApiProductComposition() { }

    public string ApplicationName => "GameGuild";

    public IReadOnlyList<string> EnabledModules { get; } =
    [
        "Compliance.FERPA",
        "Economy",
        "Economy.AdRewards",
        "Economy.Bounties",
        "Economy.Marketplace",
        "Economy.Payouts",
        "Economy.Treasury",
        "GameJams",
        "LaunchPad",
        "Learning.Assessments",
        "Learning.Certificates",
        "Learning.Cohorts",
        "Learning.Courses",
        "Learning.Enrollments",
        "Learning.Experience.Discovery",
        "Learning.Experience.LearningPaths",
        "Learning.Experience.Recommendations",
        "Learning.Experience.Social",
        "Learning.TestingLab",
        "Learning.Workspaces",
        "Lti",
        "Projects",
        "ProjectWork",
        "Social.Announcements",
        "Social.Blog",
        "Social.Feed",
        "Social.Groups",
        "Social.Posts",
        "Social.Profiles",
        "Social.Reactions",
        "Teams",
        "TestingLab"
    ];

    public IReadOnlyList<string> DisabledModules { get; } =
    [
        "Compliance.FinancialCrime",
        "TrustSafety"
    ];

    public void ConfigureServices(WebApplicationBuilder builder)
    {
        builder.Services.AddFerpaModule();
        builder.Services.AddKycComposition(builder.Configuration);
        builder.Services.AddEconomyCapabilityComposition(builder.Configuration);
        builder.Services.AddEconomyCoreComposition(builder.Configuration);
        builder.Services.AddScoped<global::GameGuild.Compliance.KYC.IKycEvidenceStore, global::GameGuild.API.Core.Integration.EconomyKycEvidenceStore>();
        builder.Services.AddScoped<IEconomyStepUpExecutor, EconomyStepUpExecutor>();
        builder.Services.AddScoped<IAdRewardRequestRiskContextResolver, AdRewardRequestRiskContextResolver>();
        builder.Services.AddAdRewardsComposition(builder.Configuration);
        builder.Services.AddBountiesComposition(builder.Configuration);
        builder.Services.AddMarketplaceComposition(builder.Configuration);
        builder.Services.AddPayoutsComposition(builder.Configuration);
        builder.Services.AddTreasuryComposition(builder.Configuration);
        builder.Services.AddOptions<EconomyDurableWorkerOptions>()
            .Bind(builder.Configuration.GetSection(EconomyDurableWorkerOptions.SectionName));
        builder.Services.AddHostedService<EconomyDurableWorker>();

        builder.Services.AddFollowsModule();
        new GameGuild.Social.Announcements.AnnouncementsModule().ConfigureServices(builder.Services, builder.Configuration);
        builder.Services.AddCoursesModule();
        builder.Services.AddAssessmentsModule();
        builder.Services.AddLearningEnrollmentsModule();
        builder.Services.AddCohortsModule();
        builder.Services.AddCertificatesModule();
        builder.Services.AddLearningWorkspacesModule();
        builder.Services.AddLtiModule();
        builder.Services.AddDiscoveryModule();
        builder.Services.AddLearningPathsModule();
        builder.Services.AddRecommendationsModule();
        LearningSocialModule.AddSocialModule(builder.Services);

        builder.Services.AddSocialProfilesModule();
        builder.Services.AddSocialBlogModule();
        builder.Services.AddSocialFeedModule();
        builder.Services.AddSocialGroupsModule();
        builder.Services.AddSocialReactionsModule();

        builder.Services.AddGameJamsModule();
        builder.Services.AddTeamsModule();
        builder.Services.AddProjectsModule();
        builder.Services.AddProjectWorkModule();
        builder.Services.AddTestingLabModule(builder.Configuration);
        builder.Services.AddLaunchPadModule();
        builder.Services.AddHealthChecks()
            .AddCheck<PaymentProviderReadinessHealthCheck>(
                "payment-provider",
                tags: ["ready", "dependency"])
            .AddCheck<EconomyCapabilityReadinessHealthCheck>(
                "economy-capabilities",
                tags: ["dependency"])
            .AddCheck<BillingInboxReadinessHealthCheck>(
                "billing-inbox",
                tags: ["ready", "dependency"]);
    }

    public void ConfigureOpenApi(SwaggerGenOptions options)
    {
        options.SchemaFilter<LegacyAssessmentTypeSchemaFilter>();
        options.SchemaFilter<LegacyProgramContentTypeSchemaFilter>();
    }

    public async Task SeedAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        await DatabaseSeeder.SeedAsync(services).ConfigureAwait(false);
        await services.GetRequiredService<PolicyDefinitionSeeder>()
            .SeedAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<bool> InitializeAsync(
        WebApplication app,
        bool databaseInitialized,
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken)
    {
        ValidatePaymentProviderConfiguration(app);

        var explicitImport = arguments.Any(argument =>
            string.Equals(argument, "--import-snapshot-courses", StringComparison.OrdinalIgnoreCase));

        var forceImport = explicitImport || ShouldForceImportSnapshotCourses(app.Configuration);

        if (explicitImport)
        {
            await ImportSnapshotCoursesAsync(app, "Snapshot course import complete", failOnError: true, force: true, cancellationToken)
                .ConfigureAwait(false);
            return false;
        }

        if (databaseInitialized && ShouldImportSnapshotCourses(app.Configuration))
        {
            await ImportSnapshotCoursesAsync(
                    app,
                    "Snapshot course startup import complete",
                    failOnError: false,
                    force: forceImport,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        return true;
    }

    private static void ValidatePaymentProviderConfiguration(WebApplication app)
    {
        var stripeGatewayOptions = app.Services.GetRequiredService<IOptions<StripeGatewayOptions>>().Value;
        var billingConfiguration = app.Services.GetRequiredService<IOptions<BillingConfiguration>>().Value;
        StripeProviderConfigurationGuard.ThrowIfInvalid(
            stripeGatewayOptions,
            billingConfiguration,
            app.Environment.EnvironmentName,
            app.Logger);
    }

    private static bool ShouldImportSnapshotCourses(IConfiguration configuration)
    {
        var configuredValue = configuration["SeedData:ImportSnapshotCourses"]
            ?? Environment.GetEnvironmentVariable("SEED_SNAPSHOT_COURSES");

        return bool.TryParse(configuredValue, out var enabled) && enabled;
    }

    private static bool ShouldForceImportSnapshotCourses(IConfiguration configuration)
    {
        var configuredValue = configuration["SeedData:ForceImportSnapshotCourses"]
            ?? Environment.GetEnvironmentVariable("FORCE_SEED_SNAPSHOT_COURSES");

        return bool.TryParse(configuredValue, out var force) && force;
    }

    private static async Task ImportSnapshotCoursesAsync(
        WebApplication app,
        string message,
        bool failOnError,
        bool force,
        CancellationToken cancellationToken)
    {
        try
        {
            using var scope = app.Services.CreateScope();
            var result = await SnapshotCourseSeeder.SeedAsync(scope.ServiceProvider, force, cancellationToken)
                .ConfigureAwait(false);
            app.Logger.LogInformation(
                "{Message}. Parsed {ParsedPrograms} programs and {ParsedContents} contents from {CoursesRoot}. Created {CreatedPrograms} new programs and {CreatedContents} contents. DbContext sees {PublicProgramCount} published/public programs in database {DatabaseName}.",
                message,
                result.ParsedPrograms,
                result.ParsedContents,
                result.CoursesRoot,
                result.CreatedPrograms,
                result.CreatedContents,
                result.PublicProgramCount,
                result.DatabaseName);
        }
        catch (Exception exception) when (!failOnError)
        {
            app.Logger.LogWarning(
                exception,
                "{Message} failed. API-backed course management may be empty.",
                message);
        }
    }
}
