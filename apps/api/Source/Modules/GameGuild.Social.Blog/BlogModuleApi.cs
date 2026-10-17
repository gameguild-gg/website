using GameGuild.CQRS;
using GameGuild.Social.Blog.Authoring;
using GameGuild.Social.Blog.Commands;
using GameGuild.Social.Blog.Queries;
using GameGuild.Social.Blog.Services;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace GameGuild.Social.Blog;

public static class BlogDependencyInjection
{
    public static IServiceCollection AddSocialBlogModule(this IServiceCollection services)
    {
        services.AddScoped<IBlogSlugService, BlogSlugService>();
        services.AddScoped<IPublicationAnnouncer, PublicationAnnouncerAdapter>();
        services.AddScoped<BlogAssetManifestService>();
        services.AddScoped<IBlogPostService>(provider => ActivatorUtilities.CreateInstance<BlogPostService>(provider));
        services.AddScoped<IBlogViewCounterService, BlogViewCounterService>();
        services.AddScoped<GameGuild.Assets.IAssetUsageGuard, Services.BlogAssetUsageGuard>();

        services.AddSingleton<IBlogAuthoringAiRunQueue, BlogAuthoringAiRunQueue>();
        services.AddScoped<IBlogAuthoringAiService, BlogAuthoringAiService>();
        services.AddHostedService<BlogAuthoringAiBackgroundService>();
        services.AddScoped<ICommandHandler<CreateBlogAiRunCommand, BlogAiRunDto>, BlogAiCommandHandler>();
        services.AddScoped<IRequestHandler<CreateBlogAiRunCommand, BlogAiRunDto>>(sp => sp.GetRequiredService<ICommandHandler<CreateBlogAiRunCommand, BlogAiRunDto>>());
        services.AddScoped<ICommandHandler<CancelBlogAiRunCommand, BlogAiRunDto>, BlogAiCommandHandler>();
        services.AddScoped<IRequestHandler<CancelBlogAiRunCommand, BlogAiRunDto>>(sp => sp.GetRequiredService<ICommandHandler<CancelBlogAiRunCommand, BlogAiRunDto>>());
        services.AddScoped<ICommandHandler<ApplyBlogAiProposalCommand, BlogPostDto>, BlogAiCommandHandler>();
        services.AddScoped<IRequestHandler<ApplyBlogAiProposalCommand, BlogPostDto>>(sp => sp.GetRequiredService<ICommandHandler<ApplyBlogAiProposalCommand, BlogPostDto>>());
        services.AddScoped<ICommandHandler<DiscardBlogAiProposalCommand, BlogAiProposalDto>, BlogAiCommandHandler>();
        services.AddScoped<IRequestHandler<DiscardBlogAiProposalCommand, BlogAiProposalDto>>(sp => sp.GetRequiredService<ICommandHandler<DiscardBlogAiProposalCommand, BlogAiProposalDto>>());

        services.AddScoped<ICommandHandler<CreateBlogPostCommand, BlogPost>, CreateBlogPostCommandHandler>();
        services.AddScoped<IRequestHandler<CreateBlogPostCommand, BlogPost>>(sp => sp.GetRequiredService<ICommandHandler<CreateBlogPostCommand, BlogPost>>());
        services.AddScoped<ICommandHandler<UpdateBlogPostDraftCommand, BlogPost>, UpdateBlogPostDraftCommandHandler>();
        services.AddScoped<IRequestHandler<UpdateBlogPostDraftCommand, BlogPost>>(sp => sp.GetRequiredService<ICommandHandler<UpdateBlogPostDraftCommand, BlogPost>>());
        services.AddScoped<ICommandHandler<ChangeBlogPostSlugCommand, BlogPost>, ChangeBlogPostSlugCommandHandler>();
        services.AddScoped<IRequestHandler<ChangeBlogPostSlugCommand, BlogPost>>(sp => sp.GetRequiredService<ICommandHandler<ChangeBlogPostSlugCommand, BlogPost>>());
        services.AddScoped<ICommandHandler<AddBlogPostCoauthorCommand, Unit>, AddBlogPostCoauthorCommandHandler>();
        services.AddScoped<IRequestHandler<AddBlogPostCoauthorCommand, Unit>>(sp => sp.GetRequiredService<ICommandHandler<AddBlogPostCoauthorCommand, Unit>>());
        services.AddScoped<ICommandHandler<RemoveBlogPostCoauthorCommand, Unit>, RemoveBlogPostCoauthorCommandHandler>();
        services.AddScoped<IRequestHandler<RemoveBlogPostCoauthorCommand, Unit>>(sp => sp.GetRequiredService<ICommandHandler<RemoveBlogPostCoauthorCommand, Unit>>());
        services.AddScoped<ICommandHandler<TransferBlogPrimaryCommand, BlogPost>, TransferBlogPrimaryCommandHandler>();
        services.AddScoped<IRequestHandler<TransferBlogPrimaryCommand, BlogPost>>(sp => sp.GetRequiredService<ICommandHandler<TransferBlogPrimaryCommand, BlogPost>>());
        services.AddScoped<ICommandHandler<PublishBlogPostCommand, BlogPost>, PublishBlogPostCommandHandler>();
        services.AddScoped<IRequestHandler<PublishBlogPostCommand, BlogPost>>(sp => sp.GetRequiredService<ICommandHandler<PublishBlogPostCommand, BlogPost>>());
        services.AddScoped<ICommandHandler<UnpublishBlogPostCommand, BlogPost>, UnpublishBlogPostCommandHandler>();
        services.AddScoped<IRequestHandler<UnpublishBlogPostCommand, BlogPost>>(sp => sp.GetRequiredService<ICommandHandler<UnpublishBlogPostCommand, BlogPost>>());
        services.AddScoped<ICommandHandler<DeleteBlogPostCommand, Unit>, DeleteBlogPostCommandHandler>();
        services.AddScoped<IRequestHandler<DeleteBlogPostCommand, Unit>>(sp => sp.GetRequiredService<ICommandHandler<DeleteBlogPostCommand, Unit>>());
        services.AddScoped<ICommandHandler<AddBlogCommentCommand, BlogComment>, AddBlogCommentCommandHandler>();
        services.AddScoped<IRequestHandler<AddBlogCommentCommand, BlogComment>>(sp => sp.GetRequiredService<ICommandHandler<AddBlogCommentCommand, BlogComment>>());
        services.AddScoped<ICommandHandler<DeleteBlogCommentCommand, Unit>, DeleteBlogCommentCommandHandler>();
        services.AddScoped<IRequestHandler<DeleteBlogCommentCommand, Unit>>(sp => sp.GetRequiredService<ICommandHandler<DeleteBlogCommentCommand, Unit>>());

        services.AddScoped<IQueryHandler<GetBlogPostForAuthorQuery, BlogPost?>, GetBlogPostForAuthorQueryHandler>();
        services.AddScoped<IRequestHandler<GetBlogPostForAuthorQuery, BlogPost?>>(sp => sp.GetRequiredService<IQueryHandler<GetBlogPostForAuthorQuery, BlogPost?>>());
        services.AddScoped<IQueryHandler<ListMyBlogPostsQuery, IReadOnlyList<BlogPost>>, ListMyBlogPostsQueryHandler>();
        services.AddScoped<IRequestHandler<ListMyBlogPostsQuery, IReadOnlyList<BlogPost>>>(sp => sp.GetRequiredService<IQueryHandler<ListMyBlogPostsQuery, IReadOnlyList<BlogPost>>>());
        services.AddScoped<IQueryHandler<ResolveBlogRouteQuery, BlogPost?>, ResolveBlogRouteQueryHandler>();
        services.AddScoped<IRequestHandler<ResolveBlogRouteQuery, BlogPost?>>(sp => sp.GetRequiredService<IQueryHandler<ResolveBlogRouteQuery, BlogPost?>>());
        services.AddScoped<IQueryHandler<GetBlogAuthorPublicQuery, IReadOnlyList<BlogPost>>, GetBlogAuthorPublicQueryHandler>();
        services.AddScoped<IRequestHandler<GetBlogAuthorPublicQuery, IReadOnlyList<BlogPost>>>(sp => sp.GetRequiredService<IQueryHandler<GetBlogAuthorPublicQuery, IReadOnlyList<BlogPost>>>());
        services.AddScoped<IQueryHandler<GetBlogPostPublicQuery, BlogPost?>, GetBlogPostPublicQueryHandler>();
        services.AddScoped<IRequestHandler<GetBlogPostPublicQuery, BlogPost?>>(sp => sp.GetRequiredService<IQueryHandler<GetBlogPostPublicQuery, BlogPost?>>());
        services.AddScoped<IQueryHandler<ListBlogIndexQuery, IReadOnlyList<BlogPost>>, ListBlogIndexQueryHandler>();
        services.AddScoped<IRequestHandler<ListBlogIndexQuery, IReadOnlyList<BlogPost>>>(sp => sp.GetRequiredService<IQueryHandler<ListBlogIndexQuery, IReadOnlyList<BlogPost>>>());

        services.AddScoped<IQueryHandler<ListPublicBlogSummariesQuery, BlogPostSummaryPage>, ListPublicBlogSummariesQueryHandler>();
        services.AddScoped<IRequestHandler<ListPublicBlogSummariesQuery, BlogPostSummaryPage>>(sp => sp.GetRequiredService<IQueryHandler<ListPublicBlogSummariesQuery, BlogPostSummaryPage>>());
        services.AddScoped<IQueryHandler<ListAuthorBlogSummariesQuery, BlogPostSummaryPage>, ListAuthorBlogSummariesQueryHandler>();
        services.AddScoped<IRequestHandler<ListAuthorBlogSummariesQuery, BlogPostSummaryPage>>(sp => sp.GetRequiredService<IQueryHandler<ListAuthorBlogSummariesQuery, BlogPostSummaryPage>>());
        services.AddScoped<IQueryHandler<GetPublicBlogPostDetailQuery, BlogPostDetailDto?>, GetPublicBlogPostDetailQueryHandler>();
        services.AddScoped<IRequestHandler<GetPublicBlogPostDetailQuery, BlogPostDetailDto?>>(sp => sp.GetRequiredService<IQueryHandler<GetPublicBlogPostDetailQuery, BlogPostDetailDto?>>());
        services.AddScoped<IQueryHandler<ListBlogCommentsQuery, BlogCommentPage>, ListBlogCommentsQueryHandler>();
        services.AddScoped<IRequestHandler<ListBlogCommentsQuery, BlogCommentPage>>(sp => sp.GetRequiredService<IQueryHandler<ListBlogCommentsQuery, BlogCommentPage>>());
        services.AddScoped<IQueryHandler<ResolveBlogRouteForRedirectQuery, BlogRouteResolutionDto?>, ResolveBlogRouteForRedirectQueryHandler>();
        services.AddScoped<IRequestHandler<ResolveBlogRouteForRedirectQuery, BlogRouteResolutionDto?>>(sp => sp.GetRequiredService<IQueryHandler<ResolveBlogRouteForRedirectQuery, BlogRouteResolutionDto?>>());
        services.AddScoped<IQueryHandler<IsBlogPostPublishedQuery, bool>, IsBlogPostPublishedQueryHandler>();
        services.AddScoped<IRequestHandler<IsBlogPostPublishedQuery, bool>>(sp => sp.GetRequiredService<IQueryHandler<IsBlogPostPublishedQuery, bool>>());

        return services;
    }
}

public sealed class SocialBlogModule : ModuleBase
{
    public override string Name => "Social.Blog";
    public override int Order => 163;

    public override IServiceCollection ConfigureServices(IServiceCollection services, IConfiguration configuration)
        => services.AddSocialBlogModule();

    public override IEndpointRouteBuilder MapEndpoints(IEndpointRouteBuilder endpoints) => endpoints;
}
