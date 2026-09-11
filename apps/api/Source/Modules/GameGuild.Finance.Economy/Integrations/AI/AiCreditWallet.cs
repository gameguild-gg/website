using System.ComponentModel.DataAnnotations;
using System.Numerics;
using GameGuild.Finance.Economy.Contracts;
using GameGuild.Finance.Economy.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.Extensions.Options;

namespace GameGuild.Finance.Economy.Integrations.AI;

public enum AiCreditReservationStatus
{
    Reserved = 1,
    Settled = 2,
    Released = 3,
}

public sealed class InsufficientAiCreditsException(long available, long required)
    : InvalidOperationException($"Insufficient AI credits. Available: {available}; required: {required}.")
{
    public long Available { get; } = available;
    public long Required { get; } = required;
}

public sealed class AiCreditReservation
{
    private AiCreditReservation() { }

    public Guid Id { get; private set; }
    public Guid RunId { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid ActorId { get; private set; }
    public Guid WalletId { get; private set; }
    public string ServiceCode { get; private set; } = string.Empty;
    public string Provider { get; private set; } = string.Empty;
    public string Model { get; private set; } = string.Empty;
    public string RateCardVersion { get; private set; } = string.Empty;
    public long InputSoftUnitsPerMillion { get; private set; }
    public long OutputSoftUnitsPerMillion { get; private set; }
    public long ReservedSoftUnits { get; private set; }
    public long SettledSoftUnits { get; private set; }
    public long ReleasedSoftUnits { get; private set; }
    public int InputTokens { get; private set; }
    public int OutputTokens { get; private set; }
    public string? ProviderUsageId { get; private set; }
    public AiCreditReservationStatus Status { get; private set; }
    public string ReservationIdempotencyKey { get; private set; } = string.Empty;
    public string? SettlementIdempotencyKey { get; private set; }
    public string? ReleaseReason { get; private set; }
    public DateTimeOffset ReservedAt { get; private set; }
    public DateTimeOffset? SettledAt { get; private set; }
    public DateTimeOffset? ReleasedAt { get; private set; }
    public long Version { get; private set; }

    public static AiCreditReservation Create(
        Guid runId,
        Guid tenantId,
        Guid actorId,
        Guid walletId,
        string serviceCode,
        string provider,
        string model,
        long maximumSoftUnits,
        string idempotencyKey,
        DateTimeOffset now,
        string rateCardVersion = "direct-v1",
        long inputSoftUnitsPerMillion = 0,
        long outputSoftUnitsPerMillion = 0)
    {
        if (runId == Guid.Empty || tenantId == Guid.Empty || actorId == Guid.Empty || walletId == Guid.Empty)
            throw new ArgumentException("Run, tenant, actor, and wallet IDs are required.");
        ArgumentException.ThrowIfNullOrWhiteSpace(serviceCode);
        ArgumentException.ThrowIfNullOrWhiteSpace(provider);
        ArgumentException.ThrowIfNullOrWhiteSpace(model);
        ArgumentException.ThrowIfNullOrWhiteSpace(idempotencyKey);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumSoftUnits);
        ArgumentOutOfRangeException.ThrowIfNegative(inputSoftUnitsPerMillion);
        ArgumentOutOfRangeException.ThrowIfNegative(outputSoftUnitsPerMillion);

        return new AiCreditReservation
        {
            Id = Guid.NewGuid(),
            RunId = runId,
            TenantId = tenantId,
            ActorId = actorId,
            WalletId = walletId,
            ServiceCode = serviceCode.Trim(),
            Provider = provider.Trim(),
            Model = model.Trim(),
            RateCardVersion = rateCardVersion.Trim(),
            InputSoftUnitsPerMillion = inputSoftUnitsPerMillion,
            OutputSoftUnitsPerMillion = outputSoftUnitsPerMillion,
            ReservedSoftUnits = maximumSoftUnits,
            Status = AiCreditReservationStatus.Reserved,
            ReservationIdempotencyKey = idempotencyKey.Trim(),
            ReservedAt = now,
            Version = 1,
        };
    }

    public void Settle(
        int inputTokens,
        int outputTokens,
        long actualSoftUnits,
        string providerUsageId,
        string idempotencyKey,
        DateTimeOffset now)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(inputTokens);
        ArgumentOutOfRangeException.ThrowIfNegative(outputTokens);
        ArgumentOutOfRangeException.ThrowIfNegative(actualSoftUnits);
        if (actualSoftUnits > ReservedSoftUnits)
            throw new InvalidOperationException("Actual AI usage cannot exceed its maximum reservation.");
        ArgumentException.ThrowIfNullOrWhiteSpace(providerUsageId);
        ArgumentException.ThrowIfNullOrWhiteSpace(idempotencyKey);

        if (Status == AiCreditReservationStatus.Settled && SettlementIdempotencyKey == idempotencyKey.Trim())
            return;
        if (Status != AiCreditReservationStatus.Reserved)
            throw new InvalidOperationException("Only a reserved AI credit charge can be settled.");

        InputTokens = inputTokens;
        OutputTokens = outputTokens;
        SettledSoftUnits = actualSoftUnits;
        ReleasedSoftUnits = checked(ReservedSoftUnits - actualSoftUnits);
        ProviderUsageId = providerUsageId.Trim();
        SettlementIdempotencyKey = idempotencyKey.Trim();
        Status = AiCreditReservationStatus.Settled;
        SettledAt = now;
        Version++;
    }

    public void Release(string reason, string idempotencyKey, DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        ArgumentException.ThrowIfNullOrWhiteSpace(idempotencyKey);
        if (Status == AiCreditReservationStatus.Released && SettlementIdempotencyKey == idempotencyKey.Trim())
            return;
        if (Status != AiCreditReservationStatus.Reserved)
            throw new InvalidOperationException("Only a reserved AI credit charge can be released.");

        ReleasedSoftUnits = ReservedSoftUnits;
        ReleaseReason = reason.Trim();
        SettlementIdempotencyKey = idempotencyKey.Trim();
        Status = AiCreditReservationStatus.Released;
        ReleasedAt = now;
        Version++;
    }
}

public sealed class AiCreditRateCard
{
    private AiCreditRateCard() { }
    public Guid Id { get; private set; }
    public string Version { get; private set; } = string.Empty;
    public string ServiceCode { get; private set; } = string.Empty;
    public string Provider { get; private set; } = string.Empty;
    public string Model { get; private set; } = string.Empty;
    public long InputSoftUnitsPerMillion { get; private set; }
    public long OutputSoftUnitsPerMillion { get; private set; }
    public DateTimeOffset EffectiveAt { get; private set; }
    public DateTimeOffset? ExpiresAt { get; private set; }

    public static AiCreditRateCard Create(string version, string serviceCode, string provider, string model,
        long inputSoftUnitsPerMillion, long outputSoftUnitsPerMillion, DateTimeOffset effectiveAt) => new()
        {
            Id = Guid.NewGuid(),
            Version = version.Trim(),
            ServiceCode = serviceCode.Trim(),
            Provider = provider.Trim(),
            Model = model.Trim(),
            InputSoftUnitsPerMillion = inputSoftUnitsPerMillion,
            OutputSoftUnitsPerMillion = outputSoftUnitsPerMillion,
            EffectiveAt = effectiveAt,
        };

    public long Price(int inputTokens, int outputTokens)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(inputTokens);
        ArgumentOutOfRangeException.ThrowIfNegative(outputTokens);
        var input = Ceiling((BigInteger)inputTokens * InputSoftUnitsPerMillion, 1_000_000);
        var output = Ceiling((BigInteger)outputTokens * OutputSoftUnitsPerMillion, 1_000_000);
        var total = input + output;
        return total == 0 && inputTokens + outputTokens > 0 ? 1 : checked((long)total);
    }

    private static BigInteger Ceiling(BigInteger value, int divisor) =>
        BigInteger.DivRem(value, divisor, out var remainder) + (remainder.IsZero ? 0 : 1);
}

public sealed record AiCreditQuote(
    string RateCardVersion,
    string Provider,
    string Model,
    int MaximumInputTokens,
    int MaximumOutputTokens,
    long MaximumSoftUnits,
    long InputSoftUnitsPerMillion,
    long OutputSoftUnitsPerMillion);

public sealed record AiCreditBalance(long AvailableSoftUnits, long ReservedSoftUnits, long SettledSoftUnits);

public interface IAiCreditWalletService
{
    Task<AiCreditBalance> GetBalanceAsync(Guid tenantId, Guid actorId, CancellationToken cancellationToken = default);
    Task<AiCreditQuote> QuoteAsync(string serviceCode, string provider, string model, int maximumInputTokens, int maximumOutputTokens, CancellationToken cancellationToken = default);
    Task<AiCreditReservation> ReserveAsync(Guid runId, Guid tenantId, Guid actorId, string serviceCode, AiCreditQuote quote, string idempotencyKey, CancellationToken cancellationToken = default);
    Task<AiCreditReservation> SettleAsync(Guid runId, int inputTokens, int outputTokens, string providerUsageId, string idempotencyKey, CancellationToken cancellationToken = default);
    Task<AiCreditReservation> ReleaseAsync(Guid runId, string reason, string idempotencyKey, CancellationToken cancellationToken = default);
}

public sealed class AiCreditPricingOptions
{
    public const string SectionName = "Economy:AiCredits";
    public long DefaultInputSoftUnitsPerMillion { get; set; } = 2_000;
    public long DefaultOutputSoftUnitsPerMillion { get; set; } = 8_000;
}

internal sealed class AiCreditWalletService(
    IApplicationDbContext db,
    IOptions<AiCreditPricingOptions> options,
    TimeProvider timeProvider) : IAiCreditWalletService
{
    public async Task<AiCreditBalance> GetBalanceAsync(Guid tenantId, Guid actorId, CancellationToken cancellationToken = default)
    {
        var (wallet, projection) = await WalletAsync(tenantId, actorId, cancellationToken).ConfigureAwait(false);
        var charges = await db.Set<AiCreditReservation>()
            .Where(candidate => candidate.WalletId == wallet.Id)
            .GroupBy(_ => 1)
            .Select(group => new
            {
                Reserved = group.Where(item => item.Status == AiCreditReservationStatus.Reserved).Sum(item => item.ReservedSoftUnits),
                Settled = group.Where(item => item.Status == AiCreditReservationStatus.Settled).Sum(item => item.SettledSoftUnits),
            })
            .SingleOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        var reserved = charges?.Reserved ?? 0;
        var settled = charges?.Settled ?? 0;
        return new AiCreditBalance(Math.Max(0, projection.AvailableSoftToSpend - reserved - settled), reserved, settled);
    }

    public async Task<AiCreditQuote> QuoteAsync(string serviceCode, string provider, string model, int maximumInputTokens, int maximumOutputTokens, CancellationToken cancellationToken = default)
    {
        var now = timeProvider.GetUtcNow();
        var card = await CurrentRateCard(serviceCode, provider, model, now, cancellationToken)
            .ConfigureAwait(false);
        if (card is null)
        {
            var values = options.Value;
            card = AiCreditRateCard.Create(
                $"default-{now:yyyyMMdd}", serviceCode, provider, model,
                values.DefaultInputSoftUnitsPerMillion,
                values.DefaultOutputSoftUnitsPerMillion,
                now);
            db.Set<AiCreditRateCard>().Add(card);
            try
            {
                await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (DbUpdateException)
            {
                if (db is DbContext context)
                    context.Entry(card).State = EntityState.Detached;
                card = await CurrentRateCard(serviceCode, provider, model, now, cancellationToken)
                    .ConfigureAwait(false);
                if (card is null)
                    throw;
            }
        }
        var maximum = card.Price(maximumInputTokens, maximumOutputTokens);
        return new AiCreditQuote(card.Version, provider, model, maximumInputTokens, maximumOutputTokens,
            maximum, card.InputSoftUnitsPerMillion, card.OutputSoftUnitsPerMillion);
    }

    private Task<AiCreditRateCard?> CurrentRateCard(
        string serviceCode,
        string provider,
        string model,
        DateTimeOffset now,
        CancellationToken cancellationToken) =>
        db.Set<AiCreditRateCard>().AsNoTracking()
            .Where(candidate => candidate.ServiceCode == serviceCode &&
                                candidate.Provider == provider &&
                                candidate.Model == model &&
                                candidate.EffectiveAt <= now &&
                                (candidate.ExpiresAt == null || candidate.ExpiresAt > now))
            .OrderByDescending(candidate => candidate.EffectiveAt)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<AiCreditReservation> ReserveAsync(Guid runId, Guid tenantId, Guid actorId, string serviceCode, AiCreditQuote quote, string idempotencyKey, CancellationToken cancellationToken = default)
    {
        ValidateActor(tenantId, actorId);
        ArgumentException.ThrowIfNullOrWhiteSpace(idempotencyKey);
        var relationalContext = db as DbContext;
        await using var transaction = relationalContext?.Database.IsRelational() == true
            ? await db.BeginTransactionAsync(cancellationToken).ConfigureAwait(false)
            : null;
        var (wallet, _) = await WalletAsync(tenantId, actorId, cancellationToken).ConfigureAwait(false);
        if (relationalContext is not null && transaction is not null)
        {
            _ = await relationalContext.Database.SqlQuery<int>($"""
                    SELECT 1 AS "Value"
                    FROM public.economy_wallets
                    WHERE "Id" = {wallet.Id}
                    FOR UPDATE
                    """)
                .SingleAsync(cancellationToken)
                .ConfigureAwait(false);
        }

        var duplicate = await db.Set<AiCreditReservation>()
            .SingleOrDefaultAsync(candidate => candidate.ReservationIdempotencyKey == idempotencyKey, cancellationToken)
            .ConfigureAwait(false);
        if (duplicate is not null)
        {
            if (duplicate.RunId != runId || duplicate.ActorId != actorId || duplicate.TenantId != tenantId ||
                !string.Equals(duplicate.ServiceCode, serviceCode.Trim(), StringComparison.Ordinal) ||
                !string.Equals(duplicate.Provider, quote.Provider, StringComparison.Ordinal) ||
                !string.Equals(duplicate.Model, quote.Model, StringComparison.Ordinal) ||
                !string.Equals(duplicate.RateCardVersion, quote.RateCardVersion, StringComparison.Ordinal) ||
                duplicate.ReservedSoftUnits != quote.MaximumSoftUnits ||
                duplicate.InputSoftUnitsPerMillion != quote.InputSoftUnitsPerMillion ||
                duplicate.OutputSoftUnitsPerMillion != quote.OutputSoftUnitsPerMillion)
                throw new InvalidOperationException("AI credit idempotency key is bound to another reservation request.");
            if (transaction is not null)
                await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return duplicate;
        }

        var balance = await GetBalanceAsync(tenantId, actorId, cancellationToken).ConfigureAwait(false);
        if (balance.AvailableSoftUnits < quote.MaximumSoftUnits)
            throw new InsufficientAiCreditsException(balance.AvailableSoftUnits, quote.MaximumSoftUnits);

        var reservation = AiCreditReservation.Create(
            runId, tenantId, actorId, wallet.Id, serviceCode, quote.Provider, quote.Model,
            quote.MaximumSoftUnits, idempotencyKey, timeProvider.GetUtcNow(), quote.RateCardVersion,
            quote.InputSoftUnitsPerMillion, quote.OutputSoftUnitsPerMillion);
        db.Set<AiCreditReservation>().Add(reservation);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        if (transaction is not null)
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return reservation;
    }

    public async Task<AiCreditReservation> SettleAsync(Guid runId, int inputTokens, int outputTokens, string providerUsageId, string idempotencyKey, CancellationToken cancellationToken = default)
    {
        var reservation = await FindReservation(runId, cancellationToken).ConfigureAwait(false);
        var actual = Price(inputTokens, outputTokens, reservation.InputSoftUnitsPerMillion, reservation.OutputSoftUnitsPerMillion);
        reservation.Settle(inputTokens, outputTokens, actual, providerUsageId, idempotencyKey, timeProvider.GetUtcNow());
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return reservation;
    }

    public async Task<AiCreditReservation> ReleaseAsync(Guid runId, string reason, string idempotencyKey, CancellationToken cancellationToken = default)
    {
        var reservation = await FindReservation(runId, cancellationToken).ConfigureAwait(false);
        reservation.Release(reason, idempotencyKey, timeProvider.GetUtcNow());
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return reservation;
    }

    private async Task<(EconomyWalletRow Wallet, EconomyWalletBalanceProjectionRow Projection)> WalletAsync(Guid tenantId, Guid actorId, CancellationToken cancellationToken)
    {
        ValidateActor(tenantId, actorId);
        var wallet = await db.Set<EconomyWalletRow>()
            .SingleOrDefaultAsync(candidate => candidate.TenantId == tenantId && candidate.OwnerId == actorId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new InsufficientAiCreditsException(0, 1);
        var projection = await db.Set<EconomyWalletBalanceProjectionRow>()
            .SingleOrDefaultAsync(candidate => candidate.WalletId == wallet.Id, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new InsufficientAiCreditsException(0, 1);
        return (wallet, projection);
    }

    private Task<AiCreditReservation> FindReservation(Guid runId, CancellationToken cancellationToken) =>
        db.Set<AiCreditReservation>().SingleAsync(candidate => candidate.RunId == runId, cancellationToken);

    private static void ValidateActor(Guid tenantId, Guid actorId)
    {
        if (tenantId == Guid.Empty || actorId == Guid.Empty)
            throw new UnauthorizedAccessException("AI credit operations require an authenticated tenant actor.");
    }

    private static long Price(int inputTokens, int outputTokens, long inputRate, long outputRate)
    {
        var input = BigInteger.DivRem((BigInteger)inputTokens * inputRate, 1_000_000, out var inputRemainder) + (inputRemainder.IsZero ? 0 : 1);
        var output = BigInteger.DivRem((BigInteger)outputTokens * outputRate, 1_000_000, out var outputRemainder) + (outputRemainder.IsZero ? 0 : 1);
        var total = checked((long)(input + output));
        return total == 0 && inputTokens + outputTokens > 0 ? 1 : total;
    }
}

internal sealed class AiCreditReservationConfiguration : IEntityTypeConfiguration<AiCreditReservation>
{
    public void Configure(EntityTypeBuilder<AiCreditReservation> builder)
    {
        builder.ToTable("ai_credit_reservations");
        builder.HasKey(candidate => candidate.Id);
        builder.Property(candidate => candidate.Id).ValueGeneratedNever();
        builder.Property(candidate => candidate.ServiceCode).HasMaxLength(128).IsRequired();
        builder.Property(candidate => candidate.Provider).HasMaxLength(64).IsRequired();
        builder.Property(candidate => candidate.Model).HasMaxLength(256).IsRequired();
        builder.Property(candidate => candidate.RateCardVersion).HasMaxLength(128).IsRequired();
        builder.Property(candidate => candidate.ReservationIdempotencyKey).HasMaxLength(128).IsRequired();
        builder.Property(candidate => candidate.SettlementIdempotencyKey).HasMaxLength(128);
        builder.Property(candidate => candidate.ProviderUsageId).HasMaxLength(256);
        builder.Property(candidate => candidate.Version).IsConcurrencyToken();
        builder.HasIndex(candidate => candidate.RunId).IsUnique();
        builder.HasIndex(candidate => candidate.ReservationIdempotencyKey).IsUnique();
        builder.HasIndex(candidate => candidate.SettlementIdempotencyKey).IsUnique().HasFilter("\"SettlementIdempotencyKey\" IS NOT NULL");
        builder.HasIndex(candidate => new { candidate.TenantId, candidate.ActorId, candidate.ReservedAt });
    }
}

internal sealed class AiCreditRateCardConfiguration : IEntityTypeConfiguration<AiCreditRateCard>
{
    public void Configure(EntityTypeBuilder<AiCreditRateCard> builder)
    {
        builder.ToTable("ai_credit_rate_cards");
        builder.HasKey(candidate => candidate.Id);
        builder.Property(candidate => candidate.Id).ValueGeneratedNever();
        builder.Property(candidate => candidate.Version).HasMaxLength(128).IsRequired();
        builder.Property(candidate => candidate.ServiceCode).HasMaxLength(128).IsRequired();
        builder.Property(candidate => candidate.Provider).HasMaxLength(64).IsRequired();
        builder.Property(candidate => candidate.Model).HasMaxLength(256).IsRequired();
        builder.HasIndex(candidate => new { candidate.ServiceCode, candidate.Provider, candidate.Model, candidate.Version }).IsUnique();
    }
}
