using GameGuild.CQRS;
using GameGuild.Identity.Context.Actors;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace GameGuild.Identity.Authentication;

// ==================== CREATE API KEY ====================

public sealed record CreateApiKeyCommand : ICommand<Result<CreateApiKeyResponse>>
{
    public required string Name { get; init; }
    public required string[] Scopes { get; init; }
    public DateTime? ExpiresAt { get; init; }
    public string? IpWhitelist { get; init; }
}

public sealed record CreateApiKeyResponse
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string ApiKey { get; init; } = string.Empty; // Only returned on creation
    public string KeyPrefix { get; init; } = string.Empty;
    public string[] Scopes { get; init; } = Array.Empty<string>();
    public DateTime? ExpiresAt { get; init; }
    public DateTime CreatedAt { get; init; }

    public static CreateApiKeyResponse FromEntity(ApiKey entity, string plaintext)
    {
        return new CreateApiKeyResponse
        {
            Id = entity.Id,
            Name = entity.Name,
            ApiKey = plaintext,
            KeyPrefix = entity.KeyPrefix,
            Scopes = entity.GetScopes(),
            ExpiresAt = entity.ExpiresAt,
            CreatedAt = entity.CreatedAt
        };
    }
}

public sealed class CreateApiKeyValidator : AbstractValidator<CreateApiKeyCommand>
{
    public CreateApiKeyValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Scopes).NotEmpty().WithMessage("At least one scope is required");
        RuleFor(x => x.ExpiresAt)
            .Must(expiry => !expiry.HasValue || expiry.Value > SystemClock.UtcNow)
            .When(x => x.ExpiresAt.HasValue)
            .WithMessage("Expiry date must be in the future");
    }
}

public sealed class CreateApiKeyHandler : ICommandHandler<CreateApiKeyCommand, Result<CreateApiKeyResponse>>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly IActorContextAccessor _actorContext;
    private readonly ILogger<CreateApiKeyHandler> _logger;
    private readonly IApiKeyAuditEventSink? _apiKeyAuditSink;

    public CreateApiKeyHandler(
        IApplicationDbContext dbContext,
        IActorContextAccessor actorContext,
        ILogger<CreateApiKeyHandler> logger,
        IApiKeyAuditEventSink? apiKeyAuditSink = null)
    {
        _dbContext = dbContext;
        _actorContext = actorContext;
        _logger = logger;
        _apiKeyAuditSink = apiKeyAuditSink;
    }

    public async Task<Result<CreateApiKeyResponse>> Handle(CreateApiKeyCommand request, CancellationToken cancellationToken)
    {
        var actor = _actorContext.ActorContext;
        if (!actor.SubjectIdAsGuid.HasValue)
            return Result.Failure<CreateApiKeyResponse>(Error.Failure("Auth.Required", "User must be authenticated to create API keys"));

        var (apiKey, plaintext) = ApiKey.Create(
            actor.SubjectIdAsGuid.Value,
            actor.TenantId ?? Guid.Empty,
            request.Name,
            request.Scopes,
            request.ExpiresAt,
            request.IpWhitelist);

        _dbContext.Set<ApiKey>().Add(apiKey);
        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation("API key created: {KeyId} for user {UserId} with scopes {Scopes}",
            apiKey.Id, actor.SubjectIdAsGuid.Value, string.Join(", ", request.Scopes));

        await ApiKeyLifecycleAudit.RecordAsync(
            _apiKeyAuditSink,
            new ApiKeyAuditEvent(
                ApiKeyAuditActions.Created,
                apiKey.Id,
                actor.SubjectIdAsGuid.Value,
                apiKey.TenantId,
                Description: $"API key '{apiKey.Name}' created",
                Metadata: new { Scopes = apiKey.GetScopes() }),
            _logger,
            cancellationToken).ConfigureAwait(false);

        return Result.Success(CreateApiKeyResponse.FromEntity(apiKey, plaintext));
    }
}

// ==================== LIST API KEYS ====================

public sealed record ListApiKeysQuery : IRequest<Result<List<ApiKeyDto>>>
{
}

public sealed record ApiKeyDto
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string KeyPrefix { get; init; } = string.Empty;
    public string[] Scopes { get; init; } = Array.Empty<string>();
    public bool IsActive { get; init; }
    public DateTime? ExpiresAt { get; init; }
    public DateTime? LastUsedAt { get; init; }
    public long UsageCount { get; init; }
    public DateTime CreatedAt { get; init; }

    /// <summary>
    ///     Key replaced by this key (set when this key was issued by a rotation)
    /// </summary>
    public Guid? ReplacesKeyId { get; init; }

    /// <summary>
    ///     When the rotation overlap window for this key ends (rotated keys only)
    /// </summary>
    public DateTime? RotationGraceEndsAt { get; init; }

    public static ApiKeyDto FromEntity(ApiKey entity)
    {
        return new ApiKeyDto
        {
            Id = entity.Id,
            Name = entity.Name,
            KeyPrefix = entity.KeyPrefix,
            Scopes = entity.GetScopes(),
            IsActive = entity.IsActive,
            ExpiresAt = entity.ExpiresAt,
            LastUsedAt = entity.LastUsedAt,
            UsageCount = entity.UsageCount,
            CreatedAt = entity.CreatedAt,
            ReplacesKeyId = entity.ReplacesKeyId,
            RotationGraceEndsAt = entity.RotationGraceEndsAt
        };
    }
}

public sealed class ListApiKeysHandler : IRequestHandler<ListApiKeysQuery, Result<List<ApiKeyDto>>>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly IActorContextAccessor _actorContext;

    public ListApiKeysHandler(
        IApplicationDbContext dbContext,
        IActorContextAccessor actorContext)
    {
        _dbContext = dbContext;
        _actorContext = actorContext;
    }

    public async Task<Result<List<ApiKeyDto>>> Handle(ListApiKeysQuery request, CancellationToken cancellationToken)
    {
        var actor = _actorContext.ActorContext;
        if (!actor.SubjectIdAsGuid.HasValue)
            return Result.Failure<List<ApiKeyDto>>(Error.Failure("Auth.Required", "User must be authenticated"));

        var keys = await _dbContext.Set<ApiKey>()
            .Where(k => k.UserId == actor.SubjectIdAsGuid.Value)
            .OrderByDescending(k => k.CreatedAt)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success(keys.Select(ApiKeyDto.FromEntity).ToList());
    }
}

// ==================== REVOKE API KEY ====================

public sealed record RevokeApiKeyCommand : ICommand<Result<bool>>
{
    public required Guid KeyId { get; init; }
    public string? Reason { get; init; }
}

public sealed class RevokeApiKeyHandler : ICommandHandler<RevokeApiKeyCommand, Result<bool>>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly IActorContextAccessor _actorContext;
    private readonly ILogger<RevokeApiKeyHandler> _logger;
    private readonly IApiKeyAuditEventSink? _apiKeyAuditSink;

    public RevokeApiKeyHandler(
        IApplicationDbContext dbContext,
        IActorContextAccessor actorContext,
        ILogger<RevokeApiKeyHandler> logger,
        IApiKeyAuditEventSink? apiKeyAuditSink = null)
    {
        _dbContext = dbContext;
        _actorContext = actorContext;
        _logger = logger;
        _apiKeyAuditSink = apiKeyAuditSink;
    }

    public async Task<Result<bool>> Handle(RevokeApiKeyCommand request, CancellationToken cancellationToken)
    {
        var actor = _actorContext.ActorContext;
        if (!actor.SubjectIdAsGuid.HasValue)
            return Result.Failure<bool>(Error.Failure("Auth.Required", "User must be authenticated"));

        var apiKey = await _dbContext.Set<ApiKey>()
            .FirstOrDefaultAsync(k => k.Id == request.KeyId && k.UserId == actor.SubjectIdAsGuid.Value, cancellationToken).ConfigureAwait(false);

        if (apiKey == null)
            return Result.Failure<bool>(Error.NotFound("ApiKey.NotFound", "API key not found"));

        apiKey.Revoke(request.Reason ?? "User revoked");
        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation("API key revoked: {KeyId} by user {UserId}. Reason: {Reason}",
            request.KeyId, actor.SubjectIdAsGuid.Value, request.Reason);

        await ApiKeyLifecycleAudit.RecordAsync(
            _apiKeyAuditSink,
            new ApiKeyAuditEvent(
                ApiKeyAuditActions.Revoked,
                apiKey.Id,
                actor.SubjectIdAsGuid.Value,
                apiKey.TenantId,
                Description: $"API key '{apiKey.Name}' revoked",
                Metadata: new { Reason = apiKey.RevocationReason }),
            _logger,
            cancellationToken).ConfigureAwait(false);

        return Result.Success(true);
    }
}

// ==================== ROTATE API KEY ====================

public sealed record RotateApiKeyCommand : ICommand<Result<RotateApiKeyResponse>>
{
    public required Guid KeyId { get; init; }

    /// <summary>
    ///     Optional display name for the replacement key; defaults to the old key's name.
    /// </summary>
    public string? Name { get; init; }

    /// <summary>
    ///     Optional scopes for the replacement key; defaults to the old key's scopes.
    /// </summary>
    public string[]? Scopes { get; init; }

    /// <summary>
    ///     Optional absolute expiry for the replacement key; defaults to preserving the old key's expiry.
    /// </summary>
    public DateTime? ExpiresAt { get; init; }

    /// <summary>
    ///     Optional overlap window during which the old key keeps working;
    ///     defaults to <see cref="ApiKeyLifecycleOptions.RotationGracePeriodMinutes"/>.
    /// </summary>
    public TimeSpan? GracePeriod { get; init; }
}

public sealed record RotateApiKeyResponse
{
    /// <summary>
    ///     The key that was rotated (now superseded)
    /// </summary>
    public Guid OldKeyId { get; init; }

    /// <summary>
    ///     The newly issued key (plaintext is only returned here)
    /// </summary>
    public CreateApiKeyResponse NewKey { get; init; } = new();

    /// <summary>
    ///     When the old key stops being honored (null when it was revoked immediately)
    /// </summary>
    public DateTime? OldKeyGraceEndsAt { get; init; }

    /// <summary>
    ///     Whether the old key was revoked immediately instead of getting an overlap window
    /// </summary>
    public bool OldKeyRevoked { get; init; }
}

public sealed class RotateApiKeyValidator : AbstractValidator<RotateApiKeyCommand>
{
    private static readonly TimeSpan MaxGrace = TimeSpan.FromMinutes(43200); // 30 days

    public RotateApiKeyValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(100)
            .When(x => x.Name is not null);
        RuleFor(x => x.Scopes)
            .NotEmpty()
            .WithMessage("At least one scope is required when scopes are overridden")
            .When(x => x.Scopes is not null);
        RuleFor(x => x.ExpiresAt)
            .Must(expiry => !expiry.HasValue || expiry.Value > SystemClock.UtcNow)
            .When(x => x.ExpiresAt.HasValue)
            .WithMessage("Expiry date must be in the future");
        RuleFor(x => x.GracePeriod)
            .Must(grace => !grace.HasValue || (grace.Value >= TimeSpan.Zero && grace.Value <= MaxGrace))
            .When(x => x.GracePeriod.HasValue)
            .WithMessage("Grace period must be between zero and 30 days");
    }
}

public sealed class RotateApiKeyHandler : ICommandHandler<RotateApiKeyCommand, Result<RotateApiKeyResponse>>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly IActorContextAccessor _actorContext;
    private readonly ApiKeyLifecycleOptions _options;
    private readonly ILogger<RotateApiKeyHandler> _logger;
    private readonly IApiKeyAuditEventSink? _apiKeyAuditSink;

    public RotateApiKeyHandler(
        IApplicationDbContext dbContext,
        IActorContextAccessor actorContext,
        ILogger<RotateApiKeyHandler> logger,
        ApiKeyLifecycleOptions? options = null,
        IApiKeyAuditEventSink? apiKeyAuditSink = null)
    {
        _dbContext = dbContext;
        _actorContext = actorContext;
        _options = options ?? new ApiKeyLifecycleOptions();
        _logger = logger;
        _apiKeyAuditSink = apiKeyAuditSink;
    }

    public async Task<Result<RotateApiKeyResponse>> Handle(RotateApiKeyCommand request, CancellationToken cancellationToken)
    {
        var actor = _actorContext.ActorContext;
        if (!actor.SubjectIdAsGuid.HasValue)
            return Result.Failure<RotateApiKeyResponse>(Error.Failure("Auth.Required", "User must be authenticated"));

        var oldKey = await _dbContext.Set<ApiKey>()
            .FirstOrDefaultAsync(k => k.Id == request.KeyId && k.UserId == actor.SubjectIdAsGuid.Value, cancellationToken).ConfigureAwait(false);

        if (oldKey == null)
            return Result.Failure<RotateApiKeyResponse>(Error.NotFound("ApiKey.NotFound", "API key not found"));

        if (!oldKey.IsValid())
            return Result.Failure<RotateApiKeyResponse>(Error.Failure("ApiKey.Invalid", "Only a valid API key can be rotated"));

        var gracePeriod = ResolveGracePeriod(request.GracePeriod);
        var scopes = request.Scopes is { Length: > 0 } ? request.Scopes : oldKey.GetScopes();

        var (newKey, plaintext) = ApiKey.Create(
            oldKey.UserId,
            oldKey.TenantId ?? Guid.Empty,
            request.Name ?? oldKey.Name,
            scopes,
            request.ExpiresAt ?? oldKey.ExpiresAt,
            oldKey.IpWhitelist);
        newKey.ReplacesKeyId = oldKey.Id;

        DateTime? graceEndsAt = null;
        if (gracePeriod > TimeSpan.Zero)
        {
            graceEndsAt = SystemClock.UtcNow.Add(gracePeriod);
            oldKey.BeginRotationGrace(graceEndsAt.Value);
        }
        else
        {
            oldKey.Revoke($"Rotated: superseded by {newKey.Id}");
        }

        _dbContext.Set<ApiKey>().Add(newKey);
        // Single save: new-key issuance and old-key transition are atomic.
        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "API key rotated: {OldKeyId} superseded by {NewKeyId} (grace until {GraceEndsAt}) by user {UserId}",
            oldKey.Id,
            newKey.Id,
            graceEndsAt,
            actor.SubjectIdAsGuid.Value);

        await ApiKeyLifecycleAudit.RecordAsync(
            _apiKeyAuditSink,
            new ApiKeyAuditEvent(
                ApiKeyAuditActions.Rotated,
                newKey.Id,
                actor.SubjectIdAsGuid.Value,
                newKey.TenantId,
                Description: $"API key '{newKey.Name}' rotated from key {oldKey.Id}",
                Metadata: new
                {
                    OldKeyId = oldKey.Id,
                    NewKeyId = newKey.Id,
                    OldKeyGraceEndsAt = graceEndsAt,
                    Scopes = newKey.GetScopes()
                }),
            _logger,
            cancellationToken).ConfigureAwait(false);

        return Result.Success(new RotateApiKeyResponse
        {
            OldKeyId = oldKey.Id,
            NewKey = CreateApiKeyResponse.FromEntity(newKey, plaintext),
            OldKeyGraceEndsAt = graceEndsAt,
            OldKeyRevoked = graceEndsAt is null
        });
    }

    private TimeSpan ResolveGracePeriod(TimeSpan? requested)
    {
        var grace = requested ?? TimeSpan.FromMinutes(_options.RotationGracePeriodMinutes);
        var max = TimeSpan.FromMinutes(Math.Max(1, _options.MaxRotationGracePeriodMinutes));
        if (grace < TimeSpan.Zero)
        {
            grace = TimeSpan.Zero;
        }

        return grace > max ? max : grace;
    }
}

/// <summary>
///     Best-effort lifecycle audit emission: an audit transport failure must never
///     fail a create/rotate/revoke operation.
/// </summary>
internal static class ApiKeyLifecycleAudit
{
    public static async Task RecordAsync(
        IApiKeyAuditEventSink? sink,
        ApiKeyAuditEvent auditEvent,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        if (sink is null)
        {
            return;
        }

        try
        {
            await sink.RecordAsync(auditEvent, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Could not audit API key event {ActionType} for key {ApiKeyId}",
                auditEvent.ActionType, auditEvent.ApiKeyId);
        }
    }
}
