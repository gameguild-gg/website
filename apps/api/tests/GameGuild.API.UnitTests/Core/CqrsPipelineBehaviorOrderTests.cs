using FluentAssertions;
using GameGuild.API.Eventing;
using GameGuild.API.Setup;
using GameGuild.CQRS;
using GameGuild.Resources;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace GameGuild.API.UnitTests.Core;

/// <summary>
///     Issue #394 (validation pipeline integration): <see cref="ValidationBehavior{TRequest,TResponse}"/>
///     must be registered in the API composition root as an <see cref="IPipelineBehavior{TRequest,TResponse}"/>,
///     BEFORE the quota and use-case operation (audit/event) behaviors. The mediator wraps behaviors in
///     registration order (first registered = outermost), so this ordering makes invalid commands
///     short-circuit with <see cref="RequestValidationException"/> without consuming quotas and without
///     emitting operation events.
/// </summary>
public sealed class CqrsPipelineBehaviorOrderTests
{
    [Fact]
    public void AddInfrastructureLayer_Registers_ValidationBehavior_Before_Quota_And_Operation_Behaviors()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = "Testing"
        });
        builder.Configuration["InfrastructureLayer:EnableDatabase"] = "false";

        builder.AddInfrastructureLayer(options =>
        {
            options.UseInMemoryDatabase = true;
        });

        var behaviorImplementations = builder.Services
            .Where(descriptor => descriptor.ServiceType == typeof(IPipelineBehavior<,>))
            .Select(descriptor => descriptor.ImplementationType)
            .ToList();

        behaviorImplementations.Should().Contain(typeof(ValidationBehavior<,>),
            "the validation behavior must be registered as a pipeline behavior (issue #394)");
        behaviorImplementations.Should().Contain(typeof(UseCaseOperationBehavior<,>));

        var validationIndex = behaviorImplementations.IndexOf(typeof(ValidationBehavior<,>));
        var quotaIndex = behaviorImplementations.IndexOf(typeof(ResourceQuotaBehavior<,>));
        var operationIndex = behaviorImplementations.IndexOf(typeof(UseCaseOperationBehavior<,>));

        validationIndex.Should().BeInRange(0, behaviorImplementations.Count - 1);
        validationIndex.Should().BeLessThan(operationIndex,
            "validation must run before the operation/audit behavior so invalid commands never emit operation events");
        if (quotaIndex >= 0)
        {
            validationIndex.Should().BeLessThan(quotaIndex,
                "validation must run before the quota behavior so invalid commands never consume quotas");
        }
    }
}
