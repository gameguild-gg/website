using System.Text.Json;
using GameGuild.Compliance.Audit;
using GameGuild.Identity.Context.Actors;
using Moq;
using Xunit;

namespace GameGuild.Tests.Audit.Unit.Services;

public sealed class AuditReplayReconstructionServiceTests
{
    private static readonly Guid Tenant = Guid.Parse("10000000-0000-0000-0000-000000000190");
    private static readonly Guid Admin = Guid.Parse("10000000-0000-0000-0000-000000000191");
    private static readonly Guid SecondUser = Guid.Parse("10000000-0000-0000-0000-000000000192");
    private static readonly Guid Session = Guid.Parse("10000000-0000-0000-0000-000000000193");
    private static readonly Guid OtherSession = Guid.Parse("10000000-0000-0000-0000-000000000194");
    private static readonly DateTimeOffset Start = new(2026, 10, 12, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset End = Start.AddSeconds(30);
    private static readonly DateTimeOffset Captured = Start.AddSeconds(60);
    private readonly Mock<IActorContextAccessor> _actors = new(MockBehavior.Strict);
    private readonly Mock<IAuditService> _audit = new();
    private readonly List<CreateAuditLogRequest> _access = [];
    private readonly Queue<bool> _auditResults = new();

    public AuditReplayReconstructionServiceTests()
    {
        SetActor(Actor());
        _audit.Setup(service => service.TryLogAsync(It.IsAny<CreateAuditLogRequest>()))
            .Returns((CreateAuditLogRequest request) =>
            {
                _access.Add(request);
                return Task.FromResult(_auditResults.Count == 0 || _auditResults.Dequeue());
            });
    }

    private static ActorContext Actor(Guid? tenant = null) => new()
    {
        ActorKind = ActorKind.User, SubjectId = Admin.ToString("D"), TenantId = tenant ?? Tenant,
        IsAuthenticated = true, Roles = new HashSet<string> { "SystemAdmin" }, Permissions = new HashSet<string>()
    };

    private void SetActor(ActorContext actor) => _actors.SetupGet(accessor => accessor.ActorContext).Returns(actor);
    private AuditReplayReconstructionService Service() => new(_actors.Object, _audit.Object);

    private static AuditLogDto Event(int number, Guid user, Guid? session, bool success = true, DateTime? at = null) => new()
    {
        Id = Guid.Parse($"20000000-0000-0000-0000-{number:D12}"), TenantId = Tenant,
        CreatedAt = at ?? Start.UtcDateTime, ActionType = "Read", ResourceType = "Document",
        ResourceId = "doc-1", UserId = user, SessionId = session, CorrelationId = "request-1", Success = success
    };

    private static AuditReplaySnapshot Capture(params AuditLogDto[] events) =>
        AuditReplaySnapshot.Capture(Tenant, events, events.Length, Start, End, Captured);

    /// <summary>Mirrors the client: the posted document is exactly what the capture JSON described.</summary>
    private static AuditReplaySnapshotDocument DocumentOf(AuditReplaySnapshot snapshot)
    {
        using var json = JsonDocument.Parse(snapshot.Json);
        var root = json.RootElement;
        return new AuditReplaySnapshotDocument(
            root.GetProperty("schemaVersion").GetInt32(),
            root.GetProperty("tenantId").GetString(),
            root.GetProperty("totalCount").GetInt32(),
            root.GetProperty("startUtc").GetString()!,
            root.GetProperty("endUtc").GetString()!,
            root.GetProperty("capturedAtUtc").GetString()!,
            [.. root.GetProperty("events").EnumerateArray().Select(item => new AuditReplaySnapshotEvent(
                item.GetProperty("id").GetString()!,
                item.GetProperty("tenantId").GetString(),
                item.GetProperty("createdAt").GetString()!,
                item.GetProperty("actionType").GetString()!,
                item.GetProperty("resourceType").GetString()!,
                item.GetProperty("resourceId").GetString(),
                item.GetProperty("userId").GetString(),
                item.GetProperty("sessionId").GetString(),
                item.GetProperty("correlationId").GetString(),
                item.GetProperty("success").GetBoolean()))]);
    }

    [Fact]
    public async Task ReconstructsChronologicalTimelineWithUserJourneysAndSessions()
    {
        var later = Start.UtcDateTime.AddSeconds(5);
        var snapshot = Capture(
            Event(2, SecondUser, OtherSession, success: false, at: later),
            Event(1, Admin, Session));
        var document = DocumentOf(snapshot);

        var reconstruction = await Service().ReconstructAsync(document, snapshot.Sha256, CancellationToken.None);

        Assert.Equal(snapshot.Sha256, reconstruction.Sha256);
        Assert.Equal(Tenant, reconstruction.TenantId);
        Assert.Equal(2, reconstruction.TotalCount);
        Assert.Equal(1, reconstruction.FailedCount);
        // Chronological ordering is re-derived, not trusted from the posted order.
        Assert.Equal(snapshot.Events[0].Id, reconstruction.Events[0].Id);
        Assert.Equal(snapshot.Events[1].Id, reconstruction.Events[1].Id);
        Assert.Equal(2, reconstruction.Users.Count);
        var admin = Assert.Single(reconstruction.Users, item => item.UserId == Admin.ToString("D"));
        Assert.Equal(1, admin.EventCount);
        Assert.Equal(0, admin.FailedCount);
        Assert.Equal([Session.ToString("D")], admin.SessionIds);
        var second = Assert.Single(reconstruction.Users, item => item.UserId == SecondUser.ToString("D"));
        Assert.Equal(1, second.FailedCount);
        Assert.Equal(later.ToString("O", System.Globalization.CultureInfo.InvariantCulture), second.LastSeenAtUtc);
        Assert.Equal(2, reconstruction.Sessions.Count);
        var other = Assert.Single(reconstruction.Sessions, item => item.SessionId == OtherSession.ToString("D"));
        Assert.Equal([SecondUser.ToString("D")], other.UserIds);
        // Successful reconstruction: the requested audit and the completed audit.
        Assert.Equal(2, _access.Count);
        Assert.Equal("AuditReplayReconstructionRequested", _access[0].ActionType);
        Assert.Equal("AuditReplayReconstructionCompleted", _access[^1].ActionType);
        Assert.True(_access.All(item => item.UserId == Admin && item.TenantId == Tenant));
    }

    [Fact]
    public async Task DigestMismatchFailsClosedAndIsAuditedAsFailure()
    {
        var snapshot = Capture(Event(1, Admin, Session));
        var document = DocumentOf(snapshot);
        var tampered = snapshot.Sha256.Substring(0, 63) + (snapshot.Sha256[^1] == '0' ? '1' : '0');

        await Assert.ThrowsAsync<AuditReplayIntegrityMismatchException>(
            () => Service().ReconstructAsync(document, tampered, CancellationToken.None));

        Assert.Equal(2, _access.Count);
        Assert.Equal("AuditReplayReconstructionFailed", _access[^1].ActionType);
        Assert.False(_access[^1].Success);
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)] [InlineData(4)] [InlineData(5)]
    public async Task RejectsInvalidActorsBeforeAnyAudit(int scenario)
    {
        var actor = scenario switch
        {
            0 => Actor() with { IsAuthenticated = false },
            1 => Actor() with { Roles = new HashSet<string> { "TenantAdmin" } },
            2 => Actor() with { SubjectId = null },
            3 => Actor() with { SubjectId = Guid.Empty.ToString("D") },
            4 => Actor() with { TenantId = Guid.Empty },
            5 => Actor(Guid.NewGuid()),
            _ => throw new ArgumentOutOfRangeException(nameof(scenario))
        };
        SetActor(actor);
        var snapshot = Capture();
        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => Service().ReconstructAsync(DocumentOf(snapshot), snapshot.Sha256, CancellationToken.None));
        Assert.Empty(_access);
    }

    [Fact]
    public async Task SnapshotOfAnotherTenantScopeIsRejected()
    {
        var foreign = Capture(Event(1, Admin, Session));
        var document = DocumentOf(foreign) with { TenantId = Guid.NewGuid().ToString("D") };
        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => Service().ReconstructAsync(document, foreign.Sha256, CancellationToken.None));
        Assert.Empty(_access);
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)]
    public async Task RejectsMalformedDocumentsBeforeTimeline(int scenario)
    {
        var snapshot = Capture(Event(1, Admin, Session), Event(2, Admin, Session));
        var document = scenario switch
        {
            0 => DocumentOf(snapshot) with { TotalCount = 3 },                       // declared count mismatch
            1 => DocumentOf(snapshot) with { StartUtc = "not-a-timestamp" },        // malformed bound
            2 => DocumentOf(snapshot) with { Events = [] },                          // events missing
            3 => DocumentOf(snapshot) with { CapturedAtUtc = Start.AddSeconds(-1).UtcDateTime.ToString("O", System.Globalization.CultureInfo.InvariantCulture) }, // end beyond capture
            _ => throw new ArgumentOutOfRangeException(nameof(scenario))
        };
        await Assert.ThrowsAsync<ArgumentException>(
            () => Service().ReconstructAsync(document, snapshot.Sha256, CancellationToken.None));
        Assert.Equal(2, _access.Count);
        Assert.Equal("AuditReplayReconstructionFailed", _access[^1].ActionType);
    }

    [Fact]
    public async Task MissingDigestIsRejectedBeforeAudit()
    {
        var snapshot = Capture();
        await Assert.ThrowsAsync<ArgumentException>(
            () => Service().ReconstructAsync(DocumentOf(snapshot), "", CancellationToken.None));
        Assert.Empty(_access);
    }

    [Fact]
    public async Task FailedRequestedAuditPreventsAnyReconstruction()
    {
        _auditResults.Enqueue(false);
        var snapshot = Capture();
        await Assert.ThrowsAsync<AuditReplayCaptureAuditException>(
            () => Service().ReconstructAsync(DocumentOf(snapshot), snapshot.Sha256, CancellationToken.None));
        Assert.Single(_access);
    }

    [Fact]
    public async Task UppercaseDigestIsNormalizedBeforeVerification()
    {
        var snapshot = Capture(Event(1, Admin, Session));
        var reconstruction = await Service().ReconstructAsync(
            DocumentOf(snapshot), snapshot.Sha256.ToUpperInvariant(), CancellationToken.None);
        Assert.Equal(snapshot.Sha256, reconstruction.Sha256);
    }

    [Fact]
    public async Task ReorderedPostedEventsStillVerifyAgainstTheCanonicalDigest()
    {
        var snapshot = Capture(Event(1, Admin, Session), Event(2, SecondUser, OtherSession, at: Start.UtcDateTime.AddSeconds(5)));
        // A client that re-serialized the document in a different order must still verify:
        // canonicalization sorts before hashing.
        var document = DocumentOf(snapshot) with { Events = [.. snapshot.Events.Reverse()] };
        var reconstruction = await Service().ReconstructAsync(document, snapshot.Sha256, CancellationToken.None);
        Assert.Equal(snapshot.Sha256, reconstruction.Sha256);
        Assert.Equal(snapshot.Events[0].Id, reconstruction.Events[0].Id);
    }
}
