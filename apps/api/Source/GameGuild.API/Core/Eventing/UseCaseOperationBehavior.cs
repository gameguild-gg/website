using System.Reflection;
using System.Text;
using Microsoft.EntityFrameworkCore;
using GameGuild.API.Database;
using GameGuild.CQRS;
using GameGuild.Identity.Context.Actors;

namespace GameGuild.API.Eventing;

internal sealed class UseCaseOperationBehavior<TRequest, TResponse>(
    ApplicationDbContext context,
    IActorContextAccessor actorContextAccessor,
    IUseCaseOperationContextAccessor operationContextAccessor,
    IUseCaseEventContractRegistry? eventContractRegistry = null,
    IUseCaseEventVerifier? eventVerifier = null)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequestBase
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        if (!IsCommand())
        {
            return await next().ConfigureAwait(false);
        }

        var contract = eventContractRegistry?.GetRequired(typeof(TRequest));
        var actor = actorContextAccessor.ActorContext;
        var operationContext = new UseCaseOperationContext(
            contract?.OperationCode ?? ResolveOperationCode(),
            typeof(TRequest).Name,
            actor.TenantId ?? DurableIntegrationEventTenants.Platform,
            actor.SubjectIdAsGuid ?? DurableIntegrationEventActors.System,
            Guid.NewGuid(),
            null);
        using var operation = operationContextAccessor.Begin(operationContext);

        if (!context.Database.IsRelational() || context.Database.CurrentTransaction is not null)
        {
            var response = await next().ConfigureAwait(false);
            if (!CommandOutcome.IsFailure(response))
            {
                await EnsureOperationEventStoredAsync(request, operationContext, cancellationToken).ConfigureAwait(false);
                if (contract is not null && eventVerifier is not null)
                    await eventVerifier.VerifyAsync(contract, operationContext, cancellationToken).ConfigureAwait(false);
            }
            return response;
        }

        var executionStrategy = context.Database.CreateExecutionStrategy();
        return await executionStrategy.ExecuteAsync(async () =>
        {
            await using var transaction = await context.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                var response = await next().ConfigureAwait(false);
                if (CommandOutcome.IsFailure(response))
                {
                    await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                    context.ChangeTracker.Clear();
                    return response;
                }

                await EnsureOperationEventStoredAsync(request, operationContext, cancellationToken).ConfigureAwait(false);
                if (contract is not null && eventVerifier is not null)
                    await eventVerifier.VerifyAsync(contract, operationContext, cancellationToken).ConfigureAwait(false);

                await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
                return response;
            }
            catch
            {
                await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                context.ChangeTracker.Clear();
                throw;
            }
        }).ConfigureAwait(false);
    }

    private async Task EnsureOperationEventStoredAsync(
        TRequest request,
        UseCaseOperationContext operationContext,
        CancellationToken cancellationToken)
    {
        if (!operationContext.BusinessMutationObserved || operationContext.OperationEventCaptured)
            return;

        var aggregateId = typeof(TRequest).GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Where(property => property.Name.EndsWith("Id", StringComparison.Ordinal))
            .Select(property => property.GetValue(request)?.ToString())
            .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))
            ?? operationContext.CorrelationId.ToString();
        var operationEvent = operationContext.GetOrCreateOperationEvent(typeof(TRequest).Name, aggregateId);
        DurableIntegrationEventValidator.Validate(operationEvent);
        context.Set<OutboxMessage>().Add(new OutboxMessage
        {
            EventId = operationEvent.EventId,
            TenantId = operationEvent.TenantId,
            ActorId = operationEvent.ActorId,
            EventName = operationEvent.EventName,
            EventType = DurableIntegrationEventValidator.GetStableTypeName(operationEvent.GetType()),
            SourceModule = operationEvent.SourceModule,
            AggregateType = operationEvent.AggregateType,
            AggregateId = operationEvent.AggregateId,
            CorrelationId = operationEvent.CorrelationId,
            CausationId = operationEvent.CausationId,
            OccurredAtUtc = operationEvent.OccurredAt,
            SchemaVersion = operationEvent.SchemaVersion,
            Payload = DurableEventSerializer.Serialize(operationEvent),
            CreatedAtUtc = DateTimeOffset.UtcNow
        });
        operationContext.MarkBusinessMutationObserved();
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        operationContext.MarkOperationEventCaptured();
    }

    private static bool IsCommand() =>
        typeof(ICommand).IsAssignableFrom(typeof(TRequest))
        || typeof(TRequest).GetInterfaces().Any(contract =>
            contract.IsGenericType && contract.GetGenericTypeDefinition() == typeof(ICommand<>));

    private static string ResolveOperationCode()
    {
        var explicitCode = typeof(TRequest).GetCustomAttribute<UseCaseOperationCodeAttribute>()?.Code;
        if (!string.IsNullOrWhiteSpace(explicitCode))
        {
            return explicitCode;
        }

        var typeName = typeof(TRequest).Name;
        var stem = typeName.EndsWith("Command", StringComparison.Ordinal)
            ? typeName[..^"Command".Length]
            : typeName;
        var code = new StringBuilder(stem.Length + 8);
        for (var index = 0; index < stem.Length; index++)
        {
            var character = stem[index];
            if (index > 0 && char.IsUpper(character))
            {
                code.Append('-');
            }

            code.Append(char.ToLowerInvariant(character));
        }

        return code.ToString();
    }
}
