using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using GameGuild.API.Email;
using GameGuild.Email;
using Moq;
using SendGrid;
using SendGrid.Helpers.Mail;

namespace GameGuild.API.UnitTests.Email;

public sealed class EmailSenderTests
{
    [Fact]
    public void ResolveProvider_WhenProviderIsOmittedAndApiKeyExists_UsesSendGrid()
    {
        var method = typeof(EmailSender).GetMethod("ResolveProvider", BindingFlags.Static | BindingFlags.NonPublic);
        method.Should().NotBeNull();

        var provider = method!.Invoke(null, [new EmailDeliveryOptions { SendGridApiKey = "test-key" }]);

        provider.Should().Be("SendGrid");
    }

    [Fact]
    public async Task SendAsync_WhenEmailDeliveryIsDisabled_SkipsWithoutThrowing()
    {
        var sender = new EmailSender(
            Options.Create(new EmailDeliveryOptions
            {
                Enabled = false
            }),
            NullLogger<EmailSender>.Instance);

        Func<Task> act = () => sender.SendAsync(
            new EmailMessage(
                "user@example.com",
                "Welcome",
                "Plain",
                "<p>Html</p>",
                Attachments: []));

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task SendAsync_WhenProviderIsOmitted_UsesSmtpValidationPath()
    {
        var sender = new EmailSender(
            Options.Create(new EmailDeliveryOptions
            {
                Enabled = true,
                FromEmail = "no-reply@example.com"
            }),
            NullLogger<EmailSender>.Instance);

        Func<Task> act = () => sender.SendAsync(
            new EmailMessage(
                "user@example.com",
                "Welcome",
                "Plain",
                "<p>Html</p>",
                Attachments: []));

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("EmailDelivery:SmtpHost is required when email delivery uses SMTP.");
    }

    [Fact]
    public async Task SendAsync_WhenSendGridIsExplicit_RequiresSendGridApiKey()
    {
        var sender = new EmailSender(
            Options.Create(new EmailDeliveryOptions
            {
                Enabled = true,
                Provider = "SendGrid",
                FromEmail = "no-reply@example.com"
            }),
            NullLogger<EmailSender>.Instance);

        Func<Task> act = () => sender.SendAsync(
            new EmailMessage(
                "user@example.com",
                "Welcome",
                "Plain",
                "<p>Html</p>",
                Attachments: []));

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("EmailDelivery:SendGridApiKey is required when email delivery uses SendGrid.");
    }

    [Fact]
    public async Task SendAsync_WhenFromEmailIsMissing_RejectsBeforeSelectingProvider()
    {
        var sender = CreateSender(new EmailDeliveryOptions { Enabled = true });

        Func<Task> act = () => sender.SendAsync(CreateMessage());

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("EmailDelivery:FromEmail is required to send email.");
    }

    [Fact]
    public async Task SendAsync_WhenProviderIsUnsupported_RejectsConfiguration()
    {
        var sender = CreateSender(new EmailDeliveryOptions
        {
            Enabled = true,
            Provider = " unsupported ",
            FromEmail = "no-reply@example.com"
        });

        Func<Task> act = () => sender.SendAsync(CreateMessage());

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("EmailDelivery:Provider ' unsupported ' is not supported. Use 'Ses', 'SendGrid' or 'Smtp'.");
    }

    [Fact]
    public async Task SendAsync_WhenMessageIsNull_RejectsArgument()
    {
        var sender = CreateSender(new EmailDeliveryOptions());

        Func<Task> act = () => sender.SendAsync(null!);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task SendAsync_WhenSendGridAcceptsMessage_PreservesNamesAndAttachments()
    {
        SendGridMessage? capturedMessage = null;
        var client = new Mock<ISendGridClient>(MockBehavior.Strict);
        client.Setup(value => value.SendEmailAsync(It.IsAny<SendGridMessage>(), It.IsAny<CancellationToken>()))
            .Callback<SendGridMessage, CancellationToken>((message, _) => capturedMessage = message)
            .ReturnsAsync(CreateSendGridResponse(HttpStatusCode.Accepted, "message-123"));
        var sender = CreateSender(
            CreateSendGridOptions(fromName: "Platform Mail"),
            _ => client.Object);
        var attachment = new EmailAttachment("statement.txt", "text/plain", "attachment"u8.ToArray());

        var providerMessageId = await sender.SendAsync(CreateMessage(toName: "Account Owner", attachments: [attachment]));

        providerMessageId.Should().Be("message-123");
        capturedMessage.Should().NotBeNull();
        capturedMessage!.From.Email.Should().Be("no-reply@example.com");
        capturedMessage.From.Name.Should().Be("Platform Mail");
        capturedMessage.Personalizations.Single().Tos.Single().Name.Should().Be("Account Owner");
        capturedMessage.Attachments.Should().ContainSingle()
            .Which.Content.Should().Be(Convert.ToBase64String(attachment.Content));
        client.VerifyAll();
    }

    [Fact]
    public async Task SendAsync_WhenOptionalSendGridNamesAndAttachmentsAreMissing_UsesEmailFallbacks()
    {
        SendGridMessage? capturedMessage = null;
        var client = new Mock<ISendGridClient>(MockBehavior.Strict);
        client.Setup(value => value.SendEmailAsync(It.IsAny<SendGridMessage>(), It.IsAny<CancellationToken>()))
            .Callback<SendGridMessage, CancellationToken>((message, _) => capturedMessage = message)
            .ReturnsAsync(CreateSendGridResponse(HttpStatusCode.OK));
        var sender = CreateSender(CreateSendGridOptions(), _ => client.Object);

        var providerMessageId = await sender.SendAsync(CreateMessage());

        providerMessageId.Should().BeNull();
        capturedMessage!.From.Name.Should().Be("no-reply@example.com");
        capturedMessage.Personalizations.Single().Tos.Single().Name.Should().Be("user@example.com");
        capturedMessage.Attachments.Should().BeNullOrEmpty();
    }

    [Fact]
    public async Task SendAsync_WhenSendGridRejectsMessage_ReportsStatusCode()
    {
        var client = new Mock<ISendGridClient>(MockBehavior.Strict);
        client.Setup(value => value.SendEmailAsync(It.IsAny<SendGridMessage>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateSendGridResponse(HttpStatusCode.BadRequest));
        var sender = CreateSender(CreateSendGridOptions(), _ => client.Object);

        Func<Task> act = () => sender.SendAsync(CreateMessage());

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("SendGrid rejected email delivery with status code 400.");
    }

    [Fact]
    public async Task SendAsync_WhenSendGridCallIsCancelled_DoesNotInvokeProvider()
    {
        var client = new Mock<ISendGridClient>(MockBehavior.Strict);
        var sender = CreateSender(CreateSendGridOptions(), _ => client.Object);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        Func<Task> act = () => sender.SendAsync(CreateMessage(), cancellation.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
        client.Verify(value => value.SendEmailAsync(It.IsAny<SendGridMessage>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SendAsync_WhenSmtpIsConfigured_DeliversMultipartMessageWithAttachment()
    {
        await using var server = new MiniSmtpServer();
        var sender = CreateSender(new EmailDeliveryOptions
        {
            Enabled = true,
            Provider = "Smtp",
            FromEmail = "no-reply@example.com",
            FromName = "Platform Mail",
            SmtpHost = "127.0.0.1",
            SmtpPort = server.Port,
            SmtpUsername = "smtp-user",
            SmtpPassword = "smtp-password"
        });

        await sender.SendAsync(CreateMessage(
            toName: "Account Owner",
            attachments: [new EmailAttachment("statement.txt", "text/plain", "attachment"u8.ToArray())]));
        var transcript = await server.Completion.WaitAsync(TimeSpan.FromSeconds(5));

        transcript.Should().Contain("Subject: Delivery test");
        transcript.Should().Contain("name=statement.txt");
    }

    [Fact]
    public async Task SendAsync_WhenSmtpNamesCredentialsAndAttachmentsAreMissing_UsesFallbacks()
    {
        await using var server = new MiniSmtpServer();
        var sender = CreateSender(new EmailDeliveryOptions
        {
            Enabled = true,
            Provider = "Smtp",
            FromEmail = "no-reply@example.com",
            SmtpHost = "127.0.0.1",
            SmtpPort = server.Port
        });

        await sender.SendAsync(CreateMessage());
        var transcript = await server.Completion.WaitAsync(TimeSpan.FromSeconds(5));

        transcript.Should().Contain("From: \"no-reply@example.com\" <no-reply@example.com>");
        transcript.Should().Contain("To: \"user@example.com\" <user@example.com>");
    }

    [Fact]
    public async Task SendAsync_WhenSmtpPasswordIsMissing_UsesEmptyCredentialPassword()
    {
        await using var server = new MiniSmtpServer();
        var sender = CreateSender(new EmailDeliveryOptions
        {
            Enabled = true,
            Provider = "Smtp",
            FromEmail = "no-reply@example.com",
            SmtpHost = "127.0.0.1",
            SmtpPort = server.Port,
            SmtpUsername = "smtp-user"
        });

        await sender.SendAsync(CreateMessage());

        (await server.Completion.WaitAsync(TimeSpan.FromSeconds(5))).Should().Contain("Subject: Delivery test");
    }

    private static EmailSender CreateSender(
        EmailDeliveryOptions options,
        Func<string, ISendGridClient>? sendGridClientFactory = null) =>
        new(
            Options.Create(options),
            NullLogger<EmailSender>.Instance,
            sendGridClientFactory ?? (apiKey => new SendGridClient(apiKey)));

    private static EmailDeliveryOptions CreateSendGridOptions(string? fromName = null) => new()
    {
        Enabled = true,
        Provider = "SendGrid",
        SendGridApiKey = "test-key",
        FromEmail = "no-reply@example.com",
        FromName = fromName
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

    private static Response CreateSendGridResponse(HttpStatusCode statusCode, string? messageId = null)
    {
        var response = new HttpResponseMessage();
        if (messageId is not null)
            response.Headers.TryAddWithoutValidation("X-Message-Id", messageId);
        return new Response(statusCode, new StringContent(string.Empty), response.Headers);
    }

    private sealed class MiniSmtpServer : IAsyncDisposable
    {
        private readonly TcpListener listener = new(IPAddress.Loopback, 0);

        public MiniSmtpServer()
        {
            listener.Start();
            Port = ((IPEndPoint)listener.LocalEndpoint).Port;
            Completion = ServeAsync();
        }

        public int Port { get; }

        public Task<string> Completion { get; }

        public ValueTask DisposeAsync()
        {
            listener.Stop();
            return ValueTask.CompletedTask;
        }

        private async Task<string> ServeAsync()
        {
            using var client = await listener.AcceptTcpClientAsync();
            await using var stream = client.GetStream();
            using var reader = new StreamReader(stream, Encoding.ASCII, leaveOpen: true);
            await using var writer = new StreamWriter(stream, Encoding.ASCII, leaveOpen: true)
            {
                AutoFlush = true,
                NewLine = "\r\n"
            };
            var transcript = new StringBuilder();
            await writer.WriteLineAsync("220 localhost test server");

            while (await reader.ReadLineAsync() is { } line)
            {
                transcript.AppendLine(line);
                if (line.StartsWith("EHLO", StringComparison.OrdinalIgnoreCase)
                    || line.StartsWith("HELO", StringComparison.OrdinalIgnoreCase))
                {
                    await writer.WriteLineAsync("250-localhost");
                    await writer.WriteLineAsync("250 SIZE 10485760");
                }
                else if (line.Equals("DATA", StringComparison.OrdinalIgnoreCase))
                {
                    await writer.WriteLineAsync("354 End data with <CR><LF>.<CR><LF>");
                    while (await reader.ReadLineAsync() is { } dataLine && dataLine != ".")
                    {
                        transcript.AppendLine(dataLine);
                    }
                    await writer.WriteLineAsync("250 queued");
                }
                else if (line.Equals("QUIT", StringComparison.OrdinalIgnoreCase))
                {
                    await writer.WriteLineAsync("221 closing connection");
                    break;
                }
                else
                {
                    await writer.WriteLineAsync("250 ok");
                }
            }

            return transcript.ToString();
        }
    }
}
