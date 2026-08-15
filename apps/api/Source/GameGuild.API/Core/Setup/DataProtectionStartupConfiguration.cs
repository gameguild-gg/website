using Microsoft.AspNetCore.DataProtection;

namespace GameGuild.API.Setup;

internal static class DataProtectionStartupConfiguration
{
    public static void Configure(WebApplicationBuilder builder, IApiProductComposition productComposition)
    {
        var keysPath = ResolveKeysPath(
            builder.Configuration,
            productComposition.DefaultDataProtectionKeysPath,
            Environment.GetEnvironmentVariable);
        ConfigureServices(
            builder.Services,
            keysPath,
            productComposition.ApplicationName,
            Console.Error.WriteLine);
    }

    internal static string ResolveKeysPath(
        IConfiguration configuration,
        string defaultPath,
        Func<string, string?> environmentReader) =>
        configuration["DataProtection:KeysPath"]
        ?? environmentReader("DATAPROTECTION_KEYS_PATH")
        ?? defaultPath;

    internal static void ConfigureServices(
        IServiceCollection services,
        string keysPath,
        string applicationName,
        Action<string> writeError)
    {
        try
        {
            Directory.CreateDirectory(keysPath);
            services.AddDataProtection()
                .PersistKeysToFileSystem(new DirectoryInfo(keysPath))
                .SetApplicationName(applicationName);
        }
        catch (Exception exception)
        {
            writeError(
                $"[DataProtection] Failed to configure persistent keys at '{keysPath}': {exception.Message}. Falling back to defaults.");
            services.AddDataProtection().SetApplicationName(applicationName);
        }
    }
}
