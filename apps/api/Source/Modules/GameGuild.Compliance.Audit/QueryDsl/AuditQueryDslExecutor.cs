using System.Diagnostics;
using System.Text.Json;
using GameGuild.Identity.Authorization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GameGuild.Compliance.Audit;

/// <summary>
///     Executes a parsed audit query DSL expression against the unified audit stores
///     (authentication attempts, permission audit logs, general audit logs) with hard
///     bounds: a derived scan window, per-source row caps, a wall-clock timeout, tenant
///     scope clamping for non-system-admin callers, aggregation over the full matched
///     set, and data-masked result pages.
/// </summary>
public interface IAuditQueryDslExecutor
{
    Task<Result<AuditQueryDslExecutionResult>> ExecuteAsync(
        AuditQueryDslExecutionRequest request,
        CancellationToken cancellationToken = default);
}

/// <inheritdoc />
public sealed class AuditQueryDslExecutor(
    IAuditLogQueryService auditLogQueryService,
    IDataMaskingService dataMaskingService,
    IAuditDataAccessRecorder? accessRecorder,
    IOptions<AuditQueryDslOptions> options,
    ILogger<AuditQueryDslExecutor> logger) : IAuditQueryDslExecutor
{
    private const string ExecutionErrorCode = "AuditQueryDsl.Execution";
    private const string TimeoutErrorCode = "AuditQueryDsl.Timeout";
    private const string TenantScopeErrorCode = "AuditQueryDsl.TenantScope";

    private static readonly JsonSerializerOptions MaskingSerializerOptions = new(JsonSerializerDefaults.Web);

    public async Task<Result<AuditQueryDslExecutionResult>> ExecuteAsync(
        AuditQueryDslExecutionRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var bounds = options.Value;
        var stopwatch = Stopwatch.StartNew();

        if (request.Skip < 0)
        {
            return Result.Failure<AuditQueryDslExecutionResult>(Error.Validation(ExecutionErrorCode, "Skip must be zero or greater."));
        }

        if (request.Take < 1)
        {
            return Result.Failure<AuditQueryDslExecutionResult>(Error.Validation(ExecutionErrorCode, "Take must be at least 1."));
        }

        var take = Math.Min(request.Take, bounds.MaxPageSize);

        var timeoutSeconds = Math.Clamp(
            request.TimeoutSeconds ?? bounds.DefaultTimeoutSeconds,
            1,
            Math.Max(1, bounds.MaxTimeoutSeconds));
        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));

        // Tenant scope clamp: a tenant-scoped caller can never observe rows outside their tenant,
        // and an explicit tenant condition that points anywhere else fails closed.
        var tenantScopeError = ValidateTenantScope(request.Query.Filter, request.TenantScope);
        if (tenantScopeError is not null)
        {
            return Result.Failure<AuditQueryDslExecutionResult>(tenantScopeError);
        }

        var filter = request.TenantScope.HasValue
            ? ApplyTenantScope(request.Query.Filter, request.TenantScope.Value)
            : request.Query.Filter;

        var window = DeriveScanWindow(filter, bounds);
        var sourceRestriction = DeriveSourceRestriction(filter);

        List<UnifiedSecurityAuditEntry> entries;
        try
        {
            entries = (await auditLogQueryService.CollectUnifiedEntriesAsync(
                    window.StartUtc,
                    window.EndUtc,
                    sourceRestriction,
                    bounds.MaxRowsScanned,
                    timeoutSource.Token)
                .ConfigureAwait(false)).ToList();
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return Result.Failure<AuditQueryDslExecutionResult>(Error.Failure(
                TimeoutErrorCode,
                $"Audit query execution exceeded the {timeoutSeconds}s timeout while reading the audit stores. Narrow the time window with timestamp conditions (AFTER/BEFORE)."));
        }

        var scanTruncated = false;
        var maxScan = bounds.MaxRowsScanned;
        if (entries.Count > maxScan)
        {
            entries = entries.OrderByDescending(entry => entry.Timestamp).Take(maxScan).ToList();
            scanTruncated = true;
        }

        var matched = new List<UnifiedSecurityAuditEntry>();
        try
        {
            for (var index = 0; index < entries.Count; index++)
            {
                if (index % 512 == 0 && timeoutSource.Token.IsCancellationRequested)
                {
                    throw new OperationCanceledException(timeoutSource.Token);
                }

                if (AuditQueryDslEvaluator.Matches(filter, entries[index]))
                {
                    matched.Add(entries[index]);
                }
            }
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return Result.Failure<AuditQueryDslExecutionResult>(Error.Failure(
                TimeoutErrorCode,
                $"Audit query execution exceeded the {timeoutSeconds}s timeout while filtering {entries.Count} rows. Narrow the time window with timestamp conditions (AFTER/BEFORE)."));
        }

        var ascending = string.Equals(request.Query.SortDirection, "asc", StringComparison.OrdinalIgnoreCase);
        var ordered = ascending
            ? matched.OrderBy(entry => entry.Timestamp).ToList()
            : matched.OrderByDescending(entry => entry.Timestamp).ToList();

        var page = ordered.Skip(request.Skip).Take(take).ToList();

        if (accessRecorder is not null)
        {
            await accessRecorder.RecordAsync(
                page.Where(entry => entry.SourceType == SecurityAuditSourceType.General)
                    .Select(entry => new AuditAccessedRecord(entry.TenantId, entry.Timestamp)),
                timeoutSource.Token).ConfigureAwait(false);
        }

        object maskedPage = page;
        try
        {
            var masked = await dataMaskingService.ApplyAsync(
                bounds.MaskingResourceType,
                page,
                typeof(List<UnifiedSecurityAuditEntry>),
                MaskingSerializerOptions,
                timeoutSource.Token).ConfigureAwait(false);
            if (masked is not null)
            {
                maskedPage = masked;
            }
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return Result.Failure<AuditQueryDslExecutionResult>(Error.Failure(
                TimeoutErrorCode,
                $"Audit query execution exceeded the {timeoutSeconds}s timeout while applying data masking."));
        }
        catch (Exception exception)
        {
            // Fail closed: if the masking policy cannot be evaluated, no rows leave the service.
            logger.LogError(exception, "Data masking evaluation failed for an audit query executed by {ActingUserId}; returning an error instead of unmasked rows.", request.ActingUserId);
            return Result.Failure<AuditQueryDslExecutionResult>(Error.Failure(
                ExecutionErrorCode,
                "Result redaction could not be evaluated; the query result was withheld."));
        }

        stopwatch.Stop();
        logger.LogInformation(
            "Audit query executed by {ActingUserId}: {RowsScanned} scanned, {MatchedCount} matched, {PageCount} returned in {DurationMs}ms (window {WindowStart:o}..{WindowEnd:o}, source={Source})",
            request.ActingUserId,
            entries.Count,
            matched.Count,
            page.Count,
            stopwatch.ElapsedMilliseconds,
            window.StartUtc,
            window.EndUtc,
            sourceRestriction?.ToString() ?? "All");

        return Result.Success(new AuditQueryDslExecutionResult
        {
            Rows = FlattenMaskedPage(maskedPage),
            TotalCount = matched.Count,
            Skip = request.Skip,
            Take = page.Count,
            Aggregates = ComputeAggregates(matched),
            DurationMs = stopwatch.ElapsedMilliseconds,
            RowsScanned = entries.Count,
            ScanTruncated = scanTruncated,
            WindowStartUtc = window.StartUtc,
            WindowEndUtc = window.EndUtc
        });
    }

    private static List<object> FlattenMaskedPage(object maskedPage) => maskedPage switch
    {
        List<object> alreadyFlat => alreadyFlat,
        System.Collections.IEnumerable enumerable and not string => enumerable.Cast<object>().ToList(),
        _ => [maskedPage]
    };

    internal static Error? ValidateTenantScope(AuditQueryNode filter, Guid? tenantScope)
    {
        if (!tenantScope.HasValue)
        {
            return null;
        }

        var mismatches = new List<string>();
        CollectTenantConditions(filter, tenantScope.Value, mismatches, andContext: true);
        return mismatches.Count > 0
            ? Error.Forbidden(
                TenantScopeErrorCode,
                $"Query attempted to read audit data for a tenant other than the caller's tenant ({string.Join(", ", mismatches.Take(3))}).")
            : null;
    }

    private static void CollectTenantConditions(AuditQueryNode node, Guid tenantScope, List<string> mismatches, bool andContext)
    {
        switch (node)
        {
            case AuditQueryGroup group:
                var childContext = andContext && group.Logic == AuditQueryLogicalOperator.And && !group.Negate;
                foreach (var child in group.Children)
                {
                    CollectTenantConditions(child, tenantScope, mismatches, childContext);
                }

                break;

            case AuditQueryCondition { Field: AuditQueryField.Tenant } condition when andContext && !condition.Negate:
                var requested = condition.Value.ValueKind == JsonValueKind.String && Guid.TryParse(condition.Value.GetString(), out var guid)
                    ? guid
                    : (Guid?)null;
                if (requested != tenantScope)
                {
                    mismatches.Add(condition.Value.ToString());
                }

                break;
        }
    }

    private static AuditQueryNode ApplyTenantScope(AuditQueryNode filter, Guid tenantScope) =>
        new AuditQueryGroup
        {
            Logic = AuditQueryLogicalOperator.And,
            Children =
            [
                filter,
                new AuditQueryCondition
                {
                    Field = AuditQueryField.Tenant,
                    Operator = AuditQueryComparison.Eq,
                    Value = JsonSerializer.SerializeToElement(tenantScope.ToString())
                }
            ]
        };

    /// <summary>
    ///     Derives the narrowest safe scan window: only timestamp conditions that sit in a pure
    ///     AND ancestry chain are necessary conditions for a match, so intersecting their bounds
    ///     can never exclude a matching row. Without any such bound, the default lookback applies
    ///     (and the response reports it so callers can widen explicitly).
    /// </summary>
    internal static (DateTime StartUtc, DateTime EndUtc) DeriveScanWindow(AuditQueryNode filter, AuditQueryDslOptions bounds)
    {
        DateTime? lower = null;
        DateTime? upper = null;
        CollectTimestampBounds(filter, ref lower, ref upper, andContext: true);

        var now = DateTime.UtcNow;
        var start = lower ?? now.AddDays(-bounds.DefaultLookbackDays);
        var end = upper ?? now;
        if (end < start)
        {
            end = start;
        }

        return (start, end);
    }

    private static void CollectTimestampBounds(AuditQueryNode node, ref DateTime? lower, ref DateTime? upper, bool andContext)
    {
        switch (node)
        {
            case AuditQueryGroup group:
                var childContext = andContext && group.Logic == AuditQueryLogicalOperator.And && !group.Negate;
                foreach (var child in group.Children)
                {
                    CollectTimestampBounds(child, ref lower, ref upper, childContext);
                }

                break;

            case AuditQueryCondition { Field: AuditQueryField.Timestamp } condition when andContext && !condition.Negate:
                ApplyTimestampBound(condition, ref lower, ref upper);
                break;

            default:
                // Other fields and negated conditions cannot safely narrow the scan window.
                break;
        }
    }

    private static void ApplyTimestampBound(AuditQueryCondition condition, ref DateTime? lower, ref DateTime? upper)
    {
        if (condition.Operator is not (AuditQueryComparison.Gte or AuditQueryComparison.Gt or AuditQueryComparison.Eq or AuditQueryComparison.Lte or AuditQueryComparison.Lt))
        {
            return;
        }

        var parsed = DateTime.TryParse(
            condition.Value.ValueKind == JsonValueKind.String ? condition.Value.GetString() : condition.Value.GetRawText(),
            System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.AssumeUniversal,
            out var timestamp)
            ? timestamp.ToUniversalTime()
            : (DateTime?)null;
        if (parsed is null)
        {
            return;
        }

        switch (condition.Operator)
        {
            case AuditQueryComparison.Gt:
            case AuditQueryComparison.Gte:
            case AuditQueryComparison.Eq:
                if (parsed.Value > lower.GetValueOrDefault(DateTime.MinValue))
                {
                    lower = parsed;
                }

                break;

            case AuditQueryComparison.Lt:
            case AuditQueryComparison.Lte:
                if (parsed.Value < upper.GetValueOrDefault(DateTime.MaxValue))
                {
                    upper = parsed;
                }

                break;

            default:
                // Comparisons without a timestamp bound are filtered out above.
                break;
        }
    }

    /// <summary>
    ///     Restricts the store fetch to a single source when every AND-context source condition
    ///     names the same source; otherwise all stores are read. AND-context conditions are
    ///     necessary conditions, so this pushdown never changes results.
    /// </summary>
    internal static SecurityAuditSourceType? DeriveSourceRestriction(AuditQueryNode filter)
    {
        SecurityAuditSourceType? restriction = null;
        var conflict = false;
        CollectSourceRestrictions(filter, ref restriction, ref conflict, andContext: true);
        return conflict ? null : restriction;
    }

    private static void CollectSourceRestrictions(AuditQueryNode node, ref SecurityAuditSourceType? restriction, ref bool conflict, bool andContext)
    {
        switch (node)
        {
            case AuditQueryGroup group:
                var childContext = andContext && group.Logic == AuditQueryLogicalOperator.And && !group.Negate;
                foreach (var child in group.Children)
                {
                    CollectSourceRestrictions(child, ref restriction, ref conflict, childContext);
                }

                break;

            case AuditQueryCondition { Field: AuditQueryField.Source, Operator: AuditQueryComparison.Eq } condition when andContext && !condition.Negate:
                if (condition.Value.ValueKind == JsonValueKind.String &&
                    Enum.TryParse<SecurityAuditSourceType>(condition.Value.GetString(), ignoreCase: true, out var source))
                {
                    if (restriction.HasValue && restriction.Value != source)
                    {
                        conflict = true;
                    }
                    else
                    {
                        restriction = source;
                    }
                }

                break;
        }
    }

    private static AuditQueryDslAggregates ComputeAggregates(IReadOnlyList<UnifiedSecurityAuditEntry> matched)
    {
        var aggregates = new AuditQueryDslAggregates
        {
            MatchedCount = matched.Count,
            SuccessCount = matched.Count(entry => entry.Success),
            FailureCount = matched.Count(entry => !entry.Success),
            FirstMatchedAt = matched.Count == 0 ? null : matched.Min(entry => entry.Timestamp),
            LastMatchedAt = matched.Count == 0 ? null : matched.Max(entry => entry.Timestamp)
        };

        foreach (var group in matched.GroupBy(entry => entry.SourceType.ToString(), StringComparer.OrdinalIgnoreCase))
        {
            aggregates.BySource[group.Key] = group.Count();
        }

        foreach (var group in matched.GroupBy(entry => entry.RiskLevel.ToString(), StringComparer.OrdinalIgnoreCase))
        {
            aggregates.BySeverity[group.Key] = group.Count();
        }

        foreach (var group in matched.GroupBy(entry => entry.ActionType, StringComparer.OrdinalIgnoreCase)
                     .OrderByDescending(group => group.Count())
                     .ThenBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
                     .Take(10))
        {
            aggregates.ByAction[group.Key] = group.Count();
        }

        foreach (var bucket in matched
                     .GroupBy(entry => DateOnly.FromDateTime(entry.Timestamp))
                     .OrderBy(day => day.Key)
                     .Take(366))
        {
            aggregates.TimeBuckets.Add(new AuditQueryDslTimeBucket(bucket.Key.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc), bucket.Count()));
        }

        return aggregates;
    }
}
