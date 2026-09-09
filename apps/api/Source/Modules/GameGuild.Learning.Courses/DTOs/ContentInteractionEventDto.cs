using GameGuild.Learning.Grading.Contracts;

namespace GameGuild.Learning.Courses;

public sealed record ContentInteractionEventDto(
    Guid Id,
    Guid InteractionId,
    ContentInteractionEventType Type,
    DateTime OccurredAt,
    int? DurationSeconds,
    decimal? PositionSeconds,
    PercentValue? ProgressPercentage,
    string? Payload,
    string? IdempotencyKey)
{
    public static ContentInteractionEventDto FromEntity(ContentInteractionEvent item) =>
        new(
            item.Id,
            item.InteractionId,
            item.Type,
            item.OccurredAt,
            item.DurationSeconds,
            item.PositionSeconds,
            item.ProgressPercentage,
            item.Payload,
            item.IdempotencyKey);
}
