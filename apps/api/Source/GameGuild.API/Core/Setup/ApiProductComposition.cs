using GameGuild.API.Database;
using GameGuild.API.HealthChecks;
using GameGuild.Commerce.Billing;
using GameGuild.Commerce.Payments;
using GameGuild.Compliance.FERPA;
using GameGuild.Economy;
using GameGuild.Economy.Payouts;
using GameGuild.GameJams;
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
using GameGuild.ProjectWork;
using GameGuild.Projects;
using GameGuild.Social.Blog;
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

    public string DefaultDataProtectionKeysPath => "/app/.aspnet/DataProtection-Keys";

    public IReadOnlyList<string> EnabledModules { get; } =
    [
        "Compliance.FERPA",
        "Economy",
        "Economy.Payouts",
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
        "Projects",
        "ProjectWork",
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
        "Economy.AdRewards",
        "Economy.Bounties",
        "Economy.Marketplace",
        "Economy.Treasury",
        "TrustSafety"
    ];

    public void ConfigureServices(WebApplicationBuilder builder)
    {
        builder.Services.AddFerpaModule();
        builder.Services.AddEconomyCapabilityComposition(builder.Configuration);
        builder.Services.AddEconomyCoreComposition(builder.Configuration);
        builder.Services.AddPayoutsComposition(builder.Configuration);

        builder.Services.AddCoursesModule();
        builder.Services.AddAssessmentsModule();
        builder.Services.AddLearningEnrollmentsModule();
        builder.Services.AddCohortsModule();
        builder.Services.AddCertificatesModule();
        builder.Services.AddLearningWorkspacesModule();
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

    public Task SeedAsync(IServiceProvider services, CancellationToken cancellationToken)
        => DatabaseSeeder.SeedAsync(services);

    public async Task<bool> InitializeAsync(
        WebApplication app,
        bool databaseInitialized,
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken)
    {
        ValidatePaymentProviderConfiguration(app);

        var explicitImport = arguments.Any(argument =>
            string.Equals(argument, "--import-snapshot-courses", StringComparison.OrdinalIgnoreCase));

        if (explicitImport)
        {
            await ImportSnapshotCoursesAsync(app, "Snapshot course import complete", true, cancellationToken)
                .ConfigureAwait(false);
            return false;
        }

        if (databaseInitialized && ShouldImportSnapshotCourses(app.Configuration))
        {
            await ImportSnapshotCoursesAsync(
                    app,
                    "Snapshot course startup import complete",
                    false,
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

    private static async Task ImportSnapshotCoursesAsync(
        WebApplication app,
        string message,
        bool failOnError,
        CancellationToken cancellationToken)
    {
        try
        {
            using var scope = app.Services.CreateScope();
            var result = await SnapshotCourseSeeder.SeedAsync(scope.ServiceProvider, cancellationToken)
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
