namespace GameGuild.Commerce.Payments;

/// <summary>Kind of mismatch detected between an external statement and internal revenue events</summary>
public enum RevenueDiscrepancyKind
{
    /// <summary>The external statement contains a reference with no matching internal revenue event</summary>
    MissingInternal = 0,

    /// <summary>An internal revenue event in the period was not covered by the external statement</summary>
    MissingExternal = 1,

    /// <summary>Internal and external amounts differ for the same reference</summary>
    AmountMismatch = 2,

    /// <summary>Internal and external currencies differ for the same reference</summary>
    CurrencyMismatch = 3,

    /// <summary>The external statement repeated the same reference more than once</summary>
    DuplicateExternalReference = 4
}
