using System.Text.Json;
using GameGuild.Configuration.ApplicationLayer;

namespace GameGuild.Identity.Authentication;

/// <summary>Read-only fixed-method port using the shared bounded JSON-RPC transport.</summary>
public sealed class EvmCertificateRpcClient : IEvmCertificateRpc, IDisposable
{
    public static string HttpClientName => "EvmCertificateRpc";
    private readonly EvmCertificateRpcTransport transport;

    public EvmCertificateRpcClient(HttpClient http, EvmCertificateNetworkOptions options)
    {
        transport = new EvmCertificateRpcTransport(http, options);
    }

    public Task<JsonElement> RequestAsync(EvmCertificateRpcMethod method, object?[] parameters) =>
        RequestAsync(method, parameters, CancellationToken.None);

    public async Task<JsonElement> RequestAsync(EvmCertificateRpcMethod method, object?[] parameters, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(parameters);
        cancellationToken.ThrowIfCancellationRequested();
        return await transport.RequestAsync(WireMethod(method), parameters, cancellationToken).ConfigureAwait(false);
    }

    public void Dispose() => transport.Dispose();

    private static string WireMethod(EvmCertificateRpcMethod method) => method switch
    {
        EvmCertificateRpcMethod.ChainId => "eth_chainId",
        EvmCertificateRpcMethod.GetCode => "eth_getCode",
        EvmCertificateRpcMethod.Call => "eth_call",
        EvmCertificateRpcMethod.GetTransactionByHash => "eth_getTransactionByHash",
        EvmCertificateRpcMethod.GetTransactionReceipt => "eth_getTransactionReceipt",
        EvmCertificateRpcMethod.GetBlockByNumber => "eth_getBlockByNumber",
        _ => throw new ArgumentOutOfRangeException(nameof(method), "Unsupported certificate RPC operation.")
    };
}
