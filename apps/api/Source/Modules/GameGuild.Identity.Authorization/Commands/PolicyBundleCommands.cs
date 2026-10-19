using GameGuild.CQRS;
using GameGuild.CQRS.Models;
using GameGuild.Identity.Context.Actors;
using Microsoft.Extensions.Logging;

namespace GameGuild.Identity.Authorization;

/// <summary>
///     Command to create a draft policy bundle in the central registry.
/// </summary>
public sealed record CreatePolicyBundleCommand : ICommand<Guid>
{
    /// <summary>Gets the bundle name (unique within its scope).</summary>
    public required string Name { get; init; }

    /// <summary>Gets the optional description.</summary>
    public string? Description { get; init; }

    /// <summary>Gets the tenant scope; null means the global scope.</summary>
    public Guid? TenantId { get; init; }

    /// <summary>Gets the semantic version of the bundle.</summary>
    public required string Version { get; init; }

    /// <summary>Gets the bundle type.</summary>
    public PolicyBundleType BundleType { get; init; } = PolicyBundleType.Composite;

    /// <summary>Gets the policy definitions as a JSON array.</summary>
    public required string PolicyData { get; init; }

    /// <summary>Gets the optional metadata as a JSON object.</summary>
    public string? Metadata { get; init; }

    /// <summary>Gets the optional activation instant.</summary>
    public DateTime? EffectiveFrom { get; init; }

    /// <summary>Gets the optional expiry instant.</summary>
    public DateTime? EffectiveUntil { get; init; }

    /// <summary>Gets the previous version this bundle replaces.</summary>
    public Guid? PreviousVersionId { get; init; }
}

/// <summary>
///     Handler for <see cref="CreatePolicyBundleCommand"/>. Bundles are created as drafts;
///     signing, approval and deployment are separate guarded lifecycle steps.
/// </summary>
public sealed class CreatePolicyBundleCommandHandler(
    IPolicyBundleRepository bundleRepository,
    IPolicyRegistryAuditLogRepository auditRepository,
    IPolicyBundleSignatureService signatureService,
    IActorContextAccessor actorContextAccessor,
    ILogger<CreatePolicyBundleCommandHandler> logger
) : ICommandHandler<CreatePolicyBundleCommand, Guid>
{
    private ActorContext Actor => actorContextAccessor.ActorContext;

    /// <inheritdoc />
    public async Task<Guid> Handle(CreatePolicyBundleCommand request, CancellationToken cancellationToken)
    {
        PolicyBundleRegistryGuards.EnsureCanManageBundles(Actor, request.TenantId, logger, "create");

        var actorId = Actor.SubjectIdAsGuid ?? Guid.Empty;
        var bundle = new PolicyBundle
        {
            Name = request.Name,
            Description = request.Description,
            TenantId = request.TenantId is { } tenantId ? new TenantId(tenantId) : null,
            IsGlobal = request.TenantId is null,
            Version = request.Version,
            BundleType = request.BundleType,
            PolicyData = request.PolicyData,
            Metadata = request.Metadata,
            EffectiveFrom = request.EffectiveFrom,
            EffectiveUntil = request.EffectiveUntil,
            PreviousVersionId = request.PreviousVersionId,
            Status = PolicyBundleStatus.Draft,
            CreatedBy = actorId
        };

        // Fail closed on invalid inputs before anything is persisted.
        var errors = signatureService.ValidateBundleInputs(bundle);
        if (errors.Count > 0)
        {
            throw new PolicyBundleSignatureException(
            $"Policy bundle '{request.Name}' rejected: {string.Join(" ", errors)}");
        }

        await bundleRepository.CreateAsync(bundle, cancellationToken).ConfigureAwait(false);

        await auditRepository.CreateAsync(
            new PolicyRegistryAuditLog
            {
                BundleId = bundle.Id,
                Action = PolicyRegistryAction.Create,
                PerformedBy = actorId,
                Details = $"Created draft bundle '{bundle.Name}' v{bundle.Version}",
                Success = true
            },
            cancellationToken).ConfigureAwait(false);

        logger.LogInformation(
            "Created policy bundle {BundleId} ('{BundleName}') in scope {Scope}",
            bundle.Id, bundle.Name, bundle.IsGlobal ? "global" : bundle.TenantId?.ToString());

        return bundle.Id;
    }
}

/// <summary>
///     Command to sign a policy bundle. <b>System administrators only.</b>
/// </summary>
public sealed record SignPolicyBundleCommand : ICommand<PolicyBundleSignatureInfo>
{
    /// <summary>Gets the bundle to sign.</summary>
    public required Guid BundleId { get; init; }
}

/// <summary>
///     Signature metadata returned by <see cref="SignPolicyBundleCommand"/>.
/// </summary>
public sealed record PolicyBundleSignatureInfo
{
    /// <summary>Gets the signed bundle id.</summary>
    public required Guid BundleId { get; init; }

    /// <summary>Gets the trusted key that produced the signature.</summary>
    public required string KeyId { get; init; }

    /// <summary>Gets the signing time (UTC).</summary>
    public required DateTime SignedAt { get; init; }

    /// <summary>Gets the SHA-256 content hash of the canonical signed payload.</summary>
    public required string ContentHash { get; init; }
}

/// <summary>
///     Handler for <see cref="SignPolicyBundleCommand"/>. Signing is restricted to system
///     administrators and fails closed when bundle inputs violate the signing contract or
///     the active signing key is not trusted.
/// </summary>
public sealed class SignPolicyBundleCommandHandler(
    IPolicyBundleRepository bundleRepository,
    IPolicyRegistryAuditLogRepository auditRepository,
    IPolicyBundleSignatureService signatureService,
    IActorContextAccessor actorContextAccessor,
    ILogger<SignPolicyBundleCommandHandler> logger
) : ICommandHandler<SignPolicyBundleCommand, PolicyBundleSignatureInfo>
{
    private ActorContext Actor => actorContextAccessor.ActorContext;

    /// <inheritdoc />
    public async Task<PolicyBundleSignatureInfo> Handle(
        SignPolicyBundleCommand request,
        CancellationToken cancellationToken)
    {
        // SECURITY: signing is system-admin-only. Tenant admins manage bundles but never sign them.
        if (!Actor.IsAuthenticated || !Actor.IsSystemAdmin)
        {
            logger.LogWarning(
                "Actor {ActorId} attempted to sign policy bundle {BundleId} without system administration",
                Actor.SubjectId, request.BundleId);

            throw new UnauthorizedAccessException("Signing policy bundles requires system administration.");
        }

        var actorId = Actor.SubjectIdAsGuid ?? Guid.Empty;
        var bundle = await bundleRepository.GetByIdAsync(request.BundleId, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Policy bundle {request.BundleId} does not exist.");

        if (bundle.Status is PolicyBundleStatus.Deprecated or PolicyBundleStatus.Revoked)
        {
            throw new InvalidOperationException(
            $"Policy bundle {request.BundleId} is {bundle.Status} and can no longer be signed.");
        }

        signatureService.SignBundle(bundle, actorId);
        await bundleRepository.UpdateAsync(bundle, cancellationToken).ConfigureAwait(false);

        await auditRepository.CreateAsync(
            new PolicyRegistryAuditLog
            {
                BundleId = bundle.Id,
                Action = PolicyRegistryAction.Sign,
                PerformedBy = actorId,
                Details = $"Signed bundle '{bundle.Name}' with key '{bundle.SignedBy}'",
                Success = true
            },
            cancellationToken).ConfigureAwait(false);

        return new PolicyBundleSignatureInfo
        {
            BundleId = bundle.Id,
            KeyId = bundle.SignedBy ?? string.Empty,
            SignedAt = bundle.SignedAt ?? SystemClock.UtcNow,
            ContentHash = bundle.ContentHash
        };
    }
}

/// <summary>
///     Command to approve a signed policy bundle. <b>System administrators only</b>, and the
///     signature must verify (fail closed) before the status can become Approved.
/// </summary>
public sealed record ApprovePolicyBundleCommand : ICommand<Guid>
{
    /// <summary>Gets the bundle to approve.</summary>
    public required Guid BundleId { get; init; }
}

/// <summary>
///     Handler for <see cref="ApprovePolicyBundleCommand"/>.
/// </summary>
public sealed class ApprovePolicyBundleCommandHandler(
    IPolicyBundleRepository bundleRepository,
    IPolicyRegistryAuditLogRepository auditRepository,
    IPolicyBundleSignatureService signatureService,
    IActorContextAccessor actorContextAccessor,
    ILogger<ApprovePolicyBundleCommandHandler> logger
) : ICommandHandler<ApprovePolicyBundleCommand, Guid>
{
    private ActorContext Actor => actorContextAccessor.ActorContext;

    /// <inheritdoc />
    public async Task<Guid> Handle(ApprovePolicyBundleCommand request, CancellationToken cancellationToken)
    {
        if (!Actor.IsAuthenticated || !Actor.IsSystemAdmin)
        {
            logger.LogWarning(
                "Actor {ActorId} attempted to approve policy bundle {BundleId} without system administration",
                Actor.SubjectId, request.BundleId);

            throw new UnauthorizedAccessException("Approving policy bundles requires system administration.");
        }

        var actorId = Actor.SubjectIdAsGuid ?? Guid.Empty;
        var bundle = await bundleRepository.GetByIdAsync(request.BundleId, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Policy bundle {request.BundleId} does not exist.");

        // FAIL CLOSED: an approved bundle must carry a valid signature from a trusted key.
        var verification = signatureService.VerifyBundle(bundle);
        if (!verification.IsValid)
        {
            throw new PolicyBundleSignatureException(
            $"Policy bundle {request.BundleId} cannot be approved: signature verification failed ({verification.Reason}).");
        }

        bundle.Status = PolicyBundleStatus.Approved;
        bundle.ApprovedBy = actorId;
        bundle.ApprovedAt = SystemClock.UtcNow;
        await bundleRepository.UpdateAsync(bundle, cancellationToken).ConfigureAwait(false);

        await auditRepository.CreateAsync(
            new PolicyRegistryAuditLog
            {
                BundleId = bundle.Id,
                Action = PolicyRegistryAction.Approve,
                PerformedBy = actorId,
                Details = $"Approved bundle '{bundle.Name}' (verified by key '{verification.KeyId}')",
                Success = true
            },
            cancellationToken).ConfigureAwait(false);

        return bundle.Id;
    }
}

/// <summary>
///     Command to deploy an approved policy bundle. The bundle's verified policy definitions
///     are materialized for the dynamic authorization policy provider and the tenant security
///     versions are bumped so cached policies invalidate.
/// </summary>
public sealed record DeployPolicyBundleCommand : ICommand<Guid>
{
    /// <summary>Gets the bundle to deploy.</summary>
    public required Guid BundleId { get; init; }

    /// <summary>Gets the target environment name (e.g. "Production").</summary>
    public string Environment { get; init; } = "Production";

    /// <summary>Gets the optional deployment notes.</summary>
    public string? Notes { get; init; }
}

/// <summary>
///     Handler for <see cref="DeployPolicyBundleCommand"/>.
/// </summary>
public sealed class DeployPolicyBundleCommandHandler(
    IPolicyBundleRepository bundleRepository,
    IPolicyBundleDeploymentRepository deploymentRepository,
    IPolicyRegistryAuditLogRepository auditRepository,
    ISignedPolicyBundleStore signedStore,
    IPolicyBundlePolicyMaterializer materializer,
    ITenantSecurityVersionStore securityVersionStore,
    IActorContextAccessor actorContextAccessor,
    ILogger<DeployPolicyBundleCommandHandler> logger
) : ICommandHandler<DeployPolicyBundleCommand, Guid>
{
    private ActorContext Actor => actorContextAccessor.ActorContext;

    /// <inheritdoc />
    public async Task<Guid> Handle(DeployPolicyBundleCommand request, CancellationToken cancellationToken)
    {
        if (!Actor.IsAuthenticated || !Actor.IsSystemAdmin)
        {
            logger.LogWarning(
                "Actor {ActorId} attempted to deploy policy bundle {BundleId} without system administration",
                Actor.SubjectId, request.BundleId);

            throw new UnauthorizedAccessException("Deploying policy bundles requires system administration.");
        }

        var actorId = Actor.SubjectIdAsGuid ?? Guid.Empty;

        // FAIL CLOSED: the signed store refuses unsigned bundles and bundles whose signature
        // no longer verifies (e.g. key revoked or content tampered).
        var bundle = await signedStore
            .GetVerifiedBundleAsync(request.BundleId, allowUnsignedDraft: false, cancellationToken)
            .ConfigureAwait(false);

        if (bundle.Status != PolicyBundleStatus.Approved)
        {
            throw new InvalidOperationException(
            $"Policy bundle {request.BundleId} is {bundle.Status}; only approved bundles can be deployed.");
        }

        var deployment = new PolicyBundleDeployment
        {
            BundleId = bundle.Id,
            TenantId = bundle.TenantId,
            Environment = string.IsNullOrWhiteSpace(request.Environment) ? "Production" : request.Environment,
            Status = PolicyDeploymentStatus.Pending,
            DeployedBy = actorId,
            VerificationPassed = true,
            DeploymentNotes = request.Notes
        };

        // Materialize first: any policy parse failure throws before statuses change.
        await materializer.MaterializeAsync(bundle, cancellationToken).ConfigureAwait(false);

        deployment.Activate();
        await deploymentRepository.CreateAsync(deployment, cancellationToken).ConfigureAwait(false);

        bundle.Status = PolicyBundleStatus.Active;
        bundle.DeploymentCount++;
        bundle.LastDeployedAt = SystemClock.UtcNow;
        await bundleRepository.UpdateAsync(bundle, cancellationToken).ConfigureAwait(false);

        // Invalidate policy caches: bump the tenant version (and the global version for
        // tenant bundles, whose merged definitions include the global base).
        var tenantScope = bundle.TenantId?.Value ?? Guid.Empty;
        await securityVersionStore
            .IncrementVersionAsync(tenantScope.ToString(), cancellationToken)
            .ConfigureAwait(false);
        if (tenantScope != Guid.Empty)
        {
            await securityVersionStore
                .IncrementVersionAsync(Guid.Empty.ToString(), cancellationToken)
                .ConfigureAwait(false);
        }

        await auditRepository.CreateAsync(
            new PolicyRegistryAuditLog
            {
                BundleId = bundle.Id,
                Action = PolicyRegistryAction.Deploy,
                PerformedBy = actorId,
                Details = $"Deployed bundle '{bundle.Name}' to '{deployment.Environment}' (deployment {deployment.Id})",
                Success = true
            },
            cancellationToken).ConfigureAwait(false);

        logger.LogInformation(
            "Deployed policy bundle {BundleId} ('{BundleName}') to {Environment}",
            bundle.Id, bundle.Name, deployment.Environment);

        return deployment.Id;
    }
}

/// <summary>
///     Command to roll back a policy bundle deployment. Materialized policy definitions are
///     removed and the bundle is deprecated. <b>System administrators only.</b>
/// </summary>
public sealed record RollbackPolicyBundleDeploymentCommand : ICommand<bool>
{
    /// <summary>Gets the deployment to roll back.</summary>
    public required Guid DeploymentId { get; init; }

    /// <summary>Gets the rollback reason.</summary>
    public required string Reason { get; init; }
}

/// <summary>
///     Handler for <see cref="RollbackPolicyBundleDeploymentCommand"/>.
/// </summary>
public sealed class RollbackPolicyBundleDeploymentCommandHandler(
    IPolicyBundleRepository bundleRepository,
    IPolicyBundleDeploymentRepository deploymentRepository,
    IPolicyRegistryAuditLogRepository auditRepository,
    IPolicyBundlePolicyMaterializer materializer,
    ITenantSecurityVersionStore securityVersionStore,
    IActorContextAccessor actorContextAccessor,
    ILogger<RollbackPolicyBundleDeploymentCommandHandler> logger
) : ICommandHandler<RollbackPolicyBundleDeploymentCommand, bool>
{
    private ActorContext Actor => actorContextAccessor.ActorContext;

    /// <inheritdoc />
    public async Task<bool> Handle(
        RollbackPolicyBundleDeploymentCommand request,
        CancellationToken cancellationToken)
    {
        if (!Actor.IsAuthenticated || !Actor.IsSystemAdmin)
        {
            logger.LogWarning(
                "Actor {ActorId} attempted to roll back policy bundle deployment {DeploymentId} without system administration",
                Actor.SubjectId, request.DeploymentId);

            throw new UnauthorizedAccessException("Rolling back policy bundles requires system administration.");
        }

        var actorId = Actor.SubjectIdAsGuid ?? Guid.Empty;
        var deployment = await deploymentRepository
            .GetByIdAsync(request.DeploymentId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Policy bundle deployment {request.DeploymentId} does not exist.");

        if (deployment.Status != PolicyDeploymentStatus.Active)
        {
            throw new InvalidOperationException(
            $"Deployment {request.DeploymentId} is {deployment.Status}; only active deployments can be rolled back.");
        }

        var bundle = await bundleRepository
            .GetByIdAsync(deployment.BundleId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new InvalidOperationException(
                $"Policy bundle {deployment.BundleId} for deployment {request.DeploymentId} does not exist.");

        await materializer.DematerializeAsync(bundle, cancellationToken).ConfigureAwait(false);

        deployment.Rollback(actorId, request.Reason);
        await deploymentRepository.UpdateAsync(deployment, cancellationToken).ConfigureAwait(false);

        bundle.Status = PolicyBundleStatus.Deprecated;
        await bundleRepository.UpdateAsync(bundle, cancellationToken).ConfigureAwait(false);

        var tenantScope = bundle.TenantId?.Value ?? Guid.Empty;
        await securityVersionStore
            .IncrementVersionAsync(tenantScope.ToString(), cancellationToken)
            .ConfigureAwait(false);
        if (tenantScope != Guid.Empty)
        {
            await securityVersionStore
                .IncrementVersionAsync(Guid.Empty.ToString(), cancellationToken)
                .ConfigureAwait(false);
        }

        await auditRepository.CreateAsync(
            new PolicyRegistryAuditLog
            {
                BundleId = bundle.Id,
                Action = PolicyRegistryAction.Rollback,
                PerformedBy = actorId,
                Details = $"Rolled back deployment {deployment.Id} of '{bundle.Name}': {request.Reason}",
                Success = true
            },
            cancellationToken).ConfigureAwait(false);

        return true;
    }
}

/// <summary>
///     Query to list policy bundles visible to the acting administrator.
/// </summary>
public sealed record ListPolicyBundlesQuery : IQuery<IReadOnlyList<PolicyBundleSummary>>
{
    /// <summary>Gets the tenant scope filter; null lists the global scope.</summary>
    public Guid? TenantId { get; init; }
}

/// <summary>
///     Registry summary projection of a policy bundle.
/// </summary>
public sealed record PolicyBundleSummary
{
    /// <summary>Gets the bundle id.</summary>
    public required Guid Id { get; init; }

    /// <summary>Gets the bundle name.</summary>
    public required string Name { get; init; }

    /// <summary>Gets the semantic version.</summary>
    public required string Version { get; init; }

    /// <summary>Gets the tenant scope, or null for global.</summary>
    public Guid? TenantId { get; init; }

    /// <summary>Gets a value indicating whether the bundle is global.</summary>
    public bool IsGlobal { get; init; }

    /// <summary>Gets the lifecycle status.</summary>
    public PolicyBundleStatus Status { get; init; }

    /// <summary>Gets a value indicating whether the bundle carries a signature.</summary>
    public bool IsSigned { get; init; }

    /// <summary>Gets the trusted key that signed the bundle, when signed.</summary>
    public string? SignedBy { get; init; }

    /// <summary>Gets the signing time (UTC), when signed.</summary>
    public DateTime? SignedAt { get; init; }

    /// <summary>Gets the creation time (UTC).</summary>
    public DateTime CreatedAt { get; init; }
}

/// <summary>
///     Handler for <see cref="ListPolicyBundlesQuery"/>.
/// </summary>
public sealed class ListPolicyBundlesQueryHandler(
    IPolicyBundleRepository bundleRepository,
    IActorContextAccessor actorContextAccessor,
    ILogger<ListPolicyBundlesQueryHandler> logger
) : IQueryHandler<ListPolicyBundlesQuery, IReadOnlyList<PolicyBundleSummary>>
{
    private ActorContext Actor => actorContextAccessor.ActorContext;

    /// <inheritdoc />
    public async Task<IReadOnlyList<PolicyBundleSummary>> Handle(
        ListPolicyBundlesQuery request,
        CancellationToken cancellationToken)
    {
        PolicyBundleRegistryGuards.EnsureCanViewBundles(Actor, request.TenantId, logger);

        // System administrators may inspect the global registry; tenant administrators
        // are constrained to their own tenant scope.
        var scope = Actor.IsSystemAdmin ? request.TenantId : Actor.TenantId;
        var bundles = await bundleRepository.GetByTenantAsync(scope, cancellationToken).ConfigureAwait(false);

        return bundles
            .Select(b => new PolicyBundleSummary
            {
                Id = b.Id,
                Name = b.Name,
                Version = b.Version,
                TenantId = b.TenantId?.Value,
                IsGlobal = b.IsGlobal,
                Status = b.Status,
                IsSigned = !string.IsNullOrWhiteSpace(b.DigitalSignature),
                SignedBy = b.SignedBy,
                SignedAt = b.SignedAt,
                CreatedAt = b.CreatedAt
            })
            .ToList();
    }
}

/// <summary>
///     Shared guards for policy bundle registry operations.
/// </summary>
internal static class PolicyBundleRegistryGuards
{
    public static void EnsureCanManageBundles(
        ActorContext actor,
        Guid? tenantId,
        ILogger logger,
        string operation)
    {
        if (!actor.IsAuthenticated)
        {
            throw new UnauthorizedAccessException("User is not authenticated.");
        }

        if (actor.IsSystemAdmin)
        {
            return;
        }

        if (tenantId is null)
        {
            logger.LogWarning(
                "Actor {ActorId} attempted to {Operation} a global policy bundle without system administration",
                actor.SubjectId, operation);

            throw new UnauthorizedAccessException(
                $"Global policy bundle {operation} requires system administration.");
        }

        if (tenantId.Value != actor.TenantId || !actor.IsTenantAdmin)
        {
            logger.LogWarning(
                "Actor {ActorId} attempted to {Operation} a policy bundle in tenant {TenantId} without tenant administration",
                actor.SubjectId, operation, tenantId.Value);

            throw new UnauthorizedAccessException(
                $"Policy bundle {operation} requires tenant administration of the target tenant or system administration.");
        }
    }

    public static void EnsureCanViewBundles(ActorContext actor, Guid? tenantId, ILogger logger)
    {
        if (!actor.IsAuthenticated)
        {
            throw new UnauthorizedAccessException("User is not authenticated.");
        }

        if (actor.IsSystemAdmin)
        {
            return;
        }

        if ((tenantId ?? actor.TenantId) != actor.TenantId || !actor.IsTenantAdmin)
        {
            logger.LogWarning(
                "Actor {ActorId} attempted to list policy bundles outside tenant {TenantId}",
                actor.SubjectId, actor.TenantId);

            throw new UnauthorizedAccessException(
                "Listing policy bundles requires tenant administration of the requested scope or system administration.");
        }
    }
}
