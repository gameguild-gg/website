using GameGuild.CQRS;
using GameGuild.Projects;

namespace GameGuild.API.Projects;

public sealed record AddProjectTeamOwnershipEndpointCommand(ProjectTeam Team) : ICommand;
public sealed record UpdateProjectTeamOwnershipEndpointCommand(ProjectTeam Team) : ICommand;
public sealed record RemoveProjectTeamOwnershipEndpointCommand(ProjectTeam Team) : ICommand;
public sealed record TransferProjectOwnerTeamEndpointCommand(Project Project) : ICommand;
public sealed record CreateProjectAllocationEndpointCommand(ProjectMemberAllocation Allocation) : ICommand;
public sealed record UpdateProjectAllocationEndpointCommand(ProjectMemberAllocation Allocation) : ICommand;
public sealed record RemoveProjectAllocationEndpointCommand(ProjectMemberAllocation Allocation) : ICommand;
public sealed record CreateProjectTeamAgreementEndpointCommand(ProjectTeamAgreement Agreement) : ICommand;
public sealed record CounterProjectTeamAgreementEndpointCommand(ProjectTeamAgreement Agreement) : ICommand;
public sealed record AcceptProjectTeamAgreementEndpointCommand(ProjectTeamAgreement Agreement) : ICommand;
public sealed record CancelProjectTeamAgreementEndpointCommand(ProjectTeamAgreement Agreement) : ICommand;
public sealed record CompleteProjectTeamAgreementEndpointCommand(ProjectTeamAgreement Agreement) : ICommand;

public sealed class ProjectOwnershipEndpointCommandHandler(IApplicationDbContext context) :
    ICommandHandler<AddProjectTeamOwnershipEndpointCommand>,
    ICommandHandler<UpdateProjectTeamOwnershipEndpointCommand>,
    ICommandHandler<RemoveProjectTeamOwnershipEndpointCommand>,
    ICommandHandler<TransferProjectOwnerTeamEndpointCommand>,
    ICommandHandler<CreateProjectAllocationEndpointCommand>,
    ICommandHandler<UpdateProjectAllocationEndpointCommand>,
    ICommandHandler<RemoveProjectAllocationEndpointCommand>,
    ICommandHandler<CreateProjectTeamAgreementEndpointCommand>,
    ICommandHandler<CounterProjectTeamAgreementEndpointCommand>,
    ICommandHandler<AcceptProjectTeamAgreementEndpointCommand>,
    ICommandHandler<CancelProjectTeamAgreementEndpointCommand>,
    ICommandHandler<CompleteProjectTeamAgreementEndpointCommand>
{
    public Task<Unit> Handle(AddProjectTeamOwnershipEndpointCommand request, CancellationToken ct)
    {
        context.Set<ProjectTeam>().Add(request.Team);
        return SaveAsync(ct);
    }

    public Task<Unit> Handle(UpdateProjectTeamOwnershipEndpointCommand request, CancellationToken ct) => SaveAsync(ct);
    public Task<Unit> Handle(RemoveProjectTeamOwnershipEndpointCommand request, CancellationToken ct) => SaveAsync(ct);
    public Task<Unit> Handle(TransferProjectOwnerTeamEndpointCommand request, CancellationToken ct) => SaveAsync(ct);

    public Task<Unit> Handle(CreateProjectAllocationEndpointCommand request, CancellationToken ct)
    {
        context.Set<ProjectMemberAllocation>().Add(request.Allocation);
        return SaveAsync(ct);
    }

    public Task<Unit> Handle(UpdateProjectAllocationEndpointCommand request, CancellationToken ct) => SaveAsync(ct);
    public Task<Unit> Handle(RemoveProjectAllocationEndpointCommand request, CancellationToken ct) => SaveAsync(ct);

    public Task<Unit> Handle(CreateProjectTeamAgreementEndpointCommand request, CancellationToken ct)
    {
        context.Set<ProjectTeamAgreement>().Add(request.Agreement);
        return SaveAsync(ct);
    }

    public Task<Unit> Handle(CounterProjectTeamAgreementEndpointCommand request, CancellationToken ct) => SaveAsync(ct);
    public Task<Unit> Handle(AcceptProjectTeamAgreementEndpointCommand request, CancellationToken ct) => SaveAsync(ct);
    public Task<Unit> Handle(CancelProjectTeamAgreementEndpointCommand request, CancellationToken ct) => SaveAsync(ct);
    public Task<Unit> Handle(CompleteProjectTeamAgreementEndpointCommand request, CancellationToken ct) => SaveAsync(ct);

    private async Task<Unit> SaveAsync(CancellationToken ct)
    {
        await context.SaveChangesAsync(ct).ConfigureAwait(false);
        return Unit.Value;
    }
}
