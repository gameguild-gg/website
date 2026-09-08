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

    public CreateApiKeyHandler(
        IApplicationDbContext dbContext,
        IActorContextAccessor actorContext,
        ILogger<CreateApiKeyHandler> logger)
    {
        _dbContext = dbContext;
        _actorContext = actorContext;
        _logger = logger;
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
            CreatedAt = entity.CreatedAt
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

    public RevokeApiKeyHandler(
        IApplicationDbContext dbContext,
        IActorContextAccessor actorContext,
        ILogger<RevokeApiKeyHandler> logger)
    {
        _dbContext = dbContext;
        _actorContext = actorContext;
        _logger = logger;
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

        return Result.Success(true);
    }
}
