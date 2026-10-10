using GameGuild.CQRS;
using GameGuild.Identity.Provisioning.Scim;
using Microsoft.Extensions.Logging;

namespace GameGuild.Identity.Provisioning;

// ==================== SCIM USER COMMANDS ====================

public sealed record ScimCreateUserCommand(ScimUserRequest Request) : ICommand<Result<ScimUserCreateResult>>;

public sealed record ScimReplaceUserCommand(Guid UserId, ScimUserRequest Request) : ICommand<Result<ScimUserResource>>;

public sealed record ScimPatchUserCommand(Guid UserId, ScimPatchRequest Request) : ICommand<Result<ScimUserResource>>;

public sealed record ScimDeleteUserCommand(Guid UserId) : ICommand<Result<bool>>;

/// <summary>Shared authorization prelude for the SCIM user mutation handlers.</summary>
internal static class ScimCommandGuards
{
    public static ScimProvisioningActor RequireWrite(IScimProvisioningContext provisioningContext)
    {
        var actor = provisioningContext.RequireActor();
        ScimScopes.RequireWrite(actor);
        return actor;
    }

    public static ScimProvisioningActor RequireRead(IScimProvisioningContext provisioningContext)
    {
        var actor = provisioningContext.RequireActor();
        ScimScopes.RequireRead(actor);
        return actor;
    }
}

public sealed class ScimCreateUserHandler(
    IScimProvisioningContext provisioningContext,
    ScimUserService userService,
    ILogger<ScimCreateUserHandler> logger)
    : ICommandHandler<ScimCreateUserCommand, Result<ScimUserCreateResult>>
{
    public async Task<Result<ScimUserCreateResult>> Handle(ScimCreateUserCommand command, CancellationToken cancellationToken)
    {
        var actor = ScimCommandGuards.RequireWrite(provisioningContext);
        var result = await userService.CreateAsync(actor, command.Request, cancellationToken).ConfigureAwait(false);
        logger.LogInformation("SCIM user create on tenant {TenantId}: created={Created}", actor.TenantId, result.Created);
        return Result.Success(result);
    }
}

public sealed class ScimReplaceUserHandler(
    IScimProvisioningContext provisioningContext,
    ScimUserService userService)
    : ICommandHandler<ScimReplaceUserCommand, Result<ScimUserResource>>
{
    public async Task<Result<ScimUserResource>> Handle(ScimReplaceUserCommand command, CancellationToken cancellationToken)
    {
        var actor = ScimCommandGuards.RequireWrite(provisioningContext);
        return Result.Success(await userService
            .ReplaceAsync(actor, command.UserId, command.Request, cancellationToken)
            .ConfigureAwait(false));
    }
}

public sealed class ScimPatchUserHandler(
    IScimProvisioningContext provisioningContext,
    ScimUserService userService)
    : ICommandHandler<ScimPatchUserCommand, Result<ScimUserResource>>
{
    public async Task<Result<ScimUserResource>> Handle(ScimPatchUserCommand command, CancellationToken cancellationToken)
    {
        var actor = ScimCommandGuards.RequireWrite(provisioningContext);
        return Result.Success(await userService
            .PatchAsync(actor, command.UserId, command.Request, cancellationToken)
            .ConfigureAwait(false));
    }
}

public sealed class ScimDeleteUserHandler(
    IScimProvisioningContext provisioningContext,
    ScimUserService userService)
    : ICommandHandler<ScimDeleteUserCommand, Result<bool>>
{
    public async Task<Result<bool>> Handle(ScimDeleteUserCommand command, CancellationToken cancellationToken)
    {
        var actor = ScimCommandGuards.RequireWrite(provisioningContext);
        await userService.DeleteAsync(actor, command.UserId, cancellationToken).ConfigureAwait(false);
        return Result.Success(true);
    }
}
