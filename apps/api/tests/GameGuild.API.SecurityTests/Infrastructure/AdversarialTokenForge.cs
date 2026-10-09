using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using GameGuild.Identity.Authentication;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace GameGuild.API.SecurityTests.Infrastructure;

/// <summary>
///     Hand-crafted JWT forger for the adversarial token-manipulation scenarios (issue #327).
///     Produces attacker-style mutations of an otherwise valid access token: wrong signing
///     key, disallowed algorithms, backdated lifetimes, claim tampering and spoofed
///     issuer/audience. The signing material and validation expectations are read from the
///     host's own configuration so forged tokens exercise exactly the production validator.
/// </summary>
public sealed class AdversarialTokenForge(WebApplicationFactory<Program> factory)
{
    private TokenForgeMaterial? _material;

    public TokenForgeMaterial Material => _material ??= ResolveMaterial();

    private TokenForgeMaterial ResolveMaterial()
    {
        using var scope = factory.Services.CreateScope();
        var provider = scope.ServiceProvider;
        var configuration = provider.GetRequiredService<Microsoft.Extensions.Configuration.IConfiguration>();

        // The API resolves JWT settings from the "Authentication" section (typed options)
        // and mirrors them into "Jwt" for the bearer handler; read both and prefer the
        // values the handler validates against.
        var secret = configuration["Authentication:JwtSecretKey"]
            ?? configuration["Jwt:SecretKey"]
            ?? throw new InvalidOperationException("JWT secret is not configured for the test host.");
        var issuer = configuration["Authentication:JwtIssuer"] ?? configuration["Jwt:Issuer"] ?? "GameGuild";
        var audience = configuration["Authentication:JwtAudience"] ?? configuration["Jwt:Audience"] ?? "GameGuild.Users";

        return new TokenForgeMaterial(secret, issuer, audience);
    }

    public sealed record TokenForgeMaterial(string SecretKey, string Issuer, string Audience)
    {
        public SymmetricSecurityKey ProductionSigningKey => new(Encoding.UTF8.GetBytes(SecretKey));

        /// <summary>
        ///     The production signing material padded to at least 64 bytes (512 bits) so the
        ///     forge can also mint tokens with longer-key algorithms (HS512) — the .NET crypto
        ///     stack refuses to create an HS512 signature over a shorter key, which would fail
        ///     the forge, not the host under attack. The server still validates HS256 only, so
        ///     the scenario keeps testing the algorithm whitelist, not key length.
        /// </summary>
        public SymmetricSecurityKey ProductionSigningKeyPaddedForHs512
        {
            get
            {
                var bytes = Encoding.UTF8.GetBytes(SecretKey);
                if (bytes.Length >= 64)
                {
                    return new SymmetricSecurityKey(bytes);
                }

                var padded = new byte[64];
                bytes.CopyTo(padded, 0);
                for (var index = bytes.Length; index < padded.Length; index++)
                {
                    padded[index] = 0x2d; // '-'
                }

                return new SymmetricSecurityKey(padded);
            }
        }

        public SymmetricSecurityKey AttackerSigningKey => new(Encoding.UTF8.GetBytes(
            "attacker-controlled-secret-key-with-at-least-thirty-two-characters"));
    }

    /// <summary>Fully controlled token mint (attacker style): arbitrary key, algorithm and claims.</summary>
    public string Forge(
        SymmetricSecurityKey signingKey,
        string algorithm,
        IEnumerable<Claim> claims,
        string? issuer = null,
        string? audience = null,
        DateTime? notBefore = null,
        DateTime? expires = null,
        bool omitSignature = false)
    {
        var now = DateTime.UtcNow;
        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = issuer ?? Material.Issuer,
            Audience = audience ?? Material.Audience,
            NotBefore = notBefore ?? now.AddMinutes(-1),
            Expires = expires ?? now.AddMinutes(30),
            Subject = new ClaimsIdentity(claims),
            SigningCredentials = omitSignature
                ? null
                : new SigningCredentials(signingKey, algorithm),
        };
        var handler = new JwtSecurityTokenHandler();
        if (omitSignature)
        {
            // JwtSecurityTokenHandler refuses to serialize the "none" algorithm through
            // SecurityTokenDescriptor, so the unsigned variant is assembled by hand:
            // a hand-written {"alg":"none"} header, a real serialized payload, empty signature.
            var unsigned = handler.CreateJwtSecurityToken(
                issuer: descriptor.Issuer,
                audience: descriptor.Audience,
                subject: descriptor.Subject,
                notBefore: descriptor.NotBefore,
                expires: descriptor.Expires,
                issuedAt: DateTime.UtcNow,
                signingCredentials: null);
            var headerJson = """{"alg":"none","typ":"JWT"}""";
            return $"{Base64UrlEncode(Encoding.UTF8.GetBytes(headerJson))}.{unsigned.EncodedPayload}.";
        }

        return handler.CreateEncodedJwt(descriptor);
    }

    /// <summary>Rebuilds a claim set matching a production-issued token for <paramref name="account"/>.</summary>
    public IReadOnlyList<Claim> StandardClaims(AdversarialAccount account, string tokenVersion = "1") =>
    [
        new Claim(JwtRegisteredClaimNames.Sub, account.User.Id.ToString()),
        new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
        new Claim(JwtRegisteredClaimNames.Iat, EpochTime.GetIntDate(DateTime.UtcNow).ToString(), ClaimValueTypes.Integer64),
        new Claim("email", account.User.Email),
        new Claim("role", account.MembershipRole),
        new Claim("tid", account.TenantId.ToString()),
        new Claim("sid", account.Session.Id.ToString()),
        new Claim("token_version", tokenVersion),
    ];

    /// <summary>Tamper attack: swaps the payload of a valid token but keeps its original signature.</summary>
    public static string SwapPayloadKeepSignature(string validToken, IReadOnlyList<Claim> replacementClaims, string issuer, string audience)
    {
        var parts = validToken.Split('.');
        if (parts.Length != 3)
        {
            throw new InvalidOperationException("Expected a signed JWS token.");
        }

        var handler = new JwtSecurityTokenHandler();
        var inner = new JwtSecurityToken(
            issuer,
            audience,
            replacementClaims,
            DateTime.UtcNow.AddMinutes(-1),
            DateTime.UtcNow.AddMinutes(30));
        var forgedPayload = inner.EncodedPayload;
        return $"{parts[0]}.{forgedPayload}.{parts[2]}";
    }

    /// <summary>Corrupts the signature segment of a valid token (single byte flip).</summary>
    public static string CorruptSignature(string validToken)
    {
        var parts = validToken.Split('.');
        if (parts.Length != 3)
        {
            throw new InvalidOperationException("Expected a signed JWS token.");
        }

        var signature = parts[2];
        var flipped = signature.Length == 0 ? "A" : (signature[0] == 'A' ? 'B' : 'A') + signature[1..];
        return $"{parts[0]}.{parts[1]}.{flipped}";
    }

    private static string Base64UrlEncode(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
