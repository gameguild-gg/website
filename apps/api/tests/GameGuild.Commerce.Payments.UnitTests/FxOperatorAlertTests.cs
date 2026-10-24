using FluentAssertions;
using GameGuild.Notifications;
using GameGuild.Notifications.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace GameGuild.Commerce.Payments.UnitTests;

public class FxOperatorAlertTests
{
    [Theory]
    [InlineData("none", false, true)]
    [InlineData("ecb", false, false)]
    [InlineData("ecb", true, true)]
    [InlineData("openexchangerates", false, false)]
    [InlineData("openexchangerates", true, true)]
    public void EnabledProvider_RequiresExplicitOperatorRecipients(string provider, bool configured, bool valid)
    {
        var options = new FxSyncOptions
        {
            Provider = provider,
            AlertRecipientIds = configured ? [Guid.NewGuid()] : [],
            OpenExchangeRates = new() { AppId = "fixture-app-id" },
        };
        new FxSyncOptionsValidator().Validate(null, options).Succeeded.Should().Be(valid);
    }

    [Fact]
    public void EmptyRecipientId_IsRejected()
    {
        new FxSyncOptionsValidator().Validate(null, new FxSyncOptions
        {
            Provider = "ecb", AlertRecipientIds = [Guid.Empty],
        }).Failed.Should().BeTrue();
    }

    [Fact]
    public async Task ConcurrentRepeatedAlerts_ReachEachDistinctOperatorOncePerKind()
    {
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        var deliveries = new List<(Guid Recipient, string Kind)>();
        var service = NotificationService((recipient, kind) =>
        {
            deliveries.Add((recipient, kind));
            return Success(recipient);
        });
        using var provider = Container(service);
        var alerter = Alerter(provider, [first, first, second]);

        await Task.WhenAll(Enumerable.Range(0, 5).Select(_ => alerter.RaiseSyncFailureAlertAsync("ecb", 3, "unavailable")));
        await alerter.RaiseStalenessAlertAsync(DateTime.UtcNow.AddDays(-3), 2);

        deliveries.Should().BeEquivalentTo(new[]
        {
            (first, "FxRateSyncRuns"), (second, "FxRateSyncRuns"),
            (first, "FxRateSnapshots"), (second, "FxRateSnapshots"),
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FailedRecipient_IsRetriedWithoutDuplicatingSuccessfulRecipients(bool throws)
    {
        var failed = Guid.NewGuid();
        var healthy = Guid.NewGuid();
        var attempts = new Dictionary<Guid, int>();
        var service = NotificationService((recipient, _) =>
        {
            attempts[recipient] = attempts.GetValueOrDefault(recipient) + 1;
            if (recipient == failed && attempts[recipient] == 1)
            {
                if (throws) throw new InvalidOperationException("temporary persistence failure");
                return Result.Failure<Notification>(Error.Failure("Fixture.Transient", "temporary delivery failure"));
            }
            return Success(recipient);
        });
        using var provider = Container(service);
        var alerter = Alerter(provider, [failed, healthy]);

        await alerter.RaiseSyncFailureAlertAsync("ecb", 3, "unavailable");
        await alerter.RaiseSyncFailureAlertAsync("ecb", 4, "unavailable");

        attempts[failed].Should().Be(2);
        attempts[healthy].Should().Be(1);
    }

    private static Result<Notification> Success(Guid recipient) => Result.Success(Notification.Create(
        recipient, NotificationType.System, NotificationChannel.InApp, "FX fixture", "delivery fixture"));

    private static INotificationService NotificationService(Func<Guid, string, Result<Notification>> deliver)
    {
        var service = new Mock<INotificationService>(MockBehavior.Strict);
        service.Setup(value => value.SendAsync(It.IsAny<Guid?>(), NotificationType.System, It.IsAny<string>(), It.IsAny<string>(),
                NotificationChannel.InApp, null, null, NotificationPriority.High, null, It.IsAny<string>(), It.IsAny<string>(), null,
                It.IsAny<CancellationToken>()))
            .Returns((Guid? recipient, NotificationType type, string title, string message, NotificationChannel channel,
                Guid? tenant, string? action, NotificationPriority priority, Guid? referenceId, string? referenceType,
                string? metadata, string? email, CancellationToken cancellation) => Task.FromResult(deliver(recipient!.Value, referenceType!)));
        return service.Object;
    }

    private static ServiceProvider Container(INotificationService service) => new ServiceCollection()
        .AddSingleton(service).BuildServiceProvider();

    private static NotificationFxRateSyncAlerter Alerter(ServiceProvider provider, Guid[] recipients) => new(
        provider.GetRequiredService<IServiceScopeFactory>(), NullLogger<NotificationFxRateSyncAlerter>.Instance,
        Options.Create(new FxSyncOptions { AlertRecipientIds = recipients }));
}
