using GameGuild.CQRS;

namespace GameGuild.Learning.Cohorts;

public sealed record CreateCohortCommand(CreateCohortRequest Request) : ICommand<Result<Cohort>>;
public sealed record UpdateCohortCommand(Guid CohortId, UpdateCohortRequest Request) : ICommand<Result<Cohort>>;
public sealed record OpenCohortCommand(Guid CohortId) : ICommand<Result<Cohort>>;
public sealed record CloseCohortCommand(Guid CohortId) : ICommand<Result<Cohort>>;
public sealed record CompleteCohortCommand(Guid CohortId) : ICommand<Result<Cohort>>;
public sealed record CancelCohortCommand(Guid CohortId) : ICommand<Result<Cohort>>;
public sealed record DeleteCohortCommand(Guid CohortId) : ICommand<Result>;

public sealed class CohortEndpointCommandHandler(ICohortService service) :
    ICommandHandler<CreateCohortCommand, Result<Cohort>>,
    ICommandHandler<UpdateCohortCommand, Result<Cohort>>,
    ICommandHandler<OpenCohortCommand, Result<Cohort>>,
    ICommandHandler<CloseCohortCommand, Result<Cohort>>,
    ICommandHandler<CompleteCohortCommand, Result<Cohort>>,
    ICommandHandler<CancelCohortCommand, Result<Cohort>>,
    ICommandHandler<DeleteCohortCommand, Result>
{
    public Task<Result<Cohort>> Handle(CreateCohortCommand request, CancellationToken cancellationToken) =>
        service.CreateCohortAsync(request.Request);

    public Task<Result<Cohort>> Handle(UpdateCohortCommand request, CancellationToken cancellationToken) =>
        service.UpdateCohortAsync(request.CohortId, request.Request);

    public Task<Result<Cohort>> Handle(OpenCohortCommand request, CancellationToken cancellationToken) =>
        service.OpenCohortAsync(request.CohortId);

    public Task<Result<Cohort>> Handle(CloseCohortCommand request, CancellationToken cancellationToken) =>
        service.CloseCohortAsync(request.CohortId);

    public Task<Result<Cohort>> Handle(CompleteCohortCommand request, CancellationToken cancellationToken) =>
        service.CompleteCohortAsync(request.CohortId);

    public Task<Result<Cohort>> Handle(CancelCohortCommand request, CancellationToken cancellationToken) =>
        service.CancelCohortAsync(request.CohortId);

    public Task<Result> Handle(DeleteCohortCommand request, CancellationToken cancellationToken) =>
        service.DeleteCohortAsync(request.CohortId);
}
