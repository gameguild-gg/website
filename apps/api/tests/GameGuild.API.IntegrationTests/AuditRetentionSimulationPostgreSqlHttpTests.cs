using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using GameGuild.API.Database;
using GameGuild.API.IntegrationTests.Infrastructure;
using GameGuild.Compliance.Audit;
using GameGuild.Identity.Context.Actors;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;

namespace GameGuild.API.IntegrationTests;

[Collection(ApiPostgreSqlCollection.Name)]
public sealed class AuditRetentionSimulationPostgreSqlHttpTests(ApiPostgreSqlFixture fixture)
{
    private const string Route = "/v1/audit/retention-simulation";
    private static ConfigureAuditRetentionRequest Configuration(int revision = 0) => new()
    {
        ExpectedRevision = revision, Currency = "BRL", MonthlyBudget = 50,
        Baseline = new() { Name = "current", RetentionDays = 365, HotDays = 30, WarmUntilDays = 90, ColdUntilDays = 180 },
        Obligations = [new() { Name = "Tenant contract", Source = "Test contract", MinimumRetentionDays = 30 }],
        TierPrices = Enum.GetValues<AuditStorageTier>().Select(tier => new AuditStorageTierPrice
            { Tier = tier, MonthlyCostPerGiB = 10 - (int)tier * 2, ExpectedReadLatencyMilliseconds = (int)tier * 10 }).ToList()
    };
    private static RunAuditRetentionSimulationRequest Request() => new()
    {
        HistoricalDays = 30, ForecastMonths = 24, GrowthModel = AuditRetentionGrowthModel.HistoricalTrend,
        Scenarios = [new() { Name = "shorter", RetentionDays = 90, HotDays = 7, WarmUntilDays = 30, ColdUntilDays = 60 }]
    };

    [Fact]
    public async Task MeasuresRowsRecordsReadsPersistsSnapshotsAndEnforcesTenantAndRevision()
    {
        var tenant = Guid.NewGuid();
        var otherTenant = Guid.NewGuid();
        var user = Guid.NewGuid();
        var date = DateTime.UtcNow.Date;
        await using (var seedScope = fixture.Factory.Services.CreateAsyncScope())
        {
            var context = seedScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            context.Set<AuditLog>().AddRange(Enumerable.Range(1, 30).Select(age => new AuditLog
            {
                TenantId = tenant, ActionType = "RetentionTest", ResourceType = "Test", CreatedAt = date.AddDays(-age),
                Description = new string('a', age * 10)
            }));
            context.Set<AuditLog>().Add(new AuditLog { TenantId = otherTenant, ActionType = "OtherTenant", ResourceType = "Test", CreatedAt = date.AddDays(-1) });
            context.Set<TamperEvidentAuditLog>().Add(TamperEvidentAuditLog.Create(tenant, user,
                "RetentionTest", "Test", null, null, null, "{}", "Low", "127.0.0.1", "test", null, null, null,
                "previous", 1));
            await context.SaveChangesAsync();
            var snapshot = await seedScope.ServiceProvider.GetRequiredService<IAuditRetentionDataSource>()
                .CaptureAsync(tenant, DateTime.UtcNow, 30, default);
            Assert.Equal(31, snapshot.Cohorts.Sum(cohort => cohort.RecordCount));
            Assert.True(snapshot.Cohorts.Sum(cohort => cohort.LogicalBytes) > 0);
        }
        using var admin = fixture.CreateAuthenticatedClient(user, tenant, isSystemAdmin: true);
        var forgedConfiguration = JsonSerializer.SerializeToNode(Configuration(), new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        forgedConfiguration["tenantId"] = otherTenant.ToString();
        forgedConfiguration["updatedByUserId"] = Guid.NewGuid().ToString();
        var configured = await admin.PutAsJsonAsync(Route + "/configuration", forgedConfiguration);
        Assert.Equal(HttpStatusCode.OK, configured.StatusCode);
        var configuration = await configured.Content.ReadFromJsonAsync<AuditRetentionConfigurationResponse>();
        Assert.Equal(user, configuration!.UpdatedByUserId);
        Assert.Equal(tenant, configuration.TenantId);
        Assert.Equal(1, configuration.Revision);
        Assert.Equal(HttpStatusCode.Conflict, (await admin.PutAsJsonAsync(Route + "/configuration", Configuration())).StatusCode);

        // Exercise the production read service; the fixture intentionally omits the older audit controller's database policy seeds.
        await using (var readScope = fixture.Factory.Services.CreateAsyncScope())
        {
            var actors = readScope.ServiceProvider.GetRequiredService<IActorContextAccessor>();
            actors.SetActorContext(new ActorContext
            {
                ActorKind = ActorKind.User, SubjectId = user.ToString(), TenantId = tenant, IsAuthenticated = true,
                Roles = new HashSet<string> { "SystemAdmin" }, Permissions = new HashSet<string>()
            });
            try
            {
                var rows = await readScope.ServiceProvider.GetRequiredService<IAuditService>()
                    .GetAuditLogsAsync(new AuditLogQuery { TenantId = tenant, ActionType = "RetentionTest", Take = 100 });
                Assert.Equal(30, rows.Count);
            }
            finally { actors.ClearActorContext(); }
        }
        var forgedRequest = JsonSerializer.SerializeToNode(Request(), new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        forgedRequest["tenantId"] = otherTenant.ToString();
        forgedRequest["createdByUserId"] = Guid.NewGuid().ToString();
        var simulated = await admin.PostAsJsonAsync(Route, forgedRequest);
        var errorText = await simulated.Content.ReadAsStringAsync();
        Assert.True(simulated.StatusCode == HttpStatusCode.OK, errorText);
        var run = JsonSerializer.Deserialize<AuditRetentionSimulationResponse>(errorText, new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        Assert.Equal(tenant, run.TenantId);
        Assert.Equal(user, run.CreatedByUserId);
        Assert.Equal(1, run.ConfigurationRevision);
        Assert.True(run.Report.Evidence.StoredRecordCount >= 31);
        Assert.True(run.Report.Evidence.StoredLogicalBytes > 0);
        Assert.Equal(30, run.Report.Evidence.ObservedReadCount);
        Assert.Equal(24, run.Report.Baseline.Months.Count);
        Assert.Equal(2, run.Report.Baseline.Years.Count);
        Assert.Equal("BRL", run.Report.Currency);
        Assert.Equal("MeetsConfiguredObligations", run.Report.Scenarios.Single().ComplianceStatus);
        Assert.NotNull(run.Report.Recommendation.SuggestedScenario);

        Assert.Equal(HttpStatusCode.OK, (await admin.PutAsJsonAsync(Route + "/configuration", Configuration(1) with { Currency = "USD" })).StatusCode);
        var loaded = await admin.GetFromJsonAsync<AuditRetentionSimulationResponse>($"{Route}/{run.Id}");
        Assert.Equal("BRL", loaded!.ConfigurationSnapshot.Currency);
        Assert.Equal(1, loaded.ConfigurationRevision);
        Assert.Equal(run.Report.Baseline.TotalCost, loaded.Report.Baseline.TotalCost);
        Assert.Equal(run.Report.Evidence.StoredRecordCount, loaded.Report.Evidence.StoredRecordCount);
        Assert.Equal(run.Id, (await admin.GetFromJsonAsync<List<AuditRetentionSimulationSummary>>(Route))!.Single().Id);
        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync("/api/audit/retention-simulation/configuration")).StatusCode);

        using var other = fixture.CreateAuthenticatedClient(user, otherTenant, isSystemAdmin: true);
        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync($"{Route}/{run.Id}")).StatusCode);
        Assert.Empty((await other.GetFromJsonAsync<List<AuditRetentionSimulationSummary>>(Route))!);

        await using var verifyScope = fixture.Factory.Services.CreateAsyncScope();
        var verified = verifyScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.Equal(30, await verified.Set<AuditLog>().CountAsync(log => log.TenantId == tenant && log.ActionType == "RetentionTest"));
        Assert.Equal(30, await verified.Set<AuditDataAccessObservation>().Where(item => item.TenantId == tenant).SumAsync(item => item.ReadCount));
        Assert.Equal(1, await verified.Set<AuditRetentionSimulationRun>().CountAsync(item => item.TenantId == tenant));
    }

    [Fact]
    public async Task DeniesAnonymousOrdinaryUsersAndInvalidInputs()
    {
        using var anonymous = fixture.Factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync(Route)).StatusCode);
        using var user = fixture.CreateAuthenticatedClient(Guid.NewGuid(), Guid.NewGuid());
        Assert.Equal(HttpStatusCode.Forbidden, (await user.GetAsync(Route)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await user.PutAsJsonAsync(Route + "/configuration", Configuration())).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await user.PostAsJsonAsync(Route, Request())).StatusCode);
        using var admin = fixture.CreateAuthenticatedClient(Guid.NewGuid(), Guid.NewGuid(), isSystemAdmin: true);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PutAsJsonAsync(Route + "/configuration", Configuration() with { TierPrices = [] })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync(Route, Request() with { ForecastMonths = 121 })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.GetAsync(Route + "?take=101")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.GetAsync(Route + "?skip=-1")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.GetAsync(Route + "?take=0")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.PostAsJsonAsync(Route, Request())).StatusCode);
    }

    [Fact]
    public async Task RecordsOnlyYieldedStreamRowsAndSameTenantDates()
    {
        var tenant = Guid.NewGuid();
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        context.Set<AuditLog>().AddRange(Enumerable.Range(1, 10).Select(age => new AuditLog
        {
            TenantId = tenant, ActionType = "StreamRead", ResourceType = "Test", CreatedAt = DateTime.UtcNow.AddDays(-age)
        }));
        await context.SaveChangesAsync();
        var actors = scope.ServiceProvider.GetRequiredService<IActorContextAccessor>();
        actors.SetActorContext(new ActorContext
        {
            ActorKind = ActorKind.User, SubjectId = Guid.NewGuid().ToString(), TenantId = tenant,
            IsAuthenticated = true, Roles = new HashSet<string> { "TenantAdmin" }, Permissions = new HashSet<string>()
        });
        try
        {
            var audit = scope.ServiceProvider.GetRequiredService<IAuditService>();
            var count = 0;
            await foreach (var row in audit.StreamAuditLogsAsync(new AuditLogQuery { TenantId = tenant }, default))
            {
                count++;
                if (count == 3) { break; }
            }
            var recorder = scope.ServiceProvider.GetRequiredService<IAuditDataAccessRecorder>();
            await recorder.RecordAsync([new(Guid.NewGuid(), DateTime.UtcNow.AddDays(-10))]);
            var snapshot = await scope.ServiceProvider.GetRequiredService<IAuditRetentionDataSource>()
                .CaptureAsync(tenant, DateTime.UtcNow, 30, default);
            Assert.Equal(3, snapshot.AccessAges.Sum(bucket => bucket.ReadCount));
            Assert.Equal(10, snapshot.Cohorts.Sum(cohort => cohort.RecordCount));
        }
        finally { actors.ClearActorContext(); }
    }

    [Fact]
    public async Task ConcurrentConfigurationCreationAndRevisionUpdateHaveOneWinner()
    {
        var tenant = Guid.NewGuid();
        using var first = fixture.CreateAuthenticatedClient(Guid.NewGuid(), tenant, isSystemAdmin: true);
        using var second = fixture.CreateAuthenticatedClient(Guid.NewGuid(), tenant, isSystemAdmin: true);
        var creation = await Task.WhenAll(first.PutAsJsonAsync(Route + "/configuration", Configuration()),
            second.PutAsJsonAsync(Route + "/configuration", Configuration()));
        Assert.Equal(1, creation.Count(response => response.StatusCode == HttpStatusCode.OK));
        Assert.Equal(1, creation.Count(response => response.StatusCode == HttpStatusCode.Conflict));
        var update = await Task.WhenAll(first.PutAsJsonAsync(Route + "/configuration", Configuration(1)),
            second.PutAsJsonAsync(Route + "/configuration", Configuration(1)));
        Assert.Equal(1, update.Count(response => response.StatusCode == HttpStatusCode.OK));
        Assert.Equal(1, update.Count(response => response.StatusCode == HttpStatusCode.Conflict));
        var saved = await first.GetFromJsonAsync<AuditRetentionConfigurationResponse>(Route + "/configuration");
        Assert.Equal(2, saved!.Revision);
    }

    [Fact]
    public async Task PolicyTemplatesAndInheritedBaselineAreServedUnderPolicyRoutes()
    {
        var tenant = Guid.NewGuid();
        using var admin = fixture.CreateAuthenticatedClient(Guid.NewGuid(), tenant, isSystemAdmin: true);

        var templates = await admin.GetFromJsonAsync<List<AuditRetentionPolicyTemplate>>("/api/audit/retention-policies/templates");
        Assert.NotNull(templates);
        Assert.Contains(templates, template => template.IsBaseline && template.BaseTemplateId is null);
        Assert.All(templates.Where(template => !template.IsBaseline),
            template => Assert.Equal("gameguild-platform-baseline", template.BaseTemplateId));
        Assert.All(templates, template =>
        {
            Assert.NotEmpty(template.SensitivityRules);
            Assert.NotEmpty(template.Obligations);
            Assert.Equal(4, template.TierPrices.Count);
        });

        // No explicit tenant configuration: plain GET stays 404, inherited GET serves the baseline.
        Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync("/api/audit/retention-policies/configuration")).StatusCode);
        var inherited = await admin.GetFromJsonAsync<AuditRetentionConfigurationResponse>(
            "/api/audit/retention-policies/configuration?includeInherited=true");
        Assert.NotNull(inherited);
        Assert.Equal(tenant, inherited.TenantId);
        Assert.Equal(0, inherited.Revision);
        Assert.Equal("gameguild-platform-baseline", inherited.InheritedFromTemplateId);
        Assert.Equal("USD", inherited.Configuration.Currency);

        // Templates and inheritance are tenant-admin only.
        using var anonymous = fixture.Factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/audit/retention-policies/templates")).StatusCode);
        using var user = fixture.CreateAuthenticatedClient(Guid.NewGuid(), Guid.NewGuid());
        Assert.Equal(HttpStatusCode.Forbidden, (await user.GetAsync("/api/audit/retention-policies/templates")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await user.GetAsync("/api/audit/retention-policies/configuration?includeInherited=true")).StatusCode);
    }

    [Fact]
    public async Task OpenApiDocumentsSimulationRoutesAndTypedInputs()
    {
        using var developmentFactory = fixture.CreateFactory(builder => builder.UseEnvironment("Development"));
        using var client = developmentFactory.CreateClient();
        var response = await client.GetAsync("/swagger/v1/swagger.json");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(json);
        var paths = document.RootElement.GetProperty("paths");
        Assert.True(paths.TryGetProperty(Route, out var simulation));
        Assert.True(simulation.TryGetProperty("post", out _));
        var pagination = simulation.GetProperty("get").GetProperty("parameters").EnumerateArray()
            .ToDictionary(parameter => parameter.GetProperty("name").GetString()!);
        Assert.Equal(0, pagination["skip"].GetProperty("schema").GetProperty("minimum").GetInt32());
        Assert.Equal(1, pagination["take"].GetProperty("schema").GetProperty("minimum").GetInt32());
        Assert.Equal(100, pagination["take"].GetProperty("schema").GetProperty("maximum").GetInt32());
        Assert.True(paths.TryGetProperty(Route + "/configuration", out var configuration));
        Assert.True(configuration.TryGetProperty("put", out _));
        Assert.True(paths.TryGetProperty("/api/audit/retention-simulation", out _));
        Assert.True(paths.TryGetProperty("/api/audit/retention-policies", out _));
        Assert.True(paths.TryGetProperty("/api/audit/retention-policies/configuration", out var policyConfiguration));
        Assert.True(policyConfiguration.TryGetProperty("put", out _));
        Assert.True(policyConfiguration.TryGetProperty("get", out var policyGet));
        Assert.Contains(policyGet.GetProperty("parameters").EnumerateArray(),
            parameter => parameter.GetProperty("name").GetString() == "includeInherited");
        Assert.True(paths.TryGetProperty("/api/audit/retention-policies/templates", out var templatesPath));
        Assert.True(templatesPath.TryGetProperty("get", out _));
        var schemas = document.RootElement.GetProperty("components").GetProperty("schemas");
        Assert.True(schemas.TryGetProperty("Compliance_Audit_" + nameof(AuditRetentionSimulationReport), out _));
        Assert.Equal("string", schemas.GetProperty("Compliance_Audit_" + nameof(AuditStorageTier)).GetProperty("type").GetString());
        Assert.True(schemas.TryGetProperty("Compliance_Audit_" + nameof(AuditRetentionPolicyTemplate), out _));
        var capturePath = Environment.GetEnvironmentVariable("GAMEGUILD_RETENTION_OPENAPI_CAPTURE");
        if (!string.IsNullOrEmpty(capturePath))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(capturePath))!);
            await File.WriteAllTextAsync(capturePath, json);
        }
    }
}
