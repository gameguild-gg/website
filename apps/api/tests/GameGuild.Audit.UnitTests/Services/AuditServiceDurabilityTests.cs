using FluentAssertions;
using GameGuild.API.Database;
using GameGuild.Compliance.Audit;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace GameGuild.Tests.Audit.Unit.Services;

/// <summary>
///     Issue #346 (comprehensive audit logging): a failed audit write must not break
///     business operations, but it may no longer be silently swallowed — the write is
///     retried once and then escalated to a critical structured-log entry carrying the
///     full audit payload so the record survives in the durable logging pipeline.
/// </summary>
public sealed class AuditServiceDurabilityTests : IDisposable
{
    private readonly Mock<IHttpContextAccessor> _httpContextAccessor = new();
    private readonly List<ProbeApplicationDbContext> _createdContexts = [];
    private readonly SharedCounter _saveAttempts = new();
    private ServiceProvider? _provider;

    public void Dispose() => _provider?.Dispose();

    [Fact]
    public async Task LogAsync_WhenPersistenceKeepsFailing_RetriesOnceAndDoesNotThrow()
    {
        var service = BuildService(failFirstSaveAttempts: int.MaxValue);

        var act = () => service.LogAsync(CreateRequest());

        await act.Should().NotThrowAsync("audit persistence must not break business operations");
        _saveAttempts.Attempts.Should().Be(2, "the audit write must be attempted exactly twice (initial + one retry)");
    }

    [Fact]
    public async Task LogAsync_WhenPersistenceFailsPermanently_EscalatesToCriticalDurableLogEntry()
    {
        var logger = new Mock<ILogger<AuditService>>();
        var service = BuildService(failFirstSaveAttempts: int.MaxValue, logger: logger.Object);

        await service.LogAsync(CreateRequest());

        logger.Verify(
            l => l.Log(
                LogLevel.Critical,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((state, _) =>
                    state.ToString()!.Contains("Audit log persistence FAILED")
                    && state.ToString()!.Contains("monetization.durability-probe")),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once,
            "a permanently failed audit write must leave a critical structured-log record of the payload");
    }

    [Fact]
    public async Task LogAsync_WhenPersistenceFailsTransiently_RetriesAndPersistsTheEntry()
    {
        var service = BuildService(failFirstSaveAttempts: 1);

        await service.LogAsync(CreateRequest());

        _createdContexts.Should().HaveCount(2);
        _createdContexts[1].SavedAuditLogs.Should().ContainSingle()
            .Which.ActionType.Should().Be("monetization.durability-probe");
    }

    private AuditService BuildService(int failFirstSaveAttempts, ILogger<AuditService>? logger = null)
    {
        var databaseRoot = new InMemoryDatabaseRoot();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"AuditDurability_{Guid.NewGuid()}", databaseRoot)
            .Options;

        var services = new ServiceCollection();
        services.AddScoped<IApplicationDbContext>(_ =>
        {
            var context = new ProbeApplicationDbContext(options, _saveAttempts, failFirstSaveAttempts);
            _createdContexts.Add(context);
            return context;
        });
        _provider = services.BuildServiceProvider();
        var scopeFactory = _provider.GetRequiredService<IServiceScopeFactory>();

        return new AuditService(
            scopeFactory,
            _httpContextAccessor.Object,
            logger ?? Mock.Of<ILogger<AuditService>>());
    }

    private static CreateAuditLogRequest CreateRequest() => new()
    {
        ActionType = "monetization.durability-probe",
        ResourceType = "SubscriptionPlan",
        ResourceId = Guid.NewGuid().ToString(),
        UserId = Guid.NewGuid(),
        Success = true,
        Description = "durability probe"
    };

    private sealed class SharedCounter
    {
        public int Attempts;
    }

    /// <summary>
    ///     Probe context whose <see cref="SaveChangesAsync"/> fails while the shared
    ///     attempt counter is within the first <c>failFirstSaveAttempts</c> attempts.
    /// </summary>
    private sealed class ProbeApplicationDbContext(
        DbContextOptions<ApplicationDbContext> options,
        SharedCounter saveAttempts,
        int failFirstSaveAttempts)
        : TestApplicationDbContext(options)
    {
        public List<AuditLog> SavedAuditLogs { get; } = [];

        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            saveAttempts.Attempts++;
            if (saveAttempts.Attempts <= failFirstSaveAttempts)
            {
                throw new InvalidOperationException("simulated audit store outage");
            }

            SavedAuditLogs.AddRange(Set<AuditLog>().Local.ToList());
            return Task.FromResult(SavedAuditLogs.Count);
        }
    }
}
