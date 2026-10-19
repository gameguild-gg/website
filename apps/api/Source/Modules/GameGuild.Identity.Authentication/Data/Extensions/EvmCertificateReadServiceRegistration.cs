using GameGuild.Configuration.ApplicationLayer;
using Microsoft.Extensions.DependencyInjection;

namespace GameGuild.Identity.Authentication;

/// <summary>Explicit read/proof-service composition for the real-chain provider under construction.</summary>
/// <remarks>This does not select/activate an anchoring provider, create a signer, or send a transaction.</remarks>
public static class EvmCertificateReadServiceRegistration
{
    public static IServiceCollection AddEvmCertificateVerification(this IServiceCollection services, EvmCertificateNetworkOptions options)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(options);
        var errors = options.Validate();
        if (errors.Count > 0) throw new ArgumentException(string.Join(" ", errors), nameof(options));

        var snapshot = new EvmCertificateNetworkOptions
        {
            ChainId = options.ChainId,
            RpcEndpoint = options.RpcEndpoint,
            ContractAddress = options.ContractAddress,
            IssuerId = options.IssuerId,
            ExpectedRuntimeCodeSha256 = options.ExpectedRuntimeCodeSha256,
            FinalityMode = options.FinalityMode,
            MinimumConfirmations = options.MinimumConfirmations,
            RequestTimeout = options.RequestTimeout,
            MaximumResponseBytes = options.MaximumResponseBytes
        };

        services.AddSingleton(snapshot);
        services.AddHttpClient(EvmCertificateRpcClient.HttpClientName, client => client.Timeout = Timeout.InfiniteTimeSpan)
            .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler { AllowAutoRedirect = false, UseCookies = false })
            .RemoveAllLoggers();
        services.AddScoped<IEvmCertificateRpc>(provider => new EvmCertificateRpcClient(
            provider.GetRequiredService<IHttpClientFactory>().CreateClient(EvmCertificateRpcClient.HttpClientName),
            provider.GetRequiredService<EvmCertificateNetworkOptions>()));
        services.AddScoped<IEvmCertificateProofVerifier, EvmCertificateProofVerifier>();
        return services;
    }
}
