using FluentAssertions;
using GameGuild.Identity.Authorization;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace GameGuild.Identity.Authorization.UnitTests.Services;

/// <summary>
/// Focused regression coverage for one-sided permission-history date filters.
/// Repository mocks keep this service coverage independent of a database.
/// </summary>
public class PermissionAuditServiceHistoryTests
{
    private static readonly DateTime Boundary = new(2026, 10, 10, 12, 0, 0, DateTimeKind.Utc);

    private static PermissionAuditLog Entry(int offsetHours) =>
        new() { Timestamp = Boundary.AddHours(offsetHours) };

    private static PermissionAuditService Service(Mock<IPermissionAuditLogRepository> repository) =>
        new(repository.Object, NullLogger<PermissionAuditService>.Instance);

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task SingleBound_IsInclusive_AndForwardsTenantAndCancellation(bool lowerBound)
    {
        var tenant = Guid.NewGuid();
        using var cancellation = new CancellationTokenSource();
        var token = cancellation.Token;
        List<PermissionAuditLog> rows = [Entry(-1), Entry(0), Entry(1)];
        var repository = new Mock<IPermissionAuditLogRepository>(MockBehavior.Strict);
        repository.Setup(r => r.GetByTenantAsync(tenant, token)).ReturnsAsync(rows);

        var result = await Service(repository).GetAuditLogsAsync(
            tenant,
            fromDate: lowerBound ? Boundary : null,
            toDate: lowerBound ? null : Boundary,
            cancellationToken: token);

        result.Should().Equal(lowerBound ? rows.Skip(1) : rows.Take(2));
        rows.Should().HaveCount(3);
        repository.Verify(r => r.GetByTenantAsync(tenant, token), Times.Once);
        repository.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task SingleBound_AppliesLimitAfterFiltering(bool lowerBound)
    {
        var tenant = Guid.NewGuid();
        List<PermissionAuditLog> rows = lowerBound
            ? [Entry(-1), Entry(0), Entry(1)]
            : [Entry(1), Entry(0), Entry(-1)];
        var repository = new Mock<IPermissionAuditLogRepository>(MockBehavior.Strict);
        repository.Setup(r => r.GetByTenantAsync(tenant, CancellationToken.None)).ReturnsAsync(rows);

        var result = await Service(repository).GetAuditLogsAsync(
            tenant,
            fromDate: lowerBound ? Boundary : null,
            toDate: lowerBound ? null : Boundary,
            limit: 1);

        result.Should().ContainSingle().Which.Should().BeSameAs(rows[1]);
        repository.Verify(r => r.GetByTenantAsync(tenant, CancellationToken.None), Times.Once);
        repository.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task SingleBound_WithoutMatchingEntries_ReturnsEmpty(bool lowerBound)
    {
        var tenant = Guid.NewGuid();
        List<PermissionAuditLog> rows = [Entry(lowerBound ? -1 : 1)];
        var repository = new Mock<IPermissionAuditLogRepository>(MockBehavior.Strict);
        repository.Setup(r => r.GetByTenantAsync(tenant, CancellationToken.None)).ReturnsAsync(rows);

        var result = await Service(repository).GetAuditLogsAsync(
            tenant,
            fromDate: lowerBound ? Boundary : null,
            toDate: lowerBound ? null : Boundary);

        result.Should().BeEmpty();
        repository.Verify(r => r.GetByTenantAsync(tenant, CancellationToken.None), Times.Once);
        repository.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task BothBounds_PreserveIndexedRepositoryPathAndLimit()
    {
        var tenant = Guid.NewGuid();
        using var cancellation = new CancellationTokenSource();
        var token = cancellation.Token;
        var end = Boundary.AddHours(1);
        List<PermissionAuditLog> rows = [Entry(0), Entry(1)];
        var repository = new Mock<IPermissionAuditLogRepository>(MockBehavior.Strict);
        repository.Setup(r => r.GetByDateRangeAsync(Boundary, end, tenant, token)).ReturnsAsync(rows);

        var result = await Service(repository).GetAuditLogsAsync(tenant, Boundary, end, 1, token);

        result.Should().ContainSingle().Which.Should().BeSameAs(rows[0]);
        repository.Verify(r => r.GetByDateRangeAsync(Boundary, end, tenant, token), Times.Once);
        repository.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task NoBounds_PreservesReturnedOrderWithoutInventingDateBounds()
    {
        var tenant = Guid.NewGuid();
        List<PermissionAuditLog> rows = [Entry(1), Entry(-1), Entry(0)];
        var repository = new Mock<IPermissionAuditLogRepository>(MockBehavior.Strict);
        repository.Setup(r => r.GetByTenantAsync(tenant, CancellationToken.None)).ReturnsAsync(rows);

        var result = await Service(repository).GetAuditLogsAsync(tenant);

        result.Should().Equal(rows);
        repository.Verify(r => r.GetByTenantAsync(tenant, CancellationToken.None), Times.Once);
        repository.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task ExplicitGlobalScope_IsPassedAsNullWithoutInventingAnotherTenant()
    {
        List<PermissionAuditLog> rows = [Entry(-1), Entry(0)];
        var repository = new Mock<IPermissionAuditLogRepository>(MockBehavior.Strict);
        repository.Setup(r => r.GetByTenantAsync(null, CancellationToken.None)).ReturnsAsync(rows);

        var result = await Service(repository).GetAuditLogsAsync(null, fromDate: Boundary);

        result.Should().ContainSingle().Which.Should().BeSameAs(rows[1]);
        repository.Verify(r => r.GetByTenantAsync(null, CancellationToken.None), Times.Once);
        repository.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task ZeroLimit_ReturnsEmptyAfterApplyingSingleBound()
    {
        var tenant = Guid.NewGuid();
        List<PermissionAuditLog> rows = [Entry(0)];
        var repository = new Mock<IPermissionAuditLogRepository>(MockBehavior.Strict);
        repository.Setup(r => r.GetByTenantAsync(tenant, CancellationToken.None)).ReturnsAsync(rows);

        var result = await Service(repository).GetAuditLogsAsync(tenant, fromDate: Boundary, limit: 0);

        result.Should().BeEmpty();
        repository.Verify(r => r.GetByTenantAsync(tenant, CancellationToken.None), Times.Once);
        repository.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task CancellationFailure_PropagatesWithTheSuppliedToken()
    {
        var tenant = Guid.NewGuid();
        using var cancellation = new CancellationTokenSource();
        var token = cancellation.Token;
        cancellation.Cancel();
        var repository = new Mock<IPermissionAuditLogRepository>(MockBehavior.Strict);
        repository.Setup(r => r.GetByTenantAsync(tenant, token))
            .ThrowsAsync(new OperationCanceledException(token));

        Func<Task> query = () => Service(repository).GetAuditLogsAsync(tenant, fromDate: Boundary, cancellationToken: token);

        var result = await query.Should().ThrowAsync<OperationCanceledException>();
        result.Which.CancellationToken.Should().Be(token);
        repository.Verify(r => r.GetByTenantAsync(tenant, token), Times.Once);
        repository.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task RepositoryFailure_IsNotConvertedToAnEmptyHistory()
    {
        var tenant = Guid.NewGuid();
        var expected = new InvalidOperationException("Synthetic repository failure");
        var repository = new Mock<IPermissionAuditLogRepository>(MockBehavior.Strict);
        repository.Setup(r => r.GetByTenantAsync(tenant, CancellationToken.None)).ThrowsAsync(expected);

        Func<Task> query = () => Service(repository).GetAuditLogsAsync(tenant, toDate: Boundary);

        var result = await query.Should().ThrowAsync<InvalidOperationException>();
        result.Which.Should().BeSameAs(expected);
        repository.Verify(r => r.GetByTenantAsync(tenant, CancellationToken.None), Times.Once);
        repository.VerifyNoOtherCalls();
    }
}
