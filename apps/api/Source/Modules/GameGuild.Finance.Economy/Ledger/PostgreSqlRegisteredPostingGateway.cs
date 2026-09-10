using System.Data.Common;
using GameGuild.Finance.Economy.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GameGuild.Finance.Economy.Ledger;

public sealed class PostgreSqlRegisteredPostingGateway : IRegisteredPostingGateway
{
    private readonly DbContext _db;

    public PostgreSqlRegisteredPostingGateway(IApplicationDbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        _db = context as DbContext
            ?? throw new InvalidOperationException(
                "Registered economy posting requires the application's relational DbContext.");
    }

    public RegisteredPostingReceipt Post(RegisteredPostingRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var accountIds = ResolveAccountIds(request.Posting.Lines);
        var payload = RegisteredPostingPayloadFactory.Create(request, accountIds);
        var source = request.Posting.Source;

        try
        {
            FormattableString writer;
            if (request.Posting.Template.Kind == Contracts.PostingTemplateKind.BountyEscrow)
            {
                writer = $"""
                    SELECT *
                    FROM economy_private.post_registered_posting_v2(
                        {request.Authority.CapabilityId},
                        {request.Authority.ActorId},
                        {request.Authority.TenantId},
                        {request.Posting.Id.Value},
                        {request.Posting.IdempotencyKey.Value},
                        {(int)request.Posting.Template.Kind},
                        {request.Posting.Template.Version},
                        {(int)request.Posting.Authority},
                        {request.Posting.PolicyVersion.Value},
                        {request.Posting.ReserveVersion.Value},
                        {request.Authority.RiskDecisionId},
                        {request.Authority.RiskOperationFingerprint},
                        {request.Authority.ExpectedCounterVersion},
                        {source?.Id.Value},
                        {source?.EvidenceHash},
                        {request.Posting.RequestedAt},
                        CAST({payload.Lines} AS jsonb),
                        CAST({payload.Allocations} AS jsonb),
                        CAST({payload.RootRanges} AS jsonb),
                        CAST({payload.ExpectedReversalEpochs} AS jsonb),
                        {request.DispatchSnapshotHash})
                    """;
            }
            else
            {
                writer = $"""
                    SELECT *
                    FROM economy_private.post_registered_posting_v1(
                        {request.Authority.CapabilityId},
                        {request.Authority.ActorId},
                        {request.Authority.TenantId},
                        {request.Posting.Id.Value},
                        {request.Posting.IdempotencyKey.Value},
                        {(int)request.Posting.Template.Kind},
                        {request.Posting.Template.Version},
                        {(int)request.Posting.Authority},
                        {request.Posting.PolicyVersion.Value},
                        {request.Posting.ReserveVersion.Value},
                        {request.Authority.RiskDecisionId},
                        {request.Authority.RiskOperationFingerprint},
                        {request.Authority.ExpectedCounterVersion},
                        {source?.Id.Value},
                        {source?.EvidenceHash},
                        {request.Posting.RequestedAt},
                        CAST({payload.Lines} AS jsonb),
                        CAST({payload.Allocations} AS jsonb),
                        CAST({payload.RootRanges} AS jsonb),
                        CAST({payload.ExpectedReversalEpochs} AS jsonb),
                        {request.DispatchSnapshotHash})
                    """;
            }

            var result = _db.Set<RegisteredPostingReceiptRow>()
                .FromSqlInterpolated(writer)
                .AsNoTracking()
                .Single();

            return new RegisteredPostingReceipt(
                new Contracts.PostingId(result.PostingId),
                result.JournalSequence,
                result.JournalHash,
                result.Duplicate);
        }
        catch (Exception exception) when (IsDatabaseFailure(exception))
        {
            throw new RegisteredPostingRejectedException(
                "The registered economy writer rejected the posting.",
                exception);
        }
    }

    private IReadOnlyDictionary<int, Guid> ResolveAccountIds(IReadOnlyList<Contracts.PostingLine> lines)
    {
        var accountIds = new Dictionary<int, Guid>();
        foreach (var line in lines)
        {
            var walletId = line.WalletId?.Value;
            var accountId = _db.Set<EconomyAccountRow>()
                .AsNoTracking()
                .Where(account => account.Code == line.Account &&
                                  account.Currency == line.Amount.Currency &&
                                  account.WalletId == walletId &&
                                  account.Provenance == line.Provenance)
                .Select(account => account.Id)
                .SingleOrDefault();

            if (accountId == Guid.Empty)
                throw new RegisteredPostingRejectedException(
                    "The posting references an economy account that is not provisioned.");

            accountIds.Add(line.Sequence, accountId);
        }

        return accountIds;
    }

    private static bool IsDatabaseFailure(Exception exception) =>
        exception is DbException or DbUpdateException or InvalidOperationException ||
        exception.GetBaseException() is DbException;
}
