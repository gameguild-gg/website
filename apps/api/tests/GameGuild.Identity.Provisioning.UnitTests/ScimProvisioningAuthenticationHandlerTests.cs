using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace GameGuild.Identity.Provisioning.UnitTests;

/// <summary>
///     Repository-backed tests for <see cref="ScimProvisioningAuthenticationHandler"/>:
/// bearer lookup by hash, fail-closed revocation/expiry handling, lazy rotation
/// finalization, claim shape, and usage recording.
/// </summary>
public sealed class ScimProvisioningAuthenticationHandlerTests
{
    [Fact]
    public async Task ValidToken_AuthenticatesWithTenantFromTheToken_AndRecordsUsage()
    {
        var tenantId = Guid.NewGuid();
        await using var context = CreateContext();
        var repository = new ScimProvisioningTokenRepository(context);
        var (token, plaintext) = ScimProvisioningToken.Create(tenantId, Guid.NewGuid(), "Okta", ["scim:read", "scim:write"]);
        await repository.AddAsync(token);

        var result = await AuthenticateAsync(repository, $"Bearer {plaintext}");

        result.Succeeded.Should().BeTrue();
        result.Principal!.FindFirst("tenant_id")!.Value.Should().Be(tenantId.ToString());
        result.Principal.FindFirst("scim_token_id")!.Value.Should().Be(token.Id.ToString());
        result.Principal.FindFirst("auth_method")!.Value.Should().Be("scim_token");
        result.Principal.FindAll("scope").Select(claim => claim.Value)
            .Should().BeEquivalentTo(["scim:read", "scim:write"]);

        var reloaded = await repository.GetByKeyHashAsync(token.KeyHash);
        reloaded!.UsageCount.Should().Be(1);
        reloaded.LastUsedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task NonScimBearerToken_ReturnsNoResult_WithoutFailing()
    {
        await using var context = CreateContext();
        var repository = new ScimProvisioningTokenRepository(context);

        var result = await AuthenticateAsync(repository, "Bearer some-jwt-token");

        result.None.Should().BeTrue("non gg_scim_ bearers belong to other schemes");
    }

    [Fact]
    public async Task MissingAuthorization_ReturnsNoResult()
    {
        await using var context = CreateContext();
        var repository = new ScimProvisioningTokenRepository(context);

        var result = await AuthenticateAsync(repository, authorizationHeader: null);

        result.None.Should().BeTrue();
    }

    [Fact]
    public async Task UnknownToken_FailsClosed()
    {
        await using var context = CreateContext();
        var repository = new ScimProvisioningTokenRepository(context);

        var result = await AuthenticateAsync(repository, "Bearer gg_scim_never_issued_000000000000000000");

        result.Succeeded.Should().BeFalse();
        result.Failure!.Message.Should().Contain("Invalid SCIM provisioning token");
    }

    [Fact]
    public async Task RevokedToken_FailsClosed_WithoutRecordingUsage()
    {
        await using var context = CreateContext();
        var repository = new ScimProvisioningTokenRepository(context);
        var (token, plaintext) = ScimProvisioningToken.Create(Guid.NewGuid(), Guid.NewGuid(), "t", ScimScopes.All);
        token.Revoke("leaked");
        await repository.AddAsync(token);

        var result = await AuthenticateAsync(repository, $"Bearer {plaintext}");

        result.Succeeded.Should().BeFalse();
        result.Failure!.Message.Should().Contain("inactive or expired");
        (await repository.GetByKeyHashAsync(token.KeyHash))!.UsageCount.Should().Be(0);
    }

    [Fact]
    public async Task ExpiredToken_FailsClosed()
    {
        await using var context = CreateContext();
        var repository = new ScimProvisioningTokenRepository(context);
        var (token, plaintext) = ScimProvisioningToken.Create(
            Guid.NewGuid(), Guid.NewGuid(), "t", ScimScopes.All, expiresAt: SystemClock.UtcNow.AddMinutes(-5));
        await repository.AddAsync(token);

        var result = await AuthenticateAsync(repository, $"Bearer {plaintext}");

        result.Succeeded.Should().BeFalse();
    }

    [Fact]
    public async Task RotatedTokenWhoseGraceExpired_FailsClosed_AndFinalizesRevocationLazily()
    {
        await using var context = CreateContext();
        var repository = new ScimProvisioningTokenRepository(context);
        var (token, plaintext) = ScimProvisioningToken.Create(Guid.NewGuid(), Guid.NewGuid(), "t", ScimScopes.All);
        await repository.AddAsync(token);
        var tracked = (await repository.GetByKeyHashAsync(token.KeyHash))!;
        tracked.BeginRotationGrace(SystemClock.UtcNow.AddMinutes(-1));
        await context.SaveChangesAsync();

        var result = await AuthenticateAsync(repository, $"Bearer {plaintext}");

        result.Succeeded.Should().BeFalse();
        var reloaded = await repository.GetByKeyHashAsync(token.KeyHash);
        reloaded!.RevokedAt.Should().NotBeNull("the expired rotation is finalized on use");
        reloaded.IsActive.Should().BeFalse();
    }

    private static async Task<AuthenticateResult> AuthenticateAsync(
        ScimProvisioningTokenRepository repository,
        string? authorizationHeader)
    {
        var options = new Mock<IOptionsMonitor<ScimProvisioningAuthenticationOptions>>();
        options.Setup(monitor => monitor.Get(It.IsAny<string>())).Returns(new ScimProvisioningAuthenticationOptions());
        options.SetupGet(monitor => monitor.CurrentValue).Returns(new ScimProvisioningAuthenticationOptions());
        var handler = new ScimProvisioningAuthenticationHandler(
            options.Object,
            NullLoggerFactory.Instance,
            System.Text.Encodings.Web.UrlEncoder.Default,
            repository);
        var context = new DefaultHttpContext();
        if (authorizationHeader is not null)
        {
            context.Request.Headers.Authorization = authorizationHeader;
        }

        await handler.InitializeAsync(
            new AuthenticationScheme(
                ScimProvisioningAuthenticationOptions.SchemeName,
                ScimProvisioningAuthenticationOptions.SchemeName,
                typeof(ScimProvisioningAuthenticationHandler)),
            context);
        return await handler.AuthenticateAsync();
    }

    private static TestProvisioningDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<TestProvisioningDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        return new TestProvisioningDbContext(options);
    }

    private sealed class TestProvisioningDbContext(DbContextOptions<TestProvisioningDbContext> options)
        : DbContext(options), IApplicationDbContext
    {
        // The repository reaches the tokens through Set<ScimProvisioningToken>(),
        // so the InMemory model must carry the module's entity configurations.
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => new ProvisioningModelConfiguration().Configure(modelBuilder);

        public Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }
}
