using System.Net;
using System.Net.Http.Json;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using GameGuild.API.Database;
using GameGuild.API.IntegrationTests.Infrastructure;
using GameGuild.Identity.Authentication;
using GameGuild.Identity.Tenants;
using GameGuild.Identity.Users;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace GameGuild.API.IntegrationTests;

[Collection(ApiPostgreSqlCollection.Name)]
public sealed class PasswordHistoryPostgreSqlHttpTests(ApiPostgreSqlFixture fixture)
{
    private static readonly string ChangeEndpoint = AuthEndpoint(nameof(AuthController.ChangePassword));
    private static readonly string ResetEndpoint = AuthEndpoint(nameof(AuthController.ResetPassword));

    [Theory]
    [InlineData("change", 0)]
    [InlineData("change", 1)]
    [InlineData("change", 2)]
    [InlineData("change", 3)]
    [InlineData("change", 4)]
    [InlineData("change", 5)]
    [InlineData("reset", 0)]
    [InlineData("reset", 1)]
    [InlineData("reset", 2)]
    [InlineData("reset", 3)]
    [InlineData("reset", 4)]
    [InlineData("reset", 5)]
    public async Task BothWritersRejectCurrentAndEveryRetainedPassword(string writer, int age)
    {
        using var factory = CreateFactory();
        var account = await SeedHistoryAsync(factory);
        var before = await ReadUserAsync(factory, account.UserId);
        using var response = await WritePasswordAsync(factory, account, writer, account.Passwords[6 - age]);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("reuse", await response.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);
        var after = await ReadUserAsync(factory, account.UserId);
        Assert.Equal(before.PasswordHash, after.PasswordHash);
        Assert.Equal(before.PasswordHistoryHashes, after.PasswordHistoryHashes);
        Assert.Equal(before.TokenVersion, after.TokenVersion);
        Assert.Equal(before.Version, after.Version);
    }

    [Theory]
    [InlineData("change")]
    [InlineData("reset")]
    public async Task BothWritersAllowEvictedPasswordAndRotatePersistedHistory(string writer)
    {
        using var factory = CreateFactory();
        var account = await SeedHistoryAsync(factory);
        var before = await ReadUserAsync(factory, account.UserId);
        using var response = await WritePasswordAsync(factory, account, writer, account.Passwords[0]);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var after = await ReadUserAsync(factory, account.UserId);
        using var scope = factory.Services.CreateScope();
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
        Assert.True(hasher.VerifyPassword(after.PasswordHash!, account.Passwords[0]));
        Assert.NotEqual(account.Hashes[0], after.PasswordHash);
        Assert.Equal(new[] { account.Hashes[6], account.Hashes[5], account.Hashes[4], account.Hashes[3], account.Hashes[2] }, after.GetPasswordHistoryHashes());
        Assert.Equal(before.TokenVersion + 1, after.TokenVersion);
        Assert.Equal(before.Version + 1, after.Version);
        using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.True(payload.RootElement.GetProperty("success").GetBoolean());
        foreach (var hash in account.Hashes) { Assert.DoesNotContain(hash, payload.RootElement.GetRawText(), StringComparison.Ordinal); }
    }

    [Fact]
    public async Task MigratedStorageRetainsFiveMixedFormatsAndDoesNotSerializeHashes()
    {
        using var factory = CreateFactory();
        var account = await SeedHistoryAsync(factory);
        var user = await ReadUserAsync(factory, account.UserId);
        Assert.Equal(account.Hashes[6], user.PasswordHash);
        Assert.Equal(account.Hashes.Reverse().Skip(1).Take(5), user.GetPasswordHistoryHashes());
        Assert.DoesNotContain(account.Hashes[0], user.GetPasswordHistoryHashes());
        Assert.Contains(user.GetPasswordHistoryHashes(), hash => hash.StartsWith("pbkdf2-sha256$", StringComparison.Ordinal));
        Assert.Contains(user.GetPasswordHistoryHashes(), hash => hash.StartsWith("$2", StringComparison.Ordinal));
        var serialized = JsonSerializer.Serialize(user);
        Assert.DoesNotContain("PasswordHash", serialized, StringComparison.OrdinalIgnoreCase);
        foreach (var hash in account.Hashes) { Assert.DoesNotContain(hash, serialized, StringComparison.Ordinal); }
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var migrations = await context.Database.GetAppliedMigrationsAsync();
        Assert.Contains("20260928024931_AddUserPasswordHistory", migrations);
        Assert.Equal(5, user.GetPasswordHistoryHashes().Count);
        Assert.True(user.PasswordHistoryHashes!.Length <= 2600);
    }

    [Fact]
    public async Task SimultaneousChangeAndResetFromOneSnapshotCommitOneTransition()
    {
        using var factory = CreateFactory();
        var account = await SeedHistoryAsync(factory);
        var before = await ReadUserAsync(factory, account.UserId);
        var newChange = SyntheticPassword(false);
        var newReset = SyntheticPassword(true);
        using var firstScope = factory.Services.CreateScope();
        using var secondScope = factory.Services.CreateScope();
        using var tokenScope = factory.Services.CreateScope();
        var tokenService = tokenScope.ServiceProvider.GetRequiredService<IEmailVerificationService>();
        var token = await tokenService.GeneratePasswordResetTokenAsync(account.UserId, account.Email);
        var loaded = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var arrivals = 0;

        IUserRepository ScheduleRead(IUserRepository inner)
        {
            // Only timing is controlled: both real scoped repositories read the same committed snapshot.
            var scheduled = new Mock<IUserRepository>(MockBehavior.Strict);
            scheduled.Setup(repository => repository.GetByIdAsync(account.UserId, It.IsAny<CancellationToken>()))
                .Returns(async (Guid id, CancellationToken cancellationToken) =>
                {
                    var user = await inner.GetByIdAsync(id, cancellationToken);
                    Assert.NotNull(user);
                    Assert.Equal(before.PasswordHash, user.PasswordHash);
                    if (Interlocked.Increment(ref arrivals) == 2) { loaded.SetResult(); }
                    await loaded.Task.WaitAsync(TimeSpan.FromSeconds(20), cancellationToken);
                    return user;
                });
            scheduled.Setup(repository => repository.UpdatePasswordHashAsync(account.UserId, It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
                .Returns((Guid id, string hash, string? expected, CancellationToken cancellationToken) => inner.UpdatePasswordHashAsync(id, hash, expected, cancellationToken));
            return scheduled.Object;
        }

        var change = new ChangePasswordCommandHandler(
            ScheduleRead(firstScope.ServiceProvider.GetRequiredService<IUserRepository>()),
            firstScope.ServiceProvider.GetRequiredService<IPasswordHasher>(),
            NullLogger<ChangePasswordCommandHandler>.Instance);
        var reset = new ResetPasswordCommandHandler(
            ScheduleRead(secondScope.ServiceProvider.GetRequiredService<IUserRepository>()),
            secondScope.ServiceProvider.GetRequiredService<IPasswordHasher>(), tokenService,
            NullLogger<ResetPasswordCommandHandler>.Instance);
        var changeTask = Task.Run(() => change.Handle(new ChangePasswordCommand
        {
            UserId = account.UserId, CurrentPassword = account.Passwords[6],
            NewPassword = newChange, ConfirmPassword = newChange, RevokeOtherSessions = false
        }, CancellationToken.None));
        var resetTask = Task.Run(() => reset.Handle(new ResetPasswordCommand
        {
            Token = token, NewPassword = newReset, ConfirmPassword = newReset
        }, CancellationToken.None));
        await Task.WhenAll(changeTask, resetTask);
        var changeResult = await changeTask;
        var resetResult = await resetTask;
        Assert.Equal(2, arrivals);
        Assert.NotEqual(changeResult.Success, resetResult.Success);
        var loserMessage = changeResult.Success ? resetResult.Message : changeResult.Message;
        Assert.Contains("changed during", loserMessage, StringComparison.OrdinalIgnoreCase);
        var after = await ReadUserAsync(factory, account.UserId);
        using var verification = factory.Services.CreateScope();
        var hasher = verification.ServiceProvider.GetRequiredService<IPasswordHasher>();
        Assert.True(hasher.VerifyPassword(after.PasswordHash!, changeResult.Success ? newChange : newReset));
        Assert.False(hasher.VerifyPassword(after.PasswordHash!, changeResult.Success ? newReset : newChange));
        Assert.Equal(new[] { account.Hashes[6], account.Hashes[5], account.Hashes[4], account.Hashes[3], account.Hashes[2] }, after.GetPasswordHistoryHashes());
        Assert.Equal(before.TokenVersion + 1, after.TokenVersion);
        Assert.Equal(before.Version + 1, after.Version);
    }

    private WebApplicationFactory<Program> CreateFactory() => fixture.CreateFactory(builder =>
        builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["PresentationLayer:Authentication:PasswordPolicy:BCryptWorkFactor"] = "10"
        })));

    private static async Task<Account> SeedHistoryAsync(WebApplicationFactory<Program> factory)
    {
        using var scope = factory.Services.CreateScope();
        var marker = Guid.NewGuid().ToString("N");
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
        var passwords = Enumerable.Range(0, 7).Select(index => SyntheticPassword(index % 2 == 0)).ToArray();
        var hashes = passwords.Select(hasher.HashPassword).ToArray();
        var user = new User { Id = Guid.NewGuid(), Email = $"history-{marker}@example.test", Name = $"History {marker}", PasswordHash = hashes[0], IsActive = true };
        user.VerifyEmail();
        var repository = scope.ServiceProvider.GetRequiredService<IUserRepository>();
        await repository.AddAsync(user);
        await repository.SaveChangesAsync();
        for (var index = 1; index < hashes.Length; index++)
        {
            Assert.True(await repository.UpdatePasswordHashAsync(user.Id, hashes[index], hashes[index - 1], CancellationToken.None));
        }
        var tenantId = Guid.NewGuid();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        context.Set<Tenant>().Add(new Tenant { Id = tenantId, Name = $"History {marker}", Slug = $"history-{marker}", AdminEmail = $"admin-{marker}@example.test", IsActive = true });
        context.Set<TenantMember>().Add(new TenantMember { Id = Guid.NewGuid(), TenantId = tenantId, UserId = user.Id, Role = "Member", IsActive = true });
        await context.SaveChangesAsync();
        return new Account(user.Id, tenantId, user.Email, passwords, hashes);
    }

    private async Task<HttpResponseMessage> WritePasswordAsync(WebApplicationFactory<Program> factory, Account account, string writer, string candidate)
    {
        using var client = factory.CreateClient();
        if (writer == "change")
        {
            using var principalClient = fixture.CreateAuthenticatedClient(account.UserId, account.TenantId);
            foreach (var header in principalClient.DefaultRequestHeaders)
            {
                Assert.True(client.DefaultRequestHeaders.TryAddWithoutValidation(header.Key, header.Value));
            }
            return await client.PostAsJsonAsync(ChangeEndpoint, new { currentPassword = account.Passwords[6], newPassword = candidate, confirmPassword = candidate, revokeOtherSessions = false });
        }
        using var scope = factory.Services.CreateScope();
        var token = await scope.ServiceProvider.GetRequiredService<IEmailVerificationService>().GeneratePasswordResetTokenAsync(account.UserId, account.Email);
        return await client.PostAsJsonAsync(ResetEndpoint, new { token, newPassword = candidate, confirmPassword = candidate });
    }

    private static async Task<User> ReadUserAsync(WebApplicationFactory<Program> factory, Guid userId)
    {
        using var scope = factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Set<User>().AsNoTracking().SingleAsync(user => user.Id == userId);
    }

    private static string AuthEndpoint(string action)
    {
        var route = typeof(AuthController).GetMethod(action)?.GetCustomAttribute<HttpPostAttribute>()?.Template
            ?? throw new InvalidOperationException($"No POST route for auth action {action}.");
        return "/" + route.Replace("v{version:apiVersion}", "v1", StringComparison.Ordinal);
    }

    private static string SyntheticPassword(bool longPassword) => "aA7!" + Convert.ToHexString(RandomNumberGenerator.GetBytes(longPassword ? 50 : 20));
    private sealed record Account(Guid UserId, Guid TenantId, string Email, string[] Passwords, string[] Hashes);
}
