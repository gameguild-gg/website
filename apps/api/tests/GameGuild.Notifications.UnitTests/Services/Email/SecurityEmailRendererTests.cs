using GameGuild.Notifications.Services.Email;
using GameGuild.Notifications.Services.Email.Renderers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace GameGuild.Notifications.UnitTests.Services.Email;

public sealed class SecurityEmailRendererTests
{
    [Fact]
    public async Task RendersEncodedMessageAndTrustedRecoveryInstructionsWithoutMetadata()
    {
        var notification = Notification.Create(Guid.NewGuid(), NotificationType.Security,
            NotificationChannel.Email, "Account security alert", "<script>alert('x')</script> & containment",
            metadata: "{\"rawToken\":\"synthetic-secret-must-not-be-rendered\"}",
            priority: NotificationPriority.Urgent, recipientEmail: "synthetic@example.test");

        var message = await new SecurityEmailRenderer().RenderAsync(notification);

        message.Should().NotBeNull();
        message!.ToEmail.Should().BeEmpty();
        message.Subject.Should().Be(notification.Title);
        message.HtmlContent.Should().Contain("&lt;script&gt;").And.Contain("&amp; containment");
        message.HtmlContent.Should().NotContain("<script>").And.NotContain("synthetic-secret-must-not-be-rendered");
        message.PlainTextContent.Should().Contain(notification.Message).And.Contain("usual trusted address")
            .And.Contain("reset your password").And.NotContain("synthetic-secret-must-not-be-rendered");
        message.HtmlContent.Should().NotContain("unsubscribe");
    }

    [Fact]
    public async Task RejectsAnUnrelatedNotificationType()
    {
        var notification = Notification.Create(null, NotificationType.System,
            NotificationChannel.Email, "Title", "Body", recipientEmail: "synthetic@example.test");
        Func<Task> render = () => new SecurityEmailRenderer().RenderAsync(notification);
        await render.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task HonorsCancellationBeforeRendering()
    {
        var notification = Notification.Create(null, NotificationType.Security,
            NotificationChannel.Email, "Title", "Body", recipientEmail: "synthetic@example.test");
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        Func<Task> render = () => new SecurityEmailRenderer().RenderAsync(notification, cancellation.Token);
        await render.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public void ModuleRegistrationResolvesSecurityRendererThroughActualRegistry()
    {
        var services = new ServiceCollection();
        services.AddNotificationsModule();
        services.RemoveAll<IEmailFooterService>();
        services.AddScoped(_ => Mock.Of<IEmailFooterService>());
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var renderer = scope.ServiceProvider.GetRequiredService<IEmailRendererRegistry>().Resolve(NotificationType.Security);

        renderer.Should().BeOfType<SecurityEmailRenderer>();
        scope.ServiceProvider.GetServices<IEmailRenderer>().Count(r => r.Type == NotificationType.Security).Should().Be(1);
    }
}
