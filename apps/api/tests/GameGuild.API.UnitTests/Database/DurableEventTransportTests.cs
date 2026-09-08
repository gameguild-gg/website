using FluentAssertions;
using System.Diagnostics.Metrics;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using GameGuild.API.Database;
using GameGuild.API.Eventing;
using GameGuild.Identity.Authorization;
using GameGuild.Identity.Tenants;

namespace GameGuild.API.UnitTests.Database;

public sealed class DurableEventTransportTests
{
    [Fact]
    public void EventTransportMetrics_WhenDispatchAndDeliveryAreRecorded_EmitsStableMetricNames()
    {
        // Given
        var measurements = new List<string>();
        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, meterListener) =>
        {
            if (instrument.Meter.Name == EventTransportMetrics.MeterName)
                meterListener.EnableMeasurementEvents(instrument);
        };
        listener.SetMeasurementEventCallback<long>((instrument, _, _, _) => measurements.Add(instrument.Name));
        listener.Start();

        // When
        EventTransportMetrics.RecordDispatchCycle(2, TimeSpan.FromMilliseconds(50));
        EventTransportMetrics.RecordConsumerCompletion("tenant.deactivated", "test.consumer");
        EventTransportMetrics.RecordConsumerFailure("tenant.deactivated", "test.consumer", deadLettered: true);

        // Then
        measurements.Should().BeEquivalentTo(
            "gameguild_outbox_messages_claimed_total",
            "gameguild_outbox_consumer_completions_total",
            "gameguild_outbox_consumer_failures_total",
            "gameguild_outbox_dead_letters_total");
    }

    [Fact]
    public async Task SaveChangesAsync_CapturesDurableEventInSameSave()
    {
        // Given
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        await using var context = new ApplicationDbContext(options);
        var tenant = NewTenant();
        var integrationEvent = NewEvent(tenant.Id);
        tenant.AddIntegrationEvent(integrationEvent);
        context.Add(tenant);

        // When
        await context.SaveChangesAsync();

        // Then
        var persisted = await context.Set<OutboxMessage>().SingleAsync();
        persisted.EventId.Should().Be(integrationEvent.EventId);
        persisted.TenantId.Should().Be(tenant.Id);
        persisted.AggregateId.Should().Be(tenant.Id.ToString());
        persisted.SchemaVersion.Should().Be(1);
        persisted.Payload.Should().Contain("tenant.deactivated");
        tenant.IntegrationEvents.Should().BeEmpty();
    }

    [Fact]
    public async Task SaveChangesAsync_WhenFirstPersistenceAttemptFails_RetryPersistsSameDurableEvent()
    {
        // Given
        var interceptor = new RejectFirstSaveInterceptor();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .AddInterceptors(interceptor)
            .Options;
        await using var context = new ApplicationDbContext(options);
        var tenant = NewTenant();
        tenant.AddIntegrationEvent(NewEvent(tenant.Id));
        var eventId = tenant.IntegrationEvents.Single().EventId;
        context.Add(tenant);

        // When
        var firstAttempt = () => context.SaveChangesAsync();

        // Then
        await firstAttempt.Should().ThrowAsync<DbUpdateException>();
        tenant.IntegrationEvents.Should().ContainSingle();
        await context.SaveChangesAsync();
        (await context.Set<OutboxMessage>().SingleAsync()).EventId.Should().Be(eventId);
        tenant.IntegrationEvents.Should().BeEmpty();
    }

    [Fact]
    public async Task DispatchPendingAsync_WhenOneConsumerFails_RetriesOnlyThatConsumer()
    {
        // Given
        var databaseName = Guid.NewGuid().ToString();
        var clock = new MutableTimeProvider(new DateTimeOffset(2026, 8, 26, 12, 0, 0, TimeSpan.Zero));
        var successful = new SuccessfulHandler();
        var flaky = new FlakyHandler();
        var services = new ServiceCollection();
        services.AddDbContext<ApplicationDbContext>(options => options.UseInMemoryDatabase(databaseName));
        services.AddSingleton<TimeProvider>(clock);
        services.AddSingleton<IIntegrationEventHandler<TestDurableEvent>>(successful);
        services.AddSingleton<IIntegrationEventHandler<TestDurableEvent>>(flaky);
        services.AddScoped<IInboxStore, InboxStore>();
        services.AddScoped<IOutboxDispatcher, OutboxDispatcher>();
        services.AddLogging();
        await using var provider = services.BuildServiceProvider();
        await using (var arrangeScope = provider.CreateAsyncScope())
        {
            var context = arrangeScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var tenant = NewTenant();
            tenant.AddIntegrationEvent(NewEvent(tenant.Id));
            context.Add(tenant);
            await context.SaveChangesAsync();
        }

        // When
        await using (var firstScope = provider.CreateAsyncScope())
        {
            await firstScope.ServiceProvider.GetRequiredService<IOutboxDispatcher>()
                .DispatchPendingAsync(CancellationToken.None);
        }
        clock.Advance(TimeSpan.FromMinutes(1));
        await using (var secondScope = provider.CreateAsyncScope())
        {
            await secondScope.ServiceProvider.GetRequiredService<IOutboxDispatcher>()
                .DispatchPendingAsync(CancellationToken.None);
        }

        // Then
        successful.Calls.Should().Be(1);
        flaky.Calls.Should().Be(2);
        await using var assertScope = provider.CreateAsyncScope();
        var receipts = await assertScope.ServiceProvider.GetRequiredService<ApplicationDbContext>()
            .Set<InboxReceipt>().ToListAsync();
        receipts.Should().HaveCount(2).And.OnlyContain(receipt => receipt.CompletedAtUtc != null);
    }

    [Fact]
    public void EventTransportAdminController_RequiresSystemAdminAndVersionedAdminRoute()
    {
        // Given
        var controllerType = typeof(EventTransportAdminController);

        // When
        var authorize = controllerType.GetCustomAttributes(typeof(AuthorizeAttribute), true)
            .Cast<AuthorizeAttribute>().Single();
        var route = controllerType.GetCustomAttributes(typeof(RouteAttribute), true)
            .Cast<RouteAttribute>().Single();

        // Then
        authorize.Policy.Should().Be(Policies.SystemAdmin);
        route.Template.Should().Be("v{version:apiVersion}/admin/events");
        controllerType.GetMethod(nameof(EventTransportAdminController.GetStatus)).Should().NotBeNull();
        controllerType.GetMethod(nameof(EventTransportAdminController.GetDeadLetters)).Should().NotBeNull();
        controllerType.GetMethod(nameof(EventTransportAdminController.Replay)).Should().NotBeNull();
    }

    [Fact]
    public async Task SaveChangesAsync_WhenDurableContractContainsPii_RejectsPayload()
    {
        // Given
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        await using var context = new ApplicationDbContext(options);
        var tenant = NewTenant();
        tenant.AddIntegrationEvent(new UnsafeDurableEvent
        {
            TenantId = tenant.Id,
            ActorId = Guid.NewGuid(),
            AggregateType = "Tenant",
            AggregateId = tenant.Id.ToString(),
            Email = "person@example.com"
        });
        context.Add(tenant);

        // When
        var action = () => context.SaveChangesAsync();

        // Then
        await action.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*unclassified*");
    }

    [Fact]
    public async Task SaveChangesAsync_WhenTenantOrActorMetadataIsMissing_RejectsDurableEvent()
    {
        // Given
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        await using var context = new ApplicationDbContext(options);
        var tenant = NewTenant();
        tenant.AddIntegrationEvent(new TestDurableEvent
        {
            TenantId = Guid.Empty,
            ActorId = Guid.Empty,
            AggregateType = "Tenant",
            AggregateId = tenant.Id.ToString()
        });
        context.Add(tenant);

        // When
        var action = () => context.SaveChangesAsync();

        // Then
        await action.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*tenant and actor*");
    }

    [Fact]
    public async Task SaveChangesAsync_WhenEventSpecificFieldIsUnclassified_RejectsPayload()
    {
        // Given
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        await using var context = new ApplicationDbContext(options);
        var tenant = NewTenant();
        tenant.AddIntegrationEvent(new UnclassifiedValueEvent
        {
            TenantId = tenant.Id,
            ActorId = Guid.NewGuid(),
            AggregateType = "Tenant",
            AggregateId = tenant.Id.ToString(),
            Value = "123.456.789-00"
        });
        context.Add(tenant);

        // When
        var action = () => context.SaveChangesAsync();

        // Then
        await action.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*unclassified*");
    }

    [Fact]
    public async Task SaveChangesAsync_WhenNestedFieldIsUnclassified_RejectsPayload()
    {
        // Given
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        await using var context = new ApplicationDbContext(options);
        var tenant = NewTenant();
        tenant.AddIntegrationEvent(new NestedPayloadEvent
        {
            TenantId = tenant.Id,
            ActorId = Guid.NewGuid(),
            AggregateType = "Tenant",
            AggregateId = tenant.Id.ToString(),
            Details = new NestedDetails([new NestedValue("sensitive-value")])
        });
        context.Add(tenant);

        // When
        var action = () => context.SaveChangesAsync();

        // Then
        await action.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*unclassified*");
    }

    [Fact]
    public async Task DispatchPendingAsync_WhenHandlerMutatesAndThrows_RollsBackMutationBeforeRecordingFailure()
    {
        // Given
        var databaseName = Guid.NewGuid().ToString();
        var clock = new MutableTimeProvider(new DateTimeOffset(2026, 8, 26, 12, 0, 0, TimeSpan.Zero));
        var services = new ServiceCollection();
        services.AddDbContext<ApplicationDbContext>(options => options.UseInMemoryDatabase(databaseName));
        services.AddSingleton<TimeProvider>(clock);
        services.AddScoped<IIntegrationEventHandler<TestDurableEvent>, MutatingFailHandler>();
        services.AddScoped<IInboxStore, InboxStore>();
        services.AddScoped<IOutboxDispatcher, OutboxDispatcher>();
        services.AddLogging();
        await using var provider = services.BuildServiceProvider();
        var tenant = NewTenant();
        var originalName = tenant.Name;
        await using (var arrangeScope = provider.CreateAsyncScope())
        {
            var context = arrangeScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            tenant.AddIntegrationEvent(NewEvent(tenant.Id));
            context.Add(tenant);
            await context.SaveChangesAsync();
        }

        // When
        await using (var dispatchScope = provider.CreateAsyncScope())
        {
            await dispatchScope.ServiceProvider.GetRequiredService<IOutboxDispatcher>()
                .DispatchPendingAsync(CancellationToken.None);
        }

        // Then
        await using var assertScope = provider.CreateAsyncScope();
        var assertContext = assertScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        (await assertContext.Set<Tenant>().SingleAsync(entity => entity.Id == tenant.Id)).Name.Should().Be(originalName);
        (await assertContext.Set<InboxReceipt>().SingleAsync()).AttemptCount.Should().Be(1);
    }

    [Fact]
    public async Task DispatchPendingAsync_WhenOneConsumerIsDeadLettered_KeepsRetryingOtherConsumer()
    {
        // Given
        var databaseName = Guid.NewGuid().ToString();
        var clock = new MutableTimeProvider(new DateTimeOffset(2026, 8, 26, 12, 0, 0, TimeSpan.Zero));
        var dead = new DeadLetteredHandler();
        var retryable = new RetryableHandler();
        var services = new ServiceCollection();
        services.AddDbContext<ApplicationDbContext>(options => options.UseInMemoryDatabase(databaseName));
        services.AddSingleton<TimeProvider>(clock);
        services.AddSingleton<IIntegrationEventHandler<TestDurableEvent>>(dead);
        services.AddSingleton<IIntegrationEventHandler<TestDurableEvent>>(retryable);
        services.AddScoped<IInboxStore, InboxStore>();
        services.AddScoped<IOutboxDispatcher, OutboxDispatcher>();
        services.AddLogging();
        await using var provider = services.BuildServiceProvider();
        await using (var arrangeScope = provider.CreateAsyncScope())
        {
            var context = arrangeScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var tenant = NewTenant();
            var integrationEvent = NewEvent(tenant.Id);
            tenant.AddIntegrationEvent(integrationEvent);
            context.Add(tenant);
            await context.SaveChangesAsync();
            context.AddRange(
                new InboxReceipt
                {
                    EventId = integrationEvent.EventId,
                    ConsumerName = dead.GetType().FullName!,
                    AttemptCount = 5,
                    NextAttemptAtUtc = clock.GetUtcNow(),
                    DeadLetteredAtUtc = clock.GetUtcNow(),
                    UpdatedAtUtc = clock.GetUtcNow()
                },
                new InboxReceipt
                {
                    EventId = integrationEvent.EventId,
                    ConsumerName = retryable.GetType().FullName!,
                    AttemptCount = 0,
                    NextAttemptAtUtc = clock.GetUtcNow(),
                    UpdatedAtUtc = clock.GetUtcNow()
                });
            await context.SaveChangesAsync();
        }

        // When
        await using (var firstScope = provider.CreateAsyncScope())
        {
            await firstScope.ServiceProvider.GetRequiredService<IOutboxDispatcher>()
                .DispatchPendingAsync(CancellationToken.None);
        }
        clock.Advance(TimeSpan.FromMinutes(1));
        await using (var secondScope = provider.CreateAsyncScope())
        {
            await secondScope.ServiceProvider.GetRequiredService<IOutboxDispatcher>()
                .DispatchPendingAsync(CancellationToken.None);
        }

        // Then
        dead.Calls.Should().Be(0);
        retryable.Calls.Should().Be(2);
        await using var assertScope = provider.CreateAsyncScope();
        var message = await assertScope.ServiceProvider.GetRequiredService<ApplicationDbContext>()
            .Set<OutboxMessage>().SingleAsync();
        message.DeadLetteredAtUtc.Should().NotBeNull();
    }

    [Fact]
    public async Task ReplayAsync_WhenConsumerIsDeadLettered_RetainsAndReschedulesEvent()
    {
        // Given
        var clock = new MutableTimeProvider(new DateTimeOffset(2026, 8, 26, 12, 0, 0, TimeSpan.Zero));
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        await using var context = new ApplicationDbContext(options);
        var integrationEvent = NewEvent(Guid.NewGuid());
        context.Add(new OutboxMessage
        {
            EventId = integrationEvent.EventId,
            EventName = integrationEvent.EventName,
            EventType = integrationEvent.GetType().FullName!,
            SourceModule = integrationEvent.SourceModule,
            AggregateType = integrationEvent.AggregateType,
            AggregateId = integrationEvent.AggregateId,
            CorrelationId = integrationEvent.CorrelationId,
            OccurredAtUtc = integrationEvent.OccurredAt,
            SchemaVersion = integrationEvent.SchemaVersion,
            Payload = "{}",
            CreatedAtUtc = clock.GetUtcNow(),
            DeadLetteredAtUtc = clock.GetUtcNow()
        });
        context.Add(new InboxReceipt
        {
            EventId = integrationEvent.EventId,
            ConsumerName = "TenantProjection",
            AttemptCount = 5,
            NextAttemptAtUtc = clock.GetUtcNow(),
            DeadLetteredAtUtc = clock.GetUtcNow(),
            LastError = "listener failure",
            UpdatedAtUtc = clock.GetUtcNow()
        });
        await context.SaveChangesAsync();
        var replay = new EventReplayService(context, clock);

        // When
        var replayed = await replay.ReplayAsync(integrationEvent.EventId, "TenantProjection");

        // Then
        replayed.Should().BeTrue();
        (await context.Set<OutboxMessage>().SingleAsync()).DeadLetteredAtUtc.Should().BeNull();
        var receipt = await context.Set<InboxReceipt>().SingleAsync();
        receipt.AttemptCount.Should().Be(0);
        receipt.DeadLetteredAtUtc.Should().BeNull();
        receipt.NextAttemptAtUtc.Should().Be(clock.GetUtcNow());
    }

    [Fact]
    public async Task DispatchPendingAsync_WhenReceiptsAlreadyCompleted_FinalizesOutboxAfterCrash()
    {
        // Given
        var databaseName = Guid.NewGuid().ToString();
        var clock = new MutableTimeProvider(new DateTimeOffset(2026, 8, 26, 12, 0, 0, TimeSpan.Zero));
        var successful = new SuccessfulHandler();
        var services = NewDispatcherServices(databaseName, clock, successful);
        await using var provider = services.BuildServiceProvider();
        Guid eventId;
        await using (var arrangeScope = provider.CreateAsyncScope())
        {
            var context = arrangeScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var tenant = NewTenant();
            var integrationEvent = NewEvent(tenant.Id);
            eventId = integrationEvent.EventId;
            tenant.AddIntegrationEvent(integrationEvent);
            context.Add(tenant);
            await context.SaveChangesAsync();
            context.Add(new InboxReceipt
            {
                EventId = eventId,
                ConsumerName = successful.GetType().FullName!,
                AttemptCount = 1,
                NextAttemptAtUtc = clock.GetUtcNow(),
                CompletedAtUtc = clock.GetUtcNow(),
                UpdatedAtUtc = clock.GetUtcNow()
            });
            await context.SaveChangesAsync();
        }

        // When
        await using (var dispatchScope = provider.CreateAsyncScope())
        {
            await dispatchScope.ServiceProvider.GetRequiredService<IOutboxDispatcher>()
                .DispatchPendingAsync(CancellationToken.None);
        }

        // Then
        successful.Calls.Should().Be(0);
        await using var assertScope = provider.CreateAsyncScope();
        var message = await assertScope.ServiceProvider.GetRequiredService<ApplicationDbContext>()
            .Set<OutboxMessage>().SingleAsync(message => message.EventId == eventId);
        message.CompletedAtUtc.Should().Be(clock.GetUtcNow());
    }

    [Fact]
    public async Task DispatchPendingAsync_WhenRemovedConsumerHasHistoricalTerminalReceipt_FinalizesCurrentConsumers()
    {
        // Given
        var databaseName = Guid.NewGuid().ToString();
        var clock = new MutableTimeProvider(new DateTimeOffset(2026, 8, 26, 12, 0, 0, TimeSpan.Zero));
        var successful = new SuccessfulHandler();
        var services = NewDispatcherServices(databaseName, clock, successful);
        await using var provider = services.BuildServiceProvider();
        Guid eventId;
        await using (var arrangeScope = provider.CreateAsyncScope())
        {
            var context = arrangeScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var tenant = NewTenant();
            var integrationEvent = NewEvent(tenant.Id);
            eventId = integrationEvent.EventId;
            tenant.AddIntegrationEvent(integrationEvent);
            context.Add(tenant);
            await context.SaveChangesAsync();
            context.AddRange(
                new InboxReceipt
                {
                    EventId = eventId,
                    ConsumerName = successful.GetType().FullName!,
                    AttemptCount = 1,
                    NextAttemptAtUtc = clock.GetUtcNow(),
                    CompletedAtUtc = clock.GetUtcNow(),
                    UpdatedAtUtc = clock.GetUtcNow()
                },
                new InboxReceipt
                {
                    EventId = eventId,
                    ConsumerName = "Removed.Consumer",
                    AttemptCount = 5,
                    NextAttemptAtUtc = clock.GetUtcNow(),
                    DeadLetteredAtUtc = clock.GetUtcNow(),
                    UpdatedAtUtc = clock.GetUtcNow()
                });
            await context.SaveChangesAsync();
        }

        // When
        await using (var dispatchScope = provider.CreateAsyncScope())
        {
            await dispatchScope.ServiceProvider.GetRequiredService<IOutboxDispatcher>()
                .DispatchPendingAsync(CancellationToken.None);
        }

        // Then
        successful.Calls.Should().Be(0);
        await using var assertScope = provider.CreateAsyncScope();
        var assertContext = assertScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var message = await assertContext.Set<OutboxMessage>().SingleAsync(item => item.EventId == eventId);
        message.CompletedAtUtc.Should().Be(clock.GetUtcNow());
        message.DeadLetteredAtUtc.Should().BeNull();
        (await assertContext.Set<InboxReceipt>().CountAsync()).Should().Be(2);
    }

    [Fact]
    public async Task DispatchPendingAsync_WhenNewConsumerLacksReceipt_DoesNotCountHistoricalReceiptAsSuccess()
    {
        // Given
        var databaseName = Guid.NewGuid().ToString();
        var clock = new MutableTimeProvider(new DateTimeOffset(2026, 8, 26, 12, 0, 0, TimeSpan.Zero));
        var newlyRegistered = new FlakyHandler();
        var services = new ServiceCollection();
        services.AddDbContext<ApplicationDbContext>(options => options.UseInMemoryDatabase(databaseName));
        services.AddSingleton<TimeProvider>(clock);
        services.AddSingleton<IIntegrationEventHandler<TestDurableEvent>>(newlyRegistered);
        services.AddScoped<IInboxStore, InboxStore>();
        services.AddScoped<IOutboxDispatcher, OutboxDispatcher>();
        services.AddLogging();
        await using var provider = services.BuildServiceProvider();
        Guid eventId;
        await using (var arrangeScope = provider.CreateAsyncScope())
        {
            var context = arrangeScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var tenant = NewTenant();
            var integrationEvent = NewEvent(tenant.Id);
            eventId = integrationEvent.EventId;
            tenant.AddIntegrationEvent(integrationEvent);
            context.Add(tenant);
            await context.SaveChangesAsync();
            context.Add(new InboxReceipt
            {
                EventId = eventId,
                ConsumerName = "Removed.Consumer",
                AttemptCount = 1,
                NextAttemptAtUtc = clock.GetUtcNow(),
                CompletedAtUtc = clock.GetUtcNow(),
                UpdatedAtUtc = clock.GetUtcNow()
            });
            await context.SaveChangesAsync();
        }

        // When
        await using (var dispatchScope = provider.CreateAsyncScope())
        {
            await dispatchScope.ServiceProvider.GetRequiredService<IOutboxDispatcher>()
                .DispatchPendingAsync(CancellationToken.None);
        }

        // Then
        newlyRegistered.Calls.Should().Be(1);
        await using var assertScope = provider.CreateAsyncScope();
        var assertContext = assertScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var message = await assertContext.Set<OutboxMessage>().SingleAsync(item => item.EventId == eventId);
        message.CompletedAtUtc.Should().BeNull();
        message.DeadLetteredAtUtc.Should().BeNull();
        var receipts = await assertContext.Set<InboxReceipt>().ToListAsync();
        receipts.Should().HaveCount(2);
        receipts.Single(receipt => receipt.ConsumerName == newlyRegistered.GetType().FullName)
            .CompletedAtUtc.Should().BeNull();
    }

    [Fact]
    public async Task DispatchPendingAsync_WhenEnvelopeCannotDeserialize_ContinuesOtherEvents()
    {
        // Given
        var databaseName = Guid.NewGuid().ToString();
        var clock = new MutableTimeProvider(new DateTimeOffset(2026, 8, 26, 12, 0, 0, TimeSpan.Zero));
        var successful = new SuccessfulHandler();
        var services = NewDispatcherServices(databaseName, clock, successful);
        await using var provider = services.BuildServiceProvider();
        await using (var arrangeScope = provider.CreateAsyncScope())
        {
            var context = arrangeScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var firstTenant = NewTenant();
            var secondTenant = NewTenant();
            firstTenant.AddIntegrationEvent(NewEvent(firstTenant.Id));
            secondTenant.AddIntegrationEvent(NewEvent(secondTenant.Id));
            context.AddRange(firstTenant, secondTenant);
            await context.SaveChangesAsync();
            var messages = await context.Set<OutboxMessage>().OrderBy(message => message.EventId).ToListAsync();
            messages[0].EventType = "Missing.Event, Missing.Assembly";
            messages[0].OccurredAtUtc = messages[0].OccurredAtUtc.AddMinutes(-1);
            await context.SaveChangesAsync();
        }

        // When
        await using (var dispatchScope = provider.CreateAsyncScope())
        {
            await dispatchScope.ServiceProvider.GetRequiredService<IOutboxDispatcher>()
                .DispatchPendingAsync(CancellationToken.None);
        }

        // Then
        successful.Calls.Should().Be(1);
        await using var assertScope = provider.CreateAsyncScope();
        var contextForAssert = assertScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        (await contextForAssert.Set<OutboxMessage>().CountAsync()).Should().Be(2);
        (await contextForAssert.Set<OutboxMessage>().CountAsync(message => message.CompletedAtUtc != null)).Should().Be(1);
        (await contextForAssert.Set<InboxReceipt>().CountAsync()).Should().Be(2);
    }

    [Fact]
    public async Task DispatchPendingAsync_DoesNotBlockMatchingAggregateIdsAcrossTenants()
    {
        var databaseName = Guid.NewGuid().ToString();
        var clock = new MutableTimeProvider(new DateTimeOffset(2026, 8, 26, 12, 0, 0, TimeSpan.Zero));
        var handler = new FlakyHandler();
        var services = new ServiceCollection();
        services.AddDbContext<ApplicationDbContext>(options => options.UseInMemoryDatabase(databaseName));
        services.AddSingleton<TimeProvider>(clock);
        services.AddSingleton<IIntegrationEventHandler<TestDurableEvent>>(handler);
        services.AddScoped<IInboxStore, InboxStore>();
        services.AddScoped<IOutboxDispatcher, OutboxDispatcher>();
        services.AddLogging();
        await using var provider = services.BuildServiceProvider();

        await using (var arrangeScope = provider.CreateAsyncScope())
        {
            var context = arrangeScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var firstTenant = NewTenant();
            var secondTenant = NewTenant();
            firstTenant.AddIntegrationEvent(NewEvent(firstTenant.Id) with
            {
                AggregateId = "shared-aggregate-id",
                OccurredAt = clock.GetUtcNow().UtcDateTime
            });
            secondTenant.AddIntegrationEvent(NewEvent(secondTenant.Id) with
            {
                AggregateId = "shared-aggregate-id",
                OccurredAt = clock.GetUtcNow().AddMinutes(1).UtcDateTime
            });
            context.AddRange(firstTenant, secondTenant);
            await context.SaveChangesAsync();
        }

        await using (var dispatchScope = provider.CreateAsyncScope())
        {
            await dispatchScope.ServiceProvider.GetRequiredService<IOutboxDispatcher>()
                .DispatchPendingAsync(CancellationToken.None);
        }

        handler.Calls.Should().Be(2);
        await using var assertScope = provider.CreateAsyncScope();
        var assertContext = assertScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        (await assertContext.Set<OutboxMessage>().CountAsync(message => message.CompletedAtUtc != null))
            .Should().Be(1);
    }

    private static Tenant NewTenant() => new()
    {
        Id = Guid.NewGuid(),
        Name = "Durable event tenant",
        Slug = $"durable-event-{Guid.NewGuid():N}",
        AdminEmail = "admin@example.com"
    };

    private static ServiceCollection NewDispatcherServices(
        string databaseName,
        TimeProvider clock,
        SuccessfulHandler successful)
    {
        var services = new ServiceCollection();
        services.AddDbContext<ApplicationDbContext>(options => options.UseInMemoryDatabase(databaseName));
        services.AddSingleton(clock);
        services.AddSingleton<IIntegrationEventHandler<TestDurableEvent>>(successful);
        services.AddScoped<IInboxStore, InboxStore>();
        services.AddScoped<IOutboxDispatcher, OutboxDispatcher>();
        services.AddLogging();
        return services;
    }

    private static TestDurableEvent NewEvent(Guid tenantId) => new()
    {
        EventId = Guid.NewGuid(),
        TenantId = tenantId,
        ActorId = Guid.NewGuid(),
        AggregateType = "Tenant",
        AggregateId = tenantId.ToString(),
        CorrelationId = Guid.NewGuid(),
        CausationId = Guid.NewGuid(),
        OccurredAt = new DateTime(2026, 8, 26, 12, 0, 0, DateTimeKind.Utc)
    };

    public sealed record TestDurableEvent : DurableIntegrationEventBase
    {
        public override string EventName => "tenant.deactivated";
        public override string SourceModule => "Identity.Tenants";
    }

    public sealed record UnsafeDurableEvent : DurableIntegrationEventBase
    {
        public override string EventName => "tenant.unsafe";
        public override string SourceModule => "Identity.Tenants";
        public required string Email { get; init; }
    }

    public sealed record UnclassifiedValueEvent : DurableIntegrationEventBase
    {
        public override string EventName => "tenant.unclassified";
        public override string SourceModule => "Identity.Tenants";
        public required string Value { get; init; }
    }

    public sealed record NestedPayloadEvent : DurableIntegrationEventBase
    {
        public override string EventName => "tenant.nested";
        public override string SourceModule => "Identity.Tenants";
        [NonPersonalEventData]
        public required NestedDetails Details { get; init; }
    }

    public sealed record NestedDetails(
        [property: NonPersonalEventData] IReadOnlyList<NestedValue> Values);
    public sealed record NestedValue(string Value);

    private sealed class SuccessfulHandler : IIntegrationEventHandler<TestDurableEvent>
    {
        public int Calls { get; private set; }

        public Task HandleAsync(TestDurableEvent @event, CancellationToken cancellationToken = default)
        {
            Calls++;
            return Task.CompletedTask;
        }
    }

    private sealed class FlakyHandler : IIntegrationEventHandler<TestDurableEvent>
    {
        public int Calls { get; private set; }

        public Task HandleAsync(TestDurableEvent @event, CancellationToken cancellationToken = default)
        {
            Calls++;
            return Calls == 1
                ? Task.FromException(new InvalidOperationException("transient listener failure"))
                : Task.CompletedTask;
        }
    }

    private sealed class MutatingFailHandler(ApplicationDbContext db) : IIntegrationEventHandler<TestDurableEvent>
    {
        public async Task HandleAsync(TestDurableEvent @event, CancellationToken cancellationToken = default)
        {
            var tenant = await db.Set<Tenant>().SingleAsync(
                entity => entity.Id == @event.TenantId,
                cancellationToken);
            tenant.Name = "mutation that must roll back";
            throw new InvalidOperationException("listener failed after mutation");
        }
    }

    private sealed class DeadLetteredHandler : IIntegrationEventHandler<TestDurableEvent>
    {
        public int Calls { get; private set; }

        public Task HandleAsync(TestDurableEvent @event, CancellationToken cancellationToken = default)
        {
            Calls++;
            return Task.CompletedTask;
        }
    }

    private sealed class RetryableHandler : IIntegrationEventHandler<TestDurableEvent>
    {
        public int Calls { get; private set; }

        public Task HandleAsync(TestDurableEvent @event, CancellationToken cancellationToken = default)
        {
            Calls++;
            return Calls == 1
                ? Task.FromException(new InvalidOperationException("retryable failure"))
                : Task.CompletedTask;
        }
    }

    private sealed class RejectFirstSaveInterceptor : SaveChangesInterceptor
    {
        private bool _rejected;

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            if (_rejected)
            {
                return ValueTask.FromResult(result);
            }

            _rejected = true;
            return ValueTask.FromException<InterceptionResult<int>>(new DbUpdateException("producer persistence failed"));
        }
    }

    private sealed class MutableTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;

        public void Advance(TimeSpan duration) => utcNow += duration;
    }
}
