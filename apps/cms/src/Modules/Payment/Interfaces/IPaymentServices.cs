using GameGuild.Common.Enums;

namespace GameGuild.Modules.Payment.Interfaces;

/// <summary>
/// Interface for financial transaction services
/// </summary>
public interface IFinancialTransactionService
{
    Task<Models.FinancialTransaction> CreateTransactionAsync(Models.FinancialTransaction transaction);
    Task<Models.FinancialTransaction?> GetTransactionByIdAsync(int id);
    Task<Models.FinancialTransaction?> GetTransactionByExternalIdAsync(string externalId);
    Task<IEnumerable<Models.FinancialTransaction>> GetUserTransactionsAsync(int userId);
    Task<Models.FinancialTransaction> UpdateTransactionStatusAsync(int id, TransactionStatus status);
    Task<Models.FinancialTransaction> ProcessTransactionAsync(int id);
    Task<decimal> GetUserBalanceAsync(int userId);
    Task<IEnumerable<Models.FinancialTransaction>> GetPendingTransactionsAsync();
    Task<bool> RefundTransactionAsync(int transactionId, decimal? amount = null);
}

/// <summary>
/// Interface for payment method management services
/// </summary>
public interface IUserFinancialMethodService
{
    Task<Models.UserFinancialMethod> AddPaymentMethodAsync(Models.UserFinancialMethod paymentMethod);
    Task<Models.UserFinancialMethod?> GetPaymentMethodByIdAsync(int id);
    Task<IEnumerable<Models.UserFinancialMethod>> GetUserPaymentMethodsAsync(int userId);
    Task<Models.UserFinancialMethod?> GetDefaultPaymentMethodAsync(int userId);
    Task<bool> SetDefaultPaymentMethodAsync(int userId, int paymentMethodId);
    Task<Models.UserFinancialMethod> UpdatePaymentMethodAsync(Models.UserFinancialMethod paymentMethod);
    Task<bool> RemovePaymentMethodAsync(int id);
    Task<bool> ValidatePaymentMethodAsync(int id);
}

/// <summary>
/// Interface for wallet and balance management services
/// </summary>
public interface IWalletService
{
    Task<decimal> GetWalletBalanceAsync(int userId);
    Task<decimal> GetAvailableBalanceAsync(int userId);
    Task<bool> AddFundsAsync(int userId, decimal amount, string description);
    Task<bool> DeductFundsAsync(int userId, decimal amount, string description);
    Task<bool> TransferFundsAsync(int fromUserId, int toUserId, decimal amount, string description);
    Task<bool> FreezeFundsAsync(int userId, decimal amount, string reason);
    Task<bool> UnfreezeFundsAsync(int userId, decimal amount);
    Task<IEnumerable<Models.FinancialTransaction>> GetWalletTransactionsAsync(int userId);
}
