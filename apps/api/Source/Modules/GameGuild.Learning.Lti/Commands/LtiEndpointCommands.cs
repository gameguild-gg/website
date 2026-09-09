using System.IdentityModel.Tokens.Jwt;
using GameGuild.CQRS;
using GameGuild.Identity.Authentication;
using GameGuild.Identity.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace GameGuild.Learning.Lti;

public sealed record LaunchLtiCommand(string State, string IdToken) : ICommand<LtiLaunchResult>;

public sealed record CreateLtiDeploymentCommand(
    string Issuer,
    string ClientId,
    string DeploymentId,
    string AuthTokenUrl,
    string PlatformJwksUrl,
    string AuthorizationUrl,
    string KeyId,
    string PrivateKeyPem,
    bool Active) : ICommand<LtiDeployment>;

public sealed record CreateLtiLineItemCommand(
    Guid DeploymentId,
    Guid AssessmentId,
    string LineItemId,
    string LineItemUrl,
    int MaxScore) : ICommand<CreateLtiLineItemResult>;

public enum LtiLaunchStatus
{
    Success,
    MalformedToken,
    UnknownPlatform,
    InvalidToken,
    MissingClaims,
    InvalidState,
    UserNotFound
}

public sealed record LtiLaunchResult(LtiLaunchStatus Status, string? SessionToken = null);

public enum CreateLtiLineItemStatus
{
    Success,
    DeploymentNotFound,
    AssessmentAlreadyMapped
}

public sealed record CreateLtiLineItemResult(
    CreateLtiLineItemStatus Status,
    LtiLineItemMapping? Mapping = null);

public sealed class LtiEndpointCommandHandler(
    IApplicationDbContext context,
    LtiLaunchStateStore stateStore,
    LtiPlatformJwksService jwksService,
    IJwtTokenService jwtTokenService,
    ILogger<LtiEndpointCommandHandler> logger) :
    ICommandHandler<LaunchLtiCommand, LtiLaunchResult>,
    ICommandHandler<CreateLtiDeploymentCommand, LtiDeployment>,
    ICommandHandler<CreateLtiLineItemCommand, CreateLtiLineItemResult>
{
    public async Task<LtiLaunchResult> Handle(
        LaunchLtiCommand request,
        CancellationToken cancellationToken)
    {
        string issuer;
        string audience;
        try
        {
            var unvalidated = new JwtSecurityTokenHandler().ReadJwtToken(request.IdToken);
            issuer = unvalidated.Issuer;
            audience = unvalidated.Audiences.FirstOrDefault() ?? string.Empty;
        }
        catch (ArgumentException)
        {
            return new LtiLaunchResult(LtiLaunchStatus.MalformedToken);
        }

        var deployment = await FindActiveDeploymentAsync(issuer, audience, cancellationToken).ConfigureAwait(false);
        if (deployment is null)
        {
            return new LtiLaunchResult(LtiLaunchStatus.UnknownPlatform);
        }

        var principal = await jwksService.ValidateIdTokenAsync(request.IdToken, deployment).ConfigureAwait(false);
        if (principal is null)
        {
            return new LtiLaunchResult(LtiLaunchStatus.InvalidToken);
        }

        var sub = principal.FindFirst("sub")?.Value;
        var nonce = principal.FindFirst("nonce")?.Value;
        if (string.IsNullOrEmpty(sub) || string.IsNullOrEmpty(nonce))
        {
            return new LtiLaunchResult(LtiLaunchStatus.MissingClaims);
        }

        if (!stateStore.TryConsume(request.State, nonce, deployment.Id))
        {
            return new LtiLaunchResult(LtiLaunchStatus.InvalidState);
        }

        var user = await ResolveUserAsync(
            deployment,
            sub,
            principal.FindFirst("email")?.Value,
            cancellationToken).ConfigureAwait(false);
        if (user is null)
        {
            return new LtiLaunchResult(LtiLaunchStatus.UserNotFound);
        }

        var sessionToken = await jwtTokenService
            .GenerateAccessTokenAsync(user.Id, user.Email, Array.Empty<string>(), user.TenantId)
            .ConfigureAwait(false);
        return new LtiLaunchResult(LtiLaunchStatus.Success, sessionToken);
    }

    public async Task<LtiDeployment> Handle(
        CreateLtiDeploymentCommand request,
        CancellationToken cancellationToken)
    {
        var deployment = LtiDeployment.Create(
            request.Issuer,
            request.ClientId,
            request.DeploymentId,
            request.AuthTokenUrl,
            request.PlatformJwksUrl,
            request.AuthorizationUrl,
            request.KeyId,
            request.PrivateKeyPem,
            request.Active);
        context.Set<LtiDeployment>().Add(deployment);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        logger.LogInformation("LTI deployment created: {DeploymentId}", deployment.Id);
        return deployment;
    }

    public async Task<CreateLtiLineItemResult> Handle(
        CreateLtiLineItemCommand request,
        CancellationToken cancellationToken)
    {
        var deployment = await context.Set<LtiDeployment>()
            .FirstOrDefaultAsync(
                deployment => deployment.Id == request.DeploymentId && deployment.DeletedAt == null,
                cancellationToken)
            .ConfigureAwait(false);
        if (deployment is null)
        {
            return new CreateLtiLineItemResult(CreateLtiLineItemStatus.DeploymentNotFound);
        }

        var exists = await context.Set<LtiLineItemMapping>()
            .AnyAsync(mapping => mapping.AssessmentId == request.AssessmentId, cancellationToken)
            .ConfigureAwait(false);
        if (exists)
        {
            return new CreateLtiLineItemResult(CreateLtiLineItemStatus.AssessmentAlreadyMapped);
        }

        var mapping = LtiLineItemMapping.Create(
            request.AssessmentId,
            deployment.Id,
            request.LineItemId,
            request.LineItemUrl,
            request.MaxScore);
        context.Set<LtiLineItemMapping>().Add(mapping);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        logger.LogInformation(
            "LTI line item mapping created: assessment {AssessmentId} -> {LineItemUrl}",
            request.AssessmentId,
            request.LineItemUrl);
        return new CreateLtiLineItemResult(CreateLtiLineItemStatus.Success, mapping);
    }

    private Task<LtiDeployment?> FindActiveDeploymentAsync(
        string issuer,
        string clientId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(issuer) || string.IsNullOrEmpty(clientId))
        {
            return Task.FromResult<LtiDeployment?>(null);
        }

        return context.Set<LtiDeployment>().FirstOrDefaultAsync(
            deployment =>
                deployment.Issuer == issuer &&
                deployment.ClientId == clientId &&
                deployment.Active &&
                deployment.DeletedAt == null,
            cancellationToken);
    }

    private async Task<User?> ResolveUserAsync(
        LtiDeployment deployment,
        string sub,
        string? email,
        CancellationToken cancellationToken)
    {
        var mapping = await context.Set<LtiUserMapping>()
            .FirstOrDefaultAsync(
                mapping => mapping.DeploymentId == deployment.Id && mapping.Sub == sub,
                cancellationToken)
            .ConfigureAwait(false);

        if (mapping is not null)
        {
            return await context.Set<User>()
                .FirstOrDefaultAsync(
                    user => user.Id == mapping.UserId && user.DeletedAt == null,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        if (string.IsNullOrEmpty(email))
        {
            return null;
        }

        var normalized = email.Trim().ToLowerInvariant();
        var user = await context.Set<User>()
            .FirstOrDefaultAsync(
                user => user.DeletedAt == null && user.Email.ToLower() == normalized,
                cancellationToken)
            .ConfigureAwait(false);
        if (user is null)
        {
            return null;
        }

        try
        {
            context.Set<LtiUserMapping>().Add(LtiUserMapping.Create(deployment.Id, user.Id, sub));
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogWarning(
                exception,
                "LTI: user mapping upsert raced for deployment {DeploymentId} sub {Sub}",
                deployment.Id,
                sub);
        }

        return user;
    }
}
