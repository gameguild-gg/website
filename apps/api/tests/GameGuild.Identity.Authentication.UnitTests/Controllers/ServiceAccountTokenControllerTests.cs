using System.Net;
using System.Security.Cryptography.X509Certificates;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace GameGuild.Identity.Authentication.UnitTests.Controllers;

public sealed class ServiceAccountTokenControllerTests
{
    [Fact]
    public async Task Token_ShouldReturnBadRequest_WhenGrantTypeIsUnsupported()
    {
        var controller = CreateController(new Mock<IServiceAccountService>(), new Mock<IJwtTokenService>());

        var result = await controller.Token(new ClientCredentialsRequest
        {
            GrantType = "password",
            ClientId = "client",
            ClientSecret = "secret"
        }, CancellationToken.None);

        var badRequest = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        var payload = badRequest.Value.Should().BeOfType<OAuth2ErrorResponse>().Subject;
        payload.Error.Should().Be("unsupported_grant_type");
    }

    [Fact]
    public async Task Token_ShouldReturnBadRequest_WhenCredentialsAreMissing()
    {
        var controller = CreateController(new Mock<IServiceAccountService>(), new Mock<IJwtTokenService>());

        var result = await controller.Token(new ClientCredentialsRequest
        {
            GrantType = "client_credentials",
            ClientId = string.Empty,
            ClientSecret = string.Empty
        }, CancellationToken.None);

        var badRequest = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        var payload = badRequest.Value.Should().BeOfType<OAuth2ErrorResponse>().Subject;
        payload.Error.Should().Be("invalid_request");
    }

    [Fact]
    public async Task Token_ShouldReturnUnauthorized_WhenClientAuthenticationFails()
    {
        var serviceAccountService = new Mock<IServiceAccountService>();
        var jwtTokenService = new Mock<IJwtTokenService>();

        serviceAccountService
            .Setup(x => x.AuthenticateAsync("client", "secret", "10.0.0.1", It.IsAny<CancellationToken>()))
            .ReturnsAsync((ServiceAccount?)null);

        var controller = CreateController(serviceAccountService, jwtTokenService, "10.0.0.1");

        var result = await controller.Token(new ClientCredentialsRequest
        {
            GrantType = "client_credentials",
            ClientId = "client",
            ClientSecret = "secret"
        }, CancellationToken.None);

        var unauthorized = result.Should().BeOfType<UnauthorizedObjectResult>().Subject;
        var payload = unauthorized.Value.Should().BeOfType<OAuth2ErrorResponse>().Subject;
        payload.Error.Should().Be("invalid_client");
    }

    [Fact]
    public async Task Token_ShouldReturnAccessTokenResponse_WhenAuthenticationSucceeds()
    {
        var serviceAccountService = new Mock<IServiceAccountService>();
        var jwtTokenService = new Mock<IJwtTokenService>();
        var serviceAccount = new ServiceAccount
        {
            Id = Guid.NewGuid(),
            ClientId = "svc-client",
            Name = "Jobs",
            Scopes = "read:users,write:jobs",
            TenantId = Guid.NewGuid()
        };
        var expiresAt = DateTime.UtcNow.AddMinutes(30);

        serviceAccountService
            .Setup(x => x.AuthenticateAsync("svc-client", "super-secret", "10.1.2.3", It.IsAny<CancellationToken>()))
            .ReturnsAsync(serviceAccount);

        jwtTokenService
            .Setup(x => x.GenerateServiceAccountTokenAsync(
                serviceAccount.Id.ToString(),
                serviceAccount.ClientId,
                serviceAccount.Name,
                It.Is<IReadOnlySet<string>>(scopes => scopes.SetEquals(new[] { "read:users", "write:jobs" })),
                serviceAccount.TenantId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(("jwt-token", expiresAt));

        var controller = CreateController(serviceAccountService, jwtTokenService, "10.1.2.3");

        var result = await controller.Token(new ClientCredentialsRequest
        {
            GrantType = "client_credentials",
            ClientId = "svc-client",
            ClientSecret = "super-secret"
        }, CancellationToken.None);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var payload = ok.Value.Should().BeOfType<ClientCredentialsTokenResponse>().Subject;
        payload.AccessToken.Should().Be("jwt-token");
        payload.TokenType.Should().Be("Bearer");
        payload.Scope.Should().Be(serviceAccount.Scopes);
        payload.ExpiresIn.Should().BePositive();
        payload.ExpiresIn.Should().BeLessThanOrEqualTo(1800);
    }

    [Fact]
    public async Task Token_ShouldReturnBadRequest_WhenCertificateAndSecretArePresentedTogether()
    {
        using var authority = ClientCertificateTestFactory.CreateCertificateAuthority("Controller Test Root");
        using var clientCertificate = ClientCertificateTestFactory.CreateClientCertificate("controller-client", authority);
        var controller = CreateController(new Mock<IServiceAccountService>(), new Mock<IJwtTokenService>(), clientCertificate: clientCertificate);

        var result = await controller.Token(new ClientCredentialsRequest
        {
            GrantType = "client_credentials",
            ClientId = "client",
            ClientSecret = "secret"
        }, CancellationToken.None);

        var badRequest = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        var payload = badRequest.Value.Should().BeOfType<OAuth2ErrorResponse>().Subject;
        payload.Error.Should().Be("invalid_request");
        payload.ErrorDescription.Should().Contain("not both");
    }

    [Fact]
    public async Task Token_ShouldReturnBadRequest_WhenNeitherSecretNorCertificateIsPresented()
    {
        var controller = CreateController(new Mock<IServiceAccountService>(), new Mock<IJwtTokenService>());

        var result = await controller.Token(new ClientCredentialsRequest
        {
            GrantType = "client_credentials",
            ClientId = "client",
            ClientSecret = string.Empty
        }, CancellationToken.None);

        var badRequest = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        var payload = badRequest.Value.Should().BeOfType<OAuth2ErrorResponse>().Subject;
        payload.Error.Should().Be("invalid_request");
        payload.ErrorDescription.Should().Contain("client certificate");
    }

    [Fact]
    public async Task Token_ShouldAcceptBoundClientCertificate_InLieuOfSecret()
    {
        using var authority = ClientCertificateTestFactory.CreateCertificateAuthority("Controller Test Root");
        using var clientCertificate = ClientCertificateTestFactory.CreateClientCertificate("controller-client", authority);
        var serviceAccountService = new Mock<IServiceAccountService>();
        var jwtTokenService = new Mock<IJwtTokenService>();
        var serviceAccount = new ServiceAccount
        {
            Id = Guid.NewGuid(),
            ClientId = "svc-cert",
            Name = "Cert service",
            Scopes = "read:jobs",
            CertificateThumbprint = ClientCertificateAuthenticationUtilities.GetNormalizedThumbprint(clientCertificate)
        };
        var expiresAt = DateTime.UtcNow.AddMinutes(15);

        serviceAccountService
            .Setup(x => x.AuthenticateWithCertificateAsync("svc-cert", clientCertificate, "10.2.3.4", It.IsAny<CancellationToken>()))
            .ReturnsAsync(serviceAccount);
        jwtTokenService
            .Setup(x => x.GenerateServiceAccountTokenAsync(
                serviceAccount.Id.ToString(),
                serviceAccount.ClientId,
                serviceAccount.Name,
                It.Is<IReadOnlySet<string>>(scopes => scopes.SetEquals(new[] { "read:jobs" })),
                serviceAccount.TenantId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(("jwt-from-cert", expiresAt));

        var controller = CreateController(serviceAccountService, jwtTokenService, "10.2.3.4", clientCertificate);

        var result = await controller.Token(new ClientCredentialsRequest
        {
            GrantType = "client_credentials",
            ClientId = "svc-cert",
            ClientSecret = string.Empty
        }, CancellationToken.None);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var payload = ok.Value.Should().BeOfType<ClientCredentialsTokenResponse>().Subject;
        payload.AccessToken.Should().Be("jwt-from-cert");
        serviceAccountService.Verify(
            x => x.AuthenticateAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Token_ShouldReturnUnauthorized_WhenCertificateAuthenticationFails()
    {
        using var authority = ClientCertificateTestFactory.CreateCertificateAuthority("Controller Test Root");
        using var clientCertificate = ClientCertificateTestFactory.CreateClientCertificate("controller-client", authority);
        var serviceAccountService = new Mock<IServiceAccountService>();
        serviceAccountService
            .Setup(x => x.AuthenticateWithCertificateAsync("svc-cert", clientCertificate, "10.2.3.4", It.IsAny<CancellationToken>()))
            .ReturnsAsync((ServiceAccount?)null);
        var controller = CreateController(serviceAccountService, new Mock<IJwtTokenService>(), "10.2.3.4", clientCertificate);

        var result = await controller.Token(new ClientCredentialsRequest
        {
            GrantType = "client_credentials",
            ClientId = "svc-cert",
            ClientSecret = string.Empty
        }, CancellationToken.None);

        var unauthorized = result.Should().BeOfType<UnauthorizedObjectResult>().Subject;
        var payload = unauthorized.Value.Should().BeOfType<OAuth2ErrorResponse>().Subject;
        payload.Error.Should().Be("invalid_client");
    }

    private static ServiceAccountTokenController CreateController(
        Mock<IServiceAccountService> serviceAccountService,
        Mock<IJwtTokenService> jwtTokenService,
        string? remoteIpAddress = null,
        X509Certificate2? clientCertificate = null)
    {
        var controller = new ServiceAccountTokenController(new CommandHandlerSender(serviceAccountService.Object, jwtTokenService.Object))
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = CreateHttpContext(remoteIpAddress, clientCertificate)
            }
        };

        return controller;
    }

    private static DefaultHttpContext CreateHttpContext(string? remoteIpAddress, X509Certificate2? clientCertificate = null)
    {
        var httpContext = new DefaultHttpContext();

        if (remoteIpAddress != null)
        {
            httpContext.Connection.RemoteIpAddress = IPAddress.Parse(remoteIpAddress);
        }

        if (clientCertificate != null)
        {
            httpContext.Connection.ClientCertificate = clientCertificate;
        }

        return httpContext;
    }
}
