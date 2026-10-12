using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using GameGuild.Configuration.ApplicationLayer;
using GameGuild.Identity.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Moq;
using PresentationAuthenticationOptions = GameGuild.Configuration.PresentationLayer.Authentication.AuthenticationOptions;

namespace GameGuild.API.UnitTests.Core;

public sealed class JwtBearerValidationHttpTests
{
    private static readonly string Secret = new('h', 64);

    [Theory]
    [InlineData("HS256", HttpStatusCode.OK)]
    [InlineData("HS384", HttpStatusCode.Unauthorized)]
    [InlineData("HS512", HttpStatusCode.Unauthorized)]
    [InlineData("none", HttpStatusCode.Unauthorized)]
    [InlineData("wrong-key", HttpStatusCode.Unauthorized)]
    [InlineData("wrong-issuer", HttpStatusCode.Unauthorized)]
    [InlineData("wrong-audience", HttpStatusCode.Unauthorized)]
    [InlineData("expired", HttpStatusCode.Unauthorized)]
    [InlineData("future", HttpStatusCode.Unauthorized)]
    [InlineData("tampered", HttpStatusCode.Unauthorized)]
    [InlineData("malformed", HttpStatusCode.Unauthorized)]
    [InlineData("missing", HttpStatusCode.Unauthorized)]
    public async Task ActiveBearerRegistration_EnforcesCryptographicPolicyOverHttp(string scenario, HttpStatusCode expected)
    {
        await using var app = await CreateApp();
        using var client = app.GetTestClient();
        if (scenario != "missing")
        {
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Token(scenario));
        }

        using var response = await client.GetAsync("/protected");

        Assert.Equal(expected, response.StatusCode);
        if (expected == HttpStatusCode.Unauthorized)
        {
            Assert.Contains(response.Headers.WwwAuthenticate, header => header.Scheme == "Bearer");
        }
    }

    [Fact]
    public async Task GeneratedToken_CustomClaimsReachBearerPrincipalWithCanonicalIdentity()
    {
        await using var app = await CreateApp();
        using var client = app.GetTestClient();
        var user = Guid.NewGuid();
        var before = DateTime.UtcNow;
        var service = new JwtTokenService(NullLogger<JwtTokenService>.Instance,
            Mock.Of<IRefreshTokenRepository>(), Mock.Of<IRefreshTokenHasher>(),
            new Microsoft.AspNetCore.Http.HttpContextAccessor(), app.Services.GetRequiredService<IOptions<JwtOptions>>());
        var encoded = service.GenerateAccessToken(user, "user@example.invalid", ["Reader"], [new Claim("profile_theme", "dark")]);
        var after = DateTime.UtcNow;
        var issued = new JwtSecurityTokenHandler().ReadJwtToken(encoded);
        Assert.Equal("HS256", issued.Header.Alg);
        Assert.Equal("http-issuer", issued.Issuer);
        Assert.Equal(["http-audience"], issued.Audiences);
        Assert.InRange(issued.ValidTo, before.AddMinutes(17).AddSeconds(-1), after.AddMinutes(17).AddSeconds(1));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", encoded);

        using var response = await client.GetAsync("/protected");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal($"{user}:Reader:dark", await response.Content.ReadAsStringAsync());
    }

    private static async Task<WebApplication> CreateApp()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Jwt:SecretKey"] = Secret,
            ["Jwt:Issuer"] = "http-issuer",
            ["Jwt:Audience"] = "http-audience",
            ["Jwt:AccessTokenExpirationMinutes"] = "999",
            ["Jwt:ClockSkewSeconds"] = "0",
            ["Jwt:ValidateIssuer"] = "true",
            ["Jwt:ValidateAudience"] = "true",
            ["Jwt:ValidateLifetime"] = "true"
        });
        builder.Services.SetupAuthentication(builder.Configuration, new PresentationAuthenticationOptions
        {
            JwtSecretKey = Secret, JwtIssuer = "http-issuer", JwtAudience = "http-audience",
            JwtExpiration = TimeSpan.FromMinutes(17)
        });
        builder.Services.AddAuthorization();
        var app = builder.Build();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapGet("/protected", (Microsoft.AspNetCore.Http.HttpContext context) =>
            $"{context.User.FindFirst("sub")?.Value}:{context.User.FindFirst("role")?.Value}:{context.User.FindFirst("profile_theme")?.Value}")
            .RequireAuthorization();
        await app.StartAsync();
        return app;
    }

    private static string Token(string scenario)
    {
        if (scenario == "malformed")
        {
            return "not.a.valid.jwt";
        }

        var algorithm = scenario is "HS384" or "HS512" ? scenario : "HS256";
        var token = new JwtSecurityToken(
            scenario == "wrong-issuer" ? "wrong" : "http-issuer",
            scenario == "wrong-audience" ? "wrong" : "http-audience",
            [new Claim("sub", Guid.NewGuid().ToString()), new Claim("jti", Guid.NewGuid().ToString())],
            scenario == "future" ? DateTime.UtcNow.AddMinutes(1) : DateTime.UtcNow.AddMinutes(-10),
            scenario == "expired" ? DateTime.UtcNow.AddMinutes(-1) : DateTime.UtcNow.AddMinutes(5),
            scenario == "none" ? null : new SigningCredentials(new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(scenario == "wrong-key" ? new string('z', 64) : Secret)), algorithm));
        var encoded = new JwtSecurityTokenHandler().WriteToken(token);
        if (scenario != "tampered")
        {
            return encoded;
        }

        var parts = encoded.Split('.');
        parts[1] = Base64UrlEncoder.Encode("{\"sub\":\"replaced\"}");
        return string.Join('.', parts);
    }
}
