namespace GameGuild.API.Setup;

internal static class ApiHostLifecycle
{
    public static Task RunAsync(
        WebApplication app,
        IApiProductComposition productComposition,
        bool databaseInitialized,
        IReadOnlyList<string> arguments) =>
        RunAsync(
            app,
            productComposition,
            databaseInitialized,
            arguments,
            configuredApp =>
            {
                configuredApp.ConfigurePipeline();
                return Task.CompletedTask;
            },
            configuredApp => configuredApp.RunAsync());

    internal static async Task RunAsync(
        WebApplication app,
        IApiProductComposition productComposition,
        bool databaseInitialized,
        IReadOnlyList<string> arguments,
        Func<WebApplication, Task> configurePipelineAsync,
        Func<WebApplication, Task> runAsync)
    {
        if (!await productComposition.InitializeAsync(
                app,
                databaseInitialized,
                arguments,
                app.Lifetime.ApplicationStopping)
            .ConfigureAwait(false))
        {
            return;
        }

        await configurePipelineAsync(app).ConfigureAwait(false);
        await runAsync(app).ConfigureAwait(false);
    }
}
