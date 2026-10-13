using System.Collections.Concurrent;
using System.IdentityModel.Tokens.Jwt;
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
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace GameGuild.API.IntegrationTests;

[Collection(ApiPostgreSqlCollection.Name)]
public sealed class PolymorphicSignInPostgreSqlHttpTests(ApiPostgreSqlFixture fixture)
{
    private static readonly string Endpoint = "/" + typeof(AuthController).GetMethod(nameof(AuthController.PolymorphicSignIn))!
        .GetCustomAttribute<HttpPostAttribute>()!.Template!.Replace("v{version:apiVersion}", "v1", StringComparison.Ordinal);

    [Theory]
    [InlineData(CredentialType.Email, false)]
    [InlineData(CredentialType.Email, true)]
    [InlineData(CredentialType.Username, false)]
    [InlineData(CredentialType.Username, true)]
    [InlineData(CredentialType.Phone, false)]
    [InlineData(CredentialType.Phone, true)]
    public async Task ActualCredentialEndpointReturnsCanonicalIdentityJwtAndPersistedSession(CredentialType type, bool explicitType)
    {
        var attempts = new ConcurrentQueue<AuthenticationAttemptContext>();
        using var factory = CreateFactory(attempts: attempts);
        var account = await SeedAsync(factory);
        var identifier = Identifier(account, type).ToUpperInvariant();
        var fingerprint = Guid.NewGuid().ToString("N");
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.UserAgent.ParseAdd("SyntheticCredentialAcceptance/1.0");
        using var response = await client.PostAsJsonAsync(Endpoint, new
        {
            credential = " " + identifier + " ", credentialType = explicitType ? type : (CredentialType?)null,
            account.Password, account.TenantId, deviceFingerprint = fingerprint
        });
        using var payload = await ReadAsync(response, HttpStatusCode.OK);
        await AssertCompletedAsync(factory, account, payload.RootElement, fingerprint);
        var attempt = Assert.Single(attempts);
        Assert.Equal(account.User.Id, attempt.UserId);
        Assert.Equal(account.User.Email, attempt.Identifier);
        Assert.Equal(account.TenantId, attempt.TenantId);
        Assert.Equal(fingerprint, attempt.DeviceFingerprint);
        Assert.Equal("SyntheticCredentialAcceptance/1.0", attempt.UserAgent);
    }

    [Theory]
    [InlineData(CredentialType.Email, "wrong-password")]
    [InlineData(CredentialType.Username, "wrong-password")]
    [InlineData(CredentialType.Phone, "wrong-password")]
    [InlineData(CredentialType.Email, "missing")]
    [InlineData(CredentialType.Username, "missing")]
    [InlineData(CredentialType.Phone, "missing")]
    [InlineData(CredentialType.Email, "deleted")]
    [InlineData(CredentialType.Username, "deleted")]
    [InlineData(CredentialType.Phone, "deleted")]
    public async Task FailedCredentialKindsUseTheSameGenericDenialAndIssueNoSession(CredentialType type, string scenario)
    {
        var attempts = new ConcurrentQueue<AuthenticationAttemptContext>();
        using var factory = CreateFactory(attempts: attempts);
        var account = await SeedAsync(factory);
        if (scenario == "deleted")
        {
            using var scope = factory.Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var user = await context.Set<User>().SingleAsync(value => value.Id == account.User.Id);
            user.SoftDelete();
            await context.SaveChangesAsync();
        }
        var identifier = scenario == "missing" ? Identifier(await SeedAsync(factory, persist: false), type) : Identifier(account, type);
        using var client = factory.CreateClient();
        using var response = await client.PostAsJsonAsync(Endpoint, new
        {
            credential = identifier, credentialType = type,
            password = scenario == "wrong-password" ? SyntheticPassword() : account.Password,
            account.TenantId, deviceFingerprint = "synthetic-failure-device"
        });
        await AssertGenericDenialAsync(factory, response);
        Assert.Single(attempts);
        using var verification = factory.Services.CreateScope();
        var db = verification.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.False(await db.Set<UserSession>().AnyAsync(session => session.UserId == account.User.Id));
        Assert.Equal(account.User.PasswordHash, (await db.Set<User>().SingleAsync(user => user.Id == account.User.Id)).PasswordHash);
    }

    [Theory]
    [InlineData("", CredentialType.Email)]
    [InlineData("bad-email", CredentialType.Email)]
    [InlineData("user name", CredentialType.Username)]
    [InlineData("user@name", CredentialType.Username)]
    [InlineData("15551234567", CredentialType.Phone)]
    [InlineData("+0123456", CredentialType.Phone)]
    [InlineData("+1 5551234567", CredentialType.Phone)]
    [InlineData("+1234567890123456", CredentialType.Phone)]
    [InlineData("wallet", CredentialType.WalletAddress)]
    [InlineData("user", (CredentialType)999)]
    public async Task SemanticallyInvalidIdentifiersUseGenericUnauthorized(string identifier, CredentialType type)
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        using var response = await client.PostAsJsonAsync(Endpoint, new { credential = identifier, credentialType = type, password = SyntheticPassword() });
        await AssertGenericDenialAsync(factory, response);
    }

    [Theory]
    [InlineData(CredentialType.Email)]
    [InlineData(CredentialType.Username)]
    [InlineData(CredentialType.Phone)]
    public async Task AmbiguousLegacyIdentifiersDenyBothAccountsUntilTheDuplicateIsDeleted(CredentialType type)
    {
        using var factory = CreateFactory();
        var account = await SeedAsync(factory);
        var duplicate = await SeedAsync(factory, persist: false);
        duplicate.User.Email = type == CredentialType.Email ? account.User.Email.ToUpperInvariant() : duplicate.User.Email;
        duplicate.User.Username = type == CredentialType.Username ? account.User.Username!.ToUpperInvariant() : duplicate.User.Username;
        duplicate.User.PhoneNumber = type == CredentialType.Phone ? account.User.PhoneNumber : duplicate.User.PhoneNumber;
        duplicate.User.PasswordHash = account.User.PasswordHash;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            // Direct storage insertion deliberately models legacy case variants; normal signup canonicalizes handles.
            db.Set<User>().Add(duplicate.User);
            await db.SaveChangesAsync();
            var candidates = await scope.ServiceProvider.GetRequiredService<IUserRepository>().FindSignInCandidatesAsync(Identifier(account, type), Lookup(type));
            Assert.Equal(2, candidates.Count);
        }
        using var client = factory.CreateClient();
        var body = new { credential = Identifier(account, type), credentialType = type, account.Password, account.TenantId };
        using var denied = await client.PostAsJsonAsync(Endpoint, body);
        await AssertGenericDenialAsync(factory, denied);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            Assert.False(await db.Set<UserSession>().AnyAsync(session => session.UserId == account.User.Id || session.UserId == duplicate.User.Id));
            var duplicateStored = await db.Set<User>().SingleAsync(user => user.Id == duplicate.User.Id);
            duplicateStored.SoftDelete();
            await db.SaveChangesAsync();
        }
        using var accepted = await client.PostAsJsonAsync(Endpoint, body);
        using var payload = await ReadAsync(accepted, HttpStatusCode.OK);
        await AssertCompletedAsync(factory, account, payload.RootElement);
    }

    [Theory]
    [InlineData(CredentialType.Email)]
    [InlineData(CredentialType.Username)]
    [InlineData(CredentialType.Phone)]
    public async Task EveryCredentialKindRetainsRiskStepUpWithoutIssuingASession(CredentialType type)
    {
        var attempts = new ConcurrentQueue<AuthenticationAttemptContext>();
        using var factory = CreateFactory(RiskLevel.High, attempts);
        var account = await SeedAsync(factory);
        var fingerprint = Guid.NewGuid().ToString("N");
        using var client = factory.CreateClient();
        using var response = await client.PostAsJsonAsync(Endpoint, new { credential = Identifier(account, type), account.Password, account.TenantId, deviceFingerprint = fingerprint });
        using var payload = await ReadAsync(response, HttpStatusCode.OK);
        var result = payload.RootElement;
        Assert.False(result.GetProperty("success").GetBoolean());
        Assert.True(result.GetProperty("requiresStepUp").GetBoolean());
        Assert.False(string.IsNullOrWhiteSpace(result.GetProperty("stepUpToken").GetString()));
        Assert.Equal(JsonValueKind.Null, result.GetProperty("user").GetProperty("phoneNumber").ValueKind);
        Assert.True(string.IsNullOrEmpty(result.GetProperty("accessToken").GetString()));
        Assert.True(string.IsNullOrEmpty(result.GetProperty("refreshToken").GetString()));
        Assert.Equal(fingerprint, Assert.Single(attempts).DeviceFingerprint);
        using var scope = factory.Services.CreateScope();
        Assert.False(await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Set<UserSession>().AnyAsync(session => session.UserId == account.User.Id));
    }

    [Theory]
    [InlineData(CredentialType.Email)]
    [InlineData(CredentialType.Username)]
    [InlineData(CredentialType.Phone)]
    public async Task UnjoinedTenantCannotBeSelectedThroughAnyCredentialKind(CredentialType type)
    {
        using var factory = CreateFactory();
        var account = await SeedAsync(factory);
        using var client = factory.CreateClient();
        using var response = await client.PostAsJsonAsync(Endpoint, new { credential = Identifier(account, type), account.Password, tenantId = Guid.NewGuid() });
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        var text = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("accessToken", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(account.User.PhoneNumber!, text, StringComparison.Ordinal);
        using var scope = factory.Services.CreateScope();
        Assert.False(await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Set<UserSession>().AnyAsync(session => session.UserId == account.User.Id));
    }

    [Theory]
    [InlineData(CredentialType.Email, false)]
    [InlineData(CredentialType.Email, true)]
    [InlineData(CredentialType.Username, false)]
    [InlineData(CredentialType.Username, true)]
    [InlineData(CredentialType.Phone, false)]
    [InlineData(CredentialType.Phone, true)]
    public async Task RealHandlerAndLocalServiceAcceptTheCorrectPasswordAndDenyIncorrectPasswords(CredentialType type, bool incorrect)
    {
        using var factory = CreateFactory();
        var account = await SeedAsync(factory);
        using var scope = factory.Services.CreateScope();
        var handler = new PolymorphicSignInHandler(scope.ServiceProvider.GetRequiredService<IAuthService>(), scope.ServiceProvider.GetRequiredService<IUserRepository>(), NullLogger<PolymorphicSignInHandler>.Instance, scope.ServiceProvider.GetRequiredService<IPasswordSignInAdmissionService>());
        var command = new PolymorphicSignInCommand { Credential = Identifier(account, type), CredentialType = type, Password = incorrect ? SyntheticPassword() : account.Password, TenantId = account.TenantId };
        if (incorrect)
        {
            var exception = await Assert.ThrowsAsync<UnauthorizedAccessException>(() => handler.Handle(command, CancellationToken.None));
            Assert.Equal(scope.ServiceProvider.GetRequiredService<IUserEnumerationProtectionService>().GetGenericErrorMessage("login"), exception.Message);
        }
        else
        {
            var result = await handler.Handle(command, CancellationToken.None);
            Assert.True(result.Success);
            Assert.Equal(account.User.Id, result.UserId);
            Assert.Equal(account.TenantId, result.TenantId);
            Assert.False(string.IsNullOrEmpty(result.AccessToken));
            Assert.Equal(account.User.PhoneNumber, result.User.PhoneNumber);
        }
    }

    [Fact]
    public async Task PasswordChangedAfterIdentifierResolutionIsReloadedBeforeVerification()
    {
        using var factory = CreateFactory();
        var account = await SeedAsync(factory);
        var replacementPassword = SyntheticPassword();
        using var scope = factory.Services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IUserRepository>();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var replaced = false;
        var observed = new Mock<IUserRepository>(MockBehavior.Strict);
        observed.Setup(value => value.FindSignInCandidatesAsync(account.User.Username!, SignInIdentifierType.Username, CancellationToken.None))
            .Returns(async () =>
            {
                var candidates = await repository.FindSignInCandidatesAsync(account.User.Username!, SignInIdentifierType.Username);
                Assert.Single(candidates);
                if (!replaced)
                {
                    Assert.Empty(context.ChangeTracker.Entries<User>());
                    using var updateScope = factory.Services.CreateScope();
                    var db = updateScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                    var user = await db.Set<User>().SingleAsync(value => value.Id == account.User.Id);
                    user.SetPasswordHash(updateScope.ServiceProvider.GetRequiredService<IPasswordHasher>().HashPassword(replacementPassword));
                    await db.SaveChangesAsync();
                    replaced = true;
                }
                return candidates;
            });
        observed.Setup(value => value.GetByIdAsync(account.User.Id, CancellationToken.None))
            .Returns(() => repository.GetByIdAsync(account.User.Id));
        // Only the interleaving is controlled; both reads, password replacement and local authentication are real.
        var handler = new PolymorphicSignInHandler(scope.ServiceProvider.GetRequiredService<IAuthService>(), observed.Object, NullLogger<PolymorphicSignInHandler>.Instance, scope.ServiceProvider.GetRequiredService<IPasswordSignInAdmissionService>());
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => handler.Handle(new PolymorphicSignInCommand
        {
            Credential = account.User.Username!, Password = account.Password, TenantId = account.TenantId
        }, CancellationToken.None));
        Assert.False(await context.Set<UserSession>().AnyAsync(session => session.UserId == account.User.Id));
        var result = await handler.Handle(new PolymorphicSignInCommand
        {
            Credential = account.User.Username!, Password = replacementPassword, TenantId = account.TenantId
        }, CancellationToken.None);
        Assert.True(result.Success);
        Assert.Equal(account.User.Id, result.UserId);
        Assert.Equal(1, await context.Set<UserSession>().CountAsync(session => session.UserId == account.User.Id));
    }

    [Fact]
    public async Task NumericUsernameIsNotAssumedToBeAnInternationalPhone()
    {
        using var factory = CreateFactory();
        var account = await SeedAsync(factory);
        var username = RandomNumberGenerator.GetInt32(100_000_000, 999_999_999).ToString(System.Globalization.CultureInfo.InvariantCulture);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            (await db.Set<User>().SingleAsync(user => user.Id == account.User.Id)).Username = username;
            await db.SaveChangesAsync();
        }
        using var client = factory.CreateClient();
        using var response = await client.PostAsJsonAsync(Endpoint, new { credential = username, account.Password, account.TenantId });
        using var payload = await ReadAsync(response, HttpStatusCode.OK);
        Assert.True(payload.RootElement.GetProperty("success").GetBoolean());
        Assert.Equal(account.User.Id, payload.RootElement.GetProperty("userId").GetGuid());
    }

    [Fact]
    public async Task JsonCannotSupplyTheServerOnlyResolvedAccount()
    {
        using var factory = CreateFactory();
        var account = await SeedAsync(factory);
        using var client = factory.CreateClient();
        using var response = await client.PostAsJsonAsync(Endpoint, new { credential = "missing-" + Guid.NewGuid().ToString("N"), account.Password, account.TenantId, resolvedUserId = account.User.Id, credentialResolutionFailed = false });
        await AssertGenericDenialAsync(factory, response);
        using var scope = factory.Services.CreateScope();
        Assert.False(await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Set<UserSession>().AnyAsync(session => session.UserId == account.User.Id));
    }

    [Theory]
    [InlineData(CredentialType.Email, "account")]
    [InlineData(CredentialType.Username, "account")]
    [InlineData(CredentialType.Phone, "account")]
    [InlineData(CredentialType.Email, "ip")]
    [InlineData(CredentialType.Username, "ip")]
    [InlineData(CredentialType.Phone, "ip")]
    [InlineData(CredentialType.Email, "held-account")]
    [InlineData(CredentialType.Username, "held-account")]
    [InlineData(CredentialType.Phone, "held-account")]
    [InlineData(CredentialType.Email, "held-ip")]
    [InlineData(CredentialType.Username, "held-ip")]
    [InlineData(CredentialType.Phone, "held-ip")]
    public async Task EveryCredentialKindHonorsAccountIpAndConcurrentAdmission(CredentialType type, string gate)
    {
        var addressBytes = RandomNumberGenerator.GetBytes(16);
        addressBytes[0] = 0x20;
        addressBytes[1] = 0x01;
        addressBytes[2] = 0x0d;
        addressBytes[3] = 0xb8;
        var address = new IPAddress(addressBytes);
        using var factory = CreateFactory(sourceAddress: address);
        var account = await SeedAsync(factory);
        var unrelated = $"unrelated-{Guid.NewGuid():N}@example.test";
        if (gate is "account" or "ip")
        {
            using var seedScope = factory.Services.CreateScope();
            var db = seedScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var now = DateTime.UtcNow;
            db.Set<AuthenticationAttempt>().AddRange(Enumerable.Range(0, 3).Select(index => new AuthenticationAttempt
            {
                Id = Guid.NewGuid(),
                Email = gate == "account" ? account.User.Email : unrelated,
                IpAddress = gate == "ip" ? address.ToString() : "192.0.2.253",
                IsSuccessful = false,
                AttemptedAt = now.AddSeconds(-index),
                CreatedAt = now,
                UpdatedAt = now
            }));
            await db.SaveChangesAsync();
        }

        await using var lockConnection = new Npgsql.NpgsqlConnection(fixture.ConnectionString);
        if (gate.StartsWith("held-", StringComparison.Ordinal))
        {
            await lockConnection.OpenAsync();
            var identifier = gate == "held-account" ? account.User.Email : address.ToString();
            var prefix = gate == "held-account"
                ? "gameguild:auth:local-sign-in-lockout:v1:"
                : "gameguild:auth:local-sign-in-lockout:ip:v1:";
            var digest = SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(prefix + identifier));
            var key = System.Buffers.Binary.BinaryPrimitives.ReadInt64BigEndian(digest);
            await using var hold = new Npgsql.NpgsqlCommand("SELECT pg_advisory_lock(@key)", lockConnection);
            hold.Parameters.AddWithValue("key", key);
            await hold.ExecuteNonQueryAsync();
        }

        using var client = factory.CreateClient();
        var elapsed = System.Diagnostics.Stopwatch.StartNew();
        using var response = await client.PostAsJsonAsync(Endpoint, new
        {
            credential = Identifier(account, type), credentialType = type,
            account.Password, account.TenantId
        });
        await AssertGenericDenialAsync(factory, response);
        Assert.True(elapsed.Elapsed >= TimeSpan.FromMilliseconds(400));
        Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
        using var verification = factory.Services.CreateScope();
        var database = verification.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.False(await database.Set<UserSession>().AnyAsync(session => session.UserId == account.User.Id));
        Assert.False(await database.Set<AuthenticationAttempt>().AnyAsync(attempt => attempt.Email == account.User.Email && attempt.IsSuccessful));
    }

    [Theory]
    [InlineData(false, false)] [InlineData(false, true)]
    [InlineData(true, false)] [InlineData(true, true)]
    public async Task HttpIpThresholdNormalizesMappedIpv4AcrossUnrelatedAccounts(bool polymorphic, bool mapped)
    {
        var host = Interlocked.Increment(ref _lockoutAcceptanceHost);
        var normalized = new IPAddress(new byte[] { 198, 18, (byte)(host / 254), (byte)(host % 254 + 1) });
        var sourceAddress = mapped ? normalized.MapToIPv6() : normalized;
        var calls = new ConcurrentQueue<AuthenticationAttemptContext>();
        using var factory = CreateFactory(attempts: calls, sourceAddress: sourceAddress);
        var account = await SeedAsync(factory);
        using (var seed = factory.Services.CreateScope())
        {
            var database = seed.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var now = DateTime.UtcNow;
            database.Set<AuthenticationAttempt>().AddRange(Enumerable.Range(0, 3).Select(index => new AuthenticationAttempt
            {
                Id = Guid.NewGuid(), Email = "lockout-ip-" + Guid.NewGuid().ToString("N") + "@example.test",
                IpAddress = normalized.ToString(), IsSuccessful = false, AttemptedAt = now.AddSeconds(-index),
                CreatedAt = now, UpdatedAt = now
            }));
            await database.SaveChangesAsync();
        }
        using var client = factory.CreateClient();
        object body = polymorphic
            ? new { credential = account.User.Email, credentialType = CredentialType.Email, account.Password, account.TenantId }
            : new { email = account.User.Email, account.Password, account.TenantId };
        var endpoint = polymorphic ? Endpoint : LocalSignInEndpoint();
        using var response = await client.PostAsJsonAsync(endpoint, body);
        await AssertGenericDenialAsync(factory, response);
        Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
        Assert.Empty(calls);
        using var verification = factory.Services.CreateScope();
        var context = verification.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.False(await context.Set<UserSession>().AnyAsync(session => session.UserId == account.User.Id));
        Assert.False(await context.Set<AuthenticationAttempt>().AnyAsync(attempt => attempt.Email == account.User.Email && attempt.IsSuccessful));
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task HttpAccountLockoutExpiresWhileEarlierFailuresRemainInRollingHour(bool polymorphic)
    {
        var bytes = RandomNumberGenerator.GetBytes(16);
        bytes[0] = 0x20; bytes[1] = 0x01; bytes[2] = 0x0d; bytes[3] = 0xb8;
        using var factory = CreateFactory(sourceAddress: new IPAddress(bytes));
        var account = await SeedAsync(factory);
        using (var seed = factory.Services.CreateScope())
        {
            var database = seed.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var attemptedAt = DateTime.UtcNow.AddMinutes(-40);
            database.Set<AuthenticationAttempt>().AddRange(Enumerable.Range(0, 3).Select(index => new AuthenticationAttempt
            {
                Id = Guid.NewGuid(), Email = account.User.Email, IpAddress = "192.0.2.253",
                IsSuccessful = false, AttemptedAt = attemptedAt.AddSeconds(-index),
                CreatedAt = attemptedAt, UpdatedAt = attemptedAt
            }));
            await database.SaveChangesAsync();
        }
        using var client = factory.CreateClient();
        object body = polymorphic
            ? new { credential = account.User.Email, credentialType = CredentialType.Email, account.Password, account.TenantId }
            : new { email = account.User.Email, account.Password, account.TenantId };
        using var response = await client.PostAsJsonAsync(polymorphic ? Endpoint : LocalSignInEndpoint(), body);
        using var payload = await ReadAsync(response, HttpStatusCode.OK);
        await AssertCompletedAsync(factory, account, payload.RootElement);
        using var verification = factory.Services.CreateScope();
        Assert.Equal(3, await verification.ServiceProvider.GetRequiredService<ApplicationDbContext>()
            .Set<AuthenticationAttempt>().CountAsync(attempt => attempt.Email == account.User.Email && !attempt.IsSuccessful));
    }

    private static int _lockoutAcceptanceHost;
    private static string LocalSignInEndpoint() =>
        "/" + typeof(AuthController).GetMethod(nameof(AuthController.LocalSignIn))!
            .GetCustomAttribute<HttpPostAttribute>()!.Template!
            .Replace("v{version:apiVersion}", "v1", StringComparison.Ordinal);

    private sealed class SourceAddressStartupFilter(IPAddress address) : IStartupFilter
    {
        public Action<Microsoft.AspNetCore.Builder.IApplicationBuilder> Configure(Action<Microsoft.AspNetCore.Builder.IApplicationBuilder> next) => app =>
        {
            app.Use(async (context, continuation) =>
            {
                context.Connection.RemoteIpAddress = address;
                await continuation(context);
            });
            next(app);
        };
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task InactiveOrSuspendedAccountCannotObtainTokensOrMfaChallenge(bool localEndpoint, bool suspended)
    {
        using var factory = CreateFactory(level: RiskLevel.High, sourceAddress: EmailPolicySourceAddress());
        var account = await SeedAsync(factory);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var user = await db.Set<User>().SingleAsync(value => value.Id == account.User.Id);
            if (suspended)
            {
                user.Suspend();
            }
            else
            {
                user.IsActive = false;
            }
            await db.SaveChangesAsync();
        }
        using var client = factory.CreateClient();
        using var response = await PostEmailSignInAsync(client, account, localEndpoint);
        await AssertGenericDenialAsync(factory, response);
        await AssertNoIssuedCredentialsAsync(factory, account.User.Id);
    }

    [Theory]
    [InlineData(false, "global-unverified")]
    [InlineData(false, "global-high-risk-unverified")]
    [InlineData(false, "tenant-unverified")]
    [InlineData(false, "global-verified")]
    [InlineData(false, "tenant-verified")]
    [InlineData(false, "disabled-unverified")]
    [InlineData(false, "different-tenant-unverified")]
    [InlineData(true, "global-unverified")]
    [InlineData(true, "global-high-risk-unverified")]
    [InlineData(true, "tenant-unverified")]
    [InlineData(true, "global-verified")]
    [InlineData(true, "tenant-verified")]
    [InlineData(true, "disabled-unverified")]
    [InlineData(true, "different-tenant-unverified")]
    public async Task EmailVerificationPolicyUsesServerConfigurationAndResolvedTenant(bool localEndpoint, string scenario)
    {
        var tenantId = Guid.NewGuid();
        var globalRequirement = scenario.StartsWith("global-", StringComparison.Ordinal);
        var tenantRequirement = scenario.StartsWith("tenant-", StringComparison.Ordinal);
        var otherTenantRequirement = scenario == "different-tenant-unverified";
        var verified = scenario.EndsWith("-verified", StringComparison.Ordinal);
        var expectedAllowed = verified || (!globalRequirement && !tenantRequirement);
        var settings = new Dictionary<string, string?>
        {
            ["Authentication:LocalSignIn:RequireEmailVerification"] = globalRequirement ? "true" : "false"
        };
        if (tenantRequirement || otherTenantRequirement)
        {
            settings["Authentication:LocalSignIn:RequireEmailVerificationForTenants:0"] =
                (tenantRequirement ? tenantId : Guid.NewGuid()).ToString();
        }
        using var factory = CreateFactory(level: scenario == "global-high-risk-unverified" ? RiskLevel.High : RiskLevel.Low, sourceAddress: EmailPolicySourceAddress(), signInPolicy: settings);
        var account = await SeedAsync(factory, verifiedEmail: verified, tenantId: tenantId);
        using var client = factory.CreateClient();
        using var response = await PostEmailSignInAsync(client, account, localEndpoint);
        if (expectedAllowed)
        {
            using var payload = await ReadAsync(response, HttpStatusCode.OK);
            await AssertCompletedAsync(factory, account, payload.RootElement);
        }
        else
        {
            await AssertGenericDenialAsync(factory, response);
            await AssertNoIssuedCredentialsAsync(factory, account.User.Id);
        }
    }

    private static IPAddress EmailPolicySourceAddress()
    {
        var bytes = RandomNumberGenerator.GetBytes(16);
        bytes[0] = 0x20; bytes[1] = 0x01; bytes[2] = 0x0d; bytes[3] = 0xb8;
        return new IPAddress(bytes);
    }

    private static Task<HttpResponseMessage> PostEmailSignInAsync(HttpClient client, Account account, bool localEndpoint)
        => localEndpoint
            ? client.PostAsJsonAsync(LocalSignInEndpoint(), new { email = account.User.Email, account.Password, account.TenantId })
            : client.PostAsJsonAsync(Endpoint, new { credential = account.User.Email, credentialType = CredentialType.Email, account.Password, account.TenantId });

    private static async Task AssertNoIssuedCredentialsAsync(WebApplicationFactory<Program> factory, Guid userId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.False(await db.Set<UserSession>().AnyAsync(value => value.UserId == userId));
        Assert.False(await db.Set<RefreshToken>().AnyAsync(value => value.UserId == userId));
        Assert.False(await db.Set<SignInMfaChallenge>().AnyAsync(value => value.SubjectId == userId));
    }

    private WebApplicationFactory<Program> CreateFactory(RiskLevel level = RiskLevel.Low, ConcurrentQueue<AuthenticationAttemptContext>? attempts = null, IPAddress? sourceAddress = null, Dictionary<string, string?>? signInPolicy = null)
    {
        // Risk classification alone is controlled; CQRS, hashing, JWT, tenant checks, sessions and PostgreSQL are real.
        var risk = new Mock<IAuthenticationAnomalyDetectionService>(MockBehavior.Strict);
        risk.Setup(service => service.AnalyzeLoginAttemptAsync(It.IsAny<AuthenticationAttemptContext>()))
            .Callback<AuthenticationAttemptContext>(attempt => attempts?.Enqueue(attempt))
            .ReturnsAsync(new AuthenticationAnomalyResult { RiskLevel = level, IsSuspicious = level == RiskLevel.High, DetectedAnomalies = ["synthetic-risk"] });
        risk.Setup(service => service.AnalyzeBehavioralPatternsAsync(It.IsAny<Guid>(), It.IsAny<AuthenticationAttemptContext>()))
            .ReturnsAsync(new BehavioralAnalysisResult { MatchesTypicalPattern = true, RiskLevel = level });
        risk.Setup(service => service.RecordSuspiciousActivityAsync(It.IsAny<SuspiciousActivity>())).Returns(Task.CompletedTask);
        return fixture.CreateFactory(builder =>
        {
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(new Dictionary<string, string?> { ["PresentationLayer:Authentication:PasswordPolicy:BCryptWorkFactor"] = "10" }));
            if (signInPolicy is not null)
            {
                builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(signInPolicy));
            }
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IAuthenticationAnomalyDetectionService>();
                services.AddSingleton(risk.Object);
                if (sourceAddress is not null)
                {
                    services.AddSingleton<IStartupFilter>(new SourceAddressStartupFilter(sourceAddress));
                    services.RemoveAll<GameGuild.Configuration.ApplicationLayer.AuthenticationSecurityOptions>();
                    services.AddSingleton(new GameGuild.Configuration.ApplicationLayer.AuthenticationSecurityOptions
                    {
                        MaxFailedAttemptsPerHour = 3,
                        MaxAttemptsPerIpPerHour = 3,
                        EnableIpThrottling = true,
                        AccountLockoutDurationMinutes = 30
                    });
                }
            });
        });
    }

    private static async Task<Account> SeedAsync(WebApplicationFactory<Program> factory, bool persist = true, bool verifiedEmail = true, Guid? tenantId = null)
    {
        var marker = Guid.NewGuid().ToString("N");
        var password = SyntheticPassword();
        using var scope = factory.Services.CreateScope();
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
        var user = User.CreateWithPassword($"poly-{marker}@example.test", "Synthetic credential account", hasher.HashPassword(password), $"poly-{marker}");
        user.PhoneNumber = "+1" + RandomNumberGenerator.GetInt32(100_000_000, 999_999_999).ToString(System.Globalization.CultureInfo.InvariantCulture) + RandomNumberGenerator.GetInt32(1000, 9999).ToString(System.Globalization.CultureInfo.InvariantCulture);
        if (verifiedEmail)
        {
            user.VerifyEmail();
        }
        var tenant = tenantId ?? Guid.NewGuid();
        if (persist)
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.Set<User>().Add(user);
            db.Set<Tenant>().Add(new Tenant { Id = tenant, Name = $"Credential {marker}", Slug = $"poly-{marker}", AdminEmail = $"admin-{marker}@example.test", IsActive = true });
            db.Set<TenantMember>().Add(new TenantMember { Id = Guid.NewGuid(), TenantId = tenant, UserId = user.Id, Role = "Member", IsActive = true });
            await db.SaveChangesAsync();
        }
        return new Account(user, tenant, password);
    }

    private static async Task AssertCompletedAsync(WebApplicationFactory<Program> factory, Account account, JsonElement result, string? fingerprint = null)
    {
        Assert.True(result.GetProperty("success").GetBoolean());
        Assert.Equal(account.User.Id, result.GetProperty("userId").GetGuid());
        Assert.Equal(account.User.Email, result.GetProperty("email").GetString());
        Assert.Equal(account.User.PhoneNumber, result.GetProperty("user").GetProperty("phoneNumber").GetString());
        Assert.Equal(account.TenantId, result.GetProperty("tenantId").GetGuid());
        var accessToken = result.GetProperty("accessToken").GetString()!;
        var refreshToken = result.GetProperty("refreshToken").GetString()!;
        Assert.False(string.IsNullOrWhiteSpace(accessToken));
        Assert.False(string.IsNullOrWhiteSpace(refreshToken));
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(accessToken);
        Assert.Equal(account.User.Id.ToString(), jwt.Claims.Single(claim => claim.Type == "sub").Value);
        Assert.Equal(account.TenantId.ToString(), jwt.Claims.Single(claim => claim.Type == "tenant_id").Value);
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var session = await context.Set<UserSession>().SingleAsync(value => value.Id == result.GetProperty("sessionId").GetGuid());
        Assert.Equal(account.User.Id, session.UserId);
        Assert.True(session.IsActive);
        Assert.NotEqual(refreshToken, session.RefreshToken);
        Assert.Equal(scope.ServiceProvider.GetRequiredService<IRefreshTokenHasher>().HashToken(refreshToken), session.RefreshToken);
        if (fingerprint is not null)
        {
            Assert.Equal(fingerprint, session.DeviceFingerprint);
            Assert.Equal("SyntheticCredentialAcceptance/1.0", session.UserAgent);
        }
    }

    private static async Task AssertGenericDenialAsync(WebApplicationFactory<Program> factory, HttpResponseMessage response)
    {
        using var payload = await ReadAsync(response, HttpStatusCode.Unauthorized);
        using var scope = factory.Services.CreateScope();
        Assert.Equal(scope.ServiceProvider.GetRequiredService<IUserEnumerationProtectionService>().GetGenericErrorMessage("login"), payload.RootElement.GetProperty("detail").GetString());
        var text = payload.RootElement.GetRawText();
        Assert.DoesNotContain("accessToken", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("refreshToken", text, StringComparison.OrdinalIgnoreCase);
    }
    private static async Task<JsonDocument> ReadAsync(HttpResponseMessage response, HttpStatusCode status)
    {
        var body = await response.Content.ReadAsStringAsync();
        Assert.Equal(status, response.StatusCode);
        return JsonDocument.Parse(body);
    }
    private static string Identifier(Account account, CredentialType type) => type switch { CredentialType.Email => account.User.Email, CredentialType.Phone => account.User.PhoneNumber!, _ => account.User.Username! };
    private static SignInIdentifierType Lookup(CredentialType type) => type switch { CredentialType.Email => SignInIdentifierType.Email, CredentialType.Phone => SignInIdentifierType.Phone, _ => SignInIdentifierType.Username };
    private static string SyntheticPassword() => "aA7!" + Convert.ToHexString(RandomNumberGenerator.GetBytes(20));
    private sealed record Account(User User, Guid TenantId, string Password);
}
