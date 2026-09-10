using FluentAssertions;
using GameGuild.CQRS;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;

namespace GameGuild.Social.Feed.UnitTests;

public sealed class FeedRepositoryTests
{
    [Fact]
    public async Task Repository_AddsUpdatesFiltersAndOrdersFeedItems()
    {
        await using var context = CreateContext();
        var repository = new FeedRepository(context);
        var userId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        var newestHighScore = FeedItem.Create(userId, Guid.NewGuid(), FeedContentType.Post, Guid.NewGuid(), FeedItemReason.Following, DateTime.UtcNow.AddMinutes(-1), 8);
        var olderHighScore = FeedItem.Create(userId, Guid.NewGuid(), FeedContentType.BlogPost, Guid.NewGuid(), FeedItemReason.Trending, DateTime.UtcNow.AddHours(-1), 8);
        var unreadLowScore = FeedItem.Create(userId, Guid.NewGuid(), FeedContentType.Achievement, Guid.NewGuid(), FeedItemReason.Recommended, DateTime.UtcNow, 1);
        var hidden = FeedItem.Create(userId, Guid.NewGuid(), FeedContentType.CourseReview, Guid.NewGuid(), FeedItemReason.InNetwork, DateTime.UtcNow, 10);
        hidden.Hide();
        var read = FeedItem.Create(userId, Guid.NewGuid(), FeedContentType.ProjectUpdate, Guid.NewGuid(), FeedItemReason.Liked, DateTime.UtcNow, 9);
        read.MarkRead();
        var otherUser = FeedItem.Create(otherUserId, Guid.NewGuid(), FeedContentType.CourseCompletion, Guid.NewGuid(), FeedItemReason.Mentioned, DateTime.UtcNow, 10);

        foreach (var item in new[] { newestHighScore, olderHighScore, unreadLowScore, hidden, read, otherUser })
        {
            await repository.AddAsync(item);
        }

        newestHighScore.MarkRead();
        await repository.UpdateAsync(newestHighScore);

        var byId = await repository.GetByIdAsync(newestHighScore.Id);
        var missing = await repository.GetByIdAsync(Guid.NewGuid());
        var includingRead = await repository.GetUserFeedAsync(userId, -10, 200, includeRead: true);
        var unreadOnly = await repository.GetUserFeedAsync(userId, 0, 1, includeRead: false);

        byId.Should().NotBeNull();
        byId!.IsRead.Should().BeTrue();
        missing.Should().BeNull();
        includingRead.Select(item => item.Id).Should().Equal(read.Id, newestHighScore.Id, olderHighScore.Id, unreadLowScore.Id);
        includingRead.Should().NotContain(item => item.Id == hidden.Id || item.Id == otherUser.Id);
        unreadOnly.Should().ContainSingle().Which.Id.Should().Be(olderHighScore.Id);
    }

    internal static FeedTestDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<FeedTestDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new FeedTestDbContext(options);
    }
}

public sealed class FeedServiceTests
{
    [Fact]
    public async Task AddAsync_CreatesClampedFeedItemDto()
    {
        FeedItem? captured = null;
        var repository = new Mock<IFeedRepository>();
        repository.Setup(repo => repo.AddAsync(It.IsAny<FeedItem>(), It.IsAny<CancellationToken>()))
            .Callback<FeedItem, CancellationToken>((item, _) => captured = item)
            .Returns(Task.CompletedTask);
        var service = new FeedService(repository.Object);
        var command = new AddFeedItemCommand(Guid.NewGuid(), Guid.NewGuid(), FeedContentType.Post, Guid.NewGuid(), FeedItemReason.Following, DateTime.UtcNow.AddDays(-1), 99);

        var dto = await service.AddAsync(command);

        captured.Should().NotBeNull();
        captured!.RelevanceScore.Should().Be(10);
        dto.Id.Should().Be(captured.Id);
        dto.UserId.Should().Be(command.UserId);
        dto.ContentId.Should().Be(command.ContentId);
        dto.ContentType.Should().Be(command.ContentType);
        dto.AuthorId.Should().Be(command.AuthorId);
        dto.Reason.Should().Be(command.Reason);
        dto.RelevanceScore.Should().Be(10);
        dto.IsRead.Should().BeFalse();
        dto.IsHidden.Should().BeFalse();
        dto.ContentCreatedAt.Should().Be(command.ContentCreatedAt);
    }

    [Fact]
    public async Task GetFeedAsync_ReturnsMappedDtos()
    {
        var item = FeedItem.Create(Guid.NewGuid(), Guid.NewGuid(), FeedContentType.BlogPost, Guid.NewGuid(), FeedItemReason.Trending, DateTime.UtcNow, 3);
        var repository = new Mock<IFeedRepository>();
        repository.Setup(repo => repo.GetUserFeedAsync(item.UserId, 2, 5, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync([item]);
        var service = new FeedService(repository.Object);

        var dtos = await service.GetFeedAsync(new GetUserFeedQuery(item.UserId, 2, 5, false));

        dtos.Should().ContainSingle().Which.Id.Should().Be(item.Id);
    }

    [Fact]
    public async Task MarkReadAndHide_ReturnFalseWhenItemIsMissing()
    {
        var repository = new Mock<IFeedRepository>();
        repository.Setup(repo => repo.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((FeedItem?)null);
        var service = new FeedService(repository.Object);

        var read = await service.MarkReadAsync(Guid.NewGuid());
        var hidden = await service.HideAsync(Guid.NewGuid());

        read.Should().BeFalse();
        hidden.Should().BeFalse();
        repository.Verify(repo => repo.UpdateAsync(It.IsAny<FeedItem>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task MarkReadAndHide_UpdateExistingItems()
    {
        var readItem = FeedItem.Create(Guid.NewGuid(), Guid.NewGuid(), FeedContentType.Post, Guid.NewGuid(), FeedItemReason.Following, DateTime.UtcNow);
        var hideItem = FeedItem.Create(Guid.NewGuid(), Guid.NewGuid(), FeedContentType.Post, Guid.NewGuid(), FeedItemReason.Following, DateTime.UtcNow);
        var repository = new Mock<IFeedRepository>();
        repository.Setup(repo => repo.GetByIdAsync(readItem.Id, It.IsAny<CancellationToken>())).ReturnsAsync(readItem);
        repository.Setup(repo => repo.GetByIdAsync(hideItem.Id, It.IsAny<CancellationToken>())).ReturnsAsync(hideItem);
        var service = new FeedService(repository.Object);

        var read = await service.MarkReadAsync(readItem.Id);
        var hidden = await service.HideAsync(hideItem.Id);

        read.Should().BeTrue();
        hidden.Should().BeTrue();
        readItem.IsRead.Should().BeTrue();
        hideItem.IsHidden.Should().BeTrue();
        repository.Verify(repo => repo.UpdateAsync(readItem, It.IsAny<CancellationToken>()), Times.Once);
        repository.Verify(repo => repo.UpdateAsync(hideItem, It.IsAny<CancellationToken>()), Times.Once);
    }
}

public sealed class FeedHandlerTests
{
    [Fact]
    public async Task Handlers_DelegateToFeedService()
    {
        var dto = CreateDto();
        var service = new Mock<IFeedService>();
        service.Setup(s => s.AddAsync(It.IsAny<AddFeedItemCommand>(), It.IsAny<CancellationToken>())).ReturnsAsync(dto);
        service.Setup(s => s.GetFeedAsync(It.IsAny<GetUserFeedQuery>(), It.IsAny<CancellationToken>())).ReturnsAsync([dto]);
        service.Setup(s => s.MarkReadAsync(dto.Id, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        service.Setup(s => s.HideAsync(dto.Id, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var added = await new AddFeedItemCommandHandler(service.Object)
            .Handle(new AddFeedItemCommand(dto.UserId, dto.ContentId, dto.ContentType, dto.AuthorId, dto.Reason, dto.ContentCreatedAt, dto.RelevanceScore), CancellationToken.None);
        var feed = await new GetUserFeedQueryHandler(service.Object)
            .Handle(new GetUserFeedQuery(dto.UserId), CancellationToken.None);
        var read = await new MarkFeedItemReadCommandHandler(service.Object)
            .Handle(new MarkFeedItemReadCommand(dto.Id), CancellationToken.None);
        var hidden = await new HideFeedItemCommandHandler(service.Object)
            .Handle(new HideFeedItemCommand(dto.Id), CancellationToken.None);

        added.Should().Be(dto);
        feed.Should().ContainSingle().Which.Should().Be(dto);
        read.Should().BeTrue();
        hidden.Should().BeTrue();
    }

    internal static FeedItemDto CreateDto()
        => new(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            FeedContentType.Post,
            Guid.NewGuid(),
            4,
            FeedItemReason.Recommended,
            false,
            false,
            DateTime.UtcNow.AddHours(-2),
            DateTime.UtcNow);
}

public sealed class FeedControllerTests
{
    [Fact]
    public async Task GetUserFeed_NormalizesNonPositiveTakeAndSendsQuery()
    {
        var dto = FeedHandlerTests.CreateDto();
        var sender = new Mock<ISender>();
        sender.Setup(s => s.Send(It.Is<GetUserFeedQuery>(query => query.UserId == dto.UserId && query.Skip == 3 && query.Take == 50 && !query.IncludeRead), It.IsAny<CancellationToken>()))
            .ReturnsAsync([dto]);
        var controller = new FeedController(sender.Object);

        var result = await controller.GetUserFeed(dto.UserId, 3, 0, false, CancellationToken.None);

        result.Should().ContainSingle().Which.Should().Be(dto);
    }

    [Fact]
    public async Task GetUserFeed_PreservesPositiveTakeAndSendsQuery()
    {
        var dto = FeedHandlerTests.CreateDto();
        var sender = new Mock<ISender>();
        sender.Setup(s => s.Send(It.Is<GetUserFeedQuery>(query => query.UserId == dto.UserId && query.Skip == 0 && query.Take == 12 && query.IncludeRead), It.IsAny<CancellationToken>()))
            .ReturnsAsync([dto]);
        var controller = new FeedController(sender.Object);

        var result = await controller.GetUserFeed(dto.UserId, 0, 12, true, CancellationToken.None);

        result.Should().ContainSingle().Which.Should().Be(dto);
    }

    [Fact]
    public async Task Add_UsesRequestTimestampOrCurrentTime()
    {
        var explicitCreatedAt = DateTime.UtcNow.AddDays(-3);
        var requestWithTimestamp = new AddFeedItemRequest(Guid.NewGuid(), Guid.NewGuid(), FeedContentType.BlogPost, Guid.NewGuid(), FeedItemReason.Trending, explicitCreatedAt, 2);
        var requestWithoutTimestamp = new AddFeedItemRequest(Guid.NewGuid(), Guid.NewGuid(), FeedContentType.Post, Guid.NewGuid(), FeedItemReason.Following);
        var dto = FeedHandlerTests.CreateDto();
        var sender = new Mock<ISender>();
        sender.Setup(s => s.Send(It.Is<AddFeedItemCommand>(command => command.UserId == requestWithTimestamp.UserId && command.ContentCreatedAt == explicitCreatedAt), It.IsAny<CancellationToken>()))
            .ReturnsAsync(dto);
        sender.Setup(s => s.Send(It.Is<AddFeedItemCommand>(command => command.UserId == requestWithoutTimestamp.UserId && command.ContentCreatedAt > DateTime.UtcNow.AddMinutes(-1)), It.IsAny<CancellationToken>()))
            .ReturnsAsync(dto);
        var controller = new FeedController(sender.Object);

        var explicitResult = await controller.Add(requestWithTimestamp, CancellationToken.None);
        var fallbackResult = await controller.Add(requestWithoutTimestamp, CancellationToken.None);

        explicitResult.Should().Be(dto);
        fallbackResult.Should().Be(dto);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task MarkRead_ReturnsNoContentOrNotFound(bool handled)
    {
        var id = Guid.NewGuid();
        var sender = new Mock<ISender>();
        sender.Setup(s => s.Send(It.Is<MarkFeedItemReadCommand>(command => command.FeedItemId == id), It.IsAny<CancellationToken>()))
            .ReturnsAsync(handled);
        var controller = new FeedController(sender.Object);

        var result = await controller.MarkRead(id, CancellationToken.None);

        result.Should().BeOfType(handled ? typeof(NoContentResult) : typeof(NotFoundResult));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Hide_ReturnsNoContentOrNotFound(bool handled)
    {
        var id = Guid.NewGuid();
        var sender = new Mock<ISender>();
        sender.Setup(s => s.Send(It.Is<HideFeedItemCommand>(command => command.FeedItemId == id), It.IsAny<CancellationToken>()))
            .ReturnsAsync(handled);
        var controller = new FeedController(sender.Object);

        var result = await controller.Hide(id, CancellationToken.None);

        result.Should().BeOfType(handled ? typeof(NoContentResult) : typeof(NotFoundResult));
    }
}

public sealed class FeedInfrastructureTests
{
    [Fact]
    public void FeedModelConfiguration_AppliesFeedItemMapping()
    {
        using var context = FeedRepositoryTests.CreateContext();
        var entity = context.Model.FindEntityType(typeof(FeedItem));

        entity.Should().NotBeNull();
        var feedItem = entity!;
        feedItem.GetTableName().Should().Be("social_feed_items");
        feedItem.FindPrimaryKey()!.Properties.Single().Name.Should().Be(nameof(FeedItem.Id));
        feedItem.FindProperty(nameof(FeedItem.ContentType))!.GetMaxLength().Should().Be(40);
        feedItem.FindProperty(nameof(FeedItem.Reason))!.GetMaxLength().Should().Be(40);
        var userHiddenReadIndex = new[] { nameof(FeedItem.UserId), nameof(FeedItem.IsHidden), nameof(FeedItem.IsRead) };
        feedItem.GetIndexes().Should().Contain(index => index.Properties.Select(property => property.Name).SequenceEqual(userHiddenReadIndex));
        feedItem.GetIndexes().Should().Contain(index => index.Properties.Single().Name == nameof(FeedItem.ContentCreatedAt));
    }

    [Fact]
    public void AddSocialFeedModule_RegistersRepositoryServiceHandlersAndModule()
    {
        var services = new ServiceCollection();
        services.AddDbContext<FeedTestDbContext>(options => options.UseInMemoryDatabase(Guid.NewGuid().ToString()));
        services.AddScoped<IApplicationDbContext>(provider => provider.GetRequiredService<FeedTestDbContext>());

        var configured = services.AddSocialFeedModule();

        configured.Should().BeSameAs(services);
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var scoped = scope.ServiceProvider;
        scoped.GetRequiredService<IFeedRepository>().Should().BeOfType<FeedRepository>();
        scoped.GetRequiredService<IFeedService>().Should().BeOfType<FeedService>();
        scoped.GetRequiredService<ISavedPostService>().Should().BeOfType<SavedPostService>();
        scoped.GetRequiredService<IStoryService>().Should().BeOfType<StoryService>();
        scoped.GetRequiredService<ICommandHandler<CreateStoryCommand, StoryDto>>().Should().BeOfType<CreateStoryCommandHandler>();
        scoped.GetRequiredService<ICommandHandler<MarkStoryViewedCommand, bool>>().Should().BeOfType<MarkStoryViewedCommandHandler>();
        scoped.GetRequiredService<ICommandHandler<DeleteStoryCommand, bool>>().Should().BeOfType<DeleteStoryCommandHandler>();
        scoped.GetRequiredService<IQueryHandler<GetActiveStoriesQuery, IReadOnlyList<StoryDto>>>().Should().BeOfType<GetActiveStoriesQueryHandler>();
        scoped.GetRequiredService<ICommandHandler<SavePostCommand, SavedPostStateDto>>().Should().BeOfType<SavePostCommandHandler>();
        scoped.GetRequiredService<ICommandHandler<UnsavePostCommand, bool>>().Should().BeOfType<UnsavePostCommandHandler>();
        scoped.GetRequiredService<IQueryHandler<GetSavedPostStateQuery, SavedPostStateDto>>().Should().BeOfType<GetSavedPostStateQueryHandler>();
        scoped.GetRequiredService<ICommandHandler<AddFeedItemCommand, FeedItemDto>>().Should().BeOfType<AddFeedItemCommandHandler>();
        scoped.GetRequiredService<IRequestHandler<AddFeedItemCommand, FeedItemDto>>().Should().BeSameAs(scoped.GetRequiredService<ICommandHandler<AddFeedItemCommand, FeedItemDto>>());
        scoped.GetRequiredService<IQueryHandler<GetUserFeedQuery, IReadOnlyList<FeedItemDto>>>().Should().BeOfType<GetUserFeedQueryHandler>();
        scoped.GetRequiredService<IRequestHandler<GetUserFeedQuery, IReadOnlyList<FeedItemDto>>>().Should().BeSameAs(scoped.GetRequiredService<IQueryHandler<GetUserFeedQuery, IReadOnlyList<FeedItemDto>>>());
        scoped.GetRequiredService<ICommandHandler<MarkFeedItemReadCommand, bool>>().Should().BeOfType<MarkFeedItemReadCommandHandler>();
        scoped.GetRequiredService<IRequestHandler<MarkFeedItemReadCommand, bool>>().Should().BeSameAs(scoped.GetRequiredService<ICommandHandler<MarkFeedItemReadCommand, bool>>());
        scoped.GetRequiredService<ICommandHandler<HideFeedItemCommand, bool>>().Should().BeOfType<HideFeedItemCommandHandler>();
        scoped.GetRequiredService<IRequestHandler<HideFeedItemCommand, bool>>().Should().BeSameAs(scoped.GetRequiredService<ICommandHandler<HideFeedItemCommand, bool>>());
    }

    [Fact]
    public void SocialFeedModule_ExposesNameOrderServicesAndEndpointMapping()
    {
        var module = new SocialFeedModule();
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder().Build();
        var endpoints = new Mock<IEndpointRouteBuilder>().Object;

        var configuredServices = module.ConfigureServices(services, configuration);
        var mappedEndpoints = module.MapEndpoints(endpoints);

        module.Name.Should().Be("Social.Feed");
        module.Order.Should().Be(162);
        configuredServices.Should().BeSameAs(services);
        mappedEndpoints.Should().BeSameAs(endpoints);
    }
}

internal sealed class FeedTestDbContext(DbContextOptions<FeedTestDbContext> options)
    : DbContext(options), IApplicationDbContext
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        new FeedModelConfiguration().Configure(modelBuilder);
    }

    public Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException();
    }
}
