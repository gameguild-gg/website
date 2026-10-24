using FluentValidation;
using GameGuild.CQRS;
using GameGuild.Identity.Authorization;
using GameGuild.Identity.Context.Actors;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace GameGuild.Commerce.Payments;

/// <summary>
///     Command (issue #412 spec B): set or replace an operator manual FX rate override for an
///     exact <c>base → quote</c> direction. Integrates the SharedKernel manual/fixed-override
///     semantics instead of duplicating them: the override is persisted as an
///     <see cref="FxRateSnapshot"/> (<c>IsOverride=true</c>), audit-logged through
///     <see cref="AuditTrail"/>, applied to the <see cref="FxRuntimeRateSource"/> (where it
///     wins over provider grids exactly like <see cref="FixedOverrideExchangeRateProvider"/>)
///     and the cached conversion-path entry for the pair is invalidated. Reciprocal directions
///     are NOT implied — pin both directions explicitly when conversions run both ways.
/// </summary>
/// <param name="BaseCurrency">ISO 4217 code the override converts from.</param>
/// <param name="QuoteCurrency">ISO 4217 code the override converts to.</param>
/// <param name="Rate">Quote units per one base unit; strictly positive.</param>
/// <param name="Reason">Audit reason (why the market rate is being pinned).</param>
/// <param name="ExpiresAtUtc">Optional expiry instant (UTC); null = effective until replaced.</param>
[AuthorizeRequest("", RequireSystemAdmin = true)]
public sealed record SetManualFxRateOverrideCommand(
    string BaseCurrency,
    string QuoteCurrency,
    decimal Rate,
    string Reason,
    DateTime? ExpiresAtUtc = null) : ICommand<Guid>;

/// <summary>Validator for <see cref="SetManualFxRateOverrideCommand"/>.</summary>
public sealed class SetManualFxRateOverrideCommandValidator : AbstractValidator<SetManualFxRateOverrideCommand>
{
    /// <summary>Initializes the validator.</summary>
    public SetManualFxRateOverrideCommandValidator()
    {
        RuleFor(command => command.BaseCurrency)
            .NotEmpty().Length(3).Matches("^[A-Za-z]{3}$")
            .WithMessage("Base currency must be a three-letter ISO 4217 code.");

        RuleFor(command => command.QuoteCurrency)
            .NotEmpty().Length(3).Matches("^[A-Za-z]{3}$")
            .WithMessage("Quote currency must be a three-letter ISO 4217 code.");

        RuleFor(command => command.Rate)
            .GreaterThan(0m)
            .WithMessage("Exchange rate must be strictly positive.");

        RuleFor(command => command.Reason)
            .NotEmpty()
            .MaximumLength(500)
            .WithMessage("An audit reason is required for manual FX rate overrides.");

        RuleFor(command => command.ExpiresAtUtc)
            .Must(instant => instant is null || instant.Value.Kind == DateTimeKind.Utc)
            .WithMessage("Expiry instant must be a UTC instant.");
    }
}

/// <summary>Handler for <see cref="SetManualFxRateOverrideCommand"/>.</summary>
public sealed class SetManualFxRateOverrideCommandHandler(
    IApplicationDbContext context,
    GameGuild.Identity.Context.Actors.IActorContextAccessor actorContextAccessor,
    FxRuntimeRateSource runtimeSource,
    IFxRateCache rateCache) : ICommandHandler<SetManualFxRateOverrideCommand, Guid>
{
    /// <inheritdoc />
    public async Task<Guid> Handle(SetManualFxRateOverrideCommand request, CancellationToken cancellationToken)
    {
        var actorId = FxAdministrationActor.RequireSystemAdmin(actorContextAccessor);
        var baseCurrency = (request.BaseCurrency ?? string.Empty).Trim().ToUpperInvariant();
        var quoteCurrency = (request.QuoteCurrency ?? string.Empty).Trim().ToUpperInvariant();

        if (!Iso4217CurrencyCatalog.Exists(baseCurrency) || !Iso4217CurrencyCatalog.Exists(quoteCurrency))
        {
            throw new BusinessRuleViolationException(
                CurrencyConfigurationRules.UnknownCurrency,
                $"Manual FX override requires known ISO 4217 codes (got {request.BaseCurrency}→{request.QuoteCurrency}).",
                new { baseCurrency, quoteCurrency });
        }

        var rate = new ExchangeRate(
            baseCurrency,
            quoteCurrency,
            request.Rate,
            new DateTimeOffset(SystemClock.UtcNow, TimeSpan.Zero),
            "manual-override");

        var now = SystemClock.UtcNow;
        if (request.ExpiresAtUtc is { } expiry && (expiry.Kind != DateTimeKind.Utc || expiry <= now))
        {
            throw new BusinessRuleViolationException("InvalidFxOverrideExpiry", "An override expiry must be a future UTC instant.");
        }

        var existing = await context.Set<FxRateSnapshot>()
            .Where(snapshot =>
                snapshot.IsOverride
                && snapshot.BaseCurrency == baseCurrency
                && snapshot.QuoteCurrency == quoteCurrency)
            .OrderByDescending(snapshot => snapshot.AsOfUtc)
            .ThenByDescending(snapshot => snapshot.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        string? before = null;
        Guid overrideId;
        if (existing is null)
        {
            var entity = new FxRateSnapshot
            {
                BaseCurrency = baseCurrency,
                QuoteCurrency = quoteCurrency,
                Rate = request.Rate,
                EffectiveDateUtc = now.Date,
                AsOfUtc = now,
                ProviderName = "manual",
                Source = "manual-override",
                IsOverride = true,
                ExpiresAtUtc = request.ExpiresAtUtc,
            };
            await context.Set<FxRateSnapshot>().AddAsync(entity, cancellationToken).ConfigureAwait(false);
            overrideId = entity.Id;
        }
        else
        {
            before = JsonSerializer.Serialize(new { rate = existing.Rate, expiresAtUtc = existing.ExpiresAtUtc });
            existing.Rate = request.Rate;
            existing.EffectiveDateUtc = now.Date;
            existing.AsOfUtc = now;
            existing.ExpiresAtUtc = request.ExpiresAtUtc;
            existing.Touch();
            overrideId = existing.Id;
        }

        context.Set<AuditTrail>().Add(new AuditTrail
        {
            EntityType = nameof(FxRateSnapshot),
            EntityId = overrideId,
            Action = AuditAction.ConfigurationChanged,
            OldValue = before,
            NewValue = JsonSerializer.Serialize(new
            {
                baseCurrency,
                quoteCurrency,
                rate = request.Rate,
                expiresAtUtc = request.ExpiresAtUtc,
                reason = request.Reason,
            }),
            ChangedBy = actorId, // acting user from the request context, never from the body
            ChangedAt = now,
            Metadata = JsonSerializer.Serialize(new { action = "SetManualFxRateOverride", reason = request.Reason }),
        });

        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        // Make the override effective immediately on the conversion path.
        await runtimeSource.EnsureSeededAsync(cancellationToken).ConfigureAwait(false);
        runtimeSource.ApplyOverride(rate, request.ExpiresAtUtc);
        await rateCache.InvalidateAsync(baseCurrency, quoteCurrency, cancellationToken).ConfigureAwait(false);

        return overrideId;
    }
}

/// <summary>
///     Command (issue #412 spec B): trigger one FX rate sync pass immediately (admin surface;
///     same code path as the scheduled worker). Returns the run outcome.
/// </summary>
[AuthorizeRequest("", RequireSystemAdmin = true)]
public sealed record RunFxRateSyncCommand : ICommand<FxRateSyncResult>;

/// <summary>
///     Handler for <see cref="RunFxRateSyncCommand"/>: delegates to
/// <see cref="IFxRateSyncService"/>. Manual runs are refused while no provider is configured —
/// a "none" provider run would only persist failure rows.
/// </summary>
public sealed class RunFxRateSyncCommandHandler(
    IFxRateSyncService syncService,
    Microsoft.Extensions.Options.IOptions<FxSyncOptions> options,
    IActorContextAccessor actorContextAccessor) : ICommandHandler<RunFxRateSyncCommand, FxRateSyncResult>
{
    /// <inheritdoc />
    public async Task<FxRateSyncResult> Handle(RunFxRateSyncCommand request, CancellationToken cancellationToken)
    {
        FxAdministrationActor.RequireSystemAdmin(actorContextAccessor);
        var settings = options.Value;
        if (settings.Provider == FxSyncOptions.ProviderNone)
        {
            throw new BusinessRuleViolationException(
                "FxProviderNotConfigured",
                "No FX rate provider is configured (FxRates:Provider='none'); there is nothing to sync. Configure a provider first.",
                new { provider = settings.Provider });
        }

        return await syncService.SyncAsync(cancellationToken).ConfigureAwait(false);
    }
}

internal static class FxAdministrationActor
{
    public static Guid RequireSystemAdmin(IActorContextAccessor actorContextAccessor)
    {
        var actor = actorContextAccessor.ActorContext;
        if (!actor.IsAuthenticated || !actor.IsSystemAdmin
            || actor.SubjectIdAsGuid is not { } actorId || actorId == Guid.Empty)
        {
            throw new UnauthorizedAccessException("Global FX administration requires an authenticated system administrator.");
        }

        return actorId;
    }
}
