using System.Text.Json;
using GameGuild.Identity.Context.Actors;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace GameGuild.Compliance.Audit;

/// <summary>Scope constants for custodian authorization.</summary>
public static class CustodianScopes
{
    public const string Access = "access";
    public const string Handle = "handle";
    public const string Transfer = "transfer";
    public const string Acknowledge = "acknowledge";
    public const string Release = "release";
    public const string Certify = "certify";
    public const string Forensic = "forensic";

    public static readonly string[] All = [Access, Handle, Transfer, Acknowledge, Release, Certify, Forensic];
}

public sealed record RegisterCustodianRequest
{
    public Guid UserId { get; init; }
    public required string DisplayName { get; init; }
    public IReadOnlyList<string> Scopes { get; init; } = [CustodianScopes.Access];
}

public sealed record EvidenceCustodianResponse(
    Guid Id,
    Guid UserId,
    string DisplayName,
    IReadOnlyList<string> Scopes,
    bool IsActive,
    Guid AuthorizedByUserId,
    DateTime AuthorizedAtUtc,
    DateTime? RevokedAtUtc,
    string? RevocationReason);

/// <summary>Thrown when a custody operation is attempted without the required custodian authorization.</summary>
public sealed class CustodyUnauthorizedException(string message) : UnauthorizedAccessException(message);

/// <summary>
///     Custodian authorization system (issue #177): a registry of who may access, handle,
///     transfer, acknowledge, release, or certify evidence. Authorization fails closed — an
///     unknown user, a revoked custodian, or a missing scope is denied.
/// </summary>
public interface IEvidenceCustodianService
{
    Task<EvidenceCustodianResponse> RegisterAsync(RegisterCustodianRequest request, CancellationToken cancellationToken);

    Task<EvidenceCustodianResponse> RevokeAsync(Guid custodianId, string reason, CancellationToken cancellationToken);

    Task<IReadOnlyList<EvidenceCustodianResponse>> ListAsync(CancellationToken cancellationToken);

    /// <summary>Fails closed: returns the authorized custodian or throws <see cref="CustodyUnauthorizedException"/>.</summary>
    Task<EvidenceCustodian> RequireScopeAsync(Guid userId, string scope, CancellationToken cancellationToken);

    Task<bool> HasScopeAsync(Guid userId, string scope, CancellationToken cancellationToken);
}

public sealed class EvidenceCustodianService(
    IActorContextAccessor actors,
    IApplicationDbContext context,
    TimeProvider timeProvider,
    ILogger<EvidenceCustodianService> logger) : IEvidenceCustodianService
{
    private static readonly JsonSerializerOptions ScopeJsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<EvidenceCustodianResponse> RegisterAsync(RegisterCustodianRequest request, CancellationToken cancellationToken)
    {
        var (tenantId, userId) = RequireAdministrator();
        var scopes = request.Scopes.Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(scope => scope, StringComparer.OrdinalIgnoreCase).ToList();
        var invalid = scopes.Where(scope => !CustodianScopes.All.Contains(scope, StringComparer.OrdinalIgnoreCase)).ToList();
        if (invalid.Count > 0)
        {
            throw new ValueSnapshotValidationException(new Dictionary<string, string[]>
            {
                ["Scopes"] = [$"Unknown custodian scope(s): {string.Join(", ", invalid)}."]
            });
        }

        if (request.UserId == Guid.Empty || string.IsNullOrWhiteSpace(request.DisplayName))
        {
            throw new ValueSnapshotValidationException(new Dictionary<string, string[]>
            {
                ["UserId"] = ["A user id and display name are required."],
                ["DisplayName"] = ["A user id and display name are required."]
            });
        }

        var existing = await context.Set<EvidenceCustodian>()
            .FirstOrDefaultAsync(candidate => candidate.TenantId == tenantId && candidate.UserId == request.UserId, cancellationToken)
            .ConfigureAwait(false);
        if (existing is not null)
        {
            throw new ValueSnapshotValidationException(new Dictionary<string, string[]>
            {
                ["UserId"] = [$"User {request.UserId} is already a registered custodian; revoke first to re-register."]
            });
        }

        var custodian = new EvidenceCustodian
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            UserId = request.UserId,
            DisplayName = request.DisplayName,
            ScopesJson = JsonSerializer.Serialize(scopes, ScopeJsonOptions),
            AuthorizedByUserId = userId,
            AuthorizedAtUtc = timeProvider.GetUtcNow().UtcDateTime
        };
        context.Set<EvidenceCustodian>().Add(custodian);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Map(custodian);
    }

    public async Task<EvidenceCustodianResponse> RevokeAsync(Guid custodianId, string reason, CancellationToken cancellationToken)
    {
        var (_, userId) = RequireAdministrator();
        var custodian = await context.Set<EvidenceCustodian>()
            .FirstOrDefaultAsync(candidate => candidate.Id == custodianId && candidate.TenantId == (actors.ActorContext.TenantId ?? Guid.Empty), cancellationToken)
            .ConfigureAwait(false)
            ?? throw new CustodyUnauthorizedException("Custodian not found.");

        custodian.RevokedAtUtc = timeProvider.GetUtcNow().UtcDateTime;
        custodian.RevokedByUserId = userId;
        custodian.RevocationReason = reason;
        custodian.Touch();
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Map(custodian);
    }

    public async Task<IReadOnlyList<EvidenceCustodianResponse>> ListAsync(CancellationToken cancellationToken)
    {
        var tenantId = RequireAdministrator().TenantId;
        var custodians = await context.Set<EvidenceCustodian>().AsNoTracking()
            .Where(custodian => custodian.TenantId == tenantId)
            .OrderBy(custodian => custodian.DisplayName)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        return custodians.Select(Map).ToList();
    }

    public async Task<EvidenceCustodian> RequireScopeAsync(Guid userId, string scope, CancellationToken cancellationToken)
    {
        var tenantId = actors.ActorContext.TenantId ?? Guid.Empty;
        var custodian = await context.Set<EvidenceCustodian>().AsNoTracking()
            .FirstOrDefaultAsync(candidate => candidate.TenantId == tenantId && candidate.UserId == userId, cancellationToken)
            .ConfigureAwait(false);

        // Fail closed: no registration, revoked, or missing scope all deny.
        if (custodian is null || custodian.RevokedAtUtc is not null)
        {
            logger.LogWarning("Custody authorization denied for user {User} (scope {Scope}): not an active custodian", userId, scope);
            throw new CustodyUnauthorizedException("The acting user is not an active evidence custodian.");
        }

        if (!DeserializeScopes(custodian.ScopesJson).Contains(scope, StringComparer.OrdinalIgnoreCase))
        {
            logger.LogWarning("Custody authorization denied for custodian {Custodian} (scope {Scope})", custodian.Id, scope);
            throw new CustodyUnauthorizedException($"The acting custodian lacks the '{scope}' scope.");
        }

        return custodian;
    }

    public async Task<bool> HasScopeAsync(Guid userId, string scope, CancellationToken cancellationToken)
    {
        var tenantId = actors.ActorContext.TenantId ?? Guid.Empty;
        var custodian = await context.Set<EvidenceCustodian>().AsNoTracking()
            .FirstOrDefaultAsync(candidate => candidate.TenantId == tenantId && candidate.UserId == userId, cancellationToken)
            .ConfigureAwait(false);
        return custodian is { RevokedAtUtc: null }
            && DeserializeScopes(custodian.ScopesJson).Contains(scope, StringComparer.OrdinalIgnoreCase);
    }

    internal static IReadOnlyList<string> DeserializeScopes(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<List<string>>(json, ScopeJsonOptions) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    internal static EvidenceCustodianResponse Map(EvidenceCustodian custodian) =>
        new(custodian.Id, custodian.UserId, custodian.DisplayName, DeserializeScopes(custodian.ScopesJson),
            custodian.RevokedAtUtc is null, custodian.AuthorizedByUserId, custodian.AuthorizedAtUtc,
            custodian.RevokedAtUtc, custodian.RevocationReason);

    private (Guid TenantId, Guid UserId) RequireAdministrator()
    {
        var actor = actors.ActorContext;
        if (!actor.IsAuthenticated || !actor.IsTenantAdmin || actor.TenantId is not { } tenant || tenant == Guid.Empty
            || actor.SubjectIdAsGuid is not { } user || user == Guid.Empty)
        {
            logger.LogWarning("Custodian registry access denied for actor {Actor}", actor.SubjectIdAsGuid);
            throw new UnauthorizedAccessException("A tenant administrator and tenant context are required.");
        }

        return (tenant, user);
    }
}
