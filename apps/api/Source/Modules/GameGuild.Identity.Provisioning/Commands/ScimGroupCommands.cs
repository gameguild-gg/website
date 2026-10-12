using GameGuild.CQRS;
using GameGuild.Identity.Provisioning.Scim;

namespace GameGuild.Identity.Provisioning;

// ==================== SCIM GROUP COMMANDS ====================

public sealed record ScimCreateGroupCommand(ScimGroupRequest Request) : ICommand<Result<ScimGroupCreateResult>>;

public sealed record ScimReplaceGroupCommand(Guid RoleId, ScimGroupRequest Request) : ICommand<Result<ScimGroupResource>>;

public sealed record ScimPatchGroupCommand(Guid RoleId, ScimPatchRequest Request) : ICommand<Result<ScimGroupResource>>;

public sealed record ScimDeleteGroupCommand(Guid RoleId) : ICommand<Result<bool>>;

public sealed class ScimCreateGroupHandler(
    IScimProvisioningContext provisioningContext,
    ScimGroupService groupService)
    : ICommandHandler<ScimCreateGroupCommand, Result<ScimGroupCreateResult>>
{
    public async Task<Result<ScimGroupCreateResult>> Handle(ScimCreateGroupCommand command, CancellationToken cancellationToken)
    {
        var actor = ScimCommandGuards.RequireWrite(provisioningContext);
        return Result.Success(await groupService.CreateAsync(actor, command.Request, cancellationToken).ConfigureAwait(false));
    }
}

public sealed class ScimReplaceGroupHandler(
    IScimProvisioningContext provisioningContext,
    ScimGroupService groupService)
    : ICommandHandler<ScimReplaceGroupCommand, Result<ScimGroupResource>>
{
    public async Task<Result<ScimGroupResource>> Handle(ScimReplaceGroupCommand command, CancellationToken cancellationToken)
    {
        var actor = ScimCommandGuards.RequireWrite(provisioningContext);
        return Result.Success(await groupService
            .ReplaceAsync(actor, command.RoleId, command.Request, cancellationToken)
            .ConfigureAwait(false));
    }
}

public sealed class ScimPatchGroupHandler(
    IScimProvisioningContext provisioningContext,
    ScimGroupService groupService)
    : ICommandHandler<ScimPatchGroupCommand, Result<ScimGroupResource>>
{
    public async Task<Result<ScimGroupResource>> Handle(ScimPatchGroupCommand command, CancellationToken cancellationToken)
    {
        var actor = ScimCommandGuards.RequireWrite(provisioningContext);
        return Result.Success(await groupService
            .PatchAsync(actor, command.RoleId, command.Request, cancellationToken)
            .ConfigureAwait(false));
    }
}

public sealed class ScimDeleteGroupHandler(
    IScimProvisioningContext provisioningContext,
    ScimGroupService groupService)
    : ICommandHandler<ScimDeleteGroupCommand, Result<bool>>
{
    public async Task<Result<bool>> Handle(ScimDeleteGroupCommand command, CancellationToken cancellationToken)
    {
        var actor = ScimCommandGuards.RequireWrite(provisioningContext);
        await groupService.DeleteAsync(actor, command.RoleId, cancellationToken).ConfigureAwait(false);
        return Result.Success(true);
    }
}

// ==================== SCIM GROUP QUERIES ====================

public sealed record ScimGetGroupQuery(Guid RoleId) : IRequest<Result<ScimGroupResource>>;

public sealed record ScimListGroupsQuery(string? Filter, string? StartIndex, string? Count)
    : IRequest<Result<ScimListResponse<ScimGroupResource>>>;

public sealed class ScimGetGroupHandler(
    IScimProvisioningContext provisioningContext,
    ScimGroupService groupService)
    : IRequestHandler<ScimGetGroupQuery, Result<ScimGroupResource>>
{
    public async Task<Result<ScimGroupResource>> Handle(ScimGetGroupQuery request, CancellationToken cancellationToken)
    {
        var actor = ScimCommandGuards.RequireRead(provisioningContext);
        return Result.Success(await groupService.GetAsync(actor, request.RoleId, cancellationToken).ConfigureAwait(false));
    }
}

public sealed class ScimListGroupsHandler(
    IScimProvisioningContext provisioningContext,
    ScimGroupService groupService,
    Microsoft.Extensions.Options.IOptions<ScimProvisioningOptions> options)
    : IRequestHandler<ScimListGroupsQuery, Result<ScimListResponse<ScimGroupResource>>>
{
    public async Task<Result<ScimListResponse<ScimGroupResource>>> Handle(ScimListGroupsQuery request, CancellationToken cancellationToken)
    {
        var actor = ScimCommandGuards.RequireRead(provisioningContext);
        var page = ScimPageRequest.Parse(request.StartIndex, request.Count, options.Value);
        return Result.Success(await groupService
            .ListAsync(actor, request.Filter, page, cancellationToken)
            .ConfigureAwait(false));
    }
}
