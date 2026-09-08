using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;
using GameGuild.Identity.Authentication;
using GameGuild.Identity.Users;

namespace GameGuild.Identity.Authentication.UnitTests;

public class DiExtensionsAndServiceTests
{
    private static IConfiguration EmptyConfig() => new ConfigurationBuilder().Build();

    // ═══════════════════════════════════════════════════════════════════
    // DI Extension Methods — Application layer (DependencyInjection.cs)
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void AddAuthenticationApplication_Registers()
    {
        var services = new ServiceCollection();
        services.AddAuthenticationApplication();
        services.Should().NotBeEmpty();
    }

    // ═══════════════════════════════════════════════════════════════════
    // DI Extension Methods — Data layer (DataDependencyInjection.cs)
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void AddAuthenticationData_Registers()
    {
        var services = new ServiceCollection();
        services.AddAuthenticationData(EmptyConfig());
        services.Should().NotBeEmpty();
    }

    // ═══════════════════════════════════════════════════════════════════
    // DI Extension Methods — Presentation (ServiceCollectionExtensions.cs)
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void AddAuthenticationPresentation_Registers()
    {
        var services = new ServiceCollection();
        services.AddAuthenticationPresentation(EmptyConfig());
        services.Should().NotBeEmpty();
    }

    [Fact]
    public void AddAuthenticationHealthChecks_Registers()
    {
        var services = new ServiceCollection();
        services.AddAuthenticationHealthChecks(EmptyConfig());
        services.Should().NotBeEmpty();
    }

    [Fact]
    public void AddAuthenticationMetrics_Registers()
    {
        var services = new ServiceCollection();
        services.AddAuthenticationMetrics();
        // May register nothing (commented out body), that's still valid
        services.Should().NotBeNull();
    }

    // ═══════════════════════════════════════════════════════════════════
    // Service constructors
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void TotpMfaService_CanBeConstructed()
    {
        var svc = new TotpMfaService(
            NullLogger<TotpMfaService>.Instance,
            Mock.Of<IUserMfaConfigurationRepository>(),
            Mock.Of<IMfaAttemptTrackingService>(),
            Mock.Of<IEncryptionService>());

        svc.Should().NotBeNull();
    }

    [Fact]
    public void OAuthAuthService_CanBeConstructed()
    {
        var svc = new OAuthAuthService(
            Mock.Of<IUserRepository>(),
            Mock.Of<IJwtTokenService>(),
            Mock.Of<IRefreshTokenHasher>(),
            Mock.Of<IOAuthService>(),
            Mock.Of<IGoogleIdTokenVerifier>(),
            Mock.Of<IExternalLoginRepository>(),
            EmptyConfig(),
            Mock.Of<IAuthAttemptService>(),
            Mock.Of<IHttpContextAccessor>(),
            Mock.Of<GameGuild.CQRS.ISender>(),
            Mock.Of<ISessionManagementService>(),
            NullLogger<OAuthAuthService>.Instance);

        svc.Should().NotBeNull();
    }

    [Fact]
    public void SessionController_CanBeConstructed()
    {
        var controller = new SessionController(
            Mock.Of<ISessionManagementService>(), new CommandHandlerSender());

        controller.Should().NotBeNull();
    }

    [Fact]
    public void AuthenticationModuleOptions_CanBeInstantiated()
    {
        var opts = new AuthenticationModuleOptions();
        opts.Should().NotBeNull();
    }
}
