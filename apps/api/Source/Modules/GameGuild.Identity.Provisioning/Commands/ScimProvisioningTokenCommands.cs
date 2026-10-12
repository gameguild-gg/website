using GameGuild.CQRS;
using GameGuild.Identity.Authorization;
using GameGuild.Identity.Context.Actors;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GameGuild.Identity.Provisioning;

// ==================== SCIM PROVISIONING TOKEN ADMIN (JWT) ====================

public sealed record CreateScimProvisioningTokenCommand(
    string Name,
    string[]? Scopes,
    DateTime? ExpiresAt) : ICommand<Result<CreateScimProvisioningTokenResponse>>;

public sealed record RotateScimProvisioningTokenCommand(
    Guid TokenId,
    string? Name,
    string[]? Scopes,
    DateTime? ExpiresAt,
    int? GracePeriodMinutes) : ICommand<Result<RotateScimProvisioningTokenResponse>>;

public sealed record RevokeScimProvisioningTokenCommand(Guid TokenId, string? Reason) : ICommand<Result<bool>>;

public sealed record ListScimProvisioningTokensQuery : IRequest<Result<List<ScimProvisioningTokenDto>>>;

public sealed record CreateScimProvisioningTokenResponse
{
    public Guid Id { get; init; }

    public string Name { get; init; } = string.Empty;

    /// <summary>The only time the plaintext token is returned.</summary>
    public string Token { get; init; } = string.Empty;

    public string[] Scopes { get; init; } = [];

    public DateTime? ExpiresAt { get; init; }

    public DateTime CreatedAt { get; init; }

    public Guid TenantId { get; init; }
}

public sealed record RotateScimProvisioningTokenResponse
{
    public Guid Id { get; init; }

    public string Token { get; init; } = string.Empty;

    public string[] Scopes { get; init; } = [];

    public DateTime? ExpiresAt { get; init; }

    public Guid? ReplacesTokenId { get; init; }

    public DateTime? OldTokenGraceEndsAt { get; init; }
}

public sealed record ScimProvisioningTokenDto
{
    public Guid Id { get; init; }

    public string Name { get; init; } = string.Empty;

    public string[] Scopes { get; init; } = [];

    public bool IsActive { get; init; }

    public DateTime? ExpiresAt { get; init; }

    public DateTime? LastUsedAt { get; init; }

    public long UsageCount { get; init; }

    public DateTime CreatedAt { get; init; }

    public Guid? ReplacesTokenId { get; init; }

    public DateTime? RotationGraceEndsAt { get; init; }

    public static ScimProvisioningTokenDto FromEntity(ScimProvisioningToken entity) => new()
    {
        Id = entity.Id,
        Name = entity.Name,
        Scopes = entity.GetScopes(),
        IsActive = entity.IsValid(),
        ExpiresAt = entity.ExpiresAt,
        LastUsedAt = entity.LastUsedAt,
        UsageCount = entity.UsageCount,
        CreatedAt = entity.CreatedAt,
        ReplacesTokenId = entity.ReplacesTokenId,
        RotationGraceEndsAt = entity.RotationGraceEndsAt
    };
}

/// <summary>
///     Guard shared by the token admin handlers: the acting identity comes from the
///     authenticated JWT principal (never the request body), and a tenant must be in
///     context — the platform tenant cannot own provisioning tokens.
/// </summary>
internal static class ScimTokenAdminGuards
{
    public static (Guid UserId, Guid TenantId) RequireActor(IActorContextAccessor actorContext)
    {
        var actor = actorContext.ActorContext;
        if (!actor.IsAuthenticated || !actor.SubjectIdAsGuid.HasValue)
        {
            throw new ScimException(401, null, "An authenticated administrator is required.");
        }

        if (actor.TenantId is not { } tenantId || tenantId == Guid.Empty)
        {
            throw new ScimException(403, null, "SCIM provisioning tokens are managed inside a tenant context.");
        }

        return (actor.SubjectIdAsGuid.Value, tenantId);
    }
}

public sealed class CreateScimProvisioningTokenHandler(
    IScimProvisioningTokenRepository tokenRepository,
    IActorContextAccessor actorContext,
    ITenantSecurityVersionStore? securityVersionStore,
    ILogger<CreateScimProvisioningTokenHandler> logger,
    IScimProvisioningAuditSink? auditSink = null)
    : ICommandHandler<CreateScimProvisioningTokenCommand, Result<CreateScimProvisioningTokenResponse>>
{
    public async Task<Result<CreateScimProvisioningTokenResponse>> Handle(CreateScimProvisioningTokenCommand request, CancellationToken cancellationToken)
    {
        var (userId, tenantId) = ScimTokenAdminGuards.RequireActor(actorContext);
        var scopes = NormalizeScopes(request.Scopes);

        var (token, plaintext) = ScimProvisioningToken.Create(tenantId, userId, request.Name.Trim(), scopes, request.ExpiresAt);
        await tokenRepository.AddAsync(token, cancellationToken).ConfigureAwait(false);

        // Issuing a provisioning token is a permission-touching mutation: bump the tenant
        // security version and leave an audit trail.
        if (securityVersionStore is not null)
        {
            await securityVersionStore.IncrementVersionAsync(tenantId.ToString(), cancellationToken).ConfigureAwait(false);
        }

        await ScimProvisioningAudit.RecordAsync(
            auditSink,
            new ScimProvisioningAuditEvent(
                ScimProvisioningAuditActions.TokenIssued,
                TargetUserId: null,
                TargetRoleId: null,
                tenantId,
                actorContext.ActorContext.SubjectId ?? "unknown",
                Detail: $"Provisioning token '{token.Name}' issued",
                Metadata: new Dictionary<string, object?> { ["scopes"] = token.GetScopes() }),
            logger,
            cancellationToken).ConfigureAwait(false);

        logger.LogInformation("SCIM provisioning token {TokenId} issued for tenant {TenantId}", token.Id, tenantId);

        return Result.Success(new CreateScimProvisioningTokenResponse
        {
            Id = token.Id,
            Name = token.Name,
            Token = plaintext,
            Scopes = token.GetScopes(),
            ExpiresAt = token.ExpiresAt,
            CreatedAt = token.CreatedAt,
            TenantId = tenantId
        });
    }

    internal static string[] NormalizeScopes(string[]? scopes)
        => scopes is not { Length: > 0 } ? ScimScopes.All : scopes;
}

public sealed class RotateScimProvisioningTokenHandler(
    IScimProvisioningTokenRepository tokenRepository,
    IActorContextAccessor actorContext,
    ITenantSecurityVersionStore? securityVersionStore,
    IOptions<ScimProvisioningOptions> options,
    ILogger<RotateScimProvisioningTokenHandler> logger,
    IScimProvisioningAuditSink? auditSink = null)
    : ICommandHandler<RotateScimProvisioningTokenCommand, Result<RotateScimProvisioningTokenResponse>>
{
    public async Task<Result<RotateScimProvisioningTokenResponse>> Handle(RotateScimProvisioningTokenCommand request, CancellationToken cancellationToken)
    {
        var (userId, tenantId) = ScimTokenAdminGuards.RequireActor(actorContext);

        var oldToken = await tokenRepository
            .GetByIdForTenantAsync(request.TokenId, tenantId, cancellationToken).ConfigureAwait(false);
        if (oldToken is null)
        {
            return Result.Failure<RotateScimProvisioningTokenResponse>(
                Error.NotFound("Scim.TokenNotFound", $"SCIM provisioning token {request.TokenId} was not found in this tenant."));
        }

        var scopes = request.Scopes is { Length: > 0 } ? request.Scopes : oldToken.GetScopes();
        var expiresAt = request.ExpiresAt ?? oldToken.ExpiresAt;
        var (newToken, plaintext) = ScimProvisioningToken.Create(
            tenantId,
            userId,
            request.Name?.Trim() is { Length: > 0 } name ? name : oldToken.Name,
            scopes,
            expiresAt);

        var graceEndsAt = request.GracePeriodMinutes is { } minutes && minutes > 0
            ? SystemClock.UtcNow.Add(TimeSpan.FromMinutes(minutes))
            : options.Value.RotationGracePeriod > TimeSpan.Zero
                ? SystemClock.UtcNow.Add(options.Value.RotationGracePeriod)
                : (DateTime?)null;

        await tokenRepository.RotateAsync(oldToken, newToken, graceEndsAt, cancellationToken).ConfigureAwait(false);

        if (securityVersionStore is not null)
        {
            await securityVersionStore.IncrementVersionAsync(tenantId.ToString(), cancellationToken).ConfigureAwait(false);
        }

        await ScimProvisioningAudit.RecordAsync(
            auditSink,
            new ScimProvisioningAuditEvent(
                ScimProvisioningAuditActions.TokenRotated,
                TargetUserId: null,
                TargetRoleId: null,
                tenantId,
                actorContext.ActorContext.SubjectId ?? "unknown",
                Detail: $"Provisioning token '{newToken.Name}' rotated; old token {oldToken.Id}"),
            logger,
            cancellationToken).ConfigureAwait(false);

        return Result.Success(new RotateScimProvisioningTokenResponse
        {
            Id = newToken.Id,
            Token = plaintext,
            Scopes = newToken.GetScopes(),
            ExpiresAt = newToken.ExpiresAt,
            ReplacesTokenId = oldToken.Id,
            OldTokenGraceEndsAt = graceEndsAt
        });
    }
}

public sealed class RevokeScimProvisioningTokenHandler(
    IScimProvisioningTokenRepository tokenRepository,
    IActorContextAccessor actorContext,
    ITenantSecurityVersionStore? securityVersionStore,
    ILogger<RevokeScimProvisioningTokenHandler> logger,
    IScimProvisioningAuditSink? auditSink = null)
    : ICommandHandler<RevokeScimProvisioningTokenCommand, Result<bool>>
{
    public async Task<Result<bool>> Handle(RevokeScimProvisioningTokenCommand request, CancellationToken cancellationToken)
    {
        var (_, tenantId) = ScimTokenAdminGuards.RequireActor(actorContext);

        var revoked = await tokenRepository
            .RevokeAsync(request.TokenId, tenantId, request.Reason ?? "Revoked by administrator", cancellationToken)
            .ConfigureAwait(false);
        if (revoked is null)
        {
            return Result.Failure<bool>(
                Error.NotFound("Scim.TokenNotFound", $"SCIM provisioning token {request.TokenId} was not found in this tenant."));
        }

        if (securityVersionStore is not null)
        {
            await securityVersionStore.IncrementVersionAsync(tenantId.ToString(), cancellationToken).ConfigureAwait(false);
        }

        await ScimProvisioningAudit.RecordAsync(
            auditSink,
            new ScimProvisioningAuditEvent(
                ScimProvisioningAuditActions.TokenRevoked,
                TargetUserId: null,
                TargetRoleId: null,
                tenantId,
                actorContext.ActorContext.SubjectId ?? "unknown",
                Detail: $"Provisioning token '{revoked.Name}' revoked"),
            logger,
            cancellationToken).ConfigureAwait(false);

        return Result.Success(true);
    }
}

public sealed class ListScimProvisioningTokensHandler(
    IScimProvisioningTokenRepository tokenRepository,
    IActorContextAccessor actorContext)
    : IRequestHandler<ListScimProvisioningTokensQuery, Result<List<ScimProvisioningTokenDto>>>
{
    public async Task<Result<List<ScimProvisioningTokenDto>>> Handle(ListScimProvisioningTokensQuery request, CancellationToken cancellationToken)
    {
        var (_, tenantId) = ScimTokenAdminGuards.RequireActor(actorContext);
        var tokens = await tokenRepository.GetByTenantAsync(tenantId, cancellationToken).ConfigureAwait(false);
        return Result.Success(tokens.Select(ScimProvisioningTokenDto.FromEntity).ToList());
    }
}
