using GameGuild;
using GameGuild.API.Database;
using GameGuild.Compliance.Audit;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Xunit;

namespace GameGuild.Tests.Audit.Unit.Services;

public sealed class ScheduledAuditExportRepositoryTests : IDisposable
{
    private readonly TestApplicationDbContext _context;
    private readonly ScheduledAuditExportRepository _repository;

    public ScheduledAuditExportRepositoryTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"ScheduledAuditExportRepository_{Guid.NewGuid()}", new InMemoryDatabaseRoot())
            .Options;
        _context = new TestApplicationDbContext(options);
        _repository = new ScheduledAuditExportRepository(_context);
    }

    public void Dispose()
    {
        SystemClock.Reset();
        _context.Dispose();
    }

    [Fact]
    public async Task RecoverStaleClaimsAsync_FailsOldInProgressExecutionAndResetsNextRunAt()
    {
        var now = new DateTime(2026, 10, 5, 12, 0, 0, DateTimeKind.Utc);
        var export = ScheduledAuditExport.Create(Guid.NewGuid(), "Daily audit", "0 8 * * *", ExportDestinationType.TenantStorage, "tenant-storage", ExportFormat.Csv);
        var staleHistory = CreateHistory(export, now.AddMinutes(-31));
        var freshExport = ScheduledAuditExport.Create(Guid.NewGuid(), "Hourly audit", "0 * * * *", ExportDestinationType.TenantStorage, "tenant-storage", ExportFormat.Csv);
        var freshHistory = CreateHistory(freshExport, now.AddMinutes(-1));
        _context.Set<ScheduledAuditExport>().AddRange(export, freshExport);
        _context.Set<AuditExportHistory>().AddRange(staleHistory, freshHistory);
        await _context.SaveChangesAsync();

        var recovered = await _repository.RecoverStaleClaimsAsync(now, TimeSpan.FromMinutes(30), CancellationToken.None);

        Assert.Equal(1, recovered);
        Assert.Equal(ExportStatus.Failed, staleHistory.Status);
        Assert.Equal("Stale claim recovered", staleHistory.ErrorMessage);
        Assert.Equal(now, export.NextRunAt);
        Assert.Equal(1, export.FailureCount);
        Assert.Equal(ExportStatus.InProgress, freshHistory.Status);
        Assert.NotEqual(now, freshExport.NextRunAt);
    }

    [Fact]
    public async Task RecoverStaleClaimsAsync_LeavesFreshInProgressExecutionsUntouched()
    {
        var now = new DateTime(2026, 10, 5, 12, 0, 0, DateTimeKind.Utc);
        var export = ScheduledAuditExport.Create(Guid.NewGuid(), "Daily audit", "0 8 * * *", ExportDestinationType.TenantStorage, "tenant-storage", ExportFormat.Csv);
        var freshHistory = CreateHistory(export, now.AddMinutes(-29));
        _context.Set<ScheduledAuditExport>().Add(export);
        _context.Set<AuditExportHistory>().Add(freshHistory);
        await _context.SaveChangesAsync();

        var recovered = await _repository.RecoverStaleClaimsAsync(now, TimeSpan.FromMinutes(30), CancellationToken.None);

        Assert.Equal(0, recovered);
        Assert.Equal(ExportStatus.InProgress, freshHistory.Status);
        Assert.Null(freshHistory.ErrorMessage);
        Assert.Equal(0, export.FailureCount);
    }

    [Fact]
    public async Task RecoverStaleClaimsAsync_WithStaleAndFreshClaimsOnSameExport_DoesNotRequeueSchedule()
    {
        var now = new DateTime(2026, 10, 5, 12, 0, 0, DateTimeKind.Utc);
        var export = ScheduledAuditExport.Create(Guid.NewGuid(), "Daily audit", "0 8 * * *", ExportDestinationType.TenantStorage, "tenant-storage", ExportFormat.Csv);
        var scheduledNextRun = now.AddHours(4);
        export.UpdateNextRunTime(scheduledNextRun);
        var staleHistory = CreateHistory(export, now.AddMinutes(-45));
        var freshHistory = CreateHistory(export, now.AddMinutes(-2));
        _context.Set<ScheduledAuditExport>().Add(export);
        _context.Set<AuditExportHistory>().AddRange(staleHistory, freshHistory);
        await _context.SaveChangesAsync();

        var recovered = await _repository.RecoverStaleClaimsAsync(now, TimeSpan.FromMinutes(30), CancellationToken.None);

        Assert.Equal(1, recovered);
        Assert.Equal(ExportStatus.Failed, staleHistory.Status);
        Assert.Equal(ExportStatus.InProgress, freshHistory.Status);
        Assert.NotEqual(now, export.NextRunAt);
        Assert.Equal(scheduledNextRun, export.NextRunAt);
        Assert.Equal(1, export.FailureCount);
    }

    private static AuditExportHistory CreateHistory(ScheduledAuditExport export, DateTime executedAtUtc)
    {
        SystemClock.SetProvider(new FixedTimeProvider(executedAtUtc));
        return AuditExportHistory.Create(export.Id, export.TenantId!.Value);
    }

    private sealed class FixedTimeProvider(DateTime utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(utcNow, TimeSpan.Zero);
    }
}
