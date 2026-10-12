using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GameGuild.Commerce.Subscriptions.IntegrationTests;

public sealed class TestAuthHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "Test";
    public static readonly Guid DefaultUserId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private const string DefaultSubject = "integration-test-user";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.ContainsKey("Authorization"))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var subject = Request.Headers.TryGetValue("X-Test-Subject", out var subjectValues)
            ? subjectValues.ToString()
            : DefaultSubject;

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, subject),
            new Claim(ClaimTypes.Name, "Integration Test User"),
            new Claim("tenant_id", Request.Headers["X-Tenant-Id"].FirstOrDefault() ?? Guid.NewGuid().ToString())
        };
        var identity = new ClaimsIdentity(claims, SchemeName);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, SchemeName);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
