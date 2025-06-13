using System.ComponentModel;

namespace GameGuild.Common.Enums;

public enum PaymentMethodType
{
    [Description("Credit card payment method")]
    CreditCard,

    [Description("Debit card payment method")]
    DebitCard,

    [Description("Cryptocurrency wallet for blockchain payments")]
    CryptoWallet,

    [Description("Platform internal wallet balance")]
    WalletBalance,

    [Description("Direct bank transfer or wire payment")]
    BankTransfer
}

public enum PaymentMethodStatus
{
    [Description("Payment method is valid and can be used")]
    Active,

    [Description("Payment method temporarily disabled")]
    Inactive,

    [Description("Payment method has expired (e.g., expired card)")]
    Expired,

    [Description("Payment method has been deleted by the user")]
    Removed
}

public enum TransactionStatus
{
    [Description("Transaction has been initiated but not completed")]
    Pending,

    [Description("Transaction is being processed by payment provider")]
    Processing,

    [Description("Transaction has been successfully completed")]
    Completed,

    [Description("Transaction failed to process")]
    Failed,

    [Description("Transaction was completed but has been refunded")]
    Refunded,

    [Description("Transaction was cancelled before processing")]
    Cancelled
}

public enum TransactionType
{
    [Description("Payment for acquiring a product")]
    Purchase,

    [Description("Return of funds to the customer")]
    Refund,

    [Description("Removal of funds from the platform")]
    Withdrawal,

    [Description("Addition of funds to the platform")]
    Deposit,

    [Description("Movement of funds between wallets")]
    Transfer,

    [Description("Platform or processing fees")]
    Fee,

    [Description("Manual correction to balance")]
    Adjustment
}

public enum WalletStatus
{
    [Description("Wallet is operational and can process transactions")]
    Active,

    [Description("Wallet temporarily suspended, cannot process transactions")]
    Frozen,

    [Description("Wallet permanently deactivated")]
    Closed
}

public enum PromoCodeType
{
    [Description("Applies a percentage discount to the total price")]
    PercentageOff,

    [Description("Deducts a specific amount from the total price")]
    FixedAmountOff,

    [Description("Purchase one product and receive another free")]
    BuyOneGetOne,

    [Description("No charge for the first month of a subscription")]
    FirstMonthFree
}

public enum ProductAcquisitionType
{
    [Description("Product acquired through direct payment")]
    Purchase,

    [Description("Product access via recurring subscription")]
    Subscription,

    [Description("Product provided at no cost")]
    Free,

    [Description("Product received as a gift from another user")]
    Gift
}

public enum ProductAccessStatus
{
    [Description("User has full access to the product")]
    Active,

    [Description("Access period has ended")]
    Expired,

    [Description("Access manually removed by admin")]
    Revoked,

    [Description("Temporary hold on access that may be restored")]
    Suspended
}
