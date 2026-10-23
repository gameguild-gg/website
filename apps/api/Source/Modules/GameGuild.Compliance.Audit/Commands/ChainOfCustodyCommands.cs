using GameGuild.CQRS;

namespace GameGuild.Compliance.Audit;

public sealed record PreserveEvidenceCommand(PreserveEvidenceRequest Request) : ICommand<CustodyEvidenceResponse>;

public sealed record ReleaseEvidenceCommand(Guid EvidenceId, string Reason) : ICommand<CustodyEvidenceResponse>;

public sealed record BreakEvidenceSealCommand(Guid EvidenceId, string Purpose) : ICommand<CustodyEvidenceResponse>;

public sealed record RegisterCustodianCommand(RegisterCustodianRequest Request) : ICommand<EvidenceCustodianResponse>;

public sealed record RevokeCustodianCommand(Guid CustodianId, string Reason) : ICommand<EvidenceCustodianResponse>;

public sealed record InitiateEvidenceTransferCommand(InitiateTransferRequest Request) : ICommand<EvidenceTransferResponse>;

public sealed record AcknowledgeEvidenceTransferCommand(Guid TransferId, AcknowledgeTransferRequest Request) : ICommand<EvidenceTransferResponse>;

public sealed record CreateLegalHoldCommand(CreateLegalHoldRequest Request) : ICommand<LegalHoldResponse>;

public sealed record ReleaseLegalHoldCommand(Guid HoldId, string Reason) : ICommand<LegalHoldResponse>;

public sealed record DispatchLegalHoldNotificationsCommand(Guid HoldId) : ICommand<LegalHoldResponse>;

public sealed record BuildCustodyPackageCommand(BuildCustodyPackageRequest Request) : ICommand<CourtAdmissiblePackageResponse>;

public sealed class ChainOfCustodyCommandHandler(
    IChainOfCustodyService custody,
    IEvidenceCustodianService custodians,
    IEvidenceTransferService transfers,
    ILegalHoldService legalHolds,
    ICourtAdmissiblePackagingService packaging) :
    ICommandHandler<PreserveEvidenceCommand, CustodyEvidenceResponse>,
    ICommandHandler<ReleaseEvidenceCommand, CustodyEvidenceResponse>,
    ICommandHandler<BreakEvidenceSealCommand, CustodyEvidenceResponse>,
    ICommandHandler<RegisterCustodianCommand, EvidenceCustodianResponse>,
    ICommandHandler<RevokeCustodianCommand, EvidenceCustodianResponse>,
    ICommandHandler<InitiateEvidenceTransferCommand, EvidenceTransferResponse>,
    ICommandHandler<AcknowledgeEvidenceTransferCommand, EvidenceTransferResponse>,
    ICommandHandler<CreateLegalHoldCommand, LegalHoldResponse>,
    ICommandHandler<ReleaseLegalHoldCommand, LegalHoldResponse>,
    ICommandHandler<DispatchLegalHoldNotificationsCommand, LegalHoldResponse>,
    ICommandHandler<BuildCustodyPackageCommand, CourtAdmissiblePackageResponse>
{
    public Task<CustodyEvidenceResponse> Handle(PreserveEvidenceCommand command, CancellationToken cancellationToken) =>
        custody.PreserveAsync(command.Request, cancellationToken);

    public Task<CustodyEvidenceResponse> Handle(ReleaseEvidenceCommand command, CancellationToken cancellationToken) =>
        custody.ReleaseAsync(command.EvidenceId, command.Reason, cancellationToken);

    public Task<CustodyEvidenceResponse> Handle(BreakEvidenceSealCommand command, CancellationToken cancellationToken) =>
        custody.BreakSealAsync(command.EvidenceId, command.Purpose, cancellationToken);

    public Task<EvidenceCustodianResponse> Handle(RegisterCustodianCommand command, CancellationToken cancellationToken) =>
        custodians.RegisterAsync(command.Request, cancellationToken);

    public Task<EvidenceCustodianResponse> Handle(RevokeCustodianCommand command, CancellationToken cancellationToken) =>
        custodians.RevokeAsync(command.CustodianId, command.Reason, cancellationToken);

    public Task<EvidenceTransferResponse> Handle(InitiateEvidenceTransferCommand command, CancellationToken cancellationToken) =>
        transfers.InitiateAsync(command.Request, cancellationToken);

    public Task<EvidenceTransferResponse> Handle(AcknowledgeEvidenceTransferCommand command, CancellationToken cancellationToken) =>
        transfers.AcknowledgeAsync(command.TransferId, command.Request ?? new AcknowledgeTransferRequest(), cancellationToken);

    public Task<LegalHoldResponse> Handle(CreateLegalHoldCommand command, CancellationToken cancellationToken) =>
        legalHolds.CreateAsync(command.Request, cancellationToken);

    public Task<LegalHoldResponse> Handle(ReleaseLegalHoldCommand command, CancellationToken cancellationToken) =>
        legalHolds.ReleaseAsync(command.HoldId, command.Reason, cancellationToken);

    public Task<LegalHoldResponse> Handle(DispatchLegalHoldNotificationsCommand command, CancellationToken cancellationToken) =>
        legalHolds.DispatchNotificationsAsync(command.HoldId, cancellationToken);

    public Task<CourtAdmissiblePackageResponse> Handle(BuildCustodyPackageCommand command, CancellationToken cancellationToken) =>
        packaging.BuildAsync(command.Request, cancellationToken);
}
