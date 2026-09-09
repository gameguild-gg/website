using GameGuild.CQRS;

namespace GameGuild.AI;

public sealed record ChatAiCommand(AiChatRequest Request) : ICommand<Result<AiCompletionResponse>>;
public sealed record GenerateAiCommand(AiGenerateRequest Request) : ICommand<Result<AiCompletionResponse>>;
public sealed record CreateAiPromptTemplateCommand(
    Guid TenantId,
    Guid? UserId,
    CreateAiPromptTemplateRequest Request) : ICommand<Result<AiPromptTemplateDto>>;
public sealed record UpdateAiPromptTemplateCommand(
    Guid TenantId,
    Guid TemplateId,
    Guid? UserId,
    UpdateAiPromptTemplateRequest Request) : ICommand<Result<AiPromptTemplateDto>>;
public sealed record DeleteAiPromptTemplateCommand(
    Guid TenantId,
    Guid TemplateId,
    Guid? UserId) : ICommand<Result>;
public sealed record RenderAiPromptTemplateCommand(
    Guid TenantId,
    Guid TemplateId,
    IReadOnlyDictionary<string, string?>? Variables) : ICommand<Result<AiPromptTemplateRenderResponse>>;
public sealed record GenerateFromAiPromptTemplateCommand(
    Guid TenantId,
    Guid TemplateId,
    AiPromptTemplateGenerateRequest Request) : ICommand<Result<AiCompletionResponse>>;

public sealed class AiMutationCommandHandler(
    IAiOrchestrator aiOrchestrator,
    IAiPromptTemplateService promptTemplateService) :
    ICommandHandler<ChatAiCommand, Result<AiCompletionResponse>>,
    ICommandHandler<GenerateAiCommand, Result<AiCompletionResponse>>,
    ICommandHandler<CreateAiPromptTemplateCommand, Result<AiPromptTemplateDto>>,
    ICommandHandler<UpdateAiPromptTemplateCommand, Result<AiPromptTemplateDto>>,
    ICommandHandler<DeleteAiPromptTemplateCommand, Result>,
    ICommandHandler<RenderAiPromptTemplateCommand, Result<AiPromptTemplateRenderResponse>>,
    ICommandHandler<GenerateFromAiPromptTemplateCommand, Result<AiCompletionResponse>>
{
    public Task<Result<AiCompletionResponse>> Handle(ChatAiCommand command, CancellationToken cancellationToken) =>
        aiOrchestrator.ChatAsync(command.Request, cancellationToken);

    public Task<Result<AiCompletionResponse>> Handle(GenerateAiCommand command, CancellationToken cancellationToken) =>
        aiOrchestrator.GenerateAsync(command.Request, cancellationToken);

    public Task<Result<AiPromptTemplateDto>> Handle(
        CreateAiPromptTemplateCommand command,
        CancellationToken cancellationToken) =>
        promptTemplateService.CreateAsync(command.TenantId, command.UserId, command.Request, cancellationToken);

    public Task<Result<AiPromptTemplateDto>> Handle(
        UpdateAiPromptTemplateCommand command,
        CancellationToken cancellationToken) =>
        promptTemplateService.UpdateAsync(
            command.TenantId,
            command.TemplateId,
            command.UserId,
            command.Request,
            cancellationToken);

    public Task<Result> Handle(DeleteAiPromptTemplateCommand command, CancellationToken cancellationToken) =>
        promptTemplateService.DeleteAsync(command.TenantId, command.TemplateId, command.UserId, cancellationToken);

    public Task<Result<AiPromptTemplateRenderResponse>> Handle(
        RenderAiPromptTemplateCommand command,
        CancellationToken cancellationToken) =>
        promptTemplateService.RenderAsync(
            command.TenantId,
            command.TemplateId,
            command.Variables,
            cancellationToken);

    public async Task<Result<AiCompletionResponse>> Handle(
        GenerateFromAiPromptTemplateCommand command,
        CancellationToken cancellationToken)
    {
        var rendered = await promptTemplateService.RenderAsync(
            command.TenantId,
            command.TemplateId,
            command.Request.Variables,
            cancellationToken).ConfigureAwait(false);
        if (rendered.IsFailure)
            return Result.Failure<AiCompletionResponse>(rendered.Error);

        return await aiOrchestrator.GenerateAsync(
            new AiGenerateRequest(
                command.Request.Provider,
                command.Request.Model,
                rendered.Value.SystemPrompt,
                rendered.Value.Prompt,
                command.Request.Temperature,
                command.Request.MaxTokens),
            cancellationToken).ConfigureAwait(false);
    }
}
