using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using FluentAssertions;
using GameGuild.Configuration.ApplicationLayer;
using GameGuild.Configuration.PresentationLayer.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Moq;
using Xunit;

namespace GameGuild.Identity.Authentication.UnitTests.Services;

public sealed class JwtGenerationValidationPolicyTests
{
    private static readonly string Secret = new('k', 64);

    [Theory]
    [InlineData("HS256", true)]
    [InlineData("HS384", false)]
    [InlineData("HS512", false)]
    [InlineData("none", false)]
    public async Task EveryValidator_EnforcesTheRecordedHs256Policy(string algorithm, bool accepted)
    {
        var service = CreateService();
        var token = Sign(algorithm);

        (await service.ValidateTokenAsync(token)).Should().Be(accepted);
        if (accepted)
        {
            service.ValidateToken(token).Identity!.IsAuthenticated.Should().BeTrue();
            service.GetPrincipalFromExpiredToken(token).Identity!.IsAuthenticated.Should().BeTrue();
        }
        else
        {
            Assert.ThrowsAny<SecurityTokenException>(() => service.ValidateToken(token));
            Assert.ThrowsAny<SecurityTokenException>(() => service.GetPrincipalFromExpiredToken(token));
        }
    }

    [Theory]
    [InlineData("wrong-key")]
    [InlineData("wrong-issuer")]
    [InlineData("wrong-audience")]
    [InlineData("expired")]
    [InlineData("future")]
    [InlineData("tampered")]
    [InlineData("malformed")]
    public async Task NormalValidators_RejectInvalidTokens(string scenario)
    {
        var service = CreateService();
        var token = InvalidToken(scenario);

        (await service.ValidateTokenAsync(token)).Should().BeFalse();
        Assert.ThrowsAny<Exception>(() => service.ValidateToken(token));
    }

    [Fact]
    public async Task GeneratedAccessToken_PreservesConfiguredIdentityClaimsAndExpiry()
    {
        var options = Settings();
        var service = CreateService(options);
        var user = Guid.NewGuid();
        var tenant = Guid.NewGuid();
        var session = Guid.NewGuid();
        var authenticatedAt = DateTimeOffset.UtcNow.AddHours(-2);
        var before = SystemClock.UtcNow;

        var encoded = await service.GenerateAccessTokenAsync(user, "identity@example.invalid",
            ["Reader", "Editor"], tenant, 7, authenticatedAt, session);
        var after = SystemClock.UtcNow;

        var parsed = new JwtSecurityTokenHandler().ReadJwtToken(encoded);
        parsed.Header.Alg.Should().Be("HS256");
        parsed.Issuer.Should().Be(options.Issuer);
        parsed.Audiences.Should().Equal(options.Audience);
        parsed.Subject.Should().Be(user.ToString());
        parsed.Claims.Where(claim => claim.Type == "role").Select(claim => claim.Value)
            .Should().BeEquivalentTo("Reader", "Editor");
        parsed.Claims.Should().Contain(claim => claim.Type == "tenant_id" && claim.Value == tenant.ToString());
        parsed.Claims.Should().Contain(claim => claim.Type == "session_id" && claim.Value == session.ToString());
        parsed.Claims.Should().Contain(claim => claim.Type == "token_version" && claim.Value == "7");
        parsed.Claims.Should().Contain(claim => claim.Type == "auth_time" && claim.Value == authenticatedAt.ToUnixTimeSeconds().ToString());
        parsed.ValidTo.Should().BeOnOrAfter(before.AddMinutes(17).AddSeconds(-1))
            .And.BeOnOrBefore(after.AddMinutes(17).AddSeconds(1));
        (await service.ValidateTokenAsync(encoded)).Should().BeTrue();
        service.ValidateToken(encoded).FindFirst(ClaimTypes.NameIdentifier)!.Value.Should().Be(user.ToString());
    }

    [Fact]
    public void AdditionalClaims_PreserveValuesTypesAndRepeatedCustomClaims()
    {
        var service = CreateService();
        var user = Guid.NewGuid();
        var encoded = service.GenerateAccessToken(user, "identity@example.invalid", ["Reader"],
        [
            new Claim("profile_theme", "dark"),
            new Claim("profile_verified", "true", ClaimValueTypes.Boolean),
            new Claim("profile_revision", "2147483648", ClaimValueTypes.Integer64),
            new Claim("profile_tag", "first"),
            new Claim("profile_tag", "second")
        ]);

        var principal = service.ValidateToken(encoded);

        principal.FindFirst("profile_theme")!.Value.Should().Be("dark");
        principal.FindFirst("profile_verified")!.ValueType.Should().Be(ClaimValueTypes.Boolean);
        principal.FindFirst("profile_revision")!.ValueType.Should().Be(ClaimValueTypes.Integer64);
        principal.FindFirst("profile_revision")!.Value.Should().Be("2147483648");
        principal.FindAll("profile_tag").Select(claim => claim.Value).Should().Equal("first", "second");
        principal.FindFirst(ClaimTypes.NameIdentifier)!.Value.Should().Be(user.ToString());
        principal.IsInRole("Reader").Should().BeTrue();
    }

    [Theory]
    [InlineData("sub")]
    [InlineData("SUB")]
    [InlineData("iss")]
    [InlineData("aud")]
    [InlineData("exp")]
    [InlineData("nbf")]
    [InlineData("iat")]
    [InlineData("jti")]
    [InlineData("email")]
    [InlineData("role")]
    [InlineData("ROLE")]
    [InlineData("roles")]
    [InlineData("token_version")]
    [InlineData("auth_time")]
    [InlineData("tenant_id")]
    [InlineData("tid")]
    [InlineData("session_id")]
    [InlineData("sid")]
    [InlineData("perm")]
    [InlineData("role_id")]
    [InlineData("group_id")]
    [InlineData("udt")]
    [InlineData("tenant_permission_flags1")]
    [InlineData("tenant_permission_flags2")]
    [InlineData("mfa_enabled")]
    [InlineData("amr")]
    [InlineData("acr")]
    [InlineData("actor_kind")]
    [InlineData("actor_type")]
    [InlineData("ACTOR_TYPE")]
    [InlineData("UserId")]
    [InlineData("TenantId")]
    [InlineData("TENANTID")]
    [InlineData("group")]
    [InlineData("mfa_verified")]
    [InlineData("mfa_time")]
    [InlineData("mfa_timestamp")]
    [InlineData("email_verified")]
    [InlineData("client_id")]
    [InlineData("grant_type")]
    [InlineData("scope")]
    [InlineData(ClaimTypes.NameIdentifier)]
    [InlineData(ClaimTypes.Role)]
    [InlineData(ClaimTypes.Email)]
    public void AdditionalClaims_CannotReplaceIdentityAuthorizationOrProtocolClaims(string type)
    {
        var service = CreateService();

        var exception = Assert.Throws<ArgumentException>(() => service.GenerateAccessToken(
            Guid.NewGuid(), "identity@example.invalid", ["Reader"], [new Claim(type, "untrusted") ]));

        exception.ParamName.Should().Be("additionalClaims");
    }

    [Theory]
    [InlineData("custom_tenant")]
    [InlineData("custom_default_tenant")]
    [InlineData("custom_permission")]
    [InlineData("custom_role_id")]
    [InlineData("custom_group_id")]
    [InlineData("CUSTOM_GROUP_ID")]
    public void AdditionalClaims_RespectConfiguredAuthorizationClaimNames(string type)
    {
        var tokenOptions = new AuthorizationTokenOptions
        {
            TenantClaimType = "custom_tenant", UserDefaultTenantClaimType = "custom_default_tenant",
            PermissionClaimType = "custom_permission", RoleIdClaimType = "custom_role_id",
            GroupIdClaimType = "custom_group_id"
        };
        var service = CreateService(tokenOptions: tokenOptions);

        Assert.Throws<ArgumentException>(() => service.GenerateAccessToken(Guid.NewGuid(),
            "identity@example.invalid", ["Reader"], [new Claim(type, "untrusted")]));
    }

    [Fact]
    public void AdditionalClaims_RejectNullCollectionNullEntriesAndBlankTypes()
    {
        var service = CreateService();
        var user = Guid.NewGuid();

        Assert.Throws<ArgumentNullException>(() => service.GenerateAccessToken(user, "user@example.invalid", [], null!));
        Assert.Throws<ArgumentException>(() => service.GenerateAccessToken(user, "user@example.invalid", [], [null!]));
        Assert.Throws<ArgumentException>(() => service.GenerateAccessToken(user, "user@example.invalid", [], [new Claim(" ", "value")]));
        service.ValidateToken(service.GenerateAccessToken(user, "user@example.invalid", [], []))
            .FindFirst(ClaimTypes.NameIdentifier)!.Value.Should().Be(user.ToString());
    }

    [Fact]
    public async Task ExpiredPrincipalBypass_SkipsOnlyLifetimeValidation()
    {
        var service = CreateService();
        var expired = InvalidToken("expired");

        (await service.ValidateTokenAsync(expired)).Should().BeFalse();
        Assert.ThrowsAny<SecurityTokenException>(() => service.ValidateToken(expired));
        service.GetPrincipalFromExpiredToken(expired).FindFirst(ClaimTypes.NameIdentifier).Should().NotBeNull();
        Assert.ThrowsAny<SecurityTokenException>(() => service.GetPrincipalFromExpiredToken(InvalidToken("wrong-key")));
        Assert.ThrowsAny<SecurityTokenException>(() => service.GetPrincipalFromExpiredToken(Sign("HS384")));
    }

    private static JwtOptions Settings() => new()
    {
        SecretKey = Secret, Issuer = "policy-issuer", Audience = "policy-audience",
        AccessTokenExpirationMinutes = 17, ClockSkewSeconds = 0,
        ValidateIssuer = true, ValidateAudience = true, ValidateLifetime = true, ValidateIssuerSigningKey = true
    };

    private static JwtTokenService CreateService(JwtOptions? settings = null, AuthorizationTokenOptions? tokenOptions = null) =>
        new(NullLogger<JwtTokenService>.Instance, Mock.Of<IRefreshTokenRepository>(), Mock.Of<IRefreshTokenHasher>(),
            new HttpContextAccessor(), Options.Create(settings ?? Settings()),
            authorizationTokenOptions: Options.Create(tokenOptions ?? new AuthorizationTokenOptions()));

    private static string Sign(string algorithm, string? key = null, string issuer = "policy-issuer",
        string audience = "policy-audience", DateTime? notBefore = null, DateTime? expires = null)
    {
        var token = new JwtSecurityToken(issuer, audience, [new Claim("sub", Guid.NewGuid().ToString())],
            notBefore ?? DateTime.UtcNow.AddMinutes(-1), expires ?? DateTime.UtcNow.AddMinutes(5),
            algorithm == "none" ? null : new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key ?? Secret)), algorithm));
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static string InvalidToken(string scenario) => scenario switch
    {
        "wrong-key" => Sign("HS256", key: new string('z', 64)),
        "wrong-issuer" => Sign("HS256", issuer: "wrong"),
        "wrong-audience" => Sign("HS256", audience: "wrong"),
        "expired" => Sign("HS256", notBefore: DateTime.UtcNow.AddMinutes(-10), expires: DateTime.UtcNow.AddMinutes(-1)),
        "future" => Sign("HS256", notBefore: DateTime.UtcNow.AddMinutes(1), expires: DateTime.UtcNow.AddMinutes(10)),
        "tampered" => Tamper(Sign("HS256")),
        "malformed" => "not.a.valid.jwt",
        _ => throw new ArgumentOutOfRangeException(nameof(scenario))
    };

    private static string Tamper(string token)
    {
        var parts = token.Split('.');
        parts[1] = Base64UrlEncoder.Encode("{\"sub\":\"replaced\"}");
        return string.Join('.', parts);
    }
}
