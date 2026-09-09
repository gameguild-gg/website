using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using GameGuild.Learning.Grading.Contracts;

namespace GameGuild.Learning.Courses;

[Table("content_interaction_events")]
[Index(nameof(InteractionId), nameof(OccurredAt))]
[Index(nameof(InteractionId), nameof(IdempotencyKey), IsUnique = true)]
public sealed class ContentInteractionEvent : EntityBase
{
    [Required]
    public Guid InteractionId { get; set; }

    public ContentInteractionEventType Type { get; set; }

    public DateTime OccurredAt { get; set; }

    public int? DurationSeconds { get; set; }

    [Column(TypeName = "decimal(12,3)")]
    public decimal? PositionSeconds { get; set; }

    public PercentValue? ProgressPercentage { get; set; }

    public string? Payload { get; set; }

    [MaxLength(128)]
    public string? IdempotencyKey { get; set; }

    public ContentInteraction Interaction { get; set; } = null!;

    public static ContentInteractionEvent Create(
        Guid interactionId,
        ContentInteractionEventType type,
        int? durationSeconds = null,
        decimal? positionSeconds = null,
        PercentValue? progressPercentage = null,
        string? payload = null,
        string? idempotencyKey = null,
        DateTime? occurredAt = null)
    {
        if (interactionId == Guid.Empty)
        {
            throw new ArgumentException("Interaction ID is required.", nameof(interactionId));
        }

        if (!Enum.IsDefined(type))
        {
            throw new ArgumentOutOfRangeException(nameof(type), type, "Interaction event type is not supported.");
        }

        if (durationSeconds <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(durationSeconds), "Duration must be positive when provided.");
        }

        if (positionSeconds.HasValue && !IsValidPositionSeconds(positionSeconds.Value))
        {
            throw new ArgumentOutOfRangeException(
                nameof(positionSeconds),
                "Position must fit numeric(12,3) and cannot be negative.");
        }

        if (!string.IsNullOrWhiteSpace(payload))
        {
            try
            {
                using var _ = JsonDocument.Parse(payload);
            }
            catch (JsonException exception)
            {
                throw new ArgumentException("Event payload must be valid JSON.", nameof(payload), exception);
            }
        }

        var normalizedIdempotencyKey = string.IsNullOrWhiteSpace(idempotencyKey)
            ? null
            : idempotencyKey.Trim();
        if (normalizedIdempotencyKey?.Length > 128)
        {
            throw new ArgumentException("Idempotency key cannot exceed 128 characters.", nameof(idempotencyKey));
        }

        return new ContentInteractionEvent
        {
            Id = Guid.NewGuid(),
            InteractionId = interactionId,
            Type = type,
            OccurredAt = (occurredAt ?? SystemClock.UtcNow).ToUniversalTime(),
            DurationSeconds = durationSeconds,
            PositionSeconds = positionSeconds,
            ProgressPercentage = progressPercentage,
            Payload = string.IsNullOrWhiteSpace(payload) ? null : payload,
            IdempotencyKey = normalizedIdempotencyKey,
        };
    }

    internal static bool IsValidPositionSeconds(decimal value) =>
        value is >= 0 and <= 999999999.999m && value == decimal.Round(value, 3);

    internal bool MatchesReplay(
        ContentInteractionEventType type,
        int? durationSeconds,
        decimal? positionSeconds,
        PercentValue? progressPercentage,
        string? payload,
        DateTime? occurredAt)
    {
        var normalizedPayload = string.IsNullOrWhiteSpace(payload) ? null : payload;
        var occurredAtMatches = !occurredAt.HasValue ||
                                OccurredAt == occurredAt.Value.ToUniversalTime();

        return Type == type &&
               DurationSeconds == durationSeconds &&
               PositionSeconds == positionSeconds &&
               ProgressPercentage == progressPercentage &&
               Payload == normalizedPayload &&
               occurredAtMatches;
    }
}
