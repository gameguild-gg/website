using FluentAssertions;
using FluentValidation;
using GameGuild.CQRS;
using GameGuild.CQRS.Implementation;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace GameGuild.Commerce.Billing.UnitTests.Cqrs;

/// <summary>
///     Issue #394 (validation pipeline integration): once <see cref="ValidationBehavior{TRequest,TResponse}"/>
///     is registered as an <c>IPipelineBehavior&lt;,&gt;</c> in the API composition root, an invalid billing
///     command must short-circuit with <see cref="RequestValidationException"/> BEFORE any pipeline stage
///     registered after it (quota, use-case operation/audit) and before the command handler runs — so invalid
///     commands never consume quotas and never emit operation events.
/// </summary>
public class ValidationPipelineShortCircuitTests
{
    /// <summary>Inner pipeline stage registered AFTER the validation behavior (mirrors quota/operation ordering).</summary>
    private sealed class InnerRecorderBehavior : IPipelineBehavior<ProcessStripeWebhookCommand, WebhookProcessingResult>
    {
        private readonly Counter _counter;

        public InnerRecorderBehavior(Counter counter) { _counter = counter; }

        public async Task<WebhookProcessingResult> Handle(ProcessStripeWebhookCommand request, RequestHandlerDelegate<WebhookProcessingResult> next, CancellationToken cancellationToken)
        {
            _counter.Increment();
            return await next().ConfigureAwait(false);
        }
    }

    private sealed class Counter
    {
        private int _value;
        public int Value => _value;
        public void Increment() => Interlocked.Increment(ref _value);
    }

    private sealed class SpyCommandHandler : ICommandHandler<ProcessStripeWebhookCommand, WebhookProcessingResult>
    {
        public WebhookProcessingResult Response { get; } = new() { Processed = true, EventId = "evt_spy" };
        public int Invocations { get; private set; }

        public Task<WebhookProcessingResult> Handle(ProcessStripeWebhookCommand command, CancellationToken cancellationToken)
        {
            Invocations++;
            return Task.FromResult(Response);
        }
    }

    private static (ISender Sender, SpyCommandHandler Handler, Counter InnerInvocations) BuildSender()
    {
        var services = new ServiceCollection();

        // Same mediator wiring as SharedKernel AddCqrs.
        services.AddScoped<ServiceFactory>(provider => serviceType => provider.GetService(serviceType));
        services.AddScoped<IMediator, Mediator>();
        services.AddScoped<ISender>(provider => provider.GetRequiredService<IMediator>());

        // Pipeline: validation first (outermost), then an inner recorder standing in for the
        // quota/operation behaviors that the API registers after ValidationBehavior.
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
        services.AddTransient<IPipelineBehavior<ProcessStripeWebhookCommand, WebhookProcessingResult>, InnerRecorderBehavior>();

        // The real billing validator, exactly as registered by AddCqrs assembly scanning.
        services.AddScoped<IValidator<ProcessStripeWebhookCommand>, ProcessStripeWebhookCommandValidator>();

        var handler = new SpyCommandHandler();
        // AddCqrs registers concrete handlers under both IRequestHandler<,> and ICommandHandler<,>;
        // the mediator resolves IRequestHandler<,>, so the spy registers under that contract too.
        services.AddSingleton<IRequestHandler<ProcessStripeWebhookCommand, WebhookProcessingResult>>(handler);
        services.AddSingleton<ICommandHandler<ProcessStripeWebhookCommand, WebhookProcessingResult>>(handler);

        var counter = new Counter();
        services.AddSingleton(counter);

        var provider = services.BuildServiceProvider();
        return (provider.GetRequiredService<ISender>(), handler, counter);
    }

    [Fact]
    public async Task Invalid_Command_Short_Circuits_With_RequestValidationException()
    {
        var (sender, handler, innerInvocations) = BuildSender();

        var act = () => sender.Send(new ProcessStripeWebhookCommand(Payload: "", Signature: ""), CancellationToken.None);

        var exception = await act.Should().ThrowAsync<RequestValidationException>();
        exception.Which.Errors.Should().NotBeEmpty();
        exception.Which.Errors.Should().Contain(error => error.PropertyName == "Payload");
        exception.Which.Errors.Should().Contain(error => error.PropertyName == "Signature");
    }

    [Fact]
    public async Task Invalid_Command_Never_Reaches_Inner_Pipeline_Stages_Or_Handler()
    {
        var (sender, handler, innerInvocations) = BuildSender();

        var act = () => sender.Send(new ProcessStripeWebhookCommand(Payload: "", Signature: "sig"), CancellationToken.None);

        await act.Should().ThrowAsync<RequestValidationException>();
        innerInvocations.Value.Should().Be(0, "pipeline stages registered after validation must not run for invalid commands");
        handler.Invocations.Should().Be(0, "the command handler must not be invoked for invalid commands");
    }

    [Fact]
    public async Task Valid_Command_Passes_Through_Validation_To_The_Handler()
    {
        var (sender, handler, innerInvocations) = BuildSender();

        var response = await sender.Send(
            new ProcessStripeWebhookCommand(Payload: """{"id":"evt_1"}""", Signature: "t=1,v1=sig"),
            CancellationToken.None);

        response.Should().BeSameAs(handler.Response);
        innerInvocations.Value.Should().Be(1);
        handler.Invocations.Should().Be(1);
    }
}
