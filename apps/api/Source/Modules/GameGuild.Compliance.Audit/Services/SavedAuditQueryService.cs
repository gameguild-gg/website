using Microsoft.EntityFrameworkCore;

namespace GameGuild.Compliance.Audit;

/// <summary>Payload for creating or updating a saved audit query view.</summary>
public sealed record SaveSavedAuditQueryInput
{
    /// <summary>Display name (1-200 characters).</summary>
    public required string Name { get; init; }

    /// <summary>Optional description (max 1000 characters).</summary>
    public string? Description { get; init; }

    /// <summary>When true the view is shared with every caller holding audit-read access; when false only the owner sees it.</summary>
    public bool IsShared { get; init; }

    /// <summary>Structured query DSL JSON. Either this or <see cref="QueryText"/> must be provided.</summary>
    public string? QueryJson { get; init; }

    /// <summary>Light text syntax query (e.g. "actor:alice AND severity:Critical").</summary>
    public string? QueryText { get; init; }
}

/// <summary>A saved audit query view as returned by the API.</summary>
public sealed class SavedAuditQueryViewResponse
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public bool IsShared { get; set; }

    /// <summary>Canonical query DSL JSON — this is what execute-by-id runs.</summary>
    public string QueryJson { get; set; } = string.Empty;

    public Guid CreatedByUserId { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public int CurrentRevision { get; set; }

    public int ExecutionCount { get; set; }

    public DateTime? LastExecutedAt { get; set; }

    /// <summary>True when the acting user owns the view (and may update or delete it).</summary>
    public bool IsOwner { get; set; }
}

/// <summary>One revision snapshot of a saved view.</summary>
public sealed class SavedAuditQueryRevisionResponse
{
    public Guid Id { get; set; }

    public int Revision { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public string QueryJson { get; set; } = string.Empty;

    public bool IsShared { get; set; }

    public Guid ChangedByUserId { get; set; }

    public DateTime ChangedAt { get; set; }

    public bool IsCreation { get; set; }
}

/// <summary>
///     Repository for <see cref="SavedAuditQuery"/> aggregates and their
///     <see cref="SavedAuditQueryRevision"/> change history.
/// </summary>
public interface ISavedAuditQueryRepository
{
    Task<SavedAuditQuery?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<SavedAuditQueryRevision?> GetRevisionAsync(Guid id, CancellationToken cancellationToken);

    Task<IReadOnlyList<SavedAuditQuery>> ListVisibleAsync(Guid actingUserId, Guid? tenantId, CancellationToken cancellationToken);

    Task<IReadOnlyList<SavedAuditQueryRevision>> ListRevisionsAsync(Guid savedAuditQueryId, CancellationToken cancellationToken);

    Task<int> CountByNameAsync(Guid? tenantId, string name, Guid? excludeId, CancellationToken cancellationToken);

    Task AddAsync(SavedAuditQuery query, SavedAuditQueryRevision creationRevision, CancellationToken cancellationToken);

    void AddRevision(SavedAuditQueryRevision revision);

    void Remove(SavedAuditQuery query);

    Task SaveAsync(CancellationToken cancellationToken);
}

/// <inheritdoc />
public sealed class SavedAuditQueryRepository(IApplicationDbContext context) : ISavedAuditQueryRepository
{
    public Task<SavedAuditQuery?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        context.Set<SavedAuditQuery>()
            .FirstOrDefaultAsync(query => query.Id == id, cancellationToken);

    public Task<SavedAuditQueryRevision?> GetRevisionAsync(Guid id, CancellationToken cancellationToken) =>
        context.Set<SavedAuditQueryRevision>()
            .FirstOrDefaultAsync(revision => revision.Id == id, cancellationToken);

    public async Task<IReadOnlyList<SavedAuditQuery>> ListVisibleAsync(Guid actingUserId, Guid? tenantId, CancellationToken cancellationToken) =>
        await context.Set<SavedAuditQuery>()
            .AsNoTracking()
            .Where(query => query.CreatedByUserId == actingUserId || query.IsPublic)
            .Where(query => tenantId == null || query.TenantId == tenantId)
            .OrderBy(query => query.QueryName)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    public async Task<IReadOnlyList<SavedAuditQueryRevision>> ListRevisionsAsync(Guid savedAuditQueryId, CancellationToken cancellationToken) =>
        await context.Set<SavedAuditQueryRevision>()
            .AsNoTracking()
            .Where(revision => revision.SavedAuditQueryId == savedAuditQueryId)
            .OrderByDescending(revision => revision.Revision)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    public async Task<int> CountByNameAsync(Guid? tenantId, string name, Guid? excludeId, CancellationToken cancellationToken) =>
        await context.Set<SavedAuditQuery>()
            .AsNoTracking()
            .CountAsync(query => query.QueryName == name
                                  && (excludeId == null || query.Id != excludeId)
                                  && (tenantId == null || query.TenantId == tenantId), cancellationToken)
            .ConfigureAwait(false);

    public async Task AddAsync(SavedAuditQuery query, SavedAuditQueryRevision creationRevision, CancellationToken cancellationToken)
    {
        await context.Set<SavedAuditQuery>().AddAsync(query, cancellationToken).ConfigureAwait(false);
        await context.Set<SavedAuditQueryRevision>().AddAsync(creationRevision, cancellationToken).ConfigureAwait(false);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public void AddRevision(SavedAuditQueryRevision revision) =>
        context.Set<SavedAuditQueryRevision>().Add(revision);

    public void Remove(SavedAuditQuery query) =>
        context.Set<SavedAuditQuery>().Remove(query);

    public async Task SaveAsync(CancellationToken cancellationToken) =>
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
}

/// <summary>
///     Makes the saved-audit-query surface real: validated CRUD with revision history,
///     owner-or-shared access control, and execute-by-id backed by the audit query DSL executor.
/// </summary>
public interface ISavedAuditQueryService
{
    Task<Result<SavedAuditQueryViewResponse>> CreateAsync(SaveSavedAuditQueryInput input, Guid actingUserId, Guid? tenantId, CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyList<SavedAuditQueryViewResponse>>> ListAsync(Guid actingUserId, Guid? tenantId, CancellationToken cancellationToken = default);

    Task<Result<SavedAuditQueryViewResponse>> GetAsync(Guid id, Guid actingUserId, CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyList<SavedAuditQueryRevisionResponse>>> GetRevisionsAsync(Guid id, Guid actingUserId, CancellationToken cancellationToken = default);

    Task<Result<SavedAuditQueryViewResponse>> UpdateAsync(Guid id, SaveSavedAuditQueryInput input, Guid actingUserId, CancellationToken cancellationToken = default);

    Task<Result> DeleteAsync(Guid id, Guid actingUserId, CancellationToken cancellationToken = default);

    Task<Result<AuditQueryDslExecutionResult>> ExecuteAsync(Guid id, int skip, int take, Guid actingUserId, Guid? tenantScope, CancellationToken cancellationToken = default);
}

/// <inheritdoc />
public sealed class SavedAuditQueryService(
    ISavedAuditQueryRepository repository,
    IAuditQueryDslParser parser,
    IAuditQueryDslExecutor executor,
    IAuditService auditService) : ISavedAuditQueryService
{
    private const string ErrorCode = "SavedAuditQuery.Invalid";
    private const string NotFoundCode = "SavedAuditQuery.NotFound";
    private const string ForbiddenCode = "SavedAuditQuery.Forbidden";

    public async Task<Result<SavedAuditQueryViewResponse>> CreateAsync(
        SaveSavedAuditQueryInput input,
        Guid actingUserId,
        Guid? tenantId,
        CancellationToken cancellationToken = default)
    {
        var validated = await ValidateInputAsync(input, tenantId, actingUserId, existingId: null, cancellationToken).ConfigureAwait(false);
        if (validated.IsFailure)
        {
            return Result.Failure<SavedAuditQueryViewResponse>(validated.Error);
        }

        var query = SavedAuditQuery.Create(
            tenantId ?? Guid.Empty,
            input.Name.Trim(),
            validated.Value.CanonicalJson,
            actingUserId,
            input.IsShared,
            input.Description?.Trim());

        var creationRevision = SavedAuditQueryRevision.Capture(
            tenantId ?? Guid.Empty,
            query.Id,
            query.CurrentRevision,
            query.QueryName,
            query.Description,
            query.QueryDsl,
            query.IsPublic,
            actingUserId,
            isCreation: true);

        await repository.AddAsync(query, creationRevision, cancellationToken).ConfigureAwait(false);

        await auditService.LogAdminActionAsync(
            actingUserId,
            "SavedAuditQueryCreated",
            $"Saved audit query '{query.QueryName}' created (shared={query.IsPublic})",
            new { query.Id, query.QueryName, query.IsPublic, Revision = query.CurrentRevision }).ConfigureAwait(false);

        return Result.Success(ToResponse(query, actingUserId));
    }

    public async Task<Result<IReadOnlyList<SavedAuditQueryViewResponse>>> ListAsync(
        Guid actingUserId,
        Guid? tenantId,
        CancellationToken cancellationToken = default)
    {
        var queries = await repository.ListVisibleAsync(actingUserId, tenantId, cancellationToken).ConfigureAwait(false);
        return Result.Success<IReadOnlyList<SavedAuditQueryViewResponse>>(
            queries.Select(query => ToResponse(query, actingUserId)).ToList());
    }

    public async Task<Result<SavedAuditQueryViewResponse>> GetAsync(Guid id, Guid actingUserId, CancellationToken cancellationToken = default)
    {
        var query = await repository.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
        if (query is null)
        {
            return Result.Failure<SavedAuditQueryViewResponse>(Error.NotFound(NotFoundCode, $"Saved audit query '{id}' was not found."));
        }

        if (!CanRead(query, actingUserId))
        {
            return Result.Failure<SavedAuditQueryViewResponse>(Error.Forbidden(ForbiddenCode, "Saved audit query is private to its owner."));
        }

        return Result.Success(ToResponse(query, actingUserId));
    }

    public async Task<Result<IReadOnlyList<SavedAuditQueryRevisionResponse>>> GetRevisionsAsync(Guid id, Guid actingUserId, CancellationToken cancellationToken = default)
    {
        var query = await repository.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
        if (query is null)
        {
            return Result.Failure<IReadOnlyList<SavedAuditQueryRevisionResponse>>(Error.NotFound(NotFoundCode, $"Saved audit query '{id}' was not found."));
        }

        if (!CanRead(query, actingUserId))
        {
            return Result.Failure<IReadOnlyList<SavedAuditQueryRevisionResponse>>(Error.Forbidden(ForbiddenCode, "Saved audit query is private to its owner."));
        }

        var revisions = await repository.ListRevisionsAsync(id, cancellationToken).ConfigureAwait(false);
        return Result.Success<IReadOnlyList<SavedAuditQueryRevisionResponse>>(
            revisions.Select(ToRevisionResponse).ToList());
    }

    public async Task<Result<SavedAuditQueryViewResponse>> UpdateAsync(
        Guid id,
        SaveSavedAuditQueryInput input,
        Guid actingUserId,
        CancellationToken cancellationToken = default)
    {
        var query = await repository.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
        if (query is null)
        {
            return Result.Failure<SavedAuditQueryViewResponse>(Error.NotFound(NotFoundCode, $"Saved audit query '{id}' was not found."));
        }

        if (query.CreatedByUserId != actingUserId)
        {
            return Result.Failure<SavedAuditQueryViewResponse>(Error.Forbidden(ForbiddenCode, "Only the owner of a saved audit query may update it."));
        }

        var validated = await ValidateInputAsync(input, query.TenantId, actingUserId, existingId: id, cancellationToken).ConfigureAwait(false);
        if (validated.IsFailure)
        {
            return Result.Failure<SavedAuditQueryViewResponse>(validated.Error);
        }

        var newRevision = query.UpdateDetails(
            input.Name.Trim(),
            input.Description?.Trim(),
            validated.Value.CanonicalJson,
            input.IsShared);

        var revision = SavedAuditQueryRevision.Capture(
            query.TenantId ?? Guid.Empty,
            query.Id,
            newRevision,
            query.QueryName,
            query.Description,
            query.QueryDsl,
            query.IsPublic,
            actingUserId,
            isCreation: false);

        repository.AddRevision(revision);
        await repository.SaveAsync(cancellationToken).ConfigureAwait(false);

        await auditService.LogAdminActionAsync(
            actingUserId,
            "SavedAuditQueryUpdated",
            $"Saved audit query '{query.QueryName}' updated to revision {newRevision}",
            new { query.Id, query.QueryName, Revision = newRevision, query.IsPublic }).ConfigureAwait(false);

        return Result.Success(ToResponse(query, actingUserId));
    }

    public async Task<Result> DeleteAsync(Guid id, Guid actingUserId, CancellationToken cancellationToken = default)
    {
        var query = await repository.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
        if (query is null)
        {
            return Result.Failure(Error.NotFound(NotFoundCode, $"Saved audit query '{id}' was not found."));
        }

        if (query.CreatedByUserId != actingUserId)
        {
            return Result.Failure(Error.Forbidden(ForbiddenCode, "Only the owner of a saved audit query may delete it."));
        }

        // Revisions are retained: the change history of an investigation view outlives the view itself.
        repository.Remove(query);
        await repository.SaveAsync(cancellationToken).ConfigureAwait(false);

        await auditService.LogAdminActionAsync(
            actingUserId,
            "SavedAuditQueryDeleted",
            $"Saved audit query '{query.QueryName}' deleted",
            new { query.Id, query.QueryName }).ConfigureAwait(false);

        return Result.Success();
    }

    public async Task<Result<AuditQueryDslExecutionResult>> ExecuteAsync(
        Guid id,
        int skip,
        int take,
        Guid actingUserId,
        Guid? tenantScope,
        CancellationToken cancellationToken = default)
    {
        var query = await repository.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
        if (query is null)
        {
            return Result.Failure<AuditQueryDslExecutionResult>(Error.NotFound(NotFoundCode, $"Saved audit query '{id}' was not found."));
        }

        if (!CanRead(query, actingUserId))
        {
            return Result.Failure<AuditQueryDslExecutionResult>(Error.Forbidden(ForbiddenCode, "Saved audit query is private to its owner."));
        }

        // Fail closed: a stored view whose payload no longer parses never executes.
        var parsed = parser.ParseJson(query.QueryDsl);
        if (parsed.IsFailure)
        {
            return Result.Failure<AuditQueryDslExecutionResult>(Error.Failure(
                ErrorCode,
                $"Saved audit query '{query.QueryName}' contains an invalid query: {parsed.Error.Description}"));
        }

        var execution = await executor.ExecuteAsync(
            new AuditQueryDslExecutionRequest
            {
                Query = parsed.Value,
                Skip = skip,
                Take = take,
                ActingUserId = actingUserId,
                TenantScope = tenantScope
            },
            cancellationToken).ConfigureAwait(false);
        if (execution.IsFailure)
        {
            return execution;
        }

        query.RecordExecution(actingUserId);
        await repository.SaveAsync(cancellationToken).ConfigureAwait(false);

        await auditService.LogAdminActionAsync(
            actingUserId,
            "SavedAuditQueryExecuted",
            $"Saved audit query '{query.QueryName}' executed: {execution.Value.TotalCount} matching events",
            new { query.Id, query.QueryName, execution.Value.TotalCount, execution.Value.RowsScanned, execution.Value.DurationMs }).ConfigureAwait(false);

        return execution;
    }

    private async Task<Result<(string CanonicalJson, AuditQueryDslQuery Query)>> ValidateInputAsync(
        SaveSavedAuditQueryInput input,
        Guid? tenantId,
        Guid actingUserId,
        Guid? existingId,
        CancellationToken cancellationToken)
    {
        if (input.Name is null || input.Name.Trim().Length is < 1 or > 200)
        {
            return Result.Failure<(string, AuditQueryDslQuery)>(Error.Validation(ErrorCode, "Name must be between 1 and 200 characters."));
        }

        if (input.Description?.Length > 1000)
        {
            return Result.Failure<(string, AuditQueryDslQuery)>(Error.Validation(ErrorCode, "Description must be at most 1000 characters."));
        }

        if (string.IsNullOrWhiteSpace(input.QueryJson) && string.IsNullOrWhiteSpace(input.QueryText))
        {
            return Result.Failure<(string, AuditQueryDslQuery)>(Error.Validation(ErrorCode, "Either queryJson (structured DSL) or queryText (light syntax) is required."));
        }

        Result<AuditQueryDslQuery> parsed = string.IsNullOrWhiteSpace(input.QueryJson)
            ? parser.ParseText(input.QueryText!)
            : parser.ParseJson(input.QueryJson!);
        if (parsed.IsFailure)
        {
            return Result.Failure<(string, AuditQueryDslQuery)>(parsed.Error);
        }

        // Cross-tenant scope check applies to saved content too: a shared view may never embed a
        // tenant condition pointing outside the saving user's tenant.
        var tenantScopeError = AuditQueryDslExecutor.ValidateTenantScope(parsed.Value.Filter, tenantId);
        if (tenantScopeError is not null)
        {
            return Result.Failure<(string, AuditQueryDslQuery)>(tenantScopeError);
        }

        var duplicateCount = await repository.CountByNameAsync(tenantId, input.Name.Trim(), existingId, cancellationToken).ConfigureAwait(false);
        if (duplicateCount > 0)
        {
            return Result.Failure<(string, AuditQueryDslQuery)>(Error.Conflict(ErrorCode, $"A saved audit query named '{input.Name.Trim()}' already exists for this tenant."));
        }

        return Result.Success((AuditQueryDslJson.Serialize(parsed.Value), parsed.Value));
    }

    private static bool CanRead(SavedAuditQuery query, Guid actingUserId) =>
        query.CreatedByUserId == actingUserId || query.IsPublic;

    private static SavedAuditQueryViewResponse ToResponse(SavedAuditQuery query, Guid actingUserId) => new()
    {
        Id = query.Id,
        Name = query.QueryName,
        Description = query.Description,
        IsShared = query.IsPublic,
        QueryJson = query.QueryDsl,
        CreatedByUserId = query.CreatedByUserId,
        CreatedAt = query.CreatedAt,
        UpdatedAt = query.UpdatedAt,
        CurrentRevision = query.CurrentRevision,
        ExecutionCount = query.ExecutionCount,
        LastExecutedAt = query.LastExecutedAt,
        IsOwner = query.CreatedByUserId == actingUserId
    };

    private static SavedAuditQueryRevisionResponse ToRevisionResponse(SavedAuditQueryRevision revision) => new()
    {
        Id = revision.Id,
        Revision = revision.Revision,
        Name = revision.QueryName,
        Description = revision.Description,
        QueryJson = revision.QueryDsl,
        IsShared = revision.IsPublic,
        ChangedByUserId = revision.ChangedByUserId,
        ChangedAt = revision.CreatedAt,
        IsCreation = revision.IsCreation
    };
}
