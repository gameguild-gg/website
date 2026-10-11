using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using GameGuild.Compliance.Audit;
using Xunit;

namespace GameGuild.Tests.Audit.Unit.Services;

public sealed class AuditReplaySnapshotTests
{
    private static readonly Guid Tenant = Guid.Parse("10000000-0000-0000-0000-000000000090");
    private static readonly Guid User = Guid.Parse("10000000-0000-0000-0000-000000000091");
    private static readonly Guid Session = Guid.Parse("10000000-0000-0000-0000-000000000092");
    private static readonly DateTimeOffset Start = new(2026, 10, 11, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset End = Start.AddSeconds(1);
    private static readonly DateTimeOffset Captured = Start.AddSeconds(2);
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private static Guid Id(int number) => Guid.Parse($"10000000-0000-0000-0000-{number:D12}");
    private static AuditLogDto Event(int number, DateTime? at = null) => new()
    {
        Id = Id(number), TenantId = Tenant, CreatedAt = at ?? Start.UtcDateTime,
        ActionType = "Read", ResourceType = "Document", ResourceId = "doc-1",
        UserId = User, SessionId = Session, CorrelationId = "request-1", Success = true
    };
    private static AuditReplaySnapshot Capture(AuditLogDto[] events, int? total = null) =>
        AuditReplaySnapshot.Capture(Tenant, events, total ?? events.Length, Start, End, Captured);

    [Fact]
    public void PreservesDotNetTickResolutionBeforeStableGuidTies()
    {
        var snapshot = Capture([Event(3, Start.UtcDateTime.AddTicks(2)),
            Event(2, Start.UtcDateTime.AddTicks(1)), Event(1, Start.UtcDateTime.AddTicks(1))]);
        Assert.Equal([Id(1).ToString("D"), Id(2).ToString("D"), Id(3).ToString("D")], snapshot.Events.Select(item => item.Id));
        Assert.Equal("2026-10-11T00:00:00.0000001Z", snapshot.Events[0].CreatedAt);
        Assert.Equal("2026-10-11T00:00:00.0000002Z", snapshot.Events[2].CreatedAt);
    }

    [Fact]
    public void IncludesBothRangeBoundariesAndNormalizesScopeOffsets()
    {
        var offset = TimeSpan.FromHours(-3);
        var snapshot = AuditReplaySnapshot.Capture(Tenant, [Event(2, End.UtcDateTime), Event(1)],
            2, Start.ToOffset(offset), End.ToOffset(offset), Captured.ToOffset(offset));
        Assert.Equal(Start, snapshot.StartUtc);
        Assert.Equal(TimeSpan.Zero, snapshot.StartUtc.Offset);
        Assert.Equal(End, snapshot.EndUtc);
        Assert.Equal(Captured, snapshot.CapturedAtUtc);
        Assert.Equal(2, snapshot.TotalCount);
    }

    [Fact]
    public void ProjectsOnlySafeFieldsAndKeepsExplicitGlobalNull()
    {
        var input = Event(1);
        input.TenantId = null; input.UserId = null; input.SessionId = null;
        input.ResourceId = null; input.CorrelationId = null;
        input.Description = "PRIVATE_DESCRIPTION_MARKER";
        input.ErrorMessage = "PRIVATE_ERROR_MARKER";
        input.IpAddress = "PRIVATE_ADDRESS_MARKER";
        input.UserAgent = "PRIVATE_AGENT_MARKER";
        var snapshot = AuditReplaySnapshot.Capture(null, [input], 1, Start, End, Captured);
        Assert.Null(snapshot.TenantId);
        Assert.Null(snapshot.Events[0].TenantId);
        Assert.DoesNotContain("PRIVATE_", snapshot.Json);
        using var document = JsonDocument.Parse(snapshot.Json);
        var node = document.RootElement.GetProperty("events")[0];
        Assert.Equal(10, node.EnumerateObject().Count());
        Assert.Equal(JsonValueKind.Null, node.GetProperty("tenantId").ValueKind);
        foreach (var excluded in new[] { "description", "ipAddress", "userAgent", "errorMessage", "metadata" })
        {
            Assert.False(node.TryGetProperty(excluded, out _));
        }
    }

    [Fact]
    public void CopiesSourceObjectsAndFreezesReturnedCollections()
    {
        var input = Event(1);
        var list = new List<AuditLogDto> { input };
        var snapshot = AuditReplaySnapshot.Capture(Tenant, list, 1, Start, End, Captured);
        var json = snapshot.Json; var digest = snapshot.Sha256;
        input.ActionType = "Changed"; input.CreatedAt = End.UtcDateTime;
        list.Clear();
        Assert.Equal("Read", snapshot.Events[0].ActionType);
        Assert.Equal(json, snapshot.Json); Assert.Equal(digest, snapshot.Sha256);
        var mutable = Assert.IsAssignableFrom<ICollection<AuditReplaySnapshotEvent>>(snapshot.Events);
        Assert.True(mutable.IsReadOnly);
        Assert.Throws<NotSupportedException>(() => mutable.Clear());
    }

    [Theory]
    [InlineData(0)] [InlineData(2)] [InlineData(-1)] [InlineData(10001)]
    public void RejectsInconsistentOrOutOfBoundCounts(int count) =>
        Assert.Throws<ArgumentException>(() => Capture([Event(1)], count));

    [Fact]
    public void RejectsNullSource() =>
        Assert.Throws<ArgumentNullException>(() => AuditReplaySnapshot.Capture(Tenant, null!, 0, Start, End, Captured));

    [Fact]
    public void RejectsMissingEvent() =>
        Assert.Throws<ArgumentException>(() => Capture([null!]));

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)]
    public void RejectsEmptyGuids(int field)
    {
        var input = Event(1);
        switch (field)
        {
            case 0: input.Id = Guid.Empty; break;
            case 1: input.UserId = Guid.Empty; break;
            case 2: input.SessionId = Guid.Empty; break;
            case 3: input.TenantId = Guid.Empty; break;
            default: throw new ArgumentOutOfRangeException(nameof(field));
        }
        Assert.Throws<ArgumentException>(() => Capture([input]));
    }

    [Fact]
    public void RejectsEmptyScopeTenant() =>
        Assert.Throws<ArgumentException>(() => AuditReplaySnapshot.Capture(Guid.Empty, [], 0, Start, End, Captured));

    [Fact]
    public void RejectsDuplicateIds() =>
        Assert.Throws<ArgumentException>(() => Capture([Event(1), Event(1, End.UtcDateTime)]));

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)]
    public void RejectsForeignAndImplicitAllTenantSelections(int scenario)
    {
        var input = Event(1);
        var tenant = scenario == 0 ? (Guid?)null : Tenant;
        if (scenario == 1) { input.TenantId = null; }
        if (scenario == 2) { input.TenantId = Id(99); }
        Assert.Throws<ArgumentException>(() => AuditReplaySnapshot.Capture(tenant, [input], 1, Start, End, Captured));
    }

    [Theory]
    [InlineData(DateTimeKind.Unspecified)] [InlineData(DateTimeKind.Local)]
    public void RejectsTimestampKindAmbiguity(DateTimeKind kind) =>
        Assert.Throws<ArgumentException>(() => Capture([Event(1, DateTime.SpecifyKind(Start.UtcDateTime, kind))]));

    [Theory]
    [InlineData(-1)] [InlineData(10000001)]
    public void RejectsEventsOutsideRange(int ticks) =>
        Assert.Throws<ArgumentException>(() => Capture([Event(1, Start.UtcDateTime.AddTicks(ticks))]));

    [Theory]
    [InlineData(0)] [InlineData(1)]
    public void RejectsInvalidCaptureRanges(int scenario) =>
        Assert.Throws<ArgumentException>(() => AuditReplaySnapshot.Capture(Tenant, [], 0,
            scenario == 0 ? End : Start, scenario == 0 ? Start : Captured.AddTicks(1), Captured));

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)]
    [InlineData(4)] [InlineData(5)] [InlineData(6)] [InlineData(7)]
    public void RejectsEmptyOrOversizedText(int field)
    {
        var input = Event(1);
        switch (field)
        {
            case 0: input.ActionType = " "; break;
            case 1: input.ActionType = new string('a', 101); break;
            case 2: input.ResourceType = ""; break;
            case 3: input.ResourceType = new string('b', 101); break;
            case 4: input.ResourceId = ""; break;
            case 5: input.ResourceId = new string('c', 501); break;
            case 6: input.CorrelationId = ""; break;
            case 7: input.CorrelationId = new string('d', 101); break;
            default: throw new ArgumentOutOfRangeException(nameof(field));
        }
        Assert.Throws<ArgumentException>(() => Capture([input]));
    }

    [Fact]
    public void AcceptsExactly10000EventsAndStopsOverflowEnumerationWithoutPartialResults()
    {
        var snapshot = Capture(Enumerable.Range(1, 10000).Select(number => Event(number)).ToArray());
        Assert.Equal(10000, snapshot.TotalCount);
        var visited = 0;
        IEnumerable<AuditLogDto> Source()
        {
            for (var number = 1; number <= 20000; number++) { visited++; yield return Event(number); }
        }
        Assert.Throws<ArgumentException>(() => AuditReplaySnapshot.Capture(Tenant, Source(), 10000, Start, End, Captured));
        Assert.Equal(10001, visited);
    }

    [Fact]
    public void EnforcesUtf8SerializationLimitWithoutReturningPartialContent()
    {
        var events = Enumerable.Range(1, 5000).Select(number =>
        {
            var input = Event(number);
            input.ActionType = new string('\u4e00', 100);
            input.ResourceType = new string('\u4e00', 100);
            input.ResourceId = new string('\u4e00', 500);
            input.CorrelationId = new string('\u4e00', 100);
            return input;
        }).ToArray();
        var exception = Assert.Throws<ArgumentException>(() => Capture(events));
        Assert.Contains("16 MiB", exception.Message);
    }

    [Fact]
    public void DigestIsDeterministicAndDetectsContentChangesAgainstExpectedValue()
    {
        var first = Capture([Event(2, End.UtcDateTime), Event(1)]);
        var second = Capture([Event(1), Event(2, End.UtcDateTime)]);
        Assert.Equal(first.Json, second.Json);
        Assert.Equal(first.Sha256, second.Sha256);
        Assert.Equal(Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(first.Json))).ToLowerInvariant(), first.Sha256);
        Assert.True(AuditReplaySnapshot.MatchesDigest(first.Json, first.Sha256));
        Assert.False(AuditReplaySnapshot.MatchesDigest(first.Json + " ", first.Sha256));
        Assert.False(AuditReplaySnapshot.MatchesDigest(first.Json, new string('F', 64)));
        Assert.False(AuditReplaySnapshot.MatchesDigest(first.Json, new string('z', 64)));
        Assert.False(AuditReplaySnapshot.MatchesDigest(first.Json, ""));
        Assert.False(AuditReplaySnapshot.MatchesDigest(first.Json, new string('0', 63)));
        Assert.False(AuditReplaySnapshot.MatchesDigest(new string('a', AuditReplaySnapshot.MaximumBytes + 1), first.Sha256));
    }

    [Fact]
    public void EnumerationFailurePropagatesWithoutReturningPartialEvidence()
    {
        IEnumerable<AuditLogDto> Source()
        {
            yield return Event(1);
            throw new InvalidOperationException("source failed");
        }
        Assert.Throws<InvalidOperationException>(() =>
            AuditReplaySnapshot.Capture(Tenant, Source(), 1, Start, End, Captured));
    }

    [Fact]
    public void AcceptsValidCalendarExtremesWithUtcPrecision()
    {
        var start = DateTimeOffset.MinValue;
        var end = DateTimeOffset.MaxValue;
        var input = Event(1, DateTime.SpecifyKind(DateTime.MinValue, DateTimeKind.Utc));
        var last = Event(2, DateTime.SpecifyKind(DateTime.MaxValue, DateTimeKind.Utc));
        var snapshot = AuditReplaySnapshot.Capture(Tenant, [last, input], 2, start, end, end);
        Assert.Equal("0001-01-01T00:00:00.0000000Z", snapshot.Events[0].CreatedAt);
        Assert.Equal("9999-12-31T23:59:59.9999999Z", snapshot.Events[1].CreatedAt);
    }

    [Fact]
    public void PublicConstructorEnforcesTheSameCaptureInvariants()
    {
        var valid = new AuditReplaySnapshot(Tenant, [Event(1)], 1, Start, End, Captured);
        Assert.Equal(Capture([Event(1)]).Json, valid.Json);
        Assert.Throws<ArgumentException>(() => new AuditReplaySnapshot(null, [Event(1)], 1, Start, End, Captured));
        Assert.Throws<ArgumentException>(() => new AuditReplaySnapshot(Tenant, [Event(1)], 0, Start, End, Captured));
    }

    public static IEnumerable<object[]> ContractVectors()
    {
        using var document = JsonDocument.Parse(ProjectionVectors);
        foreach (var vector in document.RootElement.EnumerateArray())
        {
            yield return [vector.GetProperty("label").GetString()!, vector.GetRawText()];
        }
    }

    [Theory]
    [MemberData(nameof(ContractVectors))]
    public void MatchesProjectionFromActualWebCore(string label, string vectorJson)
    {
        Assert.False(string.IsNullOrWhiteSpace(label));
        using var document = JsonDocument.Parse(vectorJson);
        var vector = document.RootElement;
        var tenant = vector.GetProperty("tenantId").ValueKind == JsonValueKind.Null
            ? (Guid?)null : vector.GetProperty("tenantId").GetGuid();
        var inputs = JsonSerializer.Deserialize<AuditLogDto[]>(vector.GetProperty("inputs").GetRawText(), JsonOptions)!;
        var expected = JsonSerializer.Deserialize<AuditReplaySnapshotEvent[]>(vector.GetProperty("expected").GetRawText(), JsonOptions)!;
        var snapshot = AuditReplaySnapshot.Capture(tenant, inputs, inputs.Length,
            vector.GetProperty("start").GetDateTimeOffset(), vector.GetProperty("end").GetDateTimeOffset(),
            vector.GetProperty("capturedAt").GetDateTimeOffset());
        Assert.Equal(expected, snapshot.Events);
    }

    private const string ProjectionVectors = """
[
  {
    "label": "precise-order-and-guid-ties",
    "tenantId": "10000000-0000-0000-0000-000000000090",
    "start": "2026-10-11T00:00:00.0000000Z",
    "end": "2026-10-11T00:00:01.0000000Z",
    "capturedAt": "2026-10-11T00:00:02.0000000Z",
    "inputs": [
      {
        "id": "10000000-0000-0000-0000-000000000003",
        "tenantId": "10000000-0000-0000-0000-000000000090",
        "createdAt": "2026-10-11T00:00:00.0000002Z",
        "actionType": "Read",
        "resourceType": "Document",
        "resourceId": "doc-1",
        "userId": "10000000-0000-0000-0000-000000000091",
        "sessionId": "10000000-0000-0000-0000-000000000092",
        "correlationId": "request-1",
        "success": true
      },
      {
        "id": "10000000-0000-0000-0000-000000000002",
        "tenantId": "10000000-0000-0000-0000-000000000090",
        "createdAt": "2026-10-11T00:00:00.0000001Z",
        "actionType": "Read",
        "resourceType": "Document",
        "resourceId": "doc-1",
        "userId": "10000000-0000-0000-0000-000000000091",
        "sessionId": "10000000-0000-0000-0000-000000000092",
        "correlationId": "request-1",
        "success": true
      },
      {
        "id": "10000000-0000-0000-0000-000000000001",
        "tenantId": "10000000-0000-0000-0000-000000000090",
        "createdAt": "2026-10-11T00:00:00.0000001Z",
        "actionType": "Read",
        "resourceType": "Document",
        "resourceId": "doc-1",
        "userId": "10000000-0000-0000-0000-000000000091",
        "sessionId": "10000000-0000-0000-0000-000000000092",
        "correlationId": "request-1",
        "success": true
      }
    ],
    "expected": [
      {
        "id": "10000000-0000-0000-0000-000000000001",
        "tenantId": "10000000-0000-0000-0000-000000000090",
        "createdAt": "2026-10-11T00:00:00.0000001Z",
        "actionType": "Read",
        "resourceType": "Document",
        "resourceId": "doc-1",
        "userId": "10000000-0000-0000-0000-000000000091",
        "sessionId": "10000000-0000-0000-0000-000000000092",
        "correlationId": "request-1",
        "success": true
      },
      {
        "id": "10000000-0000-0000-0000-000000000002",
        "tenantId": "10000000-0000-0000-0000-000000000090",
        "createdAt": "2026-10-11T00:00:00.0000001Z",
        "actionType": "Read",
        "resourceType": "Document",
        "resourceId": "doc-1",
        "userId": "10000000-0000-0000-0000-000000000091",
        "sessionId": "10000000-0000-0000-0000-000000000092",
        "correlationId": "request-1",
        "success": true
      },
      {
        "id": "10000000-0000-0000-0000-000000000003",
        "tenantId": "10000000-0000-0000-0000-000000000090",
        "createdAt": "2026-10-11T00:00:00.0000002Z",
        "actionType": "Read",
        "resourceType": "Document",
        "resourceId": "doc-1",
        "userId": "10000000-0000-0000-0000-000000000091",
        "sessionId": "10000000-0000-0000-0000-000000000092",
        "correlationId": "request-1",
        "success": true
      }
    ]
  },
  {
    "label": "explicit-global-null-identities",
    "tenantId": null,
    "start": "2026-10-11T00:00:00.0000000Z",
    "end": "2026-10-11T00:00:01.0000000Z",
    "capturedAt": "2026-10-11T00:00:02.0000000Z",
    "inputs": [
      {
        "id": "10000000-0000-0000-0000-000000000001",
        "tenantId": null,
        "createdAt": "2026-10-11T00:00:00.0000000Z",
        "actionType": "Read",
        "resourceType": "Document",
        "resourceId": null,
        "userId": null,
        "sessionId": null,
        "correlationId": null,
        "success": true
      }
    ],
    "expected": [
      {
        "id": "10000000-0000-0000-0000-000000000001",
        "tenantId": null,
        "createdAt": "2026-10-11T00:00:00.0000000Z",
        "actionType": "Read",
        "resourceType": "Document",
        "resourceId": null,
        "userId": null,
        "sessionId": null,
        "correlationId": null,
        "success": true
      }
    ]
  },
  {
    "label": "inclusive-range-boundaries",
    "tenantId": "10000000-0000-0000-0000-000000000090",
    "start": "2026-10-11T00:00:00.0000000Z",
    "end": "2026-10-11T00:00:01.0000000Z",
    "capturedAt": "2026-10-11T00:00:02.0000000Z",
    "inputs": [
      {
        "id": "10000000-0000-0000-0000-000000000002",
        "tenantId": "10000000-0000-0000-0000-000000000090",
        "createdAt": "2026-10-11T00:00:01.0000000Z",
        "actionType": "Read",
        "resourceType": "Document",
        "resourceId": "doc-1",
        "userId": "10000000-0000-0000-0000-000000000091",
        "sessionId": "10000000-0000-0000-0000-000000000092",
        "correlationId": "request-1",
        "success": true
      },
      {
        "id": "10000000-0000-0000-0000-000000000001",
        "tenantId": "10000000-0000-0000-0000-000000000090",
        "createdAt": "2026-10-11T00:00:00.0000000Z",
        "actionType": "Read",
        "resourceType": "Document",
        "resourceId": "doc-1",
        "userId": "10000000-0000-0000-0000-000000000091",
        "sessionId": "10000000-0000-0000-0000-000000000092",
        "correlationId": "request-1",
        "success": true
      }
    ],
    "expected": [
      {
        "id": "10000000-0000-0000-0000-000000000001",
        "tenantId": "10000000-0000-0000-0000-000000000090",
        "createdAt": "2026-10-11T00:00:00.0000000Z",
        "actionType": "Read",
        "resourceType": "Document",
        "resourceId": "doc-1",
        "userId": "10000000-0000-0000-0000-000000000091",
        "sessionId": "10000000-0000-0000-0000-000000000092",
        "correlationId": "request-1",
        "success": true
      },
      {
        "id": "10000000-0000-0000-0000-000000000002",
        "tenantId": "10000000-0000-0000-0000-000000000090",
        "createdAt": "2026-10-11T00:00:01.0000000Z",
        "actionType": "Read",
        "resourceType": "Document",
        "resourceId": "doc-1",
        "userId": "10000000-0000-0000-0000-000000000091",
        "sessionId": "10000000-0000-0000-0000-000000000092",
        "correlationId": "request-1",
        "success": true
      }
    ]
  },
  {
    "label": "distinct-resource-types-and-unattributed-events",
    "tenantId": "10000000-0000-0000-0000-000000000090",
    "start": "2026-10-11T00:00:00.0000000Z",
    "end": "2026-10-11T00:00:01.0000000Z",
    "capturedAt": "2026-10-11T00:00:02.0000000Z",
    "inputs": [
      {
        "id": "10000000-0000-0000-0000-000000000002",
        "tenantId": "10000000-0000-0000-0000-000000000090",
        "createdAt": "2026-10-11T00:00:00.0000000Z",
        "actionType": "Read",
        "resourceType": "Account",
        "resourceId": "doc-1",
        "userId": null,
        "sessionId": null,
        "correlationId": "request-1",
        "success": true
      },
      {
        "id": "10000000-0000-0000-0000-000000000001",
        "tenantId": "10000000-0000-0000-0000-000000000090",
        "createdAt": "2026-10-11T00:00:00.0000000Z",
        "actionType": "Read",
        "resourceType": "Document",
        "resourceId": "doc-1",
        "userId": null,
        "sessionId": null,
        "correlationId": "request-1",
        "success": false
      }
    ],
    "expected": [
      {
        "id": "10000000-0000-0000-0000-000000000001",
        "tenantId": "10000000-0000-0000-0000-000000000090",
        "createdAt": "2026-10-11T00:00:00.0000000Z",
        "actionType": "Read",
        "resourceType": "Document",
        "resourceId": "doc-1",
        "userId": null,
        "sessionId": null,
        "correlationId": "request-1",
        "success": false
      },
      {
        "id": "10000000-0000-0000-0000-000000000002",
        "tenantId": "10000000-0000-0000-0000-000000000090",
        "createdAt": "2026-10-11T00:00:00.0000000Z",
        "actionType": "Read",
        "resourceType": "Account",
        "resourceId": "doc-1",
        "userId": null,
        "sessionId": null,
        "correlationId": "request-1",
        "success": true
      }
    ]
  },
  {
    "label": "private-content-not-projected",
    "tenantId": "10000000-0000-0000-0000-000000000090",
    "start": "2026-10-11T00:00:00.0000000Z",
    "end": "2026-10-11T00:00:01.0000000Z",
    "capturedAt": "2026-10-11T00:00:02.0000000Z",
    "inputs": [
      {
        "id": "10000000-0000-0000-0000-000000000001",
        "tenantId": "10000000-0000-0000-0000-000000000090",
        "createdAt": "2026-10-11T00:00:00.0000000Z",
        "actionType": "Read",
        "resourceType": "Document",
        "resourceId": "doc-1",
        "userId": "10000000-0000-0000-0000-000000000091",
        "sessionId": "10000000-0000-0000-0000-000000000092",
        "correlationId": "request-1",
        "success": true,
        "description": "PRIVATE_DESCRIPTION_MARKER",
        "ipAddress": "PRIVATE_ADDRESS_MARKER",
        "userAgent": "PRIVATE_AGENT_MARKER",
        "errorMessage": "PRIVATE_ERROR_MARKER",
        "metadata": "PRIVATE_METADATA_MARKER"
      }
    ],
    "expected": [
      {
        "id": "10000000-0000-0000-0000-000000000001",
        "tenantId": "10000000-0000-0000-0000-000000000090",
        "createdAt": "2026-10-11T00:00:00.0000000Z",
        "actionType": "Read",
        "resourceType": "Document",
        "resourceId": "doc-1",
        "userId": "10000000-0000-0000-0000-000000000091",
        "sessionId": "10000000-0000-0000-0000-000000000092",
        "correlationId": "request-1",
        "success": true
      }
    ]
  },
  {
    "label": "complete-empty-selection",
    "tenantId": "10000000-0000-0000-0000-000000000090",
    "start": "2026-10-11T00:00:00.0000000Z",
    "end": "2026-10-11T00:00:01.0000000Z",
    "capturedAt": "2026-10-11T00:00:02.0000000Z",
    "inputs": [],
    "expected": []
  }
]
""";
}
