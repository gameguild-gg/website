using System.Net;

namespace GameGuild.Assets.Openinary;

/// <summary>
/// Retry delegating handler for transient Openinary failures (408 and 5xx):
/// up to three attempts with exponential backoff (0.5s / 1s).
/// </summary>
public sealed class OpeninaryRetryHandler : DelegatingHandler
{
    private const int MaxAttempts = 3;

    private static readonly TimeSpan[] BackoffDelays =
    [
        TimeSpan.FromMilliseconds(500),
        TimeSpan.FromSeconds(1)
    ];

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        await BufferRequestContentAsync(request, cancellationToken);
        for (var attempt = 1; ; attempt++)
        {
            var response = await base.SendAsync(request, cancellationToken);
            if (!IsTransient(response.StatusCode) || attempt >= MaxAttempts)
            {
                return response;
            }

            response.Dispose();
            await Task.Delay(BackoffDelays[attempt - 1], cancellationToken);
        }
    }

    /// <summary>
    /// Replaces stream-backed content with an in-memory copy so that
    /// retried requests can be replayed.
    /// </summary>
    private static async Task BufferRequestContentAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (request.Content is null)
        {
            return;
        }

        var buffered = await request.Content.ReadAsByteArrayAsync(cancellationToken);
        var replacement = new ByteArrayContent(buffered);
        foreach (var header in request.Content.Headers)
        {
            replacement.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        request.Content = replacement;
    }

    private static bool IsTransient(HttpStatusCode statusCode) =>
        statusCode == HttpStatusCode.RequestTimeout
        || ((int)statusCode >= 500 && (int)statusCode <= 599);
}
