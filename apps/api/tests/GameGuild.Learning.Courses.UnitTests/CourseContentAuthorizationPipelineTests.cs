using System.Security.Claims;
using System.Text.Json;
using FluentAssertions;
using GameGuild.Identity.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;

namespace GameGuild.Learning.Courses.UnitTests;

public sealed class CourseContentAuthorizationPipelineTests
{
    [Fact]
    public async Task AuthorizationService_ResolvesLearningRuleThroughCentralScopedFactory()
    {
        var ruleset = new PolicyRuleset
        {
            Name = Policies.CourseContentPublicOutline,
            RequireAuthentication = false,
            Rules =
            [
                new RuleDefinition
                {
                    Type = RuleTypes.CourseContentAccess,
                    Params = new Dictionary<string, JsonElement>
                    {
                        ["access"] = JsonSerializer.SerializeToElement("PublicOutline")
                    }
                }
            ]
        };
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAuthorization(options => options.AddPolicy(
            Policies.CourseContentPublicOutline,
            policy => policy.AddRequirements(new RulesetRequirement(
                Policies.CourseContentPublicOutline,
                ruleset))));
        services.AddSingleton<IRuleEvaluatorRegistry>(new RuleEvaluatorRegistry([]));
        services.AddScoped<IScopedRuleEvaluatorFactory, ScopedRuleEvaluatorFactory>();
        services.AddScoped(_ => new CourseContentAccessRuleEvaluator(
            Mock.Of<ICourseAccessEvaluator>()));
        services.AddSingleton(new ScopedRuleEvaluatorRegistration(
            RuleTypes.CourseContentAccess,
            typeof(CourseContentAccessRuleEvaluator)));
        services.AddScoped(_ => Mock.Of<IRulesetProvider>());
        services.AddScoped<IAuthorizationHandler, RulesetAuthorizationHandler>();

        await using var serviceProvider = services.BuildServiceProvider();
        await using var scope = serviceProvider.CreateAsyncScope();
        var authorizationService = scope.ServiceProvider.GetRequiredService<IAuthorizationService>();
        var user = new ClaimsPrincipal(new ClaimsIdentity());

        var publicResult = await authorizationService.AuthorizeAsync(
            user,
            new Program
            {
                Status = ContentStatus.Published,
                Visibility = ContentVisibility.Public
            },
            Policies.CourseContentPublicOutline);
        var privateResult = await authorizationService.AuthorizeAsync(
            user,
            new Program
            {
                Status = ContentStatus.Published,
                Visibility = ContentVisibility.Private
            },
            Policies.CourseContentPublicOutline);

        publicResult.Succeeded.Should().BeTrue();
        privateResult.Succeeded.Should().BeFalse();
    }
}
