using GameGuild.CQRS;
using Microsoft.EntityFrameworkCore;

namespace GameGuild.Identity.Authentication;

/// <summary>
///     Handler for RevokeResourcePermissionByIdCommand
/// </summary>
public sealed class RevokeResourcePermissionByIdHandler(IApplicationDbContext context)
    : ICommandHandler<RevokeResourcePermissionByIdCommand>
{
    public async Task<Unit> Handle(RevokeResourcePermissionByIdCommand request, CancellationToken cancellationToken)
    {
        var grant = await context.Set<GenericResourcePermission>()
            .FirstOrDefaultAsync(g => g.Id == request.GrantId && g.DeletedAt == null, cancellationToken)
            ?? throw new InvalidOperationException($"Resource permission grant {request.GrantId} not found");

        // Soft delete preserves the permission history for compliance (#360); the
        // grant conveys nothing while deleted (deactivated + DeletedAt) and remains
        // restorable. A never-persisted row (Version == 0) has no history to preserve
        // and is removed instead.
        grant.IsActive = false;
        if (grant.IsNew)
        {
            context.Set<GenericResourcePermission>().Remove(grant);
        }
        else
        {
            grant.SoftDelete();
        }
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return Unit.Value;
    }
}
