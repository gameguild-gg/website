using FluentAssertions;
using GameGuild.Compliance.Audit;
using GameGuild.Identity.Authorization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using System.Text.Json;
using Xunit;

namespace GameGuild.Tests.Audit.Unit.QueryDsl;

public sealed class AuditQueryDslExecutorTests
{
    private static readonly Guid TenantA = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");
    private static readonly Guid TenantB = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000002");
    private static readonly Guid Alice = Guid.Parse("bbbbbbbb-0000-0000-0000-000000000001");
    private static readonly Guid Bob = Guid.Parse("bbbbbbbb-0000-0000-0000-000000000002");

    private readonly AuditQueryDslParser _parser = new();

    private static UnifiedSecurityAuditEntry Entry(
        int day,
        string action = "Login",
        Guid? userId = null,
        Guid? tenantId = null,
        AuditRiskLevel severity = AuditRiskLevel.Low,
        SecurityAuditSourceType source = SecurityAuditSourceType.Authentication,
        bool success = true,
        string? email = null) => new()
    {
        Id = Guid.NewGuid(),
        Timestamp = new DateTime(2026, 6, day, 12, 0, 0, DateTimeKind.Utc),
        ActionType = action,
        UserId = userId ?? Alice,
        UserEmail = email,
        TenantId = tenantId,
        RiskLevel = severity,
        SourceType = source,
        SourceEntity = source.ToString(),
        Success = success,
        ResourceType = "User"
    };

    private (Mock<IAuditLogQueryService> QueryService, Mock<IDataMaskingService> Masking, AuditQueryDslOptions Options) CreateHarness()
    {
        var queryService = new Mock<IAuditLogQueryService>();
        var masking = new Mock<IDataMaskingService>();
        masking
            .Setup(service => service.ApplyAsync(
                It.IsAny<string>(), It.IsAny<object>(), It.IsAny<Type>(), It.IsAny<JsonSerializerOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string _, object value, Type _, JsonSerializerOptions _, CancellationToken _) => value);
        var options = Options.Create(new AuditQueryDslOptions());
        return (queryService, masking, options.Value);
    }

    private AuditQueryDslExecutor CreateExecutor(
        Mock<IAuditLogQueryService> queryService,
        Mock<IDataMaskingService> masking,
        AuditQueryDslOptions options,
        IAuditDataAccessRecorder? recorder = null) =>
        new(queryService.Object, masking.Object, recorder, Microsoft.Extensions.Options.Options.Create(options), Mock.Of<ILogger<AuditQueryDslExecutor>>());

    private AuditQueryDslQuery Query(string json)
    {
        var parsed = _parser.ParseJson(json);
        parsed.IsSuccess.Should().BeTrue(parsed.Error.Description);
        return parsed.Value;
    }

    [Fact]
    public async Task Execute_FiltersSortsPaginatesAndAggregates()
    {
        var (queryService, masking, options) = CreateHarness();
        var entries = new List<UnifiedSecurityAuditEntry>
        {
            Entry(1, action: "LoginFailed", severity: AuditRiskLevel.Medium, success: false, email: "alice@example.com"),
            Entry(2, action: "Login", email: "alice@example.com"),
            Entry(3, action: "Grant", source: SecurityAuditSourceType.Permission, tenantId: TenantA, severity: AuditRiskLevel.High),
            Entry(4, action: "Grant", source: SecurityAuditSourceType.Permission, tenantId: TenantB, severity: AuditRiskLevel.Critical),
            Entry(5, action: "ViewAuditLogs", source: SecurityAuditSourceType.General, tenantId: TenantA)
        };
        queryService
            .Setup(service => service.CollectUnifiedEntriesAsync(
                It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<SecurityAuditSourceType?>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(entries);

        var executor = CreateExecutor(queryService, masking, options);
        var query = Query("""{"filter":{"and":[{"field":"actor","op":"eq","value":"alice@example.com"},{"field":"success","op":"eq","value":false}]},"sortDirection":"desc"}""");

        var result = await executor.ExecuteAsync(new AuditQueryDslExecutionRequest
        {
            Query = query,
            Skip = 0,
            Take = 10,
            ActingUserId = Alice
        });

        result.IsSuccess.Should().BeTrue();
        result.Value.TotalCount.Should().Be(1);
        result.Value.Rows.Should().HaveCount(1);
        result.Value.RowsScanned.Should().Be(5);
        result.Value.Aggregates.MatchedCount.Should().Be(1);
        result.Value.Aggregates.FailureCount.Should().Be(1);
        result.Value.Aggregates.ByAction.Should().ContainKey("LoginFailed");
        // No timestamp predicate -> documented default lookback window, reported back to the caller.
        (result.Value.WindowEndUtc - result.Value.WindowStartUtc).TotalDays.Should().BeInRange(89, 91);
    }

    [Fact]
    public async Task Execute_PushesDownSourceRestrictionAndDerivedWindow()
    {
        var (queryService, masking, options) = CreateHarness();
        queryService
            .Setup(service => service.CollectUnifiedEntriesAsync(
                It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<SecurityAuditSourceType?>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var executor = CreateExecutor(queryService, masking, options);
        var query = Query(
            """
            {"filter":{"and":[
                {"field":"source","op":"eq","value":"General"},
                {"field":"timestamp","op":"gte","value":"2026-06-10T00:00:00Z"},
                {"field":"timestamp","op":"lte","value":"2026-06-20T00:00:00Z"}
            ]}}
            """);

        var result = await executor.ExecuteAsync(new AuditQueryDslExecutionRequest { Query = query, Take = 10, ActingUserId = Alice });

        result.IsSuccess.Should().BeTrue();
        queryService.Verify(service => service.CollectUnifiedEntriesAsync(
            new DateTime(2026, 6, 10, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 6, 20, 0, 0, 0, DateTimeKind.Utc),
            SecurityAuditSourceType.General,
            It.IsAny<int>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Execute_OrContextTimestamp_UsesDefaultWindowNotTheOrBound()
    {
        var (queryService, masking, options) = CreateHarness();
        DateTime capturedStart = default;
        queryService
            .Setup(service => service.CollectUnifiedEntriesAsync(
                It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<SecurityAuditSourceType?>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .Callback<DateTime, DateTime, SecurityAuditSourceType?, int, CancellationToken>((start, _, _, _, _) => capturedStart = start)
            .ReturnsAsync(new List<UnifiedSecurityAuditEntry> { Entry(1) });

        var executor = CreateExecutor(queryService, masking, options);
        var query = Query(
            """
            {"filter":{"or":[
                {"field":"timestamp","op":"gte","value":"2026-06-15T00:00:00Z"},
                {"field":"severity","op":"eq","value":"High"}
            ]}}
            """);

        var result = await executor.ExecuteAsync(new AuditQueryDslExecutionRequest { Query = query, Take = 10, ActingUserId = Alice });

        // The OR'd timestamp bound is NOT a necessary condition, so it must never shrink the
        // fetch window: the window stays at the default lookback instead of jumping to the bound.
        result.IsSuccess.Should().BeTrue();
        result.Value.RowsScanned.Should().Be(1);
        capturedStart.Should().Be(result.Value.WindowStartUtc);
        capturedStart.Should().NotBe(new DateTime(2026, 6, 15, 0, 0, 0, DateTimeKind.Utc));
    }

    [Fact]
    public async Task Execute_TenantScope_ForeignTenantConditionFailsClosed()
    {
        var (queryService, masking, options) = CreateHarness();
        var executor = CreateExecutor(queryService, masking, options);
        var query = Query($"{{\"filter\":{{\"field\":\"tenant\",\"op\":\"eq\",\"value\":\"{TenantB}\"}}}}");

        var result = await executor.ExecuteAsync(new AuditQueryDslExecutionRequest
        {
            Query = query,
            Take = 10,
            ActingUserId = Alice,
            TenantScope = TenantA
        });

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("AuditQueryDsl.TenantScope");
        queryService.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Execute_TenantScope_FiltersRowsFromOtherTenants()
    {
        var (queryService, masking, options) = CreateHarness();
        queryService
            .Setup(service => service.CollectUnifiedEntriesAsync(
                It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<SecurityAuditSourceType?>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<UnifiedSecurityAuditEntry>
            {
                Entry(1, tenantId: TenantA),
                Entry(2, tenantId: TenantB),
                Entry(3, tenantId: null)
            });

        var executor = CreateExecutor(queryService, masking, options);
        var query = Query("""{"filter":{"field":"action","op":"eq","value":"Login"}}""");

        var result = await executor.ExecuteAsync(new AuditQueryDslExecutionRequest
        {
            Query = query,
            Take = 10,
            ActingUserId = Alice,
            TenantScope = TenantA
        });

        result.IsSuccess.Should().BeTrue();
        result.Value.TotalCount.Should().Be(1);
        result.Value.RowsScanned.Should().Be(3);
    }

    [Fact]
    public async Task Execute_PageSizeClampedToOptionsMax()
    {
        var (queryService, masking, options) = CreateHarness();
        options.MaxPageSize = 5;
        var entries = Enumerable.Range(1, 20).Select(day => Entry(day)).ToList();
        queryService
            .Setup(service => service.CollectUnifiedEntriesAsync(
                It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<SecurityAuditSourceType?>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(entries);

        var executor = CreateExecutor(queryService, masking, options);
        var query = Query("""{"filter":{"field":"action","op":"eq","value":"Login"}}""");

        var result = await executor.ExecuteAsync(new AuditQueryDslExecutionRequest { Query = query, Take = 10_000, ActingUserId = Alice });

        result.IsSuccess.Should().BeTrue();
        result.Value.Rows.Should().HaveCount(5);
        result.Value.Take.Should().Be(5);
    }

    [Fact]
    public async Task Execute_RowScanCap_MarksTruncation()
    {
        var (queryService, masking, options) = CreateHarness();
        options.MaxRowsScanned = 3;
        var entries = Enumerable.Range(1, 10).Select(day => Entry(day)).ToList();
        queryService
            .Setup(service => service.CollectUnifiedEntriesAsync(
                It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<SecurityAuditSourceType?>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(entries);

        var executor = CreateExecutor(queryService, masking, options);
        var query = Query("""{"filter":{"field":"action","op":"eq","value":"Login"}}""");

        var result = await executor.ExecuteAsync(new AuditQueryDslExecutionRequest { Query = query, Take = 10, ActingUserId = Alice });

        result.IsSuccess.Should().BeTrue();
        result.Value.RowsScanned.Should().Be(3);
        result.Value.ScanTruncated.Should().BeTrue();
    }

    [Fact]
    public async Task Execute_MaskingPassthrough_UsesConfiguredResourceType()
    {
        var (queryService, masking, options) = CreateHarness();
        options.MaskingResourceType = "CustomAuditRows";
        queryService
            .Setup(service => service.CollectUnifiedEntriesAsync(
                It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<SecurityAuditSourceType?>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<UnifiedSecurityAuditEntry> { Entry(1) });

        object? maskedInput = null;
        masking
            .Setup(service => service.ApplyAsync(
                It.IsAny<string>(), It.IsAny<object>(), It.IsAny<Type>(), It.IsAny<JsonSerializerOptions>(), It.IsAny<CancellationToken>()))
            .Callback<string, object, Type, JsonSerializerOptions, CancellationToken>((_, value, _, _, _) => maskedInput = value)
            .ReturnsAsync((string _, object value, Type _, JsonSerializerOptions _, CancellationToken _) => value);

        var executor = CreateExecutor(queryService, masking, options);
        var query = Query("""{"filter":{"field":"action","op":"eq","value":"Login"}}""");

        var result = await executor.ExecuteAsync(new AuditQueryDslExecutionRequest { Query = query, Take = 10, ActingUserId = Alice });

        result.IsSuccess.Should().BeTrue();
        maskedInput.Should().NotBeNull();
        (maskedInput as List<UnifiedSecurityAuditEntry>).Should().NotBeNull().And.HaveCount(1);
        masking.Verify(service => service.ApplyAsync(
            "CustomAuditRows",
            It.IsAny<object>(),
            It.IsAny<Type>(),
            It.IsAny<JsonSerializerOptions>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Execute_MaskingFailure_FailsClosedWithWithheldResults()
    {
        var queryService = new Mock<IAuditLogQueryService>();
        queryService
            .Setup(service => service.CollectUnifiedEntriesAsync(
                It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<SecurityAuditSourceType?>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<UnifiedSecurityAuditEntry> { Entry(1) });
        var masking = new Mock<IDataMaskingService>();
        masking
            .Setup(service => service.ApplyAsync(
                It.IsAny<string>(), It.IsAny<object>(), It.IsAny<Type>(), It.IsAny<JsonSerializerOptions>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("rule store down"));

        var executor = CreateExecutor(queryService, masking, new AuditQueryDslOptions());
        var query = Query("""{"filter":{"field":"action","op":"eq","value":"Login"}}""");

        var result = await executor.ExecuteAsync(new AuditQueryDslExecutionRequest { Query = query, Take = 10, ActingUserId = Alice });

        result.IsFailure.Should().BeTrue();
        result.Error.Description.Should().Contain("withheld");
    }

    [Fact]
    public async Task Execute_MaskingTimeout_ReturnsTimeoutError()
    {
        var queryService = new Mock<IAuditLogQueryService>();
        queryService
            .Setup(service => service.CollectUnifiedEntriesAsync(
                It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<SecurityAuditSourceType?>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<UnifiedSecurityAuditEntry> { Entry(1) });
        var masking = new Mock<IDataMaskingService>();
        masking
            .Setup(service => service.ApplyAsync(
                It.IsAny<string>(), It.IsAny<object>(), It.IsAny<Type>(), It.IsAny<JsonSerializerOptions>(), It.IsAny<CancellationToken>()))
            .Returns(async (string _, object _, Type _, JsonSerializerOptions _, CancellationToken token) =>
            {
                await Task.Delay(TimeSpan.FromSeconds(3), token);
                return null;
            });

        var executor = CreateExecutor(queryService, masking, new AuditQueryDslOptions());
        var query = Query("""{"filter":{"field":"action","op":"eq","value":"Login"}}""");

        var result = await executor.ExecuteAsync(new AuditQueryDslExecutionRequest
        {
            Query = query,
            Take = 10,
            ActingUserId = Alice,
            TimeoutSeconds = 1
        });

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("AuditQueryDsl.Timeout");
    }

    [Fact]
    public async Task Execute_AscendingSort_Honored()
    {
        var (queryService, masking, options) = CreateHarness();
        var entries = new List<UnifiedSecurityAuditEntry> { Entry(3), Entry(1), Entry(2) };
        queryService
            .Setup(service => service.CollectUnifiedEntriesAsync(
                It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<SecurityAuditSourceType?>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(entries);

        var executor = CreateExecutor(queryService, masking, options);
        var query = Query("""{"filter":{"field":"action","op":"eq","value":"Login"},"sortDirection":"asc"}""");

        var result = await executor.ExecuteAsync(new AuditQueryDslExecutionRequest { Query = query, Skip = 0, Take = 2, ActingUserId = Alice });

        result.IsSuccess.Should().BeTrue();
        result.Value.Rows.Should().HaveCount(2);
        // Ascending order: day 1 and day 2 on the first page.
        result.Value.Rows.OfType<UnifiedSecurityAuditEntry>()
            .Select(entry => entry.Timestamp.Day).Should().ContainInOrder(1, 2);
    }

    [Fact]
    public async Task Execute_InvalidPaging_FailsClosed()
    {
        var (queryService, masking, options) = CreateHarness();
        var executor = CreateExecutor(queryService, masking, options);
        var query = Query("""{"filter":{"field":"action","op":"eq","value":"Login"}}""");

        (await executor.ExecuteAsync(new AuditQueryDslExecutionRequest { Query = query, Skip = -1, Take = 10, ActingUserId = Alice }))
            .IsFailure.Should().BeTrue();
        (await executor.ExecuteAsync(new AuditQueryDslExecutionRequest { Query = query, Skip = 0, Take = 0, ActingUserId = Alice }))
            .IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task Execute_RecordsAccessForGeneralRows()
    {
        var (queryService, masking, options) = CreateHarness();
        queryService
            .Setup(service => service.CollectUnifiedEntriesAsync(
                It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<SecurityAuditSourceType?>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<UnifiedSecurityAuditEntry>
            {
                Entry(1, source: SecurityAuditSourceType.General, tenantId: TenantA),
                Entry(2, source: SecurityAuditSourceType.Authentication)
            });
        var recorder = new Mock<IAuditDataAccessRecorder>();

        var executor = CreateExecutor(queryService, masking, options, recorder.Object);
        var query = Query("""{"filter":{"field":"source","op":"eq","value":"General"}}""");

        await executor.ExecuteAsync(new AuditQueryDslExecutionRequest { Query = query, Take = 10, ActingUserId = Alice });

        recorder.Verify(rec => rec.RecordAsync(
            It.Is<IEnumerable<AuditAccessedRecord>>(records => records.Count() == 1),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
