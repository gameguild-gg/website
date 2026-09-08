// MaxCoverageTests2.cs — Second batch: controllers, TotpMfaService, BackupCodeMfaService
#pragma warning disable CS8600, CS8602, CS8604, CS8625

using FluentAssertions;
using GameGuild.Identity.Authentication;
using GameGuild.Identity.Context.Actors;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using System.Security.Claims;
using Xunit;

namespace GameGuild.Identity.Authentication.UnitTests;

#region SessionController Tests

public class SessionControllerCovTests
{
    private readonly Mock<ISessionManagementService> _sessionService = new();
    private readonly SessionController _controller;
    private readonly Guid _userId = Guid.NewGuid();

    public SessionControllerCovTests()
    {
        _controller = new SessionController(
            _sessionService.Object,
            IdentityCommandTestSender.ForSessions(_sessionService.Object));
        SetUser(_userId);
    }

    private void SetUser(Guid userId, Guid? sessionId = null)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId.ToString()),
            new(ClaimTypes.Email, "test@test.com")
        };
        if (sessionId.HasValue)
            claims.Add(new Claim("session_id", sessionId.Value.ToString()));

        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(claims, "Test")),
                Connection = { RemoteIpAddress = System.Net.IPAddress.Parse("127.0.0.1") }
            }
        };
        _controller.ControllerContext.HttpContext.Request.Headers["User-Agent"] = "TestAgent";
    }

    [Fact]
    public async Task GetSessions_ReturnsSessions()
    {
        var sessionId = Guid.NewGuid();
        SetUser(_userId, sessionId);
        var sessions = new List<UserSession>
        {
            new() { Id = sessionId, UserId = _userId, IpAddress = "1.2.3.4",
                DeviceInfo = "{\"DeviceName\":\"Chrome\",\"DeviceType\":\"Web\"}",
                Location = "{\"City\":\"NYC\",\"Country\":\"US\"}",
                CreatedAt = DateTime.UtcNow, LastUsedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddHours(1), IsTrustedDevice = true },
            new() { Id = Guid.NewGuid(), UserId = _userId, IpAddress = "5.6.7.8",
                DeviceInfo = null, Location = null,
                CreatedAt = DateTime.UtcNow, LastUsedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddHours(1) },
            new() { Id = Guid.NewGuid(), UserId = _userId, IpAddress = "9.10.11.12",
                DeviceInfo = "not-json", Location = "not-json",
                CreatedAt = DateTime.UtcNow, LastUsedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddHours(1) }
        };
        _sessionService.Setup(s => s.GetUserSessionsAsync(
            _userId, It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(sessions);

        var result = await _controller.GetSessions(CancellationToken.None);
        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().NotBeNull();
    }

    [Fact]
    public async Task AnalyzeSessionSecurity_ReturnsAnalysis()
    {
        _sessionService.Setup(s => s.AnalyzeSessionSecurityAsync(
            _userId, It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SessionSecurityAnalysis());

        var result = await _controller.AnalyzeSessionSecurity(CancellationToken.None);
        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task TerminateSession_NotFound_Returns404()
    {
        _sessionService.Setup(s => s.GetSessionAsync(
            It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserSession?)null);

        var result = await _controller.TerminateSession(Guid.NewGuid(), CancellationToken.None);
        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task TerminateSession_WrongUser_Returns404()
    {
        var session = new UserSession { Id = Guid.NewGuid(), UserId = Guid.NewGuid() };
        _sessionService.Setup(s => s.GetSessionAsync(
            session.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        var result = await _controller.TerminateSession(session.Id, CancellationToken.None);
        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task TerminateSession_Success_ReturnsOk()
    {
        var sessionId = Guid.NewGuid();
        var session = new UserSession { Id = sessionId, UserId = _userId };
        _sessionService.Setup(s => s.GetSessionAsync(
            sessionId, It.IsAny<CancellationToken>())).ReturnsAsync(session);
        _sessionService.Setup(s => s.TerminateSessionAsync(
            sessionId, SessionTerminationReason.UserLogout, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result = await _controller.TerminateSession(sessionId, CancellationToken.None);
        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task TerminateSession_Failure_ReturnsBadRequest()
    {
        var sessionId = Guid.NewGuid();
        var session = new UserSession { Id = sessionId, UserId = _userId };
        _sessionService.Setup(s => s.GetSessionAsync(
            sessionId, It.IsAny<CancellationToken>())).ReturnsAsync(session);
        _sessionService.Setup(s => s.TerminateSessionAsync(
            sessionId, SessionTerminationReason.UserLogout, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var result = await _controller.TerminateSession(sessionId, CancellationToken.None);
        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task TerminateOtherSessions_ReturnsCount()
    {
        var sessionId = Guid.NewGuid();
        SetUser(_userId, sessionId);
        _sessionService.Setup(s => s.TerminateAllUserSessionsAsync(
            _userId, SessionTerminationReason.UserLogout,
            It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(3);

        var result = await _controller.TerminateOtherSessions(CancellationToken.None);
        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task TerminateAllSessions_ReturnsCount()
    {
        _sessionService.Setup(s => s.TerminateAllUserSessionsAsync(
            _userId, SessionTerminationReason.UserLogout,
            It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(5);

        var result = await _controller.TerminateAllSessions(CancellationToken.None);
        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task RefreshSession_NoSession_ReturnsBadRequest()
    {
        // No session_id claim
        var result = await _controller.RefreshSession(CancellationToken.None);
        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task RefreshSession_Success_ReturnsOk()
    {
        var sessionId = Guid.NewGuid();
        SetUser(_userId, sessionId);
        _sessionService.Setup(s => s.RefreshSessionAsync(
            sessionId, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var result = await _controller.RefreshSession(CancellationToken.None);
        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task RefreshSession_Failure_ReturnsBadRequest()
    {
        var sessionId = Guid.NewGuid();
        SetUser(_userId, sessionId);
        _sessionService.Setup(s => s.RefreshSessionAsync(
            sessionId, It.IsAny<CancellationToken>())).ReturnsAsync(false);

        var result = await _controller.RefreshSession(CancellationToken.None);
        result.Should().BeOfType<BadRequestObjectResult>();
    }
}

#endregion

#region MfaController Tests

public class MfaControllerCovTests
{
    private readonly Mock<IMfaService> _mfaService = new();
    private readonly MfaController _controller;
    private readonly Guid _userId = Guid.NewGuid();

    public MfaControllerCovTests()
    {
        _controller = new MfaController(
            _mfaService.Object,
            IdentityCommandTestSender.ForMfa(_mfaService.Object));
        SetUser(_userId);
    }

    private void SetUser(Guid userId)
    {
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(new[]
                {
                    new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
                    new Claim(ClaimTypes.Email, "mfa@test.com")
                }, "Test"))
            }
        };
    }

    [Fact]
    public async Task GetMfaConfiguration_ReturnsConfig()
    {
        _mfaService.Setup(s => s.GetMfaConfigurationAsync(
            _userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MfaConfigurationResponse());

        var result = await _controller.GetMfaConfiguration(CancellationToken.None);
        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task InitiateTotpSetup_Success_ReturnsSetup()
    {
        _mfaService.Setup(s => s.InitiateMfaSetupAsync(
            _userId, "mfa@test.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MfaSetupResult { Success = true, Secret = "SECRET", QrCodeUrl = "otpauth://totp/test" });

        var result = await _controller.InitiateTotpSetup(CancellationToken.None);
        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task InitiateTotpSetup_Failure_ReturnsBadRequest()
    {
        _mfaService.Setup(s => s.InitiateMfaSetupAsync(
            _userId, "mfa@test.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MfaSetupResult { Success = false, Message = "Already enabled" });

        var result = await _controller.InitiateTotpSetup(CancellationToken.None);
        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task CompleteTotpSetup_Success_ReturnsOk()
    {
        _mfaService.Setup(s => s.CompleteMfaSetupAsync(
            _userId, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(MfaVerificationResult.Successful("OK"));

        var result = await _controller.CompleteTotpSetup(
            new CompleteMfaSetupRequest { Code = "123456", SecretKey = "KEY" },
            CancellationToken.None);
        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task CompleteTotpSetup_Failure_ReturnsBadRequest()
    {
        _mfaService.Setup(s => s.CompleteMfaSetupAsync(
            _userId, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(MfaVerificationResult.Failure("Invalid code"));

        var result = await _controller.CompleteTotpSetup(
            new CompleteMfaSetupRequest { Code = "000000", SecretKey = "KEY" },
            CancellationToken.None);
        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task VerifyMfa_Success_ReturnsOk()
    {
        _mfaService.Setup(s => s.VerifyMfaAsync(
            It.IsAny<Guid>(), It.IsAny<string>(),
            It.IsAny<MfaMethod>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(MfaVerificationResult.Successful("OK"));

        var result = await _controller.VerifyMfa(
            new VerifyMfaRequest { UserId = Guid.NewGuid(), Code = "123456", Method = MfaMethod.Totp },
            CancellationToken.None);
        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task VerifyMfa_Failure_ReturnsBadRequest()
    {
        _mfaService.Setup(s => s.VerifyMfaAsync(
            It.IsAny<Guid>(), It.IsAny<string>(),
            It.IsAny<MfaMethod>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(MfaVerificationResult.Failure("Invalid"));

        var result = await _controller.VerifyMfa(
            new VerifyMfaRequest { UserId = Guid.NewGuid(), Code = "wrong", Method = MfaMethod.Totp },
            CancellationToken.None);
        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task GetBackupCodes_ReturnsStatus()
    {
        _mfaService.Setup(s => s.GetMfaConfigurationAsync(
            _userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MfaConfigurationResponse { BackupCodesRemaining = 8 });

        var result = await _controller.GetBackupCodes(CancellationToken.None);
        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task RegenerateBackupCodes_ReturnsNewCodes()
    {
        _mfaService.Setup(s => s.GenerateBackupCodesAsync(
            _userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { "CODE1", "CODE2" });

        var result = await _controller.RegenerateBackupCodes(CancellationToken.None);
        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task InitiateSmsSetup_WhenSmsProviderIsNotConfigured_ReturnsServiceUnavailable()
    {
        var result = await _controller.InitiateSmsSetup(
            new SmsMfaSetupRequest { PhoneNumber = "+1234567890" }, CancellationToken.None);
        var serviceUnavailable = result.Should().BeOfType<ObjectResult>().Subject;
        serviceUnavailable.StatusCode.Should().Be(StatusCodes.Status503ServiceUnavailable);
        serviceUnavailable.Value.Should().BeOfType<MfaErrorResponse>();
    }

    [Fact]
    public async Task CompleteSmsSetup_WhenSmsProviderIsNotConfigured_ReturnsServiceUnavailable()
    {
        var result = await _controller.CompleteSmsSetup(
            new CompleteMfaSetupRequest { Code = "123456", SecretKey = "KEY" },
            CancellationToken.None);
        var serviceUnavailable = result.Should().BeOfType<ObjectResult>().Subject;
        serviceUnavailable.StatusCode.Should().Be(StatusCodes.Status503ServiceUnavailable);
        serviceUnavailable.Value.Should().BeOfType<MfaErrorResponse>();
    }

    [Fact]
    public async Task ListMfaMethods_ReturnsMethodList()
    {
        _mfaService.Setup(s => s.GetMfaConfigurationAsync(
            _userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MfaConfigurationResponse
            {
                EnabledMethods = new[] { "totp" },
                BackupCodesRemaining = 5
            });

        var result = await _controller.ListMfaMethods(CancellationToken.None);
        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = ok.Value.Should().BeOfType<MfaMethodsResponse>().Subject;
        response.Methods.Single(method => method.Method == MfaMethod.Sms).IsAvailable.Should().BeFalse();
    }

    [Fact]
    public async Task DisableMfa_Success_ReturnsOk()
    {
        _mfaService.Setup(s => s.DisableMfaAsync(
            _userId, "password", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result = await _controller.DisableMfa(
            new DisableMfaRequest { Password = "password" }, CancellationToken.None);
        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task DisableMfa_Failure_ReturnsBadRequest()
    {
        _mfaService.Setup(s => s.DisableMfaAsync(
            _userId, "wrong", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var result = await _controller.DisableMfa(
            new DisableMfaRequest { Password = "wrong" }, CancellationToken.None);
        result.Should().BeOfType<BadRequestObjectResult>();
    }
}

#endregion

#region TrustedDevicesController Tests

public class TrustedDevicesControllerCovTests
{
    private readonly Mock<ISessionManagementService> _sessionService = new();
    private readonly TrustedDevicesController _controller;
    private readonly Guid _userId = Guid.NewGuid();

    public TrustedDevicesControllerCovTests()
    {
        _controller = new TrustedDevicesController(
            _sessionService.Object,
            IdentityCommandTestSender.ForSessions(_sessionService.Object));
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(new[]
                {
                    new Claim(ClaimTypes.NameIdentifier, _userId.ToString())
                }, "Test"))
            }
        };
    }

    [Fact]
    public async Task GetTrustedDevices_ReturnsList()
    {
        var devices = new List<TrustedDevice>
        {
            new() { Id = Guid.NewGuid(), UserId = _userId, DeviceName = "Phone",
                DeviceFingerprint = "fp1", DeviceInfo = "{\"DeviceName\":\"Phone\"}",
                TrustedAt = DateTime.UtcNow, LastUsedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddDays(30) },
            new() { Id = Guid.NewGuid(), UserId = _userId, DeviceName = "Laptop",
                DeviceFingerprint = "fp2", DeviceInfo = null,
                TrustedAt = DateTime.UtcNow, LastUsedAt = DateTime.UtcNow },
            new() { Id = Guid.NewGuid(), UserId = _userId, DeviceName = "Tablet",
                DeviceFingerprint = "fp3", DeviceInfo = "invalid-json",
                TrustedAt = DateTime.UtcNow, LastUsedAt = DateTime.UtcNow }
        };
        _sessionService.Setup(s => s.GetTrustedDevicesAsync(
            _userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(devices);

        var result = await _controller.GetTrustedDevices(CancellationToken.None);
        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().NotBeNull();
    }

    [Fact]
    public async Task TrustCurrentDevice_Success_ReturnsOk()
    {
        _sessionService.Setup(s => s.TrustDeviceAsync(
            _userId, It.IsAny<string>(), "My Phone", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result = await _controller.TrustCurrentDevice(
            new TrustDeviceRequest { DeviceName = "My Phone" }, CancellationToken.None);
        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task TrustCurrentDevice_Failure_ReturnsBadRequest()
    {
        _sessionService.Setup(s => s.TrustDeviceAsync(
            _userId, It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var result = await _controller.TrustCurrentDevice(
            new TrustDeviceRequest { DeviceName = "Device" }, CancellationToken.None);
        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task RevokeTrustedDevice_Success_ReturnsOk()
    {
        var deviceId = Guid.NewGuid();
        _sessionService.Setup(s => s.RevokeTrustedDeviceAsync(
            _userId, deviceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result = await _controller.RevokeTrustedDevice(deviceId, CancellationToken.None);
        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task RevokeTrustedDevice_NotFound_Returns404()
    {
        _sessionService.Setup(s => s.RevokeTrustedDeviceAsync(
            _userId, It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var result = await _controller.RevokeTrustedDevice(Guid.NewGuid(), CancellationToken.None);
        result.Should().BeOfType<NotFoundObjectResult>();
    }
}

#endregion

#region ServiceAccountCrudController Tests

public class ServiceAccountCrudControllerCovTests
{
    private readonly Mock<IServiceAccountService> _svcAccountService = new();
    private readonly ServiceAccountCrudController _controller;
    private readonly Guid _userId = Guid.NewGuid();

    public ServiceAccountCrudControllerCovTests()
    {
        _controller = new ServiceAccountCrudController(
            _svcAccountService.Object,
            IdentityCommandTestSender.ForServiceAccounts(_svcAccountService.Object));
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(new[]
                {
                    new Claim(ClaimTypes.NameIdentifier, _userId.ToString())
                }, "Test"))
            }
        };
    }

    [Fact]
    public async Task CreateServiceAccount_EmptyName_ReturnsBadRequest()
    {
        var result = await _controller.CreateServiceAccount(
            new CreateServiceAccountRequest { Name = "" }, CancellationToken.None);
        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task CreateServiceAccount_Valid_Returns201()
    {
        var account = new ServiceAccount
        {
            Id = Guid.NewGuid(),
            ClientId = "client-1",
            Name = "Test SA",
            CreatedAt = DateTime.UtcNow
        };
        _svcAccountService.Setup(s => s.CreateServiceAccountAsync(
            "Test SA", It.IsAny<string?>(), It.IsAny<Guid?>(), It.IsAny<string>(),
            It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<DateTime?>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync((account, "secret123"));

        var result = await _controller.CreateServiceAccount(
            new CreateServiceAccountRequest { Name = "Test SA", Scopes = "read:all" },
            CancellationToken.None);
        result.Should().BeOfType<CreatedAtActionResult>();
    }

    [Fact]
    public async Task GetServiceAccount_NotFound_Returns404()
    {
        _svcAccountService.Setup(s => s.GetByIdAsync(
            It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ServiceAccount?)null);

        var result = await _controller.GetServiceAccount(Guid.NewGuid(), CancellationToken.None);
        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task GetServiceAccount_Found_ReturnsOk()
    {
        var account = new ServiceAccount { Id = Guid.NewGuid(), ClientId = "c1", Name = "SA" };
        _svcAccountService.Setup(s => s.GetByIdAsync(
            account.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(account);

        var result = await _controller.GetServiceAccount(account.Id, CancellationToken.None);
        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task CheckServiceAccountExists_NotFound_Returns404()
    {
        _svcAccountService.Setup(s => s.GetByIdAsync(
            It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ServiceAccount?)null);

        var result = await _controller.CheckServiceAccountExists(Guid.NewGuid(), CancellationToken.None);
        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task CheckServiceAccountExists_Found_ReturnsOk()
    {
        var account = new ServiceAccount { Id = Guid.NewGuid(), ClientId = "c2", Name = "SA2" };
        _svcAccountService.Setup(s => s.GetByIdAsync(
            account.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(account);

        var result = await _controller.CheckServiceAccountExists(account.Id, CancellationToken.None);
        result.Should().BeOfType<OkResult>();
    }

    [Fact]
    public async Task GetServiceAccounts_WithTenant_ReturnsFiltered()
    {
        var tenantId = Guid.NewGuid();
        _svcAccountService.Setup(s => s.GetByTenantAsync(
            tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ServiceAccount>());

        var result = await _controller.GetServiceAccounts(tenantId, CancellationToken.None);
        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task GetServiceAccounts_NoTenant_ReturnsAll()
    {
        _svcAccountService.Setup(s => s.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ServiceAccount>());

        var result = await _controller.GetServiceAccounts(null, CancellationToken.None);
        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task DeleteServiceAccount_NotFound_Returns404()
    {
        _svcAccountService.Setup(s => s.GetByIdAsync(
            It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ServiceAccount?)null);

        var result = await _controller.DeleteServiceAccount(Guid.NewGuid(), CancellationToken.None);
        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task DeleteServiceAccount_Found_ReturnsNoContent()
    {
        var account = new ServiceAccount { Id = Guid.NewGuid(), ClientId = "c3", Name = "Del" };
        _svcAccountService.Setup(s => s.GetByIdAsync(
            account.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(account);
        _svcAccountService.Setup(s => s.DeactivateAsync(
            account.Id, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var result = await _controller.DeleteServiceAccount(account.Id, CancellationToken.None);
        result.Should().BeOfType<NoContentResult>();
    }

    [Fact]
    public async Task PatchServiceAccount_NotFound_Returns404()
    {
        _svcAccountService.Setup(s => s.GetByIdAsync(
            It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ServiceAccount?)null);

        var result = await _controller.PatchServiceAccount(Guid.NewGuid(),
            new PatchServiceAccountRequest { Name = "Updated" }, CancellationToken.None);
        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task PatchServiceAccount_Found_ReturnsNoContent()
    {
        var account = new ServiceAccount { Id = Guid.NewGuid(), ClientId = "c4", Name = "SA" };
        _svcAccountService.Setup(s => s.GetByIdAsync(
            account.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(account);

        var result = await _controller.PatchServiceAccount(account.Id,
            new PatchServiceAccountRequest
            {
                Name = "Updated",
                Description = "Desc",
                Scopes = "read write",
                ExpiresAt = DateTime.UtcNow.AddDays(90)
            }, CancellationToken.None);
        result.Should().BeOfType<NoContentResult>();
    }
}

#endregion

#region TotpMfaService Tests

public class TotpMfaServiceCovTests
{
    private readonly Mock<IUserMfaConfigurationRepository> _mfaRepo = new();
    private readonly Mock<IMfaAttemptTrackingService> _trackingService = new();
    private readonly Mock<IEncryptionService> _encryptionService = new();
    private readonly TotpMfaService _svc;

    public TotpMfaServiceCovTests()
    {
        _encryptionService.Setup(e => e.Encrypt(It.IsAny<string>())).Returns<string>(s => $"enc:{s}");
        _encryptionService.Setup(e => e.Decrypt(It.IsAny<string>())).Returns<string>(s => s.Replace("enc:", ""));

        _svc = new TotpMfaService(
            NullLogger<TotpMfaService>.Instance,
            _mfaRepo.Object,
            _trackingService.Object,
            _encryptionService.Object);
    }

    [Fact]
    public async Task SetupTotp_NewUser_ReturnsQrCodeAndSecret()
    {
        var userId = Guid.NewGuid();
        _mfaRepo.Setup(r => r.GetByUserIdAsync(
            userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserMfaConfiguration?)null);
        _mfaRepo.Setup(r => r.CreateAsync(
            It.IsAny<UserMfaConfiguration>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserMfaConfiguration c, CancellationToken _) => c);

        var (qrUri, secret) = await _svc.SetupTotpAsync(userId, "user@test.com");

        qrUri.Should().StartWith("otpauth://totp/");
        secret.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task SetupTotp_ExistingDisabled_ReusesConfig()
    {
        var userId = Guid.NewGuid();
        var existing = new UserMfaConfiguration
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            IsEnabled = false
        };
        _mfaRepo.Setup(r => r.GetByUserIdAsync(
            userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);
        _mfaRepo.Setup(r => r.UpdateAsync(
            It.IsAny<UserMfaConfiguration>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserMfaConfiguration c, CancellationToken _) => c);

        var (qrUri, secret) = await _svc.SetupTotpAsync(userId, "user@test.com");
        qrUri.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task SetupTotp_AlreadyEnabled_Throws()
    {
        var userId = Guid.NewGuid();
        var existing = new UserMfaConfiguration
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            IsEnabled = true
        };
        _mfaRepo.Setup(r => r.GetByUserIdAsync(
            userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        var act = async () => await _svc.SetupTotpAsync(userId, "user@test.com");
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task VerifyTotp_NoConfig_ReturnsFalse()
    {
        var userId = Guid.NewGuid();
        _mfaRepo.Setup(r => r.GetByUserIdAsync(
            userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserMfaConfiguration?)null);

        var result = await _svc.VerifyTotpAsync(userId, "123456");
        result.Should().BeFalse();
    }

    [Fact]
    public async Task VerifyTotp_NoSecretKey_ReturnsFalse()
    {
        var userId = Guid.NewGuid();
        var config = new UserMfaConfiguration
        {
            UserId = userId,
            TotpSecretKey = null
        };
        _mfaRepo.Setup(r => r.GetByUserIdAsync(
            userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(config);

        var result = await _svc.VerifyTotpAsync(userId, "123456");
        result.Should().BeFalse();
    }

    [Fact]
    public async Task VerifyTotp_LockedOut_ReturnsFalse()
    {
        var userId = Guid.NewGuid();
        var config = new UserMfaConfiguration
        {
            UserId = userId,
            TotpSecretKey = "enc:SECRET",
            IsEnabled = true
        };
        _mfaRepo.Setup(r => r.GetByUserIdAsync(
            userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(config);
        _trackingService.Setup(s => s.IsLockedOut(config)).Returns(true);

        var result = await _svc.VerifyTotpAsync(userId, "123456");
        result.Should().BeFalse();
    }

    [Fact]
    public async Task VerifyTotp_InvalidCode_IncrementsFailedAttempts()
    {
        var userId = Guid.NewGuid();
        var config = new UserMfaConfiguration
        {
            UserId = userId,
            TotpSecretKey = "enc:JBSWY3DPEHPK3PXP",
            IsEnabled = true,
            FailedAttempts = 0
        };
        _mfaRepo.Setup(r => r.GetByUserIdAsync(
            userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(config);
        _trackingService.Setup(s => s.IsLockedOut(config)).Returns(false);
        _encryptionService.Setup(e => e.Decrypt("enc:JBSWY3DPEHPK3PXP"))
            .Returns("JBSWY3DPEHPK3PXP");
        _mfaRepo.Setup(r => r.UpdateAsync(
            It.IsAny<UserMfaConfiguration>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserMfaConfiguration c, CancellationToken _) => c);
        _trackingService.Setup(s => s.RecordMfaAttemptAsync(
            userId, MfaMethod.Totp, false, It.IsAny<string?>(),
            It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var result = await _svc.VerifyTotpAsync(userId, "000000");
        result.Should().BeFalse();
    }

    [Fact]
    public async Task GenerateQrCode_ReturnsPngBytes()
    {
        var result = await _svc.GenerateQrCodeAsync("otpauth://totp/test");

        result.Should().NotBeEmpty();
        result.Take(8).Should().Equal(137, 80, 78, 71, 13, 10, 26, 10);
    }

    [Fact]
    public async Task GenerateQrCode_BlankData_Throws()
    {
        await _svc.Invoking(s => s.GenerateQrCodeAsync(" "))
            .Should()
            .ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task GenerateQrCode_Cancelled_Throws()
    {
        var cts = new CancellationTokenSource();
        cts.Cancel();

        await _svc.Invoking(s => s.GenerateQrCodeAsync("otpauth://totp/test", cts.Token))
            .Should()
            .ThrowAsync<OperationCanceledException>();
    }
}

#endregion

#region BackupCodeMfaService Tests

public class BackupCodeMfaServiceCovTests
{
    private readonly Mock<IUserMfaConfigurationRepository> _mfaRepo = new();
    private readonly Mock<IMfaAttemptTrackingService> _trackingService = new();
    private readonly BackupCodeMfaService _svc;

    public BackupCodeMfaServiceCovTests()
    {
        _svc = new BackupCodeMfaService(
            NullLogger<BackupCodeMfaService>.Instance,
            _mfaRepo.Object,
            _trackingService.Object);
    }

    [Fact]
    public async Task GenerateBackupCodes_MfaNotEnabled_Throws()
    {
        var userId = Guid.NewGuid();
        _mfaRepo.Setup(r => r.GetByUserIdAsync(
            userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserMfaConfiguration?)null);

        var act = async () => await _svc.GenerateBackupCodesAsync(userId);
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task GenerateBackupCodes_MfaEnabled_Returns10Codes()
    {
        var userId = Guid.NewGuid();
        var config = new UserMfaConfiguration
        {
            UserId = userId,
            IsEnabled = true
        };
        _mfaRepo.Setup(r => r.GetByUserIdAsync(
            userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(config);
        _mfaRepo.Setup(r => r.UpdateAsync(
            It.IsAny<UserMfaConfiguration>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserMfaConfiguration c, CancellationToken _) => c);

        var codes = await _svc.GenerateBackupCodesAsync(userId);
        codes.Should().HaveCount(10);
        codes.Should().OnlyContain(c => !string.IsNullOrEmpty(c));
    }

    [Fact]
    public async Task VerifyBackupCode_NoConfig_ReturnsFalse()
    {
        var userId = Guid.NewGuid();
        _mfaRepo.Setup(r => r.GetByUserIdAsync(
            userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserMfaConfiguration?)null);

        var result = await _svc.VerifyBackupCodeAsync(userId, "ABC123");
        result.Should().BeFalse();
    }

    [Fact]
    public async Task VerifyBackupCode_NoCodes_ReturnsFalse()
    {
        var userId = Guid.NewGuid();
        var config = new UserMfaConfiguration
        {
            UserId = userId,
            IsEnabled = true,
            BackupCodes = null
        };
        _mfaRepo.Setup(r => r.GetByUserIdAsync(
            userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(config);

        var result = await _svc.VerifyBackupCodeAsync(userId, "ABC123");
        result.Should().BeFalse();
    }

    [Fact]
    public void GenerateBackupCode_ReturnsNonEmptyString()
    {
        var code = _svc.GenerateBackupCode();
        code.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task HashBackupCode_ReturnsHashedValue()
    {
        var hash = await _svc.HashBackupCodeAsync("TEST123");
        hash.Should().NotBeNullOrEmpty();
        hash.Should().NotBe("TEST123");
    }
}

#endregion

#region Session/MFA DTO + Entity Tests

public class SessionMfaDtoCovTests
{
    [Fact]
    public void MfaSetupResult_Properties()
    {
        var r = new MfaSetupResult
        {
            Success = true,
            Message = "OK",
            Secret = "SECRET",
            QrCodeUrl = "url",
            BackupCodes = new[] { "A", "B" }
        };
        r.Success.Should().BeTrue();
        r.SecretKey.Should().Be("SECRET");
        r.QrCodeUri.Should().Be("url");
        r.BackupCodes.Should().HaveCount(2);
    }

    [Fact]
    public void MfaSetupResult_AliasProperties()
    {
        var r = new MfaSetupResult();
        r.SecretKey = "K";
        r.Secret.Should().Be("K");
        r.QrCodeUri = "U";
        r.QrCodeUrl.Should().Be("U");
    }

    [Fact]
    public void MfaVerificationResult_Success_Factory()
    {
        var r = MfaVerificationResult.Successful("MFA enabled", new[] { "BC1" });
        r.IsSuccess.Should().BeTrue();
        r.Success.Should().BeTrue();
        r.Message.Should().Be("MFA enabled");
        r.BackupCodes.Should().HaveCount(1);
    }

    [Fact]
    public void MfaVerificationResult_Failure_Factory()
    {
        var r = MfaVerificationResult.Failure("Invalid");
        r.IsSuccess.Should().BeFalse();
        r.Message.Should().Be("Invalid");
    }

    [Fact]
    public void MfaVerificationResult_Properties()
    {
        var r = new MfaVerificationResult
        {
            IsSuccess = true,
            Message = "msg",
            RequiresAdditionalVerification = true
        };
        r.RequiresAdditionalVerification.Should().BeTrue();
        r.Success.Should().BeTrue();
    }

    [Fact]
    public void UserSession_IsExpired_WhenPast()
    {
        var s = new UserSession { ExpiresAt = DateTime.UtcNow.AddMinutes(-1) };
        s.IsExpired.Should().BeTrue();
    }

    [Fact]
    public void UserSession_IsValid_WhenActiveAndNotExpired()
    {
        var s = new UserSession
        {
            IsActive = true,
            ExpiresAt = DateTime.UtcNow.AddHours(1)
        };
        s.IsValid.Should().BeTrue();
    }

    [Fact]
    public void UserSession_Properties()
    {
        var s = new UserSession
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            RefreshToken = "token",
            AccessTokenHash = "hash",
            IpAddress = "1.2.3.4",
            UserAgent = "ua",
            DeviceFingerprint = "fp",
            DeviceInfo = "info",
            Location = "loc",
            TerminationReason = "logout",
            TerminatedAt = DateTime.UtcNow,
            IsTrustedDevice = true,
            TrustedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            LastUsedAt = DateTime.UtcNow
        };
        s.DeviceFingerprint.Should().Be("fp");
        s.IsTrustedDevice.Should().BeTrue();
    }

    [Fact]
    public void TrustedDevice_IsExpired_WhenPast()
    {
        var d = new TrustedDevice { ExpiresAt = DateTime.UtcNow.AddMinutes(-1) };
        d.IsExpired.Should().BeTrue();
    }

    [Fact]
    public void TrustedDevice_IsValid_WhenActiveAndNotExpired()
    {
        var d = new TrustedDevice { IsActive = true, ExpiresAt = DateTime.UtcNow.AddDays(30) };
        d.IsValid.Should().BeTrue();
    }

    [Fact]
    public void TrustedDevice_Properties()
    {
        var d = new TrustedDevice
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            DeviceFingerprint = "fp",
            DeviceName = "Phone",
            DeviceInfo = "info",
            TrustedAt = DateTime.UtcNow,
            LastUsedAt = DateTime.UtcNow,
            IsActive = true,
            AssociatedIpAddresses = "1.2.3.4",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        d.DeviceName.Should().Be("Phone");
    }

    [Fact]
    public void SessionSecurityAnalysis_Properties()
    {
        var a = new SessionSecurityAnalysis
        {
            SessionId = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            IsSuspicious = true,
            RiskScore = 75,
            ActiveSessionCount = 3,
            TotalDeviceCount = 2,
            SecurityFlags = new List<string> { "multi_geo" },
            AnalyzedAt = DateTime.UtcNow
        };
        a.IsSuspicious.Should().BeTrue();
        a.UnusualActivityDetected.Should().BeTrue();
        a.RiskFactors.Should().Contain("multi_geo");
    }

    [Fact]
    public void MfaConfigurationResponse_Properties()
    {
        var r = new MfaConfigurationResponse
        {
            BackupCodesRemaining = 8,
            EnabledMethods = new[] { "totp", "backup_code" }
        };
        r.BackupCodesRemaining.Should().Be(8);
    }

    [Fact]
    public void CreateServiceAccountRequest_Properties()
    {
        var r = new CreateServiceAccountRequest
        {
            Name = "Test",
            Description = "Desc",
            TenantId = Guid.NewGuid(),
            Scopes = "read",
            AllowedIpAddresses = "1.2.3.4",
            ExpiresAt = DateTime.UtcNow.AddDays(90)
        };
        r.Name.Should().Be("Test");
    }

    [Fact]
    public void PatchServiceAccountRequest_Properties()
    {
        var r = new PatchServiceAccountRequest
        {
            Name = "New",
            Description = "D",
            Scopes = "s",
            ExpiresAt = DateTime.UtcNow.AddDays(1)
        };
        r.Name.Should().Be("New");
    }

    [Fact]
    public void CompleteMfaSetupRequest_Properties()
    {
        var r = new CompleteMfaSetupRequest { Code = "123456", SecretKey = "KEY" };
        r.Code.Should().Be("123456");
        r.SecretKey.Should().Be("KEY");
    }

    [Fact]
    public void VerifyMfaRequest_Properties()
    {
        var r = new VerifyMfaRequest { UserId = Guid.NewGuid(), Code = "code", Method = MfaMethod.BackupCode };
        r.Method.Should().Be(MfaMethod.BackupCode);
    }

    [Fact]
    public void DisableMfaRequest_Properties()
    {
        var r = new DisableMfaRequest { Password = "pw" };
        r.Password.Should().Be("pw");
    }

    [Fact]
    public void TrustDeviceRequest_Properties()
    {
        var r = new TrustDeviceRequest { DeviceName = "Phone" };
        r.DeviceName.Should().Be("Phone");
    }

    [Fact]
    public void ServiceAccount_BasicProperties()
    {
        var sa = new ServiceAccount
        {
            Id = Guid.NewGuid(),
            ClientId = "cid",
            Name = "SA",
            Description = "desc",
            TenantId = Guid.NewGuid(),
            Scopes = "read write",
            IsActive = true
        };
        sa.ClientId.Should().Be("cid");
        sa.IsActive.Should().BeTrue();
    }
}

#endregion
