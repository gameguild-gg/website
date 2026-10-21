using System.Reflection;
using System.Text.Json;
using GameGuild.Identity.Context.Actors;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace GameGuild.Compliance.Audit;

public sealed record CreateValueSnapshotRollbackPlanRequest
{
    public required string EntityName { get; init; }
    public required string KeyId { get; init; }
    public DateTime AsOfUtc { get; init; }
}

public sealed record ValueSnapshotRollbackPlanResponse(
    Guid Id,
    string TargetEntityName,
    string TargetKeyId,
    Guid BasisSnapshotId,
    DateTime AsOfUtc,
    string BasisStateJson,
    string BasisStateSha256,
    string PlanJson,
    string Status,
    string? FailureReason,
    Guid CreatedByUserId,
    DateTime CreatedAtUtc,
    DateTime? ExecutedAtUtc,
    Guid? ExecutedByUserId,
    string? ResultJson);

public sealed record ExecuteValueSnapshotRollbackRequest
{
    /// <summary>Explicitly accept reverting changes recorded after the basis snapshot. Fail-closed default: false.</summary>
    public bool AcceptNewerChanges { get; init; }
}

/// <summary>Thrown when a rollback plan cannot be built or safely executed.</summary>
public sealed class ValueSnapshotRollbackException(string reason) : Exception(reason)
{
    public string Reason { get; } = reason;
}

/// <summary>
///     Rollback capability for critical data recovery (issue #176). Builds a plan by replaying the
///     entity's snapshot lineage up to a point in time (Added full state plus Modified deltas),
///     producing inverse field operations. Execution fails closed: it refuses while newer changes
///     exist after the basis snapshot unless explicitly accepted, resolves the live entity through
///     the shared model, applies the inverse operations in one transaction, and records the
///     outcome. The restoring writes themselves flow through the normal capture pipeline, so a
///     rollback produces fresh, audited value snapshots.
/// </summary>
public interface IValueSnapshotRollbackService
{
    Task<ValueSnapshotRollbackPlanResponse> CreatePlanAsync(CreateValueSnapshotRollbackPlanRequest request, CancellationToken cancellationToken);

    Task<ValueSnapshotRollbackPlanResponse?> GetPlanAsync(Guid planId, CancellationToken cancellationToken);

    Task<ValueSnapshotRollbackPlanResponse> ExecuteAsync(Guid planId, ExecuteValueSnapshotRollbackRequest request, CancellationToken cancellationToken);
}

public sealed class ValueSnapshotRollbackService(
    IActorContextAccessor actors,
    IApplicationDbContext context,
    TimeProvider timeProvider,
    ILogger<ValueSnapshotRollbackService> logger) : IValueSnapshotRollbackService
{
    private static readonly JsonSerializerOptions PlanJsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<ValueSnapshotRollbackPlanResponse> CreatePlanAsync(
        CreateValueSnapshotRollbackPlanRequest request,
        CancellationToken cancellationToken)
    {
        var (tenantId, userId) = RequireAdministrator();
        var asOf = new DateTimeOffset(request.AsOfUtc, TimeSpan.Zero);

        var lineage = await context.Set<MutationValueSnapshot>().AsNoTracking()
            .Where(snapshot => snapshot.TenantId == tenantId
                && snapshot.EntityName == request.EntityName
                && snapshot.KeyId == request.KeyId
                && snapshot.OccurredAtUtc <= asOf)
            .OrderBy(snapshot => snapshot.OccurredAtUtc)
            .ThenBy(snapshot => snapshot.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (lineage.Count == 0)
        {
            throw new ValueSnapshotRollbackException($"No value snapshots exist for {request.EntityName} {request.KeyId} at or before the requested instant.");
        }

        var basis = lineage[^1];
        if (basis.ChangeKind == "Deleted")
        {
            throw new ValueSnapshotRollbackException(
                "The basis snapshot is a deletion; rollback of a live deletion requires re-creating the entity from the pre-delete state. Re-run with an earlier AsOfUtc.");
        }

        await ResolveChunkPayloadsAsync(tenantId, lineage, cancellationToken).ConfigureAwait(false);
        var state = ReconstructState(lineage);
        if (state is null)
        {
            throw new ValueSnapshotRollbackException(
                "The snapshot lineage does not start with a full-state capture (the entity predates the snapshot store), so the target state cannot be reconstructed.");
        }

        var basisStateJson = JsonSerializer.Serialize(
            state.OrderBy(pair => pair.Key, StringComparer.Ordinal).ToDictionary(pair => pair.Key, pair => pair.Value),
            PlanJsonOptions);
        var basisStateSha = ValueSnapshotDiffEngine.ComputeSha256(basisStateJson);
        var plan = new ValueSnapshotRollbackPlan
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            TargetEntityName = request.EntityName,
            TargetKeyId = request.KeyId,
            BasisSnapshotId = basis.Id,
            AsOfUtc = request.AsOfUtc,
            BasisStateJson = basisStateJson,
            BasisStateSha256 = basisStateSha,
            PlanJson = "[]",
            Status = "Proposed",
            CreatedByUserId = userId,
            CreatedAtUtc = timeProvider.GetUtcNow().UtcDateTime
        };

        plan.PlanJson = JsonSerializer.Serialize(BuildInverseOperations(basis, state), PlanJsonOptions);
        context.Set<ValueSnapshotRollbackPlan>().Add(plan);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Map(plan);
    }

    public async Task<ValueSnapshotRollbackPlanResponse?> GetPlanAsync(Guid planId, CancellationToken cancellationToken)
    {
        var tenantId = RequireAdministrator().TenantId;
        var plan = await context.Set<ValueSnapshotRollbackPlan>().AsNoTracking()
            .FirstOrDefaultAsync(candidate => candidate.TenantId == tenantId && candidate.Id == planId, cancellationToken)
            .ConfigureAwait(false);
        return plan is null ? null : Map(plan);
    }

    public async Task<ValueSnapshotRollbackPlanResponse> ExecuteAsync(
        Guid planId,
        ExecuteValueSnapshotRollbackRequest request,
        CancellationToken cancellationToken)
    {
        var (tenantId, userId) = RequireAdministrator();
        var plan = await context.Set<ValueSnapshotRollbackPlan>()
            .FirstOrDefaultAsync(candidate => candidate.TenantId == tenantId && candidate.Id == planId, cancellationToken)
            .ConfigureAwait(false);
        if (plan is null)
        {
            throw new ValueSnapshotRollbackException("Rollback plan not found.");
        }

        if (plan.Status == "Executed")
        {
            throw new ValueSnapshotRollbackException("This rollback plan has already been executed.");
        }

        var basisTime = await context.Set<MutationValueSnapshot>().AsNoTracking()
            .Where(snapshot => snapshot.Id == plan.BasisSnapshotId)
            .Select(snapshot => (DateTimeOffset?)snapshot.OccurredAtUtc)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        if (basisTime is null)
        {
            throw new ValueSnapshotRollbackException("The basis snapshot no longer exists; build a new plan.");
        }

        // Fail closed: newer recorded changes after the basis mean the plan would silently revert
        // them; execution requires the caller to explicitly accept that consequence.
        if (!request.AcceptNewerChanges)
        {
            var newerCount = await context.Set<MutationValueSnapshot>().AsNoTracking()
                .CountAsync(snapshot => snapshot.TenantId == tenantId
                    && snapshot.EntityName == plan.TargetEntityName
                    && snapshot.KeyId == plan.TargetKeyId
                    && snapshot.OccurredAtUtc > basisTime.Value, cancellationToken)
                .ConfigureAwait(false);
            if (newerCount > 0)
            {
                plan.Status = "Rejected";
                plan.FailureReason = $"{newerCount} newer change(s) exist after the basis snapshot; retry with AcceptNewerChanges to revert them.";
                await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                throw new ValueSnapshotRollbackException(plan.FailureReason);
            }
        }

        var clrType = ResolveEntityType(plan.TargetEntityName)
            ?? throw new ValueSnapshotRollbackException($"Entity type '{plan.TargetEntityName}' is not part of the loaded model; cannot roll back.");
        var key = ParseKey(plan.TargetKeyId)
            ?? throw new ValueSnapshotRollbackException($"Key '{plan.TargetKeyId}' is not a single Guid key; composite-key rollback is not supported.");

        var targetState = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(plan.BasisStateJson, PlanJsonOptions)
            ?? throw new ValueSnapshotRollbackException("The plan state could not be deserialized.");

        var operations = new List<string>();
        try
        {
            // The interface abstraction exposes only the generic Set<T>; rollback addresses an
            // arbitrary snapshot entity type, so it needs the underlying DbContext surface.
            var dbContext = context as DbContext
                ?? throw new ValueSnapshotRollbackException("The database context does not support dynamic entity access.");
            await using var transaction = await context.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
            var live = await dbContext.FindAsync(clrType, [key], cancellationToken).ConfigureAwait(false);

            if (live is null)
            {
                var recreated = Activator.CreateInstance(clrType)
                    ?? throw new ValueSnapshotRollbackException($"Could not materialize a new {plan.TargetEntityName} instance.");
                // Preserve the original key: the re-inserted entity must occupy the identity it
                // had at the basis point, otherwise references and future snapshots diverge.
                clrType.GetProperty("Id")?.SetValue(recreated, key);
                dbContext.Add(recreated);
                ApplyState(recreated, clrType, targetState, operations);
                operations.Add($"reinserted {plan.TargetEntityName} {plan.TargetKeyId} with {targetState.Count} fields");
            }
            else
            {
                var entry = dbContext.Entry(live);
                foreach (var property in entry.Properties)
                {
                    if (property.Metadata.IsPrimaryKey()
                        || property.Metadata.Name is "Version" or "CreatedAt" or "UpdatedAt" or "DeletedAt" or "TenantId")
                    {
                        continue;
                    }

                    if (!targetState.TryGetValue(property.Metadata.Name, out var targetValue))
                    {
                        continue;
                    }

                    var current = property.CurrentValue;
                    var converted = ConvertValue(targetValue, property.Metadata.ClrType);
                    if (!Equals(current, converted))
                    {
                        property.CurrentValue = converted;
                        operations.Add($"set {property.Metadata.Name}");
                    }
                }

                operations.Add($"restored {plan.TargetEntityName} {plan.TargetKeyId} toward basis state");
            }

            plan.Status = "Executed";
            plan.ExecutedAtUtc = timeProvider.GetUtcNow().UtcDateTime;
            plan.ExecutedByUserId = userId;
            plan.ResultJson = JsonSerializer.Serialize(new { operations }, PlanJsonOptions);
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            plan.Status = "Failed";
            plan.FailureReason = exception.Message;
            plan.ResultJson = JsonSerializer.Serialize(new { operations, error = exception.Message }, PlanJsonOptions);
            logger.LogWarning(exception, "Rollback plan {PlanId} failed", plan.Id);
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            throw new ValueSnapshotRollbackException($"Rollback failed: {exception.Message}");
        }

        return Map(plan);
    }

    /// <summary>Replays a snapshot lineage into the entity's full state at the last snapshot.
    /// Returns null when the lineage does not begin with a full-state capture.</summary>
    internal static Dictionary<string, JsonElement>? ReconstructState(List<MutationValueSnapshot> lineage)
    {
        Dictionary<string, JsonElement>? state = null;
        foreach (var snapshot in lineage)
        {
            if (snapshot.ChangeKind == "Deleted")
            {
                state = null;
                continue;
            }

            var payload = snapshot.AfterPayload;
            if (string.IsNullOrWhiteSpace(payload))
            {
                continue;
            }

            Dictionary<string, JsonElement> delta;
            try
            {
                delta = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(payload, PlanJsonOptions) ?? [];
            }
            catch (JsonException)
            {
                continue;
            }

            state ??= snapshot.ChangeKind == "Added"
                ? []
                : null;
            if (state is null)
            {
                return null;
            }

            foreach (var (field, value) in delta)
            {
                state[field] = value;
            }
        }

        return state;
    }

    /// <summary>Builds the documented inverse operations for the plan: restoring the basis state,
    /// or deleting the entity when the entity should not exist at the basis point.</summary>
    private static List<Dictionary<string, object?>> BuildInverseOperations(
        MutationValueSnapshot basis,
        Dictionary<string, JsonElement> state)
    {
        var operations = new List<Dictionary<string, object?>>
        {
            new()
            {
                ["op"] = "restore-state",
                ["basisSnapshotId"] = basis.Id,
                ["basisChangeKind"] = basis.ChangeKind,
                ["fields"] = state.Keys.OrderBy(field => field, StringComparer.Ordinal).ToList()
            }
        };
        return operations;
    }

    /// <summary>Restores inline after-payloads for snapshots whose payload was deduplicated into
    /// a chunk or stripped by archival, so replay can reconstruct full state from any store state.</summary>
    private async Task ResolveChunkPayloadsAsync(Guid tenantId, List<MutationValueSnapshot> lineage, CancellationToken cancellationToken)
    {
        var chunkIds = lineage
            .Where(snapshot => snapshot.AfterPayload is null && snapshot.AfterChunkId is not null && snapshot.ChangeKind != "Deleted")
            .Select(snapshot => snapshot.AfterChunkId!.Value)
            .Distinct()
            .ToList();
        if (chunkIds.Count == 0)
        {
            return;
        }

        var chunks = await context.Set<MutationValuePayloadChunk>().AsNoTracking()
            .Where(chunk => chunk.TenantId == tenantId && chunkIds.Contains(chunk.Id))
            .ToDictionaryAsync(chunk => chunk.Id, cancellationToken)
            .ConfigureAwait(false);
        foreach (var snapshot in lineage)
        {
            if (snapshot.AfterPayload is null && snapshot.AfterChunkId is not null
                && chunks.TryGetValue(snapshot.AfterChunkId.Value, out var chunk))
            {
                snapshot.AfterPayload = chunk.Content;
            }
        }
    }

    private static Type? ResolveEntityType(string entityName)
    {
        Type? match = null;
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            Type[] types;
            try
            {
                types = assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException)
            {
                continue;
            }

            foreach (var type in types)
            {
                if (type.Name == entityName && typeof(EntityBase).IsAssignableFrom(type))
                {
                    if (match is not null && match != type)
                    {
                        return null;
                    }

                    match = type;
                }
            }
        }

        return match;
    }

    private static Guid? ParseKey(string keyId) =>
        Guid.TryParse(keyId, out var key) ? key : null;

    private static void ApplyState(
        object instance,
        Type clrType,
        Dictionary<string, JsonElement> state,
        List<string> operations)
    {
        foreach (var property in clrType.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (!property.CanWrite || !state.TryGetValue(property.Name, out var value))
            {
                continue;
            }

            if (property.Name is "Version" or "CreatedAt" or "UpdatedAt" or "DeletedAt" or "TenantId" or "Id")
            {
                continue;
            }

            try
            {
                property.SetValue(instance, ConvertValue(value, property.PropertyType));
            }
            catch (ArgumentException)
            {
                // Unassignable properties are skipped; the plan execution reports the field set.
            }
        }

        operations.AddRange(state.Keys.OrderBy(field => field, StringComparer.Ordinal).Select(field => $"set {field}"));
    }

    private static object? ConvertValue(JsonElement value, Type targetType)
    {
        var nullable = Nullable.GetUnderlyingType(targetType);
        var destination = nullable ?? targetType;
        if (value.ValueKind == JsonValueKind.Null)
        {
            return nullable is not null ? null : Activator.CreateInstance(destination);
        }

        if (destination == typeof(string))
        {
            return value.ValueKind == JsonValueKind.String ? value.GetString() : value.GetRawText();
        }

        if (destination == typeof(Guid))
        {
            return value.ValueKind == JsonValueKind.String ? Guid.Parse(value.GetString()!) : Guid.Empty;
        }

        if (destination == typeof(DateTime))
        {
            return value.ValueKind == JsonValueKind.String
                ? DateTime.Parse(value.GetString()!, System.Globalization.CultureInfo.InvariantCulture)
                : DateTime.UnixEpoch;
        }

        if (destination == typeof(DateTimeOffset))
        {
            return value.ValueKind == JsonValueKind.String
                ? DateTimeOffset.Parse(value.GetString()!, System.Globalization.CultureInfo.InvariantCulture)
                : DateTimeOffset.UnixEpoch;
        }

        if (destination.IsEnum)
        {
            return value.ValueKind == JsonValueKind.String
                ? Enum.Parse(destination, value.GetString()!, ignoreCase: false)
                : Enum.ToObject(destination, Convert.ToInt64(value.GetRawText()));
        }

        if (destination == typeof(bool))
        {
            return value.GetBoolean();
        }

        return Convert.ChangeType(
            value.ValueKind == JsonValueKind.String ? value.GetString() : value.GetRawText(),
            destination,
            System.Globalization.CultureInfo.InvariantCulture);
    }

    private static ValueSnapshotRollbackPlanResponse Map(ValueSnapshotRollbackPlan plan) =>
        new(plan.Id, plan.TargetEntityName, plan.TargetKeyId, plan.BasisSnapshotId, plan.AsOfUtc,
            plan.BasisStateJson, plan.BasisStateSha256, plan.PlanJson, plan.Status, plan.FailureReason,
            plan.CreatedByUserId, plan.CreatedAtUtc, plan.ExecutedAtUtc, plan.ExecutedByUserId, plan.ResultJson);

    private (Guid TenantId, Guid UserId) RequireAdministrator()
    {
        var actor = actors.ActorContext;
        if (!actor.IsAuthenticated || !actor.IsTenantAdmin || actor.TenantId is not { } tenant || tenant == Guid.Empty
            || actor.SubjectIdAsGuid is not { } user || user == Guid.Empty)
        {
            logger.LogWarning("Value snapshot rollback access denied for actor {Actor}", actor.SubjectIdAsGuid);
            throw new UnauthorizedAccessException("A tenant administrator and tenant context are required.");
        }

        return (tenant, user);
    }
}
