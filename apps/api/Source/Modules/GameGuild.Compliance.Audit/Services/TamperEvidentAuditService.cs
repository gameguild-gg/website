using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace GameGuild.Compliance.Audit;

/// <summary>
/// Persists signed, per-tenant hash chains for administrative and other high-risk audit events.
/// Callers are responsible for minimizing and redacting snapshots before they are submitted.
/// </summary>
public sealed class TamperEvidentAuditService(
    IServiceScopeFactory scopeFactory,
    ICryptographicSigningService signingService,
    ILogger<TamperEvidentAuditService> logger) : ITamperEvidentAuditService
{
    private const int MaximumPageSize = 500;

    public Task<Result<TamperEvidentAuditLog>> CreateAuditLogAsync(
        Guid tenantId,
        Guid? userId,
        string action,
        string entityType,
        Guid? entityId,
        string? beforeSnapshot,
        string? afterSnapshot,
        string changes,
        string riskLevel,
        string ipAddress,
        string userAgent)
        => CreateAuditLogAsync(
            tenantId, userId, action, entityType, entityId, beforeSnapshot, afterSnapshot,
            changes, riskLevel, ipAddress, userAgent, new AuditEventMetadata(), CancellationToken.None);

    public Task<Result<TamperEvidentAuditLog>> CreateAuditLogAsync(
        Guid tenantId,
        Guid? userId,
        string action,
        string entityType,
        Guid? entityId,
        string? beforeSnapshot,
        string? afterSnapshot,
        string changes,
        string riskLevel,
        string ipAddress,
        string userAgent,
        CancellationToken cancellationToken)
        => CreateAuditLogAsync(
            tenantId, userId, action, entityType, entityId, beforeSnapshot, afterSnapshot,
            changes, riskLevel, ipAddress, userAgent, new AuditEventMetadata(), cancellationToken);

    public async Task<Result<TamperEvidentAuditLog>> CreateAuditLogAsync(
        Guid tenantId,
        Guid? userId,
        string action,
        string entityType,
        Guid? entityId,
        string? beforeSnapshot,
        string? afterSnapshot,
        string changes,
        string riskLevel,
        string ipAddress,
        string userAgent,
        AuditEventMetadata metadata,
        CancellationToken cancellationToken)
    {
        if (tenantId == Guid.Empty)
        {
            return Failure<TamperEvidentAuditLog>("Audit.InvalidTenant", "A non-empty tenant ID is required.");
        }

        if (string.IsNullOrWhiteSpace(action) || string.IsNullOrWhiteSpace(entityType))
        {
            return Failure<TamperEvidentAuditLog>("Audit.InvalidAction", "An action and entity type are required.");
        }

        if (changes is null || riskLevel is null || ipAddress is null || userAgent is null || metadata is null)
        {
            return Failure<TamperEvidentAuditLog>("Audit.InvalidPayload", "Audit event fields cannot be null.");
        }

        if (action.Length > 100
            || entityType.Length > 100
            || riskLevel.Length > 50
            || ipAddress.Length > 45
            || userAgent.Length > 500
            || metadata.CorrelationId?.Length > 100
            || metadata.Country?.Length > 100
            || metadata.Region?.Length > 100
            || metadata.City?.Length > 100)
        {
            return Failure<TamperEvidentAuditLog>("Audit.PayloadTooLarge", "One or more audit event fields exceed the supported length.");
        }

        try
        {
            for (var attempt = 1; attempt <= 3; attempt++)
            {
                try
                {
                    await using var scope = scopeFactory.CreateAsyncScope();
                    var context = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
                    await using var transaction = await context.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

                    var previous = await context.Set<TamperEvidentAuditLog>()
                        .AsNoTracking()
                        .Where(entry => entry.TenantId == tenantId)
                        .OrderByDescending(entry => entry.SequenceNumber)
                        .FirstOrDefaultAsync(cancellationToken)
                        .ConfigureAwait(false);

                    var sequenceNumber = (previous?.SequenceNumber ?? 0) + 1;
                    var previousHash = previous?.ChainHash ?? string.Empty;
                    var entry = TamperEvidentAuditLog.Create(
                        tenantId,
                        userId,
                        action,
                        entityType,
                        entityId,
                        beforeSnapshot,
                        afterSnapshot,
                        changes,
                        riskLevel,
                        ipAddress,
                        userAgent,
                        metadata.Country,
                        metadata.Region,
                        metadata.City,
                        previousHash,
                        sequenceNumber,
                        metadata.SessionId,
                        metadata.CorrelationId);

                    var contentHash = signingService.ComputeContentHash(SerializeContent(entry));
                    var chainHash = signingService.ComputeChainHash(contentHash, previousHash, sequenceNumber);
                    var keyId = signingService.GetActiveKeyId();
                    var signature = signingService.SignData(chainHash, keyId);

                    entry.SetCryptographicHashes(contentHash, chainHash);
                    entry.Sign(signature, keyId);

                    context.Set<TamperEvidentAuditLog>().Add(entry);
                    await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                    await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);

                    return Result.Success(entry);
                }
                catch (DbUpdateException exception) when (attempt < 3)
                {
                    logger.LogWarning(
                        exception,
                        "Audit chain write conflict for tenant {TenantId}; retrying attempt {Attempt}",
                        tenantId,
                        attempt + 1);
                    await Task.Delay(TimeSpan.FromMilliseconds(attempt * 25), cancellationToken).ConfigureAwait(false);
                }
            }

            throw new InvalidOperationException("Audit chain write retries were exhausted.");
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogError(exception, "Failed to persist tamper-evident audit event {Action} for tenant {TenantId}", action, tenantId);
            return Failure<TamperEvidentAuditLog>("Audit.PersistenceFailed", "The tamper-evident audit event could not be persisted.");
        }
    }

    public Task<Result<bool>> VerifyChainIntegrityAsync(Guid tenantId)
        => VerifyChainIntegrityAsync(tenantId, CancellationToken.None);

    public async Task<Result<bool>> VerifyChainIntegrityAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        if (tenantId == Guid.Empty)
        {
            return Result.Failure<bool>(Error.Validation("Audit.InvalidTenant", "A non-empty tenant ID is required."));
        }

        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var context = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
            var entries = await context.Set<TamperEvidentAuditLog>()
                .AsNoTracking()
                .Where(entry => entry.TenantId == tenantId)
                .OrderBy(entry => entry.SequenceNumber)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            var expectedSequence = 1L;
            var previousHash = string.Empty;

            foreach (var entry in entries)
            {
                if (entry.SequenceNumber != expectedSequence || entry.PreviousHash != previousHash)
                {
                    return Result.Success(false);
                }

                if (!new AuditChainEvidenceVerifier(signingService).VerifyEntry(entry))
                {
                    return Result.Success(false);
                }

                previousHash = entry.ChainHash;
                expectedSequence++;
            }

            return Result.Success(true);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogError(exception, "Failed to verify tamper-evident audit chain for tenant {TenantId}", tenantId);
            return Result.Failure<bool>(Error.Failure("Audit.VerificationFailed", "The audit chain could not be verified."));
        }
    }

    public Task<Result<TamperEvidentAuditLog>> GetByIdAsync(Guid id)
        => GetByIdAsync(id, CancellationToken.None);

    public async Task<Result<TamperEvidentAuditLog>> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        var entry = await context.Set<TamperEvidentAuditLog>()
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken)
            .ConfigureAwait(false);

        if (entry is not null && scope.ServiceProvider.GetService<IAuditDataAccessRecorder>() is { } recorder)
        {
            await recorder.RecordAsync([new AuditAccessedRecord(entry.TenantId, entry.Timestamp)], cancellationToken).ConfigureAwait(false);
        }
        return entry is null
            ? Result.Failure<TamperEvidentAuditLog>(Error.NotFound("Audit.NotFound", "The audit entry was not found."))
            : Result.Success(entry);
    }

    public Task<Result<IEnumerable<TamperEvidentAuditLog>>> GetByTenantAsync(Guid tenantId)
        => GetByTenantAsync(tenantId, 0, 100, CancellationToken.None);

    public Task<Result<IEnumerable<TamperEvidentAuditLog>>> GetByTenantAsync(
        Guid tenantId,
        CancellationToken cancellationToken)
        => GetByTenantAsync(tenantId, 0, 100, cancellationToken);

    public Task<Result<IEnumerable<TamperEvidentAuditLog>>> GetByTenantAsync(Guid tenantId, int skip, int take)
        => GetByTenantAsync(tenantId, skip, take, CancellationToken.None);

    public async Task<Result<IEnumerable<TamperEvidentAuditLog>>> GetByTenantAsync(
        Guid tenantId,
        int skip,
        int take,
        CancellationToken cancellationToken)
    {
        if (tenantId == Guid.Empty || skip < 0 || take <= 0)
        {
            return Result.Failure<IEnumerable<TamperEvidentAuditLog>>(
                Error.Validation("Audit.InvalidPage", "Tenant ID, skip, and take values are invalid."));
        }

        await using var scope = scopeFactory.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        var entries = await context.Set<TamperEvidentAuditLog>()
            .AsNoTracking()
            .Where(entry => entry.TenantId == tenantId)
            .OrderByDescending(entry => entry.SequenceNumber)
            .Skip(skip)
            .Take(Math.Min(take, MaximumPageSize))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (scope.ServiceProvider.GetService<IAuditDataAccessRecorder>() is { } recorder)
        {
            await recorder.RecordAsync(entries.Select(entry => new AuditAccessedRecord(entry.TenantId, entry.Timestamp)), cancellationToken).ConfigureAwait(false);
        }
        return Result.Success<IEnumerable<TamperEvidentAuditLog>>(entries);
    }

    public Task<Result<IEnumerable<TamperEvidentAuditLog>>> GetUnverifiedAsync(Guid tenantId)
        => GetUnverifiedAsync(tenantId, CancellationToken.None);

    public async Task<Result<IEnumerable<TamperEvidentAuditLog>>> GetUnverifiedAsync(
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        if (tenantId == Guid.Empty)
        {
            return Result.Failure<IEnumerable<TamperEvidentAuditLog>>(
                Error.Validation("Audit.InvalidTenant", "A non-empty tenant ID is required."));
        }

        await using var scope = scopeFactory.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        var entries = await context.Set<TamperEvidentAuditLog>()
            .AsNoTracking()
            .Where(entry => entry.TenantId == tenantId && !entry.IsVerified)
            .OrderBy(entry => entry.SequenceNumber)
            .Take(MaximumPageSize)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (scope.ServiceProvider.GetService<IAuditDataAccessRecorder>() is { } recorder)
        {
            await recorder.RecordAsync(entries.Select(entry => new AuditAccessedRecord(entry.TenantId, entry.Timestamp)), cancellationToken).ConfigureAwait(false);
        }
        return Result.Success<IEnumerable<TamperEvidentAuditLog>>(entries);
    }

    public Task<Result> MarkAsVerifiedAsync(Guid id)
        => MarkAsVerifiedAsync(id, null, CancellationToken.None);

    public Task<Result> MarkAsVerifiedAsync(Guid id, CancellationToken cancellationToken)
        => MarkAsVerifiedAsync(id, null, cancellationToken);

    public Task<Result> MarkAsVerifiedAsync(Guid id, string? notes)
        => MarkAsVerifiedAsync(id, notes, CancellationToken.None);

    public async Task<Result> MarkAsVerifiedAsync(Guid id, string? notes, CancellationToken cancellationToken)
    {
        var existing = await GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
        if (existing.IsFailure)
        {
            return Result.Failure(existing.Error);
        }

        var chain = await VerifyChainIntegrityAsync(existing.Value.TenantId!.Value, cancellationToken).ConfigureAwait(false);
        if (chain.IsFailure)
        {
            return Result.Failure(chain.Error);
        }

        if (!chain.Value)
        {
            return Result.Failure(Error.Failure("Audit.InvalidChain", "The audit chain is invalid and cannot be certified."));
        }

        await using var scope = scopeFactory.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        var entry = await context.Set<TamperEvidentAuditLog>()
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken)
            .ConfigureAwait(false);

        if (entry is null)
        {
            return Result.Failure(Error.NotFound("Audit.NotFound", "The audit entry was not found."));
        }

        entry.MarkAsVerified(notes);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Result.Success();
    }

    internal static string SerializeContent(TamperEvidentAuditLog entry, DateTime? timestampOverride = null)
    {
        return JsonSerializer.Serialize(new AuditContent(
            entry.Id,
            entry.TenantId,
            entry.UserId,
            entry.SessionId,
            entry.CorrelationId,
            entry.Action,
            entry.EntityType,
            entry.EntityId,
            entry.BeforeSnapshot,
            entry.AfterSnapshot,
            entry.Changes,
            entry.RiskLevel,
            entry.IpAddress,
            entry.UserAgent,
            entry.Country,
            entry.Region,
            entry.City,
            timestampOverride ?? entry.Timestamp));
    }

    private static Result<TValue> Failure<TValue>(string code, string description)
    {
        return Result.Failure<TValue>(Error.Failure(code, description));
    }

    private sealed record AuditContent(
        Guid Id,
        Guid? TenantId,
        Guid? UserId,
        Guid? SessionId,
        string? CorrelationId,
        string Action,
        string EntityType,
        Guid? EntityId,
        string? BeforeSnapshot,
        string? AfterSnapshot,
        string Changes,
        string RiskLevel,
        string IpAddress,
        string UserAgent,
        string? Country,
        string? Region,
        string? City,
        DateTime Timestamp);
}
