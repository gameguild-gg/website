using System.Net.Http.Headers;
using System.Text.Json;
using GameGuild.Configuration.ApplicationLayer;

namespace GameGuild.Identity.Authentication;

/// <summary>Bounded JSON-RPC transport with strict response correlation and safe failures.</summary>
/// <remarks>Owns its HttpClient. Hosting registration disables redirects and default endpoint logging.</remarks>
public sealed class EvmCertificateRpcClient : IEvmCertificateRpc, IDisposable
{
    public static string HttpClientName => "EvmCertificateRpc";
    private readonly HttpClient http;
    private readonly Uri endpoint;
    private readonly TimeSpan timeout;
    private readonly int maximumResponseBytes;
    private long nextRequestId;

    public EvmCertificateRpcClient(HttpClient http, EvmCertificateNetworkOptions options)
    {
        ArgumentNullException.ThrowIfNull(http);
        ArgumentNullException.ThrowIfNull(options);
        var errors = options.Validate();
        if (errors.Count > 0)
        {
            throw new ArgumentException(string.Join(" ", errors), nameof(options));
        }
        this.http = http;
        endpoint = new Uri(options.RpcEndpoint, UriKind.Absolute);
        timeout = options.RequestTimeout;
        maximumResponseBytes = options.MaximumResponseBytes;
    }

    public Task<JsonElement> RequestAsync(EvmCertificateRpcMethod method, object?[] parameters) =>
        RequestAsync(method, parameters, CancellationToken.None);

    public async Task<JsonElement> RequestAsync(EvmCertificateRpcMethod method, object?[] parameters, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(parameters);
        cancellationToken.ThrowIfCancellationRequested();
        var wireMethod = WireMethod(method);
        var id = Interlocked.Increment(ref nextRequestId);
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(timeout);
        using var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
        {
            Content = new ByteArrayContent(JsonSerializer.SerializeToUtf8Bytes(
                new { jsonrpc = "2.0", id, method = wireMethod, @params = parameters }, JsonSerializerOptions.Web))
        };
        request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json") { CharSet = "utf-8" };

        try
        {
            using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, deadline.Token).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                throw new EvmCertificateRpcException(EvmCertificateRpcFailure.HttpFailure, (int)response.StatusCode);
            }

            if (response.Content.Headers.ContentLength > maximumResponseBytes)
            {
                throw new EvmCertificateRpcException(EvmCertificateRpcFailure.ResponseTooLarge);
            }

            await using var stream = await response.Content.ReadAsStreamAsync(deadline.Token).ConfigureAwait(false);
            using var bytes = new MemoryStream();
            var chunk = new byte[8192];
            while (true)
            {
                var read = await stream.ReadAsync(chunk, deadline.Token).ConfigureAwait(false);
                if (read == 0)
                {
                    break;
                }
                if (bytes.Length + read > maximumResponseBytes)
                {
                    throw new EvmCertificateRpcException(EvmCertificateRpcFailure.ResponseTooLarge);
                }

                bytes.Write(chunk, 0, read);
            }

            deadline.Token.ThrowIfCancellationRequested();
            using var document = JsonDocument.Parse(bytes.GetBuffer().AsMemory(0, (int)bytes.Length), new JsonDocumentOptions { MaxDepth = 32 });
            deadline.Token.ThrowIfCancellationRequested();
            return ReadResult(document.RootElement, id);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new EvmCertificateRpcException(EvmCertificateRpcFailure.Timeout);
        }
        catch (HttpRequestException)
        {
            throw new EvmCertificateRpcException(EvmCertificateRpcFailure.TransportFailure);
        }
        catch (IOException)
        {
            throw new EvmCertificateRpcException(EvmCertificateRpcFailure.TransportFailure);
        }
        catch (JsonException)
        {
            throw new EvmCertificateRpcException(EvmCertificateRpcFailure.InvalidResponse);
        }
    }

    public void Dispose() => http.Dispose();

    private static JsonElement ReadResult(JsonElement root, long expectedId)
    {
        if (root.ValueKind != JsonValueKind.Object || HasDuplicateProperties(root) ||
            !root.TryGetProperty("jsonrpc", out var version) || version.ValueKind != JsonValueKind.String || version.GetString() != "2.0" ||
            !root.TryGetProperty("id", out var id) || id.ValueKind != JsonValueKind.Number || !id.TryGetInt64(out var responseId) || responseId != expectedId)
        {
            throw new EvmCertificateRpcException(EvmCertificateRpcFailure.InvalidResponse);
        }

        var hasResult = root.TryGetProperty("result", out var result);
        var hasError = root.TryGetProperty("error", out var error);
        if (hasError == hasResult)
        {
            throw new EvmCertificateRpcException(EvmCertificateRpcFailure.InvalidResponse);
        }
        if (hasResult)
        {
            return result.Clone();
        }

        if (error.ValueKind != JsonValueKind.Object || HasDuplicateProperties(error) ||
            !error.TryGetProperty("code", out var code) || code.ValueKind != JsonValueKind.Number || !code.TryGetInt32(out var errorCode) ||
            !error.TryGetProperty("message", out var message) || message.ValueKind != JsonValueKind.String)
        {
            throw new EvmCertificateRpcException(EvmCertificateRpcFailure.InvalidResponse);
        }

        throw new EvmCertificateRpcException(EvmCertificateRpcFailure.RpcFailure, rpcErrorCode: errorCode);
    }

    private static bool HasDuplicateProperties(JsonElement element)
    {
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var property in element.EnumerateObject())
        {
            if (!names.Add(property.Name))
            {
                return true;
            }
        }

        return false;
    }

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
