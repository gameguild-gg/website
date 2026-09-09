namespace GameGuild.CQRS.Implementation;

public sealed class UnavailableUseCaseException(Type commandType, string reason)
    : DomainException($"Use case '{commandType.FullName}' is unavailable: {reason}")
{
    public Type CommandType { get; } = commandType;
    public string Reason { get; } = reason;
}

public sealed class UnavailableCommandHandler<TCommand> : ICommandHandler<TCommand>
    where TCommand : ICommand
{
    public Task<Unit> Handle(TCommand request, CancellationToken cancellationToken) =>
        throw UnavailableUseCase.For<TCommand>();
}

public sealed class UnavailableCommandHandler<TCommand, TResponse> : ICommandHandler<TCommand, TResponse>
    where TCommand : ICommand<TResponse>
{
    public Task<TResponse> Handle(TCommand request, CancellationToken cancellationToken) =>
        throw UnavailableUseCase.For<TCommand>();
}

internal static class UnavailableUseCase
{
    public static UnavailableUseCaseException For<TCommand>()
    {
        var commandType = typeof(TCommand);
        var reason = commandType.Assembly
            .GetCustomAttributes(typeof(UseCaseEventContractAttribute), false)
            .Cast<UseCaseEventContractAttribute>()
            .Single(contract => contract.CommandType == commandType)
            .UnavailableReason;
        return new UnavailableUseCaseException(
            commandType,
            string.IsNullOrWhiteSpace(reason) ? "No availability reason was declared." : reason);
    }
}
