namespace GameGuild.Identity.Authentication;

public abstract class BulkPermissionResult
{
    public int TotalRequested { get; set; }

    public int Successful { get; set; }

    public int Failed { get; set; }

    public List<BulkPermissionFailure> Failures { get; set; } = new List<BulkPermissionFailure>();

    public DateTime ProcessedAt { get; set; }

    /// <summary>
    ///     Business-failure contract (issues #302/#310): a bulk operation failed for at
    ///     least one item. Recognized by the transactional outcome classifier
    ///     (<c>CommandOutcome.IsFailure</c>) so partial failures are reported as failures
    ///     instead of silently reading as success.
    /// </summary>
    public bool IsFailure => Failed > 0;

    /// <summary>
    ///     Business-success contract (issues #302/#310): every requested item succeeded.
    /// </summary>
    public bool IsSuccess => !IsFailure;
}
