using FluentAssertions;
using GameGuild.Commerce.Subscriptions.UnitTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;
using static GameGuild.Commerce.Subscriptions.UnitTests.Infrastructure.PlanCommandTestDbContext;

namespace GameGuild.Commerce.Subscriptions.UnitTests.Commands;

/// <summary>
///     Issue #416: slug regeneration on full update, trial-period wiring, cache
///     invalidation.
/// </summary>
public class FullUpdateSubscriptionPlanHandlerTests
{
    private readonly Mock<ISubscriptionPlanListCache> _planListCache = new();

    private static async Task<(PlanCommandTestDbContext Context, SubscriptionPlan Plan)> SeedAsync()
    {
        var context = Create();
        var plan = new SubscriptionPlan("Old Name", "old-name", 999);
        context.SubscriptionPlans.Add(plan);
        await context.SaveChangesAsync();
        return (context, plan);
    }

    private static FullUpdateSubscriptionPlanCommand Command(
        Guid planId,
        string name = "New Name",
        string? slug = null,
        int? trialPeriodDays = null) => new(
        PlanId: planId,
        Name: name,
        Slug: slug,
        Description: null,
        MonthlyPriceInCents: 1999,
        AnnualPriceInCents: null,
        MaxUsers: 10,
        MaxStorageMb: null,
        MaxApiCallsPerMonth: null,
        HasPrioritySupport: false,
        HasAdvancedAnalytics: false,
        HasCustomBranding: false,
        Features: null,
        SortOrder: null,
        TrialPeriodDays: trialPeriodDays);

    [Fact]
    public async Task Handle_RegeneratesSlugFromName_WhenSlugOmitted()
    {
        var (context, plan) = await SeedAsync();

        await new FullUpdateSubscriptionPlanHandler(context, new SubscriptionPlanSlugGenerator(), _planListCache.Object)
            .Handle(Command(plan.Id, name: "Enterprise Edition"), CancellationToken.None);

        var updated = await context.SubscriptionPlans.SingleAsync(p => p.Id == plan.Id);
        updated.Slug.Should().Be("enterprise-edition");
        updated.Name.Should().Be("Enterprise Edition");
    }

    [Fact]
    public async Task Handle_RegeneratedSlug_ExcludesThePlanBeingUpdated()
    {
        var (context, plan) = await SeedAsync();
        // Rename the plan to its own current slug source: "old-name" would collide with
        // itself, so the unique check must exclude this plan's own row.
        var command = Command(plan.Id, name: "Old Name");

        await new FullUpdateSubscriptionPlanHandler(context, new SubscriptionPlanSlugGenerator(), _planListCache.Object)
            .Handle(command, CancellationToken.None);

        var updated = await context.SubscriptionPlans.SingleAsync(p => p.Id == plan.Id);
        updated.Slug.Should().Be("old-name");
    }

    [Fact]
    public async Task Handle_SuffixesRegeneratedSlug_WhenAnotherPlanOwnsIt()
    {
        var (context, plan) = await SeedAsync();
        context.SubscriptionPlans.Add(new SubscriptionPlan("Enterprise Edition", "enterprise-edition", 100));
        await context.SaveChangesAsync();

        await new FullUpdateSubscriptionPlanHandler(context, new SubscriptionPlanSlugGenerator(), _planListCache.Object)
            .Handle(Command(plan.Id, name: "Enterprise Edition"), CancellationToken.None);

        var updated = await context.SubscriptionPlans.SingleAsync(p => p.Id == plan.Id);
        updated.Slug.Should().Be("enterprise-edition-2");
    }

    [Fact]
    public async Task Handle_KeepsExplicitSlug()
    {
        var (context, plan) = await SeedAsync();

        await new FullUpdateSubscriptionPlanHandler(context, new SubscriptionPlanSlugGenerator(), _planListCache.Object)
            .Handle(Command(plan.Id, slug: "hand-picked"), CancellationToken.None);

        var updated = await context.SubscriptionPlans.SingleAsync(p => p.Id == plan.Id);
        updated.Slug.Should().Be("hand-picked");
    }

    [Fact]
    public async Task Handle_SetsTrialPeriodDays_WhenProvided_AndKeepsItWhenOmitted()
    {
        var (context, plan) = await SeedAsync();
        var handler = new FullUpdateSubscriptionPlanHandler(context, new SubscriptionPlanSlugGenerator(), _planListCache.Object);

        await handler.Handle(Command(plan.Id, trialPeriodDays: 14), CancellationToken.None);
        (await context.SubscriptionPlans.SingleAsync(p => p.Id == plan.Id)).TrialPeriodDays.Should().Be(14);

        // Omitted on a later full update: the stored value is preserved.
        await handler.Handle(Command(plan.Id), CancellationToken.None);
        (await context.SubscriptionPlans.SingleAsync(p => p.Id == plan.Id)).TrialPeriodDays.Should().Be(14);
    }

    [Fact]
    public async Task Handle_InvalidatesThePlanListCache()
    {
        var (context, plan) = await SeedAsync();

        await new FullUpdateSubscriptionPlanHandler(context, new SubscriptionPlanSlugGenerator(), _planListCache.Object)
            .Handle(Command(plan.Id), CancellationToken.None);

        _planListCache.Verify(cache => cache.Invalidate(), Times.Once);
    }

    [Fact]
    public async Task Handle_Throws_WhenPlanDoesNotExist()
    {
        var context = Create();

        var act = () => new FullUpdateSubscriptionPlanHandler(context, new SubscriptionPlanSlugGenerator(), _planListCache.Object)
            .Handle(Command(Guid.NewGuid()), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not found*");
    }
}
