using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Xunit;

namespace GameGuild.Identity.Authentication.UnitTests.Repositories;

public class AuthenticationAttemptRepositoryTests
{
    [Fact]
    public async Task CreateAndLookupMethods_ShouldNormalizeEmail_AndReturnOrderedAttempts()
    {
        var recordedAt = new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.Zero);
        SystemClock.SetProvider(new FrozenTimestampClock(recordedAt));
        try
        {
            await using var context = CreateContext();
            var repository = new AuthenticationAttemptRepository(context);
            var userId = Guid.NewGuid();

            var oldest = await repository.CreateAsync(CreateAttempt(
                email: "User@Example.com",
                userId: userId,
                attemptedAt: DateTime.UtcNow.AddMinutes(-10),
                isSuccessful: false));

            var newest = await repository.CreateAsync(CreateAttempt(
                email: "user@example.com",
                userId: userId,
                attemptedAt: DateTime.UtcNow.AddMinutes(-2),
                isSuccessful: true));

            await repository.CreateAsync(CreateAttempt(
                email: "other@example.com",
                userId: Guid.NewGuid(),
                attemptedAt: DateTime.UtcNow.AddMinutes(-1)));

            oldest.Email.Should().Be("user@example.com");
            oldest.Id.Should().NotBe(Guid.Empty);
            oldest.UpdatedAt.Should().Be(recordedAt.UtcDateTime);

            var byId = await repository.GetByIdAsync(oldest.Id);
            var byEmail = await repository.GetByEmailAsync("USER@example.com");
            var byUser = await repository.GetByUserIdAsync(userId, limit: 1);

            byId.Should().NotBeNull();
            byId!.Id.Should().Be(oldest.Id);
            byEmail.Select(attempt => attempt.Id).Should().ContainInOrder(newest.Id, oldest.Id);
            byUser.Should().ContainSingle();
            byUser[0].Id.Should().Be(newest.Id);
        }
        finally
        {
            SystemClock.Reset();
        }
    }

    [Fact]
    public async Task SuspiciousAndFailureQueries_ShouldFilterByRiskEmailAndIp()
    {
        await using var context = CreateContext();
        var repository = new AuthenticationAttemptRepository(context);
        var since = DateTime.UtcNow.AddHours(-2);
        var email = "victim@example.com";
        var ipAddress = "203.0.113.10";

        context.Set<AuthenticationAttempt>().AddRange(
            CreateAttempt(email: email, ipAddress: ipAddress, attemptedAt: DateTime.UtcNow.AddMinutes(-30), isSuccessful: false, isSuspicious: true, riskScore: 40),
            CreateAttempt(email: email, ipAddress: ipAddress, attemptedAt: DateTime.UtcNow.AddMinutes(-20), isSuccessful: false, isSuspicious: false, riskScore: 90),
            CreateAttempt(email: email, ipAddress: ipAddress, attemptedAt: DateTime.UtcNow.AddMinutes(-10), isSuccessful: true, isSuspicious: true, riskScore: 85),
            CreateAttempt(email: "other@example.com", ipAddress: "203.0.113.11", attemptedAt: DateTime.UtcNow.AddMinutes(-5), isSuccessful: false, isSuspicious: false, riskScore: 10),
            CreateAttempt(email: email, ipAddress: ipAddress, attemptedAt: DateTime.UtcNow.AddDays(-2), isSuccessful: false, isSuspicious: true, riskScore: 95));

        await context.SaveChangesAsync();

        var suspicious = await repository.GetSuspiciousAttemptsAsync(since);
        var failedByEmail = await repository.GetFailedAttemptsAsync("Victim@Example.com", since);
        var failedByIp = await repository.GetFailedAttemptsAsync(ipAddress, since);
        var failedEmailCount = await repository.CountFailedAttemptsAsync("Victim@Example.com", since);
        var failedIpCount = await repository.CountFailedAttemptsByIpAsync(ipAddress, since);

        suspicious.Should().HaveCount(3);
        suspicious.Should().OnlyContain(attempt => attempt.AttemptedAt >= since && (attempt.IsSuspicious || attempt.RiskScore > 70));
        failedByEmail.Should().HaveCount(2);
        failedByEmail.Should().OnlyContain(attempt => !attempt.IsSuccessful && attempt.Email == email);
        failedByIp.Should().HaveCount(2);
        failedIpCount.Should().Be(2);
        failedEmailCount.Should().Be(2);
    }

    [Fact]
    public async Task RangeRecentAndStatisticsQueries_ShouldApplyOrderingFiltersAndLimits()
    {
        await using var context = CreateContext();
        var repository = new AuthenticationAttemptRepository(context);
        var userId = Guid.NewGuid();
        var ipAddress = "198.51.100.20";
        var since = DateTime.UtcNow.AddHours(-1);

        var oldest = CreateAttempt(userId: userId, ipAddress: ipAddress, attemptedAt: DateTime.UtcNow.AddMinutes(-50), isSuccessful: false);
        var middle = CreateAttempt(userId: userId, ipAddress: ipAddress, attemptedAt: DateTime.UtcNow.AddMinutes(-40), isSuccessful: true);
        var newest = CreateAttempt(userId: userId, ipAddress: ipAddress, attemptedAt: DateTime.UtcNow.AddMinutes(-10), isSuccessful: true);
        var userDifferentIp = CreateAttempt(userId: userId, ipAddress: "198.51.100.21", attemptedAt: DateTime.UtcNow.AddMinutes(-15), isSuccessful: false);
        var otherUserSameIp = CreateAttempt(userId: Guid.NewGuid(), ipAddress: ipAddress, attemptedAt: DateTime.UtcNow.AddMinutes(-5), isSuccessful: false);

        context.Set<AuthenticationAttempt>().AddRange(
            oldest,
            middle,
            newest,
            CreateAttempt(userId: userId, ipAddress: ipAddress, attemptedAt: DateTime.UtcNow.AddHours(-3), isSuccessful: false),
            otherUserSameIp,
            userDifferentIp);

        await context.SaveChangesAsync();

        var rangedByIp = await repository.GetByIpAddressAsync(ipAddress, DateTime.UtcNow.AddMinutes(-45), DateTime.UtcNow.AddMinutes(-5));
        var recentByUser = await repository.GetRecentAttemptsAsync(userId, since, limit: 2);
        var recentByIp = await repository.GetRecentAttemptsByIpAsync(ipAddress, since, limit: 2);
        var lastSuccessful = await repository.GetLastSuccessfulAttemptAsync(userId);
        var statistics = await repository.GetUserStatisticsAsync(userId, since);

        rangedByIp.Select(attempt => attempt.Id).Should().ContainInOrder(newest.Id, middle.Id);
        rangedByIp.Should().NotContain(attempt => attempt.Id == oldest.Id);
        recentByUser.Should().HaveCount(2);
        recentByUser.Select(attempt => attempt.Id).Should().ContainInOrder(newest.Id, userDifferentIp.Id);
        recentByIp.Should().HaveCount(2);
        recentByIp.Select(attempt => attempt.Id).Should().ContainInOrder(otherUserSameIp.Id, newest.Id);
        lastSuccessful.Should().NotBeNull();
        lastSuccessful!.Id.Should().Be(newest.Id);
        statistics.TotalAttempts.Should().Be(4);
        statistics.SuccessfulAttempts.Should().Be(2);
        statistics.FailedAttempts.Should().Be(2);
    }

    [Fact]
    public async Task UpdateDeleteAndCleanupMethods_ShouldPersistExpectedChanges()
    {
        await using var context = CreateContext();
        var repository = new AuthenticationAttemptRepository(context);

        var attempt = await repository.CreateAsync(CreateAttempt(email: "cleanup@example.com", attemptedAt: DateTime.UtcNow.AddMinutes(-15), riskScore: 10));
        var originalUpdatedAt = attempt.UpdatedAt;

        attempt.RiskScore = 77;
        attempt.Metadata = "updated";

        var updated = await repository.UpdateAsync(attempt);
        var missingDelete = await repository.DeleteAsync(Guid.NewGuid());
        var deleted = await repository.DeleteAsync(attempt.Id);

        updated.RiskScore.Should().Be(77);
        updated.Metadata.Should().Be("updated");
        updated.UpdatedAt.Should().BeOnOrAfter(originalUpdatedAt);
        missingDelete.Should().BeFalse();
        deleted.Should().BeTrue();
        (await repository.GetByIdAsync(attempt.Id)).Should().BeNull();

        context.Set<AuthenticationAttempt>().AddRange(
            CreateAttempt(email: "old-one@example.com", attemptedAt: DateTime.UtcNow.AddDays(-40)),
            CreateAttempt(email: "old-two@example.com", attemptedAt: DateTime.UtcNow.AddDays(-20)),
            CreateAttempt(email: "new-one@example.com", attemptedAt: DateTime.UtcNow.AddMinutes(-1)));

        await context.SaveChangesAsync();

        var cleaned = await repository.CleanupOldAttemptsAsync(DateTime.UtcNow.AddDays(-10));
        cleaned.Should().Be(2);
        (await repository.GetByEmailAsync("new-one@example.com")).Should().ContainSingle();

        context.Set<AuthenticationAttempt>().AddRange(
            CreateAttempt(email: "delete-old@example.com", attemptedAt: DateTime.UtcNow.AddDays(-60)),
            CreateAttempt(email: "delete-new@example.com", attemptedAt: DateTime.UtcNow.AddMinutes(-2)));

        await context.SaveChangesAsync();

        await repository.DeleteOlderThanAsync(DateTime.UtcNow.AddDays(-30));

        (await repository.GetByEmailAsync("delete-old@example.com")).Should().BeEmpty();
        (await repository.GetByEmailAsync("delete-new@example.com")).Should().ContainSingle();
    }

    private static TestAuthenticationAttemptDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<TestAuthenticationAttemptDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;

        return new TestAuthenticationAttemptDbContext(options);
    }

    private static AuthenticationAttempt CreateAttempt(
        string email = "user@example.com",
        Guid? userId = null,
        string ipAddress = "203.0.113.1",
        DateTime? attemptedAt = null,
        bool isSuccessful = false,
        bool isSuspicious = false,
        int riskScore = 0)
    {
        return new AuthenticationAttempt
        {
            Id = Guid.NewGuid(),
            Email = email,
            UserId = userId,
            IpAddress = ipAddress,
            UserAgent = "unit-test-agent",
            IsSuccessful = isSuccessful,
            FailureReason = isSuccessful ? null : "invalid_credentials",
            AttemptedAt = attemptedAt ?? DateTime.UtcNow,
            ProcessingTime = TimeSpan.FromMilliseconds(120),
            Location = "Test City",
            DeviceFingerprint = "device-fingerprint",
            SessionId = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            IsSuspicious = isSuspicious,
            RiskScore = riskScore,
            Metadata = "{}",
            CorrelationId = Guid.NewGuid().ToString("N"),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }

    private sealed class FrozenTimestampClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class TestAuthenticationAttemptDbContext(DbContextOptions<TestAuthenticationAttemptDbContext> options) : DbContext(options), IApplicationDbContext
    {
        public Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<AuthenticationAttempt>(builder =>
            {
                builder.HasKey(x => x.Id);
                builder.Property(x => x.Email).IsRequired();
                builder.Property(x => x.IpAddress).IsRequired();
            });

            base.OnModelCreating(modelBuilder);
        }
    }
}