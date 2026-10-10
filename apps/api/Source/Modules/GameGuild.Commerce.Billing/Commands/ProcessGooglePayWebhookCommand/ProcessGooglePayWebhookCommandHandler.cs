using GameGuild.CQRS;
using Microsoft.Extensions.Logging;

namespace GameGuild.Commerce.Billing;

/// <summary>
///     Handler for ProcessGooglePayWebhookCommand.
///     Verifies the callback bearer JWT, then delegates to GooglePayBillingWebhookService
///     for idempotent inbox processing.
/// </summary>
public sealed class ProcessGooglePayWebhookCommandHandler(
    GooglePayBillingWebhookService googlePayWebhookService,
    ILogger<ProcessGooglePayWebhookCommandHandler> logger
) : ICommandHandler<ProcessGooglePayWebhookCommand, WebhookProcessingResult>
{
    public async Task<WebhookProcessingResult> Handle(ProcessGooglePayWebhookCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        logger.LogInformation(
            "Processing Google Pay webhook. Payload length: {PayloadLength}, Project: {ProjectId}",
            request.Payload.Length,
            request.ProjectId);

        var result = await googlePayWebhookService.ProcessGooglePayWebhookAsync(
                request.Payload,
                request.AuthHeader,
                request.ProjectId,
                cancellationToken)
            .ConfigureAwait(false);

        if (result.Processed)
        {
            logger.LogInformation(
                "[WEBHOOK_METRIC] Provider=googlepay Status=Success EventId={EventId} WasAlreadyProcessed={WasAlreadyProcessed}",
                result.EventId,
                result.WasAlreadyProcessed);
        }
        else
        {
            logger.LogWarning(
                "[WEBHOOK_METRIC] Provider=googlepay Status=Failed EventId={EventId} Error={Error} RequiresRetry={RequiresRetry}",
                result.EventId,
                result.ErrorMessage,
                result.RequiresRetry);
        }

        return result;
    }
}
