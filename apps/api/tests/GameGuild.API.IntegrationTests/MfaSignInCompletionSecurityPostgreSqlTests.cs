using System.Net;
using System.Buffers.Binary;
using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using GameGuild.API.Database;
using GameGuild.API.IntegrationTests.Infrastructure;
using GameGuild.Configuration.ApplicationLayer;
using GameGuild.Compliance.Audit;
using GameGuild.Identity.Authentication;
using GameGuild.Identity.Tenants;
using GameGuild.Identity.Users;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Moq;

namespace GameGuild.API.IntegrationTests;

[Collection(ApiPostgreSqlCollection.Name)]
public sealed class MfaSignInCompletionSecurityPostgreSqlTests(ApiPostgreSqlFixture fixture)
{
    [Theory]
    [InlineData("expired")]
    [InlineData("changed-version")]
    [InlineData("inactive-account")]
    [InlineData("inactive-membership")]
    [InlineData("unenrolled")]
    [InlineData("changed-policy")]
    [InlineData("invalid-code")]
    [InlineData("unknown-bearer")]
    public async Task InvalidOrStaleChallengeCannotPersistOrdinaryCredentials(string fault)
    {
        using var prepared = await PrepareAsync();
        using (var scope = prepared.Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            switch (fault)
            {
                case "expired":
                    var challenge = await db.Set<SignInMfaChallenge>().SingleAsync(row => row.SubjectId == prepared.UserId);
                    challenge.CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-6);
                    challenge.ExpiresAt = challenge.CreatedAt.AddMinutes(5);
                    break;
                case "changed-version": (await db.Set<User>().SingleAsync(row => row.Id == prepared.UserId)).TokenVersion++; break;
                case "inactive-account": (await db.Set<User>().SingleAsync(row => row.Id == prepared.UserId)).IsActive = false; break;
                case "inactive-membership": (await db.Set<TenantMember>().SingleAsync(row => row.UserId == prepared.UserId)).IsActive = false; break;
                case "unenrolled": (await db.Set<UserMfaConfiguration>().SingleAsync(row => row.UserId == prepared.UserId)).IsEnabled = false; break;
                case "changed-policy": scope.ServiceProvider.GetRequiredService<MfaOptions>().RequireMfaByDefault = false; break;
                case "invalid-code":
                case "unknown-bearer": break;
                default: throw new ArgumentOutOfRangeException(nameof(fault), fault, "Unknown MFA completion fault.");
            }
            await db.SaveChangesAsync();
        }
        using var response = await CompleteAsync(prepared, fault == "unknown-bearer" ? SignInMfaChallengeToken.Create() : prepared.Bearer,
            fault == "invalid-code" ? Convert.ToHexString(RandomNumberGenerator.GetBytes(6)) : prepared.Code);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        using var check = prepared.Factory.Services.CreateScope();
        var database = check.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.Empty(await database.Set<UserSession>().Where(row => row.UserId == prepared.UserId).ToListAsync());
        Assert.Empty(await database.Set<RefreshToken>().Where(row => row.UserId == prepared.UserId).ToListAsync());
        Assert.Empty(await database.Set<SessionMfaEvidence>().Where(row => row.SubjectId == prepared.UserId).ToListAsync());
        Assert.Null((await database.Set<SignInMfaChallenge>().SingleAsync(row => row.SubjectId == prepared.UserId)).ConsumedAt);
    }

    [Fact]
    public async Task ClientBindingsCannotReplaceTheServerChallengeBinding()
    {
        using var prepared = await PrepareAsync();
        using var response = await prepared.Client.PostAsJsonAsync("/v1/auth/mfa/sign-in/complete", new
        {
            mfaToken = prepared.Bearer, code = prepared.Code, method = "BackupCode", userId = Guid.NewGuid(),
            tenantId = Guid.NewGuid(), tokenVersion = int.MaxValue, policyFingerprint = new string('0', 64), mfaVerified = true
        });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(prepared.UserId, json.RootElement.GetProperty("userId").GetGuid());
        Assert.Equal(prepared.TenantId, json.RootElement.GetProperty("tenantId").GetGuid());
        using var scope = prepared.Factory.Services.CreateScope();
        var evidence = await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Set<SessionMfaEvidence>()
            .SingleAsync(row => row.SubjectId == prepared.UserId);
        Assert.Equal(prepared.TenantId, evidence.TenantId);
        Assert.NotEqual(int.MaxValue, evidence.TokenVersion);
        Assert.NotEqual(new string('0', 64), evidence.PolicyFingerprint);
    }

    [Fact]
    public async Task TwoHttpCompletionsCanCommitOnlyOneSessionAndProof()
    {
        using var prepared = await PrepareAsync();
        var responses = await Task.WhenAll(CompleteAsync(prepared, prepared.Bearer, prepared.Code),
            CompleteAsync(prepared, prepared.Bearer, prepared.Code));
        try
        {
            Assert.Single(responses, response => response.StatusCode == HttpStatusCode.OK);
            Assert.Single(responses, response => response.StatusCode == HttpStatusCode.Unauthorized);
            using var scope = prepared.Factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            Assert.Equal(1, await db.Set<UserSession>().CountAsync(row => row.UserId == prepared.UserId));
            Assert.Equal(1, await db.Set<RefreshToken>().CountAsync(row => row.UserId == prepared.UserId));
            Assert.Equal(1, await db.Set<SessionMfaEvidence>().CountAsync(row => row.SubjectId == prepared.UserId));
        }
        finally { foreach (var response in responses) { response.Dispose(); } }
    }

    [Fact]
    public async Task FailureAfterProofPersistenceRollsBackProviderChallengeAndCredentials()
    {
        var fault = new EvidenceFault();
        using var prepared = await PrepareAsync(fault);
        using var failed = await CompleteAsync(prepared, prepared.Bearer, prepared.Code);
        Assert.Equal(HttpStatusCode.InternalServerError, failed.StatusCode);
        using (var scope = prepared.Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            Assert.Equal(0, await db.Set<UserSession>().CountAsync(row => row.UserId == prepared.UserId));
            Assert.Equal(0, await db.Set<RefreshToken>().CountAsync(row => row.UserId == prepared.UserId));
            Assert.Equal(0, await db.Set<SessionMfaEvidence>().CountAsync(row => row.SubjectId == prepared.UserId));
            Assert.Null((await db.Set<SignInMfaChallenge>().SingleAsync(row => row.SubjectId == prepared.UserId)).ConsumedAt);
            Assert.Equal(Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(prepared.Code))),
                (await db.Set<UserMfaConfiguration>().SingleAsync(row => row.UserId == prepared.UserId)).BackupCodes);
        }
        fault.Enabled = false;
        using var completed = await CompleteAsync(prepared, prepared.Bearer, prepared.Code);
        Assert.Equal(HttpStatusCode.OK, completed.StatusCode);
    }

    [Fact]
    public async Task FailureAfterRequiredAuditRollsBackAuditProofChallengeAndCredentialsTogether()
    {
        var fault = new AuditFault();
        using var prepared = await PrepareAsync(auditFault: fault);
        using var failed = await CompleteAsync(prepared, prepared.Bearer, prepared.Code);
        Assert.Equal(HttpStatusCode.InternalServerError, failed.StatusCode);
        using (var scope = prepared.Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            Assert.Equal(0, await db.Set<UserSession>().CountAsync(row => row.UserId == prepared.UserId));
            Assert.Equal(0, await db.Set<RefreshToken>().CountAsync(row => row.UserId == prepared.UserId));
            Assert.Equal(0, await db.Set<SessionMfaEvidence>().CountAsync(row => row.SubjectId == prepared.UserId));
            Assert.Equal(0, await db.Set<AuditLog>().CountAsync(row => row.UserId == prepared.UserId && row.ActionType == "Authentication.MfaSignInVerified"));
            Assert.Equal(1, await db.Set<AuditLog>().CountAsync(row => row.UserId == prepared.UserId && row.ActionType == "Authentication.MfaSignInRequired"));
            Assert.Null((await db.Set<SignInMfaChallenge>().SingleAsync(row => row.SubjectId == prepared.UserId)).ConsumedAt);
            Assert.Equal(Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(prepared.Code))),
                (await db.Set<UserMfaConfiguration>().SingleAsync(row => row.UserId == prepared.UserId)).BackupCodes);
        }
        fault.Enabled = false;
        using var completed = await CompleteAsync(prepared, prepared.Bearer, prepared.Code);
        Assert.Equal(HttpStatusCode.OK, completed.StatusCode);
        using var verifiedScope = prepared.Factory.Services.CreateScope();
        var verifiedDb = verifiedScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.Equal(1, await verifiedDb.Set<SessionMfaEvidence>().CountAsync(row => row.SubjectId == prepared.UserId));
        Assert.Equal(1, await verifiedDb.Set<AuditLog>().CountAsync(row => row.UserId == prepared.UserId && row.ActionType == "Authentication.MfaSignInVerified"));
    }

    [Fact]
    public async Task ChangedMfaPolicyDeniesRefreshWithoutRotatingTheRoot()
    {
        using var prepared = await PrepareAsync();
        using var completed = await CompleteAsync(prepared, prepared.Bearer, prepared.Code);
        Assert.Equal(HttpStatusCode.OK, completed.StatusCode);
        using var json = JsonDocument.Parse(await completed.Content.ReadAsStringAsync());
        prepared.Factory.Services.GetRequiredService<MfaOptions>().RequireMfaByDefault = false;
        using var refreshed = await prepared.Client.PostAsJsonAsync("/v1/auth/tokens:refresh", new
        {
            refreshToken = json.RootElement.GetProperty("refreshToken").GetString(), tenantId = prepared.TenantId
        });
        Assert.Equal(HttpStatusCode.Unauthorized, refreshed.StatusCode);
        using var scope = prepared.Factory.Services.CreateScope();
        var token = await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Set<RefreshToken>()
            .SingleAsync(row => row.UserId == prepared.UserId);
        Assert.False(token.IsRevoked);
        Assert.Null(token.ReplacedByToken);
    }

    [Fact]
    public async Task TotpCompletionPersistsItsWatermarkAndCannotReplayAgainstAnotherChallenge()
    {
        var clock = new FixedTimeProvider(DateTimeOffset.UtcNow);
        using var prepared = await PrepareAsync(timeProvider: clock);
        var key = RandomNumberGenerator.GetBytes(20);
        const string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
        var secret = new StringBuilder();
        var buffer = 0;
        var bitCount = 0;
        foreach (var value in key)
        {
            buffer = (buffer << 8) | value;
            bitCount += 8;
            while (bitCount >= 5) { bitCount -= 5; secret.Append(alphabet[(buffer >> bitCount) & 31]); }
        }
        long step;
        using (var scope = prepared.Factory.Services.CreateScope())
        {
            var services = scope.ServiceProvider;
            var db = services.GetRequiredService<ApplicationDbContext>();
            var enrollment = await db.Set<UserMfaConfiguration>().SingleAsync(row => row.UserId == prepared.UserId);
            enrollment.TotpSecretKey = services.GetRequiredService<IEncryptionService>().Encrypt(secret.ToString());
            enrollment.PreferredMethod = MfaMethod.Totp;
            await db.SaveChangesAsync();
            step = clock.GetUtcNow().ToUnixTimeSeconds() / services.GetRequiredService<MfaOptions>().TotpTimeStepSeconds;
        }
        var message = new byte[8];
        BinaryPrimitives.WriteInt64BigEndian(message, step);
        var digest = HMACSHA1.HashData(key, message);
        var offset = digest[^1] & 15;
        var number = (BinaryPrimitives.ReadUInt32BigEndian(digest.AsSpan(offset, 4)) & 0x7fffffff) % 1000000;
        var code = number.ToString("D6", System.Globalization.CultureInfo.InvariantCulture);
        using var completed = await prepared.Client.PostAsJsonAsync("/v1/auth/mfa/sign-in/complete", new
        {
            mfaToken = prepared.Bearer, code, method = "Totp"
        });
        Assert.Equal(HttpStatusCode.OK, completed.StatusCode);
        using var json = JsonDocument.Parse(await completed.Content.ReadAsStringAsync());
        var access = new JwtSecurityTokenHandler().ReadJwtToken(json.RootElement.GetProperty("accessToken").GetString());
        Assert.Contains(access.Claims, claim => claim.Type == "amr" && claim.Value == "otp");
        using var pending = await prepared.Client.PostAsJsonAsync("/v1/auth/sign-in", new
        {
            email = prepared.Email, password = prepared.Password, tenantId = prepared.TenantId
        });
        Assert.Equal(HttpStatusCode.OK, pending.StatusCode);
        using var second = JsonDocument.Parse(await pending.Content.ReadAsStringAsync());
        var bearer = second.RootElement.GetProperty("mfaToken").GetString();
        Assert.NotEqual(prepared.Bearer, bearer);
        using var replay = await prepared.Client.PostAsJsonAsync("/v1/auth/mfa/sign-in/complete", new { mfaToken = bearer, code, method = "Totp" });
        Assert.Equal(HttpStatusCode.Unauthorized, replay.StatusCode);
        using var check = prepared.Factory.Services.CreateScope();
        var database = check.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var saved = await database.Set<UserMfaConfiguration>().SingleAsync(row => row.UserId == prepared.UserId);
        Assert.Equal(step, (await database.Set<TotpReplayState>().SingleAsync(row => row.ConfigurationId == saved.Id)).LastAcceptedStep);
        Assert.Equal(1, await database.Set<SessionMfaEvidence>().CountAsync(row => row.SubjectId == prepared.UserId && row.Method == MfaMethod.Totp));
        Assert.Equal(1, await database.Set<UserSession>().CountAsync(row => row.UserId == prepared.UserId));
        Assert.Equal(1, await database.Set<RefreshToken>().CountAsync(row => row.UserId == prepared.UserId));
    }

    [Fact]
    public async Task NewlyGeneratedBackupCodeSetCanCompleteTheBoundChallenge()
    {
        using var prepared = await PrepareAsync();
        string[] codes;
        using (var scope = prepared.Factory.Services.CreateScope())
        {
            codes = await scope.ServiceProvider.GetRequiredService<IBackupCodeMfaService>().GenerateBackupCodesAsync(prepared.UserId);
        }
        Assert.NotEmpty(codes);
        using var completed = await CompleteAsync(prepared, prepared.Bearer, codes[0]);
        Assert.Equal(HttpStatusCode.OK, completed.StatusCode);
        using var scopeForCheck = prepared.Factory.Services.CreateScope();
        var evidence = await scopeForCheck.ServiceProvider.GetRequiredService<ApplicationDbContext>().Set<SessionMfaEvidence>()
            .SingleAsync(row => row.SubjectId == prepared.UserId);
        Assert.Equal(MfaMethod.BackupCode, evidence.Method);
    }

    private static Task<HttpResponseMessage> CompleteAsync(PreparedSession prepared, string bearer, string code) =>
        prepared.Client.PostAsJsonAsync("/v1/auth/mfa/sign-in/complete", new { mfaToken = bearer, code, method = "BackupCode" });

    private async Task<PreparedSession> PrepareAsync(EvidenceFault? fault = null, TimeProvider? timeProvider = null, AuditFault? auditFault = null)
    {
        var risk = new Mock<IAuthenticationAnomalyDetectionService>(MockBehavior.Strict);
        risk.Setup(service => service.AnalyzeLoginAttemptAsync(It.IsAny<AuthenticationAttemptContext>()))
            .ReturnsAsync(new AuthenticationAnomalyResult { RiskLevel = RiskLevel.Low });
        risk.Setup(service => service.AnalyzeBehavioralPatternsAsync(It.IsAny<Guid>(), It.IsAny<AuthenticationAttemptContext>()))
            .ReturnsAsync(new BehavioralAnalysisResult { MatchesTypicalPattern = true, RiskLevel = RiskLevel.Low });
        var factory = fixture.CreateFactory(builder => builder.ConfigureTestServices(services =>
        {
            services.PostConfigure<MfaOptions>(options => { options.Enabled = true; options.RequireMfaByDefault = true; });
            services.RemoveAll<MfaOptions>();
            services.AddSingleton(provider => provider.GetRequiredService<IOptions<MfaOptions>>().Value);
            services.RemoveAll<IMfaAttemptTrackingService>();
            services.AddScoped<IMfaAttemptTrackingService>(provider => ActivatorUtilities.CreateInstance<MfaAttemptTrackingService>(provider,
                provider.GetRequiredService<IOptions<MfaOptions>>().Value));
            services.RemoveAll<IMfaService>();
            services.AddScoped<IMfaService, MfaService>();
            services.RemoveAll<IAuthenticationAnomalyDetectionService>();
            services.AddSingleton(risk.Object);
            if (timeProvider is not null)
            {
                services.RemoveAll<TimeProvider>();
                services.AddSingleton(timeProvider);
            }
            if (fault is not null)
            {
                services.RemoveAll<ISessionMfaEvidenceStore>();
                services.AddScoped<ISessionMfaEvidenceStore>(provider => new FailingEvidenceStore(
                    new PostgreSqlSessionMfaEvidenceStore(provider.GetRequiredService<ApplicationDbContext>()), fault));
            }
            if (auditFault is not null)
            {
                services.RemoveAll<IAuthenticationAuditEventSink>();
                services.AddScoped<IAuthenticationAuditEventSink>(provider => new FailingAuditSink(
                    ActivatorUtilities.CreateInstance<CentralAuthenticationAuditEventSink>(provider), auditFault));
            }
        }));
        HttpClient? client = null;
        try
        {
            var marker = Guid.NewGuid().ToString("N");
            var password = "aA7!" + Convert.ToHexString(RandomNumberGenerator.GetBytes(20));
            var code = Convert.ToHexString(RandomNumberGenerator.GetBytes(6));
            var tenantId = Guid.NewGuid();
            User user;
            using (var scope = factory.Services.CreateScope())
            {
                var services = scope.ServiceProvider;
                user = User.CreateWithPassword("mfa-security-" + marker + "@example.test", "Synthetic MFA security account",
                    services.GetRequiredService<IPasswordHasher>().HashPassword(password), "mfa-security-" + marker);
                user.VerifyEmail();
                var db = services.GetRequiredService<ApplicationDbContext>();
                db.Set<User>().Add(user);
                db.Set<Tenant>().Add(new Tenant { Id = tenantId, Name = "MFA " + marker, Slug = "mfa-security-" + marker,
                    AdminEmail = "admin-" + marker + "@example.test", IsActive = true });
                db.Set<TenantMember>().Add(new TenantMember { Id = Guid.NewGuid(), TenantId = tenantId, UserId = user.Id,
                    Role = "Member", IsActive = true });
                db.Set<UserMfaConfiguration>().Add(new UserMfaConfiguration { Id = Guid.NewGuid(), UserId = user.Id,
                    IsEnabled = true, IsSetupComplete = true,
                    BackupCodes = Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(code))) });
                await db.SaveChangesAsync();
            }
            client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
            using var response = await client.PostAsJsonAsync("/v1/auth/sign-in", new { user.Email, password, tenantId });
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            Assert.False(json.RootElement.GetProperty("success").GetBoolean());
            Assert.True(json.RootElement.GetProperty("requiresMfa").GetBoolean());
            Assert.True(string.IsNullOrEmpty(json.RootElement.GetProperty("accessToken").GetString()));
            Assert.True(string.IsNullOrEmpty(json.RootElement.GetProperty("refreshToken").GetString()));
            var bearer = json.RootElement.GetProperty("mfaToken").GetString();
            Assert.True(SignInMfaChallengeToken.TryHash(bearer, out _));
            return new PreparedSession(factory, client, user.Id, tenantId, bearer!, code, user.Email, password);
        }
        catch { client?.Dispose(); factory.Dispose(); throw; }
    }

    private sealed record PreparedSession(WebApplicationFactory<Program> Factory, HttpClient Client,
        Guid UserId, Guid TenantId, string Bearer, string Code, string Email, string Password) : IDisposable
    {
        public void Dispose() { Client.Dispose(); Factory.Dispose(); }
    }

    private sealed class EvidenceFault { public bool Enabled { get; set; } = true; }

    private sealed class AuditFault { public bool Enabled { get; set; } = true; }

    private sealed class FailingAuditSink(IAuthenticationAuditEventSink real, AuditFault fault) : IAuthenticationAuditEventSink
    {
        public async Task RecordAsync(AuthenticationAuditEvent auditEvent, CancellationToken cancellationToken)
        {
            await real.RecordAsync(auditEvent, cancellationToken);
            if (fault.Enabled && auditEvent.ActionType == "Authentication.MfaSignInVerified")
            {
                throw new InvalidOperationException("Synthetic failure after required MFA audit persistence.");
            }
        }
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class FailingEvidenceStore(ISessionMfaEvidenceStore real, EvidenceFault fault) : ISessionMfaEvidenceStore
    {
        public async Task AddAsync(Guid sessionId, SignInMfaProof proof, CancellationToken cancellationToken)
        {
            await real.AddAsync(sessionId, proof, cancellationToken);
            if (fault.Enabled) { throw new InvalidOperationException("Synthetic failure after MFA evidence persistence."); }
        }
        public Task<SessionMfaEvidence?> FindAsync(Guid sessionId, CancellationToken cancellationToken) => real.FindAsync(sessionId, cancellationToken);
    }
}
