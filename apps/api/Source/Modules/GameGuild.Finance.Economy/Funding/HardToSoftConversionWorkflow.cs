using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using GameGuild.Finance.Economy.Contracts;
using GameGuild.Finance.Economy.Persistence;
using GameGuild.Finance.Economy.Risk;
using GameGuild.Identity.Context.Actors;
using Microsoft.EntityFrameworkCore;

namespace GameGuild.Finance.Economy.Funding;

public sealed record SelfServiceHardToSoftConversionRequest(
    long PrincipalHardCoinUnits,
    string IdempotencyKey);

public sealed record SelfServiceHardToSoftConversionReceipt(
    Guid PrincipalPostingId,
    Guid? FeePostingId,
    long JournalSequence,
    string JournalHash,
    bool IsDuplicate);

public interface IHardToSoftConversionWorkflow
{
    Task<SelfServiceHardToSoftConversionReceipt> ConvertAsync(
        SelfServiceHardToSoftConversionRequest request,
        CancellationToken cancellationToken);
}

/// <summary>
/// Composes only self-service conversion inputs. The security-definer writer resolves
/// the active capability and verifies the durable risk decision, caller ownership,
/// idempotency key, and exact FIFO root set atomically with the posting.
/// </summary>
public sealed class PostgreSqlHardToSoftConversionWorkflow(
    IApplicationDbContext context,
    IActorContextAccessor actorContextAccessor,
    IEconomyValueMovementDecisionGate decisionGate,
    IHardToSoftConversionRiskEvidenceVerifier riskEvidenceVerifier,
    IHardToSoftConversionPolicyResolver policyResolver,
    IHardToSoftConversionRiskDecisionIssuer riskDecisionIssuer) : IHardToSoftConversionWorkflow
{
    public async Task<SelfServiceHardToSoftConversionReceipt> ConvertAsync(
        SelfServiceHardToSoftConversionRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        decisionGate.EnsureEnabled(EconomyValueMovementCapability.ConvertHardToSoft);

        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(request.PrincipalHardCoinUnits);

        var key = new IdempotencyKey(request.IdempotencyKey);
        var actor = actorContextAccessor.ActorContext;
        if (!actor.IsAuthenticated || actor.SubjectIdAsGuid is not { } actorId || actor.TenantId is not { } tenantId)
            throw new UnauthorizedAccessException("Economy conversion requires an authenticated user and tenant context.");

        var requestedAt = DateTimeOffset.UtcNow;
        var policy = await policyResolver.ResolveAsync(
            tenantId,
            actorId,
            request.PrincipalHardCoinUnits,
            requestedAt,
            cancellationToken);
        var feeHardCoinUnits = policy.FeeHardCoinUnits;

        var externalEvidence = await riskEvidenceVerifier
            .VerifyAsync(actorId, tenantId, cancellationToken)
            .ConfigureAwait(false);

        var walletId = await context.Set<EconomyWalletRow>()
            .AsNoTracking()
            .Where(row => row.OwnerId == actorId && row.TenantId == tenantId && row.State == WalletLifecycleState.Active)
            .Select(row => (Guid?)row.Id)
            .SingleOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        if (walletId is null)
            throw new EconomySelfServiceCommandRejectedException("The authenticated user has no active Economy wallet.");

        var principalPostingId = DeterministicGuid("hard-to-soft:principal", key.Value);
        var feePostingId = feeHardCoinUnits == 0
            ? (Guid?)null
            : DeterministicGuid("hard-to-soft:fee", key.Value);
        var outputLotId = DeterministicGuid("hard-to-soft:output", key.Value);

        async Task<SelfServiceHardToSoftConversionReceipt> IssueAndPostAsync()
        {
            var decision = await riskDecisionIssuer.IssueAsync(
                    new HardToSoftConversionRiskDecisionRequest(
                        actorId,
                        tenantId,
                        new WalletId(walletId.Value),
                        principalPostingId,
                        key,
                        feeHardCoinUnits,
                        checked(request.PrincipalHardCoinUnits + feeHardCoinUnits),
                        policy.MaximumHardCoinUnitsPerDay,
                        policy.DecisionLifetimeSeconds,
                        policy.JurisdictionCode,
                        policy.PolicyVersion,
                        policy.PolicyHash,
                        externalEvidence,
                        requestedAt),
                    cancellationToken)
                .ConfigureAwait(false);

            var receipt = await context.Set<RegisteredPostingReceiptRow>()
                .FromSqlInterpolated($"""
                    SELECT *
                    FROM economy_private.post_self_service_hard_to_soft_conversion_v1(
                        {actorId},
                        {tenantId},
                        {principalPostingId},
                        {feePostingId},
                        {key.Value},
                        {decision.Id},
                        {walletId.Value},
                        {outputLotId},
                        {decision.SourceRoots.ToArray()},
                        {request.PrincipalHardCoinUnits},
                        {feeHardCoinUnits},
                        {requestedAt},
                        {null})
                    """)
                .AsNoTracking()
                .SingleAsync(cancellationToken)
                .ConfigureAwait(false);

            return new SelfServiceHardToSoftConversionReceipt(
                receipt.PostingId,
                feePostingId,
                receipt.JournalSequence,
                receipt.JournalHash,
                receipt.Duplicate);
        }

        if (context is not DbContext dbContext || !dbContext.Database.IsRelational())
            return await IssueAndPostAsync().ConfigureAwait(false);

        return await PostgreSqlTransactionExecutor.ExecuteAsync(
                dbContext, IsolationLevel.Serializable, _ => IssueAndPostAsync(), cancellationToken)
            .ConfigureAwait(false);
    }

    internal static IReadOnlyList<Guid> ParseRootIds(string sourceRoots)
    {
        if (string.IsNullOrWhiteSpace(sourceRoots))
            throw new EconomySelfServiceCommandRejectedException("The risk decision does not authorize any source roots.");

        try
        {
            using var document = JsonDocument.Parse(sourceRoots);
            if (document.RootElement.ValueKind != JsonValueKind.Array)
                throw new EconomySelfServiceCommandRejectedException("The risk decision source roots are malformed.");

            var roots = document.RootElement
                .EnumerateArray()
                .Select(element => element.GetString())
                .Select(value => Guid.TryParse(value, out var root) ? root : Guid.Empty)
                .ToArray();
            if (roots.Length == 0 || roots.Any(root => root == Guid.Empty) || roots.Distinct().Count() != roots.Length)
                throw new EconomySelfServiceCommandRejectedException("The risk decision source roots are malformed.");

            return roots.OrderBy(root => root).ToArray();
        }
        catch (JsonException exception)
        {
            throw new EconomySelfServiceCommandRejectedException("The risk decision source roots are malformed.", exception);
        }
    }

    internal static Guid DeterministicGuid(string purpose, string idempotencyKey)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes($"{purpose}:{idempotencyKey}"));
        Span<byte> bytes = stackalloc byte[16];
        hash.AsSpan(0, 16).CopyTo(bytes);
        bytes[6] = (byte)((bytes[6] & 0x0F) | 0x50);
        bytes[8] = (byte)((bytes[8] & 0x3F) | 0x80);
        return new Guid(bytes);
    }
}

public sealed class EconomySelfServiceCommandRejectedException(string message, Exception? innerException = null)
    : InvalidOperationException(message, innerException);
