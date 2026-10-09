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

namespace GameGuild.Identity.Authentication.UnitTests.Services;

/// <summary>
///     Repository-backed integration tests for <see cref="ApiKeyAuthenticationHandler" /> (#266):
///     key lookup, usage recording, and lazy rotation finalization all flow through
///     <see cref="IApiKeyRepository" /> instead of a direct DbContext query.
/// </summary>
public sealed class ApiKeyAuthenticationHandlerRepositoryTests
{
    [Fact]
    public async Task ValidKey_AuthenticatesThroughTheRepository_AndRecordsUsage()
    {
        var userId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        await using var context = CreateContext();
        var repository = new ApiKeyRepository(context);
        var (apiKey, plaintext) = ApiKey.Create(userId, tenantId, "integration", ["reports:read", "billing:read"]);
        await repository.AddAsync(apiKey);

        var result = await AuthenticateAsync(repository, plaintext);

        result.Succeeded.Should().BeTrue();
        result.Principal.Should().NotBeNull();
        result.Principal!.FindFirst("sub")!.Value.Should().Be(userId.ToString());
        result.Principal.FindFirst(ClaimTypes.NameIdentifier)!.Value.Should().Be(userId.ToString());
        result.Principal.FindFirst("tenant_id")!.Value.Should().Be(tenantId.ToString());
        result.Principal.FindFirst("api_key_id")!.Value.Should().Be(apiKey.Id.ToString());
        result.Principal.FindFirst("auth_method")!.Value.Should().Be("api_key");
        result.Principal.FindAll("scope").Select(c => c.Value)
            .Should().BeEquivalentTo(["reports:read", "billing:read"]);

        var reloaded = await repository.GetByKeyHashAsync(apiKey.KeyHash);
        reloaded!.UsageCount.Should().Be(1, "authentication records usage through the repository");
        reloaded.LastUsedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task RevokedKey_FailsClosed_AndDoesNotRecordUsage()
    {
        var userId = Guid.NewGuid();
        await using var context = CreateContext();
        var repository = new ApiKeyRepository(context);
        var (apiKey, plaintext) = ApiKey.Create(userId, Guid.NewGuid(), "integration", ["read"]);
        apiKey.Revoke("leaked");
        await repository.AddAsync(apiKey);

        var result = await AuthenticateAsync(repository, plaintext);

        result.Succeeded.Should().BeFalse();
        result.Failure!.Message.Should().Contain("inactive or expired");
        (await repository.GetByKeyHashAsync(apiKey.KeyHash))!.UsageCount.Should().Be(0);
    }

    [Fact]
    public async Task UnknownKey_Fails_WithoutTouchingAnyRow()
    {
        await using var context = CreateContext();
        var repository = new ApiKeyRepository(context);
        var (apiKey, _) = ApiKey.Create(Guid.NewGuid(), Guid.NewGuid(), "integration", ["read"]);
        await repository.AddAsync(apiKey);

        var result = await AuthenticateAsync(repository, "gg_live_never_issued");

        result.Succeeded.Should().BeFalse();
        result.Failure!.Message.Should().Contain("Invalid API key");
        (await repository.GetByKeyHashAsync(apiKey.KeyHash))!.UsageCount.Should().Be(0);
    }

    [Fact]
    public async Task RotatedKeyWhoseGraceExpired_FailsClosed_AndFinalizesRevocationLazily()
    {
        var userId = Guid.NewGuid();
        await using var context = CreateContext();
        var repository = new ApiKeyRepository(context);
        var (apiKey, plaintext) = ApiKey.Create(userId, Guid.NewGuid(), "integration", ["read"]);
        await repository.AddAsync(apiKey);
        var tracked = (await repository.GetByKeyHashAsync(apiKey.KeyHash))!;
        tracked.BeginRotationGrace(SystemClock.UtcNow.AddMinutes(-1));
        await context.SaveChangesAsync();

        var result = await AuthenticateAsync(repository, plaintext);

        result.Succeeded.Should().BeFalse();
        var reloaded = await repository.GetByKeyHashAsync(apiKey.KeyHash);
        reloaded!.RevokedAt.Should().NotBeNull("expired rotation must be finalized on use through the repository");
        reloaded.IsActive.Should().BeFalse();
        reloaded.UsageCount.Should().Be(0);
    }

    private static async Task<AuthenticateResult> AuthenticateAsync(IApiKeyRepository repository, string plaintext)
    {
        var options = new Mock<IOptionsMonitor<ApiKeyAuthenticationOptions>>();
        options.Setup(x => x.Get(It.IsAny<string>())).Returns(new ApiKeyAuthenticationOptions());
        options.SetupGet(x => x.CurrentValue).Returns(new ApiKeyAuthenticationOptions());
        var handler = new ApiKeyAuthenticationHandler(
            options.Object,
            NullLoggerFactory.Instance,
            System.Text.Encodings.Web.UrlEncoder.Default,
            repository);
        var context = new DefaultHttpContext();
        context.Request.Headers["X-API-Key"] = plaintext;
        await handler.InitializeAsync(
            new AuthenticationScheme(
                ApiKeyAuthenticationOptions.SchemeName,
                ApiKeyAuthenticationOptions.SchemeName,
                typeof(ApiKeyAuthenticationHandler)),
            context);
        return await handler.AuthenticateAsync();
    }

    private static TestApiKeyDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<TestApiKeyDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        return new TestApiKeyDbContext(options);
    }

    private sealed class TestApiKeyDbContext(DbContextOptions<TestApiKeyDbContext> options)
        : DbContext(options), IApplicationDbContext
    {
        public DbSet<ApiKey> ApiKeys => Set<ApiKey>();

        public Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }
}
