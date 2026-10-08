using FluentAssertions;
using GameGuild.CQRS;
using GameGuild.Identity.Authentication.UnitTests.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using System.Security.Claims;
using Xunit;

namespace GameGuild.Identity.Authentication.UnitTests.Services;

// ════════════════════════════════════════════════════════════════════════════
// #225 Credential lifecycle — entity state machine
// Deactivation is reversible; revocation is terminal.
// ════════════════════════════════════════════════════════════════════════════
public sealed class UserWebAuthnCredentialLifecycleStateTests
{
    private static UserWebAuthnCredential MakeCredential() => new()
    {
        Id = Guid.NewGuid(),
        UserId = Guid.NewGuid(),
        CredentialId = Guid.NewGuid().ToString(),
        PublicKey = "pk",
        IsActive = true
    };

    [Fact]
    public void NewCredential_IsActive()
    {
        var credential = MakeCredential();

        credential.Status.Should().Be(WebAuthnCredentialStatus.Active);
        credential.IsDeactivated.Should().BeFalse();
        credential.IsRevoked.Should().BeFalse();
        credential.DeactivatedAt.Should().BeNull();
        credential.RevokedAt.Should().BeNull();
    }

    [Fact]
    public void Deactivate_MovesToDeactivated()
    {
        var credential = MakeCredential();

        credential.Deactivate();

        credential.Status.Should().Be(WebAuthnCredentialStatus.Deactivated);
        credential.IsDeactivated.Should().BeTrue();
        credential.IsActive.Should().BeFalse();
        credential.DeactivatedAt.Should().NotBeNull();
        credential.RevokedAt.Should().BeNull();
    }

    [Fact]
    public void Activate_AfterDeactivate_RestoresActive()
    {
        var credential = MakeCredential();
        credential.Deactivate();

        credential.Activate();

        credential.Status.Should().Be(WebAuthnCredentialStatus.Active);
        credential.IsActive.Should().BeTrue();
        credential.DeactivatedAt.Should().BeNull();
        credential.RevokedAt.Should().BeNull();
    }

    [Fact]
    public void Revoke_FromActive_IsTerminal()
    {
        var credential = MakeCredential();

        credential.Revoke();

        credential.Status.Should().Be(WebAuthnCredentialStatus.Revoked);
        credential.IsRevoked.Should().BeTrue();
        credential.IsActive.Should().BeFalse();
        credential.RevokedAt.Should().NotBeNull();
    }

    [Fact]
    public void Revoke_FromDeactivated_IsTerminal()
    {
        var credential = MakeCredential();
        credential.Deactivate();

        credential.Revoke();

        credential.Status.Should().Be(WebAuthnCredentialStatus.Revoked);
        credential.IsDeactivated.Should().BeFalse();
        credential.IsRevoked.Should().BeTrue();
    }

    [Fact]
    public void Activate_RevokedCredential_Throws()
    {
        var credential = MakeCredential();
        credential.Revoke();

        var act = credential.Activate;

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*revoked*terminal*");
    }

    [Fact]
    public void Deactivate_RevokedCredential_Throws()
    {
        var credential = MakeCredential();
        credential.Revoke();

        var act = credential.Deactivate;

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*revoked*terminal*");
    }

    [Fact]
    public void Deactivate_AlreadyDeactivated_Throws()
    {
        var credential = MakeCredential();
        credential.Deactivate();

        var act = credential.Deactivate;

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*already deactivated*");
    }

    [Fact]
    public void Activate_AlreadyActive_Throws()
    {
        var credential = MakeCredential();

        var act = credential.Activate;

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*already active*");
    }

    [Fact]
    public void Revoke_AlreadyRevoked_Throws()
    {
        var credential = MakeCredential();
        credential.Revoke();

        var act = credential.Revoke;

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*already revoked*");
    }

    [Fact]
    public void DeactivateThenActivate_ThenDeactivate_Again_Succeeds()
    {
        var credential = MakeCredential();

        credential.Deactivate();
        credential.Activate();
        credential.Deactivate();

        credential.Status.Should().Be(WebAuthnCredentialStatus.Deactivated);
    }
}

// ════════════════════════════════════════════════════════════════════════════
// #225 Credential lifecycle — management service transitions
// ════════════════════════════════════════════════════════════════════════════
public sealed class WebAuthnCredentialLifecycleTransitionTests
{
    private readonly Mock<IWebAuthnCredentialRepository> _repo = new();
    private readonly WebAuthnCredentialManagementService _sut;
    private readonly Guid _userId = Guid.NewGuid();

    public WebAuthnCredentialLifecycleTransitionTests()
    {
        _sut = new WebAuthnCredentialManagementService(
            _repo.Object,
            NullLogger<WebAuthnCredentialManagementService>.Instance);
    }

    private UserWebAuthnCredential MakeCredential(
        bool active = true,
        DateTime? deactivatedAt = null,
        DateTime? revokedAt = null) => new()
    {
        Id = Guid.NewGuid(),
        UserId = _userId,
        CredentialId = Guid.NewGuid().ToString(),
        PublicKey = "pk",
        IsActive = active,
        DeactivatedAt = deactivatedAt,
        RevokedAt = revokedAt
    };

    private void SetupCredential(UserWebAuthnCredential credential) =>
        _repo.Setup(r => r.GetByIdAsync(credential.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(credential);

    [Fact]
    public async Task Deactivate_ActiveCredential_Succeeds()
    {
        var credential = MakeCredential();
        SetupCredential(credential);
        _repo.Setup(r => r.DeactivateAsync(credential.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result = await _sut.DeactivateCredentialAsync(_userId, credential.Id);

        result.Success.Should().BeTrue();
        result.Error.Should().BeNull();
        result.Status.Should().Be(WebAuthnCredentialStatus.Deactivated);
    }

    [Fact]
    public async Task Deactivate_UnknownCredential_ReportsNotFound()
    {
        _repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserWebAuthnCredential?)null);

        var result = await _sut.DeactivateCredentialAsync(_userId, Guid.NewGuid());

        result.Success.Should().BeFalse();
        result.Error.Should().Be("CredentialNotFound");
        result.Status.Should().BeNull();
    }

    [Fact]
    public async Task Deactivate_CredentialOfAnotherUser_ReportsNotFound()
    {
        var credential = MakeCredential();
        credential.UserId = Guid.NewGuid();
        SetupCredential(credential);

        var result = await _sut.DeactivateCredentialAsync(_userId, credential.Id);

        result.Success.Should().BeFalse();
        result.Error.Should().Be("CredentialNotFound");
        _repo.Verify(r => r.DeactivateAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Deactivate_RevokedCredential_IsRejectedAsTerminal()
    {
        var credential = MakeCredential(active: false, revokedAt: SystemClock.UtcNow.AddHours(-1));
        SetupCredential(credential);

        var result = await _sut.DeactivateCredentialAsync(_userId, credential.Id);

        result.Success.Should().BeFalse();
        result.Error.Should().Be("InvalidTransition");
        result.Status.Should().Be(WebAuthnCredentialStatus.Revoked);
        result.ErrorDescription.Should().Contain("terminal");
        _repo.Verify(r => r.DeactivateAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Deactivate_AlreadyDeactivatedCredential_IsRejected()
    {
        var credential = MakeCredential(active: false, deactivatedAt: SystemClock.UtcNow.AddHours(-1));
        SetupCredential(credential);

        var result = await _sut.DeactivateCredentialAsync(_userId, credential.Id);

        result.Success.Should().BeFalse();
        result.Error.Should().Be("InvalidTransition");
        result.Status.Should().Be(WebAuthnCredentialStatus.Deactivated);
    }

    [Fact]
    public async Task Activate_DeactivatedCredential_Succeeds()
    {
        var credential = MakeCredential(active: false, deactivatedAt: SystemClock.UtcNow.AddHours(-1));
        SetupCredential(credential);
        _repo.Setup(r => r.ReactivateAsync(credential.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result = await _sut.ActivateCredentialAsync(_userId, credential.Id);

        result.Success.Should().BeTrue();
        result.Status.Should().Be(WebAuthnCredentialStatus.Active);
    }

    [Fact]
    public async Task Activate_RevokedCredential_IsNeverReactivated()
    {
        var credential = MakeCredential(active: false, revokedAt: SystemClock.UtcNow.AddHours(-1));
        SetupCredential(credential);

        var result = await _sut.ActivateCredentialAsync(_userId, credential.Id);

        result.Success.Should().BeFalse();
        result.Error.Should().Be("InvalidTransition");
        result.Status.Should().Be(WebAuthnCredentialStatus.Revoked);
        result.ErrorDescription.Should().Contain("terminal");
        _repo.Verify(r => r.ReactivateAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Activate_AlreadyActiveCredential_IsRejected()
    {
        var credential = MakeCredential();
        SetupCredential(credential);

        var result = await _sut.ActivateCredentialAsync(_userId, credential.Id);

        result.Success.Should().BeFalse();
        result.Error.Should().Be("InvalidTransition");
        result.Status.Should().Be(WebAuthnCredentialStatus.Active);
    }

    [Fact]
    public async Task Activate_UnknownCredential_ReportsNotFound()
    {
        _repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserWebAuthnCredential?)null);

        var result = await _sut.ActivateCredentialAsync(_userId, Guid.NewGuid());

        result.Success.Should().BeFalse();
        result.Error.Should().Be("CredentialNotFound");
    }

    [Fact]
    public async Task VerifyCredential_ReportsDeactivatedStatus()
    {
        var credential = MakeCredential(active: false, deactivatedAt: SystemClock.UtcNow.AddHours(-1));
        SetupCredential(credential);

        var result = await _sut.VerifyCredentialAsync(_userId, credential.Id);

        result.Success.Should().BeTrue();
        result.IsValid.Should().BeFalse();
        result.IsDeactivated.Should().BeTrue();
        result.IsRevoked.Should().BeFalse();
        result.Status.Should().Be(WebAuthnCredentialStatus.Deactivated);
    }

    [Fact]
    public async Task VerifyCredential_ReportsRevokedStatus()
    {
        var credential = MakeCredential(active: false, revokedAt: SystemClock.UtcNow.AddHours(-1));
        SetupCredential(credential);

        var result = await _sut.VerifyCredentialAsync(_userId, credential.Id);

        result.Success.Should().BeTrue();
        result.IsValid.Should().BeFalse();
        result.IsRevoked.Should().BeTrue();
        result.IsDeactivated.Should().BeFalse();
        result.Status.Should().Be(WebAuthnCredentialStatus.Revoked);
    }

    [Fact]
    public async Task GetCredentialById_MapsLifecycleFields()
    {
        var deactivatedAt = SystemClock.UtcNow.AddDays(-3);
        var credential = MakeCredential(active: false, deactivatedAt: deactivatedAt);
        SetupCredential(credential);

        var result = await _sut.GetCredentialByIdAsync(_userId, credential.Id);

        result.Should().NotBeNull();
        result!.Status.Should().Be(WebAuthnCredentialStatus.Deactivated);
        result.DeactivatedAt.Should().Be(deactivatedAt);
        result.RevokedAt.Should().BeNull();
    }
}

// ════════════════════════════════════════════════════════════════════════════
// #225 Credential lifecycle — command handler delegation
// ════════════════════════════════════════════════════════════════════════════
public sealed class WebAuthnCredentialLifecycleCommandHandlerTests
{
    private readonly Mock<IWebAuthnService> _webAuthnService = new();

    private WebAuthnMutationCommandHandler CreateHandler() =>
        WebAuthnMutationCommandHandlerTestsHelper.CreateHandler(_webAuthnService.Object);

    [Fact]
    public async Task DeactivateCommand_DelegatesToService()
    {
        var userId = Guid.NewGuid();
        var credentialId = Guid.NewGuid();
        var transition = new WebAuthnCredentialTransitionResult
        {
            Success = true,
            Status = WebAuthnCredentialStatus.Deactivated
        };
        _webAuthnService
            .Setup(s => s.DeactivateCredentialAsync(userId, credentialId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(transition);

        var handler = CreateHandler();
        var result = await handler.Handle(
            new DeactivateWebAuthnCredentialCommand(userId, credentialId),
            CancellationToken.None);

        result.Should().BeSameAs(transition);
    }

    [Fact]
    public async Task ActivateCommand_DelegatesToService()
    {
        var userId = Guid.NewGuid();
        var credentialId = Guid.NewGuid();
        var transition = new WebAuthnCredentialTransitionResult
        {
            Success = false,
            Error = "InvalidTransition",
            Status = WebAuthnCredentialStatus.Revoked
        };
        _webAuthnService
            .Setup(s => s.ActivateCredentialAsync(userId, credentialId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(transition);

        var handler = CreateHandler();
        var result = await handler.Handle(
            new ActivateWebAuthnCredentialCommand(userId, credentialId),
            CancellationToken.None);

        result.Should().BeSameAs(transition);
    }
}

internal static class WebAuthnMutationCommandHandlerTestsHelper
{
    public static WebAuthnMutationCommandHandler CreateHandler(IWebAuthnService webAuthnService) =>
        new(
            webAuthnService,
            Mock.Of<IJwtTokenService>(),
            Mock.Of<GameGuild.Identity.Users.IUserRepository>(),
            new Microsoft.Extensions.Configuration.ConfigurationBuilder().Build());
}

// ════════════════════════════════════════════════════════════════════════════
// #225 Credential lifecycle — controller endpoint mapping
// ════════════════════════════════════════════════════════════════════════════
public sealed class WebAuthnCredentialLifecycleEndpointTests
{
    private readonly Mock<IWebAuthnService> _webAuthnService = new();
    private readonly Mock<ISender> _sender = new();
    private readonly Guid _userId = Guid.NewGuid();

    private WebAuthnController CreateController(bool authenticated = true)
    {
        var controller = new WebAuthnController(_webAuthnService.Object, _sender.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = authenticated
                        ? new ClaimsPrincipal(new ClaimsIdentity(
                            [new Claim("id", _userId.ToString())],
                            "unit-test"))
                        : new ClaimsPrincipal(new ClaimsIdentity())
                }
            }
        };
        return controller;
    }

    private void SetupDeactivate(WebAuthnCredentialTransitionResult result) =>
        _sender.Setup(s => s.Send(
                It.IsAny<DeactivateWebAuthnCredentialCommand>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(result);

    private void SetupActivate(WebAuthnCredentialTransitionResult result) =>
        _sender.Setup(s => s.Send(
                It.IsAny<ActivateWebAuthnCredentialCommand>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(result);

    [Fact]
    public async Task DeactivateCredential_Success_ReturnsNoContent()
    {
        SetupDeactivate(new WebAuthnCredentialTransitionResult
        {
            Success = true,
            Status = WebAuthnCredentialStatus.Deactivated
        });

        var result = await CreateController().DeactivateCredential(Guid.NewGuid());

        result.Should().BeOfType<NoContentResult>();
    }

    [Fact]
    public async Task DeactivateCredential_NotFound_Returns404()
    {
        SetupDeactivate(new WebAuthnCredentialTransitionResult
        {
            Success = false,
            Error = "CredentialNotFound",
            ErrorDescription = "Credential not found",
            Status = null
        });

        var result = await CreateController().DeactivateCredential(Guid.NewGuid());

        var notFound = result.Should().BeOfType<NotFoundObjectResult>().Subject;
        notFound.StatusCode.Should().Be(StatusCodes.Status404NotFound);
        notFound.Value.Should().BeOfType<WebAuthnCredentialTransitionResult>();
    }

    [Fact]
    public async Task DeactivateCredential_Revoked_ReturnsConflict()
    {
        SetupDeactivate(new WebAuthnCredentialTransitionResult
        {
            Success = false,
            Error = "InvalidTransition",
            ErrorDescription = "A revoked credential cannot be deactivated; revocation is terminal.",
            Status = WebAuthnCredentialStatus.Revoked
        });

        var result = await CreateController().DeactivateCredential(Guid.NewGuid());

        var conflict = result.Should().BeOfType<ConflictObjectResult>().Subject;
        conflict.StatusCode.Should().Be(StatusCodes.Status409Conflict);
    }

    [Fact]
    public async Task ActivateCredential_Success_ReturnsNoContent()
    {
        SetupActivate(new WebAuthnCredentialTransitionResult
        {
            Success = true,
            Status = WebAuthnCredentialStatus.Active
        });

        var result = await CreateController().ActivateCredential(Guid.NewGuid());

        result.Should().BeOfType<NoContentResult>();
    }

    [Fact]
    public async Task ActivateCredential_Revoked_ReturnsConflict()
    {
        SetupActivate(new WebAuthnCredentialTransitionResult
        {
            Success = false,
            Error = "InvalidTransition",
            ErrorDescription = "A revoked credential cannot be reactivated; revocation is terminal.",
            Status = WebAuthnCredentialStatus.Revoked
        });

        var result = await CreateController().ActivateCredential(Guid.NewGuid());

        result.Should().BeOfType<ConflictObjectResult>();
    }

    [Fact]
    public async Task ActivateCredential_NotFound_Returns404()
    {
        SetupActivate(new WebAuthnCredentialTransitionResult
        {
            Success = false,
            Error = "CredentialNotFound"
        });

        var result = await CreateController().ActivateCredential(Guid.NewGuid());

        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task DeactivateCredential_Unauthenticated_ReturnsUnauthorized()
    {
        var result = await CreateController(authenticated: false).DeactivateCredential(Guid.NewGuid());

        result.Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public async Task ActivateCredential_Unauthenticated_ReturnsUnauthorized()
    {
        var result = await CreateController(authenticated: false).ActivateCredential(Guid.NewGuid());

        result.Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public async Task EndToEnd_ThroughTestSender_DeactivateAndActivateRoundTrip()
    {
        // Wire the controller to the real command handler backed by a mocked service facade.
        _webAuthnService
            .Setup(s => s.DeactivateCredentialAsync(_userId, It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WebAuthnCredentialTransitionResult
            {
                Success = true,
                Status = WebAuthnCredentialStatus.Deactivated
            });
        _webAuthnService
            .Setup(s => s.ActivateCredentialAsync(_userId, It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WebAuthnCredentialTransitionResult
            {
                Success = true,
                Status = WebAuthnCredentialStatus.Active
            });

        var sender = IdentityCommandTestSender.ForWebAuthn(_webAuthnService.Object);
        var controller = new WebAuthnController(_webAuthnService.Object, sender)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(
                        [new Claim("id", _userId.ToString())],
                        "unit-test"))
                }
            }
        };

        var deactivate = await controller.DeactivateCredential(Guid.NewGuid());
        var activate = await controller.ActivateCredential(Guid.NewGuid());

        deactivate.Should().BeOfType<NoContentResult>();
        activate.Should().BeOfType<NoContentResult>();
    }
}
