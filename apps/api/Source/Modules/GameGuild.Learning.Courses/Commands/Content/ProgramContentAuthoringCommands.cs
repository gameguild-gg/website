using GameGuild.CQRS;

namespace GameGuild.Learning.Courses;

public sealed record SaveProgramContentDraftCommand(
    Guid ProgramId,
    Guid ContentId,
    int ExpectedRevision,
    AuthoringContentPayload Payload,
    Guid ActorId) : ICommand<AuthoringDraftDto>;

public sealed record PublishProgramContentDraftCommand(
    Guid ProgramId,
    Guid ContentId,
    int ExpectedRevision,
    Guid ActorId) : ICommand<PublishAuthoringResult>;

public sealed record CreateAiAuthoringRunCommand(
    Guid TenantId,
    Guid ActorId,
    Guid ProgramId,
    Guid ContentId,
    AiAuthoringRunRequest Request) : ICommand<AiAuthoringRunDto>;

public sealed record ApplyAiAuthoringProposalCommand(
    Guid TenantId,
    Guid ActorId,
    Guid ProgramId,
    Guid ContentId,
    Guid ProposalId,
    ApplyAiProposalRequest Request) : ICommand<AuthoringDraftDto>;

public sealed record DiscardAiAuthoringProposalCommand(
    Guid TenantId,
    Guid ActorId,
    Guid ProgramId,
    Guid ContentId,
    Guid ProposalId) : ICommand<AiProposalDto>;

public sealed class ProgramContentAuthoringCommandHandler(IProgramContentAuthoringService authoring) :
    ICommandHandler<SaveProgramContentDraftCommand, AuthoringDraftDto>,
    ICommandHandler<PublishProgramContentDraftCommand, PublishAuthoringResult>
{
    public Task<AuthoringDraftDto> Handle(SaveProgramContentDraftCommand request, CancellationToken cancellationToken) =>
        authoring.SaveDraft(
            request.ProgramId,
            request.ContentId,
            request.ExpectedRevision,
            request.Payload,
            request.ActorId,
            cancellationToken);

    public Task<PublishAuthoringResult> Handle(PublishProgramContentDraftCommand request, CancellationToken cancellationToken) =>
        authoring.Publish(
            request.ProgramId,
            request.ContentId,
            request.ExpectedRevision,
            request.ActorId,
            cancellationToken);
}

public sealed class AiAuthoringCommandHandler(IAuthoringAiService authoring) :
    ICommandHandler<CreateAiAuthoringRunCommand, AiAuthoringRunDto>,
    ICommandHandler<ApplyAiAuthoringProposalCommand, AuthoringDraftDto>,
    ICommandHandler<DiscardAiAuthoringProposalCommand, AiProposalDto>
{
    public Task<AiAuthoringRunDto> Handle(CreateAiAuthoringRunCommand request, CancellationToken cancellationToken) =>
        authoring.CreateRun(request.TenantId, request.ActorId, request.ProgramId, request.ContentId, request.Request, cancellationToken);

    public Task<AuthoringDraftDto> Handle(ApplyAiAuthoringProposalCommand request, CancellationToken cancellationToken) =>
        authoring.ApplyProposal(request.TenantId, request.ActorId, request.ProgramId, request.ContentId, request.ProposalId, request.Request, cancellationToken);

    public Task<AiProposalDto> Handle(DiscardAiAuthoringProposalCommand request, CancellationToken cancellationToken) =>
        authoring.DiscardProposal(request.TenantId, request.ActorId, request.ProgramId, request.ContentId, request.ProposalId, cancellationToken);
}
