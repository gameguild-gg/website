using System.IO.Compression;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using GameGuild.API.Database;
using GameGuild.API.IntegrationTests.Infrastructure;
using GameGuild.Compliance.Audit;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Npgsql;

namespace GameGuild.API.IntegrationTests;

[Collection(ApiPostgreSqlCollection.Name)]
public sealed partial class CompliancePackagingPostgreSqlHttpTests(ApiPostgreSqlFixture fixture)
{
    private const string Route = "/v1/audit/compliance-packaging";
    private const string TemplateId = "iso27001-2022-evidence-v1";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };

    private WebApplicationFactory<Program> SignedFactory()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var options = new AuditSigningOptions { ActiveKeyId = "packaging-test" };
        options.Keys["packaging-test"] = new AuditSigningKeyOptions { PrivateKeyPem = key.ExportECPrivateKeyPem() };
        var signer = new EcdsaCryptographicSigningService(Options.Create(options));
        return fixture.CreateFactory(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<ICryptographicSigningService>();
            services.AddSingleton<ICryptographicSigningService>(signer);
        }));
    }

    private static HttpClient Admin(WebApplicationFactory<Program> factory, Guid tenant, Guid user)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(ApiPostgreSqlTestAuthHandler.SchemeName, "authenticated");
        client.DefaultRequestHeaders.Add(ApiPostgreSqlTestAuthHandler.UserIdHeader, user.ToString());
        client.DefaultRequestHeaders.Add(ApiPostgreSqlTestAuthHandler.TenantIdHeader, tenant.ToString());
        client.DefaultRequestHeaders.Add(ApiPostgreSqlTestAuthHandler.SystemAdminHeader, "true");
        client.DefaultRequestHeaders.Add("X-Tenant-Id", tenant.ToString());
        return client;
    }

    [Fact]
    public async Task Signed_audit_entries_remain_verifiable_after_PostgreSql_timestamp_round_trip()
    {
        using var factory = SignedFactory();
        var tenant = Guid.NewGuid();
        var instant = DateTime.UtcNow;
        instant = new DateTime(instant.Ticks - instant.Ticks % 10 + 7, DateTimeKind.Utc);
        await using var scope = factory.Services.CreateAsyncScope();
        var writer = scope.ServiceProvider.GetRequiredService<ITamperEvidentAuditService>();
        Guid id;
        SystemClock.SetProvider(new FixedTime(instant));
        try
        {
            var result = await writer.CreateAuditLogAsync(tenant, Guid.NewGuid(), "PrecisionProof", "Test",
                null, null, null, "{}", "High", "192.0.2.1", "test");
            Assert.True(result.IsSuccess);
            id = result.Value.Id;
        }
        finally { SystemClock.Reset(); }
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var persisted = await db.Set<TamperEvidentAuditLog>().AsNoTracking().SingleAsync(item => item.Id == id);
        Assert.True(scope.ServiceProvider.GetRequiredService<AuditChainEvidenceVerifier>().VerifyEntry(persisted));
        var chain = await writer.VerifyChainIntegrityAsync(tenant);
        Assert.True(chain.IsSuccess && chain.Value);
    }

    [Fact]
    public async Task Upload_review_collect_seal_download_and_cross_tenant_checks_use_real_PostgreSql()
    {
        using var factory = SignedFactory();
        var tenant = Guid.NewGuid();
        var user = Guid.NewGuid();
        var otherTenant = Guid.NewGuid();
        using var admin = Admin(factory, tenant, user);
        var template = (await admin.GetFromJsonAsync<List<ComplianceFrameworkTemplate>>(Route + "/templates", JsonOptions))!.Single(item => item.Id == TemplateId);
        Assert.Equal(93, template.Controls.Count);
        var start = DateTime.UtcNow.Date;
        await using (var seed = factory.Services.CreateAsyncScope())
        {
            var context = seed.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            context.Set<AuditLog>().AddRange(new[] { AuditCategory.Authentication, AuditCategory.Authorization, AuditCategory.Security }.Select(category => new AuditLog
            {
                TenantId = tenant, ActionType = "EvidenceSeed", ResourceType = "Test", CreatedAt = start,
                Category = category, RiskLevel = AuditRiskLevel.High, Description = "private-evidence-marker",
                Metadata = "{\"private\":\"private-evidence-marker\"}", IpAddress = "192.0.2.123", UserAgent = "private-evidence-marker"
            }));
            context.Set<AuditLog>().Add(new AuditLog { TenantId = otherTenant, ActionType = "OtherTenantOnly", ResourceType = "Test", CreatedAt = start });
            await context.SaveChangesAsync();
            var signed = await seed.ServiceProvider.GetRequiredService<ITamperEvidentAuditService>().CreateAuditLogAsync(
                tenant, user, "EvidenceSeed", "Test", null, null, null, "{\"private\":\"private-evidence-marker\"}", "High", "192.0.2.1", "private-evidence-marker");
            Assert.True(signed.IsSuccess);
        }
        var upload = Upload(template, start);
        var forged = JsonSerializer.SerializeToNode(upload, new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        forged["tenantId"] = otherTenant.ToString();
        forged["uploadedByUserId"] = Guid.NewGuid().ToString();
        var uploadResponse = await admin.PostAsJsonAsync(Route + "/documents", forged);
        Assert.True(uploadResponse.StatusCode == HttpStatusCode.OK, await uploadResponse.Content.ReadAsStringAsync());
        var document = (await uploadResponse.Content.ReadFromJsonAsync<ComplianceDocumentResponse>())!;
        Assert.Equal(user, document.UploadedByUserId);
        Assert.Equal(ComplianceDocumentReview.Pending, document.Review);
        Assert.DoesNotContain("ContentBase64", await uploadResponse.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);
        var review = new ReviewComplianceDocumentRequest { ExpectedRevision = 1, Decision = ComplianceDocumentReview.Approved, Notes = "Reviewed control implementation and evidence." };
        var approval = await admin.PostAsJsonAsync($"{Route}/documents/{document.Id}/review", review);
        Assert.True(approval.StatusCode == HttpStatusCode.OK, await approval.Content.ReadAsStringAsync());
        var approved = (await approval.Content.ReadFromJsonAsync<ComplianceDocumentResponse>())!;
        Assert.Equal(2, approved.Revision);
        Assert.Equal(user, approved.ReviewedByUserId);
        Assert.Equal(HttpStatusCode.Conflict, (await admin.PostAsJsonAsync($"{Route}/documents/{document.Id}/review", review)).StatusCode);
        var request = new CreateCompliancePackageRequest
        {
            Name = "Evidence acceptance", TemplateId = TemplateId, PeriodStartUtc = start,
            PeriodEndUtc = DateTime.UtcNow, DocumentIds = [document.Id]
        };
        var preparedResponse = await admin.PostAsJsonAsync(Route, request);
        var preparedText = await preparedResponse.Content.ReadAsStringAsync();
        Assert.True(preparedResponse.StatusCode == HttpStatusCode.OK, preparedText);
        var package = JsonSerializer.Deserialize<CompliancePackageResponse>(preparedText, JsonOptions)!;
        Assert.Equal(tenant, package.Manifest.TenantId);
        Assert.Equal(user, package.Summary.PreparedByUserId);
        Assert.True(package.Summary.ReadyForAuditorReview, JsonSerializer.Serialize(package.Manifest.Validation.Gaps));
        Assert.True((await admin.GetFromJsonAsync<ComplianceArtifactVerification>($"{Route}/{package.Summary.Id}/verification"))!.IsValid);
        var documents = (await admin.GetFromJsonAsync<List<ComplianceDocumentResponse>>(Route + "/documents?skip=0&take=1"))!;
        Assert.Equal(document.Id, Assert.Single(documents).Id);
        Assert.Empty((await admin.GetFromJsonAsync<List<ComplianceDocumentResponse>>(Route + "/documents?skip=1&take=1"))!);
        Assert.Equal(package.Summary.Id, Assert.Single((await admin.GetFromJsonAsync<List<CompliancePackageSummary>>(Route + "?take=1"))!).Id);
        var download = await admin.GetAsync($"{Route}/{package.Summary.Id}/download");
        Assert.Equal(HttpStatusCode.OK, download.StatusCode);
        Assert.Equal("application/zip", download.Content.Headers.ContentType!.MediaType);
        var zipBytes = await download.Content.ReadAsByteArrayAsync();
        Assert.Equal(package.Summary.ArtifactSha256, Convert.ToHexString(SHA256.HashData(zipBytes)).ToLowerInvariant());
        using (var zip = new ZipArchive(new MemoryStream(zipBytes), ZipArchiveMode.Read))
        {
            foreach (var entry in zip.Entries.Where(item => item.FullName.StartsWith("evidence/", StringComparison.Ordinal)))
            {
                using var reader = new StreamReader(entry.Open());
                var text = await reader.ReadToEndAsync();
                Assert.DoesNotContain("private-evidence-marker", text, StringComparison.Ordinal);
                Assert.DoesNotContain("192.0.2.123", text, StringComparison.Ordinal);
                Assert.DoesNotContain("OtherTenantOnly", text, StringComparison.Ordinal);
            }
            Assert.NotNull(zip.GetEntry("evidence/collection.json"));
        }
        // A later review does not rewrite the captured package or its approval state.
        var rejection = review with { ExpectedRevision = 2, Decision = ComplianceDocumentReview.Rejected, Notes = "Follow-up evidence superseded this assessment." };
        Assert.Equal(HttpStatusCode.OK, (await admin.PostAsJsonAsync($"{Route}/documents/{document.Id}/review", rejection)).StatusCode);
        Assert.Equal(zipBytes, await admin.GetByteArrayAsync($"{Route}/{package.Summary.Id}/download"));
        Assert.True((await admin.GetFromJsonAsync<CompliancePackageResponse>($"{Route}/{package.Summary.Id}", JsonOptions))!.Summary.ReadyForAuditorReview);
        using var other = Admin(factory, otherTenant, user);
        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync($"{Route}/{package.Summary.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync($"{Route}/{package.Summary.Id}/download")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.PostAsJsonAsync(Route, request)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync($"{Route}/documents/{document.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync("/api/audit/compliance-packaging/templates")).StatusCode);
        await using var verify = factory.Services.CreateAsyncScope();
        var db = verify.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.Equal(tenant, (await db.Set<ComplianceSealedPackage>().SingleAsync(item => item.Id == package.Summary.Id)).TenantId);
        Assert.True(await db.Set<AuditLog>().AnyAsync(item => item.TenantId == tenant && item.ActionType == "ComplianceEvidencePackageDownloaded"));
        var packageUpdate = await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync(
            $"""UPDATE "ComplianceSealedPackages" SET "Name" = 'changed' WHERE "Id" = {package.Summary.Id}"""));
        Assert.Equal(PostgresErrorCodes.CheckViolation, packageUpdate.SqlState);
        var contentUpdate = await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync(
            $"""UPDATE "ComplianceEvidenceDocuments" SET "Content" = {new byte[] { 1 }}, "Revision" = "Revision" + 1 WHERE "Id" = {document.Id}"""));
        Assert.Equal(PostgresErrorCodes.CheckViolation, contentUpdate.SqlState);
        var staleReview = await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync(
            $"""UPDATE "ComplianceEvidenceDocuments" SET "ReviewNotes" = 'unversioned' WHERE "Id" = {document.Id}"""));
        Assert.Equal(PostgresErrorCodes.CheckViolation, staleReview.SqlState);
        Assert.Equal(zipBytes, await admin.GetByteArrayAsync($"{Route}/{package.Summary.Id}/download"));
        using var wrongKeyFactory = SignedFactory();
        using var wrongKeyClient = Admin(wrongKeyFactory, tenant, user);
        Assert.False((await wrongKeyClient.GetFromJsonAsync<ComplianceArtifactVerification>($"{Route}/{package.Summary.Id}/verification"))!.IsValid);
        Assert.Equal(HttpStatusCode.Conflict, (await wrongKeyClient.GetAsync($"{Route}/{package.Summary.Id}")).StatusCode);
        var rejectedDownload = await wrongKeyClient.GetAsync($"{Route}/{package.Summary.Id}/download");
        Assert.Equal(HttpStatusCode.Conflict, rejectedDownload.StatusCode);
        Assert.Equal("application/problem+json", rejectedDownload.Content.Headers.ContentType!.MediaType);
    }

    [Fact]
    public async Task Integrity_collection_checks_the_interval_predecessor_and_reports_changed_or_oversized_entries()
    {
        using var factory = SignedFactory();
        var tenant = Guid.NewGuid();
        var user = Guid.NewGuid();
        var firstAt = DateTime.UtcNow.Date.AddDays(-2);
        var secondAt = firstAt.AddDays(1);
        var thirdAt = secondAt.AddHours(1);
        await using var scope = factory.Services.CreateAsyncScope();
        var writer = scope.ServiceProvider.GetRequiredService<ITamperEvidentAuditService>();
        var ids = new List<Guid>();
        foreach (var instant in new[] { firstAt, secondAt, thirdAt })
        {
            SystemClock.SetProvider(new FixedTime(instant));
            try
            {
                var result = await writer.CreateAuditLogAsync(tenant, user, "IntervalProof", "Test",
                    null, null, null, "{}", "High", "192.0.2.1", "test");
                Assert.True(result.IsSuccess);
                ids.Add(result.Value.Id);
            }
            finally { SystemClock.Reset(); }
        }
        var collector = scope.ServiceProvider.GetRequiredService<IComplianceEvidenceDataSource>();
        var interval = Assert.Single(await collector.CaptureAsync(tenant, secondAt, thirdAt, [ComplianceEvidenceKind.Integrity], default));
        Assert.Equal(2, interval.RecordCount);
        Assert.Empty(interval.ValidationErrors);
        using (var data = JsonDocument.Parse(interval.Content))
        {
            Assert.Equal(new[] { 2L, 3L }, data.RootElement.EnumerateArray().Select(row => row.GetProperty("sequenceNumber").GetInt64()));
            Assert.All(data.RootElement.EnumerateArray(), row => Assert.True(row.GetProperty("canonicalContentAndSignatureVerified").GetBoolean()));
        }
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await db.Database.ExecuteSqlInterpolatedAsync($"""UPDATE "TamperEvidentAuditLogs" SET "Changes" = 'changed' WHERE "Id" = {ids[0]}""");
        var missingAnchor = Assert.Single(await collector.CaptureAsync(tenant, secondAt, thirdAt, [ComplianceEvidenceKind.Integrity], default));
        Assert.Contains(missingAnchor.ValidationErrors, error => error.Contains("predecessor", StringComparison.Ordinal));
        await db.Database.ExecuteSqlInterpolatedAsync($"""UPDATE "TamperEvidentAuditLogs" SET "Changes" = {new string('x', 262145)} WHERE "Id" = {ids[1]}""");
        var truncated = Assert.Single(await collector.CaptureAsync(tenant, secondAt, thirdAt, [ComplianceEvidenceKind.Integrity], default));
        Assert.Equal(1, truncated.RecordCount);
        Assert.Contains(truncated.ValidationErrors, error => error.Contains("truncated", StringComparison.Ordinal));
        await db.Database.ExecuteSqlInterpolatedAsync($"""UPDATE "TamperEvidentAuditLogs" SET "DigitalSignature" = 'invalid' WHERE "Id" = {ids[2]}""");
        var tampered = Assert.Single(await collector.CaptureAsync(tenant, secondAt, thirdAt, [ComplianceEvidenceKind.Integrity], default));
        Assert.Contains(tampered.ValidationErrors, error => error.Contains("signature failed", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Missing_private_signing_keys_return_service_unavailable_and_never_persist_a_package()
    {
        using var factory = fixture.CreateFactory(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<ICryptographicSigningService>();
            services.AddSingleton<ICryptographicSigningService>(new EcdsaCryptographicSigningService(Options.Create(new AuditSigningOptions())));
        }));
        var tenant = Guid.NewGuid();
        using var admin = Admin(factory, tenant, Guid.NewGuid());
        var response = await admin.PostAsJsonAsync(Route, new CreateCompliancePackageRequest
        {
            Name = "Signing unavailable", TemplateId = TemplateId, PeriodStartUtc = DateTime.UtcNow.Date,
            PeriodEndUtc = DateTime.UtcNow
        });
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        await using var scope = factory.Services.CreateAsyncScope();
        Assert.False(await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Set<ComplianceSealedPackage>()
            .IgnoreQueryFilters().AnyAsync(item => item.TenantId == tenant));
    }

    [Fact]
    public async Task Anonymous_ordinary_users_invalid_inputs_and_concurrent_review_are_enforced_over_HTTP()
    {
        using var factory = SignedFactory();
        using var anonymous = factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync(Route)).StatusCode);
        using var ordinary = fixture.CreateAuthenticatedClient(Guid.NewGuid(), Guid.NewGuid());
        Assert.Equal(HttpStatusCode.Forbidden, (await ordinary.GetAsync(Route)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await ordinary.PostAsJsonAsync(Route, new CreateCompliancePackageRequest
            { Name = "Denied", TemplateId = TemplateId, PeriodStartUtc = DateTime.UtcNow.Date, PeriodEndUtc = DateTime.UtcNow })).StatusCode);
        var tenant = Guid.NewGuid();
        using var first = Admin(factory, tenant, Guid.NewGuid());
        using var second = Admin(factory, tenant, Guid.NewGuid());
        var template = (await first.GetFromJsonAsync<List<ComplianceFrameworkTemplate>>(Route + "/templates", JsonOptions))!.Single(item => item.Id == TemplateId);
        Assert.Equal(HttpStatusCode.BadRequest, (await first.GetAsync(Route + "?take=101")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await first.PostAsJsonAsync(Route + "/documents", Upload(template, DateTime.UtcNow.Date) with { ContentBase64 = "invalid" })).StatusCode);
        var upload = await first.PostAsJsonAsync(Route + "/documents", Upload(template, DateTime.UtcNow.Date));
        Assert.Equal(HttpStatusCode.OK, upload.StatusCode);
        var document = (await upload.Content.ReadFromJsonAsync<ComplianceDocumentResponse>())!;
        var request = new ReviewComplianceDocumentRequest { ExpectedRevision = 1, Decision = ComplianceDocumentReview.Approved, Notes = "Reviewed evidence." };
        var responses = await Task.WhenAll(first.PostAsJsonAsync($"{Route}/documents/{document.Id}/review", request),
            second.PostAsJsonAsync($"{Route}/documents/{document.Id}/review", request));
        Assert.Equal(1, responses.Count(item => item.StatusCode == HttpStatusCode.OK));
        Assert.Equal(1, responses.Count(item => item.StatusCode == HttpStatusCode.Conflict));
    }

    private static UploadComplianceDocumentRequest Upload(ComplianceFrameworkTemplate template, DateTime start) => new()
    {
        TemplateId = template.Id, Name = "Reviewed control assessment", Type = "control-assessment",
        MediaType = "application/json", SourceUri = "https://example.com/approved-evidence", ValidFromUtc = start.AddDays(-1),
        ValidUntilUtc = start.AddDays(30), ControlIds = template.Controls.Select(item => item.Id).ToList(),
        ContentBase64 = Convert.ToBase64String(JsonSerializer.SerializeToUtf8Bytes(new
        {
            frameworkVersion = template.Version, assessmentStatus = "satisfactory", owner = "Test assessor",
            controlAssessments = template.Controls.ToDictionary(item => item.Id, item => new
            {
                owner = "Test assessor", assessmentStatus = "satisfactory",
                controlImplementation = "Test implementation evidence for " + item.Id,
                effectivenessEvidence = "Test review evidence for " + item.Id
            })
        }))
    };

    [Fact]
    public async Task OpenApi_exposes_both_routes_typed_metadata_UTC_inputs_and_the_ZIP_download_contract()
    {
        using var factory = fixture.CreateFactory(builder => builder.UseEnvironment("Development"));
        using var client = factory.CreateClient();
        var response = await client.GetAsync("/swagger/v1/swagger.json");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(json);
        var paths = document.RootElement.GetProperty("paths");
        foreach (var prefix in new[] { Route, "/api/audit/compliance-packaging" })
        {
            Assert.True(paths.GetProperty(prefix).TryGetProperty("post", out _));
            Assert.True(paths.GetProperty(prefix + "/documents").TryGetProperty("post", out _));
            Assert.True(paths.GetProperty(prefix + "/documents/{id}/review").TryGetProperty("post", out _));
            var download = paths.GetProperty(prefix + "/{id}/download").GetProperty("get");
            var security = download.TryGetProperty("security", out var operationSecurity)
                ? operationSecurity : document.RootElement.GetProperty("security");
            Assert.True(security.GetArrayLength() > 0);
            Assert.False(download.TryGetProperty("x-allow-anonymous", out var anonymous) && anonymous.GetBoolean());
            Assert.True(download.GetProperty("responses").GetProperty("200").GetProperty("content").TryGetProperty("application/zip", out _));
        }
        var pagination = paths.GetProperty(Route).GetProperty("get").GetProperty("parameters").EnumerateArray()
            .ToDictionary(parameter => parameter.GetProperty("name").GetString()!);
        Assert.Equal(100, pagination["take"].GetProperty("schema").GetProperty("maximum").GetInt32());
        var schemas = document.RootElement.GetProperty("components").GetProperty("schemas");
        var upload = schemas.GetProperty("Compliance_Audit_" + nameof(UploadComplianceDocumentRequest));
        Assert.Contains("templateId", upload.GetProperty("required").EnumerateArray().Select(item => item.GetString()));
        Assert.False(upload.GetProperty("properties").TryGetProperty("tenantId", out _));
        Assert.False(upload.GetProperty("properties").TryGetProperty("uploadedByUserId", out _));
        var metadata = schemas.GetProperty("Compliance_Audit_" + nameof(ComplianceDocumentResponse));
        Assert.False(metadata.GetProperty("properties").TryGetProperty("content", out _));
        Assert.Equal("string", schemas.GetProperty("Compliance_Audit_" + nameof(ComplianceDocumentReview)).GetProperty("type").GetString());
        var capturePath = Environment.GetEnvironmentVariable("GAMEGUILD_COMPLIANCE_OPENAPI_CAPTURE");
        if (!string.IsNullOrWhiteSpace(capturePath))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(capturePath))!);
            await File.WriteAllTextAsync(capturePath, json);
        }
    }

    [Fact]
    public async Task Compliance_packaging_migrations_match_the_model_table_and_column_inventory()
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var ownedTableNames = new HashSet<string>(StringComparer.Ordinal)
        {
            "ComplianceEvidenceDocuments", "ComplianceSealedPackages"
        };
        var tables = db.Model.GetRelationalModel().Tables
            .Where(table => ownedTableNames.Contains(table.Name)).ToArray();
        Assert.Equal(ownedTableNames.Count, tables.Length);
        var ownedTables = tables.Select(table => (Schema: table.Schema ?? "public", Table: table.Name)).ToHashSet();
        var actual = new HashSet<(string Schema, string Table, string Column)>();
        await db.Database.OpenConnectionAsync();
        await using var query = db.Database.GetDbConnection().CreateCommand();
        query.CommandText = "SELECT table_schema, table_name, column_name FROM information_schema.columns WHERE table_schema NOT IN ('pg_catalog', 'information_schema')";
        await using (var reader = await query.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
            {
                var schema = reader.GetString(0);
                var table = reader.GetString(1);
                if (ownedTables.Contains((schema, table)))
                {
                    actual.Add((schema, table, reader.GetString(2)));
                }
            }
        }
        var expected = tables.SelectMany(table => table.Columns.Select(column =>
            (Schema: table.Schema ?? "public", Table: table.Name, Column: column.Name))).ToHashSet();
        Assert.True(expected.SetEquals(actual),
            "Missing migrated columns: " + string.Join(", ", expected.Except(actual).Order()) +
            "; unexpected database columns: " + string.Join(", ", actual.Except(expected).Order()));
    }
    private sealed class FixedTime(DateTime instant) : TimeProvider { public override DateTimeOffset GetUtcNow() => instant; }
}
