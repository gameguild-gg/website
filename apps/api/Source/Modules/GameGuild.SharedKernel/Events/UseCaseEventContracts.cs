namespace GameGuild;

[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
public sealed class UseCaseEventContractAttribute(Type commandType, string operationCode) : Attribute
{
    public Type CommandType { get; } = commandType;
    public string OperationCode { get; } = operationCode;
    public Type[] ExpectedEventTypes { get; set; } = [];
    public Type[] ConditionalEventTypes { get; set; } = [];
    public string? QuotaImpact { get; set; }
    public string? NoDomainEventReason { get; set; }
    public string? UnavailableReason { get; set; }
}

public sealed record UseCaseEventContract(
    Type CommandType,
    string OperationCode,
    IReadOnlyList<Type> ExpectedEventTypes,
    IReadOnlyList<Type> ConditionalEventTypes,
    string? QuotaImpact,
    string? NoDomainEventReason,
    string? UnavailableReason);

public interface IUseCaseEventContractRegistry
{
    UseCaseEventContract GetRequired(Type commandType);
    IReadOnlyCollection<UseCaseEventContract> GetAll();
}

public interface IUseCaseEventVerifier
{
    Task VerifyAsync(
        UseCaseEventContract contract,
        UseCaseOperationContext operationContext,
        CancellationToken cancellationToken = default);
}

[AttributeUsage(AttributeTargets.Method)]
public sealed class UnavailableEndpointAttribute(string reason) : Attribute
{
    public string Reason { get; } = string.IsNullOrWhiteSpace(reason)
        ? throw new ArgumentException("An unavailable endpoint requires a reason.", nameof(reason))
        : reason;
}
