using System.Security.Claims;
using GameGuild.Identity.Authorization;
using HotChocolate;
using HotChocolate.Resolvers;
using HotChocolate.Types;

namespace GameGuild.Projects;

[ExtendObjectType(typeof(Project))]
public sealed class ProjectPermissionsResolvers {
  public async Task<bool> CanEdit([Service] IProjectAuthorizationService authorizationService, ClaimsPrincipal user, [Parent] Project project, IResolverContext resolverContext) {
    ArgumentNullException.ThrowIfNull(authorizationService);
    ArgumentNullException.ThrowIfNull(project);

    if (user?.Identity?.IsAuthenticated != true) { return false; }

    return await ProjectGraphQLPermissionCache.HasPermissionAsync(
      resolverContext,
      authorizationService,
      project.Id,
      PermissionType.Edit,
      resolverContext.RequestAborted).ConfigureAwait(false);
  }

  public async Task<bool> CanDelete([Service] IProjectAuthorizationService authorizationService, ClaimsPrincipal user, [Parent] Project project, IResolverContext resolverContext) {
    ArgumentNullException.ThrowIfNull(authorizationService);
    ArgumentNullException.ThrowIfNull(project);

    if (user?.Identity?.IsAuthenticated != true) { return false; }

    return await ProjectGraphQLPermissionCache.HasPermissionAsync(
      resolverContext,
      authorizationService,
      project.Id,
      PermissionType.Delete,
      resolverContext.RequestAborted).ConfigureAwait(false);
  }
}
