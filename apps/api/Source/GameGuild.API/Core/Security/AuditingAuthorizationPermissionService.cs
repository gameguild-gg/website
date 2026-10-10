using System.Security.Claims;
using GameGuild.Compliance.Audit;
using GameGuild.Identity.Authentication;
using GameGuild.Identity.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using ISiemIntegrationService = GameGuild.Identity.Authentication.ISiemIntegrationService;

namespace GameGuild.API.Core.Security;

/// <summary>
/// Records permission-service decisions in the shared audit trail without changing their outcomes.
/// </summary>
internal sealed class AuditingAuthorizationPermissionService(
    AuthorizationPermissionServiceAdapter inner,
    IAuditService auditService,
    IHttpContextAccessor httpContextAccessor,
    IConfiguration configuration,
    ISiemIntegrationService siemService,
    IMemoryCache alertCache,
    IRbacPermissionResolver rbacResolver,
    ILogger<AuditingAuthorizationPermissionService> logger)
    : IAuthorizationPermissionService
{
    private static readonly object AlertCacheLock = new();
    private const int DefaultDenialAlertThreshold = 5;

    public async Task<bool> HasPermissionAsync(
        Guid userId,
        Guid tenantId,
        string permission,
        CancellationToken cancellationToken = default)
    {
        var granted = await inner.HasPermissionAsync(userId, tenantId, permission, cancellationToken)
            .ConfigureAwait(false);
        var roles = await GetSubjectRolesSafelyAsync(userId, tenantId).ConfigureAwait(false);
        await LogDecisionSafelyAsync(userId, tenantId, permission, granted, "HasPermission", roles)
            .ConfigureAwait(false);

        return granted;
    }

    public async Task<PermissionCheckResult> HasAllPermissionsAsync(
        Guid userId,
        Guid tenantId,
        IEnumerable<string> permissions,
        CancellationToken cancellationToken = default)
    {
        var permissionList = permissions.ToArray();
        var result = await inner.HasAllPermissionsAsync(userId, tenantId, permissionList, cancellationToken)
            .ConfigureAwait(false);
        var roles = await GetSubjectRolesSafelyAsync(userId, tenantId).ConfigureAwait(false);
        await LogBatchDecisionsSafelyAsync(
                userId,
                tenantId,
                permissionList,
                result.PresentPermissions,
                "HasAllPermissions",
                roles)
            .ConfigureAwait(false);

        return result;
    }

    public async Task<PermissionCheckResult> HasAnyPermissionAsync(
        Guid userId,
        Guid tenantId,
        IEnumerable<string> permissions,
        CancellationToken cancellationToken = default)
    {
        var permissionList = permissions.ToArray();
        var result = await inner.HasAnyPermissionAsync(userId, tenantId, permissionList, cancellationToken)
            .ConfigureAwait(false);
        var roles = await GetSubjectRolesSafelyAsync(userId, tenantId).ConfigureAwait(false);
        await LogBatchDecisionsSafelyAsync(
                userId,
                tenantId,
                permissionList,
                result.PresentPermissions,
                "HasAnyPermission",
                roles)
            .ConfigureAwait(false);

        return result;
    }

    public Task<IReadOnlyList<string>> GetPermissionsAsync(
        Guid userId,
        Guid tenantId,
        CancellationToken cancellationToken = default)
        => inner.GetPermissionsAsync(userId, tenantId, cancellationToken);

    private async Task LogBatchDecisionsSafelyAsync(
        Guid userId,
        Guid tenantId,
        IReadOnlyCollection<string> requestedPermissions,
        IReadOnlyCollection<string> grantedPermissions,
        string evaluationType,
        IReadOnlyCollection<string> roles)
    {
        var granted = new HashSet<string>(grantedPermissions, StringComparer.OrdinalIgnoreCase);

        foreach (var permission in requestedPermissions)
        {
            await LogDecisionSafelyAsync(
                    userId,
                    tenantId,
                    permission,
                    granted.Contains(permission),
                    evaluationType,
                    roles)
                .ConfigureAwait(false);
        }
    }

    /// <summary>
    ///     Resolves the evaluated user's role attribution (issue #359 user-context logging) from
    ///     the RBAC resolver — the same attribution the effective-permission engine feeds into
    ///     decisions (direct roles by name, hierarchy-inherited roles marked, per the #330
    ///     contract). Best-effort: resolution failures and anonymous requests yield an empty
    ///     attribution and never change the audited decision.
    /// </summary>
    private async Task<IReadOnlyCollection<string>> GetSubjectRolesSafelyAsync(Guid subjectUserId, Guid tenantId)
    {
        var principal = httpContextAccessor.HttpContext?.User;
        if (subjectUserId == Guid.Empty || principal?.Identity?.IsAuthenticated != true)
        {
            return PermissionEvaluationRoles.Empty;
        }

        try
        {
            var rbacResult = await rbacResolver
                .ResolvePermissionsAsync(subjectUserId, tenantId)
                .ConfigureAwait(false);
            return PermissionEvaluationRoles.FromContributions(rbacResult.RoleContributions);
        }
        catch (Exception exception)
        {
            logger.LogWarning(
                exception,
                "Could not resolve role attribution for the permission-evaluation audit of user {UserId} in tenant {TenantId}; the decision is unaffected.",
                subjectUserId,
                tenantId);
            return PermissionEvaluationRoles.Empty;
        }
    }

    private async Task LogDecisionSafelyAsync(
        Guid subjectUserId,
        Guid tenantId,
        string permission,
        bool granted,
        string evaluationType,
        IReadOnlyCollection<string> roles)
    {
        var context = httpContextAccessor.HttpContext;
        var actorUserId = GetActorUserId(context?.User);

        try
        {
            await auditService.LogAsync(new CreateAuditLogRequest
            {
                ActionType = granted ? AuditActionTypes.PermissionGranted : AuditActionTypes.PermissionDenied,
                ResourceType = "Permission",
                ResourceId = permission,
                UserId = actorUserId ?? subjectUserId,
                TenantId = tenantId,
                SessionId = GetGuidClaim(context?.User, JwtClaimTypes.SessionId, "session_id"),
                IpAddress = context?.Connection.RemoteIpAddress?.ToString(),
                UserAgent = context?.Request.Headers.UserAgent.ToString(),
                Description = granted
                    ? $"Permission '{permission}' was granted in tenant scope."
                    : $"Permission '{permission}' was denied in tenant scope.",
                Metadata = new
                {
                    Permission = permission,
                    SubjectUserId = subjectUserId,
                    Roles = roles,
                    EvaluationType = evaluationType,
                    HttpMethod = context?.Request.Method,
                    RouteTemplate = (context?.GetEndpoint() as RouteEndpoint)?.RoutePattern.RawText
                },
                Success = granted,
                ErrorMessage = granted ? null : "The requested permission was not present in the tenant scope.",
                RiskLevel = granted ? AuditRiskLevel.Medium : AuditRiskLevel.High,
                Category = AuditCategory.Permission,
                CorrelationId = context?.TraceIdentifier
            }).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            // Audit persistence is best-effort and must not alter the permission decision.
            logger.LogError(
                exception,
                "Could not record authorization permission decision for {Permission}",
                permission);
        }

        if (!granted)
        {
            await AnalyzeDenialPatternSafelyAsync(
                    actorUserId ?? subjectUserId,
                    tenantId,
                    permission,
                    context)
                .ConfigureAwait(false);
        }
    }

    private async Task AnalyzeDenialPatternSafelyAsync(
        Guid userId,
        Guid tenantId,
        string permission,
        HttpContext? context)
    {
        var threshold = Math.Max(
            1,
            configuration.GetValue("Authorization:Anomaly:MaxFailedAttemptsPerHour", DefaultDenialAlertThreshold));
        var endDate = SystemClock.UtcNow;
        var startDate = endDate.AddHours(-1);
        var ipAddress = context?.Connection.RemoteIpAddress?.ToString();

        try
        {
            var userDenials = await auditService.GetAuditLogCountAsync(new AuditLogQuery
            {
                UserId = userId,
                TenantId = tenantId,
                ActionType = AuditActionTypes.PermissionDenied,
                ResourceType = "Permission",
                Category = AuditCategory.Permission,
                Success = false,
                StartDate = startDate,
                EndDate = endDate
            }).ConfigureAwait(false);
            var ipDenials = string.IsNullOrWhiteSpace(ipAddress)
                ? 0
                : await auditService.GetAuditLogCountAsync(new AuditLogQuery
                {
                    IpAddress = ipAddress,
                    TenantId = tenantId,
                    ActionType = AuditActionTypes.PermissionDenied,
                    ResourceType = "Permission",
                    Category = AuditCategory.Permission,
                    Success = false,
                    StartDate = startDate,
                    EndDate = endDate
                }).ConfigureAwait(false);

            var denialCount = Math.Max(userDenials, ipDenials);
            if (denialCount < threshold)
            {
                return;
            }

            var alertDimension = userDenials >= ipDenials ? $"user:{userId:D}" : $"ip:{ipAddress}";
            var alertKey = $"authorization-denial:{tenantId:D}:{alertDimension}";
            if (!TryAcquireAlertSlot(alertKey))
            {
                return;
            }

            logger.LogWarning(
                "Repeated permission denials detected: {DenialCount} in the last hour for tenant {TenantId}",
                denialCount,
                tenantId);

            try
            {
                await siemService.SendSecurityEventAsync(new SiemEvent
                {
                    EventType = "PermissionDenialPatternDetected",
                    Severity = denialCount >= threshold * 2 ? SiemSeverity.Critical : SiemSeverity.High,
                    UserId = userId,
                    TenantId = tenantId,
                    IpAddress = ipAddress,
                    UserAgent = context?.Request.Headers.UserAgent.ToString(),
                    Description = $"Repeated permission denials detected within one hour ({denialCount} events).",
                    RiskScore = Math.Min(100, denialCount * 10),
                    CorrelationId = Guid.TryParse(context?.TraceIdentifier, out var correlationId) ? correlationId : null,
                    Metadata = new Dictionary<string, object>
                    {
                        ["permission"] = permission,
                        ["denialCount"] = denialCount,
                        ["threshold"] = threshold,
                        ["windowMinutes"] = 60,
                        ["alertDimension"] = userDenials >= ipDenials ? "user" : "ip"
                    }
                }).ConfigureAwait(false);
            }
            catch
            {
                ReleaseAlertSlot(alertKey);
                throw;
            }
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Could not analyze repeated permission denials for tenant {TenantId}", tenantId);
        }
    }

    private bool TryAcquireAlertSlot(string key)
    {
        lock (AlertCacheLock)
        {
            if (alertCache.TryGetValue(key, out _))
            {
                return false;
            }

            alertCache.Set(key, true, new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1),
                Size = 1
            });
            return true;
        }
    }

    private void ReleaseAlertSlot(string key)
    {
        lock (AlertCacheLock)
        {
            alertCache.Remove(key);
        }
    }

    private static Guid? GetActorUserId(ClaimsPrincipal? principal)
    {
        var value = principal?.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? principal?.FindFirst("sub")?.Value;
        return Guid.TryParse(value, out var userId) ? userId : null;
    }

    private static Guid? GetGuidClaim(ClaimsPrincipal? principal, params string[] claimTypes)
    {
        foreach (var claimType in claimTypes)
        {
            var value = principal?.FindFirst(claimType)?.Value;
            if (Guid.TryParse(value, out var id))
            {
                return id;
            }
        }

        return null;
    }
}
