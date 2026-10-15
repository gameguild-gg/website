using System.Net;
using System.Security.Cryptography;
using System.Text;
using FluentAssertions;
using GameGuild.Compliance.Audit;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace GameGuild.Tests.Audit.Unit.Services;

public sealed class AuditExportWebhookNotifierTests
{
    private static readonly string SigningSecret = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));

    [Fact]
    public void SigningFixture_UsesFullCryptographicKeyMaterial()
    {
        Convert.FromHexString(SigningSecret).Should().HaveCount(32);
        CreateOptions().SigningSecret.Should().Be(SigningSecret);
    }

    [Fact]
    public void ValidateWebhookUrl_ShouldRequireConfiguredHttpsAllowlistedHost()
    {
        var notifier = CreateNotifier(new RecordingHttpMessageHandler(), CreateOptions());

        notifier.ValidateWebhookUrl("https://hooks.example.com/audit").Should().BeNull();
        notifier.ValidateWebhookUrl("http://hooks.example.com/audit").Should().NotBeNull();
        notifier.ValidateWebhookUrl("https://other.example.com/audit").Should().Contain("not allowed");
        notifier.ValidateWebhookUrl("https://hooks.example.com/audit?token=secret").Should().Contain("query parameters");
        notifier.ValidateWebhookUrl("https://user:password@hooks.example.com/audit").Should().Contain("user information");
    }

    [Fact]
    public async Task NotifyAsync_ShouldSignPayloadAndRetryTransientResponses()
    {
        var handler = new RecordingHttpMessageHandler(HttpStatusCode.ServiceUnavailable, HttpStatusCode.NoContent);
        var notifier = CreateNotifier(handler, CreateOptions(maxAttempts: 2));
        var notification = new AuditExportWebhookNotification(
            "export-1:completed",
            "audit.export.completed",
            DateTimeOffset.UtcNow,
            Guid.NewGuid(),
            "csv",
            "completed",
            12,
            12,
            null);

        await notifier.NotifyAsync(
            "https://hooks.example.com/audit",
            notification,
            CancellationToken.None);

        handler.Requests.Should().HaveCount(2);
        handler.Requests[0].Headers["Idempotency-Key"].Should().Be("export-1:completed");
        handler.Requests[1].Headers["X-GameGuild-Audit-Event"].Should().Be("audit.export.completed");

        var signatureHeader = handler.Requests[1].Headers["X-GameGuild-Audit-Signature"];
        var signatureParts = signatureHeader.Split(',');
        var timestamp = signatureParts[0][2..];
        var signature = signatureParts[1][3..];
        var expectedSignature = Convert.ToHexString(HMACSHA256.HashData(
                Encoding.UTF8.GetBytes(SigningSecret),
                Encoding.UTF8.GetBytes($"{timestamp}.{handler.Requests[1].Body}")))
            .ToLowerInvariant();

        signature.Should().Be(expectedSignature);
    }

    [Fact]
    public async Task NotifyAsync_ShouldNotSendWhenNoCallbackWasRequested()
    {
        var handler = new RecordingHttpMessageHandler();
        var notifier = CreateNotifier(handler, CreateOptions());
        var notification = new AuditExportWebhookNotification(
            "export-2:completed",
            "audit.export.completed",
            DateTimeOffset.UtcNow,
            Guid.NewGuid(),
            "json",
            "completed",
            0,
            0,
            null);

        await notifier.NotifyAsync(null, notification, CancellationToken.None);

        handler.Requests.Should().BeEmpty();
    }

    private static AuditExportWebhookNotifier CreateNotifier(
        RecordingHttpMessageHandler handler,
        AuditExportWebhookOptions options)
    {
        return new AuditExportWebhookNotifier(
            new HttpClient(handler),
            Options.Create(options),
            NullLogger<AuditExportWebhookNotifier>.Instance);
    }

    private static AuditExportWebhookOptions CreateOptions(int maxAttempts = 1)
    {
        return new AuditExportWebhookOptions
        {
            SigningSecret = SigningSecret,
            AllowedHosts = ["hooks.example.com"],
            MaxAttempts = maxAttempts,
            RetryDelayMilliseconds = 0,
            TimeoutSeconds = 1
        };
    }

    private sealed class RecordingHttpMessageHandler(params HttpStatusCode[] statuses) : HttpMessageHandler
    {
        private readonly HttpStatusCode[] _statuses = statuses.Length == 0 ? [HttpStatusCode.NoContent] : statuses;

        public List<CapturedRequest> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = await request.Content!.ReadAsStringAsync(cancellationToken);
            var headers = request.Headers.ToDictionary(
                header => header.Key,
                header => string.Join(',', header.Value),
                StringComparer.OrdinalIgnoreCase);
            Requests.Add(new CapturedRequest(request.RequestUri!, body, headers));

            var status = _statuses[Math.Min(Requests.Count - 1, _statuses.Length - 1)];
            return new HttpResponseMessage(status);
        }
    }

    private sealed record CapturedRequest(Uri Uri, string Body, IReadOnlyDictionary<string, string> Headers);
}
