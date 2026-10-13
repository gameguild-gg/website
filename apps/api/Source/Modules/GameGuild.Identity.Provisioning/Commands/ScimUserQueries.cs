using GameGuild.CQRS;
using GameGuild.Identity.Provisioning.Scim;

namespace GameGuild.Identity.Provisioning;

// ==================== SCIM USER QUERIES ====================

public sealed record ScimGetUserQuery(Guid UserId) : IRequest<Result<ScimUserResource>>;

public sealed record ScimListUsersQuery(string? Filter, string? StartIndex, string? Count)
    : IRequest<Result<ScimListResponse<ScimUserResource>>>;

public sealed class ScimGetUserHandler(
    IScimProvisioningContext provisioningContext,
    ScimUserService userService)
    : IRequestHandler<ScimGetUserQuery, Result<ScimUserResource>>
{
    public async Task<Result<ScimUserResource>> Handle(ScimGetUserQuery request, CancellationToken cancellationToken)
    {
        var actor = ScimCommandGuards.RequireRead(provisioningContext);
        return Result.Success(await userService.GetAsync(actor, request.UserId, cancellationToken).ConfigureAwait(false));
    }
}

public sealed class ScimListUsersHandler(
    IScimProvisioningContext provisioningContext,
    ScimUserService userService,
    Microsoft.Extensions.Options.IOptions<ScimProvisioningOptions> options)
    : IRequestHandler<ScimListUsersQuery, Result<ScimListResponse<ScimUserResource>>>
{
    public async Task<Result<ScimListResponse<ScimUserResource>>> Handle(ScimListUsersQuery request, CancellationToken cancellationToken)
    {
        var actor = ScimCommandGuards.RequireRead(provisioningContext);
        var page = ScimPageRequest.Parse(request.StartIndex, request.Count, options.Value);
        return Result.Success(await userService
            .ListAsync(actor, request.Filter, page, cancellationToken)
            .ConfigureAwait(false));
    }
}
