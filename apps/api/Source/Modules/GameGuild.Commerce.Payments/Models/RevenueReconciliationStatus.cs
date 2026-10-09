namespace GameGuild.Commerce.Payments;

/// <summary>Lifecycle status of a revenue reconciliation run</summary>
public enum RevenueReconciliationStatus
{
    /// <summary>Run was created and matching is in progress</summary>
    Running = 0,

    /// <summary>Run finished and its results were committed</summary>
    Completed = 1,

    /// <summary>Run aborted before results were committed</summary>
    Failed = 2
}
