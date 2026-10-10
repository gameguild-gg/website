using System.Security.Claims;
using FluentAssertions;
using GameGuild.CQRS;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace GameGuild.Identity.Authentication.UnitTests.Controllers;

public class ExternalLoginsControllerTests
{
    private static ExternalLoginsController CreateController(
        Mock<ISender> sender,
        Guid? userId = null,
        string claimType = ClaimTypes.NameIdentifier)
    {
        var controller = new ExternalLoginsController(sender.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = userId.HasValue
                        ? new ClaimsPrincipal(new ClaimsIdentity(
                            [new Claim(claimType, userId.Value.ToString())],
                            "Bearer"))
                        : new ClaimsPrincipal(new ClaimsIdentity())
                }
            }
        };
        return controller;
    }

    // ── HEAD list ───────────────────────────────────────────────────────

    [Fact]
    public async Task GetExternalLogins_Returns200_WithProvidersInHeader()
    {
        var userId = Guid.NewGuid();
        var sender = new Mock<ISender>();
        var discordLinkedAt = DateTime.UtcNow;
        var googleLinkedAt = DateTime.UtcNow.AddHours(-2);
        var dtos = new List<ExternalLoginDto>
        {
            new() { Provider = "discord", CreatedAt = discordLinkedAt },
            new() { Provider = "google", CreatedAt = googleLinkedAt }
        };
        sender
            .Setup(s => s.Send(
                It.Is<GetExternalLoginsQuery>(q => q.UserId == userId),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(dtos);

        var controller = CreateController(sender, userId);
        var result = await controller.GetExternalLogins(CancellationToken.None);

        result.Should().BeOfType<OkResult>();
        var header = controller.Response.Headers["X-Linked-Providers"].ToString();
        header.Should().Be(
            $"discord={DateTime.SpecifyKind(discordLinkedAt, DateTimeKind.Utc):O}," +
            $"google={DateTime.SpecifyKind(googleLinkedAt, DateTimeKind.Utc):O}");
    }

    [Fact]
    public async Task GetExternalLogins_NoLinkedProviders_OmitsHeader()
    {
        var userId = Guid.NewGuid();
        var sender = new Mock<ISender>();
        sender
            .Setup(s => s.Send(
                It.Is<GetExternalLoginsQuery>(q => q.UserId == userId),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var controller = CreateController(sender, userId);
        var result = await controller.GetExternalLogins(CancellationToken.None);

        result.Should().BeOfType<OkResult>();
        controller.Response.Headers.ContainsKey("X-Linked-Providers").Should().BeFalse();
        controller.Response.Headers.ContainsKey("X-Granted-Scopes").Should().BeFalse();
    }

    [Fact]
    public async Task GetExternalLogins_WithRecordedConsents_ReturnsGrantedScopesHeader()
    {
        var userId = Guid.NewGuid();
        var sender = new Mock<ISender>();
        var consentedAt = DateTime.UtcNow.AddHours(-1);
        sender
            .Setup(s => s.Send(
                It.Is<GetExternalLoginsQuery>(q => q.UserId == userId),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ExternalLoginDto>
            {
                new()
                {
                    Provider = "discord",
                    CreatedAt = DateTime.UtcNow,
                    GrantedScopes = ["identify", "email"],
                    ConsentedAt = consentedAt,
                    ConsentVersion = OAuthConsentVersions.Current
                },
                new() { Provider = "google", CreatedAt = DateTime.UtcNow.AddHours(-2) }
            });

        var controller = CreateController(sender, userId);
        var result = await controller.GetExternalLogins(CancellationToken.None);

        result.Should().BeOfType<OkResult>();
        var header = controller.Response.Headers["X-Granted-Scopes"].ToString();
        header.Should().Be(
            $"discord={DateTime.SpecifyKind(consentedAt, DateTimeKind.Utc):O}|{OAuthConsentVersions.Current}|identify%20email",
            "one 'provider=timestamp|version|scopes' entry per link with a recorded consent; legacy rows are omitted");
    }

    [Fact]
    public async Task GetExternalLogins_NoRecordedConsents_OmitsGrantedScopesHeader()
    {
        var userId = Guid.NewGuid();
        var sender = new Mock<ISender>();
        sender
            .Setup(s => s.Send(
                It.Is<GetExternalLoginsQuery>(q => q.UserId == userId),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ExternalLoginDto>
            {
                new() { Provider = "google", CreatedAt = DateTime.UtcNow, GrantedScopes = [], ConsentedAt = null, ConsentVersion = 0 }
            });

        var controller = CreateController(sender, userId);
        await controller.GetExternalLogins(CancellationToken.None);

        controller.Response.Headers.ContainsKey("X-Linked-Providers").Should().BeTrue();
        controller.Response.Headers.ContainsKey("X-Granted-Scopes").Should().BeFalse();
    }

    [Fact]
    public async Task GetExternalLogins_NoUserClaims_Returns401Problem()
    {
        var sender = new Mock<ISender>();

        var result = await CreateController(sender, userId: null).GetExternalLogins(CancellationToken.None);

        var unauthorized = result.Should().BeOfType<UnauthorizedObjectResult>().Subject;
        var problem = unauthorized.Value.Should().BeOfType<ProblemDetails>().Subject;
        problem.Status.Should().Be(401);
        sender.Verify(s => s.Send(It.IsAny<GetExternalLoginsQuery>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData("sub")]
    [InlineData("user_id")]
    public async Task GetExternalLogins_AlternativeUserIdClaim_Returns200(string claimType)
    {
        var userId = Guid.NewGuid();
        var sender = new Mock<ISender>();
        sender
            .Setup(instance => instance.Send(
                It.Is<GetExternalLoginsQuery>(query => query.UserId == userId),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var result = await CreateController(sender, userId, claimType)
            .GetExternalLogins(CancellationToken.None);

        result.Should().BeOfType<OkResult>();
    }

    // ── POST google link ────────────────────────────────────────────────

    [Fact]
    public async Task LinkGoogle_HandlerSucceeds_Returns204()
    {
        var userId = Guid.NewGuid();
        var sender = new Mock<ISender>();
        sender
            .Setup(s => s.Send(
                It.Is<LinkGoogleAccountCommand>(c => c.UserId == userId && c.IdToken == "valid-id-token"),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var result = await CreateController(sender, userId)
            .LinkGoogle(new LinkGoogleAccountRequest { IdToken = "valid-id-token" }, CancellationToken.None);

        result.Should().BeOfType<NoContentResult>();
    }

    [Fact]
    public async Task LinkGoogle_InvalidIdToken_Returns401Problem()
    {
        var userId = Guid.NewGuid();
        var sender = new Mock<ISender>();
        sender
            .Setup(s => s.Send(It.IsAny<LinkGoogleAccountCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new UnauthorizedAccessException("Google ID token is invalid"));

        var result = await CreateController(sender, userId)
            .LinkGoogle(new LinkGoogleAccountRequest { IdToken = "forged-token" }, CancellationToken.None);

        var unauthorized = result.Should().BeOfType<UnauthorizedObjectResult>().Subject;
        var problem = unauthorized.Value.Should().BeOfType<ProblemDetails>().Subject;
        problem.Status.Should().Be(401);
        problem.Detail.Should().Be("Google ID token is invalid");
    }

    [Fact]
    public async Task LinkGoogle_ForeignOwnerConflict_Returns409Problem()
    {
        var userId = Guid.NewGuid();
        var sender = new Mock<ISender>();
        sender
            .Setup(s => s.Send(It.IsAny<LinkGoogleAccountCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ExternalLoginConflictException("Social account already linked to another user"));

        var result = await CreateController(sender, userId)
            .LinkGoogle(new LinkGoogleAccountRequest { IdToken = "valid-id-token" }, CancellationToken.None);

        var conflict = result.Should().BeOfType<ConflictObjectResult>().Subject;
        var problem = conflict.Value.Should().BeOfType<ProblemDetails>().Subject;
        problem.Status.Should().Be(409);
        problem.Detail.Should().Be("Social account already linked to another user");
    }

    [Fact]
    public async Task LinkGoogle_UnexpectedFailure_RethrowsOriginalException()
    {
        var expected = new ApplicationException("Unexpected failure");
        var sender = new Mock<ISender>();
        sender
            .Setup(instance => instance.Send(It.IsAny<LinkGoogleAccountCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(expected);

        var action = () => CreateController(sender, Guid.NewGuid())
            .LinkGoogle(new LinkGoogleAccountRequest { IdToken = "valid-id-token" }, CancellationToken.None);

        var thrown = await action.Should().ThrowAsync<ApplicationException>();
        thrown.Which.Should().BeSameAs(expected);
    }

    // ── POST discord link-authorize ─────────────────────────────────────

    [Fact]
    public async Task DiscordLinkAuthorize_Returns200_WithAuthUrlAndState()
    {
        var sender = new Mock<ISender>();
        var response = new DiscordLinkAuthorizeResponse
        {
            AuthUrl = "https://discord.com/oauth2/authorize?client_id=abc",
            State = "a".PadLeft(32, '0')
        };
        sender
            .Setup(s => s.Send(
                It.Is<DiscordLinkAuthorizeCommand>(c => c.RedirectUri == "https://app/callback"),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(response);

        var result = await CreateController(sender, Guid.NewGuid())
            .DiscordLinkAuthorize(new DiscordLinkAuthorizeRequest { RedirectUri = "https://app/callback" }, CancellationToken.None);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().Be(response);
    }

    // ── POST discord link-callback ──────────────────────────────────────

    [Fact]
    public async Task DiscordLinkCallback_NotConfigured_Returns503Problem()
    {
        var userId = Guid.NewGuid();
        var sender = new Mock<ISender>();
        sender
            .Setup(s => s.Send(It.IsAny<LinkDiscordAccountCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Discord OAuth client ID or client secret not configured"));

        var result = await CreateController(sender, userId).DiscordLinkCallback(
            new DiscordLinkCallbackRequest { Code = "auth-code", State = "state-1", RedirectUri = "https://app/callback" },
            CancellationToken.None);

        var serverError = result.Should().BeOfType<ObjectResult>().Subject;
        serverError.StatusCode.Should().Be(503);
        var problem = serverError.Value.Should().BeOfType<ProblemDetails>().Subject;
        problem.Status.Should().Be(503);
        problem.Detail.Should().Be("Discord OAuth client ID or client secret not configured");
    }

    [Fact]
    public async Task DiscordLinkCallback_ForeignOwnerRaceConflict_Returns409Problem()
    {
        var userId = Guid.NewGuid();
        var sender = new Mock<ISender>();
        sender
            .Setup(s => s.Send(It.IsAny<LinkDiscordAccountCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ExternalLoginConflictException("Social account already linked to another user"));

        var result = await CreateController(sender, userId).DiscordLinkCallback(
            new DiscordLinkCallbackRequest { Code = "auth-code", State = "state-1", RedirectUri = "https://app/callback" },
            CancellationToken.None);

        var conflict = result.Should().BeOfType<ConflictObjectResult>().Subject;
        var problem = conflict.Value.Should().BeOfType<ProblemDetails>().Subject;
        problem.Status.Should().Be(409);
        problem.Detail.Should().Be("Social account already linked to another user");
    }

    // ── GET link-preview ────────────────────────────────────────────────

    [Fact]
    public async Task LinkPreview_HandlerSucceeds_Returns200WithPreview()
    {
        var sender = new Mock<ISender>();
        var preview = new ExternalLoginLinkPreviewResponse { Provider = "discord", RequestedScopes = ["identify", "email"] };
        sender
            .Setup(s => s.Send(
                It.Is<GetExternalLoginLinkPreviewQuery>(q => q.Provider == "discord"),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(preview);

        var result = await CreateController(sender, Guid.NewGuid()).LinkPreview("discord", CancellationToken.None);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().Be(preview);
    }

    [Fact]
    public async Task LinkPreview_UnsupportedProvider_Returns400Problem()
    {
        var sender = new Mock<ISender>();
        sender
            .Setup(s => s.Send(It.IsAny<GetExternalLoginLinkPreviewQuery>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new NotSupportedException("Provider not supported for link preview: steam"));

        var result = await CreateController(sender, Guid.NewGuid()).LinkPreview("steam", CancellationToken.None);

        var badRequest = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        var problem = badRequest.Value.Should().BeOfType<ProblemDetails>().Subject;
        problem.Status.Should().Be(400);
    }

    // ── POST scopes:revoke ──────────────────────────────────────────────

    [Fact]
    public async Task RevokeScopes_HandlerSucceeds_Returns200WithRemainingState()
    {
        var userId = Guid.NewGuid();
        var sender = new Mock<ISender>();
        var response = new RevokeExternalLoginScopesResponse
        {
            Provider = "google",
            GrantedScopes = ["openid", "profile"],
            ConsentedAt = DateTime.UtcNow.AddDays(-1),
            ConsentVersion = OAuthConsentVersions.Current
        };
        sender
            .Setup(s => s.Send(
                It.Is<RevokeExternalLoginScopesCommand>(c => c.UserId == userId && c.Provider == "google" && c.Scopes.SequenceEqual(new[] { "email" })),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(response);

        var result = await CreateController(sender, userId).RevokeScopes(
            "google",
            new RevokeExternalLoginScopesRequest { Scopes = ["email"] },
            CancellationToken.None);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().Be(response);
    }

    [Fact]
    public async Task RevokeScopes_InvalidScopeToken_Returns400Problem()
    {
        var userId = Guid.NewGuid();
        var sender = new Mock<ISender>();
        sender
            .Setup(s => s.Send(It.IsAny<RevokeExternalLoginScopesCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOAuthScopeException("'bad scope' is not a valid OAuth scope token"));

        var result = await CreateController(sender, userId).RevokeScopes(
            "google",
            new RevokeExternalLoginScopesRequest { Scopes = ["bad scope"] },
            CancellationToken.None);

        var badRequest = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        var problem = badRequest.Value.Should().BeOfType<ProblemDetails>().Subject;
        problem.Status.Should().Be(400);
        problem.Detail.Should().Be("'bad scope' is not a valid OAuth scope token");
    }

    [Fact]
    public async Task RevokeScopes_ProviderNotLinked_Returns404Problem()
    {
        var userId = Guid.NewGuid();
        var sender = new Mock<ISender>();
        sender
            .Setup(s => s.Send(It.IsAny<RevokeExternalLoginScopesCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ExternalLoginNotFoundException("No google login linked to this account"));

        var result = await CreateController(sender, userId).RevokeScopes(
            "google",
            new RevokeExternalLoginScopesRequest { Scopes = ["email"] },
            CancellationToken.None);

        var notFound = result.Should().BeOfType<NotFoundObjectResult>().Subject;
        var problem = notFound.Value.Should().BeOfType<ProblemDetails>().Subject;
        problem.Status.Should().Be(404);
    }

    // ── DELETE unlink ───────────────────────────────────────────────────

    [Fact]
    public async Task Unlink_HandlerSucceeds_Returns204()
    {
        var userId = Guid.NewGuid();
        var sender = new Mock<ISender>();
        sender
            .Setup(s => s.Send(
                It.Is<UnlinkExternalLoginCommand>(c => c.UserId == userId && c.Provider == "google"),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var result = await CreateController(sender, userId).Unlink("google", CancellationToken.None);

        result.Should().BeOfType<NoContentResult>();
    }

    [Fact]
    public async Task Unlink_LastSignInMethod_Returns400Problem()
    {
        var userId = Guid.NewGuid();
        var sender = new Mock<ISender>();
        sender
            .Setup(s => s.Send(It.IsAny<UnlinkExternalLoginCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new LastSignInMethodException("Cannot remove the last sign-in method"));

        var result = await CreateController(sender, userId).Unlink("google", CancellationToken.None);

        var badRequest = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        var problem = badRequest.Value.Should().BeOfType<ProblemDetails>().Subject;
        problem.Status.Should().Be(400);
        problem.Detail.Should().Be("Cannot remove the last sign-in method");
    }

    [Fact]
    public async Task Unlink_NotLinked_Returns404Problem()
    {
        var userId = Guid.NewGuid();
        var sender = new Mock<ISender>();
        sender
            .Setup(s => s.Send(It.IsAny<UnlinkExternalLoginCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ExternalLoginNotFoundException("No google login linked to this account"));

        var result = await CreateController(sender, userId).Unlink("google", CancellationToken.None);

        var notFound = result.Should().BeOfType<NotFoundObjectResult>().Subject;
        var problem = notFound.Value.Should().BeOfType<ProblemDetails>().Subject;
        problem.Status.Should().Be(404);
        problem.Detail.Should().Be("No google login linked to this account");
    }

    [Fact]
    public async Task Unlink_NoUserClaims_Returns401Problem()
    {
        var sender = new Mock<ISender>();

        var result = await CreateController(sender, userId: null).Unlink("google", CancellationToken.None);

        var unauthorized = result.Should().BeOfType<UnauthorizedObjectResult>().Subject;
        var problem = unauthorized.Value.Should().BeOfType<ProblemDetails>().Subject;
        problem.Status.Should().Be(401);
        sender.Verify(s => s.Send(It.IsAny<UnlinkExternalLoginCommand>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
