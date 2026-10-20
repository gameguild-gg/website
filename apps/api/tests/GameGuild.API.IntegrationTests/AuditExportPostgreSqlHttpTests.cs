using System.Net;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Xml.Linq;
using GameGuild.API.Database;
using GameGuild.API.IntegrationTests.Infrastructure;
using GameGuild.Compliance.Audit;
using GameGuild.Identity.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Moq;
using Microsoft.OpenApi.Models;
using Microsoft.OpenApi.Writers;
using Swashbuckle.AspNetCore.Swagger;
using GameGuild.Configuration.PresentationLayer.RateLimiting;

namespace GameGuild.API.IntegrationTests;

[Collection(ApiPostgreSqlCollection.Name)]
public sealed class AuditExportPostgreSqlHttpTests(ApiPostgreSqlFixture fixture) : IAsyncLifetime
{
    public async Task InitializeAsync()
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<PolicyDefinitionSeeder>().SeedAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Theory]
    [InlineData("/api/audit/export/csv", "csv", "text/csv")]
    [InlineData("/v1/admin/audit-logs/export/csv", "csv", "text/csv")]
    [InlineData("/v1/admin/audit-logs/:export", "csv", "text/csv")]
    [InlineData("/api/audit/export/json", "json", "application/json")]
    [InlineData("/v1/admin/audit-logs/export/json", "json", "application/json")]
    [InlineData("/api/audit/export/xml", "xml", "application/xml")]
    [InlineData("/v1/admin/audit-logs/export/xml", "xml", "application/xml")]
    public async Task Requested_export_routes_stream_filtered_records_with_download_headers(string route, string format, string mediaType)
    {
        var tenant = Guid.NewGuid(); var actor = Guid.NewGuid();
        var action = "ExportContract-" + Guid.NewGuid().ToString("N");
        var instant = DateTime.UtcNow.Date.AddSeconds(1);
        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.Set<AuditLog>().AddRange(
                new AuditLog { TenantId = tenant, UserId = actor, ActionType = action, ResourceType = "Test",
                    CreatedAt = instant, RiskLevel = AuditRiskLevel.High, Description = "target, \"quoted\"",
                    Metadata = "{\"nested\":{\"id\":\"contract-proof\"}}" },
                new AuditLog { TenantId = tenant, UserId = actor, ActionType = action, ResourceType = "Test",
                    CreatedAt = instant, RiskLevel = AuditRiskLevel.Low, Description = "excluded-risk" },
                new AuditLog { TenantId = Guid.NewGuid(), UserId = actor, ActionType = action, ResourceType = "Test",
                    CreatedAt = instant, RiskLevel = AuditRiskLevel.High, Description = "excluded-tenant" },
                new AuditLog { TenantId = tenant, UserId = Guid.NewGuid(), ActionType = action, ResourceType = "Test",
                    CreatedAt = instant, RiskLevel = AuditRiskLevel.High, Description = "excluded-actor" },
                new AuditLog { TenantId = tenant, UserId = actor, ActionType = action + "-other", ResourceType = "Test",
                    CreatedAt = instant, RiskLevel = AuditRiskLevel.High, Description = "excluded-action" },
                new AuditLog { TenantId = tenant, UserId = actor, ActionType = action, ResourceType = "Test",
                    CreatedAt = instant.AddDays(-1), RiskLevel = AuditRiskLevel.High, Description = "excluded-date" });
            await db.SaveChangesAsync();
        }
        using var client = fixture.CreateAuthenticatedClient(actor, tenant, true);
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue(mediaType));
        var response = await client.PostAsJsonAsync(route, new AuditExportRequest
        {
            TenantId = tenant, UserId = actor, ActionType = action, RiskLevel = AuditRiskLevel.High,
            StartDate = instant.AddSeconds(-1), EndDate = instant.AddSeconds(1),
            Columns = ["Description", "ActionType"], PageNumber = 1, PageSize = 10
        });
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.OK, body);
        Assert.Equal(mediaType, response.Content.Headers.ContentType!.MediaType);
        Assert.Equal("utf-8", response.Content.Headers.ContentType.CharSet);
        Assert.Equal("attachment", response.Content.Headers.ContentDisposition!.DispositionType);
        Assert.Equal("1", response.Headers.GetValues("X-Audit-Total-Records").Single());
        Assert.DoesNotContain("excluded-", body);
        if (format == "csv") { Assert.Equal($"Description,ActionType\r\n\"target, \"\"quoted\"\"\",{action}\r\n", body); }
        else if (format == "json")
        {
            using var json = JsonDocument.Parse(body);
            Assert.Equal("1.0", json.RootElement.GetProperty("schemaVersion").GetString());
            Assert.Equal(1, json.RootElement.GetProperty("pagination").GetProperty("totalRecords").GetInt32());
            var record = Assert.Single(json.RootElement.GetProperty("records").EnumerateArray());
            Assert.Equal(actor, record.GetProperty("actor").GetProperty("userId").GetGuid());
            Assert.Equal(tenant, record.GetProperty("actor").GetProperty("tenantId").GetGuid());
            Assert.Equal("contract-proof", record.GetProperty("metadata").GetProperty("nested").GetProperty("id").GetString());
        }
        else
        {
            var document = XDocument.Parse(body);
            Assert.Equal("1.0", document.Root!.Element("schemaVersion")!.Value);
            Assert.Equal("1", document.Root.Element("pagination")!.Element("totalRecords")!.Value);
            var record = Assert.Single(document.Root.Element("records")!.Elements("record"));
            Assert.Equal(actor.ToString("D"), record.Element("actor")!.Element("userId")!.Value);
            Assert.Equal(tenant.ToString("D"), record.Element("actor")!.Element("tenantId")!.Value);
            using var metadata = JsonDocument.Parse(record.Element("metadata")!.Value);
            Assert.Equal("contract-proof", metadata.RootElement.GetProperty("nested").GetProperty("id").GetString());
        }
        var exportId = Guid.Parse(response.Headers.GetValues("X-Audit-Export-Id").Single());
        var progress = await client.GetAsync($"/v1/admin/audit-logs/export/{exportId}/progress");
        Assert.Equal(HttpStatusCode.OK, progress.StatusCode);
        using var other = fixture.CreateAuthenticatedClient(Guid.NewGuid(), tenant, true);
        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync($"/v1/admin/audit-logs/export/{exportId}/progress")).StatusCode);
        await using var auditScope = fixture.Factory.Services.CreateAsyncScope();
        var auditDb = auditScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.True(await auditDb.Set<AuditLog>().AnyAsync(log => log.ActionType == "ExportAuditLogs" && log.UserId == actor));
    }

    [Theory]
    [InlineData("csv")]
    [InlineData("json")]
    [InlineData("xml")]
    public async Task Anonymous_and_non_admin_actors_cannot_export(string format)
    {
        using var anonymous = fixture.Factory.CreateClient();
        using var member = fixture.CreateAuthenticatedClient(Guid.NewGuid(), Guid.NewGuid());
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await anonymous.PostAsJsonAsync("/api/audit/export/" + format, new AuditExportRequest())).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await member.PostAsJsonAsync("/api/audit/export/" + format, new AuditExportRequest())).StatusCode);
    }

    [Theory]
    [InlineData("csv")]
    [InlineData("json")]
    [InlineData("xml")]
    public async Task Invalid_pagination_returns_structured_errors_before_starting_export(string format)
    {
        using var client = fixture.CreateAuthenticatedClient(Guid.NewGuid(), Guid.NewGuid(), true);
        using var response = await client.PostAsJsonAsync("/api/audit/export/" + format,
            new AuditExportRequest { PageNumber = 1, PageSize = 1001 });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains(response.Content.Headers.ContentType!.MediaType, new[] { "application/problem+json", "application/json" });
        Assert.False(response.Headers.Contains("X-Audit-Export-Id"));
        using var error = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(400, error.RootElement.GetProperty("status").GetInt32());
    }

    [Fact]
    public async Task Invalid_csv_columns_are_rejected_before_starting_export()
    {
        using var client = fixture.CreateAuthenticatedClient(Guid.NewGuid(), Guid.NewGuid(), true);
        using var response = await client.PostAsJsonAsync("/api/audit/export/csv",
            new AuditExportRequest { Columns = ["Description", "Unknown"] });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType!.MediaType);
        Assert.False(response.Headers.Contains("X-Audit-Export-Id"));
    }

    [Theory]
    [InlineData("csv", "application/json")]
    [InlineData("json", "text/csv")]
    [InlineData("json", "application/json;q=0")]
    [InlineData("json", "application/json;q=0, */*;q=1")]
    [InlineData("json", "application/json;charset=utf-8;q=0, application/json;q=1")]
    [InlineData("json", "application/json;charset=iso-8859-1")]
    [InlineData("csv", "text/csv;q=0, text/*;q=1")]
    [InlineData("xml", "application/json")]
    [InlineData("xml", "application/xml;q=0, */*;q=1")]
    [InlineData("xml", "application/xml;charset=iso-8859-1")]
    public async Task Unacceptable_export_media_types_do_not_start_export(string format, string accept)
    {
        using var client = fixture.CreateAuthenticatedClient(Guid.NewGuid(), Guid.NewGuid(), true);
        client.DefaultRequestHeaders.Accept.ParseAdd(accept);
        using var response = await client.PostAsJsonAsync("/api/audit/export/" + format, new AuditExportRequest());
        Assert.Equal(HttpStatusCode.NotAcceptable, response.StatusCode);
        Assert.False(response.Headers.Contains("X-Audit-Export-Id"));
    }

    [Theory]
    [InlineData("csv", "text/*")]
    [InlineData("csv", "*/*")]
    [InlineData("json", "application/*")]
    [InlineData("json", "*/*")]
    [InlineData("json", "application/json;charset=utf-8")]
    [InlineData("json", "text/csv;q=1, application/json;q=0.5")]
    [InlineData("xml", "application/*")]
    [InlineData("xml", "*/*")]
    [InlineData("xml", "application/xml;charset=utf-8")]
    public async Task Compatible_media_ranges_are_accepted(string format, string accept)
    {
        using var client = fixture.CreateAuthenticatedClient(Guid.NewGuid(), Guid.NewGuid(), true);
        client.DefaultRequestHeaders.Accept.ParseAdd(accept);
        using var response = await client.PostAsJsonAsync("/api/audit/export/" + format,
            new AuditExportRequest { ActionType = "NoMatchingEvents-" + Guid.NewGuid().ToString("N") });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Theory]
    [InlineData("csv", "invalid-media-range")]
    [InlineData("json", "invalid-media-range")]
    [InlineData("csv", "text/csv;q=2")]
    [InlineData("json", "application/json;q=banana")]
    [InlineData("xml", "invalid-media-range")]
    [InlineData("xml", "application/xml;q=banana")]
    public async Task Malformed_accept_headers_are_rejected_before_starting_export(string format, string accept)
    {
        using var client = fixture.CreateAuthenticatedClient(Guid.NewGuid(), Guid.NewGuid(), true);
        client.DefaultRequestHeaders.TryAddWithoutValidation("Accept", accept);
        using var response = await client.PostAsJsonAsync("/api/audit/export/" + format, new AuditExportRequest());
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.False(response.Headers.Contains("X-Audit-Export-Id"));
    }

    [Theory]
    [InlineData("json", "application/json")]
    [InlineData("xml", "application/xml")]
    public async Task Document_pagination_and_optional_gzip_preserve_schema_and_original_text(string format, string mediaType)
    {
        var actor = Guid.NewGuid();
        var tenant = Guid.NewGuid();
        var action = "GzipContract-" + Guid.NewGuid().ToString("N");
        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            for (var index = 0; index < 3; index++)
            {
                db.Set<AuditLog>().Add(new AuditLog
                {
                    TenantId = tenant, UserId = actor, ActionType = action, ResourceType = "Test",
                    CreatedAt = DateTime.UtcNow.Date.AddMinutes(index),
                    Description = "=1+1 " + new string('é', 995), Metadata = "{\"nested\":{\"items\":[1,2]}}"
                });
            }
            await db.SaveChangesAsync();
        }
        using var client = fixture.CreateAuthenticatedClient(actor, tenant, true);
        client.DefaultRequestHeaders.Accept.ParseAdd(mediaType);
        client.DefaultRequestHeaders.AcceptEncoding.ParseAdd("gzip");
        using var compressed = await client.PostAsJsonAsync("/api/audit/export/" + format,
            new AuditExportRequest { TenantId = tenant, ActionType = action, PageNumber = 2, PageSize = 1 });
        Assert.Equal(HttpStatusCode.OK, compressed.StatusCode);
        Assert.Contains("gzip", compressed.Content.Headers.ContentEncoding);
        Assert.Contains("Accept-Encoding", compressed.Headers.Vary);
        await using var bytes = await compressed.Content.ReadAsStreamAsync();
        await using var gzip = new GZipStream(bytes, CompressionMode.Decompress);
        using var reader = new StreamReader(gzip);
        var decompressed = await reader.ReadToEndAsync();
        if (format == "json")
        {
            using var json = JsonDocument.Parse(decompressed);
            var pagination = json.RootElement.GetProperty("pagination");
            Assert.Equal(2, pagination.GetProperty("pageNumber").GetInt32());
            Assert.Equal(1, pagination.GetProperty("pageSize").GetInt32());
            Assert.Equal(3, pagination.GetProperty("totalRecords").GetInt32());
            Assert.Equal(3, pagination.GetProperty("totalPages").GetInt32());
            var record = Assert.Single(json.RootElement.GetProperty("records").EnumerateArray());
            Assert.StartsWith("=1+1 ", record.GetProperty("description").GetString());
            Assert.Equal(2, record.GetProperty("metadata").GetProperty("nested").GetProperty("items").GetArrayLength());
        }
        else
        {
            var xml = XDocument.Parse(decompressed);
            var pagination = xml.Root!.Element("pagination")!;
            Assert.Equal("2", pagination.Element("pageNumber")!.Value);
            Assert.Equal("1", pagination.Element("pageSize")!.Value);
            Assert.Equal("3", pagination.Element("totalRecords")!.Value);
            Assert.Equal("3", pagination.Element("totalPages")!.Value);
            var record = Assert.Single(xml.Root.Element("records")!.Elements("record"));
            Assert.StartsWith("=1+1 ", record.Element("description")!.Value);
            using var metadata = JsonDocument.Parse(record.Element("metadata")!.Value);
            Assert.Equal(2, metadata.RootElement.GetProperty("nested").GetProperty("items").GetArrayLength());
        }

        client.DefaultRequestHeaders.AcceptEncoding.Clear();
        using var plain = await client.PostAsJsonAsync("/api/audit/export/" + format,
            new AuditExportRequest { TenantId = tenant, ActionType = action, PageNumber = 2, PageSize = 1 });
        Assert.Empty(plain.Content.Headers.ContentEncoding);
        Assert.Equal(decompressed, await plain.Content.ReadAsStringAsync());
    }

    [Theory]
    [InlineData("csv")]
    [InlineData("json")]
    [InlineData("xml")]
    public async Task Preparation_failures_return_safe_structured_errors(string format)
    {
        var service = new Mock<IAuditService>();
        service.Setup(value => value.GetAuditLogCountAsync(It.IsAny<AuditLogQuery>()))
            .ThrowsAsync(new InvalidOperationException("private database password and connection details"));
        using var factory = fixture.CreateFactory(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IAuditService>();
            services.AddSingleton(service.Object);
        }));
        using var client = factory.CreateClient();
        Authenticate(client, Guid.NewGuid(), Guid.NewGuid());
        using var response = await client.PostAsJsonAsync("/api/audit/export/" + format, new AuditExportRequest());
        var body = await response.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType!.MediaType);
        Assert.DoesNotContain("private database", body);
        Assert.DoesNotContain("password", body);
        using var error = JsonDocument.Parse(body);
        Assert.Equal(500, error.RootElement.GetProperty("status").GetInt32());
    }

    [Theory]
    [InlineData("csv")]
    [InlineData("json")]
    [InlineData("xml")]
    public async Task Concurrent_alias_and_versioned_exports_share_the_user_rate_limit(string format)
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        var service = new Mock<IAuditService>();
        service.Setup(value => value.GetAuditLogCountAsync(It.IsAny<AuditLogQuery>()))
            .Returns(() => { entered.TrySetResult(); return release.Task; });
        service.Setup(value => value.StreamAuditLogsAsync(It.IsAny<AuditLogQuery>(), It.IsAny<CancellationToken>()))
            .Returns(EmptyRecords());
        using var factory = fixture.CreateFactory(builder =>
        {
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["Redis:Enabled"] = "false", ["PresentationLayer:EnableRateLimiting"] = "true",
                    ["PresentationLayer:RateLimiting:EnableRateLimiting"] = "true",
                    ["PresentationLayer:RateLimiting:Limit"] = "10000",
                    ["PresentationLayer:RateLimiting:MaxConcurrentRequests"] = "1",
                    ["PresentationLayer:RateLimiting:QueueLimit"] = "0"
                }));
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IAuditService>();
                services.AddSingleton(service.Object);
                // Minimal-host configuration callbacks run after these typed options
                // are bound. Configure the instance used by the real limiter.
                var options = (RateLimitingOptions)services.Single(descriptor =>
                    descriptor.ServiceType == typeof(RateLimitingOptions)).ImplementationInstance!;
                options.MaxConcurrentRequests = 1;
                options.QueueLimit = 0;
                options.Limit = 10000;
                options.Validate();
            });
        });
        using var client = factory.CreateClient();
        client.Timeout = TimeSpan.FromSeconds(15);
        Assert.Equal(1, factory.Services.GetRequiredService<RateLimitingOptions>().MaxConcurrentRequests);
        Authenticate(client, Guid.NewGuid(), Guid.NewGuid());
        var first = client.PostAsJsonAsync("/api/audit/export/" + format, new AuditExportRequest());
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(30));
            using var rejected = await client.PostAsJsonAsync("/v1/admin/audit-logs/export/" + format, new AuditExportRequest())
                .WaitAsync(TimeSpan.FromSeconds(10));
            Assert.Equal(HttpStatusCode.TooManyRequests, rejected.StatusCode);
            Assert.Equal("application/problem+json", rejected.Content.Headers.ContentType!.MediaType);
            Assert.NotNull(rejected.Headers.RetryAfter);
            service.Verify(value => value.GetAuditLogCountAsync(It.IsAny<AuditLogQuery>()), Times.Once);
        }
        finally { release.TrySetResult(0); }
        using var completed = await first;
        Assert.Equal(HttpStatusCode.OK, completed.StatusCode);
        using var retry = await client.PostAsJsonAsync("/api/audit/export/" + format, new AuditExportRequest());
        Assert.Equal(HttpStatusCode.OK, retry.StatusCode);
    }

    [Fact]
    public void OpenApi_includes_both_requested_aliases_and_existing_guarded_routes()
    {
        var document = fixture.Factory.Services.GetRequiredService<ISwaggerProvider>().GetSwagger("v1");
        foreach (var route in new[]
        {
            "/api/audit/export/csv", "/api/audit/export/json", "/api/audit/export/xml",
            "/v1/admin/audit-logs/export/csv", "/v1/admin/audit-logs/:export", "/v1/admin/audit-logs/export/json",
            "/v1/admin/audit-logs/export/xml"
        })
        {
            Assert.True(document.Paths.TryGetValue(route, out var path), "Missing OpenAPI route: " + route);
            Assert.True(path!.Operations[OperationType.Post].Security.Count > 0 || document.SecurityRequirements.Count > 0);
        }
        var csvContent = document.Paths["/api/audit/export/csv"].Operations[OperationType.Post].Responses["200"].Content;
        Assert.Equal(new[] { "text/csv" }, csvContent.Keys);
        var csvSchema = csvContent["text/csv"].Schema;
        Assert.Equal("binary", csvSchema.Format);
        var xmlContent = document.Paths["/api/audit/export/xml"].Operations[OperationType.Post].Responses["200"].Content;
        Assert.Equal(new[] { "application/xml" }, xmlContent.Keys);
        Assert.Equal("binary", xmlContent["application/xml"].Schema.Format);
        var artifact = Environment.GetEnvironmentVariable("AUDIT_EXPORT_OPENAPI_ARTIFACT");
        if (!string.IsNullOrWhiteSpace(artifact))
        {
            using var output = new StringWriter(System.Globalization.CultureInfo.InvariantCulture);
            document.SerializeAsV3(new OpenApiJsonWriter(output));
            File.WriteAllText(artifact, output.ToString());
        }
    }

    [Theory]
    [InlineData("xml")]
    [InlineData("XML")]
    public async Task Action_search_XML_exports_use_the_guarded_filtered_projection(string format)
    {
        var tenant = Guid.NewGuid();
        var actor = Guid.NewGuid();
        var action = "xml.search." + Guid.NewGuid().ToString("N");
        await using (var seed = fixture.Factory.Services.CreateAsyncScope())
        {
            var db = seed.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.AddRange(
                new AuditLog { TenantId = tenant, UserId = actor, ActionType = action, ResourceType = "Permission", Description = "<visible>" },
                new AuditLog { TenantId = Guid.NewGuid(), UserId = actor, ActionType = action, ResourceType = "Permission", Description = "foreign-tenant" });
            await db.SaveChangesAsync();
        }
        using var client = fixture.CreateAuthenticatedClient(actor, tenant, true);
        using var response = await client.GetAsync($"/v1/admin/audit-logs/search/by-action-type/export?format={format}&TenantId={tenant:D}&ActionTypes={action}");
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.OK, body);
        Assert.Equal("application/xml", response.Content.Headers.ContentType!.MediaType);
        Assert.EndsWith(".xml", response.Content.Headers.ContentDisposition!.FileName!.Trim('"'));
        var item = Assert.Single(XDocument.Parse(body).Root!.Element("records")!.Elements("record"));
        Assert.Equal("<visible>", item.Element("description")!.Value);
        Assert.Equal(tenant.ToString("D"), item.Element("actor")!.Element("tenantId")!.Value);
        Assert.DoesNotContain("foreign-tenant", body);
    }

    [Fact]
    public async Task Scheduled_XML_export_streams_real_PostgreSQL_rows_and_retains_hash_history_and_tenant_scoped_download()
    {
        // Collection-lived hosts have their own scheduled-export worker. Use a separate
        // migrated database so none can claim this test's deliberately due operation.
        var isolated = new ApiPostgreSqlFixture();
        try
        {
            await isolated.InitializeAsync();
            await VerifyScheduledXmlExportAsync(isolated);
        }
        finally
        {
            await isolated.DisposeAsync();
        }
    }

    private static async Task VerifyScheduledXmlExportAsync(ApiPostgreSqlFixture isolated)
    {
        var tenant = Guid.NewGuid();
        var actor = Guid.NewGuid();
        var action = "xml.scheduled." + Guid.NewGuid().ToString("N");
        var instant = DateTime.UtcNow;
        var storage = new CapturingXmlStorage();
        using var factory = isolated.CreateFactory(builder => builder.ConfigureTestServices(services =>
        {
            foreach (var worker in services.Where(descriptor => descriptor.ServiceType == typeof(IHostedService)
                         && descriptor.ImplementationType == typeof(ScheduledAuditExportBackgroundService)).ToArray())
            {
                services.Remove(worker);
            }
            services.RemoveAll<IAuditScheduledExportStorage>();
            services.AddSingleton<IAuditScheduledExportStorage>(storage);
        }));
        await using (var seed = factory.Services.CreateAsyncScope())
        {
            // This factory has its own migrated database; the parent fixture's seeded policies do not apply.
            await seed.ServiceProvider.GetRequiredService<PolicyDefinitionSeeder>().SeedAsync();
            var db = seed.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.AddRange(
                new AuditLog { TenantId = tenant, UserId = actor, ActionType = action, ResourceType = "Permission",
                    Description = "scheduled <record> & evidence", CreatedAt = instant, Metadata = "{\"proof\":true}" },
                new AuditLog { TenantId = Guid.NewGuid(), UserId = actor, ActionType = action, ResourceType = "Permission",
                    Description = "foreign scheduled row", CreatedAt = instant });
            await db.SaveChangesAsync();
        }
        using var client = factory.CreateClient();
        Authenticate(client, actor, tenant);
        using var created = await client.PostAsJsonAsync("/v1/admin/audit-logs/scheduled-exports", new CreateScheduledAuditExportRequest
        {
            TenantId = tenant, JobName = "XML evidence", CronExpression = "0 0 * * *", Timezone = "UTC",
            ExportFormat = ExportFormat.Xml, ActionType = action, StartDate = instant.AddSeconds(-1), EndDate = instant.AddSeconds(1)
        });
        var createdBody = await created.Content.ReadAsStringAsync();
        Assert.True(created.StatusCode == HttpStatusCode.Created, createdBody);
        using var createdJson = JsonDocument.Parse(createdBody);
        var exportId = createdJson.RootElement.GetProperty("id").GetGuid();
        await using (var forceDue = factory.Services.CreateAsyncScope())
        {
            await forceDue.ServiceProvider.GetRequiredService<ApplicationDbContext>().Set<ScheduledAuditExport>()
                .Where(export => export.Id == exportId)
                .ExecuteUpdateAsync(setters => setters.SetProperty(export => export.NextRunAt, instant.AddDays(-365)));
        }
        await using (var execute = factory.Services.CreateAsyncScope())
        {
            Assert.True(await execute.ServiceProvider.GetRequiredService<IScheduledAuditExportService>()
                .ProcessDueAsync(CancellationToken.None) >= 1);
        }
        await using var inspect = factory.Services.CreateAsyncScope();
        var service = inspect.ServiceProvider.GetRequiredService<IScheduledAuditExportService>();
        var history = Assert.Single(await service.GetHistoryAsync(exportId, tenant, CancellationToken.None));
        Assert.Equal(ExportStatus.Completed, history.Status);
        Assert.Equal(1, history.RecordCount);
        var stored = Assert.Single(storage.Files.Values, file => file.TenantId == tenant);
        Assert.Equal("application/xml; charset=utf-8", stored.ContentType);
        Assert.EndsWith(".xml", stored.FileName);
        Assert.Equal(Convert.ToHexString(SHA256.HashData(stored.Content)).ToLowerInvariant(), stored.Hash);
        var persisted = await inspect.ServiceProvider.GetRequiredService<ApplicationDbContext>().Set<AuditExportHistory>()
            .AsNoTracking().SingleAsync(row => row.Id == history.Id);
        Assert.Equal(stored.Hash, persisted.FileChecksum);
        Assert.Equal(stored.Content.Length, history.FileSizeBytes);
        var download = await service.OpenDownloadAsync(history.Id, tenant, CancellationToken.None);
        Assert.NotNull(download);
        Assert.Equal("application/xml; charset=utf-8", download.ContentType);
        await using var content = download.Content;
        var xml = await XDocument.LoadAsync(content, LoadOptions.None, CancellationToken.None);
        var row = Assert.Single(xml.Root!.Element("records")!.Elements("record"));
        Assert.Equal("scheduled <record> & evidence", row.Element("description")!.Value);
        Assert.Equal(tenant.ToString("D"), row.Element("actor")!.Element("tenantId")!.Value);
        using var metadata = JsonDocument.Parse(row.Element("metadata")!.Value);
        Assert.True(metadata.RootElement.GetProperty("proof").GetBoolean());
        Assert.Null(await service.OpenDownloadAsync(history.Id, Guid.NewGuid(), CancellationToken.None));
        using var httpDownload = await client.GetAsync($"/v1/admin/audit-logs/scheduled-export-history/{history.Id:D}/download?tenantId={tenant:D}");
        Assert.Equal(HttpStatusCode.OK, httpDownload.StatusCode);
        Assert.Equal("application/xml", httpDownload.Content.Headers.ContentType!.MediaType);
        Assert.Equal(stored.Content, await httpDownload.Content.ReadAsByteArrayAsync());
    }

    private sealed record StoredXml(Guid TenantId, byte[] Content, string Hash, string ContentType, string FileName);

    private sealed class CapturingXmlStorage : IAuditScheduledExportStorage
    {
        public Dictionary<string, StoredXml> Files { get; } = new();
        public async Task<string> StoreAsync(Guid tenantId, Stream content, string contentHash, string contentType,
            string fileName, CancellationToken cancellationToken)
        {
            await using var copy = new MemoryStream();
            await content.CopyToAsync(copy, cancellationToken);
            var key = Guid.NewGuid().ToString("N");
            Files.Add(key, new StoredXml(tenantId, copy.ToArray(), contentHash, contentType, fileName));
            return key;
        }
        public Task<Stream> OpenReadAsync(Guid tenantId, string storageReference, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var file = Files[storageReference];
            Assert.Equal(tenantId, file.TenantId);
            return Task.FromResult<Stream>(new MemoryStream(file.Content, writable: false));
        }
        public Task DeleteAsync(Guid tenantId, string storageReference, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Assert.Equal(tenantId, Files[storageReference].TenantId);
            Files.Remove(storageReference);
            return Task.CompletedTask;
        }
    }

    private static void Authenticate(HttpClient client, Guid actor, Guid tenant)
    {
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(ApiPostgreSqlTestAuthHandler.SchemeName, "authenticated");
        client.DefaultRequestHeaders.Add(ApiPostgreSqlTestAuthHandler.UserIdHeader, actor.ToString());
        client.DefaultRequestHeaders.Add(ApiPostgreSqlTestAuthHandler.TenantIdHeader, tenant.ToString());
        client.DefaultRequestHeaders.Add(ApiPostgreSqlTestAuthHandler.SystemAdminHeader, "true");
        client.DefaultRequestHeaders.Add("X-Tenant-Id", tenant.ToString());
    }

    private static async IAsyncEnumerable<AuditLog> EmptyRecords()
    {
        await Task.CompletedTask;
        yield break;
    }
}
