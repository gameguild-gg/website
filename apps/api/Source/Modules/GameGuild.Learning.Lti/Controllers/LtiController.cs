using System.IdentityModel.Tokens.Jwt;
using System.Security.Cryptography;
using GameGuild.CQRS;
using GameGuild.Identity.Context.Actors;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;

namespace GameGuild.Learning.Lti;

/// <summary>
/// LTI 1.3 tool endpoints: OIDC third-party login, launch validation, tool JWKS,
/// and admin deployment/line-item management. Launch/login/jwks are unauthenticated
/// per the LTI spec; everything else is system-admin only.
/// </summary>
[Authorize]
public sealed class LtiController(
    IApplicationDbContext context,
    LtiLaunchStateStore stateStore,
    IActorContextAccessor actorContextAccessor,
    ILogger<LtiController> logger,
    ISender sender) : BaseApiController
{
    private const string SessionCookieName = "gg_session";

    [AllowAnonymous]
    [HttpGet(".well-known/jwks.json")]
    public async Task<IActionResult> Jwks()
    {
        var deployments = await context.Set<LtiDeployment>()
            .Where(d => d.Active && d.DeletedAt == null)
            .ToListAsync()
            .ConfigureAwait(false);

        var keys = new List<object>();
        foreach (var deployment in deployments)
        {
            try
            {
                using var rsa = RSA.Create();
                rsa.ImportFromPem(deployment.PrivateKeyPem);
                var parameters = rsa.ExportParameters(false);
                keys.Add(new
                {
                    kty = "RSA",
                    use = "sig",
                    alg = "RS256",
                    kid = deployment.KeyId,
                    n = Base64UrlEncoder.Encode(parameters.Modulus!),
                    e = Base64UrlEncoder.Encode(parameters.Exponent!)
                });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "LTI: deployment {DeploymentId} has an unreadable private key; excluded from JWKS", deployment.Id);
            }
        }

        return Ok(new { keys });
    }

    /// <summary>
    /// OIDC third-party-initiated login. Validates the platform against registered
    /// active deployments, then redirects to the deployment's configured authorization
    /// endpoint with state+nonce. All redirect targets come from admin-configured
    /// deployment records — never from request input.
    /// </summary>
    [AllowAnonymous]
    [HttpPost("lti/login")]
    [NoBusinessMutationEndpoint("OIDC login initiation only validates configuration and creates ephemeral anti-replay state; it does not change durable business state.")]
    public async Task<IActionResult> Login()
    {
        if (!Request.HasFormContentType)
        {
            return BadRequest("Login initiation must be a form POST.");
        }

        var form = await Request.ReadFormAsync().ConfigureAwait(false);
        var issuer = form["iss"].ToString();
        var clientId = form["client_id"].ToString();
        var deploymentId = form["deployment_id"].ToString();

        var deployment = await FindActiveDeploymentAsync(issuer, clientId).ConfigureAwait(false);
        if (deployment is null || !string.Equals(deployment.DeploymentId, deploymentId, StringComparison.Ordinal))
        {
            return Unauthorized("Unknown LTI platform.");
        }

        var loginHint = form["login_hint"].ToString();
        if (string.IsNullOrEmpty(loginHint))
        {
            return BadRequest("login_hint is required.");
        }

        var (state, nonce) = stateStore.Issue(deployment.Id);
        var redirectUri = $"{Request.Scheme}://{Request.Host}/lti/launch";
        var query = new Dictionary<string, string?>
        {
            ["scope"] = "openid",
            ["response_type"] = "id_token",
            ["response_mode"] = "form_post",
            ["prompt"] = "none",
            ["client_id"] = deployment.ClientId,
            ["redirect_uri"] = redirectUri,
            ["login_hint"] = loginHint,
            ["state"] = state,
            ["nonce"] = nonce
        };
        var messageHint = form["lti_message_hint"].ToString();
        if (!string.IsNullOrEmpty(messageHint))
        {
            query["lti_message_hint"] = messageHint;
        }

        var separator = deployment.AuthorizationUrl.Contains('?') ? '&' : '?';
        return Redirect(deployment.AuthorizationUrl + separator + QueryString.Create(query).Value);
    }

    /// <summary>
    /// LTI 1.3 launch: the platform form-POSTs the signed id_token here.
    /// id_token in the query string is rejected outright (leaks into logs/history).
    /// </summary>
    [AllowAnonymous]
    [HttpPost("lti/launch")]
    public async Task<IActionResult> Launch()
    {
        if (Request.Query.ContainsKey("id_token"))
        {
            return BadRequest("id_token must be delivered in the POST body.");
        }

        if (!Request.HasFormContentType)
        {
            return BadRequest("Launch must be a form POST.");
        }

        var form = await Request.ReadFormAsync().ConfigureAwait(false);
        var state = form["state"].ToString();
        var idToken = form["id_token"].ToString();
        if (string.IsNullOrEmpty(state) || string.IsNullOrEmpty(idToken))
        {
            return BadRequest("state and id_token are required.");
        }

        var result = await sender.Send(new LaunchLtiCommand(state, idToken)).ConfigureAwait(false);
        if (result.Status == LtiLaunchStatus.MalformedToken)
        {
            return BadRequest("Malformed id_token.");
        }

        if (result.Status != LtiLaunchStatus.Success)
        {
            var message = result.Status switch
            {
                LtiLaunchStatus.UnknownPlatform => "Unknown LTI platform.",
                LtiLaunchStatus.InvalidToken => "Invalid launch token.",
                LtiLaunchStatus.MissingClaims => "Launch token is missing required claims.",
                LtiLaunchStatus.InvalidState => "Unknown or already used launch state.",
                LtiLaunchStatus.UserNotFound => "No gameguild account matches this launch",
                _ => "Invalid LTI launch."
            };
            return Unauthorized(message);
        }

        // SameSite=None is required: the tool runs inside the platform's iframe.
        Response.Cookies.Append(SessionCookieName, result.SessionToken!, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.None,
            Path = "/"
        });
        return Redirect("/dashboard/tasks");
    }

    // ===== admin: deployment + line-item management =====

    [HttpPost("v1/lti/deployments")]
    public async Task<IActionResult> CreateDeployment([FromBody] CreateLtiDeploymentRequest request)
    {
        if (!actorContextAccessor.ActorContext.IsSystemAdmin)
        {
            return Forbid();
        }

        try
        {
            var deployment = await sender.Send(new CreateLtiDeploymentCommand(
                request.Issuer, request.ClientId, request.DeploymentId,
                request.AuthTokenUrl, request.PlatformJwksUrl, request.AuthorizationUrl,
                request.KeyId, request.PrivateKeyPem, request.Active)).ConfigureAwait(false);

            return Created($"/v1/lti/deployments/{deployment.Id}", LtiDeploymentDto.FromEntity(deployment));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpPost("v1/lti/deployments/{id:guid}/line-items")]
    public async Task<IActionResult> CreateLineItem(Guid id, [FromBody] CreateLtiLineItemRequest request)
    {
        if (!actorContextAccessor.ActorContext.IsSystemAdmin)
        {
            return Forbid();
        }

        try
        {
            var result = await sender.Send(new CreateLtiLineItemCommand(
                id,
                request.AssessmentId,
                request.LineItemId,
                request.LineItemUrl,
                request.MaxScore)).ConfigureAwait(false);

            if (result.Status == CreateLtiLineItemStatus.DeploymentNotFound)
            {
                return NotFound();
            }

            if (result.Status == CreateLtiLineItemStatus.AssessmentAlreadyMapped)
            {
                return Conflict($"Assessment {request.AssessmentId} is already mapped to a line item.");
            }

            var mapping = result.Mapping!;
            return Created($"/v1/lti/deployments/{id}/line-items/{mapping.Id}", LtiLineItemMappingDto.FromEntity(mapping));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    private async Task<LtiDeployment?> FindActiveDeploymentAsync(string issuer, string clientId)
    {
        if (string.IsNullOrEmpty(issuer) || string.IsNullOrEmpty(clientId))
        {
            return null;
        }

        return await context.Set<LtiDeployment>()
            .FirstOrDefaultAsync(d => d.Issuer == issuer && d.ClientId == clientId && d.Active && d.DeletedAt == null)
            .ConfigureAwait(false);
    }

}
