using FluentAssertions;
using GameGuild.Compliance.Audit;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace GameGuild.Tests.Audit.Unit.Services;

public sealed class SavedAuditQueryServiceTests
{
    private static readonly Guid Tenant = Guid.Parse("cccccccc-0000-0000-0000-000000000001");
    private static readonly Guid OtherTenant = Guid.Parse("cccccccc-0000-0000-0000-000000000002");
    private static readonly Guid Alice = Guid.Parse("dddddddd-0000-0000-0000-000000000001");
    private static readonly Guid Bob = Guid.Parse("dddddddd-0000-0000-0000-000000000002");

    private readonly Mock<ISavedAuditQueryRepository> _repository = new();
    private readonly AuditQueryDslParser _parser = new();
    private readonly Mock<IAuditQueryDslExecutor> _executor = new();
    private readonly Mock<IAuditService> _auditService = new();

    private SavedAuditQueryService CreateService() =>
        new(_repository.Object, _parser, _executor.Object, _auditService.Object);

    private static SaveSavedAuditQueryInput ValidInput(string name = "Failed logins", bool shared = false, string? queryJson = null) => new()
    {
        Name = name,
        Description = "shared template X",
        IsShared = shared,
        QueryJson = queryJson ?? """{"filter":{"and":[{"field":"action","op":"eq","value":"LoginFailed"}]}}""",
        QueryText = null
    };

    private Task<Result<SavedAuditQueryViewResponse>> Act_Create(SavedAuditQueryService service, SaveSavedAuditQueryInput input, Guid user, Guid? tenant) =>
        service.CreateAsync(input, user, tenant);

    [Fact]
    public async Task Create_ValidatesStoresCanonicalJsonAndWritesCreationRevision()
    {
        var service = CreateService();
        _repository
            .Setup(repo => repo.CountByNameAsync(Tenant, "Failed logins", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        var result = await Act_Create(service, ValidInput(), Alice, Tenant);

        result.IsSuccess.Should().BeTrue();
        result.Value.Name.Should().Be("Failed logins");
        result.Value.IsOwner.Should().BeTrue();
        result.Value.QueryJson.Should().Contain("\"field\":\"action\"").And.Contain("\"value\":\"LoginFailed\"");
        _repository.Verify(repo => repo.AddAsync(
            It.Is<SavedAuditQuery>(query => query.TenantId == Tenant && query.CreatedByUserId == Alice && query.IsPublic == false && query.CurrentRevision == 1),
            It.Is<SavedAuditQueryRevision>(revision => revision.IsCreation && revision.Revision == 1 && revision.ChangedByUserId == Alice),
            It.IsAny<CancellationToken>()), Times.Once);
        _auditService.Verify(audit => audit.LogAdminActionAsync(Alice, "SavedAuditQueryCreated", It.IsAny<string>(), It.IsAny<object?>()), Times.Once);
    }

    [Fact]
    public async Task Create_AcceptsTextSyntax_AndStoresCanonicalJson()
    {
        var service = CreateService();
        _repository
            .Setup(repo => repo.CountByNameAsync(Tenant, "Text query", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);
        var input = new SaveSavedAuditQueryInput { Name = "Text query", IsShared = true, QueryText = "actor:alice AND severity:Critical" };

        var result = await Act_Create(service, input, Alice, Tenant);

        result.IsSuccess.Should().BeTrue();
        result.Value.QueryJson.Should().Contain("\"field\":\"actor\"");
    }

    [Theory]
    [InlineData(true, "bad field")]
    [InlineData(false, "bad field")]
    public async Task Create_InvalidDsl_FailsClosed(bool jsonForm, string _)
    {
        var service = CreateService();
        var input = new SaveSavedAuditQueryInput
        {
            Name = "Broken",
            IsShared = false,
            QueryJson = jsonForm ? """{"filter":{"field":"nope","op":"eq","value":"x"}}""" : null,
            QueryText = jsonForm ? null : "nope:value"
        };

        var result = await Act_Create(service, input, Alice, Tenant);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("AuditQueryDsl.Invalid");
        _repository.Verify(repo => repo.AddAsync(It.IsAny<SavedAuditQuery>(), It.IsAny<SavedAuditQueryRevision>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Create_MissingQuery_FailsClosed()
    {
        var service = CreateService();
        var input = new SaveSavedAuditQueryInput { Name = "Empty", IsShared = false };

        var result = await Act_Create(service, input, Alice, Tenant);

        result.IsFailure.Should().BeTrue();
        result.Error.Description.Should().Contain("queryJson").And.Contain("queryText");
    }

    [Fact]
    public async Task Create_DuplicateNameInTenant_Conflicts()
    {
        var service = CreateService();
        _repository
            .Setup(repo => repo.CountByNameAsync(Tenant, "Failed logins", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var result = await Act_Create(service, ValidInput(), Alice, Tenant);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("SavedAuditQuery.Invalid");
    }

    [Fact]
    public async Task Create_ForeignTenantConditionInQuery_FailsClosed()
    {
        var service = CreateService();
        var input = ValidInput(queryJson: $@"{{""filter"":{{""field"":""tenant"",""op"":""eq"",""value"":""{OtherTenant}""}}}}");

        var result = await Act_Create(service, input, Alice, Tenant);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("AuditQueryDsl.TenantScope");
    }

    [Fact]
    public async Task Get_PrivateView_NonOwnerForbidden()
    {
        var service = CreateService();
        var view = SavedAuditQuery.Create(Tenant, "Private", "{}", Alice);
        _repository.Setup(repo => repo.GetByIdAsync(view.Id, It.IsAny<CancellationToken>())).ReturnsAsync(view);

        (await service.GetAsync(view.Id, Bob)).IsSuccess.Should().BeFalse();
        (await service.GetAsync(view.Id, Alice)).IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Get_SharedView_VisibleToOthers()
    {
        var service = CreateService();
        var view = SavedAuditQuery.Create(Tenant, "Shared", "{}", Alice, isPublic: true);
        _repository.Setup(repo => repo.GetByIdAsync(view.Id, It.IsAny<CancellationToken>())).ReturnsAsync(view);

        var result = await service.GetAsync(view.Id, Bob);

        result.IsSuccess.Should().BeTrue();
        result.Value.IsOwner.Should().BeFalse();
    }

    [Fact]
    public async Task List_ReturnsOwnAndSharedOnly()
    {
        var service = CreateService();
        var own = SavedAuditQuery.Create(Tenant, "Mine", "{}", Alice);
        var shared = SavedAuditQuery.Create(Tenant, "Shared", "{}", Bob, isPublic: true);
        var privateOther = SavedAuditQuery.Create(Tenant, "Theirs", "{}", Bob);
        _repository
            .Setup(repo => repo.ListVisibleAsync(Alice, Tenant, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<SavedAuditQuery> { own, shared });

        var result = await service.ListAsync(Alice, Tenant);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().OnlyContain(view => view.Name != "Theirs");
        // The repository contract itself enforces visibility; the service must pass acting user + tenant.
        _repository.Verify(repo => repo.ListVisibleAsync(Alice, Tenant, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Update_OwnerOnly_AdvancesRevision()
    {
        var service = CreateService();
        var view = SavedAuditQuery.Create(Tenant, "View", """{"filter":{"field":"action","op":"eq","value":"Login"}}""", Alice);
        _repository.Setup(repo => repo.GetByIdAsync(view.Id, It.IsAny<CancellationToken>())).ReturnsAsync(view);
        _repository
            .Setup(repo => repo.CountByNameAsync(Tenant, "View", view.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        var updated = await service.UpdateAsync(view.Id, ValidInput(name: "View", shared: true), Alice);
        var byNonOwner = await service.UpdateAsync(view.Id, ValidInput(name: "View"), Bob);

        updated.IsSuccess.Should().BeTrue();
        updated.Value.CurrentRevision.Should().Be(2);
        updated.Value.IsShared.Should().BeTrue();
        byNonOwner.IsFailure.Should().BeTrue();
        byNonOwner.Error.Code.Should().Be("SavedAuditQuery.Forbidden");
        _repository.Verify(repo => repo.AddRevision(It.Is<SavedAuditQueryRevision>(revision => revision.Revision == 2 && !revision.IsCreation)), Times.Once);
        _repository.Verify(repo => repo.SaveAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Delete_OwnerOnly()
    {
        var service = CreateService();
        var view = SavedAuditQuery.Create(Tenant, "View", "{}", Alice);
        _repository.Setup(repo => repo.GetByIdAsync(view.Id, It.IsAny<CancellationToken>())).ReturnsAsync(view);

        (await service.DeleteAsync(view.Id, Bob)).IsFailure.Should().BeTrue();
        (await service.DeleteAsync(view.Id, Alice)).IsSuccess.Should().BeTrue();
        _repository.Verify(repo => repo.Remove(view), Times.Once);
    }

    [Fact]
    public async Task ExecuteById_SharedView_RunsStoredQueryAndTracksUsage()
    {
        var service = CreateService();
        var view = SavedAuditQuery.Create(
            Tenant,
            "Failed logins",
            AuditQueryDslJson.Serialize(_parser.ParseJson("""{"filter":{"field":"action","op":"eq","value":"LoginFailed"}}""").Value),
            Alice,
            isPublic: true);
        _repository.Setup(repo => repo.GetByIdAsync(view.Id, It.IsAny<CancellationToken>())).ReturnsAsync(view);
        _executor
            .Setup(exec => exec.ExecuteAsync(It.Is<AuditQueryDslExecutionRequest>(request => request.Take == 25 && request.TenantScope == Tenant), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(new AuditQueryDslExecutionResult { TotalCount = 7, Rows = [] }));

        var result = await service.ExecuteAsync(view.Id, skip: 0, take: 25, actingUserId: Bob, tenantScope: Tenant);

        result.IsSuccess.Should().BeTrue();
        result.Value.TotalCount.Should().Be(7);
        view.ExecutionCount.Should().Be(1);
        view.LastExecutedByUserId.Should().Be(Bob);
        _repository.Verify(repo => repo.SaveAsync(It.IsAny<CancellationToken>()), Times.Once);
        _auditService.Verify(audit => audit.LogAdminActionAsync(Bob, "SavedAuditQueryExecuted", It.IsAny<string>(), It.IsAny<object?>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteById_PrivateView_NonOwnerForbidden()
    {
        var service = CreateService();
        var view = SavedAuditQuery.Create(Tenant, "Private", """{"filter":{"field":"action","op":"eq","value":"Login"}}""", Alice);
        _repository.Setup(repo => repo.GetByIdAsync(view.Id, It.IsAny<CancellationToken>())).ReturnsAsync(view);

        var result = await service.ExecuteAsync(view.Id, 0, 10, Bob, null);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("SavedAuditQuery.Forbidden");
        _executor.Verify(exec => exec.ExecuteAsync(It.IsAny<AuditQueryDslExecutionRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ExecuteById_CorruptedStoredQuery_FailsClosed()
    {
        var service = CreateService();
        var view = SavedAuditQuery.Create(Tenant, "Corrupt", "this is not json", Alice, isPublic: true);
        _repository.Setup(repo => repo.GetByIdAsync(view.Id, It.IsAny<CancellationToken>())).ReturnsAsync(view);

        var result = await service.ExecuteAsync(view.Id, 0, 10, Bob, null);

        result.IsFailure.Should().BeTrue();
        _executor.Verify(exec => exec.ExecuteAsync(It.IsAny<AuditQueryDslExecutionRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Revisions_RequireReadAccess()
    {
        var service = CreateService();
        var view = SavedAuditQuery.Create(Tenant, "Private", "{}", Alice);
        _repository.Setup(repo => repo.GetByIdAsync(view.Id, It.IsAny<CancellationToken>())).ReturnsAsync(view);
        _repository
            .Setup(repo => repo.ListRevisionsAsync(view.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<SavedAuditQueryRevision>());

        (await service.GetRevisionsAsync(view.Id, Bob)).IsFailure.Should().BeTrue();
        (await service.GetRevisionsAsync(view.Id, Alice)).IsSuccess.Should().BeTrue();
    }
}
