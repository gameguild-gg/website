using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace GameGuild.Commerce.Payments;

/// <summary>Entity representing a user's wallet</summary>
[Table("user_wallets")]
[Index(nameof(UserId), nameof(Currency), IsUnique = true)]
[Index(nameof(Currency))]
[Index(nameof(IsActive))]
public class UserWallet : EntityBase
{
    /// <summary>Default constructor</summary>
    public UserWallet() { }

    /// <summary>Constructor for partial initialization</summary>
    /// <param name="partial">Partial wallet data</param>
    public UserWallet(object partial) : base(partial) { }

    /// <summary>Foreign key to the User entity</summary>
    [Required]
    public Guid UserId { get; set; }

    /// <summary>Current balance</summary>
    [Column(TypeName = "decimal(18,2)")]
    public decimal Balance { get; set; }

    /// <summary>Currency code</summary>
    [Required]
    [MaxLength(3)]
    public string Currency { get; set; } = "USD";

    /// <summary>Whether this wallet is active</summary>
    public bool IsActive { get; set; } = true;

    /// <summary>Whether this wallet is locked</summary>
    public bool IsLocked { get; set; }

    /// <summary>Reason for lock (if locked)</summary>
    [MaxLength(500)]
    public string? LockReason { get; set; }

    /// <summary>Last transaction timestamp</summary>
    public DateTime? LastTransactionAt { get; set; }

    /// <summary>Daily spending limit (null = no limit)</summary>
    [Column(TypeName = "decimal(18,2)")]
    public decimal? DailyLimit { get; set; }

    /// <summary>Monthly spending limit (null = no limit)</summary>
    [Column(TypeName = "decimal(18,2)")]
    public decimal? MonthlyLimit { get; set; }

    /// <summary>
    ///     Timestamp of the last low-balance threshold notification issued for this wallet
    ///     (issue #406). Null when the wallet is above the configured threshold or was never
    ///     alerted; cleared again once the balance returns to or above the threshold so a
    ///     subsequent dip re-triggers the notice.
    /// </summary>
    public DateTime? LowBalanceAlertedAt { get; set; }

    /// <summary>Navigation property to wallet transactions</summary>
    public virtual ICollection<WalletTransaction> Transactions { get; set; } = new List<WalletTransaction>();

    /// <summary>Add funds to the wallet</summary>
    /// <returns>The ledger transaction appended by this credit (issue #406: callers must
    /// register it with the repository explicitly — see <see cref="IWalletRepository.AddTransaction"/>).</returns>
    public WalletTransaction AddFunds(decimal amount, string description, string? referenceId = null)
    {
        if (!IsActive) throw new InvalidOperationException("Wallet is not active");
        if (IsLocked) throw new InvalidOperationException($"Wallet is locked: {LockReason}");
        if (amount <= 0) throw new ArgumentException("Amount must be positive", nameof(amount));

        Balance += amount;
        LastTransactionAt = SystemClock.UtcNow;

        var transaction = new WalletTransaction
        {
            WalletId = Id, Type = WalletTransactionType.Credit, Amount = amount, BalanceAfter = Balance, Description = description, ReferenceId = referenceId, Status = TransactionStatus.Completed
        };

        Transactions.Add(transaction);
        return transaction;
    }

    /// <summary>
    /// Deduct funds from the wallet with optimistic concurrency protection.
    /// The EntityBase.Version property is automatically checked on save via EF Core
    /// ConcurrencyCheck, preventing double-spend in concurrent scenarios.
    /// </summary>
    /// <returns>The ledger transaction appended by this debit (issue #406: callers must
    /// register it with the repository explicitly — see <see cref="IWalletRepository.AddTransaction"/>).</returns>
    /// <exception cref="InvalidOperationException">Thrown if wallet is inactive, locked, or has insufficient funds</exception>
    public WalletTransaction DeductFunds(decimal amount, string description, string? referenceId = null)
    {
        if (!IsActive) throw new InvalidOperationException("Wallet is not active");
        if (IsLocked) throw new InvalidOperationException($"Wallet is locked: {LockReason}");
        if (amount <= 0) throw new ArgumentException("Amount must be positive", nameof(amount));
        if (Balance < amount) throw new InvalidOperationException("Insufficient balance");

        Balance -= amount;
        LastTransactionAt = SystemClock.UtcNow;

        // Touch() increments the Version property which triggers EF Core's
        // concurrency check on SaveChangesAsync(), preventing double-spend
        Touch();

        var transaction = new WalletTransaction
        {
            WalletId = Id, Type = WalletTransactionType.Debit, Amount = amount, BalanceAfter = Balance, Description = description, ReferenceId = referenceId, Status = TransactionStatus.Completed
        };

        Transactions.Add(transaction);
        return transaction;
    }

    /// <summary>Lock the wallet</summary>
    public void Lock(string reason)
    {
        IsLocked = true;
        LockReason = reason;
        Touch();
    }

    /// <summary>Unlock the wallet</summary>
    public void Unlock()
    {
        IsLocked = false;
        LockReason = null;
        Touch();
    }
}
