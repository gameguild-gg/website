using GameGuild.CQRS;
using GameGuild.Economy.AdRewards;
using GameGuild.Economy.Bounties;
using GameGuild.Economy.Marketplace;

namespace GameGuild.API.Controllers;

public sealed record StartAdRewardSessionEndpointCommand(StartDurableAdRewardSessionRequest Request)
    : ICommand<DurableAdRewardSessionResult>;
public sealed record CompleteAdRewardSessionEndpointCommand(CompleteDurableAdRewardSessionRequest Request)
    : ICommand<DurableAdRewardCompletionResult>;
public sealed record ImportAdRewardReportEndpointCommand(ImportDurableAdProviderReportRequest Request)
    : ICommand<DurableAdProviderReportImportResult>;

public sealed record CreateBountyEndpointCommand(CreateDurableBountyRequest Request) : ICommand<DurableBountyView>;
public sealed record ClaimBountyEndpointCommand(ClaimDurableBountyRequest Request) : ICommand<DurableBountyView>;
public sealed record ReclaimBountyEndpointCommand(ReclaimDurableBountyRequest Request) : ICommand<DurableBountyView>;

public sealed record SettleMarketplaceOrderEndpointCommand(SettleAuthoritativeMarketplaceOrderRequest Request)
    : ICommand<DurableMarketplaceSettlementResult>;
public sealed record RefundMarketplaceOrderEndpointCommand(RefundAuthoritativeMarketplaceOrderRequest Request)
    : ICommand<DurableMarketplaceRefundResult>;
public sealed record RefundMarketplaceOrderAdministrationEndpointCommand(RefundAuthoritativeMarketplaceOrderRequest Request)
    : ICommand<DurableMarketplaceRefundResult>;

public sealed class EconomyPublicEndpointCommandHandler(
    IDurableAdRewardSessionService adRewardSessions,
    IDurableAdRewardCompletionService adRewardCompletions,
    IDurableAdRewardReportService adRewardReports,
    IDurableBountyApplicationService bounties,
    IDurableMarketplaceSettlementService marketplaceSettlements,
    IDurableMarketplaceRefundService marketplaceRefunds) :
    ICommandHandler<StartAdRewardSessionEndpointCommand, DurableAdRewardSessionResult>,
    ICommandHandler<CompleteAdRewardSessionEndpointCommand, DurableAdRewardCompletionResult>,
    ICommandHandler<ImportAdRewardReportEndpointCommand, DurableAdProviderReportImportResult>,
    ICommandHandler<CreateBountyEndpointCommand, DurableBountyView>,
    ICommandHandler<ClaimBountyEndpointCommand, DurableBountyView>,
    ICommandHandler<ReclaimBountyEndpointCommand, DurableBountyView>,
    ICommandHandler<SettleMarketplaceOrderEndpointCommand, DurableMarketplaceSettlementResult>,
    ICommandHandler<RefundMarketplaceOrderEndpointCommand, DurableMarketplaceRefundResult>,
    ICommandHandler<RefundMarketplaceOrderAdministrationEndpointCommand, DurableMarketplaceRefundResult>
{
    public async Task<DurableAdRewardSessionResult> Handle(StartAdRewardSessionEndpointCommand request, CancellationToken ct) =>
        await adRewardSessions.StartAsync(request.Request, ct).ConfigureAwait(false);

    public async Task<DurableAdRewardCompletionResult> Handle(CompleteAdRewardSessionEndpointCommand request, CancellationToken ct) =>
        await adRewardCompletions.CompleteAsync(request.Request, ct).ConfigureAwait(false);

    public async Task<DurableAdProviderReportImportResult> Handle(ImportAdRewardReportEndpointCommand request, CancellationToken ct) =>
        await adRewardReports.ImportAsync(request.Request, ct).ConfigureAwait(false);

    public async Task<DurableBountyView> Handle(CreateBountyEndpointCommand request, CancellationToken ct) =>
        await bounties.CreateAsync(request.Request, ct).ConfigureAwait(false);

    public async Task<DurableBountyView> Handle(ClaimBountyEndpointCommand request, CancellationToken ct) =>
        await bounties.ClaimAsync(request.Request, ct).ConfigureAwait(false);

    public async Task<DurableBountyView> Handle(ReclaimBountyEndpointCommand request, CancellationToken ct) =>
        await bounties.ReclaimAsync(request.Request, ct).ConfigureAwait(false);

    public async Task<DurableMarketplaceSettlementResult> Handle(SettleMarketplaceOrderEndpointCommand request, CancellationToken ct) =>
        await marketplaceSettlements.SettleAsync(request.Request, ct).ConfigureAwait(false);

    public async Task<DurableMarketplaceRefundResult> Handle(RefundMarketplaceOrderEndpointCommand request, CancellationToken ct) =>
        await marketplaceRefunds.RefundAsync(request.Request, ct).ConfigureAwait(false);

    public async Task<DurableMarketplaceRefundResult> Handle(RefundMarketplaceOrderAdministrationEndpointCommand request, CancellationToken ct) =>
        await marketplaceRefunds.RefundAsync(request.Request, ct).ConfigureAwait(false);
}
