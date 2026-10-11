using System.Collections.Concurrent;
using System.Data.Common;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using GameGuild.API.Database;
using GameGuild.API.IntegrationTests.Infrastructure;
using GameGuild.Compliance.Audit;
using GameGuild.Identity.Authorization;
using GameGuild.Identity.Context.Actors;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moq;

namespace GameGuild.API.IntegrationTests;

[Collection(ApiPostgreSqlCollection.Name)]
public sealed class AuditReplayPostgreSqlHttpTests(ApiPostgreSqlFixture fixture) : IAsyncLifetime
{
    private const string Route = "/v1/admin/audit-logs/replay/snapshots";
    private const string Alias = "/api/audit/replay/snapshots";

    public async Task InitializeAsync()
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<PolicyDefinitionSeeder>().SeedAsync();
    }
    public Task DisposeAsync() => Task.CompletedTask;

    [Theory]
    [InlineData(Route)] [InlineData(Alias)]
    public async Task CapturesOneSafeRelationalSelectionInAuthenticatedScope(string route)
    {
        var tenant = Guid.NewGuid();
        var actor = Guid.NewGuid();
        var other = Guid.NewGuid();
        var start = DateTime.UtcNow.Date.AddDays(-1);
        var end = start.AddSeconds(1);
        var first = Event(tenant, start);
        first.Description = "PRIVATE_DESCRIPTION";
        first.Metadata = "PRIVATE_METADATA";
        first.IpAddress = "192.0.2.1";
        first.UserAgent = "PRIVATE_AGENT";
        first.ErrorMessage = "PRIVATE_ERROR";
        var last = Event(tenant, end);
        var deleted = Event(tenant, start); deleted.DeletedAt = DateTime.UtcNow;
        await using (var seed = fixture.Factory.Services.CreateAsyncScope())
        {
            var context = seed.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            context.Set<AuditLog>().AddRange(first, last, deleted, Event(other, start),
                Event(null, start), Event(tenant, start.AddTicks(-10)), Event(tenant, end.AddTicks(10)));
            await context.SaveChangesAsync();
        }
        var probe = new AuditSelectionProbe();
        using var factory = fixture.CreateFactory(builder => builder.ConfigureTestServices(services =>
            services.ConfigureDbContext<ApplicationDbContext>(options => options.AddInterceptors(probe))));
        using var client = factory.CreateClient();
        Authenticate(client, actor, tenant);
        using var response = await client.PostAsJsonAsync(route, new
        {
            start, end, tenantId = other, requestedByUserId = Guid.NewGuid()
        });
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.OK, body);
        Assert.Equal("application/json", response.Content.Headers.ContentType!.MediaType);
        Assert.Equal("utf-8", response.Content.Headers.ContentType.CharSet);
        Assert.True(response.Headers.CacheControl!.NoStore);
        Assert.True(response.Headers.CacheControl.Private);
        Assert.Equal("nosniff", response.Headers.GetValues("X-Content-Type-Options").Single());
        var digest = response.Headers.GetValues("X-Audit-Snapshot-SHA256").Single();
        Assert.True(AuditReplaySnapshot.MatchesDigest(body, digest));
        Assert.DoesNotContain("PRIVATE_", body);
        Assert.DoesNotContain("192.0.2.1", body);
        using var json = JsonDocument.Parse(body);
        Assert.Equal(tenant.ToString("D"), json.RootElement.GetProperty("tenantId").GetString());
        Assert.Equal(2, json.RootElement.GetProperty("totalCount").GetInt32());
        Assert.Equal([first.Id.ToString("D"), last.Id.ToString("D")],
            json.RootElement.GetProperty("events").EnumerateArray().Select(item => item.GetProperty("id").GetString()).ToArray());
        var query = Assert.Single(probe.Reads);
        Assert.DoesNotContain("\"Description\"", query);
        Assert.DoesNotContain("\"Metadata\"", query);
        Assert.DoesNotContain("\"IpAddress\"", query);
        Assert.DoesNotContain("\"UserAgent\"", query);
        Assert.DoesNotContain("\"ErrorMessage\"", query);

        await using var verify = fixture.Factory.Services.CreateAsyncScope();
        var database = verify.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var access = await database.Set<AuditLog>().Where(item => item.UserId == actor &&
            item.ActionType.StartsWith("AuditReplayCapture")).OrderBy(item => item.CreatedAt).ToListAsync();
        Assert.Equal(["AuditReplayCaptureRequested", "AuditReplayCaptureCompleted"], access.Select(item => item.ActionType));
        Assert.All(access, item => Assert.Equal(tenant, item.TenantId));
        Assert.DoesNotContain("PRIVATE_", JsonSerializer.Serialize(access));
    }

    [Theory]
    [InlineData(Route, false)] [InlineData(Alias, false)]
    [InlineData(Route, true)] [InlineData(Alias, true)]
    public async Task RequiresAuthenticatedSystemAdministrator(string route, bool member)
    {
        using var client = member ? fixture.CreateAuthenticatedClient(Guid.NewGuid(), Guid.NewGuid())
            : fixture.Factory.CreateClient();
        using var response = await client.PostAsJsonAsync(route, Range());
        Assert.Equal(member ? HttpStatusCode.Forbidden : HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.False(response.Headers.Contains("X-Audit-Snapshot-SHA256"));
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)]
    public async Task RejectsMissingRangeBeforePersistingCaptureAccess(int scenario)
    {
        object? request = scenario switch
        {
            0 => null, 1 => new { }, 2 => new { start = DateTimeOffset.UtcNow.AddDays(-1) },
            3 => new { end = DateTimeOffset.UtcNow.AddDays(-1) },
            _ => throw new ArgumentOutOfRangeException(nameof(scenario))
        };
        var actor = Guid.NewGuid();
        using var client = fixture.CreateAuthenticatedClient(actor, Guid.NewGuid(), true);
        using var response = await client.PostAsJsonAsync(Route, request);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.False(response.Headers.Contains("X-Audit-Snapshot-SHA256"));
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        Assert.False(await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Set<AuditLog>()
            .AnyAsync(item => item.UserId == actor && item.ActionType.StartsWith("AuditReplayCapture")));
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task RejectsInvalidRangesWithoutReturningEvidence(bool future)
    {
        using var client = fixture.CreateAuthenticatedClient(Guid.NewGuid(), Guid.NewGuid(), true);
        var start = DateTimeOffset.UtcNow.AddDays(-2);
        var end = future ? DateTimeOffset.UtcNow.AddDays(2) : start.AddTicks(-1);
        using var response = await client.PostAsJsonAsync(Route, new { start, end });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.False(response.Headers.Contains("X-Audit-Snapshot-SHA256"));
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(400, json.RootElement.GetProperty("status").GetInt32());
    }

    [Fact]
    public async Task RequiredAuditOutageDoesNotReleaseSnapshot()
    {
        var audit = new Mock<IAuditService>();
        audit.Setup(value => value.TryLogAsync(It.IsAny<CreateAuditLogRequest>())).ReturnsAsync(false);
        using var factory = fixture.CreateFactory(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IAuditService>();
            services.AddSingleton(audit.Object);
        }));
        using var client = factory.CreateClient();
        Authenticate(client, Guid.NewGuid(), Guid.NewGuid());
        using var response = await client.PostAsJsonAsync(Route, Range());
        var body = await response.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.False(response.Headers.Contains("X-Audit-Snapshot-SHA256"));
        Assert.DoesNotContain("events", body, StringComparison.OrdinalIgnoreCase);
        audit.Verify(value => value.TryLogAsync(It.IsAny<CreateAuditLogRequest>()), Times.Once);
    }

    [Fact]
    public async Task RelationalGlobalCaptureUsesNullOnlyScopeAndRequiredPersistedAccessAudit()
    {
        var actor = Guid.NewGuid();
        var start = new DateTime(2001, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddTicks(Random.Shared.Next(1, 1_000_000) * 10L);
        var global = Event(null, start);
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        context.Set<AuditLog>().AddRange(global, Event(Guid.NewGuid(), start));
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();
        var actors = scope.ServiceProvider.GetRequiredService<IActorContextAccessor>();
        actors.SetActorContext(new ActorContext
        {
            ActorKind = ActorKind.User, SubjectId = actor.ToString("D"), IsAuthenticated = true,
            TenantId = null, Roles = new HashSet<string> { "SystemAdmin" }, Permissions = new HashSet<string>()
        });
        try
        {
            var snapshot = await scope.ServiceProvider.GetRequiredService<IAuditReplayCaptureService>()
                .CaptureAsync(new(start, start), default);
            Assert.Null(snapshot.TenantId);
            Assert.Equal(global.Id.ToString("D"), Assert.Single(snapshot.Events).Id);
            Assert.True(AuditReplaySnapshot.MatchesDigest(snapshot.Json, snapshot.Sha256));
            Assert.Empty(context.ChangeTracker.Entries<AuditLog>());
            var access = await context.Set<AuditLog>().AsNoTracking().Where(item => item.UserId == actor &&
                item.ActionType.StartsWith("AuditReplayCapture")).ToListAsync();
            Assert.Equal(2, access.Count);
            Assert.All(access, item => Assert.Null(item.TenantId));
        }
        finally { actors.ClearActorContext(); }
    }

    [Theory]
    [InlineData(Route, false)]
    [InlineData(Route, true)]
    [InlineData(Alias, false)]
    [InlineData(Alias, true)]
    public async Task FractionalRangeDoesNotExpandAtPostgreSqlParameterPrecision(string route, bool emptyRange)
    {
        var tenant = Guid.NewGuid();
        var actor = Guid.NewGuid();
        var origin = DateTime.UtcNow.Date.AddDays(-3);
        var lower = Event(tenant, origin);
        var inside = Event(tenant, origin.AddTicks(10));
        var upper = Event(tenant, origin.AddTicks(20));
        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            context.Set<AuditLog>().AddRange(lower, inside, upper);
            await context.SaveChangesAsync();
        }
        using var client = fixture.Factory.CreateClient();
        Authenticate(client, actor, tenant);
        var start = new DateTimeOffset(origin.AddTicks(1));
        var end = new DateTimeOffset(origin.AddTicks(emptyRange ? 9 : 19));
        using var response = await client.PostAsJsonAsync(route, new { start, end });
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.OK, body);
        using var json = JsonDocument.Parse(body);
        Assert.Equal(emptyRange ? 0 : 1, json.RootElement.GetProperty("totalCount").GetInt32());
        var events = json.RootElement.GetProperty("events").EnumerateArray().ToArray();
        if (emptyRange)
        {
            Assert.Empty(events);
        }
        else
        {
            Assert.Equal(inside.Id.ToString("D"), Assert.Single(events).GetProperty("id").GetString());
        }
        Assert.Equal(start, DateTimeOffset.Parse(json.RootElement.GetProperty("startUtc").GetString()!, System.Globalization.CultureInfo.InvariantCulture));
        Assert.Equal(end, DateTimeOffset.Parse(json.RootElement.GetProperty("endUtc").GetString()!, System.Globalization.CultureInfo.InvariantCulture));
        Assert.True(AuditReplaySnapshot.MatchesDigest(body, response.Headers.GetValues("X-Audit-Snapshot-SHA256").Single()));
    }

    [Fact]
    public async Task OpenApiDocumentsRequiredRangeAndSafeReplayResponse()
    {
        using var factory = fixture.CreateFactory(builder => builder.UseEnvironment("Development"));
        using var client = factory.CreateClient();
        using var response = await client.GetAsync("/swagger/v1/swagger.json");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var paths = json.RootElement.GetProperty("paths");
        Assert.True(paths.GetProperty(Route).TryGetProperty("post", out _));
        Assert.True(paths.GetProperty(Alias).TryGetProperty("post", out _));
        var schemas = json.RootElement.GetProperty("components").GetProperty("schemas");
        Assert.Equal(["end", "start"], schemas.GetProperty("CaptureAuditReplayRequest").GetProperty("required")
            .EnumerateArray().Select(item => item.GetString()).Order().ToArray());
        var responseSchema = schemas.GetProperty("AuditReplaySnapshotResponse").GetProperty("properties");
        Assert.True(responseSchema.TryGetProperty("events", out _));
        Assert.False(responseSchema.TryGetProperty("json", out _));
        Assert.False(responseSchema.TryGetProperty("description", out _));
    }


    [Theory]
    [InlineData(Route, true, false)]
    [InlineData(Alias, true, false)]
    [InlineData(Route, false, false)]
    [InlineData(Alias, false, false)]
    [InlineData(Route, true, true)]
    [InlineData(Alias, true, true)]
    [InlineData(Route, false, true)]
    [InlineData(Alias, false, true)]
    public async Task CorsPreservesOriginRestrictionsAndExposesCaptureDigest(string route, bool allowed, bool preflight)
    {
        using var factory = fixture.CreateFactory(builder => builder.ConfigureAppConfiguration((_, config) =>
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Cors:AllowedOrigins:0"] = "https://audit.example",
                ["Cors:AllowedMethods:0"] = "POST",
                ["Cors:AllowedHeaders:0"] = "Authorization",
                ["PresentationLayer:Cors:AllowedOrigins:0"] = "https://audit.example",
                ["PresentationLayer:Cors:AllowedMethods:0"] = "POST",
                ["PresentationLayer:Cors:AllowedHeaders:0"] = "Authorization"
            })));
        using var client = factory.CreateClient();
        Authenticate(client, Guid.NewGuid(), Guid.NewGuid());
        using var request = new HttpRequestMessage(preflight ? HttpMethod.Options : HttpMethod.Post, route);
        request.Headers.Add("Origin", allowed ? "https://audit.example" : "https://untrusted.example");
        if (preflight)
        {
            request.Headers.Add("Access-Control-Request-Method", "POST");
            request.Headers.Add("Access-Control-Request-Headers", "Authorization");
        }
        else
        {
            request.Content = JsonContent.Create(Range());
        }
        using var response = await client.SendAsync(request);
        Assert.Equal(preflight ? HttpStatusCode.NoContent : HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(allowed, response.Headers.Contains("Access-Control-Allow-Origin"));
        Assert.False(response.Headers.Contains("Access-Control-Allow-Credentials"));
        if (allowed)
        {
            Assert.Equal("https://audit.example", response.Headers.GetValues("Access-Control-Allow-Origin").Single());
            if (preflight)
            {
                Assert.Equal("POST", response.Headers.GetValues("Access-Control-Allow-Methods").Single());
                Assert.Equal("Authorization", response.Headers.GetValues("Access-Control-Allow-Headers").Single());
            }
            else
            {
                Assert.Equal("X-Audit-Snapshot-SHA256", response.Headers.GetValues("Access-Control-Expose-Headers").Single());
                var json = await response.Content.ReadAsStringAsync();
                Assert.True(AuditReplaySnapshot.MatchesDigest(json, response.Headers.GetValues("X-Audit-Snapshot-SHA256").Single()));
            }
        }
        else
        {
            Assert.False(response.Headers.Contains("Access-Control-Expose-Headers"));
            Assert.False(response.Headers.Contains("Access-Control-Allow-Methods"));
            Assert.False(response.Headers.Contains("Access-Control-Allow-Headers"));
        }
    }

    private static object Range() => new { start = DateTimeOffset.UtcNow.AddDays(-2), end = DateTimeOffset.UtcNow.AddDays(-1) };
    private static AuditLog Event(Guid? tenant, DateTime timestamp) => new()
    {
        TenantId = tenant, CreatedAt = timestamp, UpdatedAt = timestamp,
        ActionType = "ReplayHttp", ResourceType = "Document", Success = true
    };
    private static void Authenticate(HttpClient client, Guid actor, Guid tenant)
    {
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(ApiPostgreSqlTestAuthHandler.SchemeName, "authenticated");
        client.DefaultRequestHeaders.Add(ApiPostgreSqlTestAuthHandler.UserIdHeader, actor.ToString("D"));
        client.DefaultRequestHeaders.Add(ApiPostgreSqlTestAuthHandler.TenantIdHeader, tenant.ToString("D"));
        client.DefaultRequestHeaders.Add("X-Tenant-Id", tenant.ToString("D"));
        client.DefaultRequestHeaders.Add(ApiPostgreSqlTestAuthHandler.SystemAdminHeader, "true");
    }
    private sealed class AuditSelectionProbe : DbCommandInterceptor
    {
        public ConcurrentQueue<string> Reads { get; } = new();
        private void Record(DbCommand command)
        {
            if (command.CommandText.StartsWith("SELECT", StringComparison.Ordinal) &&
                command.CommandText.Contains("\"AuditLogs\"", StringComparison.Ordinal))
            {
                Reads.Enqueue(command.CommandText);
            }
        }
        public override InterceptionResult<DbDataReader> ReaderExecuting(DbCommand command,
            CommandEventData eventData, InterceptionResult<DbDataReader> result)
        {
            ArgumentNullException.ThrowIfNull(eventData);
            Record(command);
            return result;
        }
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command,
            CommandEventData eventData, InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(eventData);
            cancellationToken.ThrowIfCancellationRequested();
            Record(command);
            return ValueTask.FromResult(result);
        }
    }
}
