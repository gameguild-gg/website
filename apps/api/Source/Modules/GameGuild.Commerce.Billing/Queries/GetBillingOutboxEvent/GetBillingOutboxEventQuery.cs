using FluentValidation;
using GameGuild.CQRS;

namespace GameGuild.Commerce.Billing;

/// <summary>Query (issue #396): get one durable billing integration event by id.</summary>
/// <param name="EventId">Durable event identifier.</param>
public sealed record GetBillingOutboxEventQuery(Guid EventId) : IQuery<BillingOutboxEventDto?>;

/// <summary>Validator for <see cref="GetBillingOutboxEventQuery" />.</summary>
public sealed class GetBillingOutboxEventQueryValidator : AbstractValidator<GetBillingOutboxEventQuery>
{
    /// <summary>Initializes the validator.</summary>
    public GetBillingOutboxEventQueryValidator()
    {
        RuleFor(query => query.EventId).NotEmpty();
    }
}

/// <summary>Handler for <see cref="GetBillingOutboxEventQuery" /> over the platform outbox read model.</summary>
public sealed class GetBillingOutboxEventQueryHandler(
    IBillingOutboxEventReader outboxReader) : IQueryHandler<GetBillingOutboxEventQuery, BillingOutboxEventDto?>
{
    /// <inheritdoc />
    public Task<BillingOutboxEventDto?> Handle(GetBillingOutboxEventQuery request, CancellationToken cancellationToken) =>
        outboxReader.GetByIdAsync(request.EventId, cancellationToken);
}
