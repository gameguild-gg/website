using Microsoft.EntityFrameworkCore;

namespace GameGuild.Finance.Economy.Payouts;

public interface IPayoutFencingTokenAllocator
{
    ValueTask<long> AllocateAsync(CancellationToken cancellationToken = default);
}

public sealed class PostgreSqlPayoutFencingTokenAllocator : IPayoutFencingTokenAllocator
{
    private readonly DbContext _db;

    public PostgreSqlPayoutFencingTokenAllocator(IApplicationDbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        _db = context as DbContext
            ?? throw new InvalidOperationException(
                "Payout fencing requires the application's relational DbContext.");
    }

    public async ValueTask<long> AllocateAsync(CancellationToken cancellationToken = default)
    {
        var token = await _db.Database.SqlQuery<long>(
                $"SELECT economy_private.next_payout_fencing_token_v1() AS \"Value\"")
            .SingleAsync(cancellationToken).ConfigureAwait(false);
        if (token <= 0)
            throw new PayoutStaleCommandException("The durable payout fencing token is invalid.");
        return token;
    }
}
