using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace GameGuild.Assets.Openinary;

/// <summary>
/// Typed HTTP client for the Openinary asset CDN service.
/// All responses are parsed with relaxed (case-insensitive, extra-fields-ignored) JSON settings.
/// </summary>
public sealed class OpeninaryClient : IOpeninaryClient
{
    private static readonly JsonSerializerOptions JsonOptions = JsonSerializerOptions.Web;

    private readonly HttpClient _httpClient;
    private readonly OpeninaryOptions _options;

    public OpeninaryClient(HttpClient httpClient, IOptions<OpeninaryOptions> options)
    {
        _httpClient = httpClient;
        _options = options.Value;
    }

    public async Task<OpeninaryUploadResult> UploadAsync(
        Stream content,
        string fileName,
        string folder,
        IReadOnlyCollection<string> prewarmTransforms,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "upload");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey);

        using var multipart = new MultipartFormDataContent();
        var fileContent = new StreamContent(content);
        multipart.Add(fileContent, "files", fileName);
        multipart.Add(new StringContent(folder), "folder");
        multipart.Add(new StringContent(fileName), "names");
        foreach (var transform in prewarmTransforms)
        {
            multipart.Add(new StringContent(transform), "transformations");
        }

        request.Content = multipart;

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        var payload = await ReadJsonAsync<OpeninaryUploadResponse>(response, cancellationToken);

        var error = payload.Errors?.FirstOrDefault(entry =>
            string.Equals(entry.Filename, fileName, StringComparison.OrdinalIgnoreCase));
        if (error is not null)
        {
            throw new OpeninaryException(response.StatusCode,
                $"Openinary upload failed for '{fileName}': {error.Error}");
        }

        var file = payload.Files?.FirstOrDefault(entry =>
            string.Equals(entry.Filename, fileName, StringComparison.OrdinalIgnoreCase));
        if (file is null)
        {
            throw new OpeninaryException(response.StatusCode,
                $"Openinary upload response did not include file '{fileName}'.");
        }

        return new OpeninaryUploadResult(
            file.Path,
            file.Url,
            file.Size,
            file.PrewarmedUrls ?? []);
    }

    public async Task DeleteAsync(string path, CancellationToken cancellationToken) =>
        await SendExpectSuccessAsync(HttpMethod.Delete, $"storage/{EscapePath(path)}", cancellationToken);

    public async Task InvalidateAsync(string path, CancellationToken cancellationToken) =>
        await SendExpectSuccessAsync(HttpMethod.Delete, $"invalidate/{EscapePath(path)}", cancellationToken);

    public async Task<OpeninaryFileMetadata> GetMetadataAsync(string path, CancellationToken cancellationToken)
    {
        using var response = await SendAsync(
            HttpMethod.Get, $"storage/{EscapePath(path)}/metadata", cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        var payload = await ReadJsonAsync<OpeninaryMetadataResponse>(response, cancellationToken);
        return new OpeninaryFileMetadata(payload.Path, payload.Size, payload.CreatedAt, payload.UpdatedAt);
    }

    private async Task SendExpectSuccessAsync(
        HttpMethod method, string relativeUrl, CancellationToken cancellationToken)
    {
        using var response = await SendAsync(method, relativeUrl, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
    }

    private async Task<HttpResponseMessage> SendAsync(
        HttpMethod method, string relativeUrl, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(method, relativeUrl);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey);
        return await _httpClient.SendAsync(request, cancellationToken);
    }

    private static async Task EnsureSuccessAsync(
        HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var message = await TryReadErrorMessageAsync(response, cancellationToken);
        throw new OpeninaryException(response.StatusCode, message);
    }

    private static async Task<T> ReadJsonAsync<T>(
        HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            var payload = await response.Content.ReadFromJsonAsync<T>(JsonOptions, cancellationToken);
            return payload ?? throw new OpeninaryException(
                response.StatusCode, "Openinary returned an empty response body.");
        }
        catch (JsonException exception)
        {
            throw new OpeninaryException(
                response.StatusCode, $"Openinary returned an unparseable response body: {exception.Message}");
        }
    }

    private static async Task<string> TryReadErrorMessageAsync(
        HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            var error = await response.Content.ReadFromJsonAsync<OpeninaryErrorBody>(JsonOptions, cancellationToken);
            return string.IsNullOrWhiteSpace(error?.Error)
                ? $"Openinary request failed with status code {(int)response.StatusCode}."
                : error.Error;
        }
        catch (JsonException)
        {
            return $"Openinary request failed with status code {(int)response.StatusCode}.";
        }
    }

    private static string EscapePath(string path) =>
        string.Join("/", path.Split('/').Select(Uri.EscapeDataString));

    private sealed record OpeninaryUploadResponse(
        bool? Success,
        IReadOnlyList<OpeninaryUploadedFile>? Files,
        IReadOnlyList<OpeninaryUploadError>? Errors);

    private sealed record OpeninaryUploadedFile(
        string Filename,
        string Path,
        long Size,
        string Url,
        IReadOnlyList<string>? PrewarmedUrls);

    private sealed record OpeninaryUploadError(string Filename, string Error);

    private sealed record OpeninaryMetadataResponse(
        string Path,
        long Size,
        DateTimeOffset CreatedAt,
        DateTimeOffset UpdatedAt);

    private sealed record OpeninaryErrorBody(string? Error);
}
