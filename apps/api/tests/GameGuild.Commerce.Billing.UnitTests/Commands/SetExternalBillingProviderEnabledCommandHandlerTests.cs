using FluentAssertions;
using GameGuild.Commerce.Billing;
using GameGuild.Compliance.Audit;
using GameGuild.Identity.Context.Actors;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace GameGuild.Commerce.Billing.UnitTests.Commands;

public class SetExternalBillingProviderEnabledCommandHandlerTests
{
    private readonly Mock<IExternalBillingProviderRegistry> _registry = new();
    private readonly Mock<IAuditService> _auditService = new();
    private readonly Mock<IActorContextAccessor> _actorContextAccessor = new();
    private readonly Guid _actorId = Guid.NewGuid();

    public SetExternalBillingProviderEnabledCommandHandlerTests()
    {
        _actorContextAccessor
            .SetupGet(accessor => accessor.ActorContext)
            .Returns(new ActorContext
            {
                ActorKind = ActorKind.User,
                SubjectId = _actorId.ToString(),
                Roles = new HashSet<string>(["SystemAdmin"]),
                Permissions = new HashSet<string>(),
                TypedAttributes = ActorAttributes.Empty,
                AuthScheme = "Test",
                IsAuthenticated = true
            });
    }

    private SetExternalBillingProviderEnabledCommandHandler CreateHandler() => new(
        _registry.Object,
        _actorContextAccessor.Object,
        _auditService.Object,
        NullLogger<SetExternalBillingProviderEnabledCommandHandler>.Instance);

    private ExternalBillingProviderStatusDto Status(bool enabled) => new()
    {
        ProviderKey = PaymentProviders.Stripe,
        Configured = true,
        ConfigValid = true,
        WebhookEndpointConfigured = true,
        Enabled = enabled,
        Health = "healthy"
    };

    [Fact]
    public async Task Handle_Disable_Should_Apply_The_Override_With_The_Acting_User()
    {
        _registry
            .Setup(registry => registry.SetEnabledAsync(PaymentProviders.Stripe, false, _actorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Status(enabled: false));

        var status = await CreateHandler().Handle(
            new SetExternalBillingProviderEnabledCommand(PaymentProviders.Stripe, Enable: false),
            CancellationToken.None);

        status.Enabled.Should().BeFalse();
        _registry.VerifyAll();
    }

    [Fact]
    public async Task Handle_EnableDisableRoundTrip_Should_Write_Audit_Events_For_Both_Transitions()
    {
        _registry
            .Setup(registry => registry.SetEnabledAsync(PaymentProviders.Stripe, false, _actorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Status(enabled: false));
        _registry
            .Setup(registry => registry.SetEnabledAsync(PaymentProviders.Stripe, true, _actorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Status(enabled: true));

        var handler = CreateHandler();
        await handler.Handle(new SetExternalBillingProviderEnabledCommand(PaymentProviders.Stripe, Enable: false), CancellationToken.None);
        await handler.Handle(new SetExternalBillingProviderEnabledCommand(PaymentProviders.Stripe, Enable: true), CancellationToken.None);

        _auditService.Verify(
            service => service.LogAsync(It.Is<CreateAuditLogRequest>(request =>
                request.ActionType == AuditActionTypes.BillingProviderDisabled &&
                request.ResourceId == PaymentProviders.Stripe &&
                request.UserId == _actorId &&
                request.Success)),
            Times.Once);
        _auditService.Verify(
            service => service.LogAsync(It.Is<CreateAuditLogRequest>(request =>
                request.ActionType == AuditActionTypes.BillingProviderEnabled &&
                request.ResourceId == PaymentProviders.Stripe &&
                request.UserId == _actorId &&
                request.Success)),
            Times.Once);
    }

    [Fact]
    public async Task Handle_Should_Propagate_The_Fail_Closed_Registry_Error_For_Unknown_Providers()
    {
        _registry
            .Setup(registry => registry.SetEnabledAsync("not-a-provider", It.IsAny<bool>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new KeyNotFoundException("Unknown external billing provider 'not-a-provider'."));

        var act = () => CreateHandler().Handle(
            new SetExternalBillingProviderEnabledCommand("not-a-provider", Enable: true),
            CancellationToken.None);

        await act.Should().ThrowAsync<KeyNotFoundException>();
        _auditService.Verify(service => service.LogAsync(It.IsAny<CreateAuditLogRequest>()), Times.Never);
    }

    [Fact]
    public async Task Handle_Should_Keep_The_State_Change_When_Audit_Delivery_Fails()
    {
        _registry
            .Setup(registry => registry.SetEnabledAsync(PaymentProviders.Stripe, false, _actorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Status(enabled: false));
        _auditService
            .Setup(service => service.LogAsync(It.IsAny<CreateAuditLogRequest>()))
            .ThrowsAsync(new InvalidOperationException("audit pipeline down"));

        var status = await CreateHandler().Handle(
            new SetExternalBillingProviderEnabledCommand(PaymentProviders.Stripe, Enable: false),
            CancellationToken.None);

        status.Enabled.Should().BeFalse();
    }
}

public class SetExternalBillingProviderEnabledCommandValidatorTests
{
    private readonly SetExternalBillingProviderEnabledCommandValidator _validator = new();

    [Theory]
    [InlineData("stripe")]
    [InlineData("paypal")]
    [InlineData("applepay")]
    [InlineData("apple_app_store")]
    [InlineData("googlepay")]
    [InlineData("google_play_store")]
    public void Validate_Should_Accept_Supported_Provider_Keys(string providerKey)
    {
        var result = _validator.Validate(new SetExternalBillingProviderEnabledCommand(providerKey, Enable: true));

        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("chargebee")]
    [InlineData("recurly")]
    [InlineData("unknown")]
    public void Validate_Should_Fail_Closed_For_Unknown_Provider_Keys(string providerKey)
    {
        var result = _validator.Validate(new SetExternalBillingProviderEnabledCommand(providerKey, Enable: true));

        result.IsValid.Should().BeFalse();
    }
}
