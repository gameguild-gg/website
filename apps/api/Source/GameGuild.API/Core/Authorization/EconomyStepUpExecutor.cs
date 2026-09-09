using System.Data;
using System.Security.Cryptography;
using System.Text;
using GameGuild.Finance.Economy.Persistence;
using GameGuild.Identity.Authentication;

namespace GameGuild.API.Authorization;

public sealed record EconomyStepUpOperation(
    string OperationType,
    string TargetReference,
    string PayloadHash)
{
    public static EconomyStepUpOperation Create(
        string operationType,
        string targetReference,
        params string?[] payloadValues)
    {
        ArgumentNullException.ThrowIfNull(payloadValues);
        var canonical = new StringBuilder("economy-step-up-payload-v1");
        foreach (var value in payloadValues)
        {
            var normalized = value ?? string.Empty;
            canonical.Append('|')
                .Append(Encoding.UTF8.GetByteCount(normalized))
                .Append(':')
                .Append(normalized);
        }

        var payloadHash = Convert.ToHexStringLower(
            SHA256.HashData(Encoding.UTF8.GetBytes(canonical.ToString())));
        var binding = new StepUpOperationBinding(operationType, targetReference, payloadHash);
        return new EconomyStepUpOperation(
            binding.OperationType,
            binding.TargetReference,
            binding.PayloadHash);
    }
}

public interface IEconomyStepUpExecutor
{
    Task<TResult> ExecuteAsync<TResult>(
        EconomyStepUpOperation operation,
        string receipt,
        Func<string, CancellationToken, Task<TResult>> protectedAction,
        CancellationToken cancellationToken);
}

public sealed class EconomyStepUpExecutor(
    IStepUpReceiptService stepUpReceiptService,
    IApplicationDbContext context) : IEconomyStepUpExecutor
{
    public Task<TResult> ExecuteAsync<TResult>(
        EconomyStepUpOperation operation,
        string receipt,
        Func<string, CancellationToken, Task<TResult>> protectedAction,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentException.ThrowIfNullOrWhiteSpace(receipt);
        ArgumentNullException.ThrowIfNull(protectedAction);

        return PostgreSqlTransactionExecutor.ExecuteAsync(
            context,
            IsolationLevel.Serializable,
            async token =>
            {
                await stepUpReceiptService.ConsumeAsync(
                    new StepUpOperationBinding(
                        operation.OperationType,
                        operation.TargetReference,
                        operation.PayloadHash),
                    receipt,
                    token).ConfigureAwait(false);
                var evidenceHash = Convert.ToHexStringLower(
                    SHA256.HashData(Encoding.UTF8.GetBytes(receipt)));
                return await protectedAction(evidenceHash, token).ConfigureAwait(false);
            },
            cancellationToken);
    }
}
