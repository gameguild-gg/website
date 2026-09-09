using GameGuild.API.Authorization;
using GameGuild.API.Setup;
using GameGuild.CQRS;
using GameGuild.Economy.Operations;
using GameGuild.Economy.Payouts;
using GameGuild.Economy.Risk;
using GameGuild.Economy.Treasury;

namespace GameGuild.API.Controllers;

public sealed record ProposeTreasuryWithdrawalEndpointCommand(
    EconomyStepUpOperation Operation,
    string StepUpReceipt,
    Guid TenantId,
    Guid ActorId,
    DateOnly PeriodStart,
    long AmountUnits,
    string DestinationHash,
    string IdempotencyKey,
    string TransactionBinding)
    : ICommand<AdminWithdrawalRun>;
public sealed record ApproveTreasuryWithdrawalEndpointCommand(
    EconomyStepUpOperation Operation, string StepUpReceipt, ApproveAdminWithdrawalCommand Command)
    : ICommand<AdminWithdrawalRun>;
public sealed record DispatchTreasuryWithdrawalEndpointCommand(
    EconomyStepUpOperation Operation,
    string StepUpReceipt,
    Guid TenantId,
    Guid ActorId,
    Guid RunId,
    long ExpectedVersion,
    string TransactionBinding)
    : ICommand<AdminWithdrawalRun>;
public sealed record ReconcileTreasuryWithdrawalEndpointCommand(ReconcileAdminWithdrawalCommand Command)
    : ICommand<AdminWithdrawalRun>;

public sealed record CaptureLegacyEconomyEndpointCommand(CaptureLegacyEconomyShadowCommand Command)
    : ICommand<LegacyEconomyShadowBatchView>;
public sealed record BackfillLegacyEconomyEndpointCommand(BackfillLegacyEconomyWalletCommand Command)
    : ICommand<LegacyEconomyShadowBatchView>;
public sealed record ReconcileLegacyEconomyEndpointCommand(ReconcileLegacyEconomyShadowCommand Command)
    : ICommand<LegacyEconomyShadowBatchView>;
public sealed record ProposeLegacyEconomyCutoverEndpointCommand(
    EconomyStepUpOperation Operation, string StepUpReceipt, Guid BatchId, Guid TenantId, Guid ActorId, string Reason)
    : ICommand<LegacyEconomyShadowBatchView>;
public sealed record ApproveLegacyEconomyCutoverEndpointCommand(
    EconomyStepUpOperation Operation, string StepUpReceipt, Guid BatchId, Guid TenantId, Guid ActorId)
    : ICommand<LegacyEconomyShadowBatchView>;
public sealed record RollbackLegacyEconomyCutoverEndpointCommand(
    EconomyStepUpOperation Operation, string StepUpReceipt, Guid BatchId, Guid TenantId, Guid ActorId, string Reason)
    : ICommand<LegacyEconomyShadowBatchView>;

public sealed record CreateOrRefreshPayoutOnboardingEndpointCommand(Guid TenantId, Guid ActorId)
    : ICommand<ConnectOnboardingResult>;
public sealed record ReservePayoutExecutionEndpointCommand(
    EconomyStepUpOperation Operation,
    string StepUpReceipt,
    string TransactionBinding,
    Guid TenantId,
    Guid ActorId,
    Guid RequestId,
    DateTimeOffset Now)
    : ICommand<PayoutOperation>;
public sealed record DispatchPayoutExecutionEndpointCommand(
    EconomyStepUpOperation Operation,
    string StepUpReceipt,
    string TransactionBinding,
    Guid TenantId,
    Guid ActorId,
    Guid OperationId,
    long ExpectedVersion,
    DateTimeOffset Now)
    : ICommand<PayoutOperation>;
public sealed record ReconcilePayoutExecutionEndpointCommand(ReconcilePayoutOperationCommand Command)
    : ICommand<PayoutOperation>;
public sealed record ApplyStripePayoutProviderEventEndpointCommand(PayoutProviderEvent ProviderEvent)
    : ICommand<PayoutOperation>;

public sealed class EconomyFundsEndpointCommandHandler(
    IDurableAdminWithdrawalApplicationService withdrawals,
    ILegacyEconomyShadowMigration legacyMigration,
    IDurablePayoutApplicationService payouts,
    IEconomyStepUpExecutor stepUp,
    TimeProvider timeProvider) :
    ICommandHandler<ProposeTreasuryWithdrawalEndpointCommand, AdminWithdrawalRun>,
    ICommandHandler<ApproveTreasuryWithdrawalEndpointCommand, AdminWithdrawalRun>,
    ICommandHandler<DispatchTreasuryWithdrawalEndpointCommand, AdminWithdrawalRun>,
    ICommandHandler<ReconcileTreasuryWithdrawalEndpointCommand, AdminWithdrawalRun>,
    ICommandHandler<CaptureLegacyEconomyEndpointCommand, LegacyEconomyShadowBatchView>,
    ICommandHandler<BackfillLegacyEconomyEndpointCommand, LegacyEconomyShadowBatchView>,
    ICommandHandler<ReconcileLegacyEconomyEndpointCommand, LegacyEconomyShadowBatchView>,
    ICommandHandler<ProposeLegacyEconomyCutoverEndpointCommand, LegacyEconomyShadowBatchView>,
    ICommandHandler<ApproveLegacyEconomyCutoverEndpointCommand, LegacyEconomyShadowBatchView>,
    ICommandHandler<RollbackLegacyEconomyCutoverEndpointCommand, LegacyEconomyShadowBatchView>,
    ICommandHandler<CreateOrRefreshPayoutOnboardingEndpointCommand, ConnectOnboardingResult>,
    ICommandHandler<ReservePayoutExecutionEndpointCommand, PayoutOperation>,
    ICommandHandler<DispatchPayoutExecutionEndpointCommand, PayoutOperation>,
    ICommandHandler<ReconcilePayoutExecutionEndpointCommand, PayoutOperation>,
    ICommandHandler<ApplyStripePayoutProviderEventEndpointCommand, PayoutOperation>
{
    public async Task<AdminWithdrawalRun> Handle(ProposeTreasuryWithdrawalEndpointCommand request, CancellationToken ct) =>
        await stepUp.ExecuteAsync(
            request.Operation,
            request.StepUpReceipt,
            (evidenceHash, token) => withdrawals.ProposeAsync(
                new ProposeAdminWithdrawalCommand(
                    request.TenantId,
                    request.ActorId,
                    request.PeriodStart,
                    request.AmountUnits,
                    request.DestinationHash,
                    request.IdempotencyKey,
                    Reauthentication(request.ActorId, request.TransactionBinding, evidenceHash)),
                token).AsTask(),
            ct).ConfigureAwait(false);

    public async Task<AdminWithdrawalRun> Handle(ApproveTreasuryWithdrawalEndpointCommand request, CancellationToken ct) =>
        await stepUp.ExecuteAsync(
            request.Operation,
            request.StepUpReceipt,
            (_, token) => withdrawals.ApproveAsync(request.Command, token),
            ct).ConfigureAwait(false);

    public async Task<AdminWithdrawalRun> Handle(DispatchTreasuryWithdrawalEndpointCommand request, CancellationToken ct) =>
        await stepUp.ExecuteAsync(
            request.Operation,
            request.StepUpReceipt,
            (evidenceHash, token) => withdrawals.DispatchAsync(
                new DispatchAdminWithdrawalCommand(
                    request.TenantId,
                    request.ActorId,
                    request.RunId,
                    request.ExpectedVersion,
                    Reauthentication(request.ActorId, request.TransactionBinding, evidenceHash)),
                token),
            ct).ConfigureAwait(false);

    public Task<AdminWithdrawalRun> Handle(ReconcileTreasuryWithdrawalEndpointCommand request, CancellationToken ct) =>
        withdrawals.ReconcileAsync(request.Command, ct);

    public async Task<LegacyEconomyShadowBatchView> Handle(CaptureLegacyEconomyEndpointCommand request, CancellationToken ct) =>
        await legacyMigration.CaptureAsync(request.Command, ct).ConfigureAwait(false);

    public async Task<LegacyEconomyShadowBatchView> Handle(BackfillLegacyEconomyEndpointCommand request, CancellationToken ct) =>
        await legacyMigration.BackfillAsync(request.Command, ct).ConfigureAwait(false);

    public async Task<LegacyEconomyShadowBatchView> Handle(ReconcileLegacyEconomyEndpointCommand request, CancellationToken ct) =>
        await legacyMigration.ReconcileAsync(request.Command, ct).ConfigureAwait(false);

    public async Task<LegacyEconomyShadowBatchView> Handle(ProposeLegacyEconomyCutoverEndpointCommand request, CancellationToken ct) =>
        await stepUp.ExecuteAsync(
            request.Operation,
            request.StepUpReceipt,
            (evidenceHash, token) => legacyMigration.ProposeCutoverAsync(
                new ProposeLegacyEconomyCutoverCommand(
                    request.BatchId, request.TenantId, request.ActorId, request.Reason, evidenceHash, timeProvider.GetUtcNow()), token).AsTask(),
            ct).ConfigureAwait(false);

    public async Task<LegacyEconomyShadowBatchView> Handle(ApproveLegacyEconomyCutoverEndpointCommand request, CancellationToken ct) =>
        await stepUp.ExecuteAsync(
            request.Operation,
            request.StepUpReceipt,
            (evidenceHash, token) => legacyMigration.ApproveCutoverAsync(
                new ApproveLegacyEconomyCutoverCommand(
                    request.BatchId, request.TenantId, request.ActorId, evidenceHash, timeProvider.GetUtcNow()), token).AsTask(),
            ct).ConfigureAwait(false);

    public async Task<LegacyEconomyShadowBatchView> Handle(RollbackLegacyEconomyCutoverEndpointCommand request, CancellationToken ct) =>
        await stepUp.ExecuteAsync(
            request.Operation,
            request.StepUpReceipt,
            (evidenceHash, token) => legacyMigration.RollbackCutoverAsync(
                new RollbackLegacyEconomyCutoverCommand(
                    request.BatchId, request.TenantId, request.ActorId, request.Reason, evidenceHash, timeProvider.GetUtcNow()), token).AsTask(),
            ct).ConfigureAwait(false);

    public async Task<ConnectOnboardingResult> Handle(CreateOrRefreshPayoutOnboardingEndpointCommand request, CancellationToken ct) =>
        await payouts.CreateOrRefreshAccountAsync(request.TenantId, request.ActorId, ct).ConfigureAwait(false);

    public async Task<PayoutOperation> Handle(ReservePayoutExecutionEndpointCommand request, CancellationToken ct) =>
        await stepUp.ExecuteAsync(
            request.Operation,
            request.StepUpReceipt,
            (evidenceHash, token) => payouts.ReserveApprovedAsync(
                new ReserveApprovedPayoutCommand(
                    request.TenantId,
                    request.ActorId,
                    request.RequestId,
                    PayoutReauthentication(request.ActorId, request.TransactionBinding, request.Now, evidenceHash)),
                token).AsTask(),
            ct).ConfigureAwait(false);

    public async Task<PayoutOperation> Handle(DispatchPayoutExecutionEndpointCommand request, CancellationToken ct) =>
        await stepUp.ExecuteAsync(
            request.Operation,
            request.StepUpReceipt,
            (evidenceHash, token) => payouts.DispatchAsync(
                new DispatchPayoutOperationCommand(
                    request.TenantId,
                    request.ActorId,
                    request.OperationId,
                    request.ExpectedVersion,
                    PayoutReauthentication(request.ActorId, request.TransactionBinding, request.Now, evidenceHash)),
                token).AsTask(),
            ct).ConfigureAwait(false);

    public async Task<PayoutOperation> Handle(ReconcilePayoutExecutionEndpointCommand request, CancellationToken ct) =>
        await payouts.ReconcileAsync(request.Command, ct).ConfigureAwait(false);

    public async Task<PayoutOperation> Handle(ApplyStripePayoutProviderEventEndpointCommand request, CancellationToken ct) =>
        await payouts.ApplyProviderEventAsync(request.ProviderEvent, ct).ConfigureAwait(false);

    private ReauthenticationEvidence Reauthentication(Guid actorId, string transactionBinding, string evidenceHash)
    {
        var now = timeProvider.GetUtcNow();
        return new ReauthenticationEvidence(
            actorId,
            ProtectedOperationKind.AdministrativeAdjustment,
            transactionBinding,
            ReauthenticationAssurance.MultiFactor,
            now,
            now.AddMinutes(1),
            evidenceHash);
    }

    private static ReauthenticationEvidence PayoutReauthentication(
        Guid actorId,
        string transactionBinding,
        DateTimeOffset now,
        string evidenceHash) =>
        new(
            actorId,
            ProtectedOperationKind.Payout,
            transactionBinding,
            ReauthenticationAssurance.MultiFactor,
            now,
            now.AddMinutes(1),
            evidenceHash);
}
