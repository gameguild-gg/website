using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using GameGuild.API.Database;
using GameGuild.API.SecurityTests.Infrastructure;
using GameGuild.Identity.Authentication;
using GameGuild.Identity.Users;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.Extensions.DependencyInjection;

namespace GameGuild.API.SecurityTests;

/// <summary>
///     Adversarial scenario family (c): token forgery and tampering (issue #327).
///     An attacker who can craft arbitrary bearer tokens — wrong key, disallowed
///     algorithm, unsigned, backdated, not-yet-valid, claim-tampered, spoofed
///     issuer/audience — must never get past authentication on the real host.
/// </summary>
[Collection(AdversarialSecurityCollection.Name)]
public sealed class TokenForgeryAdversarialTests(AdversarialSecurityFixture fixture)
{
    private const string ProbeEndpoint = "/v1/auth/sessions";

    private AdversarialTokenForge Forge => new(fixture.Factory);

    [Theory]
    [InlineData("wrong-key")]
    [InlineData("corrupted-signature")]
    [InlineData("tampered-payload-keeps-signature")]
    [InlineData("unsigned-alg-none")]
    [InlineData("wrong-algorithm-hs512")]
    [InlineData("expired")]
    [InlineData("not-yet-valid")]
    [InlineData("wrong-issuer")]
    [InlineData("wrong-audience")]
    public async Task ForgedBearerVariantsAreRejectedWith401(string scenario)
    {
        var account = await fixture.SeedAccountAsync();
        var forge = Forge;
        string presented;

        switch (scenario)
        {
            case "wrong-key":
                presented = forge.Forge(forge.Material.AttackerSigningKey, SecurityAlgorithms.HmacSha256, forge.StandardClaims(account));
                break;
            case "corrupted-signature":
                presented = AdversarialTokenForge.CorruptSignature(await fixture.MintAccessTokenAsync(account));
                break;
            case "tampered-payload-keeps-signature":
            {
                // Escalation attempt: swap the payload of a legitimately signed token to claim
                // the SystemAdmin role, keeping the original (valid) signature over the old payload.
                var elevated = new List<System.Security.Claims.Claim>(forge.StandardClaims(account))
                {
                    new("role", "SystemAdmin"),
                    new("token_version", "9999"),
                };
                presented = AdversarialTokenForge.SwapPayloadKeepSignature(
                    await fixture.MintAccessTokenAsync(account), elevated, forge.Material.Issuer, forge.Material.Audience);
                break;
            }
            case "unsigned-alg-none":
                presented = forge.Forge(forge.Material.ProductionSigningKey, "none", forge.StandardClaims(account), omitSignature: true);
                break;
            case "wrong-algorithm-hs512":
                presented = forge.Forge(forge.Material.ProductionSigningKeyPaddedForHs512, SecurityAlgorithms.HmacSha512, forge.StandardClaims(account));
                break;
            case "expired":
                presented = forge.Forge(
                    forge.Material.ProductionSigningKey,
                    SecurityAlgorithms.HmacSha256,
                    forge.StandardClaims(account),
                    notBefore: DateTime.UtcNow.AddHours(-2),
                    expires: DateTime.UtcNow.AddMinutes(-10));
                break;
            case "not-yet-valid":
                presented = forge.Forge(
                    forge.Material.ProductionSigningKey,
                    SecurityAlgorithms.HmacSha256,
                    forge.StandardClaims(account),
                    notBefore: DateTime.UtcNow.AddMinutes(30),
                    expires: DateTime.UtcNow.AddHours(2));
                break;
            case "wrong-issuer":
                presented = forge.Forge(
                    forge.Material.ProductionSigningKey,
                    SecurityAlgorithms.HmacSha256,
                    forge.StandardClaims(account),
                    issuer: "https://attacker.example.test");
                break;
            case "wrong-audience":
                presented = forge.Forge(
                    forge.Material.ProductionSigningKey,
                    SecurityAlgorithms.HmacSha256,
                    forge.StandardClaims(account),
                    audience: "attacker-audience");
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(scenario));
        }

        using var client = fixture.Factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(JwtBearerDefaults.AuthenticationScheme, presented);
        using var response = await client.GetAsync(ProbeEndpoint);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        await AssertRejectionHygieneAsync(response, presented);
    }

    [Theory]
    [InlineData("not-a-jwt")]
    [InlineData("a.b")]
    [InlineData(". .")]
    [InlineData("eyJhbGciOiJIUzI1NiJ9..c2lnbmF0dXJl")]
    [InlineData("===garbage===")]
    public async Task MalformedBearerValuesAreRejectedWith401(string value)
    {
        using var client = fixture.Factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(JwtBearerDefaults.AuthenticationScheme, value);
        using var response = await client.GetAsync(ProbeEndpoint);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        await AssertRejectionHygieneAsync(response, value);
    }

    [Fact]
    public async Task AnonymousRequestsToProtectedEndpointsAreRejectedWith401()
    {
        using var client = fixture.Factory.CreateClient();
        using var response = await client.GetAsync(ProbeEndpoint);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Contains(response.Headers.WwwAuthenticate, header => header.Scheme == JwtBearerDefaults.AuthenticationScheme);
    }

    [Fact]
    public async Task LegitimatelyMintedTokenIsAcceptedOnTheSameEndpoint()
    {
        // Positive control proving the forgery rejections above come from token
        // validation, not from a broken endpoint.
        var account = await fixture.SeedAccountAsync();
        using var client = await fixture.CreateBearerClientAsync(account);
        using var response = await client.GetAsync(ProbeEndpoint);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task RevokedJtiIsRejectedImmediately()
    {
        var account = await fixture.SeedAccountAsync();
        var token = await fixture.MintAccessTokenAsync(account);
        using var client = fixture.Factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(JwtBearerDefaults.AuthenticationScheme, token);

        using var before = await client.GetAsync(ProbeEndpoint);
        Assert.Equal(HttpStatusCode.OK, before.StatusCode);

        using (var scope = fixture.Factory.Services.CreateScope())
        {
            var store = scope.ServiceProvider.GetRequiredService<ITokenRevocationService>();
            var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);
            await store.RevokeTokenAsync(jwt.Id, jwt.ValidTo, "Adversarial validation: jti revocation");
        }

        using var after = await client.GetAsync(ProbeEndpoint);
        Assert.Equal(HttpStatusCode.Unauthorized, after.StatusCode);
    }

    [Fact]
    public async Task StaleTokenVersionIsRejectedImmediately()
    {
        var account = await fixture.SeedAccountAsync();
        var token = await fixture.MintAccessTokenAsync(account);
        using var client = fixture.Factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(JwtBearerDefaults.AuthenticationScheme, token);

        using var before = await client.GetAsync(ProbeEndpoint);
        Assert.Equal(HttpStatusCode.OK, before.StatusCode);

        using (var scope = fixture.Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            (await db.Set<User>().SingleAsync(user => user.Id == account.User.Id)).IncrementTokenVersion();
            await db.SaveChangesAsync();
        }

        using var after = await client.GetAsync(ProbeEndpoint);
        Assert.Equal(HttpStatusCode.Unauthorized, after.StatusCode);
    }

    private static async Task AssertRejectionHygieneAsync(HttpResponseMessage response, string secret)
    {
        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain(secret, body, StringComparison.Ordinal);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Contains(response.Headers.WwwAuthenticate, header => header.Scheme == JwtBearerDefaults.AuthenticationScheme);
        using var problem = JsonDocument.Parse(body);
        Assert.Equal(401, problem.RootElement.GetProperty("status").GetInt32());
    }
}
