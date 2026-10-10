using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GameGuild.Identity.Provisioning;

/// <summary>
///     Authentication handler for tenant-scoped SCIM provisioning tokens
///     (<c>Authorization: Bearer gg_scim_…</c>). Mirrors the API-key handler: SHA-256
///     hash lookup, active/expiry/rotation validation, usage recording. The tenant claim
///     always comes from the token, never from the request.
/// </summary>
public sealed class ScimProvisioningAuthenticationHandler(
    IOptionsMonitor<ScimProvisioningAuthenticationOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    IScimProvisioningTokenRepository tokenRepository,
    IScimProvisioningAuditSink? auditSink = null)
    : AuthenticationHandler<ScimProvisioningAuthenticationOptions>(options, logger, encoder)
{
    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var authorization = Request.Headers.Authorization.ToString();
        if (string.IsNullOrWhiteSpace(authorization))
        {
            return AuthenticateResult.NoResult();
        }

        const string bearerPrefix = "Bearer ";
        if (!authorization.StartsWith(bearerPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return AuthenticateResult.NoResult();
        }

        var token = authorization[bearerPrefix.Length..].Trim();
        if (!token.StartsWith(ScimProvisioningToken.TokenPrefix, StringComparison.Ordinal))
        {
            // Not a provisioning token: leave the request to other schemes (fail closed later).
            return AuthenticateResult.NoResult();
        }

        ScimProvisioningToken? entity = null;
        try
        {
            entity = await tokenRepository.GetByKeyHashAsync(ComputeHash(token), Context.RequestAborted).ConfigureAwait(false);
            if (entity is null)
            {
                Logger.LogWarning("Unknown SCIM provisioning token presented");
                return await FailAsync("Invalid SCIM provisioning token", "InvalidToken").ConfigureAwait(false);
            }

            if (!entity.IsValid())
            {
                // Lazily finalize rotation once the overlap window closes.
                await tokenRepository.FinalizeRotationRevocationAsync(entity, Context.RequestAborted).ConfigureAwait(false);
                Logger.LogWarning("Inactive or expired SCIM provisioning token {TokenId} used", entity.Id);
                return await FailAsync("SCIM provisioning token is inactive or expired", "InactiveOrExpiredToken").ConfigureAwait(false);
            }

            await tokenRepository.RecordUsageAsync(entity, Context.RequestAborted).ConfigureAwait(false);

            var claims = new List<Claim>
            {
                new("tenant_id", entity.TenantId.ToString()),
                new("scim_token_id", entity.Id.ToString()),
                new("auth_method", "scim_token"),
                new("sub", $"scim_token:{entity.Id:N}")
            };

            foreach (var scope in entity.GetScopes())
            {
                claims.Add(new Claim("scope", scope));
            }

            var identity = new ClaimsIdentity(claims, Scheme.Name);
            var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name);

            Logger.LogInformation("SCIM provisioning token {TokenId} authenticated for tenant {TenantId}",
                entity.Id, entity.TenantId);

            await RecordAuditAsync(success: true, entity, auditReason: null).ConfigureAwait(false);
            return AuthenticateResult.Success(ticket);
        }
        catch (Exception exception)
        {
            Logger.LogError(exception, "Error during SCIM provisioning token authentication");
            return await FailAsync("Authentication error", "AuthenticationError", entity).ConfigureAwait(false);
        }
    }

    protected override Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        Response.StatusCode = StatusCodes.Status401Unauthorized;
        Response.Headers.WWWAuthenticate = "Bearer realm=\"GameGuild SCIM\"";
        return Task.CompletedTask;
    }

    private async Task<AuthenticateResult> FailAsync(string message, string auditReason, ScimProvisioningToken? entity = null)
    {
        await RecordAuditAsync(success: false, entity, auditReason).ConfigureAwait(false);
        return AuthenticateResult.Fail(message);
    }

    private async Task RecordAuditAsync(bool success, ScimProvisioningToken? entity, string? auditReason)
    {
        if (auditSink is null)
        {
            return;
        }

        try
        {
            await auditSink.RecordAsync(
                new ScimProvisioningAuditEvent(
                    success ? "Scim.TokenAuthenticated" : "Scim.TokenAuthenticationFailed",
                    TargetUserId: null,
                    TargetRoleId: null,
                    TenantId: entity?.TenantId ?? Guid.Empty,
                    Actor: entity is null ? "unknown" : $"scim_token:{entity.Id:N}",
                    Detail: auditReason),
                CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            Logger.LogError(exception, "Could not record SCIM token authentication audit event");
        }
    }

    private static string ComputeHash(string plaintext)
    {
        using var sha256 = SHA256.Create();
        var bytes = System.Text.Encoding.UTF8.GetBytes(plaintext);
        return Convert.ToHexString(sha256.ComputeHash(bytes)).ToLowerInvariant();
    }
}

/// <summary>Options for the SCIM provisioning scheme.</summary>
public sealed class ScimProvisioningAuthenticationOptions : AuthenticationSchemeOptions
{
    public const string SchemeName = "ScimProvisioning";
}

/// <summary>Registration helper for the SCIM provisioning scheme.</summary>
public static class ScimProvisioningAuthenticationExtensions
{
    public static AuthenticationBuilder AddScimProvisioningAuthentication(this AuthenticationBuilder builder)
    {
        return builder.AddScheme<ScimProvisioningAuthenticationOptions, ScimProvisioningAuthenticationHandler>(
            ScimProvisioningAuthenticationOptions.SchemeName,
            _ => { });
    }
}
