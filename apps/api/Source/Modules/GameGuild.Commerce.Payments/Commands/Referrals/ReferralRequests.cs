using FluentValidation;
using GameGuild.CQRS;
using GameGuild.Identity.Authorization;
using GameGuild.Identity.Context.Actors;

namespace GameGuild.Commerce.Payments;

/// <summary>The caller's referral code and trackable share link (created on first use).</summary>
[AuthorizeRequest("")]
public sealed record GetMyReferralCodeQuery : IQuery<Result<(string Code, string Link)>>;

/// <summary>Funnel stats of the caller's referrals.</summary>
[AuthorizeRequest("")]
public sealed record GetMyReferralStatsQuery : IQuery<Result<ReferralStats>>;

/// <summary>Tracking rows of the caller's referrals, newest first (paged).</summary>
[AuthorizeRequest("")]
public sealed record GetMyReferralsQuery(int Skip = 0, int Take = 20) : IQuery<PagedResult<Referral>>;

/// <summary>Records a referral link click (authenticated tracking surface for v1).</summary>
[AuthorizeRequest("")]
public sealed record TrackReferralClickCommand(string ReferralCode, string? RefereeEmail = null) : ICommand<Result<Referral>>;

/// <summary>The signed-up caller applies a referral code; fraud blocks apply fail-closed.</summary>
[AuthorizeRequest("")]
public sealed record TrackReferralSignupCommand(string ReferralCode) : ICommand<Result<ReferralSignupResult>>;

public sealed class GetMyReferralCodeQueryHandler(
    IReferralService referralService,
    IActorContextAccessor actorContextAccessor) : IQueryHandler<GetMyReferralCodeQuery, Result<(string Code, string Link)>>
{
    public Task<Result<(string Code, string Link)>> Handle(GetMyReferralCodeQuery request, CancellationToken cancellationToken)
    {
        var (userId, tenantId) = AccountCreditActor.RequireUserAndTenant(actorContextAccessor);
        return referralService.GetOrCreateCodeAsync(tenantId, userId, cancellationToken);
    }
}

public sealed class GetMyReferralStatsQueryHandler(
    IReferralService referralService,
    IActorContextAccessor actorContextAccessor) : IQueryHandler<GetMyReferralStatsQuery, Result<ReferralStats>>
{
    public Task<Result<ReferralStats>> Handle(GetMyReferralStatsQuery request, CancellationToken cancellationToken)
    {
        var (userId, tenantId) = AccountCreditActor.RequireUserAndTenant(actorContextAccessor);
        return referralService.GetStatsAsync(tenantId, userId, cancellationToken);
    }
}

public sealed class GetMyReferralsQueryHandler(
    IReferralService referralService,
    IActorContextAccessor actorContextAccessor) : IQueryHandler<GetMyReferralsQuery, PagedResult<Referral>>
{
    public Task<PagedResult<Referral>> Handle(GetMyReferralsQuery request, CancellationToken cancellationToken)
    {
        var (userId, tenantId) = AccountCreditActor.RequireUserAndTenant(actorContextAccessor);
        var skip = Math.Max(0, request.Skip);
        var take = Math.Clamp(request.Take, 1, 100);
        return referralService.GetHistoryAsync(tenantId, userId, skip, take, cancellationToken);
    }
}

/// <summary>
///     Derives the caller IP hash for the fraud blocks. The IP never lands in the ledger
///     in clear text — only the SHA-256 hash is persisted (same-IP detection only).
/// </summary>
public static class ReferralIpHasher
{
    /// <summary>SHA-256 hex hash of the caller IP, or null when no remote address exists.</summary>
    public static string? HashRemoteIpAddress(this Microsoft.AspNetCore.Http.HttpContext? httpContext)
    {
        var remote = httpContext?.Connection?.RemoteIpAddress?.ToString();
        return string.IsNullOrEmpty(remote) ? null : GiftRedemptionCodeGenerator.HashCode(remote);
    }
}

public sealed class TrackReferralClickCommandHandler(
    IReferralService referralService,
    IActorContextAccessor actorContextAccessor,
    Microsoft.AspNetCore.Http.IHttpContextAccessor httpContextAccessor) : ICommandHandler<TrackReferralClickCommand, Result<Referral>>
{
    public Task<Result<Referral>> Handle(TrackReferralClickCommand request, CancellationToken cancellationToken)
    {
        var (_, tenantId) = AccountCreditActor.RequireUserAndTenant(actorContextAccessor);
        var ipHash = httpContextAccessor.HttpContext.HashRemoteIpAddress();
        return referralService.RecordClickAsync(tenantId, request.ReferralCode, request.RefereeEmail, ipHash, cancellationToken);
    }
}

public sealed class TrackReferralSignupCommandHandler(
    IReferralService referralService,
    IActorContextAccessor actorContextAccessor,
    Microsoft.AspNetCore.Http.IHttpContextAccessor httpContextAccessor) : ICommandHandler<TrackReferralSignupCommand, Result<ReferralSignupResult>>
{
    public Task<Result<ReferralSignupResult>> Handle(TrackReferralSignupCommand request, CancellationToken cancellationToken)
    {
        var (userId, tenantId) = AccountCreditActor.RequireUserAndTenant(actorContextAccessor);
        var ipHash = httpContextAccessor.HttpContext.HashRemoteIpAddress();
        return referralService.RecordSignupAsync(tenantId, request.ReferralCode, userId, ipHash, cancellationToken);
    }
}

public sealed class TrackReferralClickCommandValidator : AbstractValidator<TrackReferralClickCommand>
{
    public TrackReferralClickCommandValidator()
    {
        RuleFor(command => command.ReferralCode).NotEmpty().MaximumLength(32);
        RuleFor(command => command.RefereeEmail).MaximumLength(255);
    }
}

public sealed class TrackReferralSignupCommandValidator : AbstractValidator<TrackReferralSignupCommand>
{
    public TrackReferralSignupCommandValidator()
    {
        RuleFor(command => command.ReferralCode).NotEmpty().MaximumLength(32);
    }
}
