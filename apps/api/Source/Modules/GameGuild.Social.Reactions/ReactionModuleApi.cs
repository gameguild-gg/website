using GameGuild.CQRS;
using GameGuild.Identity.Context.Actors;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace GameGuild.Social.Reactions;

public sealed record ReactionDto(
    Guid Id,
    Guid UserId,
    Guid TargetId,
    ReactionTargetType TargetType,
    ReactionType Type,
    DateTime CreatedAt,
    DateTime UpdatedAt);

public sealed record TargetReactionSummaryDto(
    Guid TargetId,
    ReactionTargetType TargetType,
    IReadOnlyDictionary<ReactionType, int> Counts,
    int Total);

public sealed record SetReactionRequest(
    Guid TargetId,
    ReactionTargetType TargetType,
    ReactionType Type);

public sealed record RemoveReactionRequest(
    Guid TargetId,
    ReactionTargetType TargetType);

public sealed record SetReactionCommand(
    Guid UserId,
    Guid TargetId,
    ReactionTargetType TargetType,
    ReactionType Type) : ICommand<ReactionDto>;

public sealed record RemoveReactionCommand(
    Guid UserId,
    Guid TargetId,
    ReactionTargetType TargetType) : ICommand<bool>;

public sealed record GetTargetReactionsQuery(
    Guid TargetId,
    ReactionTargetType TargetType) : IQuery<TargetReactionSummaryDto>;

public sealed record GetUserReactionQuery(
    Guid UserId,
    Guid TargetId,
    ReactionTargetType TargetType) : IQuery<ReactionDto?>;

public interface IReactionRepository
{
    Task<Reaction?> GetByUserTargetAsync(Guid userId, Guid targetId, ReactionTargetType targetType, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Reaction>> GetByTargetAsync(Guid targetId, ReactionTargetType targetType, CancellationToken cancellationToken = default);

    Task AddAsync(Reaction reaction, CancellationToken cancellationToken = default);

    Task UpdateAsync(Reaction reaction, CancellationToken cancellationToken = default);

    Task DeleteAsync(Reaction reaction, CancellationToken cancellationToken = default);
}

public sealed class ReactionRepository(IApplicationDbContext context) : IReactionRepository
{
    public Task<Reaction?> GetByUserTargetAsync(Guid userId, Guid targetId, ReactionTargetType targetType, CancellationToken cancellationToken = default)
        => context.Set<Reaction>()
            .FirstOrDefaultAsync(
                reaction => reaction.UserId == userId && reaction.TargetId == targetId && reaction.TargetType == targetType,
                cancellationToken);

    public async Task<IReadOnlyList<Reaction>> GetByTargetAsync(Guid targetId, ReactionTargetType targetType, CancellationToken cancellationToken = default)
        => await context.Set<Reaction>()
            .Where(reaction => reaction.TargetId == targetId && reaction.TargetType == targetType)
            .OrderBy(reaction => reaction.CreatedAt)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    public async Task AddAsync(Reaction reaction, CancellationToken cancellationToken = default)
    {
        context.Set<Reaction>().Add(reaction);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task UpdateAsync(Reaction reaction, CancellationToken cancellationToken = default)
    {
        context.Set<Reaction>().Update(reaction);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task DeleteAsync(Reaction reaction, CancellationToken cancellationToken = default)
    {
        context.Set<Reaction>().Remove(reaction);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}

public interface IReactionService
{
    Task<ReactionDto> SetAsync(SetReactionCommand command, CancellationToken cancellationToken = default);

    Task<bool> RemoveAsync(RemoveReactionCommand command, CancellationToken cancellationToken = default);

    Task<TargetReactionSummaryDto> GetTargetSummaryAsync(GetTargetReactionsQuery query, CancellationToken cancellationToken = default);

    Task<ReactionDto?> GetUserReactionAsync(GetUserReactionQuery query, CancellationToken cancellationToken = default);
}

public sealed class ReactionService(IReactionRepository repository) : IReactionService
{
    public async Task<ReactionDto> SetAsync(SetReactionCommand command, CancellationToken cancellationToken = default)
    {
        var reaction = await repository
            .GetByUserTargetAsync(command.UserId, command.TargetId, command.TargetType, cancellationToken)
            .ConfigureAwait(false);

        if (reaction is null)
        {
            reaction = Reaction.Create(command.UserId, command.TargetId, command.TargetType, command.Type);
            await repository.AddAsync(reaction, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            reaction.ChangeType(command.Type);
            await repository.UpdateAsync(reaction, cancellationToken).ConfigureAwait(false);
        }

        return ToDto(reaction);
    }

    public async Task<bool> RemoveAsync(RemoveReactionCommand command, CancellationToken cancellationToken = default)
    {
        var reaction = await repository
            .GetByUserTargetAsync(command.UserId, command.TargetId, command.TargetType, cancellationToken)
            .ConfigureAwait(false);

        if (reaction is null)
        {
            return false;
        }

        await repository.DeleteAsync(reaction, cancellationToken).ConfigureAwait(false);
        return true;
    }

    public async Task<TargetReactionSummaryDto> GetTargetSummaryAsync(GetTargetReactionsQuery query, CancellationToken cancellationToken = default)
    {
        var reactions = await repository.GetByTargetAsync(query.TargetId, query.TargetType, cancellationToken)
            .ConfigureAwait(false);
        var counts = reactions
            .GroupBy(reaction => reaction.Type)
            .ToDictionary(group => group.Key, group => group.Count());

        return new TargetReactionSummaryDto(query.TargetId, query.TargetType, counts, reactions.Count);
    }

    public async Task<ReactionDto?> GetUserReactionAsync(GetUserReactionQuery query, CancellationToken cancellationToken = default)
    {
        var reaction = await repository
            .GetByUserTargetAsync(query.UserId, query.TargetId, query.TargetType, cancellationToken)
            .ConfigureAwait(false);

        return reaction is null ? null : ToDto(reaction);
    }

    private static ReactionDto ToDto(Reaction reaction)
        => new(
            reaction.Id,
            reaction.UserId,
            reaction.TargetId,
            reaction.TargetType,
            reaction.Type,
            reaction.CreatedAt,
            reaction.UpdatedAt);
}

public sealed class SetReactionCommandHandler(IReactionService service) : ICommandHandler<SetReactionCommand, ReactionDto>
{
    public Task<ReactionDto> Handle(SetReactionCommand request, CancellationToken cancellationToken)
        => service.SetAsync(request, cancellationToken);
}

public sealed class RemoveReactionCommandHandler(IReactionService service) : ICommandHandler<RemoveReactionCommand, bool>
{
    public Task<bool> Handle(RemoveReactionCommand request, CancellationToken cancellationToken)
        => service.RemoveAsync(request, cancellationToken);
}

public sealed class GetTargetReactionsQueryHandler(IReactionService service) : IQueryHandler<GetTargetReactionsQuery, TargetReactionSummaryDto>
{
    public Task<TargetReactionSummaryDto> Handle(GetTargetReactionsQuery request, CancellationToken cancellationToken)
        => service.GetTargetSummaryAsync(request, cancellationToken);
}

public sealed class GetUserReactionQueryHandler(IReactionService service) : IQueryHandler<GetUserReactionQuery, ReactionDto?>
{
    public Task<ReactionDto?> Handle(GetUserReactionQuery request, CancellationToken cancellationToken)
        => service.GetUserReactionAsync(request, cancellationToken);
}

[ApiController]
[Route("api/social/reactions")]
[Authorize]
public sealed class ReactionsController(ISender sender, IActorContextAccessor actorContextAccessor) : ControllerBase
{
    [HttpGet("target/{targetType}/{targetId:guid}")]
    public Task<TargetReactionSummaryDto> GetTargetSummary(
        ReactionTargetType targetType,
        Guid targetId,
        CancellationToken cancellationToken)
        => sender.Send(new GetTargetReactionsQuery(targetId, targetType), cancellationToken);

    [HttpGet("me/target/{targetType}/{targetId:guid}")]
    public async Task<ActionResult<ReactionDto?>> GetUserReaction(
        ReactionTargetType targetType,
        Guid targetId,
        CancellationToken cancellationToken)
    {
        if (ActorId is not { } actorId)
        {
            return Unauthorized();
        }

        return await sender.Send(new GetUserReactionQuery(actorId, targetId, targetType), cancellationToken)
            .ConfigureAwait(false);
    }

    [HttpPut]
    public async Task<ActionResult<ReactionDto>> Set(SetReactionRequest request, CancellationToken cancellationToken)
    {
        if (ActorId is not { } actorId)
        {
            return Unauthorized();
        }

        return await sender.Send(
                new SetReactionCommand(actorId, request.TargetId, request.TargetType, request.Type),
                cancellationToken)
            .ConfigureAwait(false);
    }

    [HttpDelete]
    public async Task<IActionResult> Remove(RemoveReactionRequest request, CancellationToken cancellationToken)
    {
        if (ActorId is not { } actorId)
        {
            return Unauthorized();
        }

        var removed = await sender.Send(
            new RemoveReactionCommand(actorId, request.TargetId, request.TargetType),
            cancellationToken).ConfigureAwait(false);

        return removed ? NoContent() : NotFound();
    }

    private Guid? ActorId => actorContextAccessor.ActorContext.SubjectIdAsGuid;
}

public sealed class ReactionsModelConfiguration : IModelConfiguration
{
    public void Configure(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new ReactionConfiguration());
    }
}

public sealed class ReactionConfiguration : IEntityTypeConfiguration<Reaction>
{
    public void Configure(EntityTypeBuilder<Reaction> builder)
    {
        builder.ToTable("social_reactions");
        builder.HasKey(reaction => reaction.Id);
        builder.Property(reaction => reaction.TargetType).HasConversion<string>().HasMaxLength(40);
        builder.Property(reaction => reaction.Type).HasConversion<string>().HasMaxLength(40);
        builder.HasIndex(reaction => new { reaction.UserId, reaction.TargetId, reaction.TargetType }).IsUnique();
        builder.HasIndex(reaction => new { reaction.TargetId, reaction.TargetType });
    }
}

public static class ReactionsDependencyInjection
{
    public static IServiceCollection AddSocialReactionsModule(this IServiceCollection services)
    {
        services.AddScoped<IReactionRepository, ReactionRepository>();
        services.AddScoped<IReactionService, ReactionService>();
        services.AddScoped<ICommandHandler<SetReactionCommand, ReactionDto>, SetReactionCommandHandler>();
        services.AddScoped<IRequestHandler<SetReactionCommand, ReactionDto>>(sp => sp.GetRequiredService<ICommandHandler<SetReactionCommand, ReactionDto>>());
        services.AddScoped<ICommandHandler<RemoveReactionCommand, bool>, RemoveReactionCommandHandler>();
        services.AddScoped<IRequestHandler<RemoveReactionCommand, bool>>(sp => sp.GetRequiredService<ICommandHandler<RemoveReactionCommand, bool>>());
        services.AddScoped<IQueryHandler<GetTargetReactionsQuery, TargetReactionSummaryDto>, GetTargetReactionsQueryHandler>();
        services.AddScoped<IRequestHandler<GetTargetReactionsQuery, TargetReactionSummaryDto>>(sp => sp.GetRequiredService<IQueryHandler<GetTargetReactionsQuery, TargetReactionSummaryDto>>());
        services.AddScoped<IQueryHandler<GetUserReactionQuery, ReactionDto?>, GetUserReactionQueryHandler>();
        services.AddScoped<IRequestHandler<GetUserReactionQuery, ReactionDto?>>(sp => sp.GetRequiredService<IQueryHandler<GetUserReactionQuery, ReactionDto?>>());
        return services;
    }
}

public sealed class SocialReactionsModule : ModuleBase
{
    public override string Name => "Social.Reactions";
    public override int Order => 161;

    public override IServiceCollection ConfigureServices(IServiceCollection services, IConfiguration configuration)
        => services.AddSocialReactionsModule();

    public override IEndpointRouteBuilder MapEndpoints(IEndpointRouteBuilder endpoints) => endpoints;
}
