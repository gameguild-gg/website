using GameGuild.Assets;
using GameGuild.Social.Posts.Services;
using Microsoft.Extensions.DependencyInjection;

namespace GameGuild.Social.Posts;

/// <summary>
/// Module registration for Post services
/// </summary>
public static class PostsModule
{
    /// <summary>
    /// Adds Post module services to the DI container
    /// </summary>
    public static IServiceCollection AddPostsModule(this IServiceCollection services)
    {
        // Register focused sub-services
        services.AddScoped<IPostCrudService, PostCrudService>();
        services.AddScoped<IPostEngagementService, PostEngagementService>();
        services.AddScoped<IPostCommentService, PostCommentService>();
        services.AddScoped<IPostTagService, PostTagService>();
        services.AddScoped<IPostContentReferenceService, PostContentReferenceService>();

        // Composite service for backward compatibility
        services.AddScoped<IPostService, PostService>();
        services.AddScoped<IAssetUsageGuard, PostAssetUsageGuard>();

        services.AddScoped<IPostAnnouncementService, PostAnnouncementService>();

        return services;
    }
}
