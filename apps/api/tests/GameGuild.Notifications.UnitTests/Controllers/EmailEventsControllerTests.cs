using System.Net;
using System.Text;
using System.Text.Json;
using Amazon.SimpleNotificationService.Util;
using FluentAssertions.Primitives;
using GameGuild.Email;
using GameGuild.Notifications.Services.Email;
using GameGuild.Notifications.UnitTests.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace GameGuild.Notifications.UnitTests.Controllers;

/// <summary>
/// EmailEventsController webhook tests. The verifier is mocked for flow assertions; hostile
/// envelope cases (unsigned/tampered/wrong-topic/garbage) run through the REAL verifier to prove
/// end-to-end rejection. Direct controller invocation (repo pattern) with a DefaultHttpContext
/// body, mirroring NotificationUnsubscribeControllerTests.
/// </summary>
public sealed class EmailEventsControllerTests
{
    private const string TopicArn = "arn:aws:sns:us-east-1:000000000000:email-events";
    private const string CertUrl = "https://sns.us-east-1.amazonaws.com/SimpleNotificationService-0000000000000000000000000000000000.pem";

    [Fact]
    public async Task Unsigned_But_Valid_Json_Envelope_Returns_401()
    {
        var context = CreateContext();
        var controller = CreateController(context, verifier: CreateRealVerifier(TopicArn), body: Envelope());

        var result = await controller.Receive();

        result.BeRejected(StatusCodes.Status401Unauthorized);
        context.EmailDeliveryEvents.Should().BeEmpty();
    }

    [Fact]
    public async Task Tampered_Payload_With_Copied_Signature_Returns_401()
    {
        var context = CreateContext();
        var controller = CreateController(context, verifier: CreateRealVerifier(TopicArn),
            body: Envelope(certUrl: "https://sns.no-such-region-zzz.amazonaws.com/SimpleNotificationService-bogus.pem"));

        var result = await controller.Receive();

        result.BeRejected(StatusCodes.Status401Unauthorized);
        context.EmailDeliveryEvents.Should().BeEmpty();
    }

    [Fact]
    public async Task Wrong_Topic_Arn_When_Configured_Returns_403()
    {
        var context = CreateContext();
        var controller = CreateController(context, verifier: CreateRealVerifier(TopicArn),
            body: Envelope(topicArn: "arn:aws:sns:eu-west-1:000000000000:other-topic"));

        var result = await controller.Receive();

        result.BeRejected(StatusCodes.Status403Forbidden);
        context.EmailDeliveryEvents.Should().BeEmpty();
    }

    [Fact]
    public async Task Verifier_Throwing_Input_Returns_401_Not_500()
    {
        // Simulates an SDK exception escaping the verifier contract — the controller must still
        // answer 401 (never an unhandled 500) for hostile input.
        var verifier = new Mock<ISnsMessageVerifier>();
        verifier.Setup(v => v.ValidateRequest(It.IsAny<string>())).Throws<InvalidOperationException>();
        var context = CreateContext();
        var controller = CreateController(context, verifier: verifier.Object, body: Envelope());

        var result = await controller.Receive();

        result.BeRejected(StatusCodes.Status401Unauthorized);
        context.EmailDeliveryEvents.Should().BeEmpty();
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("")]
    [InlineData("{\"half\":\"baked\"")]
    public async Task Garbage_Body_Returns_400(string garbage)
    {
        var context = CreateContext();
        var controller = CreateController(context, verifier: CreateRealVerifier(TopicArn), body: garbage);

        var result = await controller.Receive();

        result.BeRejected(StatusCodes.Status400BadRequest);
        context.EmailDeliveryEvents.Should().BeEmpty();
    }

    [Fact]
    public async Task Unknown_Envelope_Type_Returns_400()
    {
        var verifier = new Mock<ISnsMessageVerifier>();
        verifier.Setup(v => v.ValidateRequest(It.IsAny<string>()))
            .Returns(new SnsVerificationResult.Valid(Message.ParseMessage(Envelope(type: "UnsubscribeConfirmation"))));
        var context = CreateContext();
        var controller = CreateController(context, verifier: verifier.Object, body: Envelope(type: "UnsubscribeConfirmation"));

        var result = await controller.Receive();

        result.BeRejected(StatusCodes.Status400BadRequest);
        context.EmailDeliveryEvents.Should().BeEmpty();
    }

    [Fact]
    public async Task SubscriptionConfirmation_Fetches_SubscribeUrl_And_Returns_200()
    {
        var subscribeUrl = "https://sns.us-east-1.amazonaws.com/confirm?token=abc123";
        var handler = new RecordingHandler();
        var verifier = new Mock<ISnsMessageVerifier>();
        verifier.Setup(v => v.ValidateRequest(It.IsAny<string>()))
            .Returns(new SnsVerificationResult.Valid(Message.ParseMessage(Envelope(type: "SubscriptionConfirmation", subscribeUrl: subscribeUrl))));
        var context = CreateContext();
        var controller = CreateController(context, verifier: verifier.Object,
            body: Envelope(type: "SubscriptionConfirmation", subscribeUrl: subscribeUrl), handler: handler);

        var result = await controller.Receive();

        result.Should().BeOfType<OkResult>();
        handler.Requests.Should().ContainSingle().Which.Should().Be(subscribeUrl);
    }

    [Fact]
    public async Task SubscriptionConfirmation_With_Untrusted_SubscribeUrl_Is_Not_Fetched()
    {
        var evilUrl = "https://evil.example.com/confirm?token=abc123";
        var handler = new RecordingHandler();
        var verifier = new Mock<ISnsMessageVerifier>();
        verifier.Setup(v => v.ValidateRequest(It.IsAny<string>()))
            .Returns(new SnsVerificationResult.Valid(Message.ParseMessage(Envelope(type: "SubscriptionConfirmation", subscribeUrl: evilUrl))));
        var context = CreateContext();
        var controller = CreateController(context, verifier: verifier.Object,
            body: Envelope(type: "SubscriptionConfirmation", subscribeUrl: evilUrl), handler: handler);

        var result = await controller.Receive();

        result.BeRejected(StatusCodes.Status400BadRequest);
        handler.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task SubscriptionConfirmation_Fetch_Failure_Returns_502()
    {
        var handler = new RecordingHandler(statusCode: HttpStatusCode.InternalServerError);
        var verifier = new Mock<ISnsMessageVerifier>();
        verifier.Setup(v => v.ValidateRequest(It.IsAny<string>()))
            .Returns(new SnsVerificationResult.Valid(Message.ParseMessage(Envelope(type: "SubscriptionConfirmation", subscribeUrl: "https://sns.us-east-1.amazonaws.com/confirm"))));
        var context = CreateContext();
        var controller = CreateController(context, verifier: verifier.Object,
            body: Envelope(type: "SubscriptionConfirmation", subscribeUrl: "https://sns.us-east-1.amazonaws.com/confirm"), handler: handler);

        var result = await controller.Receive();

        result.BeRejected(StatusCodes.Status502BadGateway);
    }

    [Fact]
    public async Task Valid_Notification_Stores_Stripped_Event_With_Ses_Timestamp()
    {
        // Open event carries ipAddress/userAgent in real SES — the stored payload must not.
        var sesEvent = SesEvent(eventType: "Open",
            mail: """{"messageId":"ses-outbound-42","destination":["Member@Example.COM"],"timestamp":"2026-03-04T05:06:07.000Z"}""",
            extra: ""","open":{"ipAddress":"203.0.113.7","userAgent":"Mozilla/5.0 (Macintosh)","timestamp":"2026-03-04T05:06:08.000Z"}""");
        var processor = new Mock<IEmailEventProcessor>();
        var context = CreateContext();
        var controller = CreateController(context, body: NotificationBody(sesEvent), processor: processor.Object);

        var result = await controller.Receive();

        result.Should().BeOfType<OkResult>();
        var row = await context.EmailDeliveryEvents.SingleAsync();
        row.ProviderMessageId.Should().Be("ses-outbound-42");
        row.RecipientEmail.Should().Be("member@example.com"); // normalized
        row.EventType.Should().Be(EmailDeliveryEventType.Open);
        row.OccurredAt.Should().Be(new DateTime(2026, 3, 4, 5, 6, 7, DateTimeKind.Utc));
        row.SnsMessageId.Should().Be("sns-message-id-1");
        row.Payload.Should().NotContain("ipAddress").And.NotContain("userAgent");
        processor.Verify(p => p.ProcessAsync(It.Is<EmailDeliveryEvent>(e => e.Id == row.Id), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Bounce_Event_Stores_BounceType_And_DiagnosticCode()
    {
        var sesEvent = SesEvent(eventType: "Bounce",
            mail: """{"messageId":"ses-bounce-7","destination":["bounced@example.com"],"timestamp":"2026-03-04T05:06:07.000Z"}""",
            extra: ""","bounce":{"bounceType":"Permanent","bounceSubType":"General","bouncedRecipients":[{"emailAddress":"bounced@example.com","action":"failed","status":"5.1.1","diagnosticCode":"smtp; 550 5.1.1 user unknown"}]}""");
        var context = CreateContext();
        var controller = CreateController(context, body: NotificationBody(sesEvent), processor: new Mock<IEmailEventProcessor>().Object);

        var result = await controller.Receive();

        result.Should().BeOfType<OkResult>();
        var row = await context.EmailDeliveryEvents.SingleAsync();
        row.EventType.Should().Be(EmailDeliveryEventType.Bounce);
        row.BounceType.Should().Be("Permanent");
        row.DiagnosticCode.Should().Be("smtp; 550 5.1.1 user unknown");
    }

    [Fact]
    public async Task Garbage_Inner_Ses_Event_Returns_400_Without_Storing()
    {
        var context = CreateContext();
        var controller = CreateController(context, body: NotificationBody("not-json"));

        var result = await controller.Receive();

        result.BeRejected(StatusCodes.Status400BadRequest);
        context.EmailDeliveryEvents.Should().BeEmpty();
    }

    [Fact]
    public async Task Oversized_Payload_Is_Stored_As_Valid_Json_Preview_Wrapper()
    {
        var big = new string('x', 5000);
        var sesEvent = SesEvent(eventType: "Delivery",
            mail: $$"""{"messageId":"ses-big-1","destination":["member@example.com"],"timestamp":"2026-03-04T05:06:07.000Z","headers":["{{big}}"]}""");
        var context = CreateContext();
        var controller = CreateController(context, body: NotificationBody(sesEvent), processor: new Mock<IEmailEventProcessor>().Object);

        var result = await controller.Receive();

        result.Should().BeOfType<OkResult>();
        var row = await context.EmailDeliveryEvents.SingleAsync();
        using var payload = JsonDocument.Parse(row.Payload!);
        var preview = payload.RootElement.GetProperty("preview").GetString();
        preview.Should().HaveLength(4000);
    }

    [Fact]
    public async Task Duplicate_SnsMessageId_Skips_Insert_And_Re_Runs_Processor()
    {
        var processor = new Mock<IEmailEventProcessor>();
        var context = CreateContext();
        context.EmailDeliveryEvents.Add(EmailDeliveryEvent.Create(
            "ses-1", "member@example.com", EmailDeliveryEventType.Delivery,
            new DateTime(2026, 3, 4, 5, 6, 7, DateTimeKind.Utc), "sns-message-id-1"));
        await context.SaveChangesAsync();
        var controller = CreateController(context, body: NotificationBody(SesEvent("Delivery")), processor: processor.Object);

        var result = await controller.Receive();

        result.Should().BeOfType<OkResult>();
        (await context.EmailDeliveryEvents.CountAsync()).Should().Be(1);
        processor.Verify(p => p.ProcessAsync(It.IsAny<EmailDeliveryEvent>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Concurrent_Duplicate_Unique_Violation_Is_Acked_After_Processor_Re_Run()
    {
        // Simulates the unique-index race: the row exists (prior request won the race) and the
        // save throws DbUpdateException. The controller must drop the failed insert, recognize the
        // duplicate, re-run the processor idempotently and ack — NOT 500 (SNS would retry forever).
        var processor = new Mock<IEmailEventProcessor>();
        var context = CreateContext();
        context.EmailDeliveryEvents.Add(EmailDeliveryEvent.Create(
            "ses-1", "member@example.com", EmailDeliveryEventType.Delivery,
            new DateTime(2026, 3, 4, 5, 6, 7, DateTimeKind.Utc), "sns-message-id-1"));
        await context.SaveChangesAsync();
        var contextAdapter = new ThrowOnceOnSaveAdapter(context);
        var controller = CreateController(contextAdapter, body: NotificationBody(SesEvent("Delivery")), processor: processor.Object);

        var result = await controller.Receive();

        result.Should().BeOfType<OkResult>();
        (await context.EmailDeliveryEvents.CountAsync()).Should().Be(1);
        processor.Verify(p => p.ProcessAsync(It.IsAny<EmailDeliveryEvent>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task NonDuplicate_Save_Failure_Returns_500_And_Persists_Nothing()
    {
        // A genuine ingest failure (save always throws, no pre-existing row) must surface as 500
        // with NOTHING persisted — SNS retry re-ingests cleanly.
        var context = CreateContext();
        var contextAdapter = new ThrowOnceOnSaveAdapter(context);
        var controller = CreateController(contextAdapter, body: NotificationBody(SesEvent("Delivery")), processor: new Mock<IEmailEventProcessor>().Object);

        var result = await controller.Receive();

        result.BeRejected(StatusCodes.Status500InternalServerError);
        (await context.EmailDeliveryEvents.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Processor_Throwing_Returns_500_And_Persists_Nothing()
    {
        var processor = new Mock<IEmailEventProcessor>();
        processor.Setup(p => p.ProcessAsync(It.IsAny<EmailDeliveryEvent>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("suppression exploded"));
        var context = CreateContext();
        var controller = CreateController(context, body: NotificationBody(SesEvent("Bounce", bounceType: "Permanent")), processor: processor.Object);

        var result = await controller.Receive();

        result.BeRejected(StatusCodes.Status500InternalServerError);
        (await context.EmailDeliveryEvents.CountAsync()).Should().Be(0);
        (await context.EmailSuppressions.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Processing_Failure_Rolls_Back_Event_And_Suppression_Atomically()
    {
        // Real processor (T5) adds the suppression; the save fails — NOTHING may persist, proving
        // the single-transaction contract (event row AND suppression both absent).
        var context = CreateContext();
        var contextAdapter = new ThrowOnceOnSaveAdapter(context);
        var realProcessor = new EmailEventProcessor(contextAdapter, NullLogger<EmailEventProcessor>.Instance);
        var controller = CreateController(contextAdapter,
            body: NotificationBody(SesEvent("Bounce", bounceType: "Permanent")), processor: realProcessor);

        var result = await controller.Receive();

        result.BeRejected(StatusCodes.Status500InternalServerError);
        (await context.EmailDeliveryEvents.CountAsync()).Should().Be(0);
        (await context.EmailSuppressions.CountAsync()).Should().Be(0);
    }

    private static NotificationsTestDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<NotificationsTestDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new NotificationsTestDbContext(options);
    }

    private static EmailEventsController CreateController(
        IApplicationDbContext context,
        string body,
        ISnsMessageVerifier? verifier = null,
        IEmailEventProcessor? processor = null,
        RecordingHandler? handler = null)
    {
        var httpClientFactory = new Mock<IHttpClientFactory>();
        var httpClient = handler is null ? new HttpClient() : new HttpClient(handler);
        httpClientFactory.Setup(f => f.CreateClient(EmailEventsController.SubscriptionConfirmationClientName)).Returns(httpClient);

        var emailDeliveryHandler = new EmailDeliveryMutationCommandHandler(
            Mock.Of<IEmailDeliveryAdminService>(),
            processor ?? new Mock<IEmailEventProcessor>().Object,
            context);
        var controller = new EmailEventsController(
            verifier ?? CreateVerifierMock(body).Object,
            new NotificationCommandTestSender(emailDeliveryHandler: emailDeliveryHandler),
            httpClientFactory.Object,
            NullLogger<EmailEventsController>.Instance);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                Request = { Body = new MemoryStream(Encoding.UTF8.GetBytes(body)) }
            }
        };
        return controller;
    }

    private static Mock<ISnsMessageVerifier> CreateVerifierMock(string body)
    {
        var verifier = new Mock<ISnsMessageVerifier>();
        verifier.Setup(v => v.ValidateRequest(It.IsAny<string>()))
            .Returns(new SnsVerificationResult.Valid(Message.ParseMessage(body)));
        return verifier;
    }

    private static ISnsMessageVerifier CreateRealVerifier(string topicArn)
    {
        var options = Options.Create(new EmailDeliveryOptions { Events = new EmailDeliveryOptions.EventsOptions { TopicArn = topicArn } });
        var environment = new Mock<IHostEnvironment>();
        environment.SetupGet(e => e.EnvironmentName).Returns("Development");
        return new SnsMessageVerifier(options, environment.Object, NullLogger<SnsMessageVerifier>.Instance);
    }

    /// <summary>Raw SNS envelope for a Notification-type message wrapping <paramref name="sesEvent"/>.</summary>
    private static string NotificationBody(string sesEvent) => Envelope(message: sesEvent);

    /// <summary>Hand-built SNS envelope fixture (signature is never actually valid).</summary>
    private static string Envelope(
        string? topicArn = TopicArn,
        string message = """{"eventType":"Send","mail":{"messageId":"ses-1","destination":["member@example.com"],"timestamp":"2026-03-04T05:06:07.000Z"}}""",
        string type = "Notification",
        string? certUrl = null,
        string? subscribeUrl = null) =>
        JsonSerializer.Serialize(new Dictionary<string, string?>
        {
            ["Type"] = type,
            ["MessageId"] = "sns-message-id-1",
            ["TopicArn"] = topicArn,
            ["Message"] = message,
            ["Timestamp"] = "2026-03-04T05:06:07.000Z",
            ["SignatureVersion"] = "1",
            ["Signature"] = "YWJjZGVmZw==",
            ["SigningCertURL"] = certUrl ?? CertUrl,
            ["SubscribeURL"] = subscribeUrl,
        });

    /// <summary>Inner SES event JSON: mail envelope + eventType (+ optional extra JSON properties).</summary>
    private static string SesEvent(string eventType, string mail, string? extra = null)
        => "{\"eventType\":\"" + eventType + "\",\"mail\":" + mail + (extra ?? "") + "}";

    private static string SesEvent(string eventType, string? bounceType = null)
    {
        var mail = "{\"messageId\":\"ses-" + Guid.NewGuid().ToString("N") + "\",\"destination\":[\"member@example.com\"],\"timestamp\":\"2026-03-04T05:06:07.000Z\"}";
        var bounce = bounceType is null ? "" : ",\"bounce\":{\"bounceType\":\"" + bounceType + "\"}";
        return "{\"eventType\":\"" + eventType + "\",\"mail\":" + mail + bounce + "}";
    }

    private sealed class RecordingHandler(HttpStatusCode statusCode = HttpStatusCode.OK) : HttpMessageHandler
    {
        public List<string> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request.RequestUri!.ToString());
            return Task.FromResult(new HttpResponseMessage(statusCode));
        }
    }

    /// <summary>
    /// IApplicationDbContext wrapper whose first SaveChangesAsync throws DbUpdateException —
    /// simulates the unique-index violation (InMemory does not enforce unique indexes) and other
    /// save failures without touching the store.
    /// </summary>
    private sealed class ThrowOnceOnSaveAdapter(NotificationsTestDbContext inner) : IApplicationDbContext
    {
        private bool _thrown;

        public DbSet<T> Set<T>() where T : class => inner.Set<T>();

        public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            if (!_thrown)
            {
                _thrown = true;
                throw new DbUpdateException("Simulated unique constraint violation");
            }

            return await inner.SaveChangesAsync(cancellationToken);
        }

        public Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(Mock.Of<IDbContextTransaction>());
    }
}

internal static class ControllerResultAssertions
{
    public static void BeRejected(this IActionResult result, int statusCode)
    {
        var objectResult = result.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(statusCode);
        var problem = objectResult.Value.Should().BeOfType<ProblemDetails>().Subject;
        problem.Status.Should().Be(statusCode);
    }
}
