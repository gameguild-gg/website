using FluentAssertions;
using GameGuild.Learning.Assessments.Grading.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace GameGuild.Learning.Assessments.Tests;

public sealed class AcademicOutboxTests
{
    [Fact]
    public async Task Writer_FreezesTheConsumerRouteAtEnqueueTime()
    {
        await using var context = CreateContext();
        var first = new RecordingConsumer("consumer-a", "assessment-definition-prepared");
        var second = new RecordingConsumer("consumer-b", "assessment-definition-prepared");
        var writer = new AcademicOutboxWriter(context, [second, first]);

        var original = writer.Enqueue(
            Guid.NewGuid(),
            "assessment-definition-prepared",
            "1",
            "{\"schemaVersion\":1}");
        await context.SaveChangesAsync();

        var later = new RecordingConsumer("consumer-c", "assessment-definition-prepared");
        var next = new AcademicOutboxWriter(context, [first, second, later]).Enqueue(
            Guid.NewGuid(),
            "assessment-definition-prepared",
            "1",
            "{\"schemaVersion\":1}");
        await context.SaveChangesAsync();

        var originalRoute = await context.Set<AcademicOutboxDelivery>()
            .Where(delivery => delivery.OutboxMessageId == original.Id)
            .OrderBy(delivery => delivery.ConsumerKey)
            .Select(delivery => delivery.ConsumerKey)
            .ToArrayAsync();
        var nextRoute = await context.Set<AcademicOutboxDelivery>()
            .Where(delivery => delivery.OutboxMessageId == next.Id)
            .OrderBy(delivery => delivery.ConsumerKey)
            .Select(delivery => delivery.ConsumerKey)
            .ToArrayAsync();

        originalRoute.Should().Equal("consumer-a", "consumer-b");
        nextRoute.Should().Equal("consumer-a", "consumer-b", "consumer-c");
    }

    [Fact]
    public void Writer_RejectsDuplicateConsumerKeys()
    {
        using var context = CreateContext();
        var create = () => new AcademicOutboxWriter(
            context,
            [
                new RecordingConsumer("same", "event-a"),
                new RecordingConsumer("same", "event-b"),
            ]);

        create.Should().Throw<ArgumentException>();
    }

    [Fact]
    public async Task Dispatcher_RetriesOnlyTheFailedConsumerAndCompletesAfterAllReceipts()
    {
        var clock = new MutableTimeProvider(new DateTimeOffset(2026, 9, 9, 12, 0, 0, TimeSpan.Zero));
        SystemClock.SetProvider(clock);
        try
        {
            var databaseName = $"AcademicOutbox_{Guid.NewGuid()}";
            var successful = new RecordingConsumer("consumer-a", "event");
            var transient = new RecordingConsumer("consumer-b", "event", failuresBeforeSuccess: 1);
            using var provider = CreateProvider(databaseName, successful, transient);

            Guid messageId;
            using (var scope = provider.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
                var message = scope.ServiceProvider.GetRequiredService<IAcademicOutboxWriter>()
                    .Enqueue(Guid.NewGuid(), "event", "1", "{\"schemaVersion\":1}");
                messageId = message.Id;
                await context.SaveChangesAsync();
            }

            var dispatcher = new AcademicOutboxDispatcher(
                provider.GetRequiredService<IServiceScopeFactory>(),
                NullLogger<AcademicOutboxDispatcher>.Instance);
            (await dispatcher.DispatchBatchAsync()).Should().Be(2);

            using (var scope = provider.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
                var message = await context.Set<AcademicOutboxMessage>().SingleAsync(value => value.Id == messageId);
                var deliveries = await context.Set<AcademicOutboxDelivery>()
                    .Where(value => value.OutboxMessageId == messageId)
                    .ToArrayAsync();
                message.Status.Should().Be(AcademicOutboxStatus.Failed);
                deliveries.Should().ContainSingle(value => value.Status == AcademicOutboxDeliveryStatus.Confirmed);
                deliveries.Should().ContainSingle(value => value.Status == AcademicOutboxDeliveryStatus.Failed);
            }

            clock.Advance(TimeSpan.FromSeconds(3));
            (await dispatcher.DispatchBatchAsync()).Should().Be(1);

            using (var scope = provider.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
                var message = await context.Set<AcademicOutboxMessage>().SingleAsync(value => value.Id == messageId);
                var deliveries = await context.Set<AcademicOutboxDelivery>()
                    .Where(value => value.OutboxMessageId == messageId)
                    .ToArrayAsync();
                message.Status.Should().Be(AcademicOutboxStatus.Completed);
                deliveries.Should().OnlyContain(value => value.Status == AcademicOutboxDeliveryStatus.Confirmed);
            }

            successful.CallCount.Should().Be(1);
            transient.CallCount.Should().Be(2);
        }
        finally
        {
            SystemClock.Reset();
        }
    }

    [Fact]
    public void ExpiredClaims_UseTheClaimTimestampAsAConcurrencyToken()
    {
        using var context = CreateContext();
        var entity = context.Model.FindEntityType(typeof(AcademicOutboxDelivery));

        entity.Should().NotBeNull();
        entity!.FindProperty(nameof(AcademicOutboxDelivery.Status))!.IsConcurrencyToken.Should().BeTrue();
        entity.FindProperty(nameof(AcademicOutboxDelivery.ClaimedAt))!.IsConcurrencyToken.Should().BeTrue();
    }

    private static ServiceProvider CreateProvider(
        string databaseName,
        params IAcademicOutboxConsumer[] consumers)
    {
        var services = new ServiceCollection();
        services.AddSingleton(new DbContextOptionsBuilder<OutboxTestContext>()
            .UseInMemoryDatabase(databaseName)
            .Options);
        services.AddScoped<OutboxTestContext>();
        services.AddScoped<IApplicationDbContext>(provider => provider.GetRequiredService<OutboxTestContext>());
        services.AddScoped<IAcademicOutboxWriter, AcademicOutboxWriter>();
        foreach (var consumer in consumers)
            services.AddSingleton(typeof(IAcademicOutboxConsumer), consumer);
        return services.BuildServiceProvider();
    }

    private static OutboxTestContext CreateContext() => new(
        new DbContextOptionsBuilder<OutboxTestContext>()
            .UseInMemoryDatabase($"AcademicOutboxModel_{Guid.NewGuid()}")
            .Options);

    private sealed class OutboxTestContext(DbContextOptions<OutboxTestContext> options)
        : DbContext(options), IApplicationDbContext
    {
        public Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default) =>
            Database.BeginTransactionAsync(cancellationToken);

        protected override void OnModelCreating(ModelBuilder modelBuilder) =>
            new GradingPersistenceModelConfiguration().Configure(modelBuilder);
    }

    private sealed class RecordingConsumer(
        string key,
        string eventType,
        int failuresBeforeSuccess = 0) : IAcademicOutboxConsumer
    {
        public string Key { get; } = key;
        public IReadOnlySet<string> EventTypes { get; } = new HashSet<string>([eventType], StringComparer.Ordinal);
        public int CallCount { get; private set; }

        public Task ConsumeAsync(AcademicOutboxEvent message, CancellationToken cancellationToken)
        {
            CallCount++;
            if (CallCount <= failuresBeforeSuccess)
                throw new InvalidOperationException("Transient consumer failure.");
            return Task.CompletedTask;
        }
    }

    private sealed class MutableTimeProvider(DateTimeOffset value) : TimeProvider
    {
        private DateTimeOffset _value = value;
        public override DateTimeOffset GetUtcNow() => _value;
        public void Advance(TimeSpan duration) => _value += duration;
    }
}
