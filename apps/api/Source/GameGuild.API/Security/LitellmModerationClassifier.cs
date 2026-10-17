using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using GameGuild.Assets;
using Microsoft.Extensions.Options;

namespace GameGuild.API.Security;

/// <summary>
/// Options for the vision-model moderation classifier.
/// </summary>
public class ModerationClassifierOptions
{
    public const string SectionName = "Assets:AutoModeration:Classifier";

    public string BaseUrl { get; set; } = "http://litellm.litellm.svc.cluster.local:4000/v1";

    public string ApiKey { get; set; } = string.Empty;

    public string Model { get; set; } = "qwen3.8-flash-next";

    public int TimeoutSeconds { get; set; } = 120;
}

/// <summary>
///     Composition-root adapter: classifies asset images through an
///     OpenAI-compatible chat-completions endpoint (LiteLLM → qwen vision lane).
///     The Assets module stays free of any concrete AI stack; this binds its
///     <see cref="IAssetModerationClassifier"/> seam to the product's LLM proxy.
/// </summary>
public sealed class LitellmModerationClassifier : IAssetModerationClassifier
{
    private const string Prompt =
        "You are an image content moderation classifier. Analyze the attached image. " +
        "Respond ONLY with strict JSON in exactly this shape and nothing else: " +
        "{\"labels\":[{\"name\":\"<name>\",\"score\":<0.0-1.0>}]} " +
        "Every name MUST be one of: adult_content, violence, hate_symbols, drugs, weapons, self_harm, suggestive, safe. " +
        "Scores are between 0.0 and 1.0. No markdown, no explanation, no extra keys.";

    private static readonly JsonSerializerOptions JsonOptions = JsonSerializerOptions.Web;

    private readonly HttpClient _httpClient;
    private readonly ModerationClassifierOptions _options;

    public LitellmModerationClassifier(HttpClient httpClient, IOptions<ModerationClassifierOptions> options)
    {
        _httpClient = httpClient;
        _options = options.Value;
    }

    public async Task<AssetModerationClassification> ClassifyAsync(
        AssetContent content,
        Stream imageBytes,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            throw new ModerationClassificationException(
                "Moderation classifier API key is not configured (Assets:AutoModeration:Classifier:ApiKey)");
        }

        byte[] payload;
        using (var buffer = new MemoryStream())
        {
            await imageBytes.CopyToAsync(buffer, ct).ConfigureAwait(false);
            payload = buffer.ToArray();
        }

        var body = new
        {
            model = _options.Model,
            max_tokens = 4096,
            stream = false,
            chat_template_kwargs = new { thinking = false, enable_thinking = false },
            messages = new object[]
            {
                new
                {
                    role = "user",
                    content = new object[]
                    {
                        new { type = "text", text = Prompt },
                        new
                        {
                            type = "image_url",
                            image_url = new { url = $"data:{content.MimeType};base64,{Convert.ToBase64String(payload)}" }
                        }
                    }
                }
            }
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, "chat/completions");
        request.Headers.Authorization = string.IsNullOrWhiteSpace(_options.ApiKey)
            ? null
            : new AuthenticationHeaderValue("Bearer", _options.ApiKey);
        request.Content = JsonContent.Create(body, options: JsonOptions);

        using var response = await _httpClient.SendAsync(request, ct).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            throw new ModerationClassificationException(
                $"Moderation classifier HTTP {(int)response.StatusCode} from {_options.BaseUrl}");
        }

        var completion = await response.Content
            .ReadFromJsonAsync<ChatCompletionResponse>(JsonOptions, ct)
            .ConfigureAwait(false);
        var text = completion?.Choices?.FirstOrDefault()?.Message?.Content;
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new ModerationClassificationException("Moderation classifier returned an empty completion");
        }

        return ParseLabels(text, _options.Model);
    }

    /// <summary>
    /// Extracts the first JSON object and maps it to label scores. Throws
    /// <see cref="ModerationClassificationException"/> for anything unparseable —
    /// callers route that to NeedsReview, never approval.
    /// </summary>
    internal static AssetModerationClassification ParseLabels(string text, string model)
    {
        var start = text.IndexOf('{');
        var end = text.LastIndexOf('}');
        if (start < 0 || end <= start)
        {
            throw new ModerationClassificationException($"No JSON object in classifier response: {Truncate(text)}");
        }

        LabelsPayload? parsed;
        try
        {
            parsed = JsonSerializer.Deserialize<LabelsPayload>(text.Substring(start, end - start + 1), JsonOptions);
        }
        catch (JsonException ex)
        {
            throw new ModerationClassificationException($"Unparseable classifier JSON: {Truncate(text)}", ex);
        }

        if (parsed?.Labels is null || parsed.Labels.Count == 0)
        {
            throw new ModerationClassificationException($"Classifier JSON carried no labels: {Truncate(text)}");
        }

        var labels = parsed.Labels
            .Where(l => !string.IsNullOrWhiteSpace(l.Name))
            .Select(l => new ModerationLabelScore(
                l.Name!.Trim(),
                Math.Clamp(l.Score, 0.0, 1.0)))
            .ToList();

        if (labels.Count == 0)
        {
            throw new ModerationClassificationException($"Classifier labels carried no usable entries: {Truncate(text)}");
        }

        return new AssetModerationClassification(false, labels, model);
    }

    private static string Truncate(string text) =>
        text.Length <= 200 ? text : $"{text[..200]}…";

    private sealed record LabelsPayload(IReadOnlyList<LabelPayload>? Labels);

    private sealed record LabelPayload(string? Name, double Score);

    private sealed record ChatCompletionResponse(IReadOnlyList<ChoicePayload>? Choices);

    private sealed record ChoicePayload(MessagePayload? Message);

    private sealed record MessagePayload(string? Content);
}
