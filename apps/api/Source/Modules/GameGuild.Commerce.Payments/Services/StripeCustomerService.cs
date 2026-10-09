using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Stripe;

namespace GameGuild.Commerce.Payments;

/// <summary>
///     Stripe customer lifecycle management: customer creation, payment methods, and subscriptions.
/// </summary>
public class StripeCustomerService(
    IOptions<StripeGatewayOptions> options,
    ILogger<StripeCustomerService> logger) : IStripeCustomerService
{
    private readonly StripeGatewayOptions _options = InitializeOptions(options);
    private readonly CustomerService _customerService = new();
    private readonly PaymentMethodService _paymentMethodService = new();
    private readonly SetupIntentService _setupIntentService = new();
    private readonly SubscriptionService _subscriptionService = new();

    internal StripeCustomerService(
        IOptions<StripeGatewayOptions> options,
        ILogger<StripeCustomerService> logger,
        IStripeClient client) : this(options, logger)
    {
        ArgumentNullException.ThrowIfNull(client);
        _customerService = new CustomerService(client);
        _paymentMethodService = new PaymentMethodService(client);
        _setupIntentService = new SetupIntentService(client);
        _subscriptionService = new SubscriptionService(client);
    }

    private static StripeGatewayOptions InitializeOptions(IOptions<StripeGatewayOptions> options)
    {
        var stripeOptions = options.Value;
        StripePaymentGateway.EnsureApiKey(stripeOptions);
        return stripeOptions;
    }

    /// <inheritdoc />
    public async Task<GatewayCustomerResult> CreateCustomerAsync(
        GatewayCustomerRequest request,
        CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Creating Stripe customer for email {Email} (Simulation: {IsSimulation})",
            LogRedaction.MaskEmail(request.Email), _options.UseSimulation);

        if (_options.UseSimulation)
        {
            await Task.Delay(1, cancellationToken).ConfigureAwait(false);
            return SimulatedPaymentResultFactory.CustomerSuccess(logger);
        }

        try
        {
            var createOptions = new CustomerCreateOptions
            {
                Email = request.Email,
                Name = request.Name,
                Phone = request.Phone,
                Metadata = request.Metadata
            };

            var customer = await _customerService.CreateAsync(
                createOptions, cancellationToken: cancellationToken).ConfigureAwait(false);

            logger.LogInformation("Stripe customer created: {CustomerId}", LogRedaction.RedactId(customer.Id, "cus"));

            return new GatewayCustomerResult(
                Success: true,
                ExternalCustomerId: customer.Id,
                ErrorCode: null,
                ErrorMessage: null);
        }
        catch (StripeException ex)
        {
            logger.LogError("Stripe customer creation failed for email {Email}: {ErrorCode} ({ErrorType})",
                LogRedaction.MaskEmail(request.Email), LogRedaction.Sanitize(ex.StripeError?.Code), ex.GetType().FullName);

            return new GatewayCustomerResult(
                Success: false,
                ExternalCustomerId: null,
                ErrorCode: ex.StripeError?.Code ?? "stripe_error",
                ErrorMessage: ex.StripeError?.Message ?? ex.Message);
        }
        catch (Exception ex)
        {
            logger.LogError("Unexpected error during Stripe customer creation for email {Email}: {ErrorType}",
                LogRedaction.MaskEmail(request.Email), ex.GetType().FullName);
            return SimulatedPaymentResultFactory.CustomerFailure(ex.Message, "unexpected_error");
        }
    }

    /// <inheritdoc />
    public async Task<GatewayPaymentMethodResult> CreatePaymentMethodAsync(
        GatewayPaymentMethodRequest request,
        CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Attaching payment method to Stripe customer {CustomerId} (Simulation: {IsSimulation})",
            LogRedaction.RedactId(request.CustomerId, "cus"), _options.UseSimulation);

        if (_options.UseSimulation)
        {
            await Task.Delay(1, cancellationToken).ConfigureAwait(false);
            return SimulatedPaymentResultFactory.PaymentMethodSuccess(logger: logger);
        }

        try
        {
            var attachOptions = new PaymentMethodAttachOptions
            {
                Customer = request.CustomerId
            };

            var paymentMethod = await _paymentMethodService.AttachAsync(
                request.PaymentMethodToken,
                attachOptions,
                cancellationToken: cancellationToken).ConfigureAwait(false);

            logger.LogInformation("Payment method {PaymentMethodId} attached to customer {CustomerId}",
                LogRedaction.RedactId(paymentMethod.Id, "pm"), LogRedaction.RedactId(request.CustomerId, "cus"));

            if (request.SetAsDefault)
            {
                await _customerService.UpdateAsync(
                    request.CustomerId,
                    new CustomerUpdateOptions
                    {
                        InvoiceSettings = new CustomerInvoiceSettingsOptions
                        {
                            DefaultPaymentMethod = paymentMethod.Id
                        }
                    },
                    cancellationToken: cancellationToken).ConfigureAwait(false);
            }

            return new GatewayPaymentMethodResult(
                Success: true,
                ExternalPaymentMethodId: paymentMethod.Id,
                CardLast4: paymentMethod.Card?.Last4,
                CardBrand: paymentMethod.Card?.Brand,
                ExpiryMonth: (int?)paymentMethod.Card?.ExpMonth,
                ExpiryYear: (int?)paymentMethod.Card?.ExpYear,
                ErrorCode: null,
                ErrorMessage: null);
        }
        catch (StripeException ex)
        {
            logger.LogError("Stripe payment method attachment failed for customer {CustomerId}: {ErrorCode} ({ErrorType})",
                LogRedaction.RedactId(request.CustomerId, "cus"), LogRedaction.Sanitize(ex.StripeError?.Code), ex.GetType().FullName);

            return new GatewayPaymentMethodResult(
                Success: false,
                ExternalPaymentMethodId: null,
                CardLast4: null,
                CardBrand: null,
                ExpiryMonth: null,
                ExpiryYear: null,
                ErrorCode: ex.StripeError?.Code ?? "stripe_error",
                ErrorMessage: ex.StripeError?.Message ?? ex.Message);
        }
        catch (Exception ex)
        {
            logger.LogError("Unexpected error during payment method attachment for customer {CustomerId}: {ErrorType}",
                LogRedaction.RedactId(request.CustomerId, "cus"), ex.GetType().FullName);
            return SimulatedPaymentResultFactory.PaymentMethodFailure(ex.Message, "unexpected_error");
        }
    }

    /// <inheritdoc />
    public async Task<GatewaySetupIntentResult> CreateSetupIntentAsync(
        GatewaySetupIntentRequest request,
        CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Creating Stripe setup intent for customer {CustomerId} (Simulation: {IsSimulation})",
            LogRedaction.RedactId(request.CustomerId, "cus"), _options.UseSimulation);

        if (_options.UseSimulation)
        {
            await Task.Delay(1, cancellationToken).ConfigureAwait(false);
            return SimulatedPaymentResultFactory.SetupIntentSuccess(request.CustomerId, logger);
        }

        try
        {
            var createOptions = new SetupIntentCreateOptions
            {
                Customer = request.CustomerId,
                Usage = "off_session",
                Metadata = request.Metadata,
                AutomaticPaymentMethods = new SetupIntentAutomaticPaymentMethodsOptions
                {
                    Enabled = true
                }
            };

            var setupIntent = await _setupIntentService.CreateAsync(
                createOptions,
                cancellationToken: cancellationToken).ConfigureAwait(false);

            logger.LogInformation("Stripe setup intent created: {SetupIntentId} for customer {CustomerId}",
                LogRedaction.RedactId(setupIntent.Id, "seti"), LogRedaction.RedactId(request.CustomerId, "cus"));

            return new GatewaySetupIntentResult(
                Success: true,
                ExternalSetupIntentId: setupIntent.Id,
                ClientSecret: setupIntent.ClientSecret,
                CustomerId: request.CustomerId,
                ErrorCode: null,
                ErrorMessage: null);
        }
        catch (StripeException ex)
        {
            logger.LogError("Stripe setup intent creation failed for customer {CustomerId}: {ErrorCode} ({ErrorType})",
                LogRedaction.RedactId(request.CustomerId, "cus"), LogRedaction.Sanitize(ex.StripeError?.Code), ex.GetType().FullName);

            return new GatewaySetupIntentResult(
                Success: false,
                ExternalSetupIntentId: null,
                ClientSecret: null,
                CustomerId: request.CustomerId,
                ErrorCode: ex.StripeError?.Code ?? "stripe_error",
                ErrorMessage: ex.StripeError?.Message ?? ex.Message);
        }
        catch (Exception ex)
        {
            logger.LogError("Unexpected error during setup intent creation for customer {CustomerId}: {ErrorType}",
                LogRedaction.RedactId(request.CustomerId, "cus"), ex.GetType().FullName);
            return SimulatedPaymentResultFactory.SetupIntentFailure(ex.Message, "unexpected_error");
        }
    }

    /// <inheritdoc />
    public async Task<GatewayDefaultPaymentMethodResult> SetDefaultPaymentMethodAsync(
        GatewayDefaultPaymentMethodRequest request,
        CancellationToken cancellationToken = default)
    {
        logger.LogInformation(
            "Setting Stripe default payment method {PaymentMethodId} for customer {CustomerId} (Simulation: {IsSimulation})",
            LogRedaction.RedactId(request.PaymentMethodId, "pm"),
            LogRedaction.RedactId(request.CustomerId, "cus"),
            _options.UseSimulation);

        if (_options.UseSimulation)
        {
            await Task.Delay(1, cancellationToken).ConfigureAwait(false);
            return SimulatedPaymentResultFactory.DefaultPaymentMethodSuccess();
        }

        try
        {
            await _customerService.UpdateAsync(
                request.CustomerId,
                new CustomerUpdateOptions
                {
                    InvoiceSettings = new CustomerInvoiceSettingsOptions
                    {
                        DefaultPaymentMethod = request.PaymentMethodId
                    }
                },
                cancellationToken: cancellationToken).ConfigureAwait(false);

            logger.LogInformation(
                "Stripe customer {CustomerId} default payment method updated to {PaymentMethodId}",
                LogRedaction.RedactId(request.CustomerId, "cus"),
                LogRedaction.RedactId(request.PaymentMethodId, "pm"));

            return new GatewayDefaultPaymentMethodResult(
                Success: true,
                ErrorCode: null,
                ErrorMessage: null);
        }
        catch (StripeException ex)
        {
            logger.LogError(
                "Stripe default payment method update failed for customer {CustomerId}: {ErrorCode} ({ErrorType})",
                LogRedaction.RedactId(request.CustomerId, "cus"),
                LogRedaction.Sanitize(ex.StripeError?.Code), ex.GetType().FullName);

            return new GatewayDefaultPaymentMethodResult(
                Success: false,
                ErrorCode: ex.StripeError?.Code ?? "stripe_error",
                ErrorMessage: ex.StripeError?.Message ?? ex.Message);
        }
        catch (Exception ex)
        {
            logger.LogError("Unexpected error while updating default payment method for customer {CustomerId}: {ErrorType}",
                LogRedaction.RedactId(request.CustomerId, "cus"), ex.GetType().FullName);
            return SimulatedPaymentResultFactory.DefaultPaymentMethodFailure(ex.Message, "unexpected_error");
        }
    }

    /// <inheritdoc />
    public async Task<GatewayCancellationResult> CancelSubscriptionAsync(
        string externalSubscriptionId,
        CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Cancelling Stripe subscription {SubscriptionId} (Simulation: {IsSimulation})",
            LogRedaction.RedactId(externalSubscriptionId, "sub"), _options.UseSimulation);

        if (_options.UseSimulation)
        {
            await Task.Delay(1, cancellationToken).ConfigureAwait(false);
            return SimulatedPaymentResultFactory.CancellationSuccess(logger: logger);
        }

        try
        {
            var subscription = await _subscriptionService.CancelAsync(
                externalSubscriptionId,
                cancellationToken: cancellationToken).ConfigureAwait(false);

            logger.LogInformation("Stripe subscription {SubscriptionId} cancelled with status {Status}",
                LogRedaction.RedactId(subscription.Id, "sub"), LogRedaction.Sanitize(subscription.Status));

            return new GatewayCancellationResult(
                Success: subscription.Status == "canceled",
                ErrorCode: null,
                ErrorMessage: null,
                EffectiveDate: subscription.CanceledAt ?? SystemClock.UtcNow);
        }
        catch (StripeException ex)
        {
            logger.LogError("Stripe subscription cancellation failed for {SubscriptionId}: {ErrorCode} ({ErrorType})",
                LogRedaction.RedactId(externalSubscriptionId, "sub"), LogRedaction.Sanitize(ex.StripeError?.Code), ex.GetType().FullName);

            return new GatewayCancellationResult(
                Success: false,
                ErrorCode: ex.StripeError?.Code ?? "stripe_error",
                ErrorMessage: ex.StripeError?.Message ?? ex.Message,
                EffectiveDate: null);
        }
        catch (Exception ex)
        {
            logger.LogError("Unexpected error during subscription cancellation for {SubscriptionId}: {ErrorType}",
                LogRedaction.RedactId(externalSubscriptionId, "sub"), ex.GetType().FullName);
            return SimulatedPaymentResultFactory.CancellationFailure(ex.Message, "unexpected_error");
        }
    }
}
