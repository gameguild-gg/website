using GameGuild.Notifications;
using GameGuild.Notifications.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GameGuild.Commerce.Payments;

/// <summary>
///     Runs one pass of low-balance wallet monitoring (issue #406): issues a
/// <see cref="NotificationType.WalletBalanceBelowThreshold" /> notice to the owner of every
/// active wallet whose balance dropped below the configured threshold, and re-arms the notice
/// once a wallet recovers to or above the threshold. Notices are idempotent per dip — the
/// wallet's LowBalanceAlertedAt marker persists the alert state.
/// </summary>
public interface IWalletBalanceAlertMonitor
{
    /// <summary>
    ///     Executes one monitoring pass.
    /// </summary>
    /// <returns>Number of low-balance notices issued during the pass.</returns>
    Task<int> RunPassAsync(DateTime nowUtc, CancellationToken cancellationToken = default);
}

/// <inheritdoc />
public class WalletBalanceAlertMonitor(
    IWalletRepository walletRepository,
    INotificationService notificationService,
    IOptions<WalletsOptions> options,
    ILogger<WalletBalanceAlertMonitor> logger) : IWalletBalanceAlertMonitor
{
    /// <inheritdoc />
    public async Task<int> RunPassAsync(DateTime nowUtc, CancellationToken cancellationToken = default)
    {
        var threshold = options.Value.LowBalanceThreshold;
        var candidates = await walletRepository.ListBalanceAlertCandidatesAsync(threshold, cancellationToken).ConfigureAwait(false);
        var noticesIssued = 0;

        foreach (var wallet in candidates)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                noticesIssued += await ProcessAsync(wallet, threshold, nowUtc, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception)
            {
                logger.LogWarning(
                    exception,
                    "Low-balance monitoring failed for wallet {WalletId}; will retry on the next pass.",
                    wallet.Id);
            }
        }

        if (noticesIssued > 0)
        {
            logger.LogInformation("Wallet low-balance pass issued {Notices} notice(s).", noticesIssued);
        }

        return noticesIssued;
    }

    private async Task<int> ProcessAsync(UserWallet wallet, decimal threshold, DateTime nowUtc, CancellationToken cancellationToken)
    {
        if (wallet.Balance < threshold)
        {
            if (wallet.LowBalanceAlertedAt is not null)
            {
                // Already alerted for this dip — stay quiet until the balance recovers.
                return 0;
            }

            var sent = await SendLowBalanceNoticeAsync(wallet, threshold, cancellationToken).ConfigureAwait(false);
            wallet.LowBalanceAlertedAt = nowUtc;
            walletRepository.Update(wallet);
            await walletRepository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return sent ? 1 : 0;
        }

        if (wallet.LowBalanceAlertedAt is not null)
        {
            // Balance recovered to or above the threshold: clear the marker so a future dip re-alerts.
            wallet.LowBalanceAlertedAt = null;
            walletRepository.Update(wallet);
            await walletRepository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        return 0;
    }

    private async Task<bool> SendLowBalanceNoticeAsync(UserWallet wallet, decimal threshold, CancellationToken cancellationToken)
    {
        var result = await notificationService.SendAsync(
            recipientId: wallet.UserId,
            type: NotificationType.WalletBalanceBelowThreshold,
            title: "Your wallet balance is running low",
            message:
                $"Your {wallet.Currency} wallet balance ({wallet.Balance:0.00}) dropped below {threshold:0.00} {wallet.Currency}. " +
                "Top up your wallet to keep purchases and subscriptions running without interruption.",
            channel: NotificationChannel.InApp,
            tenantId: wallet.TenantId,
            priority: NotificationPriority.Normal,
            referenceEntityId: wallet.Id,
            referenceEntityType: nameof(UserWallet),
            cancellationToken: cancellationToken).ConfigureAwait(false);

        if (!result.IsSuccess)
        {
            // Preference enforcement (mute/digest/quiet-hours) intentionally produced no row.
            logger.LogInformation(
                "Low-balance notice for wallet {WalletId} was not delivered: {Reason}",
                wallet.Id, result.Error?.Description ?? "preference decision");
            return false;
        }

        return true;
    }
}
