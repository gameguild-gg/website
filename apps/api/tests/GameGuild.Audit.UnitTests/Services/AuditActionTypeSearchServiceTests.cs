using FluentAssertions;
using GameGuild;
using GameGuild.API.Database;
using GameGuild.Compliance.Audit;
using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Xunit;

namespace GameGuild.Tests.Audit.Unit.Services;

public sealed class AuditActionTypeSearchServiceTests : IDisposable
{
    private readonly TestApplicationDbContext _context;
    private readonly AuditActionTypeSearchService _service;

    public AuditActionTypeSearchServiceTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"AuditActionTypeSearch_{Guid.NewGuid()}", new InMemoryDatabaseRoot())
            .Options;
        _context = new TestApplicationDbContext(options);
        _service = new AuditActionTypeSearchService(_context);
    }

    public void Dispose() => _context.Dispose();

    [Fact]
    public async Task SearchAsync_ShouldApplyMultiSelectGroupsBooleanOperatorSortPaginationAndTrends()
    {
        var now = new DateTime(2026, 9, 30, 12, 0, 0, DateTimeKind.Utc);
        _context.Set<AuditLog>().AddRange(
            CreateLog(AuditActionTypes.Login, AuditCategory.Authentication, now),
            CreateLog(AuditActionTypes.SecurityViolation, AuditCategory.Security, now.AddHours(1)),
            CreateLog(AuditActionTypes.UserCreated, AuditCategory.User, now.AddHours(2)));
        await _context.SaveChangesAsync();

        var result = await _service.SearchAsync(new AuditActionTypeSearchRequest
        {
            ActionTypes = [AuditActionTypes.Login, AuditActionTypes.SecurityViolation],
            ActionGroups = ["Security"],
            LogicalOperator = AuditActionTypeLogicalOperator.All,
            StartDate = new DateTimeOffset(now.AddMinutes(-1), TimeSpan.Zero),
            EndDate = new DateTimeOffset(now.AddHours(2), TimeSpan.Zero),
            SortBy = AuditActionTypeSortField.ActionType,
            SortDirection = AuditActionTypeSortDirection.Ascending,
            Skip = 0,
            Take = 1,
            IncludeTrends = true,
            TrendBucketSize = AuditActivityBucketSize.Hourly
        });

        result.TotalCount.Should().Be(2);
        result.Logs.Should().ContainSingle().Which.ActionType.Should().Be(AuditActionTypes.Login);
        result.Frequency.Select(item => item.ActionType).Should().BeEquivalentTo(
            [AuditActionTypes.Login, AuditActionTypes.SecurityViolation]);
        result.Trends.Should().HaveCount(2);
    }

    [Fact]
    public async Task SearchAsync_ShouldSuggestActionsSharingCorrelationIds()
    {
        var now = new DateTime(2026, 9, 30, 12, 0, 0, DateTimeKind.Utc);
        var tenantId = Guid.NewGuid();
        var matching = CreateLog(AuditActionTypes.Login, AuditCategory.Authentication, now);
        matching.CorrelationId = "auth-flow-1";
        matching.TenantId = tenantId;
        var related = CreateLog(AuditActionTypes.MfaVerified, AuditCategory.Authentication, now.AddSeconds(2));
        related.CorrelationId = "auth-flow-1";
        related.TenantId = tenantId;
        var crossTenant = CreateLog(AuditActionTypes.UserCreated, AuditCategory.User, now.AddSeconds(1));
        crossTenant.CorrelationId = "auth-flow-1";
        var unrelated = CreateLog(AuditActionTypes.Logout, AuditCategory.Authentication, now.AddSeconds(3));
        unrelated.CorrelationId = "other-flow";
        _context.Set<AuditLog>().AddRange(matching, related, crossTenant, unrelated);
        await _context.SaveChangesAsync();

        var result = await _service.SearchAsync(new AuditActionTypeSearchRequest
        {
            ActionTypes = [AuditActionTypes.Login],
            TenantId = tenantId,
            IncludeRelatedActions = true
        });

        result.RelatedActions.Should().ContainSingle()
            .Which.ActionType.Should().Be(AuditActionTypes.MfaVerified);
        result.RelatedActions[0].CorrelationCount.Should().Be(1);
    }

    [Fact]
    public async Task SearchAsync_ShouldSupportLogicalNegationAndExplicitTaxonomyCategories()
    {
        var now = new DateTime(2026, 9, 30, 12, 0, 0, DateTimeKind.Utc);
        _context.Set<AuditLog>().AddRange(
            CreateLog(AuditActionTypes.Login, AuditCategory.Authentication, now),
            CreateLog(AuditActionTypes.UserCreated, AuditCategory.User, now.AddMinutes(1)),
            CreateLog(AuditActionTypes.UsernameNormalized, AuditCategory.User, now.AddMinutes(2)));
        await _context.SaveChangesAsync();

        var excluding = await _service.SearchAsync(new AuditActionTypeSearchRequest
        {
            ActionTypes = [AuditActionTypes.Login],
            LogicalOperator = AuditActionTypeLogicalOperator.None,
            IncludeTrends = false
        });
        var byCategory = await _service.SearchAsync(new AuditActionTypeSearchRequest
        {
            Categories = ["Security.Authentication"],
            IncludeTrends = false
        });
        var byUsernameCategory = await _service.SearchAsync(new AuditActionTypeSearchRequest
        {
            Categories = ["Identity.Usernames"],
            IncludeTrends = false
        });

        excluding.Logs.Select(log => log.ActionType).Should().BeEquivalentTo([AuditActionTypes.UserCreated, AuditActionTypes.UsernameNormalized]);
        byCategory.Logs.Should().ContainSingle().Which.ActionType.Should().Be(AuditActionTypes.Login);
        byUsernameCategory.Logs.Should().ContainSingle().Which.ActionType.Should().Be(AuditActionTypes.UsernameNormalized);
    }

    [Fact]
    public async Task ExportAsync_ShouldReturnAllMatchesAndRejectExportsOverTheLimit()
    {
        _context.Set<AuditLog>().AddRange(
            CreateLog(AuditActionTypes.Login, AuditCategory.Authentication, new DateTime(2026, 9, 30, 12, 0, 0, DateTimeKind.Utc)),
            CreateLog(AuditActionTypes.Login, AuditCategory.Authentication, new DateTime(2026, 9, 30, 12, 1, 0, DateTimeKind.Utc)));
        await _context.SaveChangesAsync();

        var request = new AuditActionTypeSearchRequest { ActionTypes = [AuditActionTypes.Login], Skip = 10, Take = 1 };
        var limited = await _service.ExportAsync(request, maximumRecords: 1);
        var complete = await _service.ExportAsync(request, maximumRecords: 2);

        limited.TotalCount.Should().Be(2);
        limited.ExceedsLimit.Should().BeTrue();
        complete.ExceedsLimit.Should().BeFalse();
        complete.Logs.Should().HaveCount(2);
    }

    [Fact]
    public void Taxonomy_ShouldExposeHierarchicalCategoriesAndPredefinedGroups()
    {
        var taxonomy = AuditActionTypeTaxonomy.GetTaxonomy();
        var securityAuthenticationPath = new[] { "Security", "Authentication" };

        taxonomy.ActionTypes.Should().Contain(item => item.ActionType == AuditActionTypes.Login
            && item.CategoryPath.SequenceEqual(securityAuthenticationPath)
            && item.Groups.Contains("Security"));
        taxonomy.ActionTypes.Should().Contain(item => item.ActionType == AuditActionTypes.UserCreated
            && item.Groups.Contains("CRUD"));
        taxonomy.ActionTypes.Should().Contain(item => item.ActionType == AuditActionTypes.AdminAction
            && item.Groups.Contains("Admin"));
    }

    [Fact]
    public async Task SearchAsync_ShouldFilterByThePredefinedCrudSecurityAndAdminGroups()
    {
        var now = new DateTime(2026, 9, 30, 12, 0, 0, DateTimeKind.Utc);
        _context.Set<AuditLog>().AddRange(
            CreateLog(AuditActionTypes.SecurityViolation, AuditCategory.Security, now),
            CreateLog(AuditActionTypes.AdminAction, AuditCategory.Admin, now.AddMinutes(1)),
            CreateLog(AuditActionTypes.UserCreated, AuditCategory.User, now.AddMinutes(2)));
        await _context.SaveChangesAsync();

        var security = await _service.SearchAsync(new AuditActionTypeSearchRequest { ActionGroups = ["Security"], IncludeTrends = false });
        var admin = await _service.SearchAsync(new AuditActionTypeSearchRequest { ActionGroups = ["Admin"], IncludeTrends = false });
        var crud = await _service.SearchAsync(new AuditActionTypeSearchRequest { ActionGroups = ["CRUD"], IncludeTrends = false });

        security.Logs.Should().ContainSingle().Which.ActionType.Should().Be(AuditActionTypes.SecurityViolation);
        admin.Logs.Should().ContainSingle().Which.ActionType.Should().Be(AuditActionTypes.AdminAction);
        crud.Logs.Should().ContainSingle().Which.ActionType.Should().Be(AuditActionTypes.UserCreated);
    }

    [Fact]
    public void RequestValidation_ShouldRejectMissingSelectorsUnknownGroupsAndReversedDates()
    {
        var request = new AuditActionTypeSearchRequest
        {
            StartDate = new DateTimeOffset(2026, 10, 2, 0, 0, 0, TimeSpan.Zero),
            EndDate = new DateTimeOffset(2026, 10, 1, 0, 0, 0, TimeSpan.Zero)
        };
        var errors = new List<ValidationResult>();

        var isValid = Validator.TryValidateObject(request, new ValidationContext(request), errors, validateAllProperties: true);
        var invalidGroupRequest = new AuditActionTypeSearchRequest { ActionGroups = ["unknown"] };
        var invalidGroupErrors = new List<ValidationResult>();
        var invalidGroupIsValid = Validator.TryValidateObject(invalidGroupRequest, new ValidationContext(invalidGroupRequest), invalidGroupErrors, validateAllProperties: true);

        isValid.Should().BeFalse();
        errors.Should().Contain(error => error.ErrorMessage!.Contains("At least one", StringComparison.Ordinal));
        errors.Should().Contain(error => error.ErrorMessage!.Contains("StartDate", StringComparison.Ordinal));
        invalidGroupIsValid.Should().BeFalse();
        invalidGroupErrors.Should().Contain(error => error.ErrorMessage!.Contains("Unknown action group", StringComparison.Ordinal));
    }

    [Fact]
    public void ExportFormatter_ShouldEscapeCsvFieldsAndSerializeJsonDtos()
    {
        var record = new AuditLogDto
        {
            Id = Guid.NewGuid(),
            ActionType = "UserUpdated",
            ResourceType = "User",
            Description = "changed, \"quoted\"\r\nline",
            CreatedAt = new DateTime(2026, 9, 30, 12, 0, 0, DateTimeKind.Utc)
        };

        var csv = System.Text.Encoding.UTF8.GetString(AuditActionTypeExportFormatter.ToCsv([record]));
        using var json = JsonDocument.Parse(AuditActionTypeExportFormatter.ToJson([record]));

        csv.Should().Contain("\"changed, \"\"quoted\"\"\r\nline\"");
        json.RootElement[0].GetProperty("actionType").GetString().Should().Be("UserUpdated");
        json.RootElement[0].GetProperty("description").GetString().Should().Be(record.Description);
    }

    private static AuditLog CreateLog(string actionType, AuditCategory category, DateTime createdAt) => new()
    {
        Id = Guid.NewGuid(),
        ActionType = actionType,
        ResourceType = "User",
        UserId = Guid.NewGuid(),
        TenantId = Guid.NewGuid(),
        Category = category,
        CreatedAt = createdAt,
        Success = true
    };
}
