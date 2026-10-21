using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using GameGuild.Configuration.ApplicationLayer;
using GameGuild.Identity.Authorization;
using GameGuild.Identity.Authorization.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace GameGuild.Identity.Authentication;

/// <summary>
///     Guest token issuance and validation (issue #213). Tokens are HS256 JWTs in the guest
///     audience namespace, signed with the platform key, carrying exactly: jti, iat, exp,
///     guest_id and the guest role. They never carry <c>sub</c>, <c>email</c> or tenant
///     claims, so a guest principal can never resolve to a real user id or tenant — and the
///     standard bearer scheme rejects the guest audience outright (structural fail-closed).
/// </summary>
public sealed class GuestTokenService(
    IOptions<JwtOptions> jwtOptions,
    ILogger<GuestTokenService> logger) : IGuestTokenService
{
    private readonly JwtOptions _jwtOptions = jwtOptions.Value;

    /// <inheritdoc />
    public string Issue(Guid guestSessionId, DateTime expiresAt)
    {
        if (guestSessionId == Guid.Empty)
        {
            throw new ArgumentException("A guest session id is required.", nameof(guestSessionId));
        }

        var now = SystemClock.UtcNow;
        if (expiresAt <= now)
        {
            throw new ArgumentException("The guest token must expire in the future.", nameof(expiresAt));
        }

        var issuedAt = new DateTimeOffset(now, TimeSpan.Zero);
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new(JwtRegisteredClaimNames.Iat, issuedAt.ToUnixTimeSeconds().ToString(System.Globalization.CultureInfo.InvariantCulture), ClaimValueTypes.Integer64),
            new(ClaimNames.GuestId, guestSessionId.ToString()),
            new(ClaimNames.Role, GuestIdentity.RoleName)
        };

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtOptions.SecretKey)) { KeyId = "GameGuild-jwt-key" };
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            _jwtOptions.Issuer,
            GuestAccessDefaults.Audience,
            claims,
            now,
            expiresAt,
            credentials);

        var encoded = new JwtSecurityTokenHandler().WriteToken(token);
        logger.LogInformation("Guest token issued for guest session {GuestSessionId}, expires {ExpiresAt}", guestSessionId, expiresAt);
        return encoded;
    }

    /// <inheritdoc />
    public ClaimsPrincipal? Validate(string? guestToken)
    {
        if (string.IsNullOrWhiteSpace(guestToken))
        {
            return null;
        }

        try
        {
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtOptions.SecretKey)) { KeyId = "GameGuild-jwt-key" };
            var parameters = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = key,
                ValidateIssuer = true,
                ValidIssuer = _jwtOptions.Issuer,
                ValidateAudience = true,
                ValidAudience = GuestAccessDefaults.Audience,
                ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
                ValidateLifetime = true,
                ClockSkew = TimeSpan.Zero,
                NameClaimType = ClaimNames.GuestId,
                RoleClaimType = ClaimNames.Role
            };

            var principal = new JwtSecurityTokenHandler().ValidateToken(guestToken, parameters, out _);
            // Structural guest check on top of cryptography: the right audience is not
            // sufficient, the principal must be a well-formed guest (no user-id claims).
            return principal.IsGuest() ? principal : null;
        }
        catch (Exception exception) when (exception is SecurityTokenException or ArgumentException)
        {
            logger.LogInformation("Guest token validation rejected a credential: {FailureType}", exception.GetType().Name);
            return null;
        }
    }
}
