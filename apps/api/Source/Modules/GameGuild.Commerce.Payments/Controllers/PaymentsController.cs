using Asp.Versioning;
using GameGuild.Commerce;
using GameGuild.Configuration.PresentationLayer.RateLimiting;
using GameGuild.CQRS;
using GameGuild.Identity.Context.Actors;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace GameGuild.Commerce.Payments;

/// <summary>
///     API controller for payment operations.
///     All endpoints require authentication; sensitive financial data must be protected.
///     Rate limiting is applied to prevent DoS attacks and abuse:
///     - ExpensiveOperations policy for mutations (payment processing, refunds)
///     - Api policy for query endpoints
/// </summary>
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/[controller]")]
[Microsoft.AspNetCore.Http.Tags("payments")]
[Authorize]
[EnableRateLimiting(RateLimitPolicies.ExpensiveOperations)]
public sealed class PaymentsController(
    ISender sender,
    IActorContextAccessor actorContextAccessor,
    IStripeCustomerService stripeCustomerService,
    ISubscriptionPaymentContextService subscriptionPaymentContextService) : BaseApiController
{
    /// <summary>
    ///     Retrieve all payment transactions with optional filtering
    /// </summary>
    /// <param name="tenantId">Optional tenant ID filter</param>
    /// <param name="status">Optional payment status filter (pending, completed, failed, cancelled, refunded)</param>
    /// <param name="startDate">Optional start date filter for payments (inclusive)</param>
    /// <param name="endDate">Optional end date filter for payments (inclusive)</param>
    /// <param name="page">Page number for pagination (default: 1)</param>
    /// <param name="pageSize">Number of items per page (default: 20, max: 100)</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Paginated list of payment transactions matching the specified criteria</returns>
    /// <remarks>
    ///     Retrieves a paginated list of all payment transactions with support for filtering by tenant, status,
    ///     and date range. This is the primary endpoint for payment administration and reporting.
    ///     Supported status filters:
    ///     - pending: Payments currently being processed
    ///     - completed: Successfully processed payments
    ///     - failed: Payments that encountered errors
    ///     - cancelled: Payments cancelled before completion
    ///     - refunded: Payments that have been refunded
    ///     Query Parameters:
    ///     - tenantId: Filter payments for specific tenant
    ///     - status: Filter by payment status
    ///     - startDate: Include payments from this date onwards (ISO 8601 format)
    ///     - endDate: Include payments up to this date (ISO 8601 format)
    ///     - page: Pagination page number (1-based)
    ///     - pageSize: Items per page (1-100)
    ///     Use cases:
    ///     - Financial reporting and reconciliation
    ///     - Payment monitoring and analytics
    ///     - Administrative payment management
    ///     - Audit trail generation
    /// </remarks>
    [HttpGet]
    [EnableRateLimiting(RateLimitPolicies.Api)]
    [EndpointSummary("Retrieve all payment transactions with optional filtering")]
    [EndpointDescription("Retrieves a paginated list of all payment transactions with support for filtering by tenant, status, and date range. This is the primary endpoint for payment administration and reporting.")]
    [ProducesResponseType<IEnumerable<PaymentResult>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetAll(
        [FromQuery] Guid? tenantId,
        [FromQuery] string? status,
        [FromQuery] DateTime? startDate,
        [FromQuery] DateTime? endDate,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default
    )
    {
        if (page < 1) page = 1;
        if (pageSize < 1 || pageSize > 100) pageSize = 20;

        var actorContext = actorContextAccessor.ActorContext;
        if (actorContext.IsAuthenticated && !actorContext.IsSystemAdmin)
        {
            var requestedTenantId = tenantId ?? actorContext.TenantId ?? Guid.Empty;
            var validationError = ValidateTenantAccess(requestedTenantId, "list payments");
            if (validationError != null) return validationError;

            tenantId = actorContext.TenantId;
        }

        return Ok(await sender.Send(new GetAllPaymentsQuery(tenantId, status, startDate, endDate, page, pageSize), ct).ConfigureAwait(false));
    }

    /// <summary>
    ///     Process a new payment transaction
    /// </summary>
    /// <param name="body">Payment processing request containing tenant ID, subscription ID, amount, and payment method</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Payment result with transaction ID and processing status</returns>
    /// <remarks>
    ///     Initiates a new payment transaction for a subscription. This endpoint handles the complete payment
    ///     processing workflow including payment method validation, amount verification, and transaction execution.
    ///     Returns the payment result immediately with a transaction ID that can be used to track payment status.
    ///     Processing workflow:
    ///     1. Validate payment method and amount
    ///     2. Verify subscription and tenant information
    ///     3. Process payment through configured payment gateway
    ///     4. Update subscription status based on result
    ///     5. Generate transaction record
    ///     Request body must include:
    ///     - TenantId: Organization identifier
    ///     - SubscriptionId: Target subscription
    ///     - Amount: Payment amount in base currency units
    ///     - PaymentMethodId: Stripe payment method identifier starting with pm_
    ///     Returns CreatedAtRoute with payment details for successful transactions.
    /// </remarks>
    [HttpPost]
    [EndpointSummary("Process a new payment transaction")]
    [EndpointDescription(
        "Initiates a new payment transaction for a subscription. This endpoint handles the complete payment processing workflow including payment method validation, amount verification, and transaction execution. Returns the payment result immediately with a transaction ID that can be used to track payment status."
    )]
    [ProducesResponseType<PaymentResult>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Process([FromBody] ProcessPaymentRequest body, CancellationToken ct)
    {
        if (body.TenantId == Guid.Empty)
        {
            return BadRequest(new { error = "TenantId cannot be empty" });
        }

        // SECURITY: Validate TenantId from authenticated context (prevents cross-tenant attack)
        var validationError = ValidateTenantAccess(body.TenantId, "process payment");
        if (validationError != null) return validationError;

        if (body.Amount <= 0)
        {
            return BadRequest(new { error = "Amount must be greater than zero" });
        }

        if (string.IsNullOrWhiteSpace(body.PaymentMethodId))
        {
            return BadRequest(new { error = "PaymentMethodId is required" });
        }

        if (!StripePaymentMethodIdentifier.IsValid(body.PaymentMethodId))
        {
            return BadRequest(new { error = StripePaymentMethodIdentifier.ValidationMessage });
        }

        var result = await sender.Send(new ProcessPaymentCommand(body.TenantId, body.SubscriptionId, body.Amount, body.PaymentMethodId), ct).ConfigureAwait(false);

        return CreatedAtRoute("GetPaymentById", new { paymentId = result.PaymentId }, result);
    }

    /// <summary>
    ///     Creates a Stripe SetupIntent for a subscription checkout.
    /// </summary>
    [HttpPost("setup-intents")]
    [EndpointSummary("Create a Stripe SetupIntent for subscription checkout")]
    [EndpointDescription("Creates or reuses a Stripe customer for the subscription and returns a SetupIntent client secret for PaymentElement-based card collection.")]
    [ProducesResponseType<CreateSetupIntentResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CreateSetupIntent([FromBody] CreateSetupIntentRequest body, CancellationToken ct)
    {
        if (body.TenantId == Guid.Empty)
        {
            return BadRequest(new { error = "TenantId cannot be empty" });
        }

        if (body.SubscriptionId == Guid.Empty)
        {
            return BadRequest(new { error = "SubscriptionId cannot be empty" });
        }

        var validationError = ValidateTenantAccess(body.TenantId, "create a setup intent");
        if (validationError != null) return validationError;

        var setupIntentResult = await sender.Send(new CreateSetupIntentCommand(
            body.TenantId,
            body.SubscriptionId,
            body.CustomerEmail,
            body.CustomerName), ct).ConfigureAwait(false);
        if (setupIntentResult.NotFound) return NotFound();
        if (!setupIntentResult.Success) return BadRequest(new { error = setupIntentResult.Error });

        return Ok(new CreateSetupIntentResponse(
            setupIntentResult.SubscriptionId,
            setupIntentResult.CustomerId!,
            setupIntentResult.SetupIntentId!,
            setupIntentResult.ClientSecret!));
    }

    /// <summary>
    ///     Completes a subscription checkout after Stripe confirms the SetupIntent.
    /// </summary>
    [HttpPost("subscription-checkouts:complete")]
    [EndpointSummary("Complete subscription checkout after setup confirmation")]
    [EndpointDescription("Sets the confirmed Stripe payment method as the customer's default and processes the first subscription charge.")]
    [ProducesResponseType<PaymentResult>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CompleteSubscriptionCheckout([FromBody] CompleteSubscriptionCheckoutRequest body, CancellationToken ct)
    {
        if (body.TenantId == Guid.Empty)
        {
            return BadRequest(new { error = "TenantId cannot be empty" });
        }

        if (body.SubscriptionId == Guid.Empty)
        {
            return BadRequest(new { error = "SubscriptionId cannot be empty" });
        }

        if (string.IsNullOrWhiteSpace(body.PaymentMethodId))
        {
            return BadRequest(new { error = "PaymentMethodId is required" });
        }

        if (!StripePaymentMethodIdentifier.IsValid(body.PaymentMethodId))
        {
            return BadRequest(new { error = StripePaymentMethodIdentifier.ValidationMessage });
        }

        var validationError = ValidateTenantAccess(body.TenantId, "complete subscription checkout");
        if (validationError != null) return validationError;

        var subscription = await subscriptionPaymentContextService.GetPaymentContextAsync(body.SubscriptionId, ct).ConfigureAwait(false);
        if (subscription == null) return NotFound();

        if (subscription.TenantId != body.TenantId)
        {
            return BadRequest(new { error = "Subscription does not belong to the specified tenant." });
        }

        if (subscription.Amount <= 0)
        {
            return BadRequest(new { error = "Only paid subscriptions require checkout completion." });
        }

        if (string.IsNullOrWhiteSpace(subscription.ExternalCustomerId))
        {
            return BadRequest(new { error = "Subscription does not have a Stripe customer associated with it yet." });
        }

        var defaultPaymentMethodResult = await stripeCustomerService.SetDefaultPaymentMethodAsync(
            new GatewayDefaultPaymentMethodRequest(subscription.ExternalCustomerId, body.PaymentMethodId),
            ct).ConfigureAwait(false);

        if (!defaultPaymentMethodResult.Success)
        {
            return BadRequest(new { error = defaultPaymentMethodResult.ErrorMessage ?? "Stripe could not store the confirmed payment method for future billing." });
        }

        var result = await sender.Send(
            new ProcessPaymentCommand(body.TenantId, body.SubscriptionId, subscription.Amount, body.PaymentMethodId),
            ct).ConfigureAwait(false);

        return Ok(result);
    }

    /// <summary>
    ///     Retrieve a specific payment by its unique identifier
    /// </summary>
    /// <param name="paymentId">The unique identifier of the payment to retrieve</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Payment details including status, amount, and transaction information</returns>
    /// <remarks>
    ///     Retrieves detailed information about a specific payment transaction, including its current status,
    ///     amount, payment method, and processing details. Use this endpoint to track payment progress
    ///     and verify transaction completion.
    ///     Response includes:
    ///     - Payment ID and transaction details
    ///     - Current payment status (pending, completed, failed, etc.)
    ///     - Payment method information
    ///     - Amount and currency
    ///     - Timestamps for creation and updates
    ///     - Associated subscription and tenant information
    /// </remarks>
    [HttpGet("{paymentId:guid}", Name = "GetPaymentById")]
    [EnableRateLimiting(RateLimitPolicies.Api)]
    [EndpointSummary("Retrieve a specific payment by its unique identifier")]
    [EndpointDescription(
        "Retrieves detailed information about a specific payment transaction, including its current status, amount, payment method, and processing details. Use this endpoint to track payment progress and verify transaction completion."
    )]
    [ProducesResponseType<PaymentResult>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid paymentId, CancellationToken ct)
    {
        var r = await sender.Send(new GetPaymentByIdQuery(paymentId), ct).ConfigureAwait(false);

        if (r == null) return NotFound();

        var validationError = ValidateTenantAccess(r.TenantId, "read payment");
        if (validationError != null) return validationError;

        return Ok(r);
    }

    /// <summary>
    ///     Cancel a payment transaction
    /// </summary>
    /// <param name="paymentId">The unique identifier of the payment to cancel</param>
    /// <param name="body">Cancellation request containing reason and optional metadata</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Payment cancellation result with cancellation details</returns>
    /// <remarks>
    ///     Cancels a payment transaction that is in progress or pending. This endpoint can be used to
    ///     cancel payments before they are processed, or to handle user-initiated cancellations during checkout.
    ///     Once canceled, a payment cannot be processed and may require a new payment attempt.
    ///     Cancellation scenarios:
    ///     - User abandons checkout process
    ///     - Administrative cancellation
    ///     - Fraud prevention trigger
    ///     - Duplicate transaction prevention
    ///     - Session timeout or expiration
    ///     - Payment method validation failure
    ///     Cancellation handling:
    ///     - Updates payment status to "canceled"
    ///     - Records cancellation reason and timestamp
    ///     - Releases any held resources or reservations
    ///     - Notifies relevant systems of cancellation
    ///     - Provides audit trail for investigation
    ///     Request body options:
    ///     - CancellationReason: Required reason for audit trail
    ///     - CanceledBy: Optional user ID for tracking
    ///     - Notes: Optional additional context
    ///     Important notes:
    ///     - Only pending or processing payments can be canceled
    ///     - Completed payments cannot be canceled (use refund instead)
    ///     - Cancellation is immediate and irreversible
    ///     - Some payment methods may have specific cancellation rules
    /// </remarks>
    [HttpPost("{paymentId:guid}:cancel")]
    [EndpointSummary("Cancel a payment transaction")]
    [EndpointDescription(
        "Cancels a payment transaction that is in progress or pending. Custom action per Google API guidelines. Once canceled, a payment cannot be processed and may require a new payment attempt."
    )]
    [ProducesResponseType<PaymentCancellationResult>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Cancel(Guid paymentId, [FromBody] CancelPaymentRequest body, CancellationToken ct)
    {
        var payment = await sender.Send(new GetPaymentByIdQuery(paymentId), ct).ConfigureAwait(false);
        if (payment == null) return NotFound();

        var validationError = ValidateTenantAccess(payment.TenantId, "cancel payment");
        if (validationError != null) return validationError;

        var result = await sender.Send(new CancelPaymentCommand(paymentId, body.CancellationReason, body.CanceledBy), ct).ConfigureAwait(false);

        return Ok(result);
    }

    /// <summary>
    ///     Process a refund for a completed payment
    /// </summary>
    /// <param name="paymentId">The unique identifier of the payment to refund</param>
    /// <param name="body">Refund request containing optional amount and reason</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Refund processing result with refund ID and status</returns>
    /// <remarks>
    ///     Processes a full or partial refund for a previously completed payment transaction.
    ///     If no amount is specified, a full refund will be processed. The refund reason is optional
    ///     but recommended for record keeping and customer service purposes. Refunds are processed
    ///     back to the original payment method and may take several business days to appear.
    ///     Refund types:
    ///     - Full refund: Refunds the entire payment amount
    ///     - Partial refund: Refunds a specified portion of the payment
    ///     Request body options:
    ///     - Amount: Specific refund amount (null for full refund)
    ///     - Reason: Optional reason for audit and customer service
    ///     Processing notes:
    ///     - Refunds are processed to the original payment method
    ///     - Processing time varies by payment processor (2-10 business days)
    ///     - Refund fees may apply depending on payment method
    ///     - Only successful payments can be refunded
    ///     - Multiple partial refunds allowed up to original amount
    ///     Response includes refund ID for tracking and customer communication.
    /// </remarks>
    [HttpPost("{paymentId:guid}:refund")]
    [EndpointSummary("Process a refund for a completed payment")]
    [EndpointDescription(
        "Processes a full or partial refund for a completed payment. Custom action per Google API guidelines. Refunds are processed back to the original payment method."
    )]
    [ProducesResponseType<ProcessRefundResult>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Refund(Guid paymentId, [FromBody] RefundRequest body, CancellationToken ct)
    {
        var payment = await sender.Send(new GetPaymentByIdQuery(paymentId), ct).ConfigureAwait(false);
        if (payment == null) return NotFound();

        var validationError = ValidateTenantAccess(payment.TenantId, "refund payment");
        if (validationError != null) return validationError;

        var r = await sender.Send(new ProcessRefundCommand(paymentId, body.Amount ?? 0, body.Reason ?? "No reason provided"), ct).ConfigureAwait(false);

        return Ok(r);
    }

    /// <summary>
    ///     Retry a failed payment transaction
    /// </summary>
    /// <param name="paymentId">The unique identifier of the failed payment to retry</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Payment retry result with updated transaction status</returns>
    /// <remarks>
    ///     Attempts to reprocess a previously failed payment transaction using the same payment method and amount.
    ///     This is useful when payments fail due to temporary issues like network problems or insufficient funds
    ///     that have since been resolved. The retry operation creates a new transaction attempt while maintaining
    ///     the link to the original payment record.
    ///     Retry scenarios:
    ///     - Temporary network connectivity issues resolved
    ///     - Insufficient funds now available
    ///     - Payment gateway was temporarily unavailable
    ///     - Rate limiting issues have cleared
    ///     - Card issuer temporary restrictions lifted
    ///     Important notes:
    ///     - Only failed payments can be retried
    ///     - Successful payments will return a 400 Bad Request
    ///     - Retry preserves original payment details
    ///     - New transaction ID is generated for the retry attempt
    ///     - Original payment record maintains audit trail
    /// </remarks>
    [HttpPost("{paymentId:guid}:retry")]
    [EndpointSummary("Retry a failed payment transaction")]
    [EndpointDescription(
        "Retries a failed payment using the original payment method. Custom action per Google API guidelines. Creates a new transaction attempt while maintaining the link to the original payment record."
    )]
    [ProducesResponseType<PaymentRetryResult>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Retry(Guid paymentId, CancellationToken ct)
    {
        var payment = await sender.Send(new GetPaymentByIdQuery(paymentId), ct).ConfigureAwait(false);
        if (payment == null) return NotFound();

        var validationError = ValidateTenantAccess(payment.TenantId, "retry payment");
        if (validationError != null) return validationError;

        var r = await sender.Send(new RetryPaymentCommand(paymentId), ct).ConfigureAwait(false);

        return Ok(r);
    }

    public sealed record ProcessPaymentRequest(Guid TenantId, Guid SubscriptionId, decimal Amount, string PaymentMethodId);

    public sealed record CreateSetupIntentRequest(Guid TenantId, Guid SubscriptionId, string? CustomerEmail, string? CustomerName);

    public sealed record CreateSetupIntentResponse(Guid SubscriptionId, string CustomerId, string SetupIntentId, string ClientSecret);

    public sealed record CompleteSubscriptionCheckoutRequest(Guid TenantId, Guid SubscriptionId, string PaymentMethodId);

    public sealed record RefundRequest(decimal? Amount, string? Reason);

    public sealed record CancelPaymentRequest(string CancellationReason, Guid? CanceledBy = null, string? Notes = null);

    #region Private Methods

    /// <summary>
    ///     Validates that the authenticated user has access to the specified tenant.
    ///     Uses shared TenantValidationExtensions for DRY compliance.
    /// </summary>
    private IActionResult? ValidateTenantAccess(Guid requestedTenantId, string operation)
        => actorContextAccessor.ActorContext.IsSystemAdmin
            ? null
            : actorContextAccessor.ValidateTenantAccessAsActionResult(requestedTenantId, operation);

    #endregion
}
