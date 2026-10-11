using Asp.Versioning;
using GameGuild.Commerce.Payments.Commands.CloseWallet;
using GameGuild.Commerce.Payments.Commands.FreezeWallet;
using GameGuild.Commerce.Payments.Commands.PatchWallet;
using GameGuild.Commerce.Payments.Commands.UnfreezeWallet;
using GameGuild.Commerce.Payments.Models;
using GameGuild.Commerce.Payments.Queries.GetWalletAuditLog;
using GameGuild.Commerce.Payments.Queries.GetWalletById;
using GameGuild.Commerce.Payments.Queries.ListWallets;
using GameGuild.CQRS;
using GameGuild.Identity.Authorization;
using GameGuild.Identity.Context.Actors;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace GameGuild.Commerce.Payments;

/// <summary>
///     Self-service wallet access and explicitly privileged platform administration.
/// </summary>
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}")]
[Microsoft.AspNetCore.Http.Tags("wallets")]
[Authorize]
public sealed class WalletsController(ISender sender, IActorContextAccessor actorContextAccessor) : BaseApiController
{
    [HttpPost("wallet")]
    [EndpointSummary("Create my wallet")]
    [ProducesResponseType(typeof(UserWallet), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateMyWallet([FromBody] CreateWalletRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!HasSelfServiceContext()) return Forbid();

        try
        {
            var wallet = await sender.Send(new CreateMyWalletCommand(request.Currency ?? "USD"), ct).ConfigureAwait(false);
            return CreatedAtAction(nameof(GetMyWallet), wallet);
        }
        catch (InvalidOperationException exception)
        {
            // Issue #406: one wallet per user per currency — a duplicate in the same currency conflicts.
            return Conflict(new { error = exception.Message });
        }
    }

    [HttpGet("wallet")]
    [EndpointSummary("Get my wallet")]
    [ProducesResponseType(typeof(UserWallet), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetMyWallet(CancellationToken ct)
    {
        if (!HasSelfServiceContext()) return Forbid();

        var wallet = await sender.Send(new GetMyWalletQuery(), ct).ConfigureAwait(false);
        return wallet is null ? NotFound() : Ok(wallet);
    }

    [HttpGet("wallet/balance")]
    [EndpointSummary("Get my wallet balance")]
    public async Task<IActionResult> GetMyWalletBalance(CancellationToken ct)
    {
        if (!HasSelfServiceContext()) return Forbid();

        return Ok(await sender.Send(new GetMyWalletBalanceQuery(), ct).ConfigureAwait(false));
    }

    [HttpPost("wallet:lock")]
    [EndpointSummary("Lock my wallet")]
    public async Task<IActionResult> LockMyWallet([FromBody] LockWalletRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!HasSelfServiceContext()) return Forbid();

        await sender.Send(new LockMyWalletCommand(request.Reason), ct).ConfigureAwait(false);
        return NoContent();
    }

    [HttpPost("wallet:unlock")]
    [EndpointSummary("Unlock my wallet")]
    public async Task<IActionResult> UnlockMyWallet(CancellationToken ct)
    {
        if (!HasSelfServiceContext()) return Forbid();

        await sender.Send(new UnlockMyWalletCommand(), ct).ConfigureAwait(false);
        return NoContent();
    }

    [HttpGet("wallets")]
    [EndpointSummary("List all wallets")]
    public async Task<IActionResult> ListWallets(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? currency = null,
        [FromQuery] bool? isFrozen = null,
        CancellationToken ct = default)
    {
        if (!CanAdministerWallets()) return Forbid();
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 100);
        return Ok(await sender.Send(new ListWalletsQuery(page, pageSize, currency, isFrozen), ct).ConfigureAwait(false));
    }

    [HttpGet("wallets/{walletId:guid}")]
    [EndpointSummary("Get wallet by ID")]
    public async Task<IActionResult> GetWalletById(Guid walletId, CancellationToken ct)
    {
        if (!CanAdministerWallets()) return Forbid();
        var wallet = await sender.Send(new GetWalletByIdQuery(walletId), ct).ConfigureAwait(false);
        return wallet is null ? NotFound() : Ok(wallet);
    }

    [HttpHead("wallets/{walletId:guid}")]
    [EndpointSummary("Check if wallet exists")]
    public async Task<IActionResult> CheckWalletExists(Guid walletId, CancellationToken ct)
    {
        if (!CanAdministerWallets()) return Forbid();
        return await sender.Send(new GetWalletByIdQuery(walletId), ct).ConfigureAwait(false) is null ? NotFound() : Ok();
    }

    [HttpPatch("wallets/{walletId:guid}")]
    [EndpointSummary("Update wallet settings")]
    public async Task<IActionResult> PatchWallet(Guid walletId, [FromBody] PatchWalletRequest body, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(body);
        if (!CanAdministerWallets()) return Forbid();
        await sender.Send(new PatchWalletCommand(walletId, body.Currency, body.DailyLimit, body.MonthlyLimit), ct).ConfigureAwait(false);
        return NoContent();
    }

    [HttpDelete("wallets/{walletId:guid}")]
    [EndpointSummary("Close wallet")]
    public async Task<IActionResult> DeleteWallet(Guid walletId, CancellationToken ct)
    {
        if (!CanAdministerWallets()) return Forbid();
        await sender.Send(new CloseWalletCommand(walletId), ct).ConfigureAwait(false);
        return NoContent();
    }

    [HttpPost("wallets/{walletId:guid}:freeze")]
    [EndpointSummary("Freeze wallet")]
    public async Task<IActionResult> FreezeWallet(Guid walletId, [FromBody] FreezeWalletRequest body, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(body);
        if (!CanAdministerWallets()) return Forbid();
        await sender.Send(new FreezeWalletCommand(walletId, body.Reason), ct).ConfigureAwait(false);
        return NoContent();
    }

    [HttpPost("wallets/{walletId:guid}:unfreeze")]
    [EndpointSummary("Unfreeze wallet")]
    public async Task<IActionResult> UnfreezeWallet(Guid walletId, CancellationToken ct)
    {
        if (!CanAdministerWallets()) return Forbid();
        await sender.Send(new UnfreezeWalletCommand(walletId), ct).ConfigureAwait(false);
        return NoContent();
    }

    [HttpGet("wallets/{walletId:guid}/audit-log")]
    [EndpointSummary("Get wallet audit log")]
    public async Task<IActionResult> GetWalletAuditLog(
        Guid walletId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        if (!CanAdministerWallets()) return Forbid();
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 100);
        return Ok(await sender.Send(new GetWalletAuditLogQuery(walletId, page, pageSize), ct).ConfigureAwait(false));
    }

    /// <summary>
    ///     Administrative balance adjustment (issue #406): credits or debits a wallet through
    ///     the audited AddFunds/DeductFunds commands. A duplicate ReferenceId replays
    ///     idempotently and returns the original transaction.
    /// </summary>
    [HttpPost("wallets/{walletId:guid}:adjust")]
    [EndpointSummary("Credit or debit a wallet (administrative adjustment)")]
    [ProducesResponseType(typeof(WalletTransaction), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> AdjustWallet(Guid walletId, [FromBody] AdjustWalletRequest body, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(body);
        if (!CanAdministerWallets()) return Forbid();

        var wallet = await sender.Send(new GetWalletByIdQuery(walletId), ct).ConfigureAwait(false);
        if (wallet is null) return NotFound();

        if (body.Direction is not ("credit" or "debit"))
        {
            return BadRequest(new { error = "Direction must be 'credit' or 'debit'" });
        }

        try
        {
            var transaction = body.Direction == "credit"
                ? await sender.Send(new AddFundsCommand(wallet.UserId, body.Amount, body.Reason, body.ReferenceId, wallet.Currency), ct).ConfigureAwait(false)
                : await sender.Send(new DeductFundsCommand(wallet.UserId, body.Amount, body.Reason, body.ReferenceId, wallet.Currency), ct).ConfigureAwait(false);
            return Ok(transaction);
        }
        catch (InvalidOperationException exception)
        {
            return Conflict(new { error = exception.Message });
        }
    }

    /// <summary>
    ///     Wallet-to-wallet transfer (issue #406): moves funds between two same-currency wallets
    ///     through the audited TransferFunds command. A duplicate ReferenceId replays
    ///     idempotently.
    /// </summary>
    [HttpPost("wallets:transfer")]
    [EndpointSummary("Transfer funds between two wallets")]
    [ProducesResponseType(typeof(TransferWalletsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> TransferBetweenWallets([FromBody] TransferWalletsRequest body, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(body);
        if (!CanAdministerWallets()) return Forbid();

        if (body.FromWalletId == body.ToWalletId)
        {
            return BadRequest(new { error = "Cannot transfer funds to the same wallet" });
        }

        var fromWallet = await sender.Send(new GetWalletByIdQuery(body.FromWalletId), ct).ConfigureAwait(false);
        var toWallet = await sender.Send(new GetWalletByIdQuery(body.ToWalletId), ct).ConfigureAwait(false);
        if (fromWallet is null || toWallet is null) return NotFound();

        if (!string.Equals(fromWallet.Currency, toWallet.Currency, StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new { error = $"Currency mismatch: {fromWallet.Currency} != {toWallet.Currency}" });
        }

        try
        {
            var result = await sender.Send(
                new TransferFundsCommand(fromWallet.UserId, toWallet.UserId, body.Amount, body.Description, body.ReferenceId, fromWallet.Currency),
                ct).ConfigureAwait(false);
            return Ok(new TransferWalletsResponse(result.DebitTransaction, result.CreditTransaction));
        }
        catch (InvalidOperationException exception)
        {
            return Conflict(new { error = exception.Message });
        }
    }

    /// <summary>
    ///     Report-only balance reconciliation (issue #406): per wallet, verifies Balance against
    ///     the latest BalanceAfter and the signed sum of completed transactions. Never mutates state.
    /// </summary>
    [HttpGet("wallets/reconciliation")]
    [EndpointSummary("Reconcile wallet balances against transaction history")]
    [ProducesResponseType(typeof(WalletReconciliationReportResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetWalletReconciliation(
        [FromQuery] Guid? walletId = null,
        CancellationToken ct = default)
    {
        if (!CanAdministerWallets()) return Forbid();
        return Ok(await sender.Send(new GetWalletReconciliationReportQuery(walletId), ct).ConfigureAwait(false));
    }

    /// <summary>
    ///     Wallet balance analytics (issue #406): total balances by currency, active/frozen
    ///     wallet counts and completed transaction volume by type and day.
    /// </summary>
    [HttpGet("wallets/metrics")]
    [EndpointSummary("Wallet balance and transaction volume metrics")]
    [ProducesResponseType(typeof(WalletMetricsResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetWalletMetrics(
        [FromQuery] int days = 30,
        CancellationToken ct = default)
    {
        if (!CanAdministerWallets()) return Forbid();
        days = Math.Clamp(days, 1, 365);
        return Ok(await sender.Send(new GetWalletMetricsQuery(days), ct).ConfigureAwait(false));
    }

    private bool HasSelfServiceContext()
    {
        var actor = actorContextAccessor.ActorContext;
        return actor.IsAuthenticated && actor.SubjectIdAsGuid.HasValue && actor.TenantId.HasValue;
    }

    private bool CanAdministerWallets()
    {
        var actor = actorContextAccessor.ActorContext;
        return actor.IsAuthenticated && actor.TenantId.HasValue &&
               actor.HasPermission(WalletsPermission.Keys.Admin);
    }
}
