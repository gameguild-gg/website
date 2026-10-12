using GameGuild.API.Database;
using GameGuild.Compliance.Audit;
using Microsoft.EntityFrameworkCore;

namespace GameGuild.Tests.Audit.Unit.Services;

/// <summary>
/// Test-specific ApplicationDbContext that ignores problematic entities
/// for in-memory database testing
/// </summary>
public class TestApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : ApplicationDbContext(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Explicitly configure AuditLog entity for testing
        modelBuilder.Entity<AuditLog>();
    }
}

/// <summary>
/// No-op redaction pipeline used by audit tests that exercise persistence paths
/// without rule-driven redaction.
/// </summary>
public sealed class NoopPiiRedactionService : IPiiRedactionService
{
    public Task<PiiRedactionSummary> ApplyToAuditLogAsync(AuditLog entry, IApplicationDbContext context, CancellationToken cancellationToken) =>
        Task.FromResult(new PiiRedactionSummary());

    public Task<CreateAuditLogRequest> RedactRequestForDurableFallbackAsync(CreateAuditLogRequest request, CancellationToken cancellationToken) =>
        Task.FromResult(request);

    public Task<PiiRedactionTextResult> RedactTextAsync(string content, Guid? tenantId, PiiRedactionSource source, IApplicationDbContext? context, CancellationToken cancellationToken) =>
        Task.FromResult(new PiiRedactionTextResult(content, new PiiRedactionSummary()));
}
