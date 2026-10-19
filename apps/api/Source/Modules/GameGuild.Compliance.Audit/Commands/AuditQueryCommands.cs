using GameGuild.CQRS;

namespace GameGuild.Compliance.Audit;

/// <summary>
///     Executes an audit query DSL expression (JSON or light text syntax) against the audit
///     stores. Mutates only the audit trail (a query-execution record with user attribution).
/// </summary>
public sealed record ExecuteAuditQueryCommand(
    Guid AdminUserId,
    Guid? TenantScope,
    string? QueryJson,
    string? QueryText,
    int Skip,
    int Take,
    int? TimeoutSeconds) : ICommand<Result<AuditQueryDslExecutionResult>>;

/// <summary>Validates a DSL query and returns its canonical form. Persists nothing.</summary>
public sealed record ValidateAuditQueryCommand(
    string? QueryJson,
    string? QueryText) : ICommand<AuditQueryValidationResponse>;

/// <summary>Creates a saved audit query view with a creation revision snapshot.</summary>
public sealed record CreateSavedAuditQueryCommand(
    Guid ActingUserId,
    Guid? TenantId,
    SaveSavedAuditQueryInput Input) : ICommand<Result<SavedAuditQueryViewResponse>>;

/// <summary>Updates a saved audit query view (owner only) and records a new revision.</summary>
public sealed record UpdateSavedAuditQueryCommand(
    Guid Id,
    Guid ActingUserId,
    SaveSavedAuditQueryInput Input) : ICommand<Result<SavedAuditQueryViewResponse>>;

/// <summary>Deletes a saved audit query view (owner only); revision history is retained.</summary>
public sealed record DeleteSavedAuditQueryCommand(
    Guid Id,
    Guid ActingUserId) : ICommand<Result>;

/// <summary>Executes a saved audit query view by identifier (owner or shared).</summary>
public sealed record ExecuteSavedAuditQueryCommand(
    Guid Id,
    Guid ActingUserId,
    Guid? TenantScope,
    int Skip,
    int Take) : ICommand<Result<AuditQueryDslExecutionResult>>;

/// <summary>Handlers binding the audit query DSL surface to its services.</summary>
public sealed class AuditQueryCommandHandler(
    IAuditQueryDslParser parser,
    IAuditQueryDslExecutor executor,
    ISavedAuditQueryService savedQueryService,
    IAuditService auditService) :
    ICommandHandler<ExecuteAuditQueryCommand, Result<AuditQueryDslExecutionResult>>,
    ICommandHandler<ValidateAuditQueryCommand, AuditQueryValidationResponse>,
    ICommandHandler<CreateSavedAuditQueryCommand, Result<SavedAuditQueryViewResponse>>,
    ICommandHandler<UpdateSavedAuditQueryCommand, Result<SavedAuditQueryViewResponse>>,
    ICommandHandler<DeleteSavedAuditQueryCommand, Result>,
    ICommandHandler<ExecuteSavedAuditQueryCommand, Result<AuditQueryDslExecutionResult>>
{
    public async Task<Result<AuditQueryDslExecutionResult>> Handle(
        ExecuteAuditQueryCommand command,
        CancellationToken cancellationToken)
    {
        var parsed = Parse(command.QueryJson, command.QueryText);
        if (parsed.IsFailure)
        {
            return Result.Failure<AuditQueryDslExecutionResult>(parsed.Error);
        }

        var execution = await executor.ExecuteAsync(
            new AuditQueryDslExecutionRequest
            {
                Query = parsed.Value,
                Skip = command.Skip,
                Take = command.Take,
                TimeoutSeconds = command.TimeoutSeconds,
                ActingUserId = command.AdminUserId,
                TenantScope = command.TenantScope
            },
            cancellationToken).ConfigureAwait(false);

        if (execution.IsSuccess)
        {
            await auditService.LogAdminActionAsync(
                command.AdminUserId,
                "AuditQueryExecuted",
                $"Audit query executed: {execution.Value.TotalCount} matching events from {execution.Value.RowsScanned} scanned rows",
                new
                {
                    execution.Value.TotalCount,
                    execution.Value.RowsScanned,
                    execution.Value.DurationMs,
                    execution.Value.WindowStartUtc,
                    execution.Value.WindowEndUtc,
                    QueryJson = AuditQueryDslJson.Serialize(parsed.Value)
                }).ConfigureAwait(false);
        }

        return execution;
    }

    public Task<AuditQueryValidationResponse> Handle(
        ValidateAuditQueryCommand command,
        CancellationToken cancellationToken)
    {
        var parsed = Parse(command.QueryJson, command.QueryText);
        if (parsed.IsFailure)
        {
            return Task.FromResult(new AuditQueryValidationResponse
            {
                IsValid = false,
                CanonicalJson = string.Empty,
                Fields = [],
                Error = parsed.Error.Description
            });
        }

        return Task.FromResult(new AuditQueryValidationResponse
        {
            IsValid = true,
            CanonicalJson = AuditQueryDslJson.Serialize(parsed.Value),
            Fields = AuditQueryCommandHandler.CollectFields(parsed.Value.Filter),
            Error = null
        });
    }

    public Task<Result<SavedAuditQueryViewResponse>> Handle(
        CreateSavedAuditQueryCommand command,
        CancellationToken cancellationToken) =>
        savedQueryService.CreateAsync(command.Input, command.ActingUserId, command.TenantId, cancellationToken);

    public Task<Result<SavedAuditQueryViewResponse>> Handle(
        UpdateSavedAuditQueryCommand command,
        CancellationToken cancellationToken) =>
        savedQueryService.UpdateAsync(command.Id, command.Input, command.ActingUserId, cancellationToken);

    public Task<Result> Handle(DeleteSavedAuditQueryCommand command, CancellationToken cancellationToken) =>
        savedQueryService.DeleteAsync(command.Id, command.ActingUserId, cancellationToken);

    public Task<Result<AuditQueryDslExecutionResult>> Handle(
        ExecuteSavedAuditQueryCommand command,
        CancellationToken cancellationToken) =>
        savedQueryService.ExecuteAsync(command.Id, command.Skip, command.Take, command.ActingUserId, command.TenantScope, cancellationToken);

    private Result<AuditQueryDslQuery> Parse(string? queryJson, string? queryText)
    {
        if (string.IsNullOrWhiteSpace(queryJson) && string.IsNullOrWhiteSpace(queryText))
        {
            return Result.Failure<AuditQueryDslQuery>(Error.Validation(
                "AuditQueryDsl.Invalid",
                "Provide either queryJson (structured DSL) or queryText (light syntax, e.g. 'actor:alice AND action:granted AFTER 2026-01-01')."));
        }

        return string.IsNullOrWhiteSpace(queryJson)
            ? parser.ParseText(queryText!)
            : parser.ParseJson(queryJson!);
    }

    internal static List<string> CollectFields(AuditQueryNode node)
    {
        var fields = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        CollectFieldsInto(node, fields);
        return fields.ToList();
    }

    private static void CollectFieldsInto(AuditQueryNode node, SortedSet<string> fields)
    {
        switch (node)
        {
            case AuditQueryGroup group:
                foreach (var child in group.Children)
                {
                    CollectFieldsInto(child, fields);
                }

                break;

            case AuditQueryCondition condition:
                fields.Add(AuditQueryFieldRegistry.Get(condition.Field).Names.First());
                break;

            default:
                // Only groups and conditions contribute field names.
                break;
        }
    }
}
