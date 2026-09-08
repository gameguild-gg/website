using System.Net;
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

    private static ServiceAccountTokenController CreateController(
        Mock<IServiceAccountService> serviceAccountService,
        Mock<IJwtTokenService> jwtTokenService,
        string? remoteIpAddress = null)
    {
        var controller = new ServiceAccountTokenController(new CommandHandlerSender(serviceAccountService.Object, jwtTokenService.Object))
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = CreateHttpContext(remoteIpAddress)
            }
        };

        return controller;
    }

    private static DefaultHttpContext CreateHttpContext(string? remoteIpAddress)
    {
        var httpContext = new DefaultHttpContext();

        if (remoteIpAddress != null)
        {
            httpContext.Connection.RemoteIpAddress = IPAddress.Parse(remoteIpAddress);
        }

        return httpContext;
    }
}
