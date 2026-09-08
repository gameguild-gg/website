namespace GameGuild.Commerce.Subscriptions;

public interface IMonthlyStatementAttachmentBuilder
{
    Task<MonthlyStatementArtifacts> BuildAsync(
        Guid tenantId,
        DateOnly fromDate,
        DateOnly toDate,
        CancellationToken cancellationToken = default);
}

public sealed record MonthlyStatementArtifacts(
    MonthlyStatementReport Report,
    IReadOnlyList<MonthlyStatementEmailAttachment> Attachments);

public sealed record MonthlyStatementReport(
    Guid TenantId,
    DateTime GeneratedAtUtc,
    DateOnly FromDate,
    DateOnly ToDate,
    int LedgerCount,
    int RootLedgerCount,
    int EntryCount,
    decimal TotalDebit,
    decimal TotalCredit,
    decimal NetCashFlow,
    decimal ClosingBalance,
    IReadOnlyList<StatementCategorySummary> Categories,
    IReadOnlyList<StatementPeriodSummary> Periods,
    IReadOnlyList<StatementTransactionSummary> Transactions,
    IReadOnlyList<StatementOwnerSummary> OwnerStatements,
    IReadOnlyList<StatementRenterSummary> RenterPayments,
    StatementMaintenanceSummary? MaintenanceReport);

public sealed record StatementCategorySummary(
    string Category,
    decimal TotalDebit,
    decimal TotalCredit,
    decimal NetAmount,
    int EntryCount,
    decimal PercentageOfTotal);

public sealed record StatementPeriodSummary(
    string PeriodStart,
    string PeriodEnd,
    string PeriodLabel,
    decimal TotalDebit,
    decimal TotalCredit,
    decimal NetChange,
    decimal RunningBalance,
    int EntryCount);

public sealed record StatementTransactionSummary(
    Guid Id,
    string TransactionDate,
    string LedgerCode,
    string Type,
    string Category,
    string Description,
    decimal Amount,
    string Status,
    string? CounterpartyName,
    DateTime CreatedAtUtc);

public sealed record StatementOwnerSummary(
    Guid OwnerId,
    string OwnerName,
    string Email,
    int PropertyCount,
    decimal EstimatedMonthlyGrossUsd,
    decimal EstimatedMonthlyExpensesUsd,
    decimal ApprovedMaintenanceUsd,
    decimal EstimatedMonthlyNetUsd,
    DateTime GeneratedAtUtc);

public sealed record StatementRenterSummary(
    Guid RenterId,
    string RenterName,
    string Email,
    int PropertyCount,
    int PaymentCount,
    decimal TotalBilledUsd,
    decimal TotalPaidUsd,
    int OverdueCount,
    decimal CurrentDueUsd,
    DateTime GeneratedAtUtc);

public sealed record StatementMaintenanceSummary(
    DateTime GeneratedAtUtc,
    int TicketCount,
    int OpenTicketCount,
    int OverdueTicketCount,
    int EscalatedTicketCount,
    int QuoteCount,
    int PendingQuoteCount,
    int OverdueQuoteCount,
    int EscalatedQuoteCount);
