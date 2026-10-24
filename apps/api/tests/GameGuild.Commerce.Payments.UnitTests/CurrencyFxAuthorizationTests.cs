using FluentAssertions;
using GameGuild.Identity.Context.Actors;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace GameGuild.Commerce.Payments.UnitTests;

public class CurrencyFxAuthorizationTests
{
    public static IEnumerable<object[]> DeniedCurrencyOperations =>
        from operation in new[] { "enable", "disable", "set-base" }
        from actor in new[] { "member", "anonymous-admin", "invalid-subject", "empty-tenant" }
        select new object[] { operation, actor };

    public static IEnumerable<object[]> DeniedFxOperations =>
        from operation in new[] { "override", "sync", "rates", "status" }
        from actor in new[] { "member", "tenant-admin", "anonymous-admin", "invalid-subject" }
        select new object[] { operation, actor };

    [Theory]
    [MemberData(nameof(DeniedCurrencyOperations))]
    public async Task CurrencyMutation_DeniesUnauthorizedActorBeforeReadingOrWriting(string operation, string actor)
    {
        var context = new Mock<IApplicationDbContext>(MockBehavior.Strict);
        var usage = new Mock<ICurrencySubscriptionUsageProbe>(MockBehavior.Strict);
        var accessor = Accessor(actor);

        Func<Task> act = () => operation switch
        {
            "enable" => new EnableCurrencyCommandHandler(context.Object, accessor)
                .Handle(new EnableCurrencyCommand("USD"), CancellationToken.None),
            "disable" => new DisableCurrencyCommandHandler(context.Object, accessor, usage.Object)
                .Handle(new DisableCurrencyCommand("USD", true), CancellationToken.None),
            _ => new SetBaseCurrencyCommandHandler(context.Object, accessor)
                .Handle(new SetBaseCurrencyCommand("USD"), CancellationToken.None),
        };

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
        context.VerifyNoOtherCalls();
        usage.VerifyNoOtherCalls();
    }

    [Theory]
    [MemberData(nameof(DeniedFxOperations))]
    public async Task GlobalFxOperation_DeniesUnauthorizedActorBeforeAnySideEffect(string operation, string actor)
    {
        var context = new Mock<IApplicationDbContext>(MockBehavior.Strict);
        var sync = new Mock<IFxRateSyncService>(MockBehavior.Strict);
        var cache = new Mock<IFxRateCache>(MockBehavior.Strict);
        var scopes = new Mock<IServiceScopeFactory>(MockBehavior.Strict);
        var runtime = new FxRuntimeRateSource(scopes.Object, NullLogger<FxRuntimeRateSource>.Instance);
        var options = Options.Create(new FxSyncOptions { Provider = FxSyncOptions.ProviderEcb });
        var accessor = Accessor(actor);

        Func<Task> act = () => operation switch
        {
            "override" => new SetManualFxRateOverrideCommandHandler(context.Object, accessor, runtime, cache.Object)
                .Handle(new SetManualFxRateOverrideCommand("USD", "EUR", 0.75m, "operator correction"), CancellationToken.None),
            "sync" => new RunFxRateSyncCommandHandler(sync.Object, options, accessor)
                .Handle(new RunFxRateSyncCommand(), CancellationToken.None),
            "rates" => new GetFxRatesQueryHandler(context.Object, accessor)
                .Handle(new GetFxRatesQuery(), CancellationToken.None),
            _ => new GetFxSyncStatusQueryHandler(context.Object, options, accessor)
                .Handle(new GetFxSyncStatusQuery(), CancellationToken.None),
        };

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
        context.VerifyNoOtherCalls();
        sync.VerifyNoOtherCalls();
        cache.VerifyNoOtherCalls();
        scopes.VerifyNoOtherCalls();
        runtime.CurrentOverrides.Should().BeEmpty();
    }

    [Theory]
    [InlineData("tenant-admin")]
    [InlineData("system-admin")]
    public async Task CurrencyAdministration_UsesOnlyAuthenticatedTenantAndActor(string actor)
    {
        await using var context = NewContext();
        var accessor = Accessor(actor);
        var tenantId = accessor.ActorContext.TenantId!.Value;
        var foreign = new CurrencyConfiguration { TenantId = Guid.NewGuid(), CurrencyCode = "EUR", IsEnabled = true, IsBaseCurrency = true };
        context.Add(foreign);
        await context.SaveChangesAsync();

        await new SetBaseCurrencyCommandHandler(context, accessor)
            .Handle(new SetBaseCurrencyCommand("USD"), CancellationToken.None);

        var own = await context.Set<CurrencyConfiguration>().SingleAsync(row => row.TenantId == tenantId);
        own.CurrencyCode.Should().Be("USD");
        own.IsBaseCurrency.Should().BeTrue();
        foreign.IsBaseCurrency.Should().BeTrue();
        foreign.IsEnabled.Should().BeTrue();
        context.Set<AuditTrail>().Should().OnlyContain(row => row.ChangedBy == accessor.ActorContext.SubjectIdAsGuid);
    }

    [Fact]
    public async Task CurrencyList_AllowsMemberButDoesNotExposeAnotherTenantsPreferences()
    {
        await using var context = NewContext();
        var accessor = Accessor("member");
        context.Add(new CurrencyConfiguration { TenantId = Guid.NewGuid(), CurrencyCode = "USD", IsEnabled = true, DisplaySymbol = "foreign-only" });
        context.Add(new CurrencyConfiguration { TenantId = accessor.ActorContext.TenantId, CurrencyCode = "EUR", IsEnabled = true, DisplaySymbol = "own" });
        await context.SaveChangesAsync();

        var rows = await new GetTenantCurrenciesQueryHandler(context, accessor).Handle(new GetTenantCurrenciesQuery(), CancellationToken.None);

        rows.Single(row => row.CurrencyCode == "USD").IsEnabled.Should().BeFalse();
        rows.Should().NotContain(row => row.DisplaySymbol == "foreign-only");
        rows.Single(row => row.CurrencyCode == "EUR").DisplaySymbol.Should().Be("own");
    }

    [Fact]
    public async Task CurrencyList_RejectsUnauthenticatedContextEvenWithTenantAndAdminRole()
    {
        var context = new Mock<IApplicationDbContext>(MockBehavior.Strict);
        var act = () => new GetTenantCurrenciesQueryHandler(context.Object, Accessor("anonymous-admin"))
            .Handle(new GetTenantCurrenciesQuery(), CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
        context.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task GlobalFxQueries_AllowSystemAdministrator()
    {
        await using var context = NewContext();
        var accessor = Accessor("system-admin");
        var rates = await new GetFxRatesQueryHandler(context, accessor).Handle(new GetFxRatesQuery(), CancellationToken.None);
        var status = await new GetFxSyncStatusQueryHandler(context, Options.Create(new FxSyncOptions()), accessor)
            .Handle(new GetFxSyncStatusQuery(), CancellationToken.None);

        rates.Should().BeEmpty();
        status.SnapshotCount.Should().Be(0);
    }

    [Fact]
    public async Task ManualSync_AllowsSystemAdministratorAndInvokesServiceOnce()
    {
        var sync = new Mock<IFxRateSyncService>(MockBehavior.Strict);
        var expected = new FxRateSyncResult(FxRateSyncRunStatus.Succeeded, "ecb", 1, 0, 0, DateTime.UtcNow.Date, DateTime.UtcNow.Date);
        sync.Setup(service => service.SyncAsync(It.IsAny<CancellationToken>())).ReturnsAsync(expected);
        var handler = new RunFxRateSyncCommandHandler(sync.Object,
            Options.Create(new FxSyncOptions { Provider = FxSyncOptions.ProviderEcb }), Accessor("system-admin"));

        (await handler.Handle(new RunFxRateSyncCommand(), CancellationToken.None)).Should().Be(expected);
        sync.Verify(service => service.SyncAsync(It.IsAny<CancellationToken>()), Times.Once);
        sync.VerifyNoOtherCalls();
    }

    private static CurrencyFxTestDbContext NewContext() => new(
        new DbContextOptionsBuilder<CurrencyFxTestDbContext>().UseInMemoryDatabase($"fx-authorization-{Guid.NewGuid():N}").Options);

    private static IActorContextAccessor Accessor(string scenario)
    {
        var actor = new ActorContext
        {
            ActorKind = ActorKind.User,
            SubjectId = scenario == "invalid-subject" ? "invalid" : Guid.NewGuid().ToString(),
            TenantId = scenario == "empty-tenant" ? Guid.Empty : Guid.NewGuid(),
            Roles = scenario switch
            {
                "member" => new HashSet<string>(),
                "tenant-admin" => new HashSet<string> { "TenantAdmin" },
                _ => new HashSet<string> { "SystemAdmin" },
            },
            Permissions = new HashSet<string>(),
            TypedAttributes = ActorAttributes.Empty,
            AuthScheme = "Test",
            IsAuthenticated = scenario != "anonymous-admin",
        };
        var accessor = new Mock<IActorContextAccessor>();
        accessor.SetupGet(value => value.ActorContext).Returns(actor);
        return accessor.Object;
    }
}
