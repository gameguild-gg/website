using System.Reflection;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace GameGuild;

/// <summary>
///     Extension methods for discovering and mapping <see cref="IEndpoint" /> implementations.
/// </summary>
public static class EndpointExtensions
{
    /// <summary>
    ///     Scans the specified <paramref name="assembly" /> for <see cref="IEndpoint" /> implementations
    ///     and registers them as transient services.
    /// </summary>
    public static IServiceCollection AddEndpoints(this IServiceCollection services, Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(assembly);

        var serviceDescriptors = assembly.DefinedTypes.Where(type => type is { IsAbstract: false, IsInterface: false } && type.IsAssignableTo(typeof(IEndpoint)))
            .Select(type => ServiceDescriptor.Transient(typeof(IEndpoint), type))
            .ToArray();

        services.TryAddEnumerable(serviceDescriptors);

        return services;
    }

    /// <summary>
    ///     Maps all registered <see cref="IEndpoint" /> implementations to routes
    ///     on the given <paramref name="app" />.
    /// </summary>
    public static IApplicationBuilder MapEndpoints(this WebApplication app, RouteGroupBuilder? routeGroupBuilder)
    {
        ArgumentNullException.ThrowIfNull(app);

        var endpoints = app.Services.GetRequiredService<IEnumerable<IEndpoint>>();

        IEndpointRouteBuilder builder = routeGroupBuilder is null ? app : routeGroupBuilder;

        foreach (var endpoint in endpoints)
        {
            endpoint.MapEndpoint(builder);
        }

        return app;
    }

    /// <summary>
    ///     Adds an authorization policy requirement for the specified <paramref name="permission" />.
    /// </summary>
    public static RouteHandlerBuilder HasPermission(this RouteHandlerBuilder app, string permission) { return app.RequireAuthorization(permission); }
}
