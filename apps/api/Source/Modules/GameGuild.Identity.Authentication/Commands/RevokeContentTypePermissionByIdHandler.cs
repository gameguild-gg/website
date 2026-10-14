using GameGuild.CQRS;
using Microsoft.EntityFrameworkCore;

namespace GameGuild.Identity.Authentication;

/// <summary>
///     Handler for RevokeContentTypePermissionByIdCommand
/// </summary>
public sealed class RevokeContentTypePermissionByIdHandler(IApplicationDbContext context)
    : ICommandHandler<RevokeContentTypePermissionByIdCommand>
{
    public async Task<Unit> Handle(RevokeContentTypePermissionByIdCommand request, CancellationToken cancellationToken)
    {
        var grant = await context.Set<ContentTypePermission>()
            .FirstOrDefaultAsync(g => g.Id == request.GrantId && g.DeletedAt == null, cancellationToken)
            ?? throw new InvalidOperationException($"Content type permission grant {request.GrantId} not found");

        // Soft delete preserves the permission history for compliance (#360); the
        // grant conveys nothing while deleted (deactivated + DeletedAt) and remains
        // restorable. A never-persisted row (Version == 0) has no history to preserve
        // and is removed instead.
        grant.IsActive = false;
        if (grant.IsNew)
        {
            context.Set<ContentTypePermission>().Remove(grant);
        }
        else
        {
            grant.SoftDelete();
        }
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return Unit.Value;
    }
}
