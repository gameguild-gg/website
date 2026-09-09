using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;
using Fido2NetLib;
using GameGuild.Identity.Authentication;

namespace GameGuild.Identity.Authentication.UnitTests;

public class ControllerAndServiceCtorTests
{
    // ═══════════════════════════════════════════════════════════════════
    // Controller constructors
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void MfaController_CanBeConstructed()
    {
        var ctrl = new MfaController(Mock.Of<IMfaService>(), new CommandHandlerSender());
        ctrl.Should().NotBeNull();
    }

    [Fact]
    public void ServiceAccountCrudController_CanBeConstructed()
    {
        var ctrl = new ServiceAccountCrudController(Mock.Of<IServiceAccountService>(), new CommandHandlerSender());
        ctrl.Should().NotBeNull();
    }

    [Fact]
    public void TrustedDevicesController_CanBeConstructed()
    {
        var ctrl = new TrustedDevicesController(Mock.Of<ISessionManagementService>(), new CommandHandlerSender());
        ctrl.Should().NotBeNull();
    }

    // ═══════════════════════════════════════════════════════════════════
    // Service constructors
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void MfaAttemptTrackingService_CanBeConstructed()
    {
        var svc = new MfaAttemptTrackingService(
            NullLogger<MfaAttemptTrackingService>.Instance,
            Mock.Of<IUserMfaConfigurationRepository>(),
            Mock.Of<IMfaAttemptRepository>(),
            Mock.Of<IHttpContextAccessor>());
        svc.Should().NotBeNull();
    }

    [Fact]
    public void AuthAttemptService_CanBeConstructed()
    {
        var svc = new AuthAttemptService(
            Mock.Of<IAuthenticationAttemptRepository>(),
            Mock.Of<IUserEnumerationProtectionService>(),
            NullLogger<AuthAttemptService>.Instance);
        svc.Should().NotBeNull();
    }

    [Fact]
    public void WebAuthnRegistrationService_CanBeConstructed()
    {
        var svc = new WebAuthnRegistrationService(
            Mock.Of<IFido2>(),
            Mock.Of<IWebAuthnCredentialRepository>(),
            NullLogger<WebAuthnRegistrationService>.Instance);
        svc.Should().NotBeNull();
    }
}
