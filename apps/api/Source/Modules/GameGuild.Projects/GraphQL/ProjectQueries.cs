using GameGuild.CQRS;
using GameGuild.Identity.Authorization;
using GameGuild.Identity.Authorization.GraphQL;
using HotChocolate;
using HotChocolate.Types;

namespace GameGuild.Projects;

/// <summary> GraphQL queries for Project module using CQRS pattern </summary>
[ExtendObjectType("Query")]
public class ProjectQueries
{
    private static T UnwrapResult<T>(Result<T> result) =>
        result.IsSuccess ? result.Value : throw new GraphQLException(result.Error.Description);

    /// <summary> Gets all projects accessible to the current user </summary>
    public async Task<IEnumerable<Project>> Projects([Service] IMediator mediator)
    {
        var result = await mediator.Send(new GetAllProjectsQuery()).ConfigureAwait(false);
        return UnwrapResult(result);
    }

    /// <summary> Gets a project by its unique identifier </summary>
    [RequireGraphQLProjectPermission(PermissionType.Read, ResourceIdArgumentName = "id")]
    public async Task<Project?> ProjectById(Guid id, [Service] IMediator mediator)
    {
        var result = await mediator.Send(new GetProjectByIdQuery { ProjectId = id }).ConfigureAwait(false);
        return UnwrapResult(result);
    }

    /// <summary> Gets a project by its slug </summary>
    public async Task<Project?> GetProjectBySlug(string slug, [Service] IMediator mediator)
    {
        var result = await mediator.Send(new GetProjectBySlugQuery { Slug = slug }).ConfigureAwait(false);
        return UnwrapResult(result);
    }

    /// <summary> Gets projects by category </summary>
    public async Task<IEnumerable<Project>> GetProjectsByCategory(Guid categoryId, [Service] IMediator mediator)
    {
        var result = await mediator.Send(new GetProjectsByCategoryQuery { CategoryId = categoryId }).ConfigureAwait(false);
        return UnwrapResult(result);
    }

    /// <summary> Gets projects by creator </summary>
    public async Task<IEnumerable<Project>> GetProjectsByCreator(Guid creatorId, [Service] IMediator mediator)
    {
        var result = await mediator.Send(new GetProjectsByCreatorQuery { CreatorId = creatorId }).ConfigureAwait(false);
        return UnwrapResult(result);
    }

    /// <summary> Gets projects by status </summary>
    public async Task<IEnumerable<Project>> GetProjectsByStatus(ContentStatus status, [Service] IMediator mediator)
    {
        var result = await mediator.Send(new GetProjectsByStatusQuery { Status = status }).ConfigureAwait(false);
        return UnwrapResult(result);
    }

    /// <summary>
    ///     Gets deleted projects (admin only). The general @authorize policy requirement
    ///     is enforced by the GraphQL authorization field middleware (issue #335).
    /// </summary>
    [GraphQLAuthorize(Policy = "RequireAdminRole")]
    public async Task<IEnumerable<Project>> GetDeletedProjects([Service] IMediator mediator)
    {
        var result = await mediator.Send(new GetDeletedProjectsQuery()).ConfigureAwait(false);
        return UnwrapResult(result);
    }

    /// <summary> Search projects </summary>
    public async Task<IEnumerable<Project>> SearchProjects(string searchTerm, [Service] IMediator mediator,
        ProjectType? type = null, Guid? categoryId = null, ContentStatus? status = null, int skip = 0, int take = 50)
    {
        var query = new SearchProjectsQuery { SearchTerm = searchTerm, Type = type, CategoryId = categoryId, Status = status, Skip = skip, Take = take };
        var result = await mediator.Send(query).ConfigureAwait(false);
        return UnwrapResult(result);
    }

    /// <summary> Gets popular projects </summary>
    public async Task<IEnumerable<Project>> GetPopularProjects([Service] IMediator mediator, ProjectType? type = null, int take = 10)
    {
        var result = await mediator.Send(new GetPopularProjectsQuery { Type = type, Take = take }).ConfigureAwait(false);
        return UnwrapResult(result);
    }

    /// <summary> Gets recent projects </summary>
    public async Task<IEnumerable<Project>> GetRecentProjects([Service] IMediator mediator, ProjectType? type = null, int take = 10)
    {
        var result = await mediator.Send(new GetRecentProjectsQuery { Type = type, Take = take }).ConfigureAwait(false);
        return UnwrapResult(result);
    }

    /// <summary> Gets featured projects </summary>
    public async Task<IEnumerable<Project>> GetFeaturedProjects([Service] IMediator mediator, ProjectType? type = null, int take = 10)
    {
        var result = await mediator.Send(new GetFeaturedProjectsQuery { Type = type, Take = take }).ConfigureAwait(false);
        return UnwrapResult(result);
    }
}
