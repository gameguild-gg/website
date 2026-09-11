using GameGuild;
using Microsoft.Extensions.Logging;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace GameGuild.AI;

internal sealed class AnthropicAdapter(IHttpClientFactory httpClientFactory, ILogger<AnthropicAdapter> logger) : IAiProviderAdapter
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public AiProvider Provider => AiProvider.Anthropic;

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
                model = request.Model,
                system = request.SystemPrompt,
                messages = request.Messages.Select(static message => new
                {
                    role = message.Role.Equals("assistant", StringComparison.OrdinalIgnoreCase) ? "assistant" : "user",
                    content = message.Content
                }),
                temperature = request.Temperature,
                max_tokens = request.MaxTokens ?? 1024,
                stream = true
            };

            using var httpRequest = new HttpRequestMessage(HttpMethod.Post, $"{request.BaseUrl.TrimEnd('/')}/v1/messages");
            httpRequest.Headers.Add("x-api-key", request.ApiKey);
            httpRequest.Headers.Add("anthropic-version", "2023-06-01");
            httpRequest.Headers.Accept.ParseAdd("text/event-stream");
            httpRequest.Content = new StringContent(JsonSerializer.Serialize(payload, SerializerOptions), Encoding.UTF8, "application/json");

            var client = httpClientFactory.CreateClient();
            using var response = await client.SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                return Result.Failure<AiProviderExecutionResult>(AiProviderErrorMapper.Map("Anthropic", response.StatusCode, errorBody));
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            using var reader = new StreamReader(stream);
            var text = new StringBuilder();
            var model = request.Model;
            string? finishReason = null;
            int? inputTokens = null;
            int? outputTokens = null;
            while (await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false) is { } line)
            {
                if (!line.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
                    continue;
                var data = line[5..].Trim();
                if (data.Length == 0 || data == "[DONE]")
                    continue;

                using var document = JsonDocument.Parse(data);
                var root = document.RootElement;
                var type = root.TryGetProperty("type", out var typeElement) ? typeElement.GetString() : null;
                if (type == "message_start" && root.TryGetProperty("message", out var message))
                {
                    if (message.TryGetProperty("model", out var modelElement) && modelElement.ValueKind == JsonValueKind.String)
                        model = modelElement.GetString() ?? model;
                    if (message.TryGetProperty("usage", out var usageElement))
                        inputTokens = AiJsonHelpers.TryGetInt(usageElement, "input_tokens");
                    continue;
                }
                if (type == "message_delta")
                {
                    if (root.TryGetProperty("usage", out var usageElement))
                        outputTokens = AiJsonHelpers.TryGetInt(usageElement, "output_tokens");
                    if (root.TryGetProperty("delta", out var messageDelta)
                        && messageDelta.TryGetProperty("stop_reason", out var stopReason)
                        && stopReason.ValueKind == JsonValueKind.String)
                        finishReason = stopReason.GetString();
                    continue;
                }
                if (type != "content_block_delta"
                    || !root.TryGetProperty("delta", out var contentDelta)
                    || !contentDelta.TryGetProperty("text", out var deltaText)
                    || deltaText.ValueKind != JsonValueKind.String)
                    continue;
                var delta = deltaText.GetString();
                if (string.IsNullOrEmpty(delta))
                    continue;
                text.Append(delta);
                await onDelta(delta, cancellationToken).ConfigureAwait(false);
            }

            if (text.Length == 0)
                return Result.Failure<AiProviderExecutionResult>(Error.Failure("AI.AnthropicEmptyResponse", "Anthropic returned an empty response."));
            var totalTokens = inputTokens.HasValue || outputTokens.HasValue
                ? (int?)((inputTokens ?? 0) + (outputTokens ?? 0))
                : null;
            return Result.Success(new AiProviderExecutionResult(model, text.ToString(), finishReason, inputTokens, outputTokens, totalTokens));
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return Result.Failure<AiProviderExecutionResult>(Error.Failure("AI.AnthropicTimeout", "Anthropic did not respond in time."));
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Anthropic streaming request failed for tenant {TenantId}", request.TenantId);
            return Result.Failure<AiProviderExecutionResult>(Error.Failure("AI.AnthropicRequestFailed", "Failed to execute the Anthropic streaming request."));
        }
    }

    public async Task<Result<AiProviderExecutionResult>> CompleteAsync(AiResolvedRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            var payload = new
            {
                model = request.Model,
                system = request.SystemPrompt,
                messages = request.Messages.Select(static message => new
                {
                    role = message.Role.Equals("assistant", StringComparison.OrdinalIgnoreCase) ? "assistant" : "user",
                    content = message.Content
                }),
                temperature = request.Temperature,
                max_tokens = request.MaxTokens ?? 1024
            };

            using var httpRequest = new HttpRequestMessage(HttpMethod.Post, $"{request.BaseUrl.TrimEnd('/')}/v1/messages");
            httpRequest.Headers.Add("x-api-key", request.ApiKey);
            httpRequest.Headers.Add("anthropic-version", "2023-06-01");
            httpRequest.Content = new StringContent(JsonSerializer.Serialize(payload, SerializerOptions), Encoding.UTF8, "application/json");

            var client = httpClientFactory.CreateClient();
            using var response = await client.SendAsync(httpRequest, cancellationToken).ConfigureAwait(false);
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
                return Result.Failure<AiProviderExecutionResult>(AiProviderErrorMapper.Map("Anthropic", response.StatusCode, responseBody));

            using var document = JsonDocument.Parse(responseBody);
            var root = document.RootElement;
            var contentArray = root.GetProperty("content");
            var text = AiJsonHelpers.ExtractTextFromParts(contentArray);
            if (string.IsNullOrWhiteSpace(text))
            {
                foreach (var contentPart in contentArray.EnumerateArray())
                {
                    if (contentPart.ValueKind == JsonValueKind.Object
                        && contentPart.TryGetProperty("type", out var typeElement)
                        && typeElement.GetString() == "text"
                        && contentPart.TryGetProperty("text", out var textElement))
                    {
                        text = textElement.GetString();
                        break;
                    }
                }
            }

            if (string.IsNullOrWhiteSpace(text))
                return Result.Failure<AiProviderExecutionResult>(Error.Failure("AI.AnthropicEmptyResponse", "Anthropic returned an empty response."));

            var usage = root.TryGetProperty("usage", out var usageElement) ? usageElement : default;

            return Result.Success(new AiProviderExecutionResult(
                root.TryGetProperty("model", out var modelElement) ? modelElement.GetString() ?? request.Model : request.Model,
                text,
                root.TryGetProperty("stop_reason", out var finishReasonElement) ? finishReasonElement.GetString() : null,
                AiJsonHelpers.TryGetInt(usage, "input_tokens"),
                AiJsonHelpers.TryGetInt(usage, "output_tokens"),
                CalculateTotalTokens(usage)));
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return Result.Failure<AiProviderExecutionResult>(Error.Failure("AI.AnthropicTimeout", "Anthropic did not respond in time."));
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Anthropic request failed for tenant {TenantId}", request.TenantId);
            return Result.Failure<AiProviderExecutionResult>(Error.Failure("AI.AnthropicRequestFailed", "Failed to execute the Anthropic request."));
        }
    }

    private static int? CalculateTotalTokens(JsonElement usageElement)
    {
        var inputTokens = AiJsonHelpers.TryGetInt(usageElement, "input_tokens");
        var outputTokens = AiJsonHelpers.TryGetInt(usageElement, "output_tokens");

        return inputTokens.HasValue || outputTokens.HasValue
            ? (inputTokens ?? 0) + (outputTokens ?? 0)
            : null;
    }
}
