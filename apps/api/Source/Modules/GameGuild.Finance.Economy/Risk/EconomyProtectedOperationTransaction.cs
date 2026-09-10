using System.Data;
using GameGuild.Finance.Economy.Persistence;

namespace GameGuild.Finance.Economy.Risk;

public sealed class EconomyProtectedOperationTransaction(IApplicationDbContext context)
    : IEconomyProtectedOperationTransaction
{
    public Task<T> ExecuteAsync<T>(
        Func<CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken) =>
        PostgreSqlTransactionExecutor.ExecuteAsync(
            context,
            IsolationLevel.Serializable,
            operation,
            cancellationToken);
}
