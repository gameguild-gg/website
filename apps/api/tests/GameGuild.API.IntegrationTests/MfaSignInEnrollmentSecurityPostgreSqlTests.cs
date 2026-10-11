using System.Buffers.Binary;
using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
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
public sealed class MfaSignInEnrollmentSecurityPostgreSqlTests(ApiPostgreSqlFixture fixture)
{
    [Fact]
    public async Task SameChallengeResumesTheExactSetupWithoutRecoveryCodesOrCredentials()
    {
        using var flow = await CreateAsync();
        var first = await flow.SetupAsync();
        var second = await flow.SetupAsync();
        Assert.Equal(first.Secret, second.Secret);
        Assert.Equal(first.Expiry, second.Expiry);
        using var scope = flow.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var configuration = await db.Set<UserMfaConfiguration>().SingleAsync(row => row.UserId == flow.User.Id);
        Assert.Null(configuration.BackupCodes);
        Assert.Null(configuration.QrCodeSetupData);
        var challenge = await db.Set<SignInMfaChallenge>().SingleAsync(row => row.SubjectId == flow.User.Id);
        Assert.Equal(configuration.Id, challenge.EnrollmentConfigurationId);
        Assert.NotEqual(configuration.TotpSecretKey, challenge.EnrollmentSecretFingerprint);
        Assert.Equal(64, challenge.EnrollmentSecretFingerprint!.Length);
        Assert.Null(challenge.ConsumedAt);
        await AssertNoCredentialsAsync(flow);
    }

    [Fact]
    public async Task TwoChallengesCannotReplaceAnActiveEnrollmentAndTwoConfirmationsIssueOnce()
    {
        using var flow = await CreateAsync();
        var other = await flow.SignInAsync();
        using var first = await flow.Client.PostAsJsonAsync("/v1/auth/mfa/sign-in/enrollment", new { mfaToken = flow.Bearer });
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        using var firstBody = JsonDocument.Parse(await first.Content.ReadAsStringAsync());
        var secret = firstBody.RootElement.GetProperty("secretKey").GetString()!;
        using var second = await flow.Client.PostAsJsonAsync("/v1/auth/mfa/sign-in/enrollment", new { mfaToken = other });
        Assert.Equal(HttpStatusCode.Unauthorized, second.StatusCode);
        var attempts = await Task.WhenAll(flow.CompleteAsync(secret), flow.CompleteAsync(secret));
        try
        {
            Assert.Single(attempts, response => response.StatusCode == HttpStatusCode.OK);
            Assert.Single(attempts, response => response.StatusCode == HttpStatusCode.Unauthorized);
            using var scope = flow.Factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            Assert.Equal(1, await db.Set<UserMfaConfiguration>().CountAsync(row => row.UserId == flow.User.Id));
            Assert.Equal(1, await db.Set<UserSession>().CountAsync(row => row.UserId == flow.User.Id));
            Assert.Equal(1, await db.Set<RefreshToken>().CountAsync(row => row.UserId == flow.User.Id));
            Assert.Equal(1, await db.Set<SessionMfaEvidence>().CountAsync(row => row.SubjectId == flow.User.Id));
        }
        finally { foreach (var response in attempts) { response.Dispose(); } }
    }

    [Theory]
    [InlineData("expired")]
    [InlineData("version")]
    [InlineData("membership")]
    [InlineData("policy")]
    [InlineData("secret")]
    [InlineData("configuration")]
    [InlineData("lockout")]
    [InlineData("revoked")]
    public async Task ChangedBindingsDenyEnrollmentCompletionWithoutConsumingProof(string fault)
    {
        using var flow = await CreateAsync();
        var setup = await flow.SetupAsync();
        using (var scope = flow.Factory.Services.CreateScope())
        {
            var services = scope.ServiceProvider;
            var db = services.GetRequiredService<ApplicationDbContext>();
            var challenge = await db.Set<SignInMfaChallenge>().SingleAsync(row => row.SubjectId == flow.User.Id);
            var configuration = await db.Set<UserMfaConfiguration>().SingleAsync(row => row.UserId == flow.User.Id);
            switch (fault)
            {
                case "expired": flow.Clock.Advance(TimeSpan.FromMinutes(6)); break;
                case "version": (await db.Set<User>().SingleAsync(row => row.Id == flow.User.Id)).TokenVersion++; break;
                case "membership": (await db.Set<TenantMember>().SingleAsync(row => row.UserId == flow.User.Id)).IsActive = false; break;
                case "policy": services.GetRequiredService<MfaOptions>().RequireMfaByDefault = false; break;
                case "secret": configuration.TotpSecretKey = services.GetRequiredService<IEncryptionService>().Encrypt("JBSWY3DPEHPK3PXPJBSWY3DPEHPK3PXP"); break;
                case "configuration": challenge.EnrollmentConfigurationId = Guid.NewGuid(); break;
                case "lockout": configuration.LockedOutUntil = flow.Clock.GetUtcNow().UtcDateTime.AddMinutes(15); break;
                case "revoked": challenge.RevokedAt = flow.Clock.GetUtcNow(); break;
                default: throw new ArgumentOutOfRangeException(nameof(fault));
            }
            await db.SaveChangesAsync();
        }
        using var response = await flow.CompleteAsync(setup.Secret);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        await AssertNoCredentialsAsync(flow);
        using var finalScope = flow.Factory.Services.CreateScope();
        var database = finalScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var saved = await database.Set<UserMfaConfiguration>().SingleAsync(row => row.UserId == flow.User.Id);
        Assert.False(saved.IsEnabled);
        Assert.False(saved.IsSetupComplete);
        Assert.Null(saved.BackupCodes);
        Assert.Empty(await database.Set<TotpReplayState>().Where(row => row.ConfigurationId == saved.Id).ToListAsync());
        Assert.Null((await database.Set<SignInMfaChallenge>().SingleAsync(row => row.SubjectId == flow.User.Id)).ConsumedAt);
    }

    [Fact]
    public async Task EnrollmentRejectsBackupCodesAndRetainsFailedTotpAccounting()
    {
        using var flow = await CreateAsync();
        var setup = await flow.SetupAsync();
        using var backup = await flow.Client.PostAsJsonAsync("/v1/auth/mfa/sign-in/complete", new
        {
            mfaToken = flow.Bearer, code = "synthetic-unused-recovery-code", method = "BackupCode"
        });
        Assert.Equal(HttpStatusCode.Unauthorized, backup.StatusCode);
        using var invalid = await flow.Client.PostAsJsonAsync("/v1/auth/mfa/sign-in/complete", new
        {
            mfaToken = flow.Bearer, code = "invalid-" + Guid.NewGuid().ToString("N"), method = "Totp"
        });
        Assert.Equal(HttpStatusCode.Unauthorized, invalid.StatusCode);
        await AssertNoCredentialsAsync(flow);
        using (var scope = flow.Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var pending = await db.Set<UserMfaConfiguration>().SingleAsync(row => row.UserId == flow.User.Id);
            Assert.Equal(1, pending.FailedAttempts);
            Assert.False(pending.IsEnabled);
            Assert.Null(pending.BackupCodes);
        }
        using var completed = await flow.CompleteAsync(setup.Secret);
        Assert.Equal(HttpStatusCode.OK, completed.StatusCode);
        using var replay = await flow.CompleteAsync(setup.Secret);
        Assert.Equal(HttpStatusCode.Unauthorized, replay.StatusCode);
    }

    [Fact]
    public async Task AuditFailureRollsBackEnrollmentProofCredentialsAndRecoveryCodes()
    {
        var fault = new AuditFault();
        using var flow = await CreateAsync(fault);
        var setup = await flow.SetupAsync();
        fault.Fail = true;
        using var failed = await flow.CompleteAsync(setup.Secret);
        Assert.Equal(HttpStatusCode.InternalServerError, failed.StatusCode);
        await AssertNoCredentialsAsync(flow);
        using (var scope = flow.Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var saved = await db.Set<UserMfaConfiguration>().SingleAsync(row => row.UserId == flow.User.Id);
            Assert.False(saved.IsEnabled);
            Assert.False(saved.IsSetupComplete);
            Assert.Null(saved.BackupCodes);
            Assert.Empty(await db.Set<TotpReplayState>().Where(row => row.ConfigurationId == saved.Id).ToListAsync());
            Assert.Null((await db.Set<SignInMfaChallenge>().SingleAsync(row => row.SubjectId == flow.User.Id)).ConsumedAt);
        }
        fault.Fail = false;
        using var retried = await flow.CompleteAsync(setup.Secret);
        Assert.Equal(HttpStatusCode.OK, retried.StatusCode);
    }

    private static async Task AssertNoCredentialsAsync(Flow flow)
    {
        using var scope = flow.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.Empty(await db.Set<UserSession>().Where(row => row.UserId == flow.User.Id).ToListAsync());
        Assert.Empty(await db.Set<RefreshToken>().Where(row => row.UserId == flow.User.Id).ToListAsync());
        Assert.Empty(await db.Set<SessionMfaEvidence>().Where(row => row.SubjectId == flow.User.Id).ToListAsync());
    }

    private async Task<Flow> CreateAsync(AuditFault? fault = null)
    {
        var clock = new Clock(DateTimeOffset.UtcNow);
        var risk = new Mock<IAuthenticationAnomalyDetectionService>(MockBehavior.Strict);
        risk.Setup(port => port.AnalyzeLoginAttemptAsync(It.IsAny<AuthenticationAttemptContext>()))
            .ReturnsAsync(new AuthenticationAnomalyResult { RiskLevel = RiskLevel.Low });
        risk.Setup(port => port.AnalyzeBehavioralPatternsAsync(It.IsAny<Guid>(), It.IsAny<AuthenticationAttemptContext>()))
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
            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(clock);
            if (fault is not null)
            {
                services.RemoveAll<IAuthenticationAuditEventSink>();
                services.AddScoped<IAuthenticationAuditEventSink>(provider => new AuditSink(
                    ActivatorUtilities.CreateInstance<CentralAuthenticationAuditEventSink>(provider), fault));
            }
        }));
        try
        {
            var marker = Guid.NewGuid().ToString("N");
            var password = Convert.ToHexString(RandomNumberGenerator.GetBytes(24)) + "!aA7";
            var tenantId = Guid.NewGuid();
            User user;
            using (var scope = factory.Services.CreateScope())
            {
                var services = scope.ServiceProvider;
                user = User.CreateWithPassword("mfa-enroll-security-" + marker + "@example.test", "Synthetic enrollment account",
                    services.GetRequiredService<IPasswordHasher>().HashPassword(password), "mfa-enroll-security-" + marker);
                user.VerifyEmail();
                var db = services.GetRequiredService<ApplicationDbContext>();
                db.Set<User>().Add(user);
                db.Set<Tenant>().Add(new Tenant { Id = tenantId, Name = "MFA " + marker, Slug = "mfa-enroll-security-" + marker,
                    AdminEmail = "admin-" + marker + "@example.test", IsActive = true });
                db.Set<TenantMember>().Add(new TenantMember { Id = Guid.NewGuid(), TenantId = tenantId, UserId = user.Id, Role = "Member", IsActive = true });
                await db.SaveChangesAsync();
            }
            var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
            var flow = new Flow(factory, client, clock, user, password, tenantId);
            try { flow.Bearer = await flow.SignInAsync(); return flow; }
            catch { flow.Dispose(); throw; }
        }
        catch { factory.Dispose(); throw; }
    }

    private sealed class Flow(WebApplicationFactory<Program> factory, HttpClient client, Clock clock, User user,
        string password, Guid tenantId) : IDisposable
    {
        public WebApplicationFactory<Program> Factory { get; } = factory;
        public HttpClient Client { get; } = client;
        public Clock Clock { get; } = clock;
        public User User { get; } = user;
        public string Bearer { get; set; } = string.Empty;
        public async Task<string> SignInAsync()
        {
            using var response = await Client.PostAsJsonAsync("/v1/auth/sign-in", new { User.Email, password, tenantId });
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            Assert.False(body.RootElement.GetProperty("success").GetBoolean());
            Assert.True(body.RootElement.GetProperty("requiresMfa").GetBoolean());
            Assert.True(string.IsNullOrEmpty(body.RootElement.GetProperty("accessToken").GetString()));
            return body.RootElement.GetProperty("mfaToken").GetString()!;
        }
        public async Task<(string Secret, DateTimeOffset Expiry)> SetupAsync()
        {
            using var response = await Client.PostAsJsonAsync("/v1/auth/mfa/sign-in/enrollment", new { mfaToken = Bearer });
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            Assert.True(body.RootElement.GetProperty("success").GetBoolean());
            Assert.False(body.RootElement.TryGetProperty("accessToken", out _));
            Assert.False(body.RootElement.TryGetProperty("refreshToken", out _));
            Assert.False(body.RootElement.TryGetProperty("backupCodes", out _));
            return (body.RootElement.GetProperty("secretKey").GetString()!, body.RootElement.GetProperty("expiresAt").GetDateTimeOffset());
        }
        public Task<HttpResponseMessage> CompleteAsync(string secret) => Client.PostAsJsonAsync("/v1/auth/mfa/sign-in/complete",
            new { mfaToken = Bearer, code = Totp(secret, Clock.GetUtcNow().ToUnixTimeSeconds() / 30), method = "Totp" });
        public void Dispose() { Client.Dispose(); Factory.Dispose(); }
    }

    private sealed class Clock(DateTimeOffset now) : TimeProvider
    {
        private DateTimeOffset _now = now;
        public override DateTimeOffset GetUtcNow() => _now;
        public void Advance(TimeSpan duration) => _now += duration;
    }
    private sealed class AuditFault { public bool Fail { get; set; } }
    private sealed class AuditSink(IAuthenticationAuditEventSink actual, AuditFault fault) : IAuthenticationAuditEventSink
    {
        public Task RecordAsync(AuthenticationAuditEvent value, CancellationToken cancellationToken)
        {
            if (fault.Fail && value.ActionType == "Authentication.MfaSignInVerified") { throw new InvalidOperationException("Synthetic audit persistence failure."); }
            return actual.RecordAsync(value, cancellationToken);
        }
    }

    private static string Totp(string secret, long step)
    {
        const string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
        var bytes = new List<byte>();
        var buffer = 0;
        var bits = 0;
        foreach (var letter in secret)
        {
            var value = alphabet.IndexOf(letter);
            Assert.InRange(value, 0, 31);
            buffer = (buffer << 5) | value;
            bits += 5;
            if (bits >= 8) { bits -= 8; bytes.Add((byte)(buffer >> bits)); }
        }
        var message = new byte[8];
        BinaryPrimitives.WriteInt64BigEndian(message, step);
        var digest = HMACSHA1.HashData(bytes.ToArray(), message);
        var offset = digest[^1] & 15;
        var number = (BinaryPrimitives.ReadUInt32BigEndian(digest.AsSpan(offset, 4)) & 0x7fffffff) % 1000000;
        return number.ToString("D6", System.Globalization.CultureInfo.InvariantCulture);
    }
}
