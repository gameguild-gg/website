using FluentAssertions;
using FluentValidation;
using GameGuild.Assets;
using GameGuild.Identity.Authorization;
using GameGuild.Learning.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace GameGuild.Learning.Courses.UnitTests.Configuration;

public sealed class CoursesModuleCoverageTests
{
    [Fact]
    public void AddCoursesModule_RegistersCompleteCourseCompositionWithExpectedLifetimes()
    {
        var services = new ServiceCollection();

        var result = services.AddCoursesModule();

        result.Should().BeSameAs(services);
        AssertRegistration<IProgramReadService, ProgramReadService>(services, ServiceLifetime.Scoped);
        AssertRegistration<IProgramWriteService, ProgramWriteService>(services, ServiceLifetime.Scoped);
        AssertRegistration<IProgramCrudService, ProgramCrudService>(services, ServiceLifetime.Scoped);
        AssertRegistration<IProgramLifecycleService, ProgramLifecycleService>(services, ServiceLifetime.Scoped);
        AssertRegistration<IProgramService, ProgramService>(services, ServiceLifetime.Scoped);
        AssertRegistration<IProgramContentScheduleGuard, NullProgramContentScheduleGuard>(services, ServiceLifetime.Scoped);
        AssertRegistration<IProgramContentLifecycleGuard, NullProgramContentLifecycleGuard>(services, ServiceLifetime.Scoped);
        AssertRegistration<IProgramContentService, ProgramContentService>(services, ServiceLifetime.Scoped);
        AssertRegistration<IProgramContentAuthoringService, ProgramContentAuthoringService>(services, ServiceLifetime.Scoped);
        AssertRegistration<LearningAssetManifestService, LearningAssetManifestService>(services, ServiceLifetime.Scoped);
        AssertFactoryRegistration<ILearningAssetManifestService>(services, ServiceLifetime.Scoped);
        AssertFactoryRegistration<IAssetUsageGuard>(services, ServiceLifetime.Scoped);
        AssertRegistration<IAssetParentAuthorizationResolver, LearningAssetParentAuthorizationResolver>(services, ServiceLifetime.Scoped);
        AssertRegistration<IAuthoringAiRunQueue, AuthoringAiRunQueue>(services, ServiceLifetime.Singleton);
        AssertRegistration<IAuthoringAiService, AuthoringAiService>(services, ServiceLifetime.Scoped);
        AssertRegistration<CourseContentAccessRuleEvaluator, CourseContentAccessRuleEvaluator>(services, ServiceLifetime.Scoped);
        AssertRegistration<ICourseAccessEvaluator, CourseAccessEvaluator>(services, ServiceLifetime.Scoped);
        AssertRegistration<ICourseEnrollmentAccessReader, FailClosedCourseEnrollmentAccessReader>(services, ServiceLifetime.Scoped);
        AssertRegistration<IProgramEnrollmentService, ProgramEnrollmentService>(services, ServiceLifetime.Scoped);
        AssertRegistration<IContentInteractionService, ContentInteractionService>(services, ServiceLifetime.Scoped);
        AssertRegistration<IActivityGradeService, ActivityGradeService>(services, ServiceLifetime.Scoped);
        AssertRegistration<IContentProgressService, ContentProgressService>(services, ServiceLifetime.Scoped);
        AssertRegistration<IPrerequisiteService, PrerequisiteService>(services, ServiceLifetime.Scoped);
        AssertRegistration<IProductProgramProvider, ProductProgramProvider>(services, ServiceLifetime.Scoped);
        AssertRegistration<IValidator<CodingAssignmentContent>, CodingAssignmentContentValidator>(services, ServiceLifetime.Singleton);
        AssertRegistration<ICodingAssignmentContentService, CodingAssignmentContentService>(services, ServiceLifetime.Scoped);

        services.Should().ContainSingle(descriptor =>
            descriptor.ServiceType == typeof(ScopedRuleEvaluatorRegistration) &&
            descriptor.Lifetime == ServiceLifetime.Singleton &&
            descriptor.ImplementationInstance is ScopedRuleEvaluatorRegistration);
        services.Should().ContainSingle(descriptor =>
            descriptor.ServiceType == typeof(IHostedService) &&
            descriptor.ImplementationType == typeof(AuthoringAiBackgroundService));
    }

    [Fact]
    public void AuthoringAiBackgroundService_CanBeConstructedFromRegisteredDependencies()
    {
        using var service = new AuthoringAiBackgroundService(
            new AuthoringAiRunQueue(),
            Mock.Of<IServiceScopeFactory>(),
            NullLogger<AuthoringAiBackgroundService>.Instance);

        service.Should().NotBeNull();
    }

    private static void AssertRegistration<TService, TImplementation>(
        IServiceCollection services,
        ServiceLifetime lifetime)
        where TService : class
        where TImplementation : class, TService
    {
        services.Should().ContainSingle(descriptor =>
            descriptor.ServiceType == typeof(TService) &&
            descriptor.ImplementationType == typeof(TImplementation) &&
            descriptor.Lifetime == lifetime);
    }

    private static void AssertFactoryRegistration<TService>(
        IServiceCollection services,
        ServiceLifetime lifetime)
        where TService : class
    {
        services.Should().ContainSingle(descriptor =>
            descriptor.ServiceType == typeof(TService) &&
            descriptor.ImplementationFactory != null &&
            descriptor.Lifetime == lifetime);
    }
}
