using GameGuild.CQRS;

namespace GameGuild.Learning.Courses;

public sealed record SubmitProgramLifecycleCommand(Guid ProgramId) : ICommand<Program?>;
public sealed record ApproveProgramLifecycleCommand(Guid ProgramId) : ICommand<Program?>;
public sealed record RejectProgramLifecycleCommand(Guid ProgramId, string Reason) : ICommand<Program?>;
public sealed record WithdrawProgramLifecycleCommand(Guid ProgramId) : ICommand<Program?>;
public sealed record ArchiveProgramLifecycleCommand(Guid ProgramId) : ICommand<Program?>;
public sealed record RestoreProgramLifecycleCommand(Guid ProgramId) : ICommand<Program?>;
public sealed record PublishProgramLifecycleCommand(Guid ProgramId) : ICommand<Program>;
public sealed record UnpublishProgramLifecycleCommand(Guid ProgramId) : ICommand<Program>;
public sealed record ScheduleProgramLifecycleCommand(Guid ProgramId, DateTime PublishAt) : ICommand<Program?>;

public sealed class ProgramLifecycleEndpointCommandHandler(IProgramLifecycleService lifecycleService) :
    ICommandHandler<SubmitProgramLifecycleCommand, Program?>,
    ICommandHandler<ApproveProgramLifecycleCommand, Program?>,
    ICommandHandler<RejectProgramLifecycleCommand, Program?>,
    ICommandHandler<WithdrawProgramLifecycleCommand, Program?>,
    ICommandHandler<ArchiveProgramLifecycleCommand, Program?>,
    ICommandHandler<RestoreProgramLifecycleCommand, Program?>,
    ICommandHandler<PublishProgramLifecycleCommand, Program>,
    ICommandHandler<UnpublishProgramLifecycleCommand, Program>,
    ICommandHandler<ScheduleProgramLifecycleCommand, Program?>
{
    public Task<Program?> Handle(SubmitProgramLifecycleCommand request, CancellationToken cancellationToken) =>
        lifecycleService.SubmitProgramAsync(request.ProgramId);

    public Task<Program?> Handle(ApproveProgramLifecycleCommand request, CancellationToken cancellationToken) =>
        lifecycleService.ApproveProgramAsync(request.ProgramId);

    public Task<Program?> Handle(RejectProgramLifecycleCommand request, CancellationToken cancellationToken) =>
        lifecycleService.RejectProgramAsync(request.ProgramId, request.Reason);

    public Task<Program?> Handle(WithdrawProgramLifecycleCommand request, CancellationToken cancellationToken) =>
        lifecycleService.WithdrawProgramAsync(request.ProgramId);

    public Task<Program?> Handle(ArchiveProgramLifecycleCommand request, CancellationToken cancellationToken) =>
        lifecycleService.ArchiveProgramAsync(request.ProgramId);

    public Task<Program?> Handle(RestoreProgramLifecycleCommand request, CancellationToken cancellationToken) =>
        lifecycleService.RestoreProgramAsync(request.ProgramId);

    public Task<Program> Handle(PublishProgramLifecycleCommand request, CancellationToken cancellationToken) =>
        lifecycleService.PublishProgramAsync(request.ProgramId);

    public Task<Program> Handle(UnpublishProgramLifecycleCommand request, CancellationToken cancellationToken) =>
        lifecycleService.UnpublishProgramAsync(request.ProgramId);

    public Task<Program?> Handle(ScheduleProgramLifecycleCommand request, CancellationToken cancellationToken) =>
        lifecycleService.ScheduleProgramAsync(request.ProgramId, request.PublishAt);
}
