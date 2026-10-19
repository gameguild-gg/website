using System.Net;
using System.Text;
using System.Text.Json;
using Xunit;

namespace GameGuild.Identity.Authentication.UnitTests.Services;

public sealed class EvmCertificateRpcClientTests
{
    [Fact]
    public async Task RequestPreservesJsonRpcIdentityAndLiteralParameters()
    {
        JsonElement captured = default;
        Uri? endpoint = null;
        using var handler = new ReplyHandler(async (request, cancellationToken) =>
        {
            endpoint = request.RequestUri;
            Assert.True(request.Content!.Headers.ContentLength is > 0);
            using var requestDocument = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(cancellationToken));
            captured = requestDocument.RootElement.Clone();
            return JsonResponse(captured.GetProperty("id").GetInt64(), "{\"value\":\"owned & literal\"}");
        });
        using var http = new HttpClient(handler);
        var client = new EvmCertificateRpcClient(http, EvmCertificateNetworkOptionsTests.ValidOptions());
        var result = await client.RequestAsync(EvmCertificateRpcMethod.Call, [new { data = "owned & literal" }, "finalized"]);
        Assert.Equal(new Uri("https://rpc.invalid/"), endpoint);
        Assert.Equal("2.0", captured.GetProperty("jsonrpc").GetString());
        Assert.Equal("eth_call", captured.GetProperty("method").GetString());
        Assert.Equal("owned & literal", captured.GetProperty("params")[0].GetProperty("data").GetString());
        Assert.Equal("finalized", captured.GetProperty("params")[1].GetString());
        Assert.Equal("owned & literal", result.GetProperty("value").GetString());
    }

    [Theory]
    [InlineData(EvmCertificateRpcMethod.ChainId, "eth_chainId")]
    [InlineData(EvmCertificateRpcMethod.GetCode, "eth_getCode")]
    [InlineData(EvmCertificateRpcMethod.GetTransactionByHash, "eth_getTransactionByHash")]
    [InlineData(EvmCertificateRpcMethod.GetTransactionReceipt, "eth_getTransactionReceipt")]
    [InlineData(EvmCertificateRpcMethod.GetBlockByNumber, "eth_getBlockByNumber")]
    public async Task TypedMethodsUseTheExpectedWireName(EvmCertificateRpcMethod method, string wireName)
    {
        string? observed = null;
        using var handler = new ReplyHandler(async (request, cancellationToken) =>
        {
            using var document = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(cancellationToken));
            observed = document.RootElement.GetProperty("method").GetString();
            return JsonResponse(document.RootElement.GetProperty("id").GetInt64(), "null");
        });
        using var http = new HttpClient(handler);
        var client = new EvmCertificateRpcClient(http, EvmCertificateNetworkOptionsTests.ValidOptions());
        var result = await client.RequestAsync(method, []);
        Assert.Equal(wireName, observed);
        Assert.Equal(JsonValueKind.Null, result.ValueKind);
    }

    [Theory]
    [InlineData("{\"jsonrpc\":\"1.0\",\"id\":1,\"result\":true}")]
    [InlineData("{\"jsonrpc\":\"2.0\",\"id\":-1,\"result\":true}")]
    [InlineData("{\"jsonrpc\":\"2.0\",\"id\":\"1\",\"result\":true}")]
    [InlineData("{\"jsonrpc\":\"2.0\",\"result\":true}")]
    [InlineData("{\"jsonrpc\":\"2.0\",\"id\":1}")]
    [InlineData("{\"jsonrpc\":\"2.0\",\"id\":1,\"result\":true,\"error\":{\"code\":-32000}}")]
    [InlineData("{\"jsonrpc\":\"2.0\",\"id\":1,\"result\":1,\"result\":2}")]
    [InlineData("[]")]
    [InlineData("private-provider-marker is not JSON")]
    public async Task InvalidOrAmbiguousResponseIsRejectedWithoutEchoingProviderText(string responseBody)
    {
        using var handler = new ReplyHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(responseBody, Encoding.UTF8, "application/json")
        }));
        using var http = new HttpClient(handler);
        var client = new EvmCertificateRpcClient(http, EvmCertificateNetworkOptionsTests.ValidOptions());
        var error = await Assert.ThrowsAsync<EvmCertificateRpcException>(() => client.RequestAsync(EvmCertificateRpcMethod.ChainId, []));
        Assert.Equal(EvmCertificateRpcFailure.InvalidResponse, error.Failure);
        Assert.DoesNotContain("private-provider-marker", error.ToString());
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.TooManyRequests)]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.TemporaryRedirect)]
    public async Task HttpFailureIsExplicitAndDoesNotExposeResponseBody(HttpStatusCode status)
    {
        using var handler = new ReplyHandler((_, _) => Task.FromResult(new HttpResponseMessage(status)
        {
            Content = new StringContent("private-provider-marker")
        }));
        using var http = new HttpClient(handler);
        var client = new EvmCertificateRpcClient(http, EvmCertificateNetworkOptionsTests.ValidOptions());
        var error = await Assert.ThrowsAsync<EvmCertificateRpcException>(() => client.RequestAsync(EvmCertificateRpcMethod.ChainId, []));
        Assert.Equal(EvmCertificateRpcFailure.HttpFailure, error.Failure);
        Assert.Equal((int)status, error.HttpStatusCode);
        Assert.DoesNotContain("private-provider-marker", error.ToString());
    }

    [Fact]
    public async Task RpcErrorRetainsNumericCodeWithoutProviderMessageOrData()
    {
        using var handler = new ReplyHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{\"jsonrpc\":\"2.0\",\"id\":1,\"error\":{\"code\":-32005,\"message\":\"private-provider-marker\",\"data\":\"private-data-marker\"}}")
        }));
        using var http = new HttpClient(handler);
        var client = new EvmCertificateRpcClient(http, EvmCertificateNetworkOptionsTests.ValidOptions());
        var error = await Assert.ThrowsAsync<EvmCertificateRpcException>(() => client.RequestAsync(EvmCertificateRpcMethod.ChainId, []));
        Assert.Equal(EvmCertificateRpcFailure.RpcFailure, error.Failure);
        Assert.Equal(-32005, error.RpcErrorCode);
        Assert.DoesNotContain("private-provider-marker", error.ToString());
        Assert.DoesNotContain("private-data-marker", error.ToString());
    }

    [Fact]
    public async Task OversizedKnownLengthResponseIsRejected()
    {
        using var handler = new ReplyHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(new string('x', 1025))
        }));
        using var http = new HttpClient(handler);
        var options = EvmCertificateNetworkOptionsTests.ValidOptions();
        options.MaximumResponseBytes = 1024;
        var client = new EvmCertificateRpcClient(http, options);
        var error = await Assert.ThrowsAsync<EvmCertificateRpcException>(() => client.RequestAsync(EvmCertificateRpcMethod.ChainId, []));
        Assert.Equal(EvmCertificateRpcFailure.ResponseTooLarge, error.Failure);
    }

    [Fact]
    public async Task UnknownLengthResponseCannotBypassTheByteBudget()
    {
        using var handler = new ReplyHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new UnknownLengthContent(Encoding.UTF8.GetBytes(new string('x', 1025)))
        }));
        using var http = new HttpClient(handler);
        var options = EvmCertificateNetworkOptionsTests.ValidOptions();
        options.MaximumResponseBytes = 1024;
        var client = new EvmCertificateRpcClient(http, options);
        var error = await Assert.ThrowsAsync<EvmCertificateRpcException>(() => client.RequestAsync(EvmCertificateRpcMethod.ChainId, []));
        Assert.Equal(EvmCertificateRpcFailure.ResponseTooLarge, error.Failure);
    }

    [Fact]
    public async Task ExcessiveJsonNestingIsRejected()
    {
        var result = new string('[', 40) + "0" + new string(']', 40);
        using var handler = new ReplyHandler((_, _) => Task.FromResult(JsonResponse(1, result)));
        using var http = new HttpClient(handler);
        var client = new EvmCertificateRpcClient(http, EvmCertificateNetworkOptionsTests.ValidOptions());
        var error = await Assert.ThrowsAsync<EvmCertificateRpcException>(() => client.RequestAsync(EvmCertificateRpcMethod.ChainId, []));
        Assert.Equal(EvmCertificateRpcFailure.InvalidResponse, error.Failure);
    }

    [Fact]
    public async Task RequestDeadlineCancelsAnUnresponsiveProvider()
    {
        using var handler = new ReplyHandler(async (_, cancellationToken) =>
        {
            await Task.Delay(Timeout.Infinite, cancellationToken);
            return JsonResponse(1, "true");
        });
        using var http = new HttpClient(handler);
        var options = EvmCertificateNetworkOptionsTests.ValidOptions();
        options.RequestTimeout = TimeSpan.FromMilliseconds(20);
        var client = new EvmCertificateRpcClient(http, options);
        var error = await Assert.ThrowsAsync<EvmCertificateRpcException>(() => client.RequestAsync(EvmCertificateRpcMethod.ChainId, []));
        Assert.Equal(EvmCertificateRpcFailure.Timeout, error.Failure);
    }

    [Fact]
    public async Task UnsupportedTypedMethodDoesNotMakeAnHttpRequest()
    {
        var called = false;
        using var handler = new ReplyHandler((_, _) =>
        {
            called = true;
            return Task.FromResult(JsonResponse(1, "true"));
        });
        using var http = new HttpClient(handler);
        var client = new EvmCertificateRpcClient(http, EvmCertificateNetworkOptionsTests.ValidOptions());
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => client.RequestAsync((EvmCertificateRpcMethod)999, []));
        Assert.False(called);
    }

    [Fact]
    public async Task TransportExceptionDoesNotExposeTheRpcCredentialPath()
    {
        using var handler = new ReplyHandler((_, _) => throw new HttpRequestException("private-provider-marker"));
        using var http = new HttpClient(handler);
        var client = new EvmCertificateRpcClient(http, EvmCertificateNetworkOptionsTests.ValidOptions());
        var error = await Assert.ThrowsAsync<EvmCertificateRpcException>(() => client.RequestAsync(EvmCertificateRpcMethod.ChainId, []));
        Assert.Equal(EvmCertificateRpcFailure.TransportFailure, error.Failure);
        Assert.DoesNotContain("private-provider-marker", error.ToString());
    }

    [Fact]
    public async Task CallerCancellationIsPropagated()
    {
        var called = false;
        using var handler = new ReplyHandler((_, _) =>
        {
            called = true;
            return Task.FromResult(JsonResponse(1, "true"));
        });
        using var http = new HttpClient(handler);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var client = new EvmCertificateRpcClient(http, EvmCertificateNetworkOptionsTests.ValidOptions());
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => client.RequestAsync(EvmCertificateRpcMethod.ChainId, [], cancellation.Token));
        Assert.False(called);
    }

    private static HttpResponseMessage JsonResponse(long id, string result) => new(HttpStatusCode.OK)
    {
        Content = new StringContent($"{{\"jsonrpc\":\"2.0\",\"id\":{id},\"result\":{result}}}", Encoding.UTF8, "application/json")
    };

    private sealed class ReplyHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> reply) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => reply(request, cancellationToken);
    }

    private sealed class UnknownLengthContent(byte[] bytes) : HttpContent
    {
        protected override bool TryComputeLength(out long length)
        {
            length = 0;
            return false;
        }

        protected override async Task SerializeToStreamAsync(Stream stream, TransportContext? context)
        {
            using var content = new ByteArrayContent(bytes);
            await content.CopyToAsync(stream, context);
        }
        protected override Task<Stream> CreateContentReadStreamAsync() => Task.FromResult<Stream>(new MemoryStream(bytes, writable: false));
    }
}
