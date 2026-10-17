namespace GameGuild.Assets;

/// <summary>
/// A single moderation label with its confidence score.
/// </summary>
public sealed record ModerationLabelScore(string Name, double Score);

/// <summary>
/// Result of classifying asset content with an automated moderation model.
/// <see cref="Failed"/> indicates the classifier could not produce a usable
/// result (transport error, unparseable response, timeout). Failed results are
/// never auto-approved by the decision policy.
/// </summary>
public sealed record AssetModerationClassification(
    bool Failed,
    IReadOnlyList<ModerationLabelScore> Labels,
    string Model);

/// <summary>
/// Thrown by classifier adapters when the backing model responds with an
/// unparseable or unusable payload. Callers treat this as a classifier failure
/// (NeedsReview), never as approval.
/// </summary>
public sealed class ModerationClassificationException(string message, Exception? innerException = null)
    : Exception(message, innerException);

/// <summary>
/// Platform-pure abstraction over automated content moderation classifiers.
/// Implementations are registered by the host composition root; the module
/// never references any concrete AI stack.
/// </summary>
public interface IAssetModerationClassifier
{
    /// <summary>
    /// Classifies the given image bytes.
    /// </summary>
    /// <param name="content">The content row (mime type, metadata).</param>
    /// <param name="imageBytes">The original binary to classify.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <exception cref="ModerationClassificationException">
    /// The classifier response was unusable. Transport-level failures surface
    /// as their original exceptions; both paths count as classifier failure.
    /// </exception>
    Task<AssetModerationClassification> ClassifyAsync(
        AssetContent content,
        Stream imageBytes,
        CancellationToken ct = default);
}
