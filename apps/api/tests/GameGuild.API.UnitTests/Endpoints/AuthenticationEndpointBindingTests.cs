using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using FluentAssertions;
using GameGuild.API.Database;
using GameGuild.API.Endpoints;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Moq;
using IAuthService = GameGuild.Identity.Authentication.IAuthService;
using LocalSignUpRequest = GameGuild.Identity.Authentication.LocalSignUpRequest;
using SignInResponse = GameGuild.Identity.Authentication.SignInResponse;
using User = GameGuild.Identity.Users.User;

namespace GameGuild.API.UnitTests.Endpoints;

public sealed class AuthenticationEndpointBindingTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task SignIn_UsesBCryptAndRecordsOnlySuccessfulLogins(bool correctPassword)
    {
        await using var app = await CreateAppAsync();
        var password = Guid.NewGuid().ToString("N");
        var passwordHash = BCrypt.Net.BCrypt.HashPassword(password, workFactor: 4);
        var user = User.CreateWithPassword("legacy@example.test", "Legacy User", passwordHash, "legacy-user");
        await using (var scope = app.Services.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            context.Set<User>().Add(user);
            await context.SaveChangesAsync();
        }

        using var client = app.GetTestClient();
        using var response = await client.PostAsJsonAsync("/auth/sign-in",
            new SignInRequest(user.Email, correctPassword ? password : password + "-incorrect"));

        response.StatusCode.Should().Be(correctPassword ? HttpStatusCode.OK : HttpStatusCode.Unauthorized);
        if (correctPassword)
        {
            var body = await response.Content.ReadFromJsonAsync<SignInResponseDto>();
            body!.User.Id.Should().Be(user.Id);
            body.User.Email.Should().Be(user.Email);
            body.AccessToken.Should().NotBeNullOrWhiteSpace();
            body.RefreshToken.Should().NotBeNullOrWhiteSpace();
        }
        else
        {
            (await response.Content.ReadAsStringAsync()).Should().BeEmpty();
        }

        await using var verificationScope = app.Services.CreateAsyncScope();
        var persisted = await verificationScope.ServiceProvider.GetRequiredService<ApplicationDbContext>()
            .Set<User>().SingleAsync(candidate => candidate.Id == user.Id);
        persisted.LastLoginAt.HasValue.Should().Be(correctPassword);
        persisted.PasswordHash.Should().Be(passwordHash);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task SignIn_RejectsPasswordlessAndUnknownAccounts(bool existingAccount)
    {
        await using var app = await CreateAppAsync();
        const string email = "passwordless@example.test";
        if (existingAccount)
        {
            await using var scope = app.Services.CreateAsyncScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            context.Set<User>().Add(new User { Email = email, Name = "Passwordless User", Username = "passwordless-user" });
            await context.SaveChangesAsync();
        }

        using var client = app.GetTestClient();
        using var response = await client.PostAsJsonAsync("/auth/sign-in", new SignInRequest(email, Guid.NewGuid().ToString("N")));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await response.Content.ReadAsStringAsync()).Should().BeEmpty();
    }

    [Fact]
    public async Task SignUp_BindsTheRequestAndPreservesTheServiceResponse()
    {
        var request = new SignUpRequest("created@example.test", Guid.NewGuid().ToString("N"), "created-user");
        var expected = new SignInResponse
        {
            UserId = Guid.NewGuid(), Email = request.Email, ExpiresAt = DateTime.UtcNow.AddMinutes(20),
            AccessToken = Guid.NewGuid().ToString("N"), RefreshToken = Guid.NewGuid().ToString("N")
        };
        var auth = new Mock<IAuthService>();
        auth.Setup(service => service.LocalSignUpAsync(It.IsAny<LocalSignUpRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);
        await using var app = await CreateAppAsync(auth.Object);
        using var client = app.GetTestClient();

        using var response = await client.PostAsJsonAsync("/auth/sign-up", request);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        response.Headers.Location!.OriginalString.Should().Be($"/users/{expected.UserId}");
        var body = await response.Content.ReadFromJsonAsync<SignInResponseDto>();
        body!.User.Id.Should().Be(expected.UserId);
        body.User.Username.Should().Be(request.Username);
        body.AccessToken.Should().Be(expected.AccessToken);
        body.RefreshToken.Should().Be(expected.RefreshToken);
        auth.Verify(service => service.LocalSignUpAsync(It.Is<LocalSignUpRequest>(input =>
            input.Email == request.Email && input.Password == request.Password && input.Username == request.Username),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData("invalid", "valid-length-password")]
    [InlineData("valid@example.test", "short")]
    public async Task SignUp_RejectsInvalidInputBeforeCallingTheService(string email, string password)
    {
        var auth = new Mock<IAuthService>(MockBehavior.Strict);
        await using var app = await CreateAppAsync(auth.Object);
        using var client = app.GetTestClient();

        using var response = await client.PostAsJsonAsync("/auth/sign-up", new SignUpRequest(email, password, "invalid-user"));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        auth.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData("")]
    [InlineData("\r\nforged-entry")]
    [InlineData("\u0085\u2028\u2029")]
    public async Task SignUp_SuccessLogsDoNotExposeRequestOrResponseIdentifiers(string suffix)
    {
        var request = new SignUpRequest($"request-person{suffix}@private-request.example.test{suffix}",
            Guid.NewGuid().ToString("N"), "created-user");
        var expected = new SignInResponse
        {
            UserId = Guid.NewGuid(), Email = $"response-person{suffix}@private-response.example.test{suffix}",
            ExpiresAt = DateTime.UtcNow.AddMinutes(20), AccessToken = Guid.NewGuid().ToString("N"),
            RefreshToken = Guid.NewGuid().ToString("N")
        };
        var auth = new Mock<IAuthService>();
        auth.Setup(service => service.LocalSignUpAsync(It.IsAny<LocalSignUpRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);
        var logger = new Mock<ILogger<Program>>();
        await using var app = await CreateAppAsync(auth.Object, logger.Object);
        using var client = app.GetTestClient();

        using var response = await client.PostAsJsonAsync("/auth/sign-up", request);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<SignInResponseDto>();
        body!.User.Email.Should().Be(expected.Email);
        body.User.Id.Should().Be(expected.UserId);
        body.AccessToken.Should().Be(expected.AccessToken);
        body.RefreshToken.Should().Be(expected.RefreshToken);
        auth.Verify(service => service.LocalSignUpAsync(It.Is<LocalSignUpRequest>(input =>
            input.Email == request.Email && input.Password == request.Password && input.Username == request.Username),
            It.IsAny<CancellationToken>()), Times.Once);
        AssertRedactedSuccessLog(logger, "User signed up successfully", expected.UserId,
            request.Email, expected.Email, "request-person", "response-person", "private-request.example.test",
            "private-response.example.test", request.Password, expected.AccessToken, expected.RefreshToken);
    }

    [Theory]
    [InlineData("")]
    [InlineData("\r\nforged-entry")]
    [InlineData("\u0085\u2028\u2029")]
    public async Task SignIn_SuccessLogsDoNotExposeTheEmailOrCredentials(string suffix)
    {
        var logger = new Mock<ILogger<Program>>();
        await using var app = await CreateAppAsync(logger: logger.Object);
        var password = Guid.NewGuid().ToString("N");
        var passwordHash = BCrypt.Net.BCrypt.HashPassword(password, workFactor: 4);
        var user = User.CreateWithPassword($"private-person{suffix}@private-login.example.test{suffix}",
            "Private User", passwordHash, "private-user");
        await using (var scope = app.Services.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            context.Set<User>().Add(user);
            await context.SaveChangesAsync();
        }

        using var client = app.GetTestClient();
        using var response = await client.PostAsJsonAsync("/auth/sign-in", new SignInRequest(user.Email, password));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<SignInResponseDto>();
        body!.User.Email.Should().Be(user.Email);
        body.User.Id.Should().Be(user.Id);
        body.AccessToken.Should().NotBeNullOrWhiteSpace();
        body.RefreshToken.Should().NotBeNullOrWhiteSpace();
        await using var verificationScope = app.Services.CreateAsyncScope();
        var persisted = await verificationScope.ServiceProvider.GetRequiredService<ApplicationDbContext>()
            .Set<User>().SingleAsync(candidate => candidate.Id == user.Id);
        persisted.LastLoginAt.Should().NotBeNull();
        persisted.PasswordHash.Should().Be(passwordHash);
        AssertRedactedSuccessLog(logger, "User signed in successfully", user.Id, user.Email, "private-person",
            "private-login.example.test", password, passwordHash, body.AccessToken, body.RefreshToken);
    }

    private static void AssertRedactedSuccessLog(Mock<ILogger<Program>> logger, string prefix, Guid userId, params string[] secrets)
    {
        var invocation = logger.Invocations.Single(call => call.Method.Name == nameof(ILogger.Log)
            && (LogLevel) call.Arguments[0] == LogLevel.Information
            && call.Arguments[2].ToString()!.StartsWith(prefix, StringComparison.Ordinal));
        var state = ((IEnumerable<KeyValuePair<string, object?>>) invocation.Arguments[2]).ToArray();
        var rendered = invocation.Arguments[2].ToString()!;
        foreach (var secret in secrets.Append(userId.ToString()))
        {
            rendered.Should().NotContain(secret);
            foreach (var field in state)
            {
                field.Value?.ToString().Should().NotContain(secret);
            }
        }

        rendered.Any(character => char.IsControl(character) || character is '\u2028' or '\u2029').Should().BeFalse();
        state.Should().NotContain(field => field.Key == "Email" || field.Key == "ResponseEmail");
        state.Single(field => field.Key == "UserId").Value.Should().Be(LogRedaction.RedactId(userId, "uid"));
    }

    private static async Task<WebApplication> CreateAppAsync(IAuthService? auth = null, ILogger<Program>? logger = null)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = Environments.Development });
        builder.WebHost.UseTestServer();
        builder.Logging.ClearProviders();
        if (logger is not null)
        {
            builder.Services.AddSingleton(logger);
        }
        builder.Configuration["Jwt:SecretKey"] = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        var databaseName = Guid.NewGuid().ToString("N");
        builder.Services.AddDbContext<ApplicationDbContext>(options => options.UseInMemoryDatabase(databaseName));
        builder.Services.AddSingleton(auth ?? Mock.Of<IAuthService>());
        var app = builder.Build();
        app.MapAuthEndpoints();
        await app.StartAsync();
        return app;
    }
}
