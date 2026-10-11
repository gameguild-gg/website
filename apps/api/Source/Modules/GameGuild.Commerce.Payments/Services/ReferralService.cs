using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace GameGuild.Commerce.Payments;

/// <summary>
///     Referral tracking implementation (issue #410 v1). Reward amounts, currency, cap and
///     the share-link base URL come from <see cref="AccountCreditOptions" /> (single active
///     program, config-driven). The fraud blocks reuse the velocity thinking of the
///     payments fraud gate (#409): windowless same-IP repeat detection over prior referral
///     rows plus a hard referrer cap — both evaluated fail-closed before any reward.
/// </summary>
public sealed class ReferralService(
    IApplicationDbContext context,
    IAccountCreditService creditService,
    IOptions<AccountCreditOptions> options) : IReferralService
{
    /// <inheritdoc />
    public async Task<Result<(string Code, string Link)>> GetOrCreateCodeAsync(Guid tenantId, Guid referrerUserId, CancellationToken cancellationToken = default)
    {
        var anchor = await FindAnchorAsync(tenantId, referrerUserId, cancellationToken).ConfigureAwait(false);
        if (anchor is not null)
        {
            return Result.Success((anchor.ReferralCode, BuildLink(anchor.ReferralCode)));
        }

        for (var attempt = 0; attempt < 5; attempt++)
        {
            var code = GenerateCode();
            var taken = await context.Set<Referral>()
                .AsNoTracking()
                .AnyAsync(referral => referral.TenantId == tenantId && referral.ReferralCode == code, cancellationToken)
                .ConfigureAwait(false);
            if (taken)
            {
                continue;
            }

            var referral = new Referral(tenantId, referrerUserId, code, BuildLink(code));
            context.Set<Referral>().Add(referral);
            try
            {
                await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                return Result.Success((code, BuildLink(code)));
            }
            catch (DbUpdateException)
            {
                // extremely unlikely code collision across tenants; regenerate
            }
        }

        return Result.Failure<(string, string)>(Error.Problem("Referrals.CodeGeneration", "Could not allocate a unique referral code; retry."));
    }

    /// <inheritdoc />
    public async Task<Result<Referral>> RecordClickAsync(Guid tenantId, string referralCode, string? refereeEmail, string? ipHash, CancellationToken cancellationToken = default)
    {
        var referral = await FindByCodeAsync(tenantId, referralCode, requireOpenRow: true, cancellationToken).ConfigureAwait(false);
        if (referral is null)
        {
            return Result.Failure<Referral>(Error.NotFound("Referrals.NotFound", "Referral code was not found."));
        }

        referral.RecordClick(refereeEmail, ipHash, SystemClock.UtcNow);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Result.Success(referral);
    }

    /// <inheritdoc />
    public async Task<Result<ReferralSignupResult>> RecordSignupAsync(Guid tenantId, string referralCode, Guid refereeUserId, string? ipHash, CancellationToken cancellationToken = default)
    {
        var anchor = await FindByCodeAsync(tenantId, referralCode, requireOpenRow: false, cancellationToken).ConfigureAwait(false);
        if (anchor is null)
        {
            return Result.Failure<ReferralSignupResult>(Error.NotFound("Referrals.NotFound", "Referral code was not found."));
        }

        var now = SystemClock.UtcNow;
        var alreadyTracked = await context.Set<Referral>()
            .AsNoTracking()
            .AnyAsync(referral => referral.TenantId == tenantId
                                  && referral.RefereeUserId == refereeUserId
                                  && referral.ReferrerUserId == anchor.ReferrerUserId,
                cancellationToken)
            .ConfigureAwait(false);
        if (alreadyTracked)
        {
            return Result.Failure<ReferralSignupResult>(
                Error.Conflict("Referrals.AlreadyTracked", "This referee is already tracked under this referrer."));
        }

        // Fail-closed cap: never track beyond the configured maximum rewarded referrals.
        var cap = options.Value.MaxReferralsPerUser;
        if (cap.HasValue)
        {
            var tracked = await context.Set<Referral>()
                .AsNoTracking()
                .CountAsync(referral => referral.TenantId == tenantId
                                        && referral.ReferrerUserId == anchor.ReferrerUserId
                                        && referral.RefereeUserId != null,
                    cancellationToken)
                .ConfigureAwait(false);
            if (tracked >= cap.Value)
            {
                return Result.Failure<ReferralSignupResult>(
                    Error.Conflict("Referrals.CapReached", $"Referral cap of {cap.Value} reached for this referrer."));
            }
        }

        // Same-IP fraud block: one rewarded signup per IP per referrer (velocity-style).
        if (!string.IsNullOrEmpty(ipHash))
        {
            var sameIpRewarded = await context.Set<Referral>()
                .AsNoTracking()
                .AnyAsync(referral => referral.TenantId == tenantId
                                      && referral.ReferrerUserId == anchor.ReferrerUserId
                                      && referral.RefereeUserId != null
                                      && referral.RefereeIpHash == ipHash,
                    cancellationToken)
                .ConfigureAwait(false);
            if (sameIpRewarded)
            {
                var blocked = NewTrackingRow(tenantId, anchor, now);
                blocked.MarkFraudulent("Same-IP referral blocked: this address already signed up under this referrer.", now);
                context.Set<Referral>().Add(blocked);
                await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                return Result.Success(new ReferralSignupResult(blocked, Blocked: true,
                    Reason: "Referral recorded but blocked: signup originated from an address already used under this referrer."));
            }
        }

        // Reuse the anchor row when it has no referee yet; otherwise append a tracking row.
        var target = anchor.RefereeUserId is null && anchor.Status is ReferralStatus.Pending or ReferralStatus.Clicked
            ? anchor
            : NewTrackingRow(tenantId, anchor, now);

        target.RecordSignup(refereeUserId, ipHash, now); // flags self-referral internally
        if (target.Status == ReferralStatus.Fraudulent)
        {
            if (!ReferenceEquals(target, anchor))
            {
                context.Set<Referral>().Add(target);
            }

            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return Result.Success(new ReferralSignupResult(target, Blocked: true, Reason: target.FraudReason));
        }

        if (!ReferenceEquals(target, anchor))
        {
            context.Set<Referral>().Add(target);
        }

        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Result.Success(new ReferralSignupResult(target, Blocked: false, Reason: null));
    }

    /// <inheritdoc />
    public async Task<Result> HandleInvoicePaidAsync(Guid tenantId, Guid refereeUserId, Guid subscriptionId, Guid invoiceId, CancellationToken cancellationToken = default)
    {
        // Conversion requires a tracked signup; replays of the same paid-invoice event find
        // no SignedUp row (the referral is already Converted/RewardIssued) and no-op.
        var referral = await context.Set<Referral>()
            .Where(row => row.TenantId == tenantId
                          && row.RefereeUserId == refereeUserId
                          && row.Status == ReferralStatus.SignedUp)
            .OrderBy(row => row.SignupAt)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (referral is null)
        {
            return Result.Success(); // not a referred user (or already handled); nothing to do
        }

        var now = SystemClock.UtcNow;

        // RequirePayment (v1 fail-closed default): rewards only after the first paid invoice,
        // which is exactly what this handler represents.
        referral.RecordConversion(subscriptionId, invoiceId, now);

        var settings = options.Value;
        var rewardCurrency = settings.ReferralRewardCurrency.ToUpperInvariant();

        AccountCreditGrant? referrerGrant = null;
        if (settings.ReferrerRewardAmount > 0)
        {
            var grant = await creditService.GrantAsync(
                new AccountCreditGrantRequest(
                    tenantId,
                    referral.ReferrerUserId,
                    settings.ReferrerRewardAmount,
                    rewardCurrency,
                    AccountCreditSource.Referral,
                    AccountCreditType.General,
                    $"Referral reward: referee {referral.RefereeUserId}",
                    referral.Id,
                    IdempotencyKey: $"referral:{referral.Id}:referrer"),
                cancellationToken).ConfigureAwait(false);
            if (grant.IsFailure)
            {
                return grant;
            }

            referrerGrant = grant.Value;
            AccountCreditMetrics.RecordReferralReward("referrer");
        }

        AccountCreditGrant? refereeGrant = null;
        if (settings.RefereeRewardAmount > 0)
        {
            var grant = await creditService.GrantAsync(
                new AccountCreditGrantRequest(
                    tenantId,
                    refereeUserId,
                    settings.RefereeRewardAmount,
                    rewardCurrency,
                    AccountCreditSource.Referral,
                    AccountCreditType.General,
                    "Referral welcome reward",
                    referral.Id,
                    IdempotencyKey: $"referral:{referral.Id}:referee"),
                cancellationToken).ConfigureAwait(false);
            if (grant.IsFailure)
            {
                return grant;
            }

            refereeGrant = grant.Value;
            AccountCreditMetrics.RecordReferralReward("referee");
        }

        referral.AttachRewards(referrerGrant?.Id, refereeGrant?.Id, now);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Result.Success();
    }

    /// <inheritdoc />
    public async Task<Result<ReferralStats>> GetStatsAsync(Guid tenantId, Guid referrerUserId, CancellationToken cancellationToken = default)
    {
        var anchor = await FindAnchorAsync(tenantId, referrerUserId, cancellationToken).ConfigureAwait(false);
        if (anchor is null)
        {
            return Result.Failure<ReferralStats>(Error.NotFound("Referrals.NotFound", "No referral code exists for this user yet."));
        }

        var counts = await context.Set<Referral>()
            .AsNoTracking()
            .Where(referral => referral.TenantId == tenantId && referral.ReferrerUserId == referrerUserId)
            .GroupBy(referral => referral.Status)
            .Select(grouping => new { Status = grouping.Key, Count = grouping.Count() })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var byStatus = counts.ToDictionary(row => row.Status, row => row.Count);
        var clicked = await context.Set<Referral>()
            .AsNoTracking()
            .CountAsync(referral => referral.TenantId == tenantId
                                    && referral.ReferrerUserId == referrerUserId
                                    && referral.ClickedAt != null,
                cancellationToken)
            .ConfigureAwait(false);

        var stats = new ReferralStats(
            referrerUserId,
            anchor.ReferralCode,
            BuildLink(anchor.ReferralCode),
            Shared: byStatus.GetValueOrDefault(ReferralStatus.Pending)
                     + byStatus.GetValueOrDefault(ReferralStatus.Clicked)
                     + byStatus.GetValueOrDefault(ReferralStatus.SignedUp)
                     + byStatus.GetValueOrDefault(ReferralStatus.Converted)
                     + byStatus.GetValueOrDefault(ReferralStatus.RewardIssued)
                     + byStatus.GetValueOrDefault(ReferralStatus.Expired)
                     + byStatus.GetValueOrDefault(ReferralStatus.Fraudulent),
            Clicked: clicked,
            SignedUp: byStatus.GetValueOrDefault(ReferralStatus.SignedUp),
            Converted: byStatus.GetValueOrDefault(ReferralStatus.Converted),
            RewardIssued: byStatus.GetValueOrDefault(ReferralStatus.RewardIssued),
            Fraudulent: byStatus.GetValueOrDefault(ReferralStatus.Fraudulent),
            Expired: byStatus.GetValueOrDefault(ReferralStatus.Expired));

        return Result.Success(stats);
    }

    /// <inheritdoc />
    public async Task<PagedResult<Referral>> GetHistoryAsync(Guid tenantId, Guid referrerUserId, int skip, int take, CancellationToken cancellationToken = default)
    {
        var query = context.Set<Referral>()
            .AsNoTracking()
            .Where(referral => referral.TenantId == tenantId && referral.ReferrerUserId == referrerUserId);

        var total = await query.CountAsync(cancellationToken).ConfigureAwait(false);
        var items = await query
            .OrderByDescending(referral => referral.CreatedAt)
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return new PagedResult<Referral>(items, total, skip, take);
    }

    private Referral NewTrackingRow(Guid tenantId, Referral anchor, DateTime nowUtc)
        => new(tenantId, anchor.ReferrerUserId, anchor.ReferralCode, anchor.ReferralLink);

    private async Task<Referral?> FindAnchorAsync(Guid tenantId, Guid referrerUserId, CancellationToken cancellationToken)
        => await context.Set<Referral>()
            .Where(referral => referral.TenantId == tenantId && referral.ReferrerUserId == referrerUserId)
            .OrderBy(referral => referral.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

    private async Task<Referral?> FindByCodeAsync(Guid tenantId, string referralCode, bool requireOpenRow, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(referralCode))
        {
            return null;
        }

        var code = referralCode.Trim();
        var query = context.Set<Referral>()
            .Where(referral => referral.TenantId == tenantId && referral.ReferralCode == code);

        if (requireOpenRow)
        {
            query = query.Where(referral => referral.RefereeUserId == null
                                            && referral.Status != ReferralStatus.Fraudulent);
        }

        return await query
            .OrderBy(referral => referral.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    private string BuildLink(string code)
        => $"{options.Value.ReferralLinkBaseUrl.TrimEnd('/')}?ref={code}";

    private static string GenerateCode()
    {
        var bytes = System.Security.Cryptography.RandomNumberGenerator.GetBytes(9);
        return Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }
}
