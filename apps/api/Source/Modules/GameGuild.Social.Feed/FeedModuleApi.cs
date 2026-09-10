using GameGuild.CQRS;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace GameGuild.Social.Feed;

public sealed record FeedItemDto(
    Guid Id,
    Guid UserId,
    Guid ContentId,
    FeedContentType ContentType,
    Guid AuthorId,
    double RelevanceScore,
    FeedItemReason Reason,
    bool IsRead,
    bool IsHidden,
    DateTime ContentCreatedAt,
    DateTime CreatedAt);

public sealed record AddFeedItemRequest(
    Guid UserId,
    Guid ContentId,
    FeedContentType ContentType,
    Guid AuthorId,
    FeedItemReason Reason,
    DateTime? ContentCreatedAt = null,
    double RelevanceScore = 1.0);

public sealed record AddFeedItemCommand(
    Guid UserId,
    Guid ContentId,
    FeedContentType ContentType,
    Guid AuthorId,
    FeedItemReason Reason,
    DateTime ContentCreatedAt,
    double RelevanceScore) : ICommand<FeedItemDto>;

public sealed record GetUserFeedQuery(Guid UserId, int Skip = 0, int Take = 50, bool IncludeRead = true) : IQuery<IReadOnlyList<FeedItemDto>>;

public sealed record MarkFeedItemReadCommand(Guid FeedItemId) : ICommand<bool>;

public sealed record HideFeedItemCommand(Guid FeedItemId) : ICommand<bool>;

public interface IFeedRepository
{
    Task<FeedItem?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<FeedItem>> GetUserFeedAsync(Guid userId, int skip, int take, bool includeRead, CancellationToken cancellationToken = default);

    Task AddAsync(FeedItem item, CancellationToken cancellationToken = default);

    Task UpdateAsync(FeedItem item, CancellationToken cancellationToken = default);
}

public sealed class FeedRepository(IApplicationDbContext context) : IFeedRepository
{
    public Task<FeedItem?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => context.Set<FeedItem>().FirstOrDefaultAsync(item => item.Id == id, cancellationToken);

    public async Task<IReadOnlyList<FeedItem>> GetUserFeedAsync(Guid userId, int skip, int take, bool includeRead, CancellationToken cancellationToken = default)
    {
        var query = context.Set<FeedItem>()
            .Where(item => item.UserId == userId && !item.IsHidden);

        if (!includeRead)
        {
            query = query.Where(item => !item.IsRead);
        }

        return await query
            .OrderByDescending(item => item.RelevanceScore)
            .ThenByDescending(item => item.ContentCreatedAt)
            .Skip(Math.Max(0, skip))
            .Take(Math.Clamp(take, 1, 100))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task AddAsync(FeedItem item, CancellationToken cancellationToken = default)
    {
        context.Set<FeedItem>().Add(item);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task UpdateAsync(FeedItem item, CancellationToken cancellationToken = default)
    {
        context.Set<FeedItem>().Update(item);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}

public interface IFeedService
{
    Task<FeedItemDto> AddAsync(AddFeedItemCommand command, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<FeedItemDto>> GetFeedAsync(GetUserFeedQuery query, CancellationToken cancellationToken = default);

    Task<bool> MarkReadAsync(Guid id, CancellationToken cancellationToken = default);

    Task<bool> HideAsync(Guid id, CancellationToken cancellationToken = default);
}

public sealed class FeedService(IFeedRepository repository) : IFeedService
{
    public async Task<FeedItemDto> AddAsync(AddFeedItemCommand command, CancellationToken cancellationToken = default)
    {
        var item = FeedItem.Create(
            command.UserId,
            command.ContentId,
            command.ContentType,
            command.AuthorId,
            command.Reason,
            command.ContentCreatedAt,
            Math.Clamp(command.RelevanceScore, 0.0, 10.0));

        await repository.AddAsync(item, cancellationToken).ConfigureAwait(false);
        return ToDto(item);
    }

    public async Task<IReadOnlyList<FeedItemDto>> GetFeedAsync(GetUserFeedQuery query, CancellationToken cancellationToken = default)
        => (await repository.GetUserFeedAsync(query.UserId, query.Skip, query.Take, query.IncludeRead, cancellationToken)
                .ConfigureAwait(false))
            .Select(ToDto)
            .ToList();

    public async Task<bool> MarkReadAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var item = await repository.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
        if (item is null)
        {
            return false;
        }

        item.MarkRead();
        await repository.UpdateAsync(item, cancellationToken).ConfigureAwait(false);
        return true;
    }

    public async Task<bool> HideAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var item = await repository.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
        if (item is null)
        {
            return false;
        }

        item.Hide();
        await repository.UpdateAsync(item, cancellationToken).ConfigureAwait(false);
        return true;
    }

    private static FeedItemDto ToDto(FeedItem item)
        => new(
            item.Id,
            item.UserId,
            item.ContentId,
            item.ContentType,
            item.AuthorId,
            item.RelevanceScore,
            item.Reason,
            item.IsRead,
            item.IsHidden,
            item.ContentCreatedAt,
            item.CreatedAt);
}

public sealed class AddFeedItemCommandHandler(IFeedService service) : ICommandHandler<AddFeedItemCommand, FeedItemDto>
{
    public Task<FeedItemDto> Handle(AddFeedItemCommand request, CancellationToken cancellationToken)
        => service.AddAsync(request, cancellationToken);
}

public sealed class GetUserFeedQueryHandler(IFeedService service) : IQueryHandler<GetUserFeedQuery, IReadOnlyList<FeedItemDto>>
{
    public Task<IReadOnlyList<FeedItemDto>> Handle(GetUserFeedQuery request, CancellationToken cancellationToken)
        => service.GetFeedAsync(request, cancellationToken);
}

public sealed class MarkFeedItemReadCommandHandler(IFeedService service) : ICommandHandler<MarkFeedItemReadCommand, bool>
{
    public Task<bool> Handle(MarkFeedItemReadCommand request, CancellationToken cancellationToken)
        => service.MarkReadAsync(request.FeedItemId, cancellationToken);
}

public sealed class HideFeedItemCommandHandler(IFeedService service) : ICommandHandler<HideFeedItemCommand, bool>
{
    public Task<bool> Handle(HideFeedItemCommand request, CancellationToken cancellationToken)
        => service.HideAsync(request.FeedItemId, cancellationToken);
}

[ApiController]
[Route("api/social/feed")]
public sealed class FeedController(ISender sender) : ControllerBase
{
    [HttpGet("users/{userId:guid}")]
    public Task<IReadOnlyList<FeedItemDto>> GetUserFeed(
        Guid userId,
        [FromQuery] int skip,
        [FromQuery] int take,
        [FromQuery] bool includeRead,
        CancellationToken cancellationToken)
        => sender.Send(new GetUserFeedQuery(userId, skip, take <= 0 ? 50 : take, includeRead), cancellationToken);

    [HttpPost]
    public Task<FeedItemDto> Add(AddFeedItemRequest request, CancellationToken cancellationToken)
        => sender.Send(
            new AddFeedItemCommand(
                request.UserId,
                request.ContentId,
                request.ContentType,
                request.AuthorId,
                request.Reason,
                request.ContentCreatedAt ?? SystemClock.UtcNow,
                request.RelevanceScore),
            cancellationToken);

    [HttpPost("{id:guid}/read")]
    public async Task<IActionResult> MarkRead(Guid id, CancellationToken cancellationToken)
        => await sender.Send(new MarkFeedItemReadCommand(id), cancellationToken).ConfigureAwait(false)
            ? NoContent()
            : NotFound();

    [HttpPost("{id:guid}/hide")]
    public async Task<IActionResult> Hide(Guid id, CancellationToken cancellationToken)
        => await sender.Send(new HideFeedItemCommand(id), cancellationToken).ConfigureAwait(false)
            ? NoContent()
            : NotFound();
}

public sealed class FeedModelConfiguration : IModelConfiguration
{
    public void Configure(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new FeedItemConfiguration());
        modelBuilder.ApplyConfiguration(new SavedPostConfiguration());
    }
}

public sealed class SavedPostConfiguration : IEntityTypeConfiguration<SavedPost>
{
    public void Configure(EntityTypeBuilder<SavedPost> builder)
    {
        builder.ToTable("social_saved_posts");
        builder.HasKey(saved => saved.Id);
        builder.HasIndex(saved => new { saved.UserId, saved.PostId }).IsUnique();
        builder.HasIndex(saved => new { saved.UserId, saved.CreatedAt });
    }
}

public sealed class FeedItemConfiguration : IEntityTypeConfiguration<FeedItem>
{
    public void Configure(EntityTypeBuilder<FeedItem> builder)
    {
        builder.ToTable("social_feed_items");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.ContentType).HasConversion<string>().HasMaxLength(40);
        builder.Property(item => item.Reason).HasConversion<string>().HasMaxLength(40);
        builder.HasIndex(item => new { item.UserId, item.IsHidden, item.IsRead });
        builder.HasIndex(item => item.ContentCreatedAt);
    }
}

public static class FeedDependencyInjection
{
    public static IServiceCollection AddSocialFeedModule(this IServiceCollection services)
    {
        services.AddScoped<IFeedRepository, FeedRepository>();
        services.AddScoped<IFeedService, FeedService>();
        services.AddScoped<ISavedPostService, SavedPostService>();
        services.AddScoped<ICommandHandler<SavePostCommand, SavedPostStateDto>, SavePostCommandHandler>();
        services.AddScoped<IRequestHandler<SavePostCommand, SavedPostStateDto>>(sp => sp.GetRequiredService<ICommandHandler<SavePostCommand, SavedPostStateDto>>());
        services.AddScoped<ICommandHandler<UnsavePostCommand, bool>, UnsavePostCommandHandler>();
        services.AddScoped<IRequestHandler<UnsavePostCommand, bool>>(sp => sp.GetRequiredService<ICommandHandler<UnsavePostCommand, bool>>());
        services.AddScoped<IQueryHandler<GetSavedPostStateQuery, SavedPostStateDto>, GetSavedPostStateQueryHandler>();
        services.AddScoped<IRequestHandler<GetSavedPostStateQuery, SavedPostStateDto>>(sp => sp.GetRequiredService<IQueryHandler<GetSavedPostStateQuery, SavedPostStateDto>>());
        services.AddScoped<ICommandHandler<AddFeedItemCommand, FeedItemDto>, AddFeedItemCommandHandler>();
        services.AddScoped<IRequestHandler<AddFeedItemCommand, FeedItemDto>>(sp => sp.GetRequiredService<ICommandHandler<AddFeedItemCommand, FeedItemDto>>());
        services.AddScoped<IQueryHandler<GetUserFeedQuery, IReadOnlyList<FeedItemDto>>, GetUserFeedQueryHandler>();
        services.AddScoped<IRequestHandler<GetUserFeedQuery, IReadOnlyList<FeedItemDto>>>(sp => sp.GetRequiredService<IQueryHandler<GetUserFeedQuery, IReadOnlyList<FeedItemDto>>>());
        services.AddScoped<ICommandHandler<MarkFeedItemReadCommand, bool>, MarkFeedItemReadCommandHandler>();
        services.AddScoped<IRequestHandler<MarkFeedItemReadCommand, bool>>(sp => sp.GetRequiredService<ICommandHandler<MarkFeedItemReadCommand, bool>>());
        services.AddScoped<ICommandHandler<HideFeedItemCommand, bool>, HideFeedItemCommandHandler>();
        services.AddScoped<IRequestHandler<HideFeedItemCommand, bool>>(sp => sp.GetRequiredService<ICommandHandler<HideFeedItemCommand, bool>>());
        return services;
    }
}

public sealed class SocialFeedModule : ModuleBase
{
    public override string Name => "Social.Feed";
    public override int Order => 162;

    public override IServiceCollection ConfigureServices(IServiceCollection services, IConfiguration configuration)
        => services.AddSocialFeedModule();

    public override IEndpointRouteBuilder MapEndpoints(IEndpointRouteBuilder endpoints) => endpoints;
}
