using GameGuild.API.Authorization;
using GameGuild.CQRS;
using GameGuild.Economy.Ledger;
using GameGuild.Economy.Projections;
using GameGuild.Economy.Reserves;
using GameGuild.Economy.Risk;

namespace GameGuild.API.Controllers;

public sealed record ProposeEconomyPolicyEndpointCommand(EconomyCapabilityPolicyProposal Proposal)
    : ICommand<EconomyCapabilityPolicy>;
public sealed record ApproveEconomyPolicyEndpointCommand(
    EconomyStepUpOperation Operation, string StepUpReceipt, Guid PolicyId, Guid ActorId)
    : ICommand<EconomyCapabilityPolicy>;
public sealed record ActivateEconomyKillSwitchEndpointCommand(
    Guid ActivationId, EconomyKillSwitchScope Scope, string Reason, Guid ActorId, DateTimeOffset Now)
    : ICommand<EconomyKillSwitchState>;
public sealed record ProposeEconomyKillSwitchReleaseEndpointCommand(
    EconomyStepUpOperation Operation, string StepUpReceipt, Guid KillSwitchId, Guid ActorId)
    : ICommand<EconomyKillSwitchState>;
public sealed record ApproveEconomyKillSwitchReleaseEndpointCommand(
    EconomyStepUpOperation Operation, string StepUpReceipt, Guid KillSwitchId, Guid ActorId)
    : ICommand<EconomyKillSwitchState>;
public sealed record ReleaseEconomyKillSwitchEndpointCommand(Guid KillSwitchId, DateTimeOffset Now)
    : ICommand<EconomyKillSwitchState>;

public sealed record VerifyEconomyJournalEndpointCommand(string Owner, DateTimeOffset Now, int BatchSize)
    : ICommand<JournalIntegrityRunResult>;
public sealed record PublishEconomyAnchorEndpointCommand(
    DateTimeOffset Now, bool Force, string? DispatchSnapshotHash)
    : ICommand<EconomyAnchorPublicationResult?>;
public sealed record VerifyEconomyAnchorsEndpointCommand(DateTimeOffset Now)
    : ICommand<AnchorVerificationRunResult>;
public sealed record RebuildEconomyProjectionsEndpointCommand(Guid ActorId, DateTimeOffset Now)
    : ICommand<ProjectionGenerationState>;
public sealed record ApproveEconomyProjectionEndpointCommand(
    EconomyStepUpOperation Operation, string StepUpReceipt, long Generation, Guid ActorId)
    : ICommand<ProjectionGenerationState>;

public sealed record IngestEconomyCustodyObservationEndpointCommand(CustodyObservationCommand Command)
    : ICommand<DurableCustodyObservation>;
public sealed record ProposeEconomyReserveEndpointCommand(DurableReserveProposalCommand Command)
    : ICommand<DurableReserveProposalState>;
public sealed record ApproveEconomyReserveEndpointCommand(
    EconomyStepUpOperation Operation, string StepUpReceipt, Guid ProposalId, Guid ActorId)
    : ICommand<ReserveHead>;

public sealed class EconomyControlPlaneEndpointCommandHandler(
    IEconomyCapabilityPolicyStore policies,
    IEconomyKillSwitchStore killSwitches,
    IJournalIntegrityService journal,
    IEconomyAnchorPublisher anchors,
    IEconomyAnchorVerificationService anchorVerification,
    IEconomyProjectionGenerationService projections,
    IEconomyReserveCustodyControlPlane reserves,
    IEconomyStepUpExecutor stepUp,
    TimeProvider timeProvider) :
    ICommandHandler<ProposeEconomyPolicyEndpointCommand, EconomyCapabilityPolicy>,
    ICommandHandler<ApproveEconomyPolicyEndpointCommand, EconomyCapabilityPolicy>,
    ICommandHandler<ActivateEconomyKillSwitchEndpointCommand, EconomyKillSwitchState>,
    ICommandHandler<ProposeEconomyKillSwitchReleaseEndpointCommand, EconomyKillSwitchState>,
    ICommandHandler<ApproveEconomyKillSwitchReleaseEndpointCommand, EconomyKillSwitchState>,
    ICommandHandler<ReleaseEconomyKillSwitchEndpointCommand, EconomyKillSwitchState>,
    ICommandHandler<VerifyEconomyJournalEndpointCommand, JournalIntegrityRunResult>,
    ICommandHandler<PublishEconomyAnchorEndpointCommand, EconomyAnchorPublicationResult?>,
    ICommandHandler<VerifyEconomyAnchorsEndpointCommand, AnchorVerificationRunResult>,
    ICommandHandler<RebuildEconomyProjectionsEndpointCommand, ProjectionGenerationState>,
    ICommandHandler<ApproveEconomyProjectionEndpointCommand, ProjectionGenerationState>,
    ICommandHandler<IngestEconomyCustodyObservationEndpointCommand, DurableCustodyObservation>,
    ICommandHandler<ProposeEconomyReserveEndpointCommand, DurableReserveProposalState>,
    ICommandHandler<ApproveEconomyReserveEndpointCommand, ReserveHead>
{
    public async Task<EconomyCapabilityPolicy> Handle(ProposeEconomyPolicyEndpointCommand request, CancellationToken ct) =>
        await policies.ProposeAsync(request.Proposal, ct).ConfigureAwait(false);

    public async Task<EconomyCapabilityPolicy> Handle(ApproveEconomyPolicyEndpointCommand request, CancellationToken ct) =>
        await stepUp.ExecuteAsync(
            request.Operation,
            request.StepUpReceipt,
            (evidenceHash, token) => policies.ApproveAsync(
                request.PolicyId, request.ActorId, evidenceHash, timeProvider.GetUtcNow(), token).AsTask(),
            ct).ConfigureAwait(false);

    public async Task<EconomyKillSwitchState> Handle(ActivateEconomyKillSwitchEndpointCommand request, CancellationToken ct) =>
        await killSwitches.ActivateAsync(
            request.ActivationId, request.Scope, request.Reason, request.ActorId, request.Now, ct).ConfigureAwait(false);

    public async Task<EconomyKillSwitchState> Handle(ProposeEconomyKillSwitchReleaseEndpointCommand request, CancellationToken ct) =>
        await stepUp.ExecuteAsync(
            request.Operation,
            request.StepUpReceipt,
            (evidenceHash, token) => killSwitches.ProposeReleaseAsync(
                request.KillSwitchId, request.ActorId, evidenceHash, timeProvider.GetUtcNow(), token).AsTask(),
            ct).ConfigureAwait(false);

    public async Task<EconomyKillSwitchState> Handle(ApproveEconomyKillSwitchReleaseEndpointCommand request, CancellationToken ct) =>
        await stepUp.ExecuteAsync(
            request.Operation,
            request.StepUpReceipt,
            (evidenceHash, token) => killSwitches.ApproveReleaseAsync(
                request.KillSwitchId, request.ActorId, evidenceHash, timeProvider.GetUtcNow(), token).AsTask(),
            ct).ConfigureAwait(false);

    public async Task<EconomyKillSwitchState> Handle(ReleaseEconomyKillSwitchEndpointCommand request, CancellationToken ct) =>
        await killSwitches.TryReleaseAsync(request.KillSwitchId, request.Now, ct).ConfigureAwait(false);

    public async Task<JournalIntegrityRunResult> Handle(VerifyEconomyJournalEndpointCommand request, CancellationToken ct) =>
        await journal.RunIncrementAsync(request.Owner, request.Now, request.BatchSize, ct).ConfigureAwait(false);

    public async Task<EconomyAnchorPublicationResult?> Handle(PublishEconomyAnchorEndpointCommand request, CancellationToken ct) =>
        await anchors.PublishIfDueAsync(request.Now, request.Force, request.DispatchSnapshotHash, ct).ConfigureAwait(false);

    public async Task<AnchorVerificationRunResult> Handle(VerifyEconomyAnchorsEndpointCommand request, CancellationToken ct) =>
        await anchorVerification.VerifyPublishedAnchorsAsync(request.Now, ct).ConfigureAwait(false);

    public async Task<ProjectionGenerationState> Handle(RebuildEconomyProjectionsEndpointCommand request, CancellationToken ct) =>
        await projections.RebuildAsync(request.ActorId, request.Now, ct).ConfigureAwait(false);

    public async Task<ProjectionGenerationState> Handle(ApproveEconomyProjectionEndpointCommand request, CancellationToken ct) =>
        await stepUp.ExecuteAsync(
            request.Operation,
            request.StepUpReceipt,
            (evidenceHash, token) => projections.ApproveAndTryActivateAsync(
                request.Generation, request.ActorId, evidenceHash, timeProvider.GetUtcNow(), token).AsTask(),
            ct).ConfigureAwait(false);

    public async Task<DurableCustodyObservation> Handle(IngestEconomyCustodyObservationEndpointCommand request, CancellationToken ct) =>
        await reserves.IngestObservationAsync(request.Command, ct).ConfigureAwait(false);

    public async Task<DurableReserveProposalState> Handle(ProposeEconomyReserveEndpointCommand request, CancellationToken ct) =>
        await reserves.ProposeAsync(request.Command, ct).ConfigureAwait(false);

    public async Task<ReserveHead> Handle(ApproveEconomyReserveEndpointCommand request, CancellationToken ct) =>
        await stepUp.ExecuteAsync(
            request.Operation,
            request.StepUpReceipt,
            (evidenceHash, token) => reserves.ApproveAndActivateAsync(
                request.ProposalId, request.ActorId, evidenceHash, timeProvider.GetUtcNow(), token).AsTask(),
            ct).ConfigureAwait(false);
}
