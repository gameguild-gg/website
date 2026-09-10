using Microsoft.EntityFrameworkCore;

namespace GameGuild.Finance.Economy.Treasury;

public interface IAdminWithdrawalFencingTokenAllocator
{
    ValueTask<long> AllocateAsync(CancellationToken cancellationToken = default);
}

public sealed class PostgreSqlAdminWithdrawalFencingTokenAllocator :
    IAdminWithdrawalFencingTokenAllocator
{
    private readonly DbContext _db;

    public PostgreSqlAdminWithdrawalFencingTokenAllocator(IApplicationDbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        _db = context as DbContext
            ?? throw new InvalidOperationException(
                "Administrative-withdrawal fencing requires the application's relational DbContext.");
    }

    public async ValueTask<long> AllocateAsync(CancellationToken cancellationToken = default)
    {
        var token = await _db.Database.SqlQuery<long>(
                $"SELECT economy_private.next_admin_withdrawal_fencing_token_v1() AS \"Value\"")
            .SingleAsync(cancellationToken).ConfigureAwait(false);
        if (token <= 0)
            throw new AdminWithdrawalStaleCommandException(
                "The durable administrative-withdrawal fencing token is invalid.");
        return token;
    }
}
