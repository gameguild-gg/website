using GameGuild.CQRS;

namespace GameGuild.Commerce.Payments;

public sealed record CreateSetupIntentCommand(
    Guid TenantId,
    Guid SubscriptionId,
    string? CustomerEmail,
    string? CustomerName) : ICommand<CreateSetupIntentCommandResult>;

public sealed record CreateSetupIntentCommandResult(
    Guid SubscriptionId,
    string? CustomerId,
    string? SetupIntentId,
    string? ClientSecret,
    bool NotFound = false,
    string? Error = null)
{
    public bool Success => Error is null && !NotFound;
}

public sealed class CreateSetupIntentCommandHandler(
    IStripeCustomerService stripeCustomerService,
    ISubscriptionPaymentContextService subscriptionPaymentContextService)
    : ICommandHandler<CreateSetupIntentCommand, CreateSetupIntentCommandResult>
{
    public async Task<CreateSetupIntentCommandResult> Handle(
        CreateSetupIntentCommand command,
        CancellationToken cancellationToken)
    {
        var subscription = await subscriptionPaymentContextService
            .GetPaymentContextAsync(command.SubscriptionId, cancellationToken)
            .ConfigureAwait(false);
        if (subscription is null)
            return new(command.SubscriptionId, null, null, null, NotFound: true);
        if (subscription.TenantId != command.TenantId)
            return new(command.SubscriptionId, null, null, null, Error: "Subscription does not belong to the specified tenant.");

        var customerId = subscription.ExternalCustomerId;
        if (string.IsNullOrWhiteSpace(customerId))
        {
            if (string.IsNullOrWhiteSpace(command.CustomerEmail))
                return new(command.SubscriptionId, null, null, null,
                    Error: "CustomerEmail is required when the subscription does not yet have a Stripe customer.");

            var customer = await stripeCustomerService.CreateCustomerAsync(
                new GatewayCustomerRequest(
                    command.CustomerEmail.Trim(),
                    string.IsNullOrWhiteSpace(command.CustomerName) ? null : command.CustomerName.Trim(),
                    Phone: null,
                    Metadata: Metadata(command)),
                cancellationToken).ConfigureAwait(false);
            if (!customer.Success || string.IsNullOrWhiteSpace(customer.ExternalCustomerId))
                return new(command.SubscriptionId, null, null, null,
                    Error: customer.ErrorMessage ?? "Stripe could not create a customer for this subscription.");

            customerId = customer.ExternalCustomerId;
            await subscriptionPaymentContextService.SetExternalCustomerIdAsync(
                command.SubscriptionId,
                customerId,
                cancellationToken).ConfigureAwait(false);
        }

        var setupIntent = await stripeCustomerService.CreateSetupIntentAsync(
            new GatewaySetupIntentRequest(customerId, Metadata(command)),
            cancellationToken).ConfigureAwait(false);
        if (!setupIntent.Success
            || string.IsNullOrWhiteSpace(setupIntent.ClientSecret)
            || string.IsNullOrWhiteSpace(setupIntent.ExternalSetupIntentId))
            return new(command.SubscriptionId, customerId, null, null,
                Error: setupIntent.ErrorMessage ?? "Stripe could not create a setup intent for this subscription.");

        return new(
            subscription.SubscriptionId,
            customerId,
            setupIntent.ExternalSetupIntentId,
            setupIntent.ClientSecret);
    }

    private static Dictionary<string, string> Metadata(CreateSetupIntentCommand command) => new()
    {
        ["tenant_id"] = command.TenantId.ToString(),
        ["subscription_id"] = command.SubscriptionId.ToString()
    };
}
