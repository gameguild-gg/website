using System.Text.Json;
using GameGuild.API.Database;
using GameGuild.Compliance.Audit;
using GameGuild.Identity.Context.Actors;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace GameGuild.Tests.Audit.Unit.Services;

public sealed class AuditReplayCaptureServiceTests : IDisposable
{
    private static readonly Guid Tenant = Guid.Parse("10000000-0000-0000-0000-000000000090");
    private static readonly Guid Admin = Guid.Parse("10000000-0000-0000-0000-000000000091");
    private static readonly DateTimeOffset Start = new(2026, 10, 11, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset End = Start.AddSeconds(1);
    private static readonly DateTimeOffset Captured = Start.AddSeconds(2);
    private static readonly AuditReplayCaptureRequest Request = new(Start, End);
    private readonly TestApplicationDbContext _context;
    private readonly Mock<IActorContextAccessor> _actors = new();
    private readonly Mock<IAuditService> _audit = new();
    private readonly Queue<bool> _auditResults = new();
    private readonly List<CreateAuditLogRequest> _access = [];
    private Action? _duringAudit;

    public AuditReplayCaptureServiceTests()
    {
        _context = new TestApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase("AuditReplayCapture_" + Guid.NewGuid()).Options);
        SetActor(Actor());
        _audit.Setup(service => service.TryLogAsync(It.IsAny<CreateAuditLogRequest>()))
            .Returns((CreateAuditLogRequest request) =>
            {
                _access.Add(request);
                _duringAudit?.Invoke();
                return Task.FromResult(_auditResults.Count == 0 || _auditResults.Dequeue());
            });
    }

    public void Dispose() => _context.Dispose();

    private static ActorContext Actor(Guid? tenant = null, bool global = false) => new()
    {
        ActorKind = ActorKind.User, SubjectId = Admin.ToString("D"), TenantId = global ? null : tenant ?? Tenant,
        IsAuthenticated = true, Roles = new HashSet<string> { "SystemAdmin" }, Permissions = new HashSet<string>()
    };

    private void SetActor(ActorContext actor) => _actors.SetupGet(accessor => accessor.ActorContext).Returns(actor);
    private AuditReplayCaptureService Service(IApplicationDbContext? context = null) =>
        new(context ?? _context, _actors.Object, _audit.Object, new FixedClock());

    private static AuditLog Event(int number, Guid? tenant, DateTime? at = null) => new()
    {
        Id = Guid.Parse($"10000000-0000-0000-0000-{number:D12}"), TenantId = tenant,
        CreatedAt = at ?? Start.UtcDateTime, UpdatedAt = Captured.UtcDateTime,
        ActionType = "Read", ResourceType = "Document", ResourceId = "doc-1",
        UserId = Admin, SessionId = null, CorrelationId = "request-1", Success = true
    };

    private async Task Seed(params AuditLog[] events)
    {
        _context.Set<AuditLog>().AddRange(events);
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)]
    [InlineData(4)] [InlineData(5)] [InlineData(6)]
    public async Task RejectsInvalidActorsBeforeAuditOrDatabaseAccess(int scenario)
    {
        var actor = scenario switch
        {
            0 => Actor() with { IsAuthenticated = false },
            1 => Actor() with { Roles = new HashSet<string> { "TenantAdmin" } },
            2 => Actor() with { Roles = new HashSet<string> { "Member" } },
            3 => Actor() with { SubjectId = null },
            4 => Actor() with { SubjectId = "invalid" },
            5 => Actor() with { SubjectId = Guid.Empty.ToString("D") },
            6 => Actor() with { TenantId = Guid.Empty },
            _ => throw new ArgumentOutOfRangeException(nameof(scenario))
        };
        SetActor(actor);
        var noRead = new Mock<IApplicationDbContext>(MockBehavior.Strict);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => Service(noRead.Object).CaptureAsync(Request, CancellationToken.None));
        Assert.Empty(_access);
        noRead.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData(0, 20, 3)]
    [InlineData(1, 19, 1)]
    [InlineData(1, 9, 0)]
    [InlineData(0, 9, 1)]
    [InlineData(11, 20, 1)]
    [InlineData(10, 19, 1)]
    public async Task FractionalBoundsSelectOnlyStoredMicrosecondRowsWithinTheOriginalRange(int startTicks, int endTicks, int expected)
    {
        await Seed(Event(1, Tenant, Start.UtcDateTime), Event(2, Tenant, Start.UtcDateTime.AddTicks(10)),
            Event(3, Tenant, Start.UtcDateTime.AddTicks(20)));
        var request = new AuditReplayCaptureRequest(Start.AddTicks(startTicks), Start.AddTicks(endTicks));
        var snapshot = await Service().CaptureAsync(request, CancellationToken.None);
        Assert.Equal(expected, snapshot.TotalCount);
        using var json = JsonDocument.Parse(snapshot.Json);
        Assert.Equal(request.Start, DateTimeOffset.Parse(json.RootElement.GetProperty("startUtc").GetString()!, System.Globalization.CultureInfo.InvariantCulture));
        Assert.Equal(request.End, DateTimeOffset.Parse(json.RootElement.GetProperty("endUtc").GetString()!, System.Globalization.CultureInfo.InvariantCulture));
        Assert.Equal(2, _access.Count);
    }

    [Fact]
    public async Task RejectsMissingRequestBeforeDatabaseAccess()
    {
        var noRead = new Mock<IApplicationDbContext>(MockBehavior.Strict);
        await Assert.ThrowsAsync<ArgumentNullException>(() => Service(noRead.Object).CaptureAsync(null!, CancellationToken.None));
        Assert.Empty(_access);
        noRead.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData(0)] [InlineData(1)]
    public async Task RejectsInvalidRangesBeforeAuditOrDatabaseAccess(int scenario)
    {
        var request = scenario == 0 ? new AuditReplayCaptureRequest(End, Start)
            : new AuditReplayCaptureRequest(Start, Captured.AddTicks(1));
        var noRead = new Mock<IApplicationDbContext>(MockBehavior.Strict);
        await Assert.ThrowsAsync<ArgumentException>(() => Service(noRead.Object).CaptureAsync(request, CancellationToken.None));
        Assert.Empty(_access);
        noRead.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task RejectsCancellationBeforeAuditOrDatabaseAccess()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var noRead = new Mock<IApplicationDbContext>(MockBehavior.Strict);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Service(noRead.Object).CaptureAsync(Request, cancellation.Token));
        Assert.Empty(_access);
        noRead.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task FailedRequestedAuditPreventsAnyDataRead()
    {
        _auditResults.Enqueue(false);
        var noRead = new Mock<IApplicationDbContext>(MockBehavior.Strict);
        await Assert.ThrowsAsync<AuditReplayCaptureAuditException>(() => Service(noRead.Object).CaptureAsync(Request, CancellationToken.None));
        Assert.Single(_access);
        Assert.Equal("AuditReplayCaptureRequested", _access[0].ActionType);
        noRead.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task CancellationDuringRequestedAuditPreventsDataRead()
    {
        using var cancellation = new CancellationTokenSource();
        _duringAudit = cancellation.Cancel;
        var noRead = new Mock<IApplicationDbContext>(MockBehavior.Strict);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Service(noRead.Object).CaptureAsync(Request, cancellation.Token));
        Assert.Single(_access);
        noRead.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task CapturesOnlyExactActorTenantAndExplicitGlobalNull(bool global)
    {
        SetActor(Actor(global: global));
        await Seed(Event(1, Tenant), Event(2, Guid.NewGuid()), Event(3, null));
        var snapshot = await Service().CaptureAsync(Request, CancellationToken.None);
        Assert.Single(snapshot.Events);
        Assert.Equal(global ? null : Tenant, snapshot.TenantId);
        Assert.Equal(Event(global ? 3 : 1, null).Id.ToString("D"), snapshot.Events[0].Id);
        Assert.All(_access, item => Assert.Equal(global ? null : Tenant, item.TenantId));
        Assert.All(_access, item => Assert.Equal(Admin, item.UserId));
    }

    [Fact]
    public async Task IncludesBoundariesAndExcludesOutsideRangeAndDeletedRows()
    {
        var deleted = Event(5, Tenant); deleted.DeletedAt = Captured.UtcDateTime;
        await Seed(Event(1, Tenant, Start.UtcDateTime.AddTicks(-1)), Event(2, Tenant),
            Event(3, Tenant, End.UtcDateTime), Event(4, Tenant, End.UtcDateTime.AddTicks(1)), deleted);
        var offset = TimeSpan.FromHours(-3);
        var snapshot = await Service().CaptureAsync(new(Start.ToOffset(offset), End.ToOffset(offset)), CancellationToken.None);
        Assert.Equal([Event(2, null).Id.ToString("D"), Event(3, null).Id.ToString("D")], snapshot.Events.Select(item => item.Id));
        Assert.Equal(TimeSpan.Zero, snapshot.StartUtc.Offset);
        Assert.Equal(TimeSpan.Zero, snapshot.EndUtc.Offset);
    }

    [Fact]
    public async Task PreservesTicksAndStableTiesWithoutExposingFreeFormContent()
    {
        var input = Event(3, Tenant, Start.UtcDateTime.AddTicks(1));
        input.Description = "SECRET_DESCRIPTION"; input.IpAddress = "SECRET_ADDRESS";
        input.UserAgent = "SECRET_AGENT"; input.Metadata = "SECRET_METADATA"; input.ErrorMessage = "SECRET_ERROR";
        await Seed(input, Event(2, Tenant), Event(1, Tenant));
        var snapshot = await Service().CaptureAsync(Request, CancellationToken.None);
        Assert.Equal([Event(1, null).Id.ToString("D"), Event(2, null).Id.ToString("D"), input.Id.ToString("D")], snapshot.Events.Select(item => item.Id));
        Assert.Equal("2026-10-11T00:00:00.0000001Z", snapshot.Events[2].CreatedAt);
        Assert.DoesNotContain("SECRET_", snapshot.Json);
        Assert.Equal(2, _access.Count);
        Assert.Equal("AuditReplayCaptureCompleted", _access[1].ActionType);
        Assert.DoesNotContain("SECRET_", JsonSerializer.Serialize(_access));
        Assert.True(AuditReplaySnapshot.MatchesDigest(snapshot.Json, snapshot.Sha256));
        Assert.Empty(_context.ChangeTracker.Entries<AuditLog>());
    }

    [Fact]
    public async Task ActorMutationDuringAwaitDoesNotChangeCapturedScopeOrAuditIdentity()
    {
        await Seed(Event(1, Tenant), Event(2, Guid.NewGuid()));
        var first = true;
        _duringAudit = () =>
        {
            if (first)
            {
                first = false;
                SetActor(Actor(Guid.NewGuid()) with { SubjectId = Guid.NewGuid().ToString("D") });
            }
        };
        var snapshot = await Service().CaptureAsync(Request, CancellationToken.None);
        Assert.Single(snapshot.Events);
        Assert.Equal(Tenant, snapshot.TenantId);
        Assert.All(_access, item => Assert.Equal(Tenant, item.TenantId));
        Assert.All(_access, item => Assert.Equal(Admin, item.UserId));
    }

    [Fact]
    public async Task FailedCompletedAuditPreventsSnapshotReturn()
    {
        await Seed(Event(1, Tenant));
        _auditResults.Enqueue(true); _auditResults.Enqueue(false);
        await Assert.ThrowsAsync<AuditReplayCaptureAuditException>(() => Service().CaptureAsync(Request, CancellationToken.None));
        Assert.Equal(2, _access.Count);
        Assert.Equal("AuditReplayCaptureCompleted", _access[1].ActionType);
    }

    [Fact]
    public async Task DatabaseFailureRecordsSafeFailureAndPropagates()
    {
        var context = new Mock<IApplicationDbContext>(MockBehavior.Strict);
        context.Setup(value => value.Set<AuditLog>()).Throws(new InvalidOperationException("PRIVATE_DATABASE_DETAIL"));
        var failure = await Assert.ThrowsAsync<InvalidOperationException>(() => Service(context.Object).CaptureAsync(Request, CancellationToken.None));
        Assert.Equal("PRIVATE_DATABASE_DETAIL", failure.Message);
        Assert.Equal(2, _access.Count);
        Assert.Equal("AuditReplayCaptureFailed", _access[1].ActionType);
        Assert.False(_access[1].Success);
        Assert.DoesNotContain("PRIVATE_DATABASE_DETAIL", JsonSerializer.Serialize(_access));
    }

    [Fact]
    public async Task FailureAuditOutageAlsoFailsClosed()
    {
        var context = new Mock<IApplicationDbContext>(MockBehavior.Strict);
        context.Setup(value => value.Set<AuditLog>()).Throws(new InvalidOperationException("PRIVATE_DATABASE_DETAIL"));
        _auditResults.Enqueue(true); _auditResults.Enqueue(false);
        await Assert.ThrowsAsync<AuditReplayCaptureAuditException>(() => Service(context.Object).CaptureAsync(Request, CancellationToken.None));
        Assert.Equal(2, _access.Count);
        Assert.False(_access[1].Success);
    }

    [Fact]
    public async Task MalformedStoredEventFailsAtomicallyAndIsAudited()
    {
        var invalid = Event(1, Tenant); invalid.ActionType = "";
        await Seed(invalid);
        await Assert.ThrowsAsync<ArgumentException>(() => Service().CaptureAsync(Request, CancellationToken.None));
        Assert.Equal(2, _access.Count);
        Assert.Equal("AuditReplayCaptureFailed", _access[1].ActionType);
    }

    [Theory]
    [InlineData(10000)] [InlineData(10001)]
    public async Task CapturesExactCapacityButRejectsAnyOverflow(int count)
    {
        await Seed(Enumerable.Range(1, count).Select(number => Event(number, Tenant)).ToArray());
        if (count == 10000)
        {
            var snapshot = await Service().CaptureAsync(Request, CancellationToken.None);
            Assert.Equal(10000, snapshot.TotalCount);
            Assert.Equal("AuditReplayCaptureCompleted", _access[1].ActionType);
        }
        else
        {
            await Assert.ThrowsAsync<ArgumentException>(() => Service().CaptureAsync(Request, CancellationToken.None));
            Assert.Equal("AuditReplayCaptureFailed", _access[1].ActionType);
        }
    }

    [Fact]
    public async Task EmptySelectionStillRequiresCompletedAccessAudit()
    {
        var snapshot = await Service().CaptureAsync(Request, CancellationToken.None);
        Assert.Equal(0, snapshot.TotalCount);
        Assert.Equal(2, _access.Count);
        Assert.Equal("AuditReplayCaptureCompleted", _access[1].ActionType);
    }

    private sealed class FixedClock : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => Captured;
    }
}
