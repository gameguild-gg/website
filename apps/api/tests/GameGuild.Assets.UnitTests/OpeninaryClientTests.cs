using System.Net;
using System.Text;
using System.Text.Json;
using GameGuild.Assets.Extensions;
using GameGuild.Assets.Openinary;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace GameGuild.Assets.UnitTests;

/// <summary>
///     Wire-contract tests for the Openinary typed HttpClient: multipart upload fields,
///     Bearer auth, delete/invalidate/metadata endpoints, error mapping, transient retry,
///     and options binding.
/// </summary>
public sealed class OpeninaryClientTests
{
    private const string TestApiKey = "test-api-key";

    private sealed record RequestCapture(HttpRequestMessage Request, byte[] Body);

    private sealed class FakeHandler(params HttpResponseMessage[] responses) : HttpMessageHandler
    {
        public List<RequestCapture> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = request.Content is null
                ? []
                : await request.Content.ReadAsByteArrayAsync(cancellationToken);
            Requests.Add(new RequestCapture(request, body));
            var response = responses[Math.Min(Requests.Count - 1, responses.Length - 1)];
            return responses[Math.Min(Requests.Count - 1, responses.Length - 1)];
        }
    }

    private static OpeninaryClient CreateClient(
        HttpMessageHandler handler, OpeninaryOptions? options = null)
    {
        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri((options ?? new OpeninaryOptions()).BaseUrl)
        };
        return new OpeninaryClient(httpClient, Options.Create(options ?? new OpeninaryOptions()));
    }

    private static OpeninaryOptions BaseOptions() => new()
    {
        BaseUrl = "http://openinary.test",
        ApiKey = TestApiKey
    };

    private static HttpResponseMessage Json(HttpStatusCode statusCode, string json) => new()
    {
        StatusCode = statusCode,
        Content = new StringContent(json, Encoding.UTF8, "application/json")
    };

    [Fact]
    public async Task UploadAsync_PostsMultipartFieldsAndBearerHeader()
    {
        var handler = new FakeHandler(Json(HttpStatusCode.OK, """
            {"success":true,"files":[{"filename":"a.png","path":"gg/a.png","size":3,"url":"http://cdn/a.png","prewarmedUrls":["http://cdn/a.png?t=thumb"]}]}
            """));
        var client = CreateClient(handler, BaseOptions());

        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes("abc"));
        var result = await client.UploadAsync(stream, "a.png", "gg", ["t=thumb"], CancellationToken.None);

        var captured = Assert.Single(handler.Requests);
        var request = captured.Request;
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal(new Uri("http://openinary.test/upload"), request.RequestUri);
        Assert.Equal($"Bearer {TestApiKey}", request.Headers.Authorization?.ToString());

        var body = Encoding.UTF8.GetString(captured.Body);
        Assert.Contains("name=files; filename=a.png", body);
        Assert.Contains("name=folder\r\n\r\ngg", body);
        Assert.Contains("name=names\r\n\r\na.png", body);
        Assert.Contains("name=transformations\r\n\r\nt=thumb", body);

        Assert.Equal("gg/a.png", result.Path);
        Assert.Equal("http://cdn/a.png", result.Url);
        Assert.Equal(3L, result.SizeBytes);
        Assert.Equal(["http://cdn/a.png?t=thumb"], result.PrewarmedUrls);
    }

    [Fact]
    public async Task DeleteAsync_SendsDeleteToStoragePath()
    {
        var handler = new FakeHandler(new HttpResponseMessage(HttpStatusCode.NoContent));
        var client = CreateClient(handler, BaseOptions());

        await client.DeleteAsync("gg/a.png", CancellationToken.None);

        var request = Assert.Single(handler.Requests).Request;
        Assert.Equal(HttpMethod.Delete, request.Method);
        Assert.Equal(new Uri("http://openinary.test/storage/gg/a.png"), request.RequestUri);
    }

    [Fact]
    public async Task DeleteAsync_EscapesUnsafePathSegments()
    {
        var handler = new FakeHandler(new HttpResponseMessage(HttpStatusCode.NoContent));
        var client = CreateClient(handler, BaseOptions());

        await client.DeleteAsync("gg/a b/c.png", CancellationToken.None);

        Assert.Equal(new Uri("http://openinary.test/storage/gg/a%20b/c.png"), Assert.Single(handler.Requests).Request.RequestUri);
    }

    [Fact]
    public async Task InvalidateAsync_SendsDeleteToInvalidatePath()
    {
        var handler = new FakeHandler(new HttpResponseMessage(HttpStatusCode.NoContent));
        var client = CreateClient(handler, BaseOptions());

        await client.InvalidateAsync("gg/a.png", CancellationToken.None);

        var request = Assert.Single(handler.Requests).Request;
        Assert.Equal(HttpMethod.Delete, request.Method);
        Assert.Equal(new Uri("http://openinary.test/invalidate/gg/a.png"), request.RequestUri);
    }

    [Fact]
    public async Task GetMetadataAsync_ParsesTypedMetadataAndIgnoresExtraFields()
    {
        var handler = new FakeHandler(Json(HttpStatusCode.OK, """
            {"path":"gg/a.png","size":12,"createdAt":"2026-01-01T00:00:00Z","updatedAt":"2026-01-02T00:00:00Z","extra":"ignored"}
            """));
        var client = CreateClient(handler, BaseOptions());

        var metadata = await client.GetMetadataAsync("gg/a.png", CancellationToken.None);

        var request = Assert.Single(handler.Requests).Request;
        Assert.Equal(HttpMethod.Get, request.Method);
        Assert.Equal(new Uri("http://openinary.test/storage/gg/a.png/metadata"), request.RequestUri);
        Assert.Equal("gg/a.png", metadata.Path);
        Assert.Equal(12L, metadata.Size);
        Assert.Equal(DateTimeOffset.Parse("2026-01-01T00:00:00Z"), metadata.CreatedAt);
        Assert.Equal(DateTimeOffset.Parse("2026-01-02T00:00:00Z"), metadata.UpdatedAt);
    }

    [Fact]
    public async Task NonSuccessStatus_ThrowsOpeninaryExceptionWithBodyMessage()
    {
        var handler = new FakeHandler(Json(HttpStatusCode.Unauthorized, """{"error":"bad key"}"""));
        var client = CreateClient(handler, BaseOptions());

        var exception = await Assert.ThrowsAsync<OpeninaryException>(
            () => client.GetMetadataAsync("gg/a.png", CancellationToken.None));

        Assert.Equal(HttpStatusCode.Unauthorized, exception.StatusCode);
        Assert.Contains("bad key", exception.Message);
    }

    [Fact]
    public async Task UploadAsync_WithOurFileInPartialErrors_ThrowsOpeninaryException()
    {
        var handler = new FakeHandler(Json(HttpStatusCode.MultiStatus, """
            {"success":false,"files":[],"errors":[{"filename":"a.png","error":"too large"}]}
            """));
        var client = CreateClient(handler, BaseOptions());

        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes("abc"));
        var exception = await Assert.ThrowsAsync<OpeninaryException>(
            () => client.UploadAsync(stream, "a.png", "gg", [], CancellationToken.None));

        Assert.Equal(HttpStatusCode.MultiStatus, exception.StatusCode);
        Assert.Contains("too large", exception.Message);
    }

    [Fact]
    public async Task Transient500Then200_RetriedOnceAndSucceeds()
    {
        var handler = new FakeHandler(
            Json(HttpStatusCode.InternalServerError, """{"error":"boom"}"""),
            Json(HttpStatusCode.OK, """{"path":"gg/a.png","size":1,"createdAt":"2026-01-01T00:00:00Z","updatedAt":"2026-01-01T00:00:00Z"}"""));
        var client = new OpeninaryClient(
            new HttpClient(new OpeninaryRetryHandler { InnerHandler = handler }) { BaseAddress = new Uri("http://openinary.test") },
            Options.Create(BaseOptions()));

        var metadata = await client.GetMetadataAsync("gg/a.png", CancellationToken.None);

        Assert.Equal(2, handler.Requests.Count);
        Assert.Equal("gg/a.png", metadata.Path);
    }

    [Fact]
    public async Task NonTransient400_DoesNotRetry()
    {
        var handler = new FakeHandler(Json(HttpStatusCode.BadRequest, """{"error":"nope"}"""));
        var client = new OpeninaryClient(
            new HttpClient(new OpeninaryRetryHandler { InnerHandler = handler }) { BaseAddress = new Uri("http://openinary.test") },
            Options.Create(BaseOptions()));

        await Assert.ThrowsAsync<OpeninaryException>(
            () => client.GetMetadataAsync("gg/a.png", CancellationToken.None));

        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task RetryHandler_ExhaustsAfterThreeAttempts()
    {
        var handler = new FakeHandler(
            Json(HttpStatusCode.InternalServerError, """{"error":"boom"}"""),
            Json(HttpStatusCode.InternalServerError, """{"error":"boom"}"""),
            Json(HttpStatusCode.InternalServerError, """{"error":"boom"}"""));
        var client = new OpeninaryClient(
            new HttpClient(new OpeninaryRetryHandler { InnerHandler = handler }) { BaseAddress = new Uri("http://openinary.test") },
            Options.Create(BaseOptions()));

        await Assert.ThrowsAsync<OpeninaryException>(
            () => client.GetMetadataAsync("gg/a.png", CancellationToken.None));

        Assert.Equal(3, handler.Requests.Count);
    }

    [Fact]
    public void ModuleRegistration_BindsOptionsFromConfig()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Assets:Openinary:BaseUrl"] = "http://cfg.test",
                ["Assets:Openinary:ApiKey"] = "cfg-key",
                ["Assets:Openinary:FolderPrefix"] = "custom/",
                ["Assets:Openinary:MaxFileMb"] = "10",
                ["Assets:Openinary:TimeoutSeconds"] = "30"
            })
            .Build();
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(config);
        services.AddLogging();

        services.AddAssetsModule(config);
        using var provider = services.BuildServiceProvider();

        var options = provider.GetRequiredService<IOptions<OpeninaryOptions>>().Value;
        Assert.Equal("http://cfg.test", options.BaseUrl);
        Assert.Equal("cfg-key", options.ApiKey);
        Assert.Equal("custom/", options.FolderPrefix);
        Assert.Equal(10, options.MaxFileMb);
        Assert.Equal(30, options.TimeoutSeconds);

        Assert.NotNull(provider.GetRequiredService<IOpeninaryClient>());
    }

    [Fact]
    public void ModuleRegistration_DefaultsWhenConfigAbsent()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection([]).Build();
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(config);
        services.AddLogging();

        services.AddAssetsModule(config);
        using var provider = services.BuildServiceProvider();

        var options = provider.GetRequiredService<IOptions<OpeninaryOptions>>().Value;
        Assert.Equal(OpeninaryOptions.DefaultBaseUrl, options.BaseUrl);
        Assert.Equal(string.Empty, options.ApiKey);
        Assert.Equal(50, options.MaxFileMb);
        Assert.Equal(120, options.TimeoutSeconds);
    }
}
