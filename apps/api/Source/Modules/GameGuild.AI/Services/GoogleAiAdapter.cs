using GameGuild;
using Microsoft.Extensions.Logging;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace GameGuild.AI;

internal sealed class GoogleAiAdapter(IHttpClientFactory httpClientFactory, ILogger<GoogleAiAdapter> logger) : IAiProviderAdapter
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public AiProvider Provider => AiProvider.Google;

    public async Task<Result<AiProviderExecutionResult>> CompleteStreamingAsync(
        AiResolvedRequest request,
        Func<string, CancellationToken, ValueTask> onDelta,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(onDelta);
        try
        {
            var payload = new
            {
                systemInstruction = string.IsNullOrWhiteSpace(request.SystemPrompt)
                    ? null
                    : new { parts = new[] { new { text = request.SystemPrompt } } },
                contents = request.Messages.Select(static message => new
                {
                    role = message.Role.Equals("assistant", StringComparison.OrdinalIgnoreCase) ? "model" : "user",
                    parts = new[] { new { text = message.Content } }
                }),
                generationConfig = new
                {
                    temperature = request.Temperature,
                    maxOutputTokens = request.MaxTokens
                }
            };

            var endpoint = $"{request.BaseUrl.TrimEnd('/')}/v1beta/models/{Uri.EscapeDataString(request.Model)}:streamGenerateContent?alt=sse&key={Uri.EscapeDataString(request.ApiKey)}";
            using var httpRequest = new HttpRequestMessage(HttpMethod.Post, endpoint)
            {
                Content = new StringContent(JsonSerializer.Serialize(payload, SerializerOptions), Encoding.UTF8, "application/json")
            };
            httpRequest.Headers.Accept.ParseAdd("text/event-stream");

            var client = httpClientFactory.CreateClient();
            using var response = await client.SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                return Result.Failure<AiProviderExecutionResult>(AiProviderErrorMapper.Map("Google", response.StatusCode, errorBody));
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            using var reader = new StreamReader(stream);
            var text = new StringBuilder();
            string? finishReason = null;
            int? inputTokens = null;
            int? outputTokens = null;
            int? totalTokens = null;
            while (await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false) is { } line)
            {
                if (!line.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
                    continue;
                var data = line[5..].Trim();
                if (data.Length == 0 || data == "[DONE]")
                    continue;

                using var document = JsonDocument.Parse(data);
                var root = document.RootElement;
                if (root.TryGetProperty("usageMetadata", out var usageElement))
                {
                    inputTokens = AiJsonHelpers.TryGetInt(usageElement, "promptTokenCount") ?? inputTokens;
                    outputTokens = AiJsonHelpers.TryGetInt(usageElement, "candidatesTokenCount") ?? outputTokens;
                    totalTokens = AiJsonHelpers.TryGetInt(usageElement, "totalTokenCount") ?? totalTokens;
                }
                if (!root.TryGetProperty("candidates", out var candidates)
                    || candidates.ValueKind != JsonValueKind.Array
                    || candidates.GetArrayLength() == 0)
                    continue;
                var candidate = candidates[0];
                if (candidate.TryGetProperty("finishReason", out var finishElement) && finishElement.ValueKind == JsonValueKind.String)
                    finishReason = finishElement.GetString();
                var delta = candidate.TryGetProperty("content", out var contentElement)
                    && contentElement.TryGetProperty("parts", out var partsElement)
                        ? AiJsonHelpers.ExtractTextFromParts(partsElement)
                        : null;
                if (string.IsNullOrEmpty(delta))
                    continue;
                text.Append(delta);
                await onDelta(delta, cancellationToken).ConfigureAwait(false);
            }

            if (text.Length == 0)
                return Result.Failure<AiProviderExecutionResult>(Error.Failure("AI.GoogleEmptyResponse", "Google returned an empty response."));
            return Result.Success(new AiProviderExecutionResult(request.Model, text.ToString(), finishReason, inputTokens, outputTokens, totalTokens));
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return Result.Failure<AiProviderExecutionResult>(Error.Failure("AI.GoogleTimeout", "Google did not respond in time."));
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Google AI streaming request failed for tenant {TenantId}", request.TenantId);
            return Result.Failure<AiProviderExecutionResult>(Error.Failure("AI.GoogleRequestFailed", "Failed to execute the Google AI streaming request."));
        }
    }

    public async Task<Result<AiProviderExecutionResult>> CompleteAsync(AiResolvedRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            var payload = new
            {
                systemInstruction = string.IsNullOrWhiteSpace(request.SystemPrompt)
                    ? null
                    : new
                    {
                        parts = new[]
                        {
                            new { text = request.SystemPrompt }
                        }
                    },
                contents = request.Messages.Select(static message => new
                {
                    role = message.Role.Equals("assistant", StringComparison.OrdinalIgnoreCase) ? "model" : "user",
                    parts = new[]
                    {
                        new { text = message.Content }
                    }
                }),
                generationConfig = new
                {
                    temperature = request.Temperature,
                    maxOutputTokens = request.MaxTokens
                }
            };

            var endpoint = $"{request.BaseUrl.TrimEnd('/')}/v1beta/models/{Uri.EscapeDataString(request.Model)}:generateContent?key={Uri.EscapeDataString(request.ApiKey)}";

            using var httpRequest = new HttpRequestMessage(HttpMethod.Post, endpoint)
            {
                Content = new StringContent(JsonSerializer.Serialize(payload, SerializerOptions), Encoding.UTF8, "application/json")
            };

            var client = httpClientFactory.CreateClient();
            using var response = await client.SendAsync(httpRequest, cancellationToken).ConfigureAwait(false);
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
                return Result.Failure<AiProviderExecutionResult>(AiProviderErrorMapper.Map("Google", response.StatusCode, responseBody));

            using var document = JsonDocument.Parse(responseBody);
            var root = document.RootElement;
            if (!root.TryGetProperty("candidates", out var candidatesElement)
                || candidatesElement.ValueKind != JsonValueKind.Array
                || candidatesElement.GetArrayLength() == 0)
            {
                return Result.Failure<AiProviderExecutionResult>(Error.Failure("AI.GoogleEmptyResponse", "Google returned no candidates."));
            }

            var candidate = candidatesElement[0];
            var text = candidate.TryGetProperty("content", out var contentElement)
                && contentElement.TryGetProperty("parts", out var partsElement)
                    ? AiJsonHelpers.ExtractTextFromParts(partsElement)
                    : null;

            if (string.IsNullOrWhiteSpace(text))
                return Result.Failure<AiProviderExecutionResult>(Error.Failure("AI.GoogleEmptyResponse", "Google returned an empty response."));

            var usage = root.TryGetProperty("usageMetadata", out var usageElement) ? usageElement : default;

            return Result.Success(new AiProviderExecutionResult(
                request.Model,
                text,
                candidate.TryGetProperty("finishReason", out var finishReasonElement) ? finishReasonElement.GetString() : null,
                AiJsonHelpers.TryGetInt(usage, "promptTokenCount"),
                AiJsonHelpers.TryGetInt(usage, "candidatesTokenCount"),
                AiJsonHelpers.TryGetInt(usage, "totalTokenCount")));
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return Result.Failure<AiProviderExecutionResult>(Error.Failure("AI.GoogleTimeout", "Google did not respond in time."));
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Google AI request failed for tenant {TenantId}", request.TenantId);
            return Result.Failure<AiProviderExecutionResult>(Error.Failure("AI.GoogleRequestFailed", "Failed to execute the Google AI request."));
        }
    }
}
