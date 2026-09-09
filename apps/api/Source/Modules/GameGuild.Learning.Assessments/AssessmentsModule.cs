using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using GameGuild.Learning.Courses;
using GameGuild.Learning.Assessments.Grading.Capabilities;
using GameGuild.Learning.Assessments.Grading.Abstractions;
using GameGuild.Learning.Assessments.Grading.Authoring;
using GameGuild.Learning.Assessments.Grading.Persistence;
using Microsoft.Extensions.DependencyInjection.Extensions;

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
        // Register services
        services.AddScoped<IAssessmentService, AssessmentService>();
        services.AddScoped<IGroupSetService, GroupSetService>();
        services.AddScoped<IRubricService, RubricService>();
        services.AddScoped<IPeerReviewAssignmentService, PeerReviewAssignmentService>();
        services.AddScoped<IGradingQueueService, GradingQueueService>();
        services.AddScoped<ITasksService, TasksService>();
        services.AddScoped<IProgramContentLifecycleGuard, AssessmentProgramContentLifecycleGuard>();
        services.AddScoped<GameGuild.Learning.Courses.IAssessmentGradingSync, AssessmentGradingSync>();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IReviewCapabilityRegistration, CoreGradingCapabilityRegistration>());
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
        services.AddScoped<IAssessmentAuthoringService, AssessmentAuthoringService>();
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
}
