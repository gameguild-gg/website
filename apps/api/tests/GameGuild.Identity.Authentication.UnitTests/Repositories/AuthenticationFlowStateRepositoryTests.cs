using System;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Xunit;

namespace GameGuild.Identity.Authentication.UnitTests.Repositories;

public class AuthenticationFlowStateRepositoryTests
{
    [Fact]
    public async Task CreateAsync_ShouldAssignTimestamps_AndRoundtripThroughGetByFlowId()
    {
        await using var context = CreateContext();
        var repository = new AuthenticationFlowStateRepository(context);

        var record = new AuthenticationFlowStateRecord
        {
            FlowId = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            CurrentStep = AuthenticationStep.MfaVerification,
            RequiredStepsJson = "[0,1]",
            CompletedStepsJson = "[0]",
            RiskScore = 0.6,
            InitiatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddMinutes(15),
            IpAddress = "203.0.113.5",
            DeviceFingerprint = "fingerprint-1"
        };

        var created = await repository.CreateAsync(record);

        created.CreatedAt.Should().BeOnOrAfter(DateTime.UtcNow.AddSeconds(-5));
        created.UpdatedAt.Should().BeOnOrAfter(DateTime.UtcNow.AddSeconds(-5));

        var loaded = await repository.GetByFlowIdAsync(record.FlowId);
        loaded.Should().NotBeNull();
        loaded!.FlowId.Should().Be(record.FlowId);
        loaded.UserId.Should().Be(record.UserId);
        loaded.CurrentStep.Should().Be(AuthenticationStep.MfaVerification);
        loaded.RequiredStepsJson.Should().Be("[0,1]");
        loaded.CompletedStepsJson.Should().Be("[0]");
        loaded.RiskScore.Should().Be(0.6);
        loaded.DeviceFingerprint.Should().Be("fingerprint-1");
        loaded.AbandonedAt.Should().BeNull();
    }

    [Fact]
    public async Task GetByFlowIdAsync_UnknownFlow_ShouldReturnNull()
    {
        await using var context = CreateContext();
        var repository = new AuthenticationFlowStateRepository(context);

        var loaded = await repository.GetByFlowIdAsync(Guid.NewGuid());

        loaded.Should().BeNull();
    }

    [Fact]
    public async Task UpdateAsync_ShouldPersistStepProgression()
    {
        await using var context = CreateContext();
        var repository = new AuthenticationFlowStateRepository(context);
        var record = await repository.CreateAsync(new AuthenticationFlowStateRecord
        {
            FlowId = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            RequiredStepsJson = "[0,1]",
            CompletedStepsJson = "[0]",
            InitiatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddMinutes(15)
        });

        record.CompletedStepsJson = "[0,1]";
        record.IsComplete = true;
        record.CompletedAt = DateTime.UtcNow;
        record.CurrentStep = AuthenticationStep.MfaVerification;

        await repository.UpdateAsync(record);

        var loaded = await repository.GetByFlowIdAsync(record.FlowId);
        loaded!.CompletedStepsJson.Should().Be("[0,1]");
        loaded.IsComplete.Should().BeTrue();
        loaded.CompletedAt.Should().NotBeNull();
        loaded.UpdatedAt.Should().BeOnOrAfter(DateTime.UtcNow.AddSeconds(-5));
    }

    [Fact]
    public async Task AbandonAsync_ShouldMarkAbandoned_AndIgnoreUnknownFlows()
    {
        await using var context = CreateContext();
        var repository = new AuthenticationFlowStateRepository(context);
        var record = await repository.CreateAsync(new AuthenticationFlowStateRecord
        {
            FlowId = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            RequiredStepsJson = "[0]",
            CompletedStepsJson = "[0]",
            InitiatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddMinutes(15)
        });
        var abandonedAt = DateTime.UtcNow;

        await repository.AbandonAsync(record.FlowId, abandonedAt);
        // Unknown flow ids must be a safe no-op.
        await repository.AbandonAsync(Guid.NewGuid(), abandonedAt);

        var loaded = await repository.GetByFlowIdAsync(record.FlowId);
        loaded!.AbandonedAt.Should().Be(abandonedAt);
    }

    [Fact]
    public async Task DeleteExpiredAsync_ShouldRemoveOnlyExpiredFlows()
    {
        await using var context = CreateContext();
        var repository = new AuthenticationFlowStateRepository(context);
        var expired = await repository.CreateAsync(new AuthenticationFlowStateRecord
        {
            FlowId = Guid.NewGuid(),
            RequiredStepsJson = "[0]",
            CompletedStepsJson = "[0]",
            InitiatedAt = DateTime.UtcNow.AddHours(-2),
            ExpiresAt = DateTime.UtcNow.AddMinutes(-90)
        });
        var active = await repository.CreateAsync(new AuthenticationFlowStateRecord
        {
            FlowId = Guid.NewGuid(),
            RequiredStepsJson = "[0]",
            CompletedStepsJson = "[0]",
            InitiatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddMinutes(15)
        });

        await repository.DeleteExpiredAsync(DateTime.UtcNow);

        (await repository.GetByFlowIdAsync(expired.FlowId)).Should().BeNull();
        (await repository.GetByFlowIdAsync(active.FlowId)).Should().NotBeNull();
    }

    private static TestAuthenticationFlowStateDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<TestAuthenticationFlowStateDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;

        return new TestAuthenticationFlowStateDbContext(options);
    }

    private sealed class TestAuthenticationFlowStateDbContext(DbContextOptions<TestAuthenticationFlowStateDbContext> options)
        : DbContext(options), IApplicationDbContext
    {
        public Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.ApplyConfiguration(new AuthenticationFlowStateConfiguration());
        }
    }
}
