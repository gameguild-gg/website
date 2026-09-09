using GameGuild.Learning.Assessments;
using Microsoft.Extensions.DependencyInjection;

namespace GameGuild.Learning.Lti;

/// <summary>
/// Module registration for the LTI 1.3 tool (launch + AGS score passback).
/// </summary>
public static class LtiModule
{
    public const string HttpClientName = "lti";

    public static IServiceCollection AddLtiModule(this IServiceCollection services)
    {
        services.AddHttpClient(HttpClientName, client => client.Timeout = TimeSpan.FromSeconds(10));
        services.AddSingleton<LtiLaunchStateStore>();
        services.AddScoped<LtiPlatformJwksService>();
        services.AddScoped<ILtiScorePassback, AgsScoreService>();
        return services;
    }
}
