using System.Collections.Concurrent;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using GameGuild.API.Database;
using GameGuild.API.Eventing;
using Npgsql;
using Testcontainers.PostgreSql;

namespace GameGuild.API.IntegrationTests;

public sealed class DurableEventTransportPostgreSqlTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("gameguild_events")
        .WithUsername("test")
        .WithPassword("test")
        .WithCleanUp(true)
        .Build();

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
        await using var connection = new NpgsqlConnection(_postgres.GetConnectionString());
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE SCHEMA "gameguild.integration";
            CREATE TABLE "gameguild.integration"."outbox_messages" (
                "EventId" uuid PRIMARY KEY,
                "TenantId" uuid NOT NULL,
                "ActorId" uuid NOT NULL,
                "EventName" varchar(200) NOT NULL,
                "EventType" varchar(1000) NOT NULL,
                "SourceModule" varchar(200) NOT NULL,
                "AggregateType" varchar(200) NOT NULL,
                "AggregateId" varchar(200) NOT NULL,
                "CorrelationId" uuid NOT NULL,
                "CausationId" uuid NULL,
                "OccurredAtUtc" timestamptz NOT NULL,
                "SchemaVersion" integer NOT NULL,
                "Payload" jsonb NOT NULL,
                "CreatedAtUtc" timestamptz NOT NULL,
                "ClaimedUntilUtc" timestamptz NULL,
                "CompletedAtUtc" timestamptz NULL,
                "DeadLetteredAtUtc" timestamptz NULL
            );
            CREATE TABLE "gameguild.integration"."inbox_receipts" (
                "EventId" uuid NOT NULL REFERENCES "gameguild.integration"."outbox_messages"("EventId"),
                "ConsumerName" varchar(500) NOT NULL,
                "AttemptCount" integer NOT NULL,
                "NextAttemptAtUtc" timestamptz NOT NULL,
                "CompletedAtUtc" timestamptz NULL,
                "DeadLetteredAtUtc" timestamptz NULL,
                "LastError" varchar(4000) NULL,
                "UpdatedAtUtc" timestamptz NOT NULL,
                PRIMARY KEY ("EventId", "ConsumerName")
            );
            """;
        await command.ExecuteNonQueryAsync();
    }

    public async Task DisposeAsync() => await _postgres.DisposeAsync();

    [Fact]
    public async Task DispatchPendingAsync_WhenOutboxLeaseExpires_DoesNotExecuteSameConsumerConcurrently()
    {
        // Given
        await ResetAsync();
        var now = new DateTimeOffset(2026, 8, 26, 12, 0, 0, TimeSpan.Zero);
        var clock = new MutableTimeProvider(now);
        var handler = new BlockingHandler();
        await using var provider = BuildProvider(clock, services =>
            services.AddSingleton<IIntegrationEventHandler<PostgresEvent>>(handler));
        var message = NewMessage("lease", now);
        await InsertAsync(provider, message, new InboxReceipt
        {
            EventId = message.EventId,
            ConsumerName = handler.GetType().FullName!,
            AttemptCount = 0,
            NextAttemptAtUtc = now,
            UpdatedAtUtc = now
        });
        var firstDispatch = DispatchOnceAsync(provider);
        await handler.FirstStarted.Task.WaitAsync(TimeSpan.FromSeconds(15));
        clock.Advance(TimeSpan.FromMinutes(3));

        // When
        var secondDispatch = DispatchOnceAsync(provider);
        var concurrentEntry = await Task.WhenAny(handler.SecondStarted.Task, Task.Delay(TimeSpan.FromSeconds(1)));

        // Then
        concurrentEntry.Should().NotBe(handler.SecondStarted.Task);
        handler.Release.TrySetResult();
        await Task.WhenAll(firstDispatch, secondDispatch).WaitAsync(TimeSpan.FromSeconds(15));
        handler.Calls.Should().Be(1);
    }

    [Fact]
    public async Task DispatchPendingAsync_WhenAggregateHasTwoEvents_PreservesOrderAcrossClaims()
    {
        // Given
        await ResetAsync();
        var now = new DateTimeOffset(2026, 8, 26, 12, 0, 0, TimeSpan.Zero);
        var clock = new MutableTimeProvider(now);
        var handler = new RecordingHandler();
        await using var provider = BuildProvider(clock, services =>
            services.AddSingleton<IIntegrationEventHandler<PostgresEvent>>(handler));
        var first = NewMessage("ordered", now);
        var second = NewMessage("ordered", now.AddSeconds(1));
        await InsertAsync(provider, first, second);

        // When
        await DispatchOnceAsync(provider);
        await DispatchOnceAsync(provider);

        // Then
        handler.Events.Should().Equal(first.EventId, second.EventId);
    }

    [Fact]
    public async Task DispatchPendingAsync_WhenOneConsumerDeadLetters_RetriesOtherConsumerIndependently()
    {
        // Given
        await ResetAsync();
        var now = new DateTimeOffset(2026, 8, 26, 12, 0, 0, TimeSpan.Zero);
        var clock = new MutableTimeProvider(now);
        var dead = new TerminalHandler();
        var retryable = new RetryableHandler();
        await using var provider = BuildProvider(clock, services =>
        {
            services.AddSingleton<IIntegrationEventHandler<PostgresEvent>>(dead);
            services.AddSingleton<IIntegrationEventHandler<PostgresEvent>>(retryable);
        });
        var message = NewMessage("dead-letter", now);
        await InsertAsync(
            provider,
            message,
            new InboxReceipt
            {
                EventId = message.EventId,
                ConsumerName = dead.GetType().FullName!,
                AttemptCount = 5,
                NextAttemptAtUtc = now,
                DeadLetteredAtUtc = now,
                UpdatedAtUtc = now
            },
            new InboxReceipt
            {
                EventId = message.EventId,
                ConsumerName = retryable.GetType().FullName!,
                AttemptCount = 0,
                NextAttemptAtUtc = now,
                UpdatedAtUtc = now
            });

        // When
        await DispatchOnceAsync(provider);
        clock.Advance(TimeSpan.FromMinutes(1));
        await DispatchOnceAsync(provider);

        // Then
        dead.Calls.Should().Be(0);
        retryable.Calls.Should().Be(2);
        await using var scope = provider.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        (await context.Set<OutboxMessage>().SingleAsync()).DeadLetteredAtUtc.Should().NotBeNull();
    }

    private ServiceProvider BuildProvider(MutableTimeProvider clock, Action<IServiceCollection> addHandlers)
    {
        var services = new ServiceCollection();
        services.AddDbContext<ApplicationDbContext>(options => options.UseNpgsql(_postgres.GetConnectionString()));
        services.AddSingleton<TimeProvider>(clock);
        services.AddScoped<IInboxStore, InboxStore>();
        services.AddScoped<IOutboxDispatcher, OutboxDispatcher>();
        services.AddLogging();
        addHandlers(services);
        return services.BuildServiceProvider();
    }

    private async Task ResetAsync()
    {
        await using var connection = new NpgsqlConnection(_postgres.GetConnectionString());
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "TRUNCATE TABLE \"gameguild.integration\".\"inbox_receipts\", \"gameguild.integration\".\"outbox_messages\"";
        await command.ExecuteNonQueryAsync();
    }

    private static async Task DispatchOnceAsync(IServiceProvider provider)
    {
        await using var scope = provider.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<IOutboxDispatcher>()
            .DispatchPendingAsync(CancellationToken.None);
    }

    private static async Task InsertAsync(IServiceProvider provider, params object[] entities)
    {
        await using var scope = provider.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        context.AddRange(entities);
        await context.SaveChangesAsync();
    }

    private static OutboxMessage NewMessage(string aggregateId, DateTimeOffset occurredAt)
    {
        var integrationEvent = new PostgresEvent
        {
            EventId = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            ActorId = Guid.NewGuid(),
            AggregateType = "Tenant",
            AggregateId = aggregateId,
            CorrelationId = Guid.NewGuid(),
            OccurredAt = occurredAt.UtcDateTime
        };
        return new OutboxMessage
        {
            EventId = integrationEvent.EventId,
            TenantId = integrationEvent.TenantId,
            ActorId = integrationEvent.ActorId,
            EventName = integrationEvent.EventName,
            EventType = $"{typeof(PostgresEvent).FullName}, {typeof(PostgresEvent).Assembly.GetName().Name}",
            SourceModule = integrationEvent.SourceModule,
            AggregateType = integrationEvent.AggregateType,
            AggregateId = integrationEvent.AggregateId,
            CorrelationId = integrationEvent.CorrelationId,
            OccurredAtUtc = integrationEvent.OccurredAt,
            SchemaVersion = integrationEvent.SchemaVersion,
            Payload = JsonSerializer.Serialize(integrationEvent, integrationEvent.GetType(), new JsonSerializerOptions(JsonSerializerDefaults.Web)),
            CreatedAtUtc = occurredAt
        };
    }

    public sealed record PostgresEvent : DurableIntegrationEventBase
    {
        public override string EventName => "integration.postgres";
        public override string SourceModule => "IntegrationTests";
    }

    private sealed class RecordingHandler : IIntegrationEventHandler<PostgresEvent>
    {
        public ConcurrentQueue<Guid> Events { get; } = new();

        public Task HandleAsync(PostgresEvent @event, CancellationToken cancellationToken = default)
        {
            Events.Enqueue(@event.EventId);
            return Task.CompletedTask;
        }
    }

    private sealed class BlockingHandler : IIntegrationEventHandler<PostgresEvent>
    {
        private int _calls;
        public int Calls => _calls;
        public TaskCompletionSource FirstStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource SecondStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async Task HandleAsync(PostgresEvent @event, CancellationToken cancellationToken = default)
        {
            var call = Interlocked.Increment(ref _calls);
            if (call == 1)
            {
                FirstStarted.TrySetResult();
            }
            else
            {
                SecondStarted.TrySetResult();
            }

            await Release.Task.WaitAsync(cancellationToken);
        }
    }

    private sealed class TerminalHandler : IIntegrationEventHandler<PostgresEvent>
    {
        public int Calls { get; private set; }

        public Task HandleAsync(PostgresEvent @event, CancellationToken cancellationToken = default)
        {
            Calls++;
            return Task.CompletedTask;
        }
    }

    private sealed class RetryableHandler : IIntegrationEventHandler<PostgresEvent>
    {
        public int Calls { get; private set; }

        public Task HandleAsync(PostgresEvent @event, CancellationToken cancellationToken = default)
        {
            Calls++;
            return Calls == 1
                ? Task.FromException(new InvalidOperationException("retryable listener"))
                : Task.CompletedTask;
        }
    }

    private sealed class MutableTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        private DateTimeOffset _utcNow = utcNow;
        public override DateTimeOffset GetUtcNow() => _utcNow;
        public void Advance(TimeSpan duration) => _utcNow += duration;
    }
}
