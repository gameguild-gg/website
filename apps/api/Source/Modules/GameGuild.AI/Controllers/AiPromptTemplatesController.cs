using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using GameGuild.CQRS;

namespace GameGuild.AI;

[ApiVersion("1.0")]
[Route("v{version:apiVersion}/ai/prompt-templates")]
[Microsoft.AspNetCore.Http.Tags("ai/prompt-templates")]
[Authorize]
public sealed class AiPromptTemplatesController(
    ISender sender,
    IAiPromptTemplateService promptTemplateService,
    IRequestContextAccessor requestContextAccessor) : BaseApiController
{
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<AiPromptTemplateDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IReadOnlyList<AiPromptTemplateDto>>> List(
        [FromQuery] string? category = null,
        [FromQuery] bool includeInactive = false,
        CancellationToken cancellationToken = default)
    {
        if (!requestContextAccessor.CurrentTenantId.HasValue)
            return Forbid();

        var result = await promptTemplateService
            .ListAsync(requestContextAccessor.CurrentTenantId.Value, category, includeInactive, cancellationToken)
            .ConfigureAwait(false);

        return ToActionResult(result);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType<AiPromptTemplateDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AiPromptTemplateDto>> Get(Guid id, CancellationToken cancellationToken = default)
    {
        if (!requestContextAccessor.CurrentTenantId.HasValue)
            return Forbid();

        var result = await promptTemplateService
            .GetAsync(requestContextAccessor.CurrentTenantId.Value, id, cancellationToken)
            .ConfigureAwait(false);

        return ToActionResult(result);
    }

    [HttpPost]
    [ProducesResponseType<AiPromptTemplateDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<AiPromptTemplateDto>> Create(
        [FromBody] CreateAiPromptTemplateRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!requestContextAccessor.CurrentTenantId.HasValue)
            return Forbid();

        var result = await sender.Send(
            new CreateAiPromptTemplateCommand(
                requestContextAccessor.CurrentTenantId.Value,
                requestContextAccessor.CurrentUserId,
                request),
                cancellationToken)
            .ConfigureAwait(false);

        return ToCreatedResult(result);
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType<AiPromptTemplateDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AiPromptTemplateDto>> Update(
        Guid id,
        [FromBody] UpdateAiPromptTemplateRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!requestContextAccessor.CurrentTenantId.HasValue)
            return Forbid();

        var result = await sender.Send(
            new UpdateAiPromptTemplateCommand(
                requestContextAccessor.CurrentTenantId.Value,
                id,
                requestContextAccessor.CurrentUserId,
                request),
                cancellationToken)
            .ConfigureAwait(false);

        return ToActionResult(result);
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken = default)
    {
        if (!requestContextAccessor.CurrentTenantId.HasValue)
            return Forbid();

        var result = await sender.Send(
            new DeleteAiPromptTemplateCommand(
                requestContextAccessor.CurrentTenantId.Value,
                id,
                requestContextAccessor.CurrentUserId),
                cancellationToken)
            .ConfigureAwait(false);

        return ToActionResult(result);
    }

    [HttpPost("{id:guid}/render")]
    [ProducesResponseType<AiPromptTemplateRenderResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AiPromptTemplateRenderResponse>> Render(
        Guid id,
        [FromBody] AiPromptTemplateRenderRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!requestContextAccessor.CurrentTenantId.HasValue)
            return Forbid();

        var result = await sender.Send(
            new RenderAiPromptTemplateCommand(
                requestContextAccessor.CurrentTenantId.Value,
                id,
                request.Variables),
            cancellationToken)
            .ConfigureAwait(false);

        return ToActionResult(result);
    }

    [HttpPost("{id:guid}/generate")]
    [ProducesResponseType<AiCompletionResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AiCompletionResponse>> Generate(
        Guid id,
        [FromBody] AiPromptTemplateGenerateRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!requestContextAccessor.CurrentTenantId.HasValue)
            return Forbid();

        var completion = await sender.Send(
                new GenerateFromAiPromptTemplateCommand(
                    requestContextAccessor.CurrentTenantId.Value,
                    id,
                    request),
                cancellationToken)
            .ConfigureAwait(false);

        return ToActionResult(completion);
    }
}
