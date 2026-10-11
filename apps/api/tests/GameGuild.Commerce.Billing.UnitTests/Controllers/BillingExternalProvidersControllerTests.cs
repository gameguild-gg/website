using System.Reflection;
using FluentAssertions;
using GameGuild.Commerce.Billing;
using GameGuild.CQRS;
using GameGuild.Identity.Authorization;
using GameGuild.Identity.Context.Actors;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;

namespace GameGuild.Commerce.Billing.UnitTests.Controllers;

public class BillingExternalProvidersControllerTests
{
    private readonly Mock<ISender> _sender = new();
    private readonly Mock<IActorContextAccessor> _actorContextAccessor = new();

    private BillingExternalProvidersController CreateController(bool actorIsAdmin)
    {
        _actorContextAccessor
            .SetupGet(accessor => accessor.ActorContext)
            .Returns(new ActorContext
            {
                ActorKind = ActorKind.User,
                SubjectId = Guid.NewGuid().ToString(),
                Roles = new HashSet<string>(actorIsAdmin ? ["SystemAdmin"] : []),
                Permissions = new HashSet<string>(),
                TypedAttributes = ActorAttributes.Empty,
                AuthScheme = "Test",
                IsAuthenticated = true
            });

        return new BillingExternalProvidersController(_sender.Object, _actorContextAccessor.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    RequestServices = new ServiceCollection().BuildServiceProvider()
                }
            }
        };
    }

    private static ExternalBillingProviderStatusDto Status(string providerKey = PaymentProviders.Stripe, bool enabled = true) => new()
    {
        ProviderKey = providerKey,
        Configured = true,
        ConfigValid = true,
        WebhookEndpointConfigured = true,
        Enabled = enabled,
        Health = "healthy"
    };

    [Fact]
    public async Task ListProviders_Should_Reject_Non_Admin_Actors()
    {
        var controller = CreateController(actorIsAdmin: false);

        var result = await controller.ListProviders(CancellationToken.None);

        result.Should().BeOfType<ObjectResult>()
            .Which.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
        _sender.Verify(service => service.Send(It.IsAny<GetExternalBillingProvidersQuery>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ListProviders_Should_Return_The_Provider_List_For_Admins()
    {
        var providers = new List<ExternalBillingProviderStatusDto> { Status(), Status(PaymentProviders.PayPal) };
        _sender
            .Setup(service => service.Send(It.IsAny<GetExternalBillingProvidersQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(providers);
        var controller = CreateController(actorIsAdmin: true);

        var result = await controller.ListProviders(CancellationToken.None);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        ok.StatusCode.Should().Be(StatusCodes.Status200OK);
        ok.Value.Should().BeSameAs(providers);
    }

    [Fact]
    public async Task GetProvider_Should_Reject_Non_Admin_Actors()
    {
        var controller = CreateController(actorIsAdmin: false);

        var result = await controller.GetProvider(PaymentProviders.Stripe, CancellationToken.None);

        result.Should().BeOfType<ObjectResult>()
            .Which.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
        _sender.Verify(service => service.Send(It.IsAny<GetExternalBillingProviderQuery>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetProvider_Should_Translate_Unknown_Provider_To_404_Fail_Closed()
    {
        _sender
            .Setup(service => service.Send(It.IsAny<GetExternalBillingProviderQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ExternalBillingProviderStatusDto?)null);
        var controller = CreateController(actorIsAdmin: true);

        var result = await controller.GetProvider("unknown-provider", CancellationToken.None);

        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task GetProvider_Should_Return_The_Status_For_Admins()
    {
        var status = Status();
        _sender
            .Setup(service => service.Send(
                It.Is<GetExternalBillingProviderQuery>(query => query.ProviderKey == PaymentProviders.Stripe),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(status);
        var controller = CreateController(actorIsAdmin: true);

        var result = await controller.GetProvider(PaymentProviders.Stripe, CancellationToken.None);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().BeSameAs(status);
    }

    [Fact]
    public async Task EnableProvider_Should_Reject_Non_Admin_Actors()
    {
        var controller = CreateController(actorIsAdmin: false);

        var result = await controller.EnableProvider(PaymentProviders.Stripe, CancellationToken.None);

        result.Should().BeOfType<ObjectResult>()
            .Which.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
        _sender.Verify(service => service.Send(It.IsAny<SetExternalBillingProviderEnabledCommand>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task EnableProvider_Should_Translate_Unknown_Provider_To_404_Fail_Closed()
    {
        _sender
            .Setup(service => service.Send(It.IsAny<SetExternalBillingProviderEnabledCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new KeyNotFoundException("unknown"));
        var controller = CreateController(actorIsAdmin: true);

        var result = await controller.EnableProvider("unknown-provider", CancellationToken.None);

        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task DisableProvider_Should_Dispatch_The_Disable_Command_For_Admins()
    {
        var status = Status(enabled: false);
        _sender
            .Setup(service => service.Send(
                It.Is<SetExternalBillingProviderEnabledCommand>(command =>
                    command.ProviderKey == PaymentProviders.Stripe && command.Enable == false),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(status);
        var controller = CreateController(actorIsAdmin: true);

        var result = await controller.DisableProvider(PaymentProviders.Stripe, CancellationToken.None);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().BeSameAs(status);
        _sender.VerifyAll();
    }

    [Fact]
    public async Task EnableProvider_Should_Dispatch_The_Enable_Command_For_Admins()
    {
        var status = Status(enabled: true);
        _sender
            .Setup(service => service.Send(
                It.Is<SetExternalBillingProviderEnabledCommand>(command =>
                    command.ProviderKey == PaymentProviders.Stripe && command.Enable),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(status);
        var controller = CreateController(actorIsAdmin: true);

        var result = await controller.EnableProvider(PaymentProviders.Stripe, CancellationToken.None);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().BeSameAs(status);
        _sender.VerifyAll();
    }

    [Fact]
    public async Task MigrationDryRun_Should_Reject_Non_Admin_Actors()
    {
        var controller = CreateController(actorIsAdmin: false);

        var result = await controller.MigrationDryRun(
            new BillingExternalProvidersController.MigrationDryRunRequest("stripe", "paypal"),
            CancellationToken.None);

        result.Should().BeOfType<ObjectResult>()
            .Which.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
        _sender.Verify(service => service.Send(It.IsAny<MigrateBillingProviderCommand>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task MigrationDryRun_Should_Dispatch_The_Command_And_Return_The_Report()
    {
        var report = new BillingProviderMigrationReport
        {
            SourceProvider = PaymentProviders.Stripe,
            TargetProvider = PaymentProviders.PayPal,
            DryRun = true,
            MutationApplied = false,
            GeneratedAtUtc = DateTimeOffset.UtcNow,
            TotalSubscriptionsScanned = 3,
            BoundToSourceMissingTargetExternalIdCount = 1,
            AlreadyOnTargetProviderCount = 1,
            UnattributedExternalIdCount = 1,
            NotExternallyBoundCount = 0
        };
        _sender
            .Setup(service => service.Send(
                It.Is<MigrateBillingProviderCommand>(command =>
                    command.SourceProvider == "stripe" && command.TargetProvider == "paypal"),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(report);
        var controller = CreateController(actorIsAdmin: true);

        var result = await controller.MigrationDryRun(
            new BillingExternalProvidersController.MigrationDryRunRequest("stripe", "paypal"),
            CancellationToken.None);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().BeSameAs(report);
        _sender.VerifyAll();
    }

    [Fact]
    public void Every_Action_Should_Require_The_SystemAdmin_Policy()
    {
        // Anonymous requests are challenged (401) and authenticated non-admin
        // requests are forbidden (403) by the declarative policy; the in-controller
        // guard adds a fail-closed runtime layer.
        var actions = typeof(BillingExternalProvidersController)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(method => method.IsPublic && !method.IsSpecialName)
            .ToList();

        actions.Should().NotBeEmpty();

        foreach (var action in actions)
        {
            var authorize = action.GetCustomAttributes(typeof(AuthorizeAttribute), inherit: false)
                .Cast<AuthorizeAttribute>()
                .Should()
                .ContainSingle($"action {action.Name} must carry exactly one [Authorize]")
                .Subject;

            authorize.Policy.Should().Be(Policies.SystemAdmin, $"action {action.Name} must require the SystemAdmin policy");
            authorize.Roles.Should().BeNullOrEmpty();
        }
    }
}
