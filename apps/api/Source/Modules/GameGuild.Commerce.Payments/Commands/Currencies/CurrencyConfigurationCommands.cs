using FluentValidation;
using GameGuild.CQRS;
using GameGuild.Identity.Authorization;
using GameGuild.Identity.Context.Actors;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using static GameGuild.Commerce.Payments.CurrencyConfigurationCommandHelpers;

namespace GameGuild.Commerce.Payments;

/// <summary>Business rule identifiers raised by the currency configuration commands.</summary>
public static class CurrencyConfigurationRules
{
    /// <summary>Unknown/unmapped ISO 4217 code supplied.</summary>
    public const string UnknownCurrency = "UnknownCurrency";

    /// <summary>The base currency cannot be disabled.</summary>
    public const string BaseCurrencyCannotBeDisabled = "BaseCurrencyCannotBeDisabled";

    /// <summary>Active subscriptions hold the currency; explicit confirmation is required.</summary>
    public const string CurrencyInUseByActiveSubscriptions = "CurrencyInUseByActiveSubscriptions";
}

/// <summary>
///     Command (issue #412 spec A): enable a currency for the current tenant. Creates the
///     per-tenant configuration row on first use (fail-closed default: absent = disabled).
///     Idempotent — enabling an already-enabled currency is a no-op.
/// </summary>
/// <param name="CurrencyCode">ISO 4217 code to enable (any casing).</param>
/// <param name="DisplaySymbol">Optional display-preference symbol override.</param>
/// <param name="DisplayOrder">Optional selector ordering hint.</param>
[AuthorizeRequest("", RequireTenantAdmin = true)]
public sealed record EnableCurrencyCommand(
    string CurrencyCode,
    string? DisplaySymbol = null,
    int? DisplayOrder = null) : ICommand;

/// <summary>Validator for <see cref="EnableCurrencyCommand"/>.</summary>
public sealed class EnableCurrencyCommandValidator : AbstractValidator<EnableCurrencyCommand>
{
    /// <summary>Initializes the validator.</summary>
    public EnableCurrencyCommandValidator()
    {
        RuleFor(command => command.CurrencyCode)
            .NotEmpty()
            .Length(3)
            .Matches("^[A-Za-z]{3}$")
            .WithMessage("Currency code must be a three-letter ISO 4217 code.");
    }
}

/// <summary>Handler for <see cref="EnableCurrencyCommand"/>.</summary>
public sealed class EnableCurrencyCommandHandler(
    IApplicationDbContext context,
    IActorContextAccessor actorContextAccessor) : ICommandHandler<EnableCurrencyCommand>
{
    /// <inheritdoc />
    public async Task<Unit> Handle(EnableCurrencyCommand request, CancellationToken cancellationToken)
    {
        var (tenantId, actorId) = ResolveActor(actorContextAccessor);
        var code = NormalizeOrThrow(request.CurrencyCode);

        var configuration = await context.Set<CurrencyConfiguration>()
            .FirstOrDefaultAsync(item => item.TenantId == tenantId && item.CurrencyCode == code, cancellationToken)
            .ConfigureAwait(false);

        string? before = null;
        if (configuration is null)
        {
            configuration = new CurrencyConfiguration
            {
                TenantId = tenantId,
                CurrencyCode = code,
                IsEnabled = true,
                DisplaySymbol = string.IsNullOrWhiteSpace(request.DisplaySymbol) ? null : request.DisplaySymbol.Trim(),
                DisplayOrder = request.DisplayOrder ?? 0,
            };
            await context.Set<CurrencyConfiguration>().AddAsync(configuration, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            before = Describe(configuration);
            configuration.IsEnabled = true;
            if (!string.IsNullOrWhiteSpace(request.DisplaySymbol))
            {
                configuration.DisplaySymbol = request.DisplaySymbol.Trim();
            }

            if (request.DisplayOrder.HasValue)
            {
                configuration.DisplayOrder = request.DisplayOrder.Value;
            }

            configuration.Touch();
        }

        context.Set<AuditTrail>().Add(new AuditTrail
        {
            TenantId = tenantId,
            EntityType = nameof(CurrencyConfiguration),
            EntityId = configuration.Id,
            Action = AuditAction.ConfigurationChanged,
            OldValue = before,
            NewValue = Describe(configuration),
            ChangedBy = actorId,
            ChangedAt = SystemClock.UtcNow,
            Metadata = JsonSerializer.Serialize(new { tenantId, action = "EnableCurrency" }),
        });

        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Unit.Value;
    }
}

/// <summary>
///     Command (issue #412 spec A): disable a currency for the current tenant. Guards:
///     the base currency is always rejected; a currency still held by active subscriptions
///     requires <paramref name="ConfirmActiveSubscriptionImpact"/> (warn-and-require-
///     confirmation) — the violation payload carries the subscription count so the admin
///     surface can show it before re-issuing with confirmation.
/// </summary>
/// <param name="CurrencyCode">ISO 4217 code to disable (any casing).</param>
/// <param name="ConfirmActiveSubscriptionImpact">
///     Explicit confirmation that active subscriptions holding this currency may keep running
///     while it is disabled for new purchases.
/// </param>
[AuthorizeRequest("", RequireTenantAdmin = true)]
public sealed record DisableCurrencyCommand(
    string CurrencyCode,
    bool ConfirmActiveSubscriptionImpact = false) : ICommand;

/// <summary>Validator for <see cref="DisableCurrencyCommand"/>.</summary>
public sealed class DisableCurrencyCommandValidator : AbstractValidator<DisableCurrencyCommand>
{
    /// <summary>Initializes the validator.</summary>
    public DisableCurrencyCommandValidator()
    {
        RuleFor(command => command.CurrencyCode)
            .NotEmpty()
            .Length(3)
            .Matches("^[A-Za-z]{3}$")
            .WithMessage("Currency code must be a three-letter ISO 4217 code.");
    }
}

/// <summary>Handler for <see cref="DisableCurrencyCommand"/>.</summary>
public sealed class DisableCurrencyCommandHandler(
    IApplicationDbContext context,
    IActorContextAccessor actorContextAccessor,
    ICurrencySubscriptionUsageProbe subscriptionUsageProbe) : ICommandHandler<DisableCurrencyCommand>
{
    /// <inheritdoc />
    public async Task<Unit> Handle(DisableCurrencyCommand request, CancellationToken cancellationToken)
    {
        var (tenantId, actorId) = ResolveActor(actorContextAccessor);
        var code = NormalizeOrThrow(request.CurrencyCode);

        var configuration = await context.Set<CurrencyConfiguration>()
            .FirstOrDefaultAsync(item => item.TenantId == tenantId && item.CurrencyCode == code, cancellationToken)
            .ConfigureAwait(false);

        // No row or already-disabled row: the fail-closed default state — nothing to do.
        if (configuration is null || !configuration.IsEnabled)
        {
            return Unit.Value;
        }

        if (configuration.IsBaseCurrency)
        {
            throw new BusinessRuleViolationException(
                CurrencyConfigurationRules.BaseCurrencyCannotBeDisabled,
                $"Currency {code} is the tenant's base currency and cannot be disabled. Set a different base currency first.",
                new { tenantId, currencyCode = code });
        }

        var activeSubscriptions = await subscriptionUsageProbe
            .CountActiveSubscriptionsUsingCurrencyAsync(tenantId, code, cancellationToken)
            .ConfigureAwait(false);

        if (!request.ConfirmActiveSubscriptionImpact && (activeSubscriptions is null || activeSubscriptions.Value > 0))
        {
            throw new BusinessRuleViolationException(
                CurrencyConfigurationRules.CurrencyInUseByActiveSubscriptions,
                activeSubscriptions is null
                    ? $"Currency {code} usage by active subscriptions could not be determined; explicit confirmation is required to disable it."
                    : $"Currency {code} is still held by {activeSubscriptions.Value} active subscription(s); explicit confirmation is required to disable it.",
                new { tenantId, currencyCode = code, activeSubscriptions });
        }

        var before = Describe(configuration);
        configuration.IsEnabled = false;
        configuration.IsBaseCurrency = false;
        configuration.Touch();

        context.Set<AuditTrail>().Add(new AuditTrail
        {
            TenantId = tenantId,
            EntityType = nameof(CurrencyConfiguration),
            EntityId = configuration.Id,
            Action = AuditAction.ConfigurationChanged,
            OldValue = before,
            NewValue = Describe(configuration),
            ChangedBy = actorId,
            ChangedAt = SystemClock.UtcNow,
            Metadata = JsonSerializer.Serialize(new
            {
                tenantId,
                action = "DisableCurrency",
                confirmedSubscriptionImpact = request.ConfirmActiveSubscriptionImpact,
                activeSubscriptions,
            }),
        });

        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Unit.Value;
    }
}

/// <summary>
///     Command (issue #412 spec A): set the tenant's base (calculation/reporting) currency.
///     Enables the target currency if needed and clears the previous base marker in the same
///     transaction (two-phase save keeps the one-base-per-tenant filtered unique index valid
///     at every statement boundary).
/// </summary>
/// <param name="CurrencyCode">ISO 4217 code to make the base currency (any casing).</param>
[AuthorizeRequest("", RequireTenantAdmin = true)]
public sealed record SetBaseCurrencyCommand(string CurrencyCode) : ICommand;

/// <summary>Validator for <see cref="SetBaseCurrencyCommand"/>.</summary>
public sealed class SetBaseCurrencyCommandValidator : AbstractValidator<SetBaseCurrencyCommand>
{
    /// <summary>Initializes the validator.</summary>
    public SetBaseCurrencyCommandValidator()
    {
        RuleFor(command => command.CurrencyCode)
            .NotEmpty()
            .Length(3)
            .Matches("^[A-Za-z]{3}$")
            .WithMessage("Currency code must be a three-letter ISO 4217 code.");
    }
}

/// <summary>Handler for <see cref="SetBaseCurrencyCommand"/>.</summary>
public sealed class SetBaseCurrencyCommandHandler(
    IApplicationDbContext context,
    IActorContextAccessor actorContextAccessor) : ICommandHandler<SetBaseCurrencyCommand>
{
    /// <inheritdoc />
    public async Task<Unit> Handle(SetBaseCurrencyCommand request, CancellationToken cancellationToken)
    {
        var (tenantId, actorId) = ResolveActor(actorContextAccessor);
        var code = NormalizeOrThrow(request.CurrencyCode);

        // The HTTP command pipeline already owns its transaction. Direct callers still
        // need one for the two saves, but must never commit or dispose a caller's transaction.
        await using var transaction = await context.BeginAuditedMutationTransactionAsync(cancellationToken).ConfigureAwait(false);

        var previousBase = await context.Set<CurrencyConfiguration>()
            .FirstOrDefaultAsync(item => item.TenantId == tenantId && item.IsBaseCurrency, cancellationToken)
            .ConfigureAwait(false);

        if (previousBase is not null && previousBase.CurrencyCode != code)
        {
            var previousBefore = Describe(previousBase);
            previousBase.IsBaseCurrency = false;
            previousBase.Touch();
            context.Set<AuditTrail>().Add(new AuditTrail
            {
                TenantId = tenantId,
                EntityType = nameof(CurrencyConfiguration),
                EntityId = previousBase.Id,
                Action = AuditAction.ConfigurationChanged,
                OldValue = previousBefore,
                NewValue = Describe(previousBase),
                ChangedBy = actorId,
                ChangedAt = SystemClock.UtcNow,
                Metadata = JsonSerializer.Serialize(new { tenantId, action = "ClearBaseCurrency" }),
            });
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        var target = await context.Set<CurrencyConfiguration>()
            .FirstOrDefaultAsync(item => item.TenantId == tenantId && item.CurrencyCode == code, cancellationToken)
            .ConfigureAwait(false);

        string? targetBefore = null;
        if (target is null)
        {
            target = new CurrencyConfiguration
            {
                TenantId = tenantId,
                CurrencyCode = code,
                IsEnabled = true,
                IsBaseCurrency = true,
            };
            await context.Set<CurrencyConfiguration>().AddAsync(target, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            targetBefore = Describe(target);
            target.IsEnabled = true;
            target.IsBaseCurrency = true;
            target.Touch();
        }

        context.Set<AuditTrail>().Add(new AuditTrail
        {
            TenantId = tenantId,
            EntityType = nameof(CurrencyConfiguration),
            EntityId = target.Id,
            Action = AuditAction.ConfigurationChanged,
            OldValue = targetBefore,
            NewValue = Describe(target),
            ChangedBy = actorId,
            ChangedAt = SystemClock.UtcNow,
            Metadata = JsonSerializer.Serialize(new { tenantId, action = "SetBaseCurrency" }),
        });

        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await transaction.CommitAuditedMutationAsync(cancellationToken).ConfigureAwait(false);
        return Unit.Value;
    }
}

internal static class CurrencyConfigurationCommandHelpers
{
    public static (Guid TenantId, Guid ActorId) ResolveActor(IActorContextAccessor actorContextAccessor)
    {
        var actorContext = actorContextAccessor.ActorContext;
        if (!actorContext.IsAuthenticated || !actorContext.IsTenantAdmin
            || actorContext.SubjectIdAsGuid is not { } actorId || actorId == Guid.Empty)
        {
            throw new UnauthorizedAccessException("Currency configuration requires an authenticated tenant administrator.");
        }

        var tenantId = actorContext.TenantId
            ?? throw new UnauthorizedAccessException("Currency configuration requires a tenant context.");

        if (tenantId == Guid.Empty)
        {
            throw new UnauthorizedAccessException("Currency configuration requires a valid tenant context.");
        }

        return (tenantId, actorId);
    }

    public static string NormalizeOrThrow(string currencyCode)
    {
        var normalized = (currencyCode ?? string.Empty).Trim().ToUpperInvariant();
        if (!Iso4217CurrencyCatalog.Exists(normalized))
        {
            throw new BusinessRuleViolationException(
                CurrencyConfigurationRules.UnknownCurrency,
                $"'{currencyCode}' is not a known ISO 4217 currency in the platform catalog.",
                new { currencyCode = normalized });
        }

        return normalized;
    }

    public static string Describe(CurrencyConfiguration configuration)
    {
        return JsonSerializer.Serialize(new
        {
            configuration.TenantId,
            configuration.CurrencyCode,
            configuration.IsEnabled,
            configuration.IsBaseCurrency,
            configuration.DisplaySymbol,
            configuration.DisplayOrder,
        });
    }
}
