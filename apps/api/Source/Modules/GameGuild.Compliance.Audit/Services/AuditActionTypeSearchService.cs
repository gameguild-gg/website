using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;

namespace GameGuild.Compliance.Audit;

/// <summary>Queries audit logs by action type and produces bounded frequency/correlation summaries.</summary>
public sealed class AuditActionTypeSearchService(IApplicationDbContext context, IAuditDataAccessRecorder? accessRecorder = null) : IAuditActionTypeSearchService
{
    private const int RelatedActionLimit = 5000;

    public Task<AuditActionTypeSearchResult> SearchAsync(AuditActionTypeSearchRequest request) =>
        SearchAsync(request, CancellationToken.None);

    public async Task<AuditActionTypeSearchResult> SearchAsync(
        AuditActionTypeSearchRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var query = BuildQuery(request);
        var totalCount = await query.CountAsync(cancellationToken).ConfigureAwait(false);

        var frequency = await query
            .GroupBy(log => log.ActionType)
            .Select(group => new { ActionType = group.Key, EventCount = group.Count() })
            .OrderByDescending(item => item.EventCount)
            .ThenBy(item => item.ActionType)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var frequencyResults = frequency
            .Select(item => new AuditActionTypeFrequency(item.ActionType, item.EventCount))
            .ToArray();

        var logs = await ApplyOrdering(query, request)
            .Skip(request.Skip)
            .Take(request.Take)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var trends = request.IncludeTrends
            ? await GetTrendsAsync(query, request.TrendBucketSize, cancellationToken).ConfigureAwait(false)
            : [];

        var relatedActions = request.IncludeRelatedActions
            ? await GetRelatedActionsAsync(logs, request.TenantId, cancellationToken).ConfigureAwait(false)
            : [];

        if (accessRecorder is not null)
        {
            await accessRecorder.RecordAsync(logs.Select(log => new AuditAccessedRecord(log.TenantId, log.CreatedAt)), cancellationToken).ConfigureAwait(false);
        }
        return new AuditActionTypeSearchResult
        {
            Logs = logs,
            TotalCount = totalCount,
            Skip = request.Skip,
            Take = logs.Count,
            Frequency = frequencyResults,
            Trends = trends,
            RelatedActions = relatedActions
        };
    }

    public Task<AuditActionTypeExportResult> ExportAsync(AuditActionTypeSearchRequest request, int maximumRecords) =>
        ExportAsync(request, maximumRecords, CancellationToken.None);

    public async Task<AuditActionTypeExportResult> ExportAsync(
        AuditActionTypeSearchRequest request,
        int maximumRecords,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumRecords);

        var query = BuildQuery(request);
        var totalCount = await query.CountAsync(cancellationToken).ConfigureAwait(false);
        if (totalCount > maximumRecords)
        {
            return new AuditActionTypeExportResult { TotalCount = totalCount, ExceedsLimit = true };
        }

        var logs = await ApplyOrdering(query, request)
            .Take(maximumRecords)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (accessRecorder is not null)
        {
            await accessRecorder.RecordAsync(logs.Select(log => new AuditAccessedRecord(log.TenantId, log.CreatedAt)), cancellationToken).ConfigureAwait(false);
        }
        return new AuditActionTypeExportResult { TotalCount = totalCount, Logs = logs };
    }

    private IQueryable<AuditLog> BuildQuery(AuditActionTypeSearchRequest request)
    {
        var actionTypes = (request.ActionTypes ?? [])
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var groups = (request.ActionGroups ?? [])
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => AuditActionTypeTaxonomy.NormalizeGroup(value!))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var categories = (request.Categories ?? [])
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => AuditActionTypeTaxonomy.NormalizeCategory(value!))
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        IQueryable<AuditLog> query = context.Set<AuditLog>().AsNoTracking();
        if (request.UserId.HasValue)
        {
            query = query.Where(log => log.UserId == request.UserId.Value);
        }
        if (request.TenantId.HasValue)
        {
            query = query.Where(log => log.TenantId == request.TenantId.Value);
        }
        if (request.StartDate.HasValue)
        {
            query = query.Where(log => log.CreatedAt >= request.StartDate.Value.UtcDateTime);
        }
        if (request.EndDate.HasValue)
        {
            query = query.Where(log => log.CreatedAt <= request.EndDate.Value.UtcDateTime);
        }

        var predicates = new List<Expression>();
        var parameter = Expression.Parameter(typeof(AuditLog), "log");

        if (actionTypes.Length > 0)
        {
            predicates.Add(StringSetContains(actionTypes, Expression.Property(parameter, nameof(AuditLog.ActionType))));
        }

        if (groups.Length > 0)
        {
            predicates.Add(Combine(groups.Select(group => BuildGroupPredicate(parameter, group)), ExpressionType.OrElse));
        }

        if (categories.Length > 0)
        {
            predicates.Add(Combine(categories.Select(category => BuildCategoryPredicate(parameter, category)), ExpressionType.OrElse));
        }

        if (predicates.Count == 0)
        {
            throw new ArgumentException("At least one action type, action group, or taxonomy category is required.", nameof(request));
        }

        var disjunction = Combine(predicates, ExpressionType.OrElse);
        var filter = request.LogicalOperator switch
        {
            AuditActionTypeLogicalOperator.Any => disjunction,
            AuditActionTypeLogicalOperator.All => Combine(predicates, ExpressionType.AndAlso),
            AuditActionTypeLogicalOperator.None => Expression.Not(disjunction),
            _ => throw new ArgumentOutOfRangeException(nameof(request.LogicalOperator))
        };

        var lambda = Expression.Lambda<Func<AuditLog, bool>>(filter, parameter);
        return query.Where(lambda);
    }

    private static Expression BuildGroupPredicate(ParameterExpression parameter, string group)
    {
        var actionType = Expression.Property(parameter, nameof(AuditLog.ActionType));
        var actionTypes = AuditActionTypeTaxonomy.GetActionTypesForGroup(group);
        var knownActions = StringSetContains(actionTypes, actionType);

        if (group == "CRUD")
        {
            var suffixMatches = new[] { "Created", "Updated", "Deleted", "Added", "Removed" }
                .Select(suffix => Expression.Call(actionType, nameof(string.EndsWith), Type.EmptyTypes, Expression.Constant(suffix)));
            return Combine(suffixMatches.Append(knownActions), ExpressionType.OrElse);
        }

        var categories = AuditActionTypeTaxonomy.GetAuditCategoriesForGroup(group);
        return categories.Length == 0
            ? knownActions
            : Expression.OrElse(knownActions, EnumSetContains(categories, Expression.Property(parameter, nameof(AuditLog.Category))));
    }

    private static Expression BuildCategoryPredicate(ParameterExpression parameter, string category)
    {
        var actionTypes = AuditActionTypeTaxonomy.GetActionTypesForCategory(category);
        var actionTypeMatch = StringSetContains(actionTypes, Expression.Property(parameter, nameof(AuditLog.ActionType)));
        var auditCategories = AuditActionTypeTaxonomy.GetAuditCategoriesForTaxonomyCategory(category);
        return auditCategories.Length > 0
            ? Expression.OrElse(actionTypeMatch, EnumSetContains(auditCategories, Expression.Property(parameter, nameof(AuditLog.Category))))
            : actionTypeMatch;
    }

    private static Expression StringSetContains(string[] values, Expression value) =>
        Expression.Call(typeof(Enumerable), nameof(Enumerable.Contains), [typeof(string)], Expression.Constant(values), value);

    private static Expression EnumSetContains(AuditCategory[] values, Expression value) =>
        Expression.Call(typeof(Enumerable), nameof(Enumerable.Contains), [typeof(AuditCategory)], Expression.Constant(values), value);

    private static Expression Combine(IEnumerable<Expression> expressions, ExpressionType operation)
    {
        using var iterator = expressions.GetEnumerator();
        if (!iterator.MoveNext())
        {
            throw new ArgumentException("At least one expression is required.", nameof(expressions));
        }
        var result = iterator.Current;
        while (iterator.MoveNext())
        {
            result = operation == ExpressionType.AndAlso
                ? Expression.AndAlso(result, iterator.Current)
                : Expression.OrElse(result, iterator.Current);
        }

        return result;
    }

    private static IOrderedQueryable<AuditLog> ApplyOrdering(
        IQueryable<AuditLog> query,
        AuditActionTypeSearchRequest request)
    {
        var descending = request.SortDirection == AuditActionTypeSortDirection.Descending;
        return request.SortBy switch
        {
            AuditActionTypeSortField.ActionType => descending
                ? query.OrderByDescending(log => log.ActionType).ThenByDescending(log => log.Id)
                : query.OrderBy(log => log.ActionType).ThenBy(log => log.Id),
            AuditActionTypeSortField.ResourceType => descending
                ? query.OrderByDescending(log => log.ResourceType).ThenByDescending(log => log.Id)
                : query.OrderBy(log => log.ResourceType).ThenBy(log => log.Id),
            AuditActionTypeSortField.UserId => descending
                ? query.OrderByDescending(log => log.UserId).ThenByDescending(log => log.Id)
                : query.OrderBy(log => log.UserId).ThenBy(log => log.Id),
            AuditActionTypeSortField.RiskLevel => descending
                ? query.OrderByDescending(log => log.RiskLevel).ThenByDescending(log => log.Id)
                : query.OrderBy(log => log.RiskLevel).ThenBy(log => log.Id),
            _ => descending
                ? query.OrderByDescending(log => log.CreatedAt).ThenByDescending(log => log.Id)
                : query.OrderBy(log => log.CreatedAt).ThenBy(log => log.Id)
        };
    }

    private static async Task<IReadOnlyList<AuditActionTypeTrend>> GetTrendsAsync(
        IQueryable<AuditLog> query,
        AuditActivityBucketSize bucketSize,
        CancellationToken cancellationToken)
    {
        if (bucketSize == AuditActivityBucketSize.Hourly)
        {
            var hourly = await query
                .GroupBy(log => new { log.ActionType, log.CreatedAt.Year, log.CreatedAt.Month, log.CreatedAt.Day, log.CreatedAt.Hour })
                .Select(group => new
                {
                    group.Key.ActionType,
                    group.Key.Year,
                    group.Key.Month,
                    group.Key.Day,
                    group.Key.Hour,
                    EventCount = group.Count()
                })
                .OrderBy(item => item.Year).ThenBy(item => item.Month).ThenBy(item => item.Day)
                .ThenBy(item => item.Hour).ThenBy(item => item.ActionType)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            return hourly.Select(item => new AuditActionTypeTrend(
                item.ActionType,
                new DateTime(item.Year, item.Month, item.Day, item.Hour, 0, 0, DateTimeKind.Utc),
                item.EventCount)).ToArray();
        }

        if (bucketSize != AuditActivityBucketSize.Daily)
        {
            throw new ArgumentOutOfRangeException(nameof(bucketSize), bucketSize, "Only hourly and daily trend buckets are supported.");
        }

        var daily = await query
            .GroupBy(log => new { log.ActionType, log.CreatedAt.Year, log.CreatedAt.Month, log.CreatedAt.Day })
            .Select(group => new
            {
                group.Key.ActionType,
                group.Key.Year,
                group.Key.Month,
                group.Key.Day,
                EventCount = group.Count()
            })
            .OrderBy(item => item.Year).ThenBy(item => item.Month).ThenBy(item => item.Day).ThenBy(item => item.ActionType)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return daily.Select(item => new AuditActionTypeTrend(
            item.ActionType,
            new DateTime(item.Year, item.Month, item.Day, 0, 0, 0, DateTimeKind.Utc),
            item.EventCount)).ToArray();
    }

    private async Task<IReadOnlyList<AuditRelatedAction>> GetRelatedActionsAsync(
        IReadOnlyList<AuditLog> logs,
        Guid? tenantId,
        CancellationToken cancellationToken)
    {
        var correlationIds = logs
            .Select(log => log.CorrelationId)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        if (correlationIds.Length == 0)
        {
            return [];
        }

        var selectedIds = logs.Select(log => log.Id).ToArray();
        var query = context.Set<AuditLog>()
            .AsNoTracking()
            .Where(log => log.CorrelationId != null && correlationIds.Contains(log.CorrelationId) && !selectedIds.Contains(log.Id));
        if (tenantId.HasValue)
        {
            query = query.Where(log => log.TenantId == tenantId.Value);
        }

        var related = await query
            .OrderByDescending(log => log.CreatedAt)
            .Take(RelatedActionLimit)
            .GroupBy(log => log.ActionType)
            .Select(group => new { ActionType = group.Key, CorrelationCount = group.Count() })
            .OrderByDescending(item => item.CorrelationCount)
            .ThenBy(item => item.ActionType)
            .Take(20)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return related.Select(item => new AuditRelatedAction(item.ActionType, item.CorrelationCount)).ToArray();
    }
}
