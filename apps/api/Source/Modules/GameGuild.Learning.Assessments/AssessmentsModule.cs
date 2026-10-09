using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using GameGuild.Learning.Courses;
using GameGuild.Learning.Assessments.Grading.Capabilities;
using GameGuild.Learning.Assessments.Grading.Abstractions;
using GameGuild.Learning.Assessments.Grading.Authoring;
using GameGuild.Learning.Assessments.Grading.Contracts;
using GameGuild.Learning.Assessments.Grading.Persistence;
using GameGuild.Learning.Assessments.Grading.Runtime;
using Microsoft.Extensions.DependencyInjection.Extensions;
using GameGuild.Assets;
using GameGuild.Learning.Assessments.Grading.Code;

namespace GameGuild.Learning.Assessments;

/// <summary>
/// Module registration for the Assessments/Quizzes system.
/// </summary>
public static class AssessmentsModule
{
    /// <summary>
    /// Adds assessment services to the dependency injection container.
    /// </summary>
    public static IServiceCollection AddAssessmentsModule(this IServiceCollection services)
    {
        services.Configure<Microsoft.AspNetCore.Mvc.JsonOptions>(options =>
            ConfigureReviewMethodsJson(options.JsonSerializerOptions));
        services.Configure<Microsoft.AspNetCore.Http.Json.JsonOptions>(options =>
            ConfigureReviewMethodsJson(options.SerializerOptions));

        // Register services
        services.AddScoped<IAssessmentService, AssessmentService>();
        services.AddScoped<IGroupSetService, GroupSetService>();
        services.AddScoped<IRubricService, RubricService>();
        services.AddScoped<IPeerReviewAssignmentService, PeerReviewAssignmentService>();
        services.AddScoped<IGradingQueueService, GradingQueueService>();
        services.AddScoped<ITasksService, TasksService>();
        services.AddScoped<IProgramContentLifecycleGuard, AssessmentProgramContentLifecycleGuard>();
        services.AddScoped<GameGuild.Learning.Courses.IAssessmentGradingSync, AssessmentGradingSync>();
        services.AddScoped<IAssetParentAuthorizationResolver, AssessmentSubmissionAssetAuthorizationResolver>();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IReviewCapabilityRegistration, CoreGradingCapabilityRegistration>());
        services.TryAddSingleton<ICodeAssessmentExecutor, UnconfiguredCodeAssessmentExecutor>();
        services.AddSingleton<CodeAssessmentTypeAdapter>();
        services.AddSingleton<IAssessmentTypeAdapter>(provider => provider.GetRequiredService<CodeAssessmentTypeAdapter>());
        services.AddSingleton<IReviewStageHandler, CodeAutomatedReviewStageHandler>();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IReviewCapabilityRegistration, CodeCapabilityRegistration>());
        services.AddScoped<IProgramContentPublicationParticipant, CodeProgramContentPublicationParticipant>();
        services.AddScoped<IProgramContentDeleteParticipant, CodeProgramContentDeleteParticipant>();
        services.AddSingleton<CodeProgramContentBoundary>();
        services.AddSingleton<IProgramContentLearnerProjector>(provider => provider.GetRequiredService<CodeProgramContentBoundary>());
        services.AddSingleton<IProgramContentAcademicMutationGuard>(provider => provider.GetRequiredService<CodeProgramContentBoundary>());
        services.AddSingleton<IReviewCapabilityRegistry>(provider =>
        {
            var registry = new ReviewCapabilityRegistry();
            foreach (var registration in provider.GetServices<IReviewCapabilityRegistration>())
            {
                registration.Register(registry);
            }

            return registry;
        });
        services.AddSingleton<IAssessmentTypeAdapterResolver, AssessmentTypeAdapterResolver>();
        services.AddSingleton<IAssessmentExecutionPolicyResolver, AssessmentExecutionPolicyResolver>();
        services.AddSingleton<IReviewStageHandlerResolver, ReviewStageHandlerResolver>();
        services.AddSingleton<InstructorReviewStageHandler>();
        services.AddSingleton<IReviewStageHandler>(provider => provider.GetRequiredService<InstructorReviewStageHandler>());
        services.AddScoped<IAssessmentAuthoringService, AssessmentAuthoringService>();
        services.AddScoped<IAssessmentGradebookProjectionService, AssessmentGradebookProjectionService>();
        services.AddScoped<IAssessmentLearnerResultProjectionService, AssessmentLearnerResultProjectionService>();
        services.AddScoped<OfficialGradingFinalizationSink>();
        services.AddScoped<IGradingFinalizationSink>(provider => provider.GetRequiredService<OfficialGradingFinalizationSink>());
        services.AddScoped<IGradingExecutionOrchestrator, GradingExecutionOrchestrator>();
        services.AddScoped<IAssessmentGradingRuntimeService, AssessmentGradingRuntimeService>();
        services.AddScoped<IGradeReleaseService, GradeReleaseService>();
        services.AddScoped<IAcademicOutboxConsumer, ImmediateGradeReleaseConsumer>();
        services.AddScoped<IAcademicOutboxWriter, AcademicOutboxWriter>();
        services.AddSingleton<IAcademicOutboxDispatcher, AcademicOutboxDispatcher>();
        services.AddScoped<IAssessmentExecutableVersionPreflight, AssessmentExecutableVersionPreflight>();
        services.AddHostedService<AssessmentExecutableVersionPreflightHostedService>();
        services.AddHostedService<AcademicOutboxBackgroundService>();

        return services;
    }

    /// <summary>
    /// Maps assessment endpoints if using minimal APIs.
    /// </summary>
    public static IEndpointRouteBuilder MapAssessmentsEndpoints(this IEndpointRouteBuilder endpoints)
    {
        // Controllers are auto-discovered, but this can be used for minimal API routes
        return endpoints;
    }

    private static void ConfigureReviewMethodsJson(JsonSerializerOptions options)
    {
        if (options.Converters.Any(static converter => converter is ReviewMethodsJsonConverter)) return;

        // ReviewMethods is a numeric bitmask. It must precede the API-wide
        // JsonStringEnumConverter so responses match the generated contract.
        options.Converters.Insert(0, new ReviewMethodsJsonConverter());
    }
}
