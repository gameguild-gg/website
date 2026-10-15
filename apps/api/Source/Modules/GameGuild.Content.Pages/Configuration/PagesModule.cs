using Microsoft.Extensions.DependencyInjection;

namespace GameGuild.Content.Pages;

/// <summary>
///     Content.Pages module DI registration.
/// </summary>
public static class PagesModule
{
    /// <summary>
    ///     Registers all Content.Pages services in the DI container.
    /// </summary>
    public static IServiceCollection AddContentPagesModule(this IServiceCollection services)
    {
        services.AddScoped<IPageService, PageService>();
        services.AddScoped<IContentResourceService, ContentResourceService>();
        services.AddScoped<IMarketingLeadService, MarketingLeadService>();
        services.AddScoped<ICourseProposalService, CourseProposalService>();
        services.AddScoped<IOpenGraphService, OpenGraphService>();

        return services;
    }
}
