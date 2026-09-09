namespace GameGuild;

public sealed record UseCaseOperationContext(
    string OperationCode,
    string CommandType,
    Guid TenantId,
    Guid ActorId,
    Guid CorrelationId,
    Guid? CausationId)
{
    private UseCaseOperationOccurredV1? _operationEvent;
    private readonly HashSet<Type> _producedEventTypes = [];

    public bool OperationEventCaptured { get; private set; }
    public bool BusinessMutationObserved { get; private set; }
    public IReadOnlyCollection<Type> ProducedEventTypes => _producedEventTypes;

    public UseCaseOperationOccurredV1 GetOrCreateOperationEvent(string aggregateType, string aggregateId)
    {
        _operationEvent ??= new UseCaseOperationOccurredV1(OperationCode, CommandType)
        {
            TenantId = TenantId,
            ActorId = ActorId,
            AggregateType = aggregateType,
            AggregateId = aggregateId,
            CorrelationId = CorrelationId,
            CausationId = CausationId,
            OccurredAt = DateTime.UtcNow
        };

        return _operationEvent;
    }

    public void MarkOperationEventCaptured() => OperationEventCaptured = true;
    public void MarkBusinessMutationObserved() => BusinessMutationObserved = true;
    public void RecordProducedEvents(IEnumerable<IDurableIntegrationEvent> events)
    {
        foreach (var integrationEvent in events)
            _producedEventTypes.Add(integrationEvent.GetType());
    }
}

[AttributeUsage(AttributeTargets.Class)]
public sealed class UseCaseOperationCodeAttribute(string code) : Attribute
{
    public string Code { get; } = code;
}

public static class CommandOutcome
{
    public static bool IsFailure(object? response) => IsFailure(response, new HashSet<object>(ReferenceEqualityComparer.Instance));

    private static bool IsFailure(object? response, ISet<object> inspected)
    {
        if (response is null)
        {
            return true;
        }

        if (response is Result result)
        {
            return result.IsFailure;
        }

        if (response is bool boolean)
        {
            return !boolean;
        }

        var responseType = response.GetType();
        if (!responseType.IsValueType && !inspected.Add(response))
        {
            return false;
        }

        if (responseType.GetProperty("IsFailure")?.GetValue(response) is bool isFailure)
        {
            return isFailure;
        }

        if (responseType.GetProperty("IsSuccess")?.GetValue(response) is bool isSuccess)
        {
            return !isSuccess;
        }

        if (responseType.GetProperty("Success")?.GetValue(response) is bool success)
        {
            return !success;
        }

        if (responseType.GetProperty("StatusCode")?.GetValue(response) is int statusCode)
        {
            return statusCode >= 400;
        }

        var nestedResult = responseType.GetProperty("Result")?.GetValue(response);
        if (nestedResult is not null && IsFailure(nestedResult, inspected))
        {
            return true;
        }

        if (responseType.GetProperty("CodeConflict")?.GetValue(response) is true)
        {
            return true;
        }

        var error = responseType.GetProperty("Error")?.GetValue(response);
        return error switch
        {
            string text => !string.IsNullOrWhiteSpace(text),
            Enum errorValue => Convert.ToInt64(errorValue) != 0,
            _ => false
        };
    }
}

public interface IUseCaseOperationContextAccessor
{
    UseCaseOperationContext? Current { get; }
    IDisposable Begin(UseCaseOperationContext context);
}

public sealed class UseCaseOperationContextAccessor : IUseCaseOperationContextAccessor
{
    private static readonly AsyncLocal<ScopeState?> CurrentScope = new();

    public UseCaseOperationContext? Current => CurrentScope.Value?.Context;

    public IDisposable Begin(UseCaseOperationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var previous = CurrentScope.Value;
        CurrentScope.Value = new ScopeState(context);
        return new Scope(previous);
    }

    private sealed record ScopeState(UseCaseOperationContext Context);

    private sealed class Scope(ScopeState? previous) : IDisposable
    {
        private bool _disposed;

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            CurrentScope.Value = previous;
            _disposed = true;
        }
    }
}

public sealed record UseCaseOperationOccurredV1(
    [property: NonPersonalEventData] string OperationCode = "unknown",
    [property: NonPersonalEventData] string CommandType = "unknown") : DurableIntegrationEventBase
{
    public override string EventName => "platform.use-case-operation.occurred.v1";
    public override string SourceModule => "Platform";
}
