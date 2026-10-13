using GameGuild.CQRS;

namespace GameGuild.Compliance.Audit;

public sealed record CreatePiiRedactionRuleCommand(UpsertPiiRedactionRuleRequest Request) : ICommand<PiiRedactionRuleResponse>;

public sealed record UpdatePiiRedactionRuleCommand(Guid RuleId, int ExpectedRevision, UpsertPiiRedactionRuleRequest Request) : ICommand<PiiRedactionRuleResponse>;

public sealed record DeletePiiRedactionRuleCommand(Guid RuleId, int ExpectedRevision) : ICommand<bool>;

public sealed record TestPiiRedactionPipelineCommand(PiiRedactionTestRunRequest Request) : ICommand<PiiRedactionTestRunResponse>;

public sealed record ResolvePiiTokenCommand(string Token, string Reason) : ICommand<PiiTokenResolutionResponse>;

public sealed class PiiRedactionPipelineCommandHandler(IPiiRedactionAdminService service) :
    ICommandHandler<CreatePiiRedactionRuleCommand, PiiRedactionRuleResponse>,
    ICommandHandler<UpdatePiiRedactionRuleCommand, PiiRedactionRuleResponse>,
    ICommandHandler<DeletePiiRedactionRuleCommand, bool>,
    ICommandHandler<TestPiiRedactionPipelineCommand, PiiRedactionTestRunResponse>,
    ICommandHandler<ResolvePiiTokenCommand, PiiTokenResolutionResponse>
{
    public Task<PiiRedactionRuleResponse> Handle(CreatePiiRedactionRuleCommand command, CancellationToken cancellationToken) =>
        service.CreateRuleAsync(command.Request, cancellationToken);

    public Task<PiiRedactionRuleResponse> Handle(UpdatePiiRedactionRuleCommand command, CancellationToken cancellationToken) =>
        service.UpdateRuleAsync(command.RuleId, command.ExpectedRevision, command.Request, cancellationToken);

    public async Task<bool> Handle(DeletePiiRedactionRuleCommand command, CancellationToken cancellationToken)
    {
        await service.DeleteRuleAsync(command.RuleId, command.ExpectedRevision, cancellationToken).ConfigureAwait(false);
        return true;
    }

    public Task<PiiRedactionTestRunResponse> Handle(TestPiiRedactionPipelineCommand command, CancellationToken cancellationToken) =>
        service.TestAsync(command.Request, cancellationToken);

    public Task<PiiTokenResolutionResponse> Handle(ResolvePiiTokenCommand command, CancellationToken cancellationToken) =>
        service.ResolveTokenAsync(command.Token, command.Reason, cancellationToken);
}
