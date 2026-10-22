using FluentAssertions;
using GameGuild.Identity.Context.Actors;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Moq;
using Xunit;
using static GameGuild.Commerce.Payments.CurrencyConfigurationRules;

namespace GameGuild.Commerce.Payments.UnitTests;

/// <summary>
///     Currency configuration commands and query (issue #412 spec A): per-tenant currency
///     enablement with fail-closed defaults, base-currency guard (a base currency can never be
///     disabled), warn-and-require-confirmation when active subscriptions hold the currency
///     (including the null/unknown usage case), and the catalog-annotated tenant listing.
/// </summary>
public class CurrencyConfigurationTests
{
    private readonly Mock<IActorContextAccessor> _actorAccessor = new();
    private readonly Mock<ICurrencySubscriptionUsageProbe> _usageProbe = new();
    private readonly ActorContext _actor;

    public CurrencyConfigurationTests()
    {
        _actor = AuthenticatedActor();
        _actorAccessor.SetupGet(accessor => accessor.ActorContext).Returns(_actor);
        _usageProbe
            .Setup(probe => probe.CountActiveSubscriptionsUsingCurrencyAsync(
                It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);
    }

    [Fact]
    public async Task EnableCurrency_CreatesEnabledRow_AndIsIdempotent()
    {
        await using var db = NewDatabase();

        var handler = new EnableCurrencyCommandHandler(db, _actorAccessor.Object);
        await handler.Handle(new EnableCurrencyCommand("eur", DisplaySymbol: "€", DisplayOrder: 3), CancellationToken.None);
        await handler.Handle(new EnableCurrencyCommand("EUR"), CancellationToken.None);

        db.Set<CurrencyConfiguration>().Should().HaveCount(1);
        var row = db.Set<CurrencyConfiguration>().Single();
        row.CurrencyCode.Should().Be("EUR");
        row.IsEnabled.Should().BeTrue();
        row.DisplaySymbol.Should().Be("€");
        row.DisplayOrder.Should().Be(3);

        // Two enables, two audit entries (one per issued command, idempotent on state).
        db.Set<AuditTrail>()
            .Count(audit => audit.EntityType == nameof(CurrencyConfiguration))
            .Should().Be(2);
    }

    [Fact]
    public async Task EnableCurrency_UnknownCode_FailsClosed()
    {
        await using var db = NewDatabase();
        var handler = new EnableCurrencyCommandHandler(db, _actorAccessor.Object);

        var act = () => handler.Handle(new EnableCurrencyCommand("XXY"), CancellationToken.None);

        (await act.Should().ThrowAsync<BusinessRuleViolationException>())
            .Which.Rule.Should().Be(UnknownCurrency);
        db.Set<CurrencyConfiguration>().Should().BeEmpty();
    }

    [Fact]
    public async Task DisableCurrency_BaseCurrency_IsAlwaysRejected()
    {
        await using var db = NewDatabase();
        await EnableAsync(db, "USD", isBase: true);
        await EnableAsync(db, "EUR");

        var handler = new DisableCurrencyCommandHandler(db, _actorAccessor.Object, _usageProbe.Object);

        var act = () => handler.Handle(
            new DisableCurrencyCommand("USD", ConfirmActiveSubscriptionImpact: true), CancellationToken.None);

        (await act.Should().ThrowAsync<BusinessRuleViolationException>())
            .Which.Rule.Should().Be(BaseCurrencyCannotBeDisabled);
        db.Set<CurrencyConfiguration>().Single(row => row.CurrencyCode == "USD").IsEnabled.Should().BeTrue();
    }

    [Theory]
    [InlineData(false)] // known usage: count > 0 requires confirmation
    [InlineData(true)] // unknown usage (probe null): fail closed, requires confirmation
    public async Task DisableCurrency_InUseOrUnknownUsage_RequiresConfirmation(bool usageUnknown)
    {
        await using var db = NewDatabase();
        await EnableAsync(db, "EUR");
        _usageProbe
            .Setup(probe => probe.CountActiveSubscriptionsUsingCurrencyAsync(
                It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(usageUnknown ? null : 7);

        var handler = new DisableCurrencyCommandHandler(db, _actorAccessor.Object, _usageProbe.Object);

        var act = () => handler.Handle(new DisableCurrencyCommand("EUR"), CancellationToken.None);

        (await act.Should().ThrowAsync<BusinessRuleViolationException>())
            .Which.Rule.Should().Be(CurrencyInUseByActiveSubscriptions);
        db.Set<CurrencyConfiguration>().Single().IsEnabled.Should().BeTrue();
    }

    [Fact]
    public async Task DisableCurrency_WithConfirmation_DisablesAndAudits()
    {
        await using var db = NewDatabase();
        await EnableAsync(db, "EUR");
        _usageProbe
            .Setup(probe => probe.CountActiveSubscriptionsUsingCurrencyAsync(
                It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(7);

        var handler = new DisableCurrencyCommandHandler(db, _actorAccessor.Object, _usageProbe.Object);
        await handler.Handle(new DisableCurrencyCommand("EUR", ConfirmActiveSubscriptionImpact: true), CancellationToken.None);

        db.Set<CurrencyConfiguration>().Single().IsEnabled.Should().BeFalse();
        var audit = db.Set<AuditTrail>().Single(entry => entry.EntityType == nameof(CurrencyConfiguration));
        audit.Metadata.Should().Contain("confirmedSubscriptionImpact\":true").And.Contain("activeSubscriptions\":7");
    }

    [Fact]
    public async Task DisableCurrency_AbsentRow_IsAFailClosedNoOp()
    {
        await using var db = NewDatabase();
        var handler = new DisableCurrencyCommandHandler(db, _actorAccessor.Object, _usageProbe.Object);

        await handler.Handle(new DisableCurrencyCommand("JPY"), CancellationToken.None);

        db.Set<CurrencyConfiguration>().Should().BeEmpty();
        db.Set<AuditTrail>().Should().BeEmpty();
    }

    [Fact]
    public async Task SetBaseCurrency_MovesTheMarkerAndEnablesTheTarget()
    {
        await using var db = NewDatabase();
        await EnableAsync(db, "USD", isBase: true);
        await EnableAsync(db, "BRL");

        var handler = new SetBaseCurrencyCommandHandler(db, _actorAccessor.Object);
        await handler.Handle(new SetBaseCurrencyCommand("brl"), CancellationToken.None);

        var usd = db.Set<CurrencyConfiguration>().Single(row => row.CurrencyCode == "USD");
        var brl = db.Set<CurrencyConfiguration>().Single(row => row.CurrencyCode == "BRL");
        usd.IsBaseCurrency.Should().BeFalse();
        usd.IsEnabled.Should().BeTrue(); // clearing the base does not disable it
        brl.IsBaseCurrency.Should().BeTrue();
        brl.IsEnabled.Should().BeTrue();

        // ClearBaseCurrency + SetBaseCurrency both audited.
        db.Set<AuditTrail>()
            .Count(audit => audit.EntityType == nameof(CurrencyConfiguration))
            .Should().Be(2);
    }

    [Fact]
    public async Task SetBaseCurrency_TargetWithoutRow_IsCreatedEnabledAndBase()
    {
        await using var db = NewDatabase();

        var handler = new SetBaseCurrencyCommandHandler(db, _actorAccessor.Object);
        await handler.Handle(new SetBaseCurrencyCommand("EUR"), CancellationToken.None);

        var row = db.Set<CurrencyConfiguration>().Single();
        row.CurrencyCode.Should().Be("EUR");
        row.IsEnabled.Should().BeTrue();
        row.IsBaseCurrency.Should().BeTrue();
    }

    [Fact]
    public async Task SetBaseCurrency_UnknownCode_FailsClosed()
    {
        await using var db = NewDatabase();
        var handler = new SetBaseCurrencyCommandHandler(db, _actorAccessor.Object);

        var act = () => handler.Handle(new SetBaseCurrencyCommand("XYZ"), CancellationToken.None);

        (await act.Should().ThrowAsync<BusinessRuleViolationException>())
            .Which.Rule.Should().Be(UnknownCurrency);
    }

    [Fact]
    public async Task GetTenantCurrencies_AnnotatesTheCatalogWithFailClosedDefaults()
    {
        await using var db = NewDatabase();
        await EnableAsync(db, "USD", isBase: true, displaySymbol: "$US", displayOrder: -5);
        await EnableAsync(db, "EUR");

        var handler = new GetTenantCurrenciesQueryHandler(db, _actorAccessor.Object);
        var result = await handler.Handle(new GetTenantCurrenciesQuery(), CancellationToken.None);

        result.Should().NotBeEmpty();
        result.Count.Should().BeGreaterThanOrEqualTo(150); // full active ISO 4217 list one

        // Fail-closed defaults: unconfigured catalog currencies are disabled with catalog metadata.
        var jpy = result.Single(dto => dto.CurrencyCode == "JPY");
        jpy.IsEnabled.Should().BeFalse();
        jpy.IsBaseCurrency.Should().BeFalse();
        jpy.MinorUnitExponent.Should().Be(0);
        jpy.EnglishName.Should().NotBeNullOrEmpty();

        // Enabled currencies first, base first among them, display preference applied.
        result[0].CurrencyCode.Should().Be("USD");
        result[0].IsBaseCurrency.Should().BeTrue();
        result[0].DisplaySymbol.Should().Be("$US");
        result[1].CurrencyCode.Should().Be("EUR");
        result[1].IsEnabled.Should().BeTrue();

        // Three-decimal catalog entries exposed for selector formatting.
        var kwd = result.Single(dto => dto.CurrencyCode == "KWD");
        kwd.MinorUnitExponent.Should().Be(3);
    }

    [Fact]
    public async Task CurrencyCommands_WithoutTenantContext_FailClosed()
    {
        var anonymous = new ActorContext
        {
            ActorKind = ActorKind.User,
            SubjectId = Guid.NewGuid().ToString(),
            TenantId = null,
            Roles = new HashSet<string>(),
            Permissions = new HashSet<string>(),
            TypedAttributes = ActorAttributes.Empty,
            AuthScheme = "Test",
            IsAuthenticated = true
        };
        _actorAccessor.SetupGet(accessor => accessor.ActorContext).Returns(anonymous);
        await using var db = NewDatabase();
        var handler = new EnableCurrencyCommandHandler(db, _actorAccessor.Object);

        var act = () => handler.Handle(new EnableCurrencyCommand("USD"), CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public void UnknownCurrencySubscriptionUsageProbe_ReturnsNull_FailClosed()
    {
        var probe = new UnknownCurrencySubscriptionUsageProbe();

        var result = probe.CountActiveSubscriptionsUsingCurrencyAsync(Guid.NewGuid(), "USD").Result;

        result.Should().BeNull();
    }

    private async Task EnableAsync(
        CurrencyFxTestDbContext db,
        string code,
        bool isBase = false,
        string? displaySymbol = null,
        int displayOrder = 0)
    {
        var handler = new EnableCurrencyCommandHandler(db, _actorAccessor.Object);
        await handler.Handle(new EnableCurrencyCommand(code, displaySymbol, displayOrder), CancellationToken.None);
        if (isBase)
        {
            await new SetBaseCurrencyCommandHandler(db, _actorAccessor.Object)
                .Handle(new SetBaseCurrencyCommand(code), CancellationToken.None);
        }
    }

    private CurrencyFxTestDbContext NewDatabase()
    {
        var options = new DbContextOptionsBuilder<CurrencyFxTestDbContext>()
            .UseInMemoryDatabase($"currency-config-{Guid.NewGuid():N}")
            .Options;
        return new CurrencyFxTestDbContext(options);
    }

    private static ActorContext AuthenticatedActor() => new()
    {
        ActorKind = ActorKind.User,
        SubjectId = Guid.NewGuid().ToString(),
        TenantId = Guid.NewGuid(),
        Roles = new HashSet<string>(),
        Permissions = new HashSet<string>(),
        TypedAttributes = ActorAttributes.Empty,
        AuthScheme = "Test",
        IsAuthenticated = true
    };
}

/// <summary>
///     In-memory context for the currency configuration tests: maps exactly the entities the
///     commands touch, with a no-op transaction so the two-phase base-currency move (an
///     PostgreSQL partial-unique-index guard in production) stays testable on InMemory.
/// </summary>
internal sealed class CurrencyFxTestDbContext(DbContextOptions<CurrencyFxTestDbContext> options)
    : DbContext(options), IApplicationDbContext
{
    public Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(NoOpTransaction.Instance);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new CurrencyConfigurationConfiguration());
        modelBuilder.ApplyConfiguration(new FxRateSnapshotConfiguration());
        modelBuilder.ApplyConfiguration(new FxRateSyncRunConfiguration());
        modelBuilder.ApplyConfiguration(new AuditTrailConfiguration());
        base.OnModelCreating(modelBuilder);
    }

    private sealed class NoOpTransaction : IDbContextTransaction
    {
        public static readonly NoOpTransaction Instance = new();

        public Guid TransactionId { get => Guid.Empty; }

        public void Dispose() { }

        public ValueTask DisposeAsync() { return ValueTask.CompletedTask; }

        public Task CommitAsync(CancellationToken cancellationToken = default) { return Task.CompletedTask; }

        public Task RollbackAsync(CancellationToken cancellationToken = default) { return Task.CompletedTask; }
    }
}
