namespace GameGuild.Commerce.Payments;

/// <summary>
///     One settled line extracted from an external accounting/ERP statement.
///     Lines are supplied either inline by an operator or by an
///     <see cref="IExternalRevenueStatementSource" /> integration adapter; no outbound
///     calls are made by the platform itself.
/// </summary>
/// <param name="ReferenceId">Provider/settlement reference that maps to a revenue event reference.</param>
/// <param name="Amount">Settled amount as reported by the external system.</param>
/// <param name="Currency">ISO 4217 currency code of the settled amount.</param>
/// <param name="OccurredAtUtc">Moment the external system attributes the settlement to.</param>
public sealed record ExternalRevenueStatementLine(
    string ReferenceId,
    decimal Amount,
    string Currency,
    DateTime OccurredAtUtc);
