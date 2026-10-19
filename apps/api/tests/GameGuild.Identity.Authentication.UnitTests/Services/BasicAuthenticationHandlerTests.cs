using System.Security.Claims;
using System.Text;
using System.Text.Encodings.Web;
using GameGuild.Configuration.PresentationLayer.Authentication;
using GameGuild.Identity.Users;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace GameGuild.Identity.Authentication.UnitTests.Services;

public sealed class BasicAuthenticationHandlerTests
{
    [Fact]
    public async Task AuthenticateAsync_ValidEmailAndUtf8Credentials_ReturnsIdentityClaims()
    {
        var user = User.Create("person@example.com", "Example Person");
        user.SetPasswordHash("stored-hash");
        var userRepository = new Mock<IUserRepository>();
        userRepository.Setup(repository => repository.GetByEmailAsync("person@example.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        var passwordHasher = new Mock<IPasswordHasher>();
        passwordHasher.Setup(hasher => hasher.VerifyPassword("stored-hash", "correct:pässword"))
            .Returns(true);
        var mfaRepository = new Mock<IUserMfaConfigurationRepository>();
        mfaRepository.Setup(repository => repository.IsMfaEnabledAsync(user.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        var context = CreateContext("https", "person@example.com:correct:pässword");
        context.Connection.RemoteIpAddress = System.Net.IPAddress.Parse("192.0.2.10");
        context.Request.Headers.UserAgent = "TestAgent/1.0";
        var auditSink = new Mock<IAuthenticationAuditEventSink>();
        var handler = CreateHandler(userRepository.Object, passwordHasher.Object, mfaRepository.Object, auditEventSink: auditSink.Object);
        await InitializeAsync(handler, context);

        var result = await handler.AuthenticateAsync();

        Assert.True(result.Succeeded);
        Assert.Equal(BasicAuthenticationSettings.DefaultSchemeName, result.Principal!.Identity!.AuthenticationType);
        Assert.Equal(user.Id.ToString(), result.Principal.FindFirstValue("sub"));
        Assert.Equal(user.Id.ToString(), result.Principal.FindFirstValue(ClaimTypes.NameIdentifier));
        Assert.Equal("Example Person", result.Principal.FindFirstValue(ClaimTypes.Name));
        Assert.Equal("person@example.com", result.Principal.FindFirstValue(ClaimTypes.Email));
        Assert.Equal("basic", result.Principal.FindFirstValue("auth_method"));
        userRepository.Verify(repository => repository.GetByUsernameAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        auditSink.Verify(sink => sink.RecordAsync(
                It.Is<AuthenticationAuditEvent>(auditEvent =>
                    auditEvent.ActionType == "Authentication.Succeeded" &&
                    auditEvent.UserId == user.Id &&
                    auditEvent.Success &&
                    auditEvent.Method == "Basic" &&
                    auditEvent.IpAddress == "192.0.2.10" &&
                    auditEvent.UserAgent == "TestAgent/1.0" &&
                    auditEvent.ErrorMessage == null),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task AuthenticateAsync_ValidUsername_UsesUsernameLookupWhenEmailLookupMisses()
    {
        var user = User.Create("person@example.com", "Example Person");
        user.SetPasswordHash("stored-hash");
        var userRepository = new Mock<IUserRepository>();
        userRepository.Setup(repository => repository.GetByEmailAsync("player-one", It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);
        userRepository.Setup(repository => repository.GetByUsernameAsync("player-one", It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        var passwordHasher = new Mock<IPasswordHasher>();
        passwordHasher.Setup(hasher => hasher.VerifyPassword("stored-hash", "password"))
            .Returns(true);
        var mfaRepository = new Mock<IUserMfaConfigurationRepository>();
        mfaRepository.Setup(repository => repository.IsMfaEnabledAsync(user.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        var context = CreateContext("https", "player-one:password");
        var handler = CreateHandler(userRepository.Object, passwordHasher.Object, mfaRepository.Object);
        await InitializeAsync(handler, context);

        var result = await handler.AuthenticateAsync();

        Assert.True(result.Succeeded);
        userRepository.Verify(repository => repository.GetByUsernameAsync("player-one", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AuthenticateAsync_WrongPasswordOverHttps_DoesNotIssueIdentity()
    {
        var user = User.Create("person@example.com", "Example Person");
        user.SetPasswordHash("stored-hash");
        var userRepository = new Mock<IUserRepository>();
        userRepository.Setup(repository => repository.GetByEmailAsync("person@example.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        var passwordHasher = new Mock<IPasswordHasher>();
        passwordHasher.Setup(hasher => hasher.VerifyPassword("stored-hash", "wrong-password"))
            .Returns(false);
        var mfaRepository = new Mock<IUserMfaConfigurationRepository>();
        var context = CreateContext("https", "person@example.com:wrong-password");
        var handler = CreateHandler(userRepository.Object, passwordHasher.Object, mfaRepository.Object);
        await InitializeAsync(handler, context);

        var result = await handler.AuthenticateAsync();

        Assert.False(result.Succeeded);
        Assert.NotNull(result.Failure);
        Assert.Null(result.Principal);
        Assert.Null(result.Ticket);
        passwordHasher.Verify(hasher => hasher.VerifyPassword("stored-hash", "wrong-password"), Times.Once);
        mfaRepository.Verify(repository => repository.IsMfaEnabledAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task AuthenticateAsync_UnknownUserOverHttps_DoesNotIssueIdentity()
    {
        var userRepository = new Mock<IUserRepository>();
        userRepository.Setup(repository => repository.GetByEmailAsync("missing@example.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);
        userRepository.Setup(repository => repository.GetByUsernameAsync("missing@example.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);
        var passwordHasher = new Mock<IPasswordHasher>();
        var mfaRepository = new Mock<IUserMfaConfigurationRepository>();
        var context = CreateContext("https", "missing@example.com:password");
        var handler = CreateHandler(userRepository.Object, passwordHasher.Object, mfaRepository.Object);
        await InitializeAsync(handler, context);

        var result = await handler.AuthenticateAsync();

        Assert.False(result.Succeeded);
        Assert.NotNull(result.Failure);
        Assert.Null(result.Principal);
        Assert.Null(result.Ticket);
        userRepository.Verify(repository => repository.GetByUsernameAsync("missing@example.com", It.IsAny<CancellationToken>()), Times.Once);
        passwordHasher.Verify(hasher => hasher.VerifyPassword(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        mfaRepository.Verify(repository => repository.IsMfaEnabledAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task AuthenticateAsync_BasicCredentialsOverHttp_FailsBeforeCredentialLookup()
    {
        var userRepository = new Mock<IUserRepository>();
        var context = CreateContext("http", "person@example.com:password");
        var handler = CreateHandler(userRepository.Object, Mock.Of<IPasswordHasher>(), Mock.Of<IUserMfaConfigurationRepository>());
        await InitializeAsync(handler, context);

        var result = await handler.AuthenticateAsync();

        Assert.NotNull(result.Failure);
        Assert.Contains("HTTPS", result.Failure.Message, StringComparison.Ordinal);
        userRepository.Verify(repository => repository.GetByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task AuthenticateAsync_UntrustedForwardedHttpsHeaderOverHttp_FailsBeforeCredentialLookup()
    {
        var userRepository = new Mock<IUserRepository>();
        var context = CreateContext("http", "person@example.com:password");
        context.Connection.RemoteIpAddress = System.Net.IPAddress.Parse("203.0.113.9");
        context.Request.Headers["X-Forwarded-Proto"] = "https";

        var forwardedOptions = new ForwardedHeadersOptions
        {
            ForwardedHeaders = ForwardedHeaders.XForwardedProto,
            ForwardLimit = 1
        };
        forwardedOptions.KnownProxies.Add(System.Net.IPAddress.Parse("192.0.2.50"));

        using var services = new ServiceCollection().AddLogging().BuildServiceProvider();
        var forwardedHeaders = new ForwardedHeadersMiddleware(
            _ => Task.CompletedTask,
            services.GetRequiredService<ILoggerFactory>(),
            Options.Create(forwardedOptions));
        await forwardedHeaders.Invoke(context);

        Assert.False(context.Request.IsHttps);

        var handler = CreateHandler(
            userRepository.Object,
            Mock.Of<IPasswordHasher>(),
            Mock.Of<IUserMfaConfigurationRepository>());
        await InitializeAsync(handler, context);

        var result = await handler.AuthenticateAsync();

        Assert.NotNull(result.Failure);
        Assert.Contains("HTTPS", result.Failure.Message, StringComparison.Ordinal);
        userRepository.Verify(repository => repository.GetByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task AuthenticateAsync_BearerHeader_ReturnsNoResult()
    {
        var context = new DefaultHttpContext();
        context.Request.Scheme = "https";
        context.Request.Headers.Authorization = "Bearer token";
        var userRepository = new Mock<IUserRepository>();
        var handler = CreateHandler(userRepository.Object, Mock.Of<IPasswordHasher>(), Mock.Of<IUserMfaConfigurationRepository>());
        await InitializeAsync(handler, context);

        var result = await handler.AuthenticateAsync();

        Assert.False(result.Succeeded);
        Assert.Null(result.Failure);
        userRepository.Verify(repository => repository.GetByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task AuthenticateAsync_MalformedBase64Credentials_FailsWithoutLookingUpUser()
    {
        var context = new DefaultHttpContext();
        context.Request.Scheme = "https";
        context.Request.Headers.Authorization = "Basic !!!";
        var userRepository = new Mock<IUserRepository>();
        var handler = CreateHandler(userRepository.Object, Mock.Of<IPasswordHasher>(), Mock.Of<IUserMfaConfigurationRepository>());
        await InitializeAsync(handler, context);

        var result = await handler.AuthenticateAsync();

        Assert.NotNull(result.Failure);
        userRepository.Verify(repository => repository.GetByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task AuthenticateAsync_AccountWithMfaEnabled_IsNotAuthenticatedByBasic()
    {
        var user = User.Create("person@example.com", "Example Person");
        user.SetPasswordHash("stored-hash");
        var userRepository = new Mock<IUserRepository>();
        userRepository.Setup(repository => repository.GetByEmailAsync("person@example.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        var passwordHasher = new Mock<IPasswordHasher>();
        passwordHasher.Setup(hasher => hasher.VerifyPassword("stored-hash", "password"))
            .Returns(true);
        var mfaRepository = new Mock<IUserMfaConfigurationRepository>();
        mfaRepository.Setup(repository => repository.IsMfaEnabledAsync(user.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        var context = CreateContext("https", "person@example.com:password");
        var auditSink = new Mock<IAuthenticationAuditEventSink>();
        var handler = CreateHandler(userRepository.Object, passwordHasher.Object, mfaRepository.Object, auditEventSink: auditSink.Object);
        await InitializeAsync(handler, context);

        var result = await handler.AuthenticateAsync();

        Assert.NotNull(result.Failure);
        Assert.Contains("Invalid username or password", result.Failure.Message, StringComparison.Ordinal);
        auditSink.Verify(sink => sink.RecordAsync(
                It.Is<AuthenticationAuditEvent>(auditEvent =>
                    auditEvent.ActionType == "Authentication.Failed" &&
                    auditEvent.UserId == user.Id &&
                    !auditEvent.Success &&
                    auditEvent.Method == "Basic" &&
                    auditEvent.ErrorMessage == "MfaRequired"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task AuthenticateAsync_InactiveUser_IsNotAuthenticated()
    {
        var user = User.Create("person@example.com", "Example Person");
        user.IsActive = false;
        user.SetPasswordHash("stored-hash");
        var userRepository = new Mock<IUserRepository>();
        userRepository.Setup(repository => repository.GetByEmailAsync("person@example.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        var passwordHasher = new Mock<IPasswordHasher>();
        var context = CreateContext("https", "person@example.com:password");
        var handler = CreateHandler(userRepository.Object, passwordHasher.Object, Mock.Of<IUserMfaConfigurationRepository>());
        await InitializeAsync(handler, context);

        var result = await handler.AuthenticateAsync();

        Assert.NotNull(result.Failure);
        passwordHasher.Verify(hasher => hasher.VerifyPassword(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task ChallengeAsync_Returns401WithConfiguredRealmAndUtf8Challenge()
    {
        var context = new DefaultHttpContext();
        context.Request.Scheme = "https";
        var handler = CreateHandler(
            Mock.Of<IUserRepository>(),
            Mock.Of<IPasswordHasher>(),
            Mock.Of<IUserMfaConfigurationRepository>(),
            realm: "Legacy API");
        await InitializeAsync(handler, context);

        await handler.ChallengeAsync(new AuthenticationProperties());

        Assert.Equal(StatusCodes.Status401Unauthorized, context.Response.StatusCode);
        Assert.Equal("Basic realm=\"Legacy API\", charset=\"UTF-8\"", context.Response.Headers.WWWAuthenticate.ToString());
    }

    [Fact]
    public async Task ChallengeAsync_OverHttp_DoesNotAdvertiseBasicCredentials()
    {
        var context = new DefaultHttpContext();
        context.Request.Scheme = "http";
        var handler = CreateHandler(
            Mock.Of<IUserRepository>(),
            Mock.Of<IPasswordHasher>(),
            Mock.Of<IUserMfaConfigurationRepository>());
        await InitializeAsync(handler, context);

        await handler.ChallengeAsync(new AuthenticationProperties());

        Assert.Equal(StatusCodes.Status401Unauthorized, context.Response.StatusCode);
        Assert.False(context.Response.Headers.ContainsKey("WWW-Authenticate"));
    }

    private static DefaultHttpContext CreateContext(string scheme, string credentials)
    {
        var context = new DefaultHttpContext();
        context.Request.Scheme = scheme;
        context.Request.Headers.Authorization = $"Basic {Convert.ToBase64String(Encoding.UTF8.GetBytes(credentials))}";

        return context;
    }

    private static BasicAuthenticationHandler CreateHandler(
        IUserRepository userRepository,
        IPasswordHasher passwordHasher,
        IUserMfaConfigurationRepository mfaRepository,
        string realm = "GameGuild API",
        IAuthenticationAuditEventSink? auditEventSink = null)
    {
        var options = new Mock<IOptionsMonitor<BasicAuthenticationSchemeOptions>>();
        options.Setup(monitor => monitor.Get(It.IsAny<string>()))
            .Returns(new BasicAuthenticationSchemeOptions { Realm = realm });

        return new BasicAuthenticationHandler(
            options.Object,
            NullLoggerFactory.Instance,
            UrlEncoder.Default,
            userRepository,
            passwordHasher,
            mfaRepository,
            auditEventSink);
    }

    private static Task InitializeAsync(BasicAuthenticationHandler handler, HttpContext context) =>
        handler.InitializeAsync(
            new AuthenticationScheme(
                BasicAuthenticationSettings.DefaultSchemeName,
                BasicAuthenticationSettings.DefaultSchemeName,
                typeof(BasicAuthenticationHandler)),
            context);
}
