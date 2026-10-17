using FluentValidation;
using GameGuild.CQRS;
using GameGuild.Commerce.Billing;
using GameGuild.Compliance.Audit;
using GameGuild.Identity.Context.Actors;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GameGuild.Commerce.Billing;

/// <summary>
///     Records a webhook signing-secret rotation for a provider (issue #408): the given new
///     secret becomes the active verification secret and the currently-effective secret
///     stays accepted for the configured rotation window
///     (<c>Billing:Webhook:SecretRotationWindowHours</c>, default 24h), after which it is
///     automatically deprecated. The acting administrator is resolved from the actor
///     context, never from the request. Secrets are stored encrypted at rest
///     (ASP.NET DataProtection) and never appear in logs or the response.
/// </summary>
/// <param name="ProviderKey">Provider key from <see cref="PaymentProviders"/> (stripe, paypal, ...).</param>
/// <param name="NewSecret">The new signing secret exactly as configured at the provider.</param>
public sealed record RotateWebhookSecretCommand(string ProviderKey, string NewSecret)
    : ICommand<WebhookSecretRotationResult>;

/// <summary>Result of a recorded webhook signing-secret rotation. Never contains secret material.</summary>
/// <param name="RotationId">The recorded rotation.</param>
/// <param name="ProviderKey">Normalized provider key.</param>
/// <param name="RotatedAtUtc">UTC instant the rotation was recorded.</param>
/// <param name="PreviousSecretValidUntilUtc">UTC instant after which the previous secret stops verifying.</param>
/// <param name="PreviousSecretStillAccepted">Always true on creation; the previous secret verifies for the whole window.</param>
public sealed record WebhookSecretRotationResult(
    Guid RotationId,
    string ProviderKey,
    DateTime RotatedAtUtc,
    DateTime PreviousSecretValidUntilUtc,
    bool PreviousSecretStillAccepted);

/// <summary>
///     Validator for <see cref="RotateWebhookSecretCommand"/>: the provider must be
///     supported (fail-closed on unknown keys) and the new secret must be a non-empty
///     value that differs from the currently-effective secret.
/// </summary>
public sealed class RotateWebhookSecretCommandValidator : AbstractValidator<RotateWebhookSecretCommand>
{
    public RotateWebhookSecretCommandValidator()
    {
        RuleFor(command => command.ProviderKey)
            .NotEmpty()
            .Must(provider => PaymentProviders.IsSupported(provider))
            .WithMessage("ProviderKey must be a supported external billing provider (stripe, paypal, applepay, apple_app_store, googlepay, google_play_store).");

        RuleFor(command => command.NewSecret)
            .NotEmpty()
            .MinimumLength(16)
            .MaximumLength(255)
            .WithMessage("NewSecret must be between 16 and 255 characters.");
    }
}

/// <summary>Handler for <see cref="RotateWebhookSecretCommand"/>.</summary>
public sealed class RotateWebhookSecretCommandHandler(
    IApplicationDbContext context,
    IWebhookSigningSecretProvider signingSecretProvider,
    IDataProtectionProvider protectionProvider,
    IOptions<BillingConfiguration> configuration,
    IActorContextAccessor actorContextAccessor,
    IAuditService auditService,
    ILogger<RotateWebhookSecretCommandHandler> logger) : ICommandHandler<RotateWebhookSecretCommand, WebhookSecretRotationResult>
{
    public async Task<WebhookSecretRotationResult> Handle(RotateWebhookSecretCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var providerKey = PaymentProviders.Normalize(request.ProviderKey);
        var newSecret = request.NewSecret.Trim();

        // Fail closed when the provider has no signing-secret verification configured:
        // rotating nothing would silently weaken verification.
        var currentSecret = configuration.Value.GetWebhookSecret(providerKey);
        if (string.IsNullOrWhiteSpace(currentSecret))
        {
            throw new InvalidOperationException(
                $"Provider '{providerKey}' has no webhook signing secret configured; rotation requires an existing secret.");
        }

        // The currently-effective secret is everything that verifies right now minus the
        // candidate: when the newest rotation is still inside its window the effective
        // secret is that rotation's active secret, otherwise the configuration secret.
        var effectiveSecrets = signingSecretProvider.GetValidSecrets(providerKey)
            .Where(secret => !string.Equals(secret, newSecret, StringComparison.Ordinal))
            .ToList();
        if (effectiveSecrets.Count == 0)
        {
            throw new InvalidOperationException(
                "The new secret must differ from the currently-effective signing secret.");
        }

        var actor = actorContextAccessor.ActorContext;
        var nowUtc = SystemClock.UtcNow;
        var windowEndsAtUtc = nowUtc.AddHours(Math.Max(1, configuration.Value.Webhook.SecretRotationWindowHours));

        var protector = protectionProvider.CreateProtector(WebhookSigningSecretProvider.ProtectionPurpose);
        var rotation = new WebhookSecretRotation(
            providerKey,
            protector.Protect(effectiveSecrets[0]),
            protector.Protect(newSecret),
            nowUtc,
            windowEndsAtUtc,
            actor.SubjectIdAsGuid);

        context.Set<WebhookSecretRotation>().Add(rotation);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        logger.LogInformation(
            "Webhook signing secret rotated for provider {ProviderKey} by actor {ActorId}; previous secret accepted until {WindowEnd}",
            providerKey,
            actor.SubjectId ?? "unknown",
            windowEndsAtUtc);

        // Rotation is a security-relevant administrative action: audited (delivery
        // failures are surfaced but never roll the rotation back).
        try
        {
            await auditService.LogAsync(new CreateAuditLogRequest
            {
                ActionType = AuditActionTypes.BillingWebhookSecretRotated,
                ResourceType = "WebhookSecretRotation",
                ResourceId = rotation.Id.ToString(),
                UserId = actor.SubjectIdAsGuid,
                Description = $"Webhook signing secret for provider '{providerKey}' was rotated by an administrator; the previous secret stays accepted until {windowEndsAtUtc:O}.",
                Metadata = new Dictionary<string, object?>
                {
                    ["providerKey"] = providerKey,
                    ["rotationId"] = rotation.Id.ToString(),
                    ["previousSecretValidUntilUtc"] = windowEndsAtUtc.ToString("O")
                },
                Success = true,
                RiskLevel = AuditRiskLevel.Medium,
                Category = AuditCategory.Security
            }).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Failed to write the audit record for the webhook signing-secret rotation {RotationId}; the rotation remains committed",
                rotation.Id);
        }

        return new WebhookSecretRotationResult(
            rotation.Id,
            providerKey,
            nowUtc,
            windowEndsAtUtc,
            PreviousSecretStillAccepted: true);
    }
}
