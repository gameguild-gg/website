using FluentAssertions;
using GameGuild.CQRS;
using GameGuild.Identity.Context.Actors;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;

namespace GameGuild.Social.Reactions.UnitTests;

public sealed class ReactionRepositoryTests
{
    [Fact]
    public async Task Repository_AddsUpdatesDeletesAndQueriesReactions()
    {
        await using var context = CreateContext();
        var repository = new ReactionRepository(context);
        var userId = Guid.NewGuid();
        var targetId = Guid.NewGuid();
        var matching = Reaction.Create(userId, targetId, ReactionTargetType.Post, ReactionType.Like);
        var second = Reaction.Create(Guid.NewGuid(), targetId, ReactionTargetType.Post, ReactionType.Love);
        var otherTarget = Reaction.Create(Guid.NewGuid(), Guid.NewGuid(), ReactionTargetType.Post, ReactionType.Curious);
        var otherType = Reaction.Create(Guid.NewGuid(), targetId, ReactionTargetType.BlogPost, ReactionType.Support);

        await repository.AddAsync(matching);
        await repository.AddAsync(second);
        await repository.AddAsync(otherTarget);
        await repository.AddAsync(otherType);
        matching.ChangeType(ReactionType.Insightful);
        await repository.UpdateAsync(matching);

        var byUserTarget = await repository.GetByUserTargetAsync(userId, targetId, ReactionTargetType.Post);
        var missing = await repository.GetByUserTargetAsync(Guid.NewGuid(), targetId, ReactionTargetType.Post);
        var byTarget = await repository.GetByTargetAsync(targetId, ReactionTargetType.Post);
        await repository.DeleteAsync(second);
        var afterDelete = await repository.GetByTargetAsync(targetId, ReactionTargetType.Post);

        byUserTarget.Should().NotBeNull();
        byUserTarget!.Type.Should().Be(ReactionType.Insightful);
        missing.Should().BeNull();
        byTarget.Select(reaction => reaction.Id).Should().Equal(matching.Id, second.Id);
        afterDelete.Should().ContainSingle().Which.Id.Should().Be(matching.Id);
    }

    internal static ReactionTestDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ReactionTestDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new ReactionTestDbContext(options);
    }
}

public sealed class ReactionServiceTests
{
    [Fact]
    public async Task SetAsync_AddsNewReactionWhenMissing()
    {
        Reaction? captured = null;
        var repository = new Mock<IReactionRepository>();
        repository.Setup(repo => repo.GetByUserTargetAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<ReactionTargetType>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Reaction?)null);
        repository.Setup(repo => repo.AddAsync(It.IsAny<Reaction>(), It.IsAny<CancellationToken>()))
            .Callback<Reaction, CancellationToken>((reaction, _) => captured = reaction)
            .Returns(Task.CompletedTask);
        var service = new ReactionService(repository.Object);
        var command = new SetReactionCommand(Guid.NewGuid(), Guid.NewGuid(), ReactionTargetType.Post, ReactionType.Love);

        var dto = await service.SetAsync(command);

        captured.Should().NotBeNull();
        dto.Id.Should().Be(captured!.Id);
        dto.UserId.Should().Be(command.UserId);
        dto.TargetId.Should().Be(command.TargetId);
        dto.TargetType.Should().Be(command.TargetType);
        dto.Type.Should().Be(command.Type);
    }

    [Fact]
    public async Task SetAsync_UpdatesExistingReaction()
    {
        var reaction = Reaction.Create(Guid.NewGuid(), Guid.NewGuid(), ReactionTargetType.Post, ReactionType.Like);
        var repository = MockRepositoryWithReaction(reaction);
        var service = new ReactionService(repository.Object);

        var dto = await service.SetAsync(new SetReactionCommand(reaction.UserId, reaction.TargetId, reaction.TargetType, ReactionType.Celebrate));

        dto.Type.Should().Be(ReactionType.Celebrate);
        repository.Verify(repo => repo.UpdateAsync(reaction, It.IsAny<CancellationToken>()), Times.Once);
        repository.Verify(repo => repo.AddAsync(It.IsAny<Reaction>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RemoveAsync_ReturnsFalseWhenMissingAndDeletesWhenFound()
    {
        var reaction = Reaction.Create(Guid.NewGuid(), Guid.NewGuid(), ReactionTargetType.Post, ReactionType.Like);
        var repository = MockRepositoryWithReaction(reaction);
        var service = new ReactionService(repository.Object);

        var missing = await service.RemoveAsync(new RemoveReactionCommand(Guid.NewGuid(), reaction.TargetId, reaction.TargetType));
        var removed = await service.RemoveAsync(new RemoveReactionCommand(reaction.UserId, reaction.TargetId, reaction.TargetType));

        missing.Should().BeFalse();
        removed.Should().BeTrue();
        repository.Verify(repo => repo.DeleteAsync(reaction, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task QueryMethods_ReturnSummaryAndUserReaction()
    {
        var targetId = Guid.NewGuid();
        var like = Reaction.Create(Guid.NewGuid(), targetId, ReactionTargetType.BlogPost, ReactionType.Like);
        var love = Reaction.Create(Guid.NewGuid(), targetId, ReactionTargetType.BlogPost, ReactionType.Love);
        var secondLove = Reaction.Create(Guid.NewGuid(), targetId, ReactionTargetType.BlogPost, ReactionType.Love);
        var repository = new Mock<IReactionRepository>();
        repository.Setup(repo => repo.GetByTargetAsync(targetId, ReactionTargetType.BlogPost, It.IsAny<CancellationToken>()))
            .ReturnsAsync([like, love, secondLove]);
        repository.Setup(repo => repo.GetByUserTargetAsync(like.UserId, targetId, ReactionTargetType.BlogPost, It.IsAny<CancellationToken>()))
            .ReturnsAsync(like);
        repository.Setup(repo => repo.GetByUserTargetAsync(It.Is<Guid>(id => id != like.UserId), targetId, ReactionTargetType.BlogPost, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Reaction?)null);
        var service = new ReactionService(repository.Object);

        var summary = await service.GetTargetSummaryAsync(new GetTargetReactionsQuery(targetId, ReactionTargetType.BlogPost));
        var userReaction = await service.GetUserReactionAsync(new GetUserReactionQuery(like.UserId, targetId, ReactionTargetType.BlogPost));
        var missing = await service.GetUserReactionAsync(new GetUserReactionQuery(Guid.NewGuid(), targetId, ReactionTargetType.BlogPost));

        summary.TargetId.Should().Be(targetId);
        summary.TargetType.Should().Be(ReactionTargetType.BlogPost);
        summary.Total.Should().Be(3);
        summary.Counts[ReactionType.Like].Should().Be(1);
        summary.Counts[ReactionType.Love].Should().Be(2);
        userReaction.Should().NotBeNull();
        userReaction!.Id.Should().Be(like.Id);
        missing.Should().BeNull();
    }

    private static Mock<IReactionRepository> MockRepositoryWithReaction(Reaction reaction)
    {
        var repository = new Mock<IReactionRepository>();
        repository.Setup(repo => repo.GetByUserTargetAsync(reaction.UserId, reaction.TargetId, reaction.TargetType, It.IsAny<CancellationToken>()))
            .ReturnsAsync(reaction);
        repository.Setup(repo => repo.GetByUserTargetAsync(It.Is<Guid>(id => id != reaction.UserId), reaction.TargetId, reaction.TargetType, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Reaction?)null);
        return repository;
    }
}

public sealed class ReactionHandlerTests
{
    [Fact]
    public async Task Handlers_DelegateToReactionService()
    {
        var dto = CreateDto();
        var summary = new TargetReactionSummaryDto(dto.TargetId, dto.TargetType, new Dictionary<ReactionType, int> { [dto.Type] = 1 }, 1);
        var service = new Mock<IReactionService>();
        service.Setup(s => s.SetAsync(It.IsAny<SetReactionCommand>(), It.IsAny<CancellationToken>())).ReturnsAsync(dto);
        service.Setup(s => s.RemoveAsync(It.IsAny<RemoveReactionCommand>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
        service.Setup(s => s.GetTargetSummaryAsync(It.IsAny<GetTargetReactionsQuery>(), It.IsAny<CancellationToken>())).ReturnsAsync(summary);
        service.Setup(s => s.GetUserReactionAsync(It.IsAny<GetUserReactionQuery>(), It.IsAny<CancellationToken>())).ReturnsAsync(dto);

        var set = await new SetReactionCommandHandler(service.Object)
            .Handle(new SetReactionCommand(dto.UserId, dto.TargetId, dto.TargetType, dto.Type), CancellationToken.None);
        var removed = await new RemoveReactionCommandHandler(service.Object)
            .Handle(new RemoveReactionCommand(dto.UserId, dto.TargetId, dto.TargetType), CancellationToken.None);
        var target = await new GetTargetReactionsQueryHandler(service.Object)
            .Handle(new GetTargetReactionsQuery(dto.TargetId, dto.TargetType), CancellationToken.None);
        var user = await new GetUserReactionQueryHandler(service.Object)
            .Handle(new GetUserReactionQuery(dto.UserId, dto.TargetId, dto.TargetType), CancellationToken.None);

        set.Should().Be(dto);
        removed.Should().BeTrue();
        target.Should().Be(summary);
        user.Should().Be(dto);
    }

    internal static ReactionDto CreateDto()
        => new(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            ReactionTargetType.Post,
            ReactionType.Like,
            DateTime.UtcNow.AddDays(-1),
            DateTime.UtcNow);
}

public sealed class ReactionsControllerTests
{
    [Fact]
    public async Task QueryAndSetEndpoints_SendMatchingRequests()
    {
        var dto = ReactionHandlerTests.CreateDto();
        var summary = new TargetReactionSummaryDto(dto.TargetId, dto.TargetType, new Dictionary<ReactionType, int> { [dto.Type] = 1 }, 1);
        var sender = new Mock<ISender>();
        sender.Setup(s => s.Send(It.Is<GetTargetReactionsQuery>(query => query.TargetId == dto.TargetId && query.TargetType == dto.TargetType), It.IsAny<CancellationToken>()))
            .ReturnsAsync(summary);
        sender.Setup(s => s.Send(It.Is<GetUserReactionQuery>(query => query.UserId == dto.UserId && query.TargetId == dto.TargetId && query.TargetType == dto.TargetType), It.IsAny<CancellationToken>()))
            .ReturnsAsync(dto);
        sender.Setup(s => s.Send(It.Is<SetReactionCommand>(command => command.UserId == dto.UserId && command.TargetId == dto.TargetId && command.TargetType == dto.TargetType && command.Type == dto.Type), It.IsAny<CancellationToken>()))
            .ReturnsAsync(dto);
        var controller = new ReactionsController(sender.Object, Actor(dto.UserId));

        var target = await controller.GetTargetSummary(dto.TargetType, dto.TargetId, CancellationToken.None);
        var user = await controller.GetUserReaction(dto.TargetType, dto.TargetId, CancellationToken.None);
        var set = await controller.Set(new SetReactionRequest(dto.TargetId, dto.TargetType, dto.Type), CancellationToken.None);

        target.Should().Be(summary);
        user.Value.Should().Be(dto);
        set.Value.Should().Be(dto);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Remove_ReturnsNoContentOrNotFound(bool removed)
    {
        var userId = Guid.NewGuid();
        var request = new RemoveReactionRequest(Guid.NewGuid(), ReactionTargetType.Post);
        var sender = new Mock<ISender>();
        sender.Setup(s => s.Send(It.Is<RemoveReactionCommand>(command => command.UserId == userId && command.TargetId == request.TargetId && command.TargetType == request.TargetType), It.IsAny<CancellationToken>()))
            .ReturnsAsync(removed);
        var controller = new ReactionsController(sender.Object, Actor(userId));

        var result = await controller.Remove(request, CancellationToken.None);

        result.Should().BeOfType(removed ? typeof(NoContentResult) : typeof(NotFoundResult));
    }

    [Fact]
    public async Task Set_UsesAuthenticatedActor()
    {
        var userId = Guid.NewGuid();
        var targetId = Guid.NewGuid();
        var dto = new ReactionDto(Guid.NewGuid(), userId, targetId, ReactionTargetType.Post, ReactionType.Like, DateTime.UtcNow, DateTime.UtcNow);
        var sender = new Mock<ISender>();
        sender.Setup(s => s.Send(
                It.Is<SetReactionCommand>(command => command.UserId == userId && command.TargetId == targetId),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(dto);
        var controller = new ReactionsController(sender.Object, Actor(userId));

        var result = await controller.Set(new SetReactionRequest(targetId, ReactionTargetType.Post, ReactionType.Like), CancellationToken.None);

        result.Value.Should().Be(dto);
        sender.VerifyAll();
    }

    [Fact]
    public async Task Set_WithoutActor_ReturnsUnauthorized()
    {
        var sender = new Mock<ISender>();
        var controller = new ReactionsController(sender.Object, Actor(Guid.Empty));

        var result = await controller.Set(new SetReactionRequest(Guid.NewGuid(), ReactionTargetType.Post, ReactionType.Like), CancellationToken.None);

        result.Result.Should().BeOfType<UnauthorizedResult>();
        sender.Verify(s => s.Send(It.IsAny<SetReactionCommand>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private static IActorContextAccessor Actor(Guid userId)
    {
        var accessor = new Mock<IActorContextAccessor>();
        accessor.SetupGet(value => value.ActorContext).Returns(userId == Guid.Empty
            ? ActorContext.Anonymous
            : new ActorContext
            {
                ActorKind = ActorKind.User,
                SubjectId = userId.ToString(),
                Roles = new HashSet<string>(),
                Permissions = new HashSet<string>(),
                IsAuthenticated = true
            });
        return accessor.Object;
    }
}

public sealed class ReactionsInfrastructureTests
{
    [Fact]
    public void ReactionsModelConfiguration_AppliesReactionMapping()
    {
        using var context = ReactionRepositoryTests.CreateContext();
        var entity = context.Model.FindEntityType(typeof(Reaction));

        entity.Should().NotBeNull();
        var reaction = entity!;
        reaction.GetTableName().Should().Be("social_reactions");
        reaction.FindPrimaryKey()!.Properties.Single().Name.Should().Be(nameof(Reaction.Id));
        reaction.FindProperty(nameof(Reaction.TargetType))!.GetMaxLength().Should().Be(40);
        reaction.FindProperty(nameof(Reaction.Type))!.GetMaxLength().Should().Be(40);
        var userTargetTypeIndex = new[] { nameof(Reaction.UserId), nameof(Reaction.TargetId), nameof(Reaction.TargetType) };
        var targetTypeIndex = new[] { nameof(Reaction.TargetId), nameof(Reaction.TargetType) };
        reaction.GetIndexes().Should().Contain(index => index.IsUnique && index.Properties.Select(property => property.Name).SequenceEqual(userTargetTypeIndex));
        reaction.GetIndexes().Should().Contain(index => index.Properties.Select(property => property.Name).SequenceEqual(targetTypeIndex));
    }

    [Fact]
    public void AddSocialReactionsModule_RegistersRepositoryServiceHandlersAndModule()
    {
        var services = new ServiceCollection();
        services.AddDbContext<ReactionTestDbContext>(options => options.UseInMemoryDatabase(Guid.NewGuid().ToString()));
        services.AddScoped<IApplicationDbContext>(provider => provider.GetRequiredService<ReactionTestDbContext>());

        var configured = services.AddSocialReactionsModule();

        configured.Should().BeSameAs(services);
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var scoped = scope.ServiceProvider;
        scoped.GetRequiredService<IReactionRepository>().Should().BeOfType<ReactionRepository>();
        scoped.GetRequiredService<IReactionService>().Should().BeOfType<ReactionService>();
        scoped.GetRequiredService<ICommandHandler<SetReactionCommand, ReactionDto>>().Should().BeOfType<SetReactionCommandHandler>();
        scoped.GetRequiredService<IRequestHandler<SetReactionCommand, ReactionDto>>().Should().BeSameAs(scoped.GetRequiredService<ICommandHandler<SetReactionCommand, ReactionDto>>());
        scoped.GetRequiredService<ICommandHandler<RemoveReactionCommand, bool>>().Should().BeOfType<RemoveReactionCommandHandler>();
        scoped.GetRequiredService<IRequestHandler<RemoveReactionCommand, bool>>().Should().BeSameAs(scoped.GetRequiredService<ICommandHandler<RemoveReactionCommand, bool>>());
        scoped.GetRequiredService<IQueryHandler<GetTargetReactionsQuery, TargetReactionSummaryDto>>().Should().BeOfType<GetTargetReactionsQueryHandler>();
        scoped.GetRequiredService<IRequestHandler<GetTargetReactionsQuery, TargetReactionSummaryDto>>().Should().BeSameAs(scoped.GetRequiredService<IQueryHandler<GetTargetReactionsQuery, TargetReactionSummaryDto>>());
        scoped.GetRequiredService<IQueryHandler<GetUserReactionQuery, ReactionDto?>>().Should().BeOfType<GetUserReactionQueryHandler>();
        scoped.GetRequiredService<IRequestHandler<GetUserReactionQuery, ReactionDto?>>().Should().BeSameAs(scoped.GetRequiredService<IQueryHandler<GetUserReactionQuery, ReactionDto?>>());
    }

    [Fact]
    public void SocialReactionsModule_ExposesNameOrderServicesAndEndpointMapping()
    {
        var module = new SocialReactionsModule();
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder().Build();
        var endpoints = new Mock<IEndpointRouteBuilder>().Object;

        var configuredServices = module.ConfigureServices(services, configuration);
        var mappedEndpoints = module.MapEndpoints(endpoints);

        module.Name.Should().Be("Social.Reactions");
        module.Order.Should().Be(161);
        configuredServices.Should().BeSameAs(services);
        mappedEndpoints.Should().BeSameAs(endpoints);
    }
}

internal sealed class ReactionTestDbContext(DbContextOptions<ReactionTestDbContext> options)
    : DbContext(options), IApplicationDbContext
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        new ReactionsModelConfiguration().Configure(modelBuilder);
    }

    public Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException();
    }
}
