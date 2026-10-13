using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace GameGuild.Commerce.Subscriptions.UnitTests.Commands;

/// <summary>
///     Issue #416: slug auto-generation and cache invalidation on create.
/// </summary>
public class CreateSubscriptionPlanCommandHandlerTests
{
    private readonly Mock<ISubscriptionPlanRepository> _repository = new();
    private readonly Mock<ISubscriptionPlanListCache> _planListCache = new();
    private CreateSubscriptionPlanCommandHandler CreateHandler() => new(
        _repository.Object,
        new SubscriptionPlanSlugGenerator(),
        _planListCache.Object,
        NullLogger<CreateSubscriptionPlanCommandHandler>.Instance);

    [Fact]
    public async Task Handle_UsesExplicitSlug_WhenProvided()
    {
        _repository.Setup(r => r.IsSlugUniqueAsync(It.IsAny<string>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var planId = await CreateHandler().Handle(
            new CreateSubscriptionPlanCommand("Professional", "custom-slug", 4999, "USD"),
            CancellationToken.None);

        planId.Should().NotBeEmpty();
        _repository.Verify(r => r.AddAsync(
            It.Is<SubscriptionPlan>(plan => plan.Slug == "custom-slug" && plan.Name == "Professional"),
            It.IsAny<CancellationToken>()), Times.Once);
        // The generator is not consulted for uniqueness when the slug is explicit.
        _repository.Verify(r => r.IsSlugUniqueAsync(It.IsAny<string>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_GeneratesSlugFromName_WhenSlugOmitted()
    {
        _repository.Setup(r => r.IsSlugUniqueAsync(It.IsAny<string>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        await CreateHandler().Handle(
            new CreateSubscriptionPlanCommand("Professional Plan", Slug: null, 4999, "USD"),
            CancellationToken.None);

        _repository.Verify(r => r.AddAsync(
            It.Is<SubscriptionPlan>(plan => plan.Slug == "professional-plan"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_SuffixesSlug_WhenGeneratedSlugIsTaken()
    {
        _repository
            .SetupSequence(r => r.IsSlugUniqueAsync(It.IsAny<string>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false) // "professional" taken
            .ReturnsAsync(false) // "professional-2" taken
            .ReturnsAsync(true); // "professional-3" free

        await CreateHandler().Handle(
            new CreateSubscriptionPlanCommand("Professional", Slug: null, 4999, "USD"),
            CancellationToken.None);

        _repository.Verify(r => r.AddAsync(
            It.Is<SubscriptionPlan>(plan => plan.Slug == "professional-3"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_Throws_WhenSlugCannotBeGenerated()
    {
        var act = () => CreateHandler().Handle(
            new CreateSubscriptionPlanCommand("---", Slug: null, 4999, "USD"),
            CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*explicit slug*");
        _repository.Verify(r => r.AddAsync(It.IsAny<SubscriptionPlan>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_InvalidatesThePlanListCache()
    {
        _repository.Setup(r => r.IsSlugUniqueAsync(It.IsAny<string>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        await CreateHandler().Handle(
            new CreateSubscriptionPlanCommand("Professional", "professional", 4999, "USD"),
            CancellationToken.None);

        _planListCache.Verify(cache => cache.Invalidate(), Times.Once);
    }
}
