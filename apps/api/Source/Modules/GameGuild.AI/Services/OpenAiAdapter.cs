using GameGuild;
using Microsoft.Extensions.Logging;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace GameGuild.AI;

internal sealed class OpenAiAdapter(IHttpClientFactory httpClientFactory, ILogger<OpenAiAdapter> logger) : IAiProviderAdapter
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public AiProvider Provider => AiProvider.OpenAi;

    public async Task<Result<AiProviderExecutionResult>> CompleteStreamingAsync(
        AiResolvedRequest request,
        Func<string, CancellationToken, ValueTask> onDelta,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(onDelta);
        try
        {
            var payloadMessages = new List<object>();
            if (!string.IsNullOrWhiteSpace(request.SystemPrompt))
                payloadMessages.Add(new { role = "system", content = request.SystemPrompt });
            payloadMessages.AddRange(request.Messages.Select(static message => new
            {
                role = message.Role.ToLowerInvariant(),
                content = message.Content
            }));

            var payload = new
            {
                model = request.Model,
                messages = payloadMessages,
                temperature = request.Temperature,
                max_tokens = request.MaxTokens,
                stream = true,
                stream_options = new { include_usage = true }
            };

            using var httpRequest = CreateRequest(request, payload, "text/event-stream");
            var client = httpClientFactory.CreateClient();
            using var response = await client.SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                return Result.Failure<AiProviderExecutionResult>(AiProviderErrorMapper.Map("OpenAI", response.StatusCode, errorBody));
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            using var reader = new StreamReader(stream);
            var text = new StringBuilder();
            var model = request.Model;
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
                if (root.TryGetProperty("model", out var modelElement) && modelElement.ValueKind == JsonValueKind.String)
                    model = modelElement.GetString() ?? model;
                if (root.TryGetProperty("usage", out var usageElement) && usageElement.ValueKind == JsonValueKind.Object)
                {
                    inputTokens = AiJsonHelpers.TryGetInt(usageElement, "prompt_tokens");
                    outputTokens = AiJsonHelpers.TryGetInt(usageElement, "completion_tokens");
                    totalTokens = AiJsonHelpers.TryGetInt(usageElement, "total_tokens");
                }
                if (!root.TryGetProperty("choices", out var choices) || choices.ValueKind != JsonValueKind.Array || choices.GetArrayLength() == 0)
                    continue;
                var choice = choices[0];
                if (choice.TryGetProperty("finish_reason", out var finishElement) && finishElement.ValueKind == JsonValueKind.String)
                    finishReason = finishElement.GetString();
                if (!choice.TryGetProperty("delta", out var deltaElement) || !deltaElement.TryGetProperty("content", out var contentElement))
                    continue;
                var delta = contentElement.ValueKind switch
                {
                    JsonValueKind.String => contentElement.GetString(),
                    JsonValueKind.Array => AiJsonHelpers.ExtractTextFromParts(contentElement),
                    _ => null
                };
                if (string.IsNullOrEmpty(delta))
                    continue;
                text.Append(delta);
                await onDelta(delta, cancellationToken).ConfigureAwait(false);
            }

            if (text.Length == 0)
                return Result.Failure<AiProviderExecutionResult>(Error.Failure("AI.OpenAiEmptyResponse", "OpenAI returned an empty response."));
            return Result.Success(new AiProviderExecutionResult(model, text.ToString(), finishReason, inputTokens, outputTokens, totalTokens));
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return Result.Failure<AiProviderExecutionResult>(Error.Failure("AI.OpenAiTimeout", "OpenAI did not respond in time."));
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "OpenAI streaming request failed for tenant {TenantId}", request.TenantId);
            return Result.Failure<AiProviderExecutionResult>(Error.Failure("AI.OpenAiRequestFailed", "Failed to execute the OpenAI streaming request."));
        }
    }

    public async Task<Result<AiProviderExecutionResult>> CompleteAsync(AiResolvedRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            var payloadMessages = new List<object>();
            if (!string.IsNullOrWhiteSpace(request.SystemPrompt))
            {
                payloadMessages.Add(new
                {
                    role = "system",
                    content = request.SystemPrompt
                });
            }

            payloadMessages.AddRange(request.Messages.Select(static message => new
            {
                role = message.Role.ToLowerInvariant(),
                content = message.Content
            }));

            var payload = new
            {
                model = request.Model,
                messages = payloadMessages,
                temperature = request.Temperature,
                max_tokens = request.MaxTokens
            };

            var client = httpClientFactory.CreateClient();
            var responseBody = string.Empty;
            System.Net.HttpStatusCode? lastStatusCode = null;

            for (var attempt = 1; attempt <= 3; attempt++)
            {
                using var httpRequest = CreateRequest(request, payload);
                using var response = await client.SendAsync(httpRequest, cancellationToken).ConfigureAwait(false);
                responseBody = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                lastStatusCode = response.StatusCode;

                if (response.IsSuccessStatusCode)
                {
                    break;
                }

                if (attempt < 3 && IsTransient(response.StatusCode))
                {
                    logger.LogWarning(
                        "Transient OpenAI failure for tenant {TenantId}. Attempt {Attempt}/3 returned {StatusCode}",
                        request.TenantId,
                        attempt,
                        response.StatusCode);

                    await Task.Delay(TimeSpan.FromMilliseconds(200 * attempt), cancellationToken).ConfigureAwait(false);
                    continue;
                }

                return Result.Failure<AiProviderExecutionResult>(AiProviderErrorMapper.Map("OpenAI", response.StatusCode, responseBody));
            }

            if (lastStatusCode.HasValue && IsTransient(lastStatusCode.Value) && string.IsNullOrWhiteSpace(responseBody))
                return Result.Failure<AiProviderExecutionResult>(Error.Failure("AI.OpenAiRetryFailed", "OpenAI did not return a successful response after retrying transient failures."));

            if (string.IsNullOrWhiteSpace(responseBody))
                return Result.Failure<AiProviderExecutionResult>(Error.Failure("AI.OpenAiEmptyResponse", "OpenAI returned an empty HTTP response."));

            using var document = JsonDocument.Parse(responseBody);
            var root = document.RootElement;
            if (!root.TryGetProperty("choices", out var choices) ||
                choices.ValueKind != JsonValueKind.Array ||
                choices.GetArrayLength() == 0)
            {
                return Result.Failure<AiProviderExecutionResult>(Error.Failure("AI.OpenAiMalformedResponse", "OpenAI returned no completion choices."));
            }

            var choice = choices[0];
            if (!choice.TryGetProperty("message", out var message))
            {
                return Result.Failure<AiProviderExecutionResult>(Error.Failure("AI.OpenAiMalformedResponse", "OpenAI returned a choice without a message."));
            }

            var text = message.TryGetProperty("content", out var contentElement)
                ? contentElement.ValueKind switch
                {
                    JsonValueKind.String => contentElement.GetString(),
                    JsonValueKind.Array => AiJsonHelpers.ExtractTextFromParts(contentElement),
                    _ => null
                }
                : null;

            if (string.IsNullOrWhiteSpace(text))
                return Result.Failure<AiProviderExecutionResult>(Error.Failure("AI.OpenAiEmptyResponse", "OpenAI returned an empty response."));

            var finishReason = choice.TryGetProperty("finish_reason", out var finishReasonElement)
                ? finishReasonElement.GetString()
                : null;
            var model = root.TryGetProperty("model", out var modelElement)
                ? modelElement.GetString() ?? request.Model
                : request.Model;
            var usage = root.TryGetProperty("usage", out var usageElement) ? usageElement : default;

            return Result.Success(new AiProviderExecutionResult(
                model,
                text,
                finishReason,
                AiJsonHelpers.TryGetInt(usage, "prompt_tokens"),
                AiJsonHelpers.TryGetInt(usage, "completion_tokens"),
                AiJsonHelpers.TryGetInt(usage, "total_tokens")));
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return Result.Failure<AiProviderExecutionResult>(Error.Failure("AI.OpenAiTimeout", "OpenAI did not respond in time."));
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "OpenAI request failed for tenant {TenantId}", request.TenantId);
            return Result.Failure<AiProviderExecutionResult>(Error.Failure("AI.OpenAiRequestFailed", "Failed to execute the OpenAI request."));
        }
    }

    private static HttpRequestMessage CreateRequest(AiResolvedRequest request, object payload, string accept = "application/json")
    {
        var httpRequest = new HttpRequestMessage(HttpMethod.Post, $"{request.BaseUrl.TrimEnd('/')}/v1/chat/completions");
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", request.ApiKey);
        httpRequest.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue(accept));
        httpRequest.Content = new StringContent(JsonSerializer.Serialize(payload, SerializerOptions), Encoding.UTF8, "application/json");
        return httpRequest;
    }

    private static bool IsTransient(System.Net.HttpStatusCode statusCode)
        => statusCode == System.Net.HttpStatusCode.TooManyRequests || (int)statusCode >= 500;
}
