using System.Net;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using GameGuild.API.Security;
using GameGuild.Assets;
using Microsoft.Extensions.Options;
using Xunit;

namespace GameGuild.API.UnitTests.Security;

public sealed class LitellmModerationClassifierTests
{
    private readonly ModerationClassifierOptions _options = new()
    {
        BaseUrl = "http://localhost:4000/v1",
        ApiKey = "sk-test",
        Model = "qwen3.8-flash-next",
        TimeoutSeconds = 5
    };

    private static AssetContent CreateContent() => new(
        "openinary", "objects/a.png", new string('a', 64), "image/png", 10, null, null);

    private LitellmModerationClassifier CreateClassifier(
        CapturingHandler handler, ModerationClassifierOptions? options = null)
    {
        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri((options ?? _options).BaseUrl)
        };
        return new LitellmModerationClassifier(httpClient, Options.Create(options ?? _options));
    }

    [Fact]
    public async Task ClassifyAsync_SendsStrictJsonPromptWithDataUrlImageAndThinkingOff()
    {
        var handler = new CapturingHandler(
            JsonBody(WrapCompletion("{\"labels\":[{\"name\":\"safe\",\"score\":1.0}]}")));
        var classifier = CreateClassifier(handler);

        var result = await classifier.ClassifyAsync(CreateContent(), new MemoryStream([1, 2, 3]));

        result.Failed.Should().BeFalse();
        result.Model.Should().Be("qwen3.8-flash-next");
        result.Labels.Should().ContainSingle(l => l.Name == "safe" && l.Score == 1.0);

        var request = handler.LastRequest!;
        request.RequestUri!.AbsolutePath.Should().EndWith("/chat/completions");
        request.Headers.Authorization!.Scheme.Should().Be("Bearer");
        request.Headers.Authorization.Parameter.Should().Be("sk-test");

        var body = JsonDocument.Parse(handler.LastBody!).RootElement;
        body.GetProperty("model").GetString().Should().Be("qwen3.8-flash-next");
        body.GetProperty("max_tokens").GetInt32().Should().Be(4096);
        body.GetProperty("stream").GetBoolean().Should().Be(false);

        var kwargs = body.GetProperty("chat_template_kwargs");
        kwargs.GetProperty("thinking").GetBoolean().Should().BeFalse();
        kwargs.GetProperty("enable_thinking").GetBoolean().Should().BeFalse();

        var message = body.GetProperty("messages")[0];
        var parts = message.GetProperty("content");
        parts.GetArrayLength().Should().Be(2);

        var text = parts[0];
        text.GetProperty("type").GetString().Should().Be("text");
        var prompt = text.GetProperty("text").GetString()!;
        prompt.Should().Contain("adult_content");
        prompt.Should().Contain("strict JSON");

        var image = parts[1];
        image.GetProperty("type").GetString().Should().Be("image_url");
        var url = image.GetProperty("image_url").GetProperty("url").GetString()!;
        url.Should().StartWith("data:image/png;base64,");
    }

    [Fact]
    public async Task ClassifyAsync_SurroundingProse_StillParsesFirstJsonBlock()
    {
        var handler = new CapturingHandler(
            JsonBody(WrapCompletion("Here you go:\n{\"labels\":[{\"name\":\"violence\",\"score\":0.42}]}\nDone.")));
        var classifier = CreateClassifier(handler);

        var result = await classifier.ClassifyAsync(CreateContent(), new MemoryStream([1]));

        result.Labels.Should().ContainSingle(l => l.Name == "violence" && Math.Abs(l.Score - 0.42) < 0.001);
    }

    [Fact]
    public async Task ClassifyAsync_PoisonedNonJsonResponse_ThrowsClassificationException()
    {
        var handler = new CapturingHandler(JsonBody(WrapCompletion("I cannot answer that.")));
        var classifier = CreateClassifier(handler);

        var act = () => classifier.ClassifyAsync(CreateContent(), new MemoryStream([1]));

        await act.Should().ThrowAsync<ModerationClassificationException>();
    }

    [Fact]
    public async Task ClassifyAsync_MalformedJson_ThrowsClassificationException()
    {
        var handler = new CapturingHandler(JsonBody(WrapCompletion("{\"labels\":")));
        var classifier = CreateClassifier(handler);

        var act = () => classifier.ClassifyAsync(CreateContent(), new MemoryStream([1]));

        await act.Should().ThrowAsync<ModerationClassificationException>();
    }

    [Fact]
    public async Task ClassifyAsync_EmptyLabelsArray_ThrowsClassificationException()
    {
        var handler = new CapturingHandler(JsonBody(WrapCompletion("{\"labels\":[]}")));
        var classifier = CreateClassifier(handler);

        var act = () => classifier.ClassifyAsync(CreateContent(), new MemoryStream([1]));

        await act.Should().ThrowAsync<ModerationClassificationException>();
    }

    [Fact]
    public async Task ClassifyAsync_HttpError_ThrowsClassificationException()
    {
        var handler = new CapturingHandler(new HttpResponseMessage(HttpStatusCode.BadGateway));
        var classifier = CreateClassifier(handler);

        var act = () => classifier.ClassifyAsync(CreateContent(), new MemoryStream([1]));

        await act.Should().ThrowAsync<ModerationClassificationException>()
            .WithMessage("*502*");
    }

    [Fact]
    public async Task ClassifyAsync_MissingApiKey_ThrowsClassificationException()
    {
        var handler = new CapturingHandler(JsonBody(WrapCompletion("{\"labels\":[{\"name\":\"safe\",\"score\":1.0}]}")));
        var noKey = new ModerationClassifierOptions { BaseUrl = _options.BaseUrl, ApiKey = "" };
        var classifier = CreateClassifier(handler, noKey);

        var act = () => classifier.ClassifyAsync(CreateContent(), new MemoryStream([1]));

        await act.Should().ThrowAsync<ModerationClassificationException>()
            .WithMessage("*ApiKey*");
    }

    [Fact]
    public void ParseLabels_ScoresOutsideUnitInterval_AreClamped()
    {
        var result = LitellmModerationClassifier.ParseLabels(
            "{\"labels\":[{\"name\":\"safe\",\"score\":1.7},{\"name\":\"violence\",\"score\":-0.3}]}",
            "m");

        result.Labels[0].Score.Should().Be(1.0);
        result.Labels[1].Score.Should().Be(0.0);
    }

    private static string WrapCompletion(string content) =>
        JsonSerializer.Serialize(new
        {
            choices = new[] { new { message = new { content } } }
        });

    private static HttpResponseMessage JsonBody(string json) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(json, Encoding.UTF8, "application/json")
    };

    private sealed class CapturingHandler(HttpResponseMessage response) : HttpMessageHandler
    {
        public HttpRequestMessage? LastRequest { get; private set; }
        public string? LastBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            LastBody = request.Content == null
                ? null
                : await request.Content.ReadAsStringAsync(cancellationToken);
            return response;
        }
    }
}
