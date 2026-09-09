using GameGuild.Learning.Assessments.Grading.Abstractions;
using GameGuild.Learning.Assessments.Grading.Capabilities;
using Microsoft.Extensions.DependencyInjection;

namespace GameGuild.Learning.Assessments.QuizAdapter;

public static class QuizAdapterModule
{
    public static IServiceCollection AddQuizGradingAdapter(this IServiceCollection services)
    {
        services.AddSingleton<QuizItemProjector>();
        services.AddSingleton<QuizAuthoringAdapter>();
        services.AddSingleton<QuizDeliveryGenerator>();
        services.AddSingleton<QuizAnswerDecoder>();
        services.AddSingleton<QuizDeterministicReviewAlgorithm>();
        services.AddSingleton<QuizAssessmentTypeAdapter>();
        services.AddSingleton<IAssessmentTypeAdapter>(provider => provider.GetRequiredService<QuizAssessmentTypeAdapter>());
        services.AddSingleton<QuizProgramContentBoundary>();
        services.AddSingleton<GameGuild.Learning.Courses.IProgramContentLearnerProjector>(provider => provider.GetRequiredService<QuizProgramContentBoundary>());
        services.AddSingleton<GameGuild.Learning.Courses.IProgramContentAcademicMutationGuard>(provider => provider.GetRequiredService<QuizProgramContentBoundary>());
        services.AddSingleton<IReviewCapabilityRegistration, QuizCapabilityRegistration>();
        return services;
    }
}
