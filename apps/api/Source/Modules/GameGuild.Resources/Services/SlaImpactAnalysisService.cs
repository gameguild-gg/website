using Microsoft.Extensions.Logging;

namespace GameGuild.Resources;

/// <summary>
///     Implementation of SLA impact analysis and violation tracking
/// </summary>
public class SlaImpactAnalysisService(
    ISlaImpactAnalysisRepository analysisRepository,
    IResourceQuotaRepository quotaRepository,
    ISlaIncidentEscalationService escalationService,
    IIncidentTicketProvider incidentTicketProvider,
    ILogger<SlaImpactAnalysisService> logger
) : ISlaImpactAnalysisService
{
    public async Task<SlaImpactAnalysis> RecordViolationAsync(
        Guid resourceQuotaId,
        SlaViolationType violationType,
        SlaViolationSeverity severity,
        long expectedValue,
        long actualValue,
        Guid? userId = null,
        CancellationToken cancellationToken = default
    )
    {
        var quota = await quotaRepository.GetByIdAsync(resourceQuotaId, cancellationToken).ConfigureAwait(false);

        if (quota == null) { throw new ArgumentException($"Resource quota {resourceQuotaId} not found", nameof(resourceQuotaId)); }

        var violation = new SlaImpactAnalysis
        {
            ResourceQuotaId = resourceQuotaId,
            UserId = userId,
            ViolationStartTime = SystemClock.UtcNow,
            ViolationType = violationType,
            Severity = severity,
            ExpectedValue = expectedValue,
            ActualValue = actualValue,
            IsResolved = false,
            RequiresEscalation = severity >= SlaViolationSeverity.High,
            IncidentCreated = false
        };

        // Set TenantId using SetProperties (EntityBase has protected setter)
        violation.SetProperties(new Dictionary<string, object?> { ["TenantId"] = quota.TenantId });

        violation.CalculateDeviation();

        var savedViolation = await analysisRepository.CreateAsync(violation, cancellationToken).ConfigureAwait(false);

        logger.LogWarning("SLA violation recorded: Type={Type}, Severity={Severity}, Quota={QuotaId}, Expected={Expected}, Actual={Actual}", violationType, severity, resourceQuotaId, expectedValue, actualValue);

        // Auto-escalate high/critical violations
        if (severity >= SlaViolationSeverity.High)
        {
            try
            {
                var escalationResult = await escalationService.EscalateViolationAsync(savedViolation, cancellationToken).ConfigureAwait(false);

                if (escalationResult is { WasEscalated: true })
                {
                    logger.LogInformation(
                        "Violation {ViolationId} auto-escalated: Incident={IncidentId}, NotifiedUsers={UserCount}",
                        savedViolation.Id, escalationResult.IncidentId, escalationResult.NotifiedUserIds.Count);
                }
            }
            catch (Exception ex)
            {
                // Don't fail the violation recording if escalation fails
                logger.LogError(ex, "Failed to auto-escalate violation {ViolationId}", savedViolation.Id);
            }
        }

        return savedViolation;
    }

    public async Task<SlaImpactAnalysis?> GetViolationAsync(Guid violationId, CancellationToken cancellationToken = default) { return await analysisRepository.GetByIdAsync(violationId, cancellationToken).ConfigureAwait(false); }

    public async Task<IEnumerable<SlaImpactAnalysis>> GetTenantViolationsAsync(
        Guid tenantId,
        DateTime? fromDate = null,
        DateTime? toDate = null,
        SlaViolationSeverity? minSeverity = null,
        CancellationToken cancellationToken = default
    )
    {
        IEnumerable<SlaImpactAnalysis> violations;

        if (fromDate.HasValue && toDate.HasValue) { violations = await analysisRepository.GetByDateRangeAsync(tenantId, fromDate.Value, toDate.Value, cancellationToken).ConfigureAwait(false); }
        else
        {
            violations = await analysisRepository.GetByTenantAsync(tenantId, cancellationToken).ConfigureAwait(false);

            if (fromDate.HasValue)
            {
                violations = violations.Where(v => v.ViolationStartTime >= fromDate.Value);
            }

            if (toDate.HasValue)
            {
                violations = violations.Where(v => v.ViolationStartTime <= toDate.Value);
            }
        }

        if (minSeverity.HasValue) { violations = violations.Where(v => v.Severity >= minSeverity.Value); }

        return violations;
    }

    public async Task<IEnumerable<SlaImpactAnalysis>> GetUnresolvedViolationsAsync(Guid? tenantId = null, SlaViolationSeverity? minSeverity = null, CancellationToken cancellationToken = default)
    {
        if (!tenantId.HasValue)
        {
            // If no tenant specified, we can't use the repository method
            // This would need a different repository method or throw an exception
            throw new ArgumentException("TenantId is required", nameof(tenantId));
        }

        var violations = await analysisRepository.GetUnresolvedAsync(tenantId.Value, cancellationToken).ConfigureAwait(false);

        if (minSeverity.HasValue) { violations = violations.Where(v => v.Severity >= minSeverity.Value); }

        return violations;
    }

    public async Task<bool> ResolveViolationAsync(Guid violationId, Guid resolvedByUserId, string? mitigationActions = null, CancellationToken cancellationToken = default)
    {
        var violation = await analysisRepository.GetByIdAsync(violationId, cancellationToken).ConfigureAwait(false);

        if (violation == null)
        {
            return false;
        }

        violation.Resolve(resolvedByUserId, mitigationActions);

        await analysisRepository.UpdateAsync(violation, cancellationToken).ConfigureAwait(false);

        logger.LogInformation("SLA violation {ViolationId} resolved by user {UserId}", violationId, resolvedByUserId);

        return true;
    }

    public async Task<bool> UpdateViolationAsync(Guid violationId, string? rootCause = null, string? businessImpact = null, bool? requiresEscalation = null, CancellationToken cancellationToken = default)
    {
        var violation = await analysisRepository.GetByIdAsync(violationId, cancellationToken).ConfigureAwait(false);

        if (violation == null)
        {
            return false;
        }

        if (rootCause != null)
        {
            violation.RootCause = rootCause;
        }

        if (businessImpact != null)
        {
            violation.BusinessImpact = businessImpact;
        }

        if (requiresEscalation.HasValue)
        {
            violation.RequiresEscalation = requiresEscalation.Value;
        }

        violation.Touch();

        await analysisRepository.UpdateAsync(violation, cancellationToken).ConfigureAwait(false);

        logger.LogInformation("SLA violation {ViolationId} updated", violationId);

        return true;
    }

    public async Task<string> CreateIncidentTicketAsync(Guid violationId, CancellationToken cancellationToken = default)
    {
        var violation = await analysisRepository.GetByIdAsync(violationId, cancellationToken).ConfigureAwait(false);

        if (violation == null) { throw new ArgumentException($"Violation {violationId} not found", nameof(violationId)); }

        // Create incident ticket using the injected provider
        // The provider can be implemented by Incident Management module for real integration
        var ticketId = await incidentTicketProvider.CreateTicketAsync(violation, cancellationToken).ConfigureAwait(false);

        violation.IncidentCreated = true;
        violation.IncidentTicketId = ticketId;
        violation.Touch();

        await analysisRepository.UpdateAsync(violation, cancellationToken).ConfigureAwait(false);

        logger.LogInformation("Incident ticket {TicketId} created for SLA violation {ViolationId}", ticketId, violationId);

        return ticketId;
    }

    public async Task<SlaComplianceMetrics> GetComplianceMetricsAsync(Guid tenantId, DateTime periodStart, DateTime periodEnd, CancellationToken cancellationToken = default)
    {
        var violations = await analysisRepository.GetByDateRangeAsync(tenantId, periodStart, periodEnd, cancellationToken).ConfigureAwait(false);

        var violationsList = violations.ToList();

        var totalViolations = violationsList.Count;
        var criticalViolations = violationsList.Count(v => v.Severity == SlaViolationSeverity.Critical);
        var resolvedViolations = violationsList.Count(v => v.IsResolved);
        var unresolvedViolations = totalViolations - resolvedViolations;

        var violationsByType = violationsList.GroupBy(v => v.ViolationType).ToDictionary(g => g.Key, g => g.Count());

        // Calculate average resolution time
        var resolvedWithTime = violationsList.Where(v => v.IsResolved && v.ResolvedAt.HasValue).ToList();

        var avgResolutionTime = resolvedWithTime.Count > 0 ? TimeSpan.FromTicks((long) resolvedWithTime.Select(v => (v.ResolvedAt!.Value - v.ViolationStartTime).Ticks).Average()) : TimeSpan.Zero;

        // Calculate compliance percentage (percentage of time without violations)
        var totalPeriodHours = (periodEnd - periodStart).TotalHours;

        var violationHours = violationsList.Sum(v =>
            {
                var endTime = v.ViolationEndTime ?? SystemClock.UtcNow;

                return (endTime - v.ViolationStartTime).TotalHours;
            }
        );

        var compliancePercentage = totalPeriodHours > 0 ? (decimal) ((totalPeriodHours - violationHours) / totalPeriodHours * 100) : 100m;

        var metrics = new SlaComplianceMetrics
        {
            TenantId = tenantId,
            PeriodStart = periodStart,
            PeriodEnd = periodEnd,
            TotalViolations = totalViolations,
            CriticalViolations = criticalViolations,
            ResolvedViolations = resolvedViolations,
            UnresolvedViolations = unresolvedViolations,
            CompliancePercentage = Math.Max(0, Math.Min(100, compliancePercentage)),
            AverageResolutionTime = avgResolutionTime,
            ViolationsByType = violationsByType
        };

        return metrics;
    }

    public async Task<Dictionary<ResourceUsageType, int>> GetViolationsByResourceTypeAsync(Guid? tenantId = null, DateTime? fromDate = null, DateTime? toDate = null, CancellationToken cancellationToken = default)
    {
        if (!tenantId.HasValue) { throw new ArgumentException("TenantId is required", nameof(tenantId)); }

        var from = fromDate ?? SystemClock.UtcNow.AddMonths(-1);
        var to = toDate ?? SystemClock.UtcNow;

        var stringCounts = await analysisRepository.GetViolationCountsByTypeAsync(tenantId.Value, from, to, cancellationToken).ConfigureAwait(false);

        // Convert Dictionary<string, int> to Dictionary<ResourceUsageType, int>
        var result = new Dictionary<ResourceUsageType, int>();

        foreach (var kvp in stringCounts)
        {
            if (Enum.TryParse(kvp.Key, out ResourceUsageType usageType)) { result[usageType] = kvp.Value; }
        }

        return result;
    }

    public async Task<IEnumerable<SlaImpactAnalysis>> GetCriticalOngoingViolationsAsync(Guid? tenantId = null, CancellationToken cancellationToken = default)
    {
        if (!tenantId.HasValue)
        {
            throw new ArgumentException("TenantId is required for getting critical ongoing violations", nameof(tenantId));
        }

        var unresolved = await analysisRepository.GetUnresolvedAsync(tenantId.Value, cancellationToken).ConfigureAwait(false);

        return unresolved
            .Where(v => v.Severity >= SlaViolationSeverity.High && !v.IsResolved)
            .OrderByDescending(v => v.Severity)
            .ThenByDescending(v => v.ViolationStartTime);
    }

    // Integration Points:
    // - Incident Management: IIncidentTicketProvider abstraction (injected, implemented by Incident Management module)
    // - Monitoring/Alerting: ISlaIncidentEscalationService handles auto-escalation and notifications
    // - Stakeholder Notifications: ISlaNotificationSender (used by SlaIncidentEscalationService)
}
