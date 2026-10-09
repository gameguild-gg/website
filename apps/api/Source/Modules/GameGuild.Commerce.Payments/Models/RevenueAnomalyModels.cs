namespace GameGuild.Commerce.Payments;

/// <summary>Kind of revenue anomaly detected by the statistical detector</summary>
public enum RevenueAnomalyKind
{
    /// <summary>Net revenue for the evaluated day is abnormally above the baseline</summary>
    Spike = 0,

    /// <summary>Net revenue for the evaluated day is abnormally below the baseline</summary>
    Drop = 1
}

/// <summary>Lifecycle status of a revenue anomaly alert</summary>
public enum RevenueAnomalyStatus
{
    /// <summary>Detected and awaiting review</summary>
    Open = 0,

    /// <summary>Reviewed and acknowledged by an operator</summary>
    Acknowledged = 1
}
