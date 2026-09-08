using System.Net;
using Amazon.SimpleEmailV2;
using Amazon.SimpleEmailV2.Model;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using GameGuild.API.Email;
using GameGuild.Email;
using Moq;

namespace GameGuild.API.UnitTests.Email;

public sealed class SesEmailSenderTests
{
    [Fact]
    public async Task SharedSender_WhenSesConfigured_ReturnsTheProviderReceipt()
    {
        var options = Options.Create(CreateEnabledOptions());
        options.Value.Provider = "SES";
        var client = new Mock<IAmazonSimpleEmailServiceV2>();
        client.Setup(value => value.SendEmailAsync(It.IsAny<SendEmailRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SendEmailResponse { MessageId = "receipt" });
        var ses = new SesEmailSender(options, NullLogger<SesEmailSender>.Instance, _ => client.Object);
        var sender = new EmailSender(options, NullLogger<EmailSender>.Instance, ses);

        (await sender.SendAsync(CreateMessage())).Should().Be("receipt");
        client.Verify(value => value.SendEmailAsync(It.IsAny<SendEmailRequest>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData("recipient")]
    [InlineData("name")]
    [InlineData("subject")]
    [InlineData("filename")]
    [InlineData("contentType")]
    public async Task SendAsync_RejectsHeaderInjectionBeforeCreatingProviderClient(string field)
    {
        const string injection = "test\r\nBcc: attacker@example.com";
        var factory = new RecordingClientFactory();
        var sender = CreateSender(CreateEnabledOptions(), factory);
        var message = CreateMessage() with
        {
            ToEmail = field == "recipient" ? injection : "user@example.com",
            ToName = field == "name" ? injection : "Name",
            Subject = field == "subject" ? injection : "Subject",
            Attachments = [new EmailAttachment(field == "filename" ? injection : "test.txt", field == "contentType" ? injection : "text/plain", [1])]
        };
        await Assert.ThrowsAsync<ArgumentException>(() => sender.SendAsync(message));
        factory.RequestedRegions.Should().BeEmpty();
    }

    [Fact]
    public async Task SendAsync_WhenEmailDeliveryIsDisabled_ReturnsNullWithoutCreatingClient()
    {
        var factory = new RecordingClientFactory();
        var sender = CreateSender(new EmailDeliveryOptions
        {
            Enabled = false,
            FromEmail = "no-reply@example.com",
            Ses = { Region = "us-east-1" }
        }, factory);

        var result = await sender.SendAsync(CreateMessage());

        result.Should().BeNull();
        factory.RequestedRegions.Should().BeEmpty();
    }

    [Fact]
    public async Task SendAsync_WhenRegionIsMissing_FailsFast()
    {
        var sender = CreateSender(new EmailDeliveryOptions
        {
            Enabled = true,
            FromEmail = "no-reply@example.com"
        });

        Func<Task> act = () => sender.SendAsync(CreateMessage());

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("EmailDelivery:Ses:Region is required when email delivery is enabled.");
    }

    [Fact]
    public async Task SendAsync_WhenFromEmailIsMissing_RejectsBeforeSending()
    {
        var sender = CreateSender(new EmailDeliveryOptions
        {
            Enabled = true,
            Ses = { Region = "us-east-1" }
        });

        Func<Task> act = () => sender.SendAsync(CreateMessage());

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("EmailDelivery:FromEmail is required to send email.");
    }

    [Fact]
    public async Task SendAsync_WhenMessageIsNull_RejectsArgument()
    {
        var sender = CreateSender(new EmailDeliveryOptions());

        Func<Task> act = () => sender.SendAsync(null!);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task SendAsync_WhenSesAcceptsMessage_ReturnsProviderMessageId()
    {
        SendEmailRequest? capturedRequest = null;
        var client = new Mock<IAmazonSimpleEmailServiceV2>(MockBehavior.Strict);
        client.Setup(value => value.SendEmailAsync(It.IsAny<SendEmailRequest>(), It.IsAny<CancellationToken>()))
            .Callback<SendEmailRequest, CancellationToken>((request, _) => capturedRequest = request)
            .ReturnsAsync(new SendEmailResponse { MessageId = "ses-message-123" });
        var factory = new RecordingClientFactory(client.Object);
        var sender = CreateSender(CreateEnabledOptions(), factory);

        var result = await sender.SendAsync(CreateMessage(toName: "Account Owner"));

        result.Should().Be("ses-message-123");
        factory.RequestedRegions.Should().Equal("us-east-1");
        capturedRequest.Should().NotBeNull();
        capturedRequest!.FromEmailAddress.Should().Be("\"Platform Mail\" <no-reply@example.com>");
        capturedRequest.Destination.ToAddresses.Should().Equal("\"Account Owner\" <user@example.com>");
        capturedRequest.Content.Simple.Should().NotBeNull();
        capturedRequest.Content.Simple.Subject.Data.Should().Be("Delivery test");
        capturedRequest.Content.Simple.Subject.Charset.Should().Be("UTF-8");
        capturedRequest.Content.Simple.Body.Html.Data.Should().Be("<p>HTML content</p>");
        capturedRequest.Content.Simple.Body.Text.Data.Should().Be("Plain content");
        capturedRequest.Content.Raw.Should().BeNull();
        client.VerifyAll();
    }

    [Fact]
    public async Task SendAsync_WhenOptionalNamesAreMissing_UsesEmailFallbacks()
    {
        SendEmailRequest? capturedRequest = null;
        var client = new Mock<IAmazonSimpleEmailServiceV2>(MockBehavior.Strict);
        client.Setup(value => value.SendEmailAsync(It.IsAny<SendEmailRequest>(), It.IsAny<CancellationToken>()))
            .Callback<SendEmailRequest, CancellationToken>((request, _) => capturedRequest = request)
            .ReturnsAsync(new SendEmailResponse { MessageId = "ses-message-123" });
        var sender = CreateSender(CreateEnabledOptions(fromName: null), new RecordingClientFactory(client.Object));

        await sender.SendAsync(CreateMessage());

        capturedRequest!.FromEmailAddress.Should().Be("no-reply@example.com");
        capturedRequest.Destination.ToAddresses.Should().Equal("user@example.com");
    }

    [Fact]
    public async Task SendAsync_WhenConfigurationSetNameIsSet_AttachesItToRequest()
    {
        SendEmailRequest? capturedRequest = null;
        var client = new Mock<IAmazonSimpleEmailServiceV2>(MockBehavior.Strict);
        client.Setup(value => value.SendEmailAsync(It.IsAny<SendEmailRequest>(), It.IsAny<CancellationToken>()))
            .Callback<SendEmailRequest, CancellationToken>((request, _) => capturedRequest = request)
            .ReturnsAsync(new SendEmailResponse { MessageId = "ses-message-123" });
        var options = CreateEnabledOptions();
        options.Ses.ConfigurationSetName = "gameguild-events";
        var sender = CreateSender(options, new RecordingClientFactory(client.Object));

        await sender.SendAsync(CreateMessage());

        capturedRequest!.ConfigurationSetName.Should().Be("gameguild-events");
        client.VerifyAll();
    }

    [Fact]
    public async Task SendAsync_WhenMessageHasAttachments_SendsRawMimeWithBase64Attachment()
    {
        SendEmailRequest? capturedRequest = null;
        var client = new Mock<IAmazonSimpleEmailServiceV2>(MockBehavior.Strict);
        client.Setup(value => value.SendEmailAsync(It.IsAny<SendEmailRequest>(), It.IsAny<CancellationToken>()))
            .Callback<SendEmailRequest, CancellationToken>((request, _) => capturedRequest = request)
            .ReturnsAsync(new SendEmailResponse { MessageId = "ses-message-123" });
        var sender = CreateSender(CreateEnabledOptions(), new RecordingClientFactory(client.Object));
        var attachment = new EmailAttachment("statement.txt", "text/plain", "attachment"u8.ToArray());

        await sender.SendAsync(CreateMessage(toName: "Account Owner", attachments: [attachment]));

        capturedRequest!.Content.Simple.Should().BeNull();
        capturedRequest.Content.Raw.Should().NotBeNull();
        var raw = await ReadRawAsync(capturedRequest.Content.Raw.Data);
        raw.Should().Contain("From: \"Platform Mail\" <no-reply@example.com>");
        raw.Should().Contain("To: \"Account Owner\" <user@example.com>");
        raw.Should().Contain("Subject: Delivery test");
        raw.Should().Contain("Content-Type: text/plain; name=\"statement.txt\"");
        raw.Should().Contain($"Content-Disposition: attachment; filename=\"statement.txt\"");
        raw.Should().Contain(Convert.ToBase64String(attachment.Content));
        raw.Should().Contain("Content-Type: text/html; charset=UTF-8");
        client.VerifyAll();
    }

    [Fact]
    public async Task SendAsync_WhenSesRejectsMessage_ReportsStatusCode()
    {
        var client = new Mock<IAmazonSimpleEmailServiceV2>(MockBehavior.Strict);
        client.Setup(value => value.SendEmailAsync(It.IsAny<SendEmailRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new AmazonSimpleEmailServiceV2Exception("Rejected")
            {
                StatusCode = HttpStatusCode.BadRequest
            });
        var sender = CreateSender(CreateEnabledOptions(), new RecordingClientFactory(client.Object));

        Func<Task> act = () => sender.SendAsync(CreateMessage());

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Amazon SES rejected email delivery with status code 400: Rejected*")
            .WithInnerException<InvalidOperationException, AmazonSimpleEmailServiceV2Exception>();
    }

    [Fact]
    public async Task SendAsync_WhenCallIsCancelled_DoesNotInvokeProvider()
    {
        var client = new Mock<IAmazonSimpleEmailServiceV2>(MockBehavior.Strict);
        var sender = CreateSender(CreateEnabledOptions(), new RecordingClientFactory(client.Object));
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        Func<Task> act = () => sender.SendAsync(CreateMessage(), cancellation.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
        client.Verify(value => value.SendEmailAsync(It.IsAny<SendEmailRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private static SesEmailSender CreateSender(
        EmailDeliveryOptions options,
        RecordingClientFactory? factory = null) =>
        new(
            Options.Create(options),
            NullLogger<SesEmailSender>.Instance,
            (factory ?? new RecordingClientFactory()).Create);

    private static EmailDeliveryOptions CreateEnabledOptions(string? fromName = "Platform Mail") => new()
    {
        Enabled = true,
        FromEmail = "no-reply@example.com",
        FromName = fromName,
        Ses = { Region = " us-east-1 " }
    };

    private static EmailMessage CreateMessage(
        string? toName = null,
        IReadOnlyList<EmailAttachment>? attachments = null) =>
        new(
            "user@example.com",
            "Delivery test",
            "Plain content",
            "<p>HTML content</p>",
            toName,
            attachments);

    private static async Task<string> ReadRawAsync(MemoryStream data)
    {
        data.Position = 0;
        using var reader = new StreamReader(data);
        return await reader.ReadToEndAsync();
    }

    private sealed class RecordingClientFactory(IAmazonSimpleEmailServiceV2? client = null)
    {
        public List<string> RequestedRegions { get; } = [];

        public IAmazonSimpleEmailServiceV2 Create(string region)
        {
            RequestedRegions.Add(region);
            var result = client ?? new Mock<IAmazonSimpleEmailServiceV2>(MockBehavior.Strict).Object;
            Mock.Get(result).Setup(value => value.Dispose());
            return result;
        }
    }
}
