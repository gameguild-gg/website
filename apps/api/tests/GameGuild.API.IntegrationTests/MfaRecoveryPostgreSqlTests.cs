using System.Security.Cryptography;
using System.Text;
using GameGuild.API.Database;
using GameGuild.API.IntegrationTests.Infrastructure;
using GameGuild.Identity.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Microsoft.AspNetCore.Http;
using GameGuild.Configuration.ApplicationLayer;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using GameGuild.Identity.Tenants;
using GameGuild.Identity.Users;

namespace GameGuild.API.IntegrationTests;

[Collection(ApiPostgreSqlCollection.Name)]
public sealed class MfaRecoveryPostgreSqlTests(ApiPostgreSqlFixture fixture)
{
    [Fact]
    public async Task TwoIndependentContextsCannotBothConsumeSameCode()
    {
        const string syntheticCode = "ABCDEFGH2345";
        var userId = Guid.NewGuid();
        using var firstScope = fixture.Factory.Services.CreateScope();
        using var secondScope = fixture.Factory.Services.CreateScope();
        var first = firstScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var second = secondScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        first.Set<UserMfaConfiguration>().Add(new UserMfaConfiguration
        {
            UserId = userId, IsEnabled = true, IsSetupComplete = true,
            BackupCodes = Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(syntheticCode)))
        });
        await first.SaveChangesAsync();
        await second.Set<UserMfaConfiguration>().SingleAsync(value => value.UserId == userId);
        var tracking = new Mock<IMfaAttemptTrackingService>().Object;
        var firstService = new BackupCodeMfaService(NullLogger<BackupCodeMfaService>.Instance,
            new UserMfaConfigurationRepository(first), tracking);
        var secondService = new BackupCodeMfaService(NullLogger<BackupCodeMfaService>.Instance,
            new UserMfaConfigurationRepository(second), tracking);
        Assert.True(await firstService.VerifyBackupCodeAsync(userId, syntheticCode));
        Assert.False(await secondService.VerifyBackupCodeAsync(userId, syntheticCode));
        await second.Entry(await second.Set<UserMfaConfiguration>().SingleAsync(value => value.UserId == userId)).ReloadAsync();
        Assert.True(string.IsNullOrEmpty((await second.Set<UserMfaConfiguration>().SingleAsync(value => value.UserId == userId)).BackupCodes));
    }

    [Fact]
    public async Task SimultaneousVerificationReturnsExactlyOneCommittedSuccess()
    {
        var userId = await SeedLegacyCodeAsync();
        var barrier = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var arrived = 0;
        var scopes = Enumerable.Range(0, 2).Select(_ => fixture.Factory.Services.CreateScope()).ToArray();
        try
        {
            var tasks = scopes.Select(async scope =>
            {
                var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var real = new UserMfaConfigurationRepository(context);
                var repository = new Mock<IUserMfaConfigurationRepository>();
                repository.Setup(value => value.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
                    .Returns(async (Guid id, CancellationToken ct) =>
                    {
                        var row = await real.GetByUserIdAsync(id, ct);
                        if (Interlocked.Increment(ref arrived) == 2) { barrier.TrySetResult(); }
                        await barrier.Task.WaitAsync(TimeSpan.FromSeconds(30), ct);
                        return row;
                    });
                repository.Setup(value => value.UpdateAsync(It.IsAny<UserMfaConfiguration>(), It.IsAny<CancellationToken>()))
                    .Returns((UserMfaConfiguration row, CancellationToken ct) => real.UpdateAsync(row, ct));
                var service = CreateBackupService(repository.Object);
                return await service.VerifyBackupCodeAsync(userId, "ABCDEFGH2345");
            });
            Assert.Single(await Task.WhenAll(tasks), success => success);
            using var check = fixture.Factory.Services.CreateScope();
            var saved = await check.ServiceProvider.GetRequiredService<ApplicationDbContext>()
                .Set<UserMfaConfiguration>().SingleAsync(value => value.UserId == userId);
            Assert.True(string.IsNullOrEmpty(saved.BackupCodes));
        }
        finally { foreach (var scope in scopes) { scope.Dispose(); } }
    }

    [Fact]
    public async Task StaleFailedAttemptCannotResurrectConsumedCode()
    {
        var userId = await SeedLegacyCodeAsync();
        using var firstScope = fixture.Factory.Services.CreateScope();
        using var staleScope = fixture.Factory.Services.CreateScope();
        var first = new UserMfaConfigurationRepository(firstScope.ServiceProvider.GetRequiredService<ApplicationDbContext>());
        var staleContext = staleScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var stale = new UserMfaConfigurationRepository(staleContext);
        var oldState = (await stale.GetByUserIdAsync(userId))!;
        Assert.True(await CreateBackupService(first).VerifyBackupCodeAsync(userId, "ABCDEFGH2345"));
        await CreateTracking(stale).RecordFailedMfaAttemptAsync(oldState, MfaMethod.BackupCode, "Synthetic invalid attempt", null);
        await staleContext.Entry(oldState).ReloadAsync();
        Assert.True(string.IsNullOrEmpty(oldState.BackupCodes));
        Assert.Equal(1, oldState.FailedAttempts);
    }

    [Fact]
    public async Task FiveFailuresFromStaleContextsAccumulateAndBlockCorrectCode()
    {
        var userId = await SeedLegacyCodeAsync();
        var scopes = Enumerable.Range(0, 5).Select(_ => fixture.Factory.Services.CreateScope()).ToArray();
        try
        {
            var rows = await Task.WhenAll(scopes.Select(scope =>
                new UserMfaConfigurationRepository(scope.ServiceProvider.GetRequiredService<ApplicationDbContext>()).GetByUserIdAsync(userId)));
            var started = DateTime.UtcNow;
            await Task.WhenAll(scopes.Select((scope, index) => CreateTracking(new UserMfaConfigurationRepository(
                scope.ServiceProvider.GetRequiredService<ApplicationDbContext>())).RecordFailedMfaAttemptAsync(
                    rows[index]!, MfaMethod.BackupCode, "Synthetic invalid attempt", null)));
            using var check = fixture.Factory.Services.CreateScope();
            var repository = new UserMfaConfigurationRepository(check.ServiceProvider.GetRequiredService<ApplicationDbContext>());
            var saved = (await repository.GetByUserIdAsync(userId))!;
            Assert.Equal(5, saved.FailedAttempts);
            Assert.InRange(saved.LockedOutUntil!.Value, started.AddMinutes(15), DateTime.UtcNow.AddMinutes(15));
            var service = new BackupCodeMfaService(NullLogger<BackupCodeMfaService>.Instance, repository, CreateTracking(repository));
            Assert.False(await service.VerifyBackupCodeAsync(userId, "ABCDEFGH2345"));
            Assert.False(string.IsNullOrEmpty(saved.BackupCodes));
        }
        finally { foreach (var scope in scopes) { scope.Dispose(); } }
    }

    [Fact]
    public async Task StaleVerifierCannotAcceptCodeAfterRegeneration()
    {
        var userId = await SeedLegacyCodeAsync();
        using var firstScope = fixture.Factory.Services.CreateScope();
        using var staleScope = fixture.Factory.Services.CreateScope();
        var first = new UserMfaConfigurationRepository(firstScope.ServiceProvider.GetRequiredService<ApplicationDbContext>());
        var stale = new UserMfaConfigurationRepository(staleScope.ServiceProvider.GetRequiredService<ApplicationDbContext>());
        await stale.GetByUserIdAsync(userId);
        var current = await CreateBackupService(first).GenerateBackupCodesAsync(userId);
        Assert.False(await CreateBackupService(stale).VerifyBackupCodeAsync(userId, "ABCDEFGH2345"));
        Assert.True(await CreateBackupService(stale).VerifyBackupCodeAsync(userId, current[0]));
    }

    [Fact]
    public async Task HttpLegacyStatusExplicitlyLeavesHistoricalCountsUnknown()
    {
        var (userId, tenantId) = await SeedHttpActorAsync();
        using var client = fixture.CreateAuthenticatedClient(userId, tenantId);
        using var response = await client.GetAsync("/v1/auth/mfa/backup-codes");
        using var status = await ReadSuccessAsync(response);
        Assert.False(status.RootElement.GetProperty("areUsageCountsKnown").GetBoolean());
        Assert.Equal(1, status.RootElement.GetProperty("totalCount").GetInt32());
        Assert.Equal(0, status.RootElement.GetProperty("usedCount").GetInt32());
        Assert.Equal(1, status.RootElement.GetProperty("remainingCount").GetInt32());
    }

    [Fact]
    public async Task HttpRegenerationReturnsCodesOnceAndVerificationChangesStoredCounts()
    {
        var (userId, tenantId) = await SeedHttpActorAsync();
        using var client = fixture.CreateAuthenticatedClient(userId, tenantId);
        using var regeneratedResponse = await client.PostAsync("/v1/auth/mfa/backup-codes:regenerate", null);
        using var regenerated = await ReadSuccessAsync(regeneratedResponse);
        var codes = regenerated.RootElement.GetProperty("codes").EnumerateArray().Select(value => value.GetString()!).ToArray();
        Assert.Equal(10, codes.Length);
        Assert.All(codes, code => Assert.Equal(12, code.Length));
        using var verificationResponse = await client.PostAsJsonAsync("/v1/auth/mfa/verify", new
        {
            userId, code = codes[0], method = MfaMethod.BackupCode
        });
        using var verified = await ReadSuccessAsync(verificationResponse);
        Assert.True(verified.RootElement.GetProperty("isValid").GetBoolean());
        Assert.Equal(JsonValueKind.Null, verified.RootElement.GetProperty("accessToken").ValueKind);
        using var statusResponse = await client.GetAsync("/v1/auth/mfa/backup-codes");
        using var status = await ReadSuccessAsync(statusResponse);
        Assert.Equal(10, status.RootElement.GetProperty("totalCount").GetInt32());
        Assert.True(status.RootElement.GetProperty("areUsageCountsKnown").GetBoolean());
        Assert.Equal(9, status.RootElement.GetProperty("remainingCount").GetInt32());
        Assert.Equal(1, status.RootElement.GetProperty("usedCount").GetInt32());
        Assert.False(status.RootElement.TryGetProperty("codes", out _));
        using var replay = await client.PostAsJsonAsync("/v1/auth/mfa/verify", new { userId, code = codes[0], method = MfaMethod.BackupCode });
        Assert.Equal(HttpStatusCode.BadRequest, replay.StatusCode);
    }

    [Fact]
    public async Task HttpStatusUsesAuthenticatedActorInsteadOfQueryUserId()
    {
        var (userId, tenantId) = await SeedHttpActorAsync();
        var otherUser = await SeedLegacyCodeAsync();
        using (var seed = fixture.Factory.Services.CreateScope())
        {
            var context = seed.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var other = await context.Set<UserMfaConfiguration>().SingleAsync(value => value.UserId == otherUser);
            other.BackupCodes += "," + Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes("OTHER2345678")));
            await context.SaveChangesAsync();
        }
        using var client = fixture.CreateAuthenticatedClient(userId, tenantId);
        using var response = await client.GetAsync($"/v1/auth/mfa/backup-codes?userId={otherUser}");
        using var status = await ReadSuccessAsync(response);
        Assert.Equal(1, status.RootElement.GetProperty("remainingCount").GetInt32());
        using var scope = fixture.Factory.Services.CreateScope();
        var otherRow = await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>()
            .Set<UserMfaConfiguration>().SingleAsync(value => value.UserId == otherUser);
        Assert.NotNull(otherRow.BackupCodes);
    }

    [Fact]
    public async Task AnonymousClientCannotReadOrRegenerateRecoveryCodes()
    {
        using var client = fixture.Factory.CreateClient();
        using var status = await client.GetAsync("/v1/auth/mfa/backup-codes");
        using var regenerate = await client.PostAsync("/v1/auth/mfa/backup-codes:regenerate", null);
        Assert.Equal(HttpStatusCode.Unauthorized, status.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, regenerate.StatusCode);
    }

    private async Task<(Guid UserId, Guid TenantId)> SeedHttpActorAsync()
    {
        using var scope = fixture.Factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var marker = Guid.NewGuid().ToString("N");
        var user = User.Create($"mfa-recovery-{marker}@example.test", $"MFA actor {marker}");
        var tenant = new Tenant { Id = Guid.NewGuid(), Name = $"MFA {marker}", Slug = $"mfa-{marker}", IsActive = true,
            AdminEmail = $"admin-{marker}@example.test" };
        context.Set<User>().Add(user);
        context.Set<Tenant>().Add(tenant);
        context.Set<TenantMember>().Add(new TenantMember { Id = Guid.NewGuid(), UserId = user.Id, TenantId = tenant.Id, IsActive = true, Role = "Member" });
        context.Set<UserMfaConfiguration>().Add(new UserMfaConfiguration
        {
            UserId = user.Id, IsEnabled = true, IsSetupComplete = true,
            BackupCodes = Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes("ABCDEFGH2345")))
        });
        await context.SaveChangesAsync();
        return (user.Id, tenant.Id);
    }

    private static async Task<JsonDocument> ReadSuccessAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.OK, $"Expected OK, got {response.StatusCode}: {body}");
        return JsonDocument.Parse(body);
    }

    private async Task<Guid> SeedLegacyCodeAsync()
    {
        using var scope = fixture.Factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var id = Guid.NewGuid();
        context.Set<UserMfaConfiguration>().Add(new UserMfaConfiguration
        {
            UserId = id, IsEnabled = true, IsSetupComplete = true,
            BackupCodes = Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes("ABCDEFGH2345")))
        });
        await context.SaveChangesAsync();
        return id;
    }

    private static BackupCodeMfaService CreateBackupService(IUserMfaConfigurationRepository repository) =>
        new(NullLogger<BackupCodeMfaService>.Instance, repository, CreateTracking(repository), new MfaOptions { BackupCodesCount = 4 });

    private static MfaAttemptTrackingService CreateTracking(IUserMfaConfigurationRepository repository) =>
        new(NullLogger<MfaAttemptTrackingService>.Instance, repository, new Mock<IMfaAttemptRepository>().Object,
            new HttpContextAccessor());
}
