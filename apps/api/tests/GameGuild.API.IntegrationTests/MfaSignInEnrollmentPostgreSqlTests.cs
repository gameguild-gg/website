using System.Buffers.Binary;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using GameGuild.API.Database;
using GameGuild.API.IntegrationTests.Infrastructure;
using GameGuild.Configuration.ApplicationLayer;
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
public sealed class MfaSignInEnrollmentPostgreSqlTests(ApiPostgreSqlFixture fixture)
{
    [Fact]
    public async Task RequiredUnenrolledAccountCanEnrollWithoutAnOrdinaryTokenAndThenCompleteTotpSignIn()
    {
        var clock = new FixedTimeProvider(DateTimeOffset.UtcNow);
        var risk = new Mock<IAuthenticationAnomalyDetectionService>(MockBehavior.Strict);
        risk.Setup(port => port.AnalyzeLoginAttemptAsync(It.IsAny<AuthenticationAttemptContext>()))
            .ReturnsAsync(new AuthenticationAnomalyResult { RiskLevel = RiskLevel.Low });
        risk.Setup(port => port.AnalyzeBehavioralPatternsAsync(It.IsAny<Guid>(), It.IsAny<AuthenticationAttemptContext>()))
            .ReturnsAsync(new BehavioralAnalysisResult { MatchesTypicalPattern = true, RiskLevel = RiskLevel.Low });
        using var factory = fixture.CreateFactory(builder => builder.ConfigureTestServices(services =>
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
        }));
        var marker = Guid.NewGuid().ToString("N");
        var password = "aA7!" + Convert.ToHexString(RandomNumberGenerator.GetBytes(20));
        var tenantId = Guid.NewGuid();
        User user;
        using (var scope = factory.Services.CreateScope())
        {
            var services = scope.ServiceProvider;
            user = User.CreateWithPassword("mfa-enroll-" + marker + "@example.test", "Synthetic MFA enrollment account",
                services.GetRequiredService<IPasswordHasher>().HashPassword(password), "mfa-enroll-" + marker);
            user.VerifyEmail();
            var db = services.GetRequiredService<ApplicationDbContext>();
            db.Set<User>().Add(user);
            db.Set<Tenant>().Add(new Tenant { Id = tenantId, Name = "MFA " + marker, Slug = "mfa-enroll-" + marker,
                AdminEmail = "admin-" + marker + "@example.test", IsActive = true });
            db.Set<TenantMember>().Add(new TenantMember { Id = Guid.NewGuid(), TenantId = tenantId, UserId = user.Id,
                Role = "Member", IsActive = true });
            await db.SaveChangesAsync();
        }
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        using var signIn = await client.PostAsJsonAsync("/v1/auth/sign-in", new { user.Email, password, tenantId });
        Assert.Equal(HttpStatusCode.OK, signIn.StatusCode);
        using var pending = JsonDocument.Parse(await signIn.Content.ReadAsStringAsync());
        Assert.False(pending.RootElement.GetProperty("success").GetBoolean());
        Assert.True(pending.RootElement.GetProperty("requiresMfa").GetBoolean());
        Assert.True(string.IsNullOrEmpty(pending.RootElement.GetProperty("accessToken").GetString()));
        Assert.True(string.IsNullOrEmpty(pending.RootElement.GetProperty("refreshToken").GetString()));
        var bearer = pending.RootElement.GetProperty("mfaToken").GetString();
        Assert.True(SignInMfaChallengeToken.TryHash(bearer, out _));
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            Assert.Empty(await db.Set<UserMfaConfiguration>().Where(row => row.UserId == user.Id).ToListAsync());
            Assert.Empty(await db.Set<UserSession>().Where(row => row.UserId == user.Id).ToListAsync());
            Assert.Empty(await db.Set<RefreshToken>().Where(row => row.UserId == user.Id).ToListAsync());
            Assert.Equal(SignInMfaPurpose.EnrollFactor, (await db.Set<SignInMfaChallenge>().SingleAsync(row => row.SubjectId == user.Id)).Purpose);
        }

        using var setupResponse = await client.PostAsJsonAsync("/v1/auth/mfa/sign-in/enrollment", new { mfaToken = bearer });
        Assert.Equal(HttpStatusCode.OK, setupResponse.StatusCode);
        using var setup = JsonDocument.Parse(await setupResponse.Content.ReadAsStringAsync());
        Assert.True(setup.RootElement.GetProperty("success").GetBoolean());
        Assert.False(setup.RootElement.TryGetProperty("accessToken", out _));
        Assert.False(setup.RootElement.TryGetProperty("refreshToken", out _));
        Assert.False(setup.RootElement.TryGetProperty("backupCodes", out _));
        Assert.StartsWith("otpauth://totp/", setup.RootElement.GetProperty("qrCodeUri").GetString());
        var secret = setup.RootElement.GetProperty("secretKey").GetString()!;
        long step;
        using (var scope = factory.Services.CreateScope())
        {
            var services = scope.ServiceProvider;
            var db = services.GetRequiredService<ApplicationDbContext>();
            var enrollment = await db.Set<UserMfaConfiguration>().SingleAsync(row => row.UserId == user.Id);
            Assert.False(enrollment.IsEnabled);
            Assert.False(enrollment.IsSetupComplete);
            Assert.NotEqual(secret, enrollment.TotpSecretKey);
            var challenge = await db.Set<SignInMfaChallenge>().SingleAsync(row => row.SubjectId == user.Id);
            Assert.True(enrollment.SetupExpiresAt <= challenge.ExpiresAt.UtcDateTime);
            Assert.Empty(await db.Set<UserSession>().Where(row => row.UserId == user.Id).ToListAsync());
            Assert.Empty(await db.Set<RefreshToken>().Where(row => row.UserId == user.Id).ToListAsync());
            step = clock.GetUtcNow().ToUnixTimeSeconds() / services.GetRequiredService<MfaOptions>().TotpTimeStepSeconds;
        }
        using var completedResponse = await client.PostAsJsonAsync("/v1/auth/mfa/sign-in/complete", new
        {
            mfaToken = bearer, code = Totp(secret, step), method = "Totp"
        });
        Assert.Equal(HttpStatusCode.OK, completedResponse.StatusCode);
        using var completed = JsonDocument.Parse(await completedResponse.Content.ReadAsStringAsync());
        Assert.True(completed.RootElement.GetProperty("success").GetBoolean());
        Assert.Equal(user.Id, completed.RootElement.GetProperty("userId").GetGuid());
        Assert.Equal(tenantId, completed.RootElement.GetProperty("tenantId").GetGuid());
        Assert.NotEmpty(completed.RootElement.GetProperty("mfaEnrollmentBackupCodes").EnumerateArray().ToArray());
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(completed.RootElement.GetProperty("accessToken").GetString());
        Assert.Contains(jwt.Claims, claim => claim.Type == "amr" && claim.Value == "mfa");
        using var finalScope = factory.Services.CreateScope();
        var database = finalScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var saved = await database.Set<UserMfaConfiguration>().SingleAsync(row => row.UserId == user.Id);
        Assert.True(saved.IsEnabled);
        Assert.True(saved.IsSetupComplete);
        Assert.Equal(1, await database.Set<UserSession>().CountAsync(row => row.UserId == user.Id));
        Assert.Equal(1, await database.Set<RefreshToken>().CountAsync(row => row.UserId == user.Id));
        Assert.Equal(1, await database.Set<SessionMfaEvidence>().CountAsync(row => row.SubjectId == user.Id));
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

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
