using GameGuild.CQRS;
using GameGuild.Identity.Context.Actors;

namespace GameGuild.Gamification.Achievements;

public sealed record MarkAchievementNotifiedCommand(Guid UserAchievementId) : ICommand<Result>;

public sealed record CreateAchievementCommand(
    string Name,
    string? Description,
    string? Category,
    string? Type,
    int Points,
    string? IconUrl,
    string? Color,
    bool IsSecret,
    bool IsRepeatable,
    int DisplayOrder) : ICommand<Result<Achievement>>;

public sealed record UpdateAchievementCommand(
    Guid AchievementId,
    string? Name,
    string? Description,
    string? Category,
    int? Points,
    string? IconUrl,
    string? Color,
    bool? IsActive,
    bool? IsSecret,
    bool? IsRepeatable,
    int? DisplayOrder) : ICommand<Result<Achievement>>;

public sealed record DeleteAchievementCommand(Guid AchievementId) : ICommand<Result>;

public sealed record AwardAchievementCommand(
    Guid UserId,
    Guid AchievementId,
    string? Context) : ICommand<Result<UserAchievement>>;

public sealed class MarkAchievementNotifiedCommandHandler(IAchievementService achievementService)
    : ICommandHandler<MarkAchievementNotifiedCommand, Result>
{
    public Task<Result> Handle(MarkAchievementNotifiedCommand request, CancellationToken cancellationToken) =>
        achievementService.MarkNotifiedAsync(request.UserAchievementId);
}

public sealed class CreateAchievementCommandHandler(
    IAchievementService achievementService,
    IActorContextAccessor actorContextAccessor)
    : ICommandHandler<CreateAchievementCommand, Result<Achievement>>
{
    public Task<Result<Achievement>> Handle(CreateAchievementCommand request, CancellationToken cancellationToken)
    {
        var achievement = Achievement.Create(
            request.Name,
            request.Category ?? "general",
            request.Type ?? "badge",
            request.Points,
            request.Description,
            actorContextAccessor.ActorContext.TenantId);

        if (!string.IsNullOrEmpty(request.IconUrl)) achievement.IconUrl = request.IconUrl;
        if (!string.IsNullOrEmpty(request.Color)) achievement.Color = request.Color;
        achievement.IsSecret = request.IsSecret;
        achievement.IsRepeatable = request.IsRepeatable;
        achievement.DisplayOrder = request.DisplayOrder;

        return achievementService.CreateAchievementAsync(achievement);
    }
}

public sealed class UpdateAchievementCommandHandler(IAchievementService achievementService)
    : ICommandHandler<UpdateAchievementCommand, Result<Achievement>>
{
    public async Task<Result<Achievement>> Handle(
        UpdateAchievementCommand request,
        CancellationToken cancellationToken)
    {
        var achievement = await achievementService.GetAchievementByIdAsync(request.AchievementId)
            .ConfigureAwait(false);
        if (achievement is null)
            return Result.Failure<Achievement>(Error.NotFound("NotFound", "Achievement not found"));

        achievement.Name = request.Name ?? achievement.Name;
        achievement.Description = request.Description ?? achievement.Description;
        achievement.Category = request.Category ?? achievement.Category;
        achievement.IconUrl = request.IconUrl ?? achievement.IconUrl;
        achievement.Color = request.Color ?? achievement.Color;

        if (request.Points.HasValue) achievement.UpdatePoints(request.Points.Value);
        if (request.IsActive.HasValue)
        {
            if (request.IsActive.Value) achievement.Activate();
            else achievement.Deactivate();
        }
        if (request.IsSecret.HasValue) achievement.IsSecret = request.IsSecret.Value;
        if (request.IsRepeatable.HasValue) achievement.IsRepeatable = request.IsRepeatable.Value;
        if (request.DisplayOrder.HasValue) achievement.DisplayOrder = request.DisplayOrder.Value;

        return await achievementService.UpdateAchievementAsync(achievement).ConfigureAwait(false);
    }
}

public sealed class DeleteAchievementCommandHandler(IAchievementService achievementService)
    : ICommandHandler<DeleteAchievementCommand, Result>
{
    public Task<Result> Handle(DeleteAchievementCommand request, CancellationToken cancellationToken) =>
        achievementService.DeleteAchievementAsync(request.AchievementId);
}

public sealed class AwardAchievementCommandHandler(
    IAchievementService achievementService,
    IActorContextAccessor actorContextAccessor)
    : ICommandHandler<AwardAchievementCommand, Result<UserAchievement>>
{
    public Task<Result<UserAchievement>> Handle(
        AwardAchievementCommand request,
        CancellationToken cancellationToken) =>
        achievementService.AwardAchievementAsync(
            request.UserId,
            request.AchievementId,
            request.Context,
            actorContextAccessor.ActorContext.TenantId);
}
