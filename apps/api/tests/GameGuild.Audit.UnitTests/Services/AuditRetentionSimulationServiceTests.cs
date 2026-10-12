using System.Text.Json;
using GameGuild.Compliance.Audit;
using GameGuild.Identity.Context.Actors;
using Moq;
using Xunit;

namespace GameGuild.Tests.Audit.Unit.Services;

public sealed class AuditRetentionSimulationServiceTests
{
    private readonly Guid _tenant = Guid.NewGuid();
    private readonly Guid _user = Guid.NewGuid();
    private readonly Mock<IActorContextAccessor> _actors = new();
    private readonly Mock<IAuditRetentionSimulationRepository> _repository = new();
    private readonly Mock<IAuditRetentionDataSource> _source = new();
    private readonly Mock<IAuditService> _audit = new();

    public AuditRetentionSimulationServiceTests() => SetActor("TenantAdmin");
    private void SetActor(string role, bool authenticated = true, bool tenant = true) =>
        _actors.SetupGet(item => item.ActorContext).Returns(new ActorContext
        {
            ActorKind = ActorKind.User, SubjectId = _user.ToString(), TenantId = tenant ? _tenant : null,
            IsAuthenticated = authenticated, Roles = new HashSet<string> { role }, Permissions = new HashSet<string>()
        });
    private AuditRetentionSimulationService Service() => new(_actors.Object, _repository.Object, _source.Object,
        new AuditRetentionSimulationEngine(), _audit.Object, TimeProvider.System);

    [Theory]
    [InlineData("User", true, true)] [InlineData("TenantAdmin", false, true)] [InlineData("SystemAdmin", true, false)]
    public async Task RejectsAllOperationsWithoutAnAuthenticatedTenantAdministrator(string role, bool authenticated, bool tenant)
    {
        SetActor(role, authenticated, tenant);
        var service = Service();
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.GetConfigurationAsync(false, default));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.GetConfigurationAsync(true, default));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.GetPolicyTemplatesAsync(default));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.ConfigureAsync(AuditRetentionSimulationEngineTests.Configuration(), default));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.RunAsync(AuditRetentionSimulationEngineTests.Request(), default));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.GetRunAsync(Guid.NewGuid(), default));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.GetRunsAsync(0, 10, default));
        _repository.VerifyNoOtherCalls();
        _source.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task SavesConfigurationWithActorIdentityAndRevision()
    {
        AuditRetentionConfiguration? saved = null;
        _repository.Setup(item => item.SaveConfigurationAsync(It.IsAny<AuditRetentionConfiguration>(), true, It.IsAny<CancellationToken>()))
            .Callback<AuditRetentionConfiguration, bool, CancellationToken>((configuration, _, _) => saved = configuration)
            .Returns(Task.CompletedTask);
        var response = await Service().ConfigureAsync(AuditRetentionSimulationEngineTests.Configuration(), default);
        Assert.NotNull(saved);
        Assert.Equal(_tenant, response.TenantId);
        Assert.Equal(_user, response.UpdatedByUserId);
        Assert.Equal(1, response.Revision);
        Assert.Equal(1, response.Configuration.ExpectedRevision);
        Assert.Equal(_tenant, saved.TenantId);
        _audit.Verify(item => item.LogAsync(It.Is<CreateAuditLogRequest>(entry => entry.TenantId == _tenant && entry.UserId == _user)), Times.Once);
    }

    [Fact]
    public async Task RejectsStaleRevisionAndInvalidPagination()
    {
        var stored = new AuditRetentionConfiguration { TenantId = _tenant, Revision = 2 };
        _repository.Setup(item => item.GetConfigurationAsync(_tenant, It.IsAny<CancellationToken>())).ReturnsAsync(stored);
        await Assert.ThrowsAsync<AuditRetentionConcurrencyException>(() => Service().ConfigureAsync(AuditRetentionSimulationEngineTests.Configuration(), default));
        await Assert.ThrowsAsync<AuditRetentionValidationException>(() => Service().GetRunsAsync(-1, 10, default));
        await Assert.ThrowsAsync<AuditRetentionValidationException>(() => Service().GetRunsAsync(0, 101, default));
        _repository.Verify(item => item.SaveConfigurationAsync(It.IsAny<AuditRetentionConfiguration>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task PersistsRequestEvidenceAndReportAndReadsOnlyActorTenant()
    {
        var configuration = AuditRetentionSimulationEngineTests.Configuration();
        _repository.Setup(item => item.GetConfigurationAsync(_tenant, It.IsAny<CancellationToken>())).ReturnsAsync(new AuditRetentionConfiguration
        {
            TenantId = _tenant, Revision = 3, ConfigurationJson = JsonSerializer.Serialize(configuration, new JsonSerializerOptions(JsonSerializerDefaults.Web))
        });
        _source.Setup(item => item.CaptureAsync(_tenant, It.IsAny<DateTime>(), 14, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AuditRetentionDataSnapshot([], [], null, "measured empty database"));
        AuditRetentionSimulationRun? saved = null;
        _repository.Setup(item => item.AddRunAsync(It.IsAny<AuditRetentionSimulationRun>(), It.IsAny<CancellationToken>()))
            .Callback<AuditRetentionSimulationRun, CancellationToken>((run, _) => saved = run).Returns(Task.CompletedTask);
        var response = await Service().RunAsync(AuditRetentionSimulationEngineTests.Request(), default);
        Assert.NotNull(response);
        Assert.NotNull(saved);
        Assert.Equal(_user, response.CreatedByUserId);
        Assert.Equal(_tenant, response.TenantId);
        Assert.Equal(3, response.ConfigurationRevision);
        Assert.Contains("measured empty database", saved.ReportJson);
        _repository.Setup(item => item.GetRunAsync(_tenant, saved.Id, It.IsAny<CancellationToken>())).ReturnsAsync(saved);
        var loaded = await Service().GetRunAsync(saved.Id, default);
        Assert.Equal(response.Report.Baseline.TotalCost, loaded!.Report.Baseline.TotalCost);
        Assert.Equal(response.CreatedAtUtc, loaded.CreatedAtUtc);
        Assert.Equal(configuration.Baseline, loaded.ConfigurationSnapshot.Baseline);
        Assert.Equal(0, loaded.Report.Evidence.StoredRecordCount);
        Assert.Null(loaded.Report.Recommendation.SuggestedScenario);
    }

    [Fact]
    public async Task RequiresConfigurationBeforeRunning()
    {
        Assert.Null(await Service().RunAsync(AuditRetentionSimulationEngineTests.Request(), default));
        _source.VerifyNoOtherCalls();
    }
}
