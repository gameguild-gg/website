using FluentValidation;
using GameGuild.CQRS;
using GameGuild.Identity.Authorization;
using GameGuild.Identity.Context.Actors;

namespace GameGuild.Commerce.Payments;

// ── Self-service (actor-scoped: user and tenant derived from the actor, never the body) ──

/// <summary>Gets the caller's per-currency spendable credit balances.</summary>
[AuthorizeRequest("")]
public sealed record GetMyCreditBalancesQuery : IQuery<IReadOnlyList<AccountCreditBalance>>;

/// <summary>Gets the caller's paged credit ledger history.</summary>
[AuthorizeRequest("")]
public sealed record GetMyCreditHistoryQuery(int Skip = 0, int Take = 20) : IQuery<PagedResult<AccountCreditTransaction>>;

// ── Administration (fail-closed SystemAdmin) ──

/// <summary>Manually issues credit to a tenant user (operator action, audited by the ledger).</summary>
[AuthorizeRequest("", RequireSystemAdmin = true)]
public sealed record IssueAccountCreditCommand(
    Guid UserId,
    decimal Amount,
    string Currency,
    AccountCreditType CreditType,
    string Description,
    DateTime? ExpiresAt = null,
    string? IdempotencyKey = null) : ICommand<Result<AccountCreditGrant>>;

/// <summary>Revokes the remaining balance of a credit grant with an auditable reason.</summary>
[AuthorizeRequest("", RequireSystemAdmin = true)]
public sealed record RevokeAccountCreditCommand(Guid GrantId, string Reason) : ICommand<Result>;

/// <summary>Growth metrics (issue #410): credit issuance/usage/expiry plus the referral funnel and leaderboard.</summary>
[AuthorizeRequest("", RequireSystemAdmin = true)]
public sealed record GetAccountCreditMetricsQuery(Guid? TenantId = null) : IQuery<AccountCreditMetricsReport>;

// ── Handlers ──

internal static class AccountCreditActor
{
    public static (Guid UserId, Guid TenantId) RequireUserAndTenant(IActorContextAccessor actorContextAccessor)
    {
        var actor = actorContextAccessor.ActorContext;
        if (!actor.IsAuthenticated || !actor.SubjectIdAsGuid.HasValue)
        {
            throw new UnauthorizedAccessException("An authenticated user context is required.");
        }

        if (!actor.TenantId.HasValue)
        {
            throw new UnauthorizedAccessException("A tenant context is required.");
        }

        return (actor.SubjectIdAsGuid.Value, actor.TenantId.Value);
    }

    public static Guid RequireAdminTenant(IActorContextAccessor actorContextAccessor)
    {
        var actor = actorContextAccessor.ActorContext;
        if (!actor.IsAuthenticated || !actor.TenantId.HasValue)
        {
            throw new UnauthorizedAccessException("An authenticated tenant context is required.");
        }

        return actor.TenantId.Value;
    }
}

public sealed class GetMyCreditBalancesQueryHandler(
    IAccountCreditService creditService,
    IActorContextAccessor actorContextAccessor) : IQueryHandler<GetMyCreditBalancesQuery, IReadOnlyList<AccountCreditBalance>>
{
    public Task<IReadOnlyList<AccountCreditBalance>> Handle(GetMyCreditBalancesQuery request, CancellationToken cancellationToken)
    {
        var (userId, tenantId) = AccountCreditActor.RequireUserAndTenant(actorContextAccessor);
        return creditService.GetBalancesAsync(tenantId, userId, cancellationToken);
    }
}

public sealed class GetMyCreditHistoryQueryHandler(
    IAccountCreditService creditService,
    IActorContextAccessor actorContextAccessor) : IQueryHandler<GetMyCreditHistoryQuery, PagedResult<AccountCreditTransaction>>
{
    public Task<PagedResult<AccountCreditTransaction>> Handle(GetMyCreditHistoryQuery request, CancellationToken cancellationToken)
    {
        var (userId, tenantId) = AccountCreditActor.RequireUserAndTenant(actorContextAccessor);
        var skip = Math.Max(0, request.Skip);
        var take = Math.Clamp(request.Take, 1, 100);
        return creditService.GetHistoryAsync(tenantId, userId, skip, take, cancellationToken);
    }
}

public sealed class IssueAccountCreditCommandHandler(
    IAccountCreditService creditService,
    IActorContextAccessor actorContextAccessor) : ICommandHandler<IssueAccountCreditCommand, Result<AccountCreditGrant>>
{
    public Task<Result<AccountCreditGrant>> Handle(IssueAccountCreditCommand request, CancellationToken cancellationToken)
    {
        // Fail closed: the tenant is derived from the admin actor context, never the body.
        var tenantId = AccountCreditActor.RequireAdminTenant(actorContextAccessor);
        return creditService.GrantAsync(
            new AccountCreditGrantRequest(
                tenantId,
                request.UserId,
                request.Amount,
                request.Currency,
                AccountCreditSource.Manual,
                request.CreditType,
                request.Description,
                ExpiresAt: request.ExpiresAt,
                IdempotencyKey: request.IdempotencyKey),
            cancellationToken);
    }
}

public sealed class RevokeAccountCreditCommandHandler(
    IAccountCreditService creditService,
    IActorContextAccessor actorContextAccessor) : ICommandHandler<RevokeAccountCreditCommand, Result>
{
    public Task<Result> Handle(RevokeAccountCreditCommand request, CancellationToken cancellationToken)
    {
        var tenantId = AccountCreditActor.RequireAdminTenant(actorContextAccessor);
        return creditService.RevokeAsync(tenantId, request.GrantId, request.Reason, cancellationToken);
    }
}

// ── Validators ──

public sealed class IssueAccountCreditCommandValidator : AbstractValidator<IssueAccountCreditCommand>
{
    public IssueAccountCreditCommandValidator()
    {
        RuleFor(command => command.UserId).NotEmpty();
        RuleFor(command => command.Amount).GreaterThan(0);
        RuleFor(command => command.Currency)
            .NotEmpty()
            .Length(3)
            .Matches("^[A-Za-z]{3}$");
        RuleFor(command => command.Description).NotEmpty().MaximumLength(500);
        RuleFor(command => command.IdempotencyKey).MaximumLength(200);
    }
}

public sealed class RevokeAccountCreditCommandValidator : AbstractValidator<RevokeAccountCreditCommand>
{
    public RevokeAccountCreditCommandValidator()
    {
        RuleFor(command => command.GrantId).NotEmpty();
        RuleFor(command => command.Reason).NotEmpty().MaximumLength(500);
    }
}
