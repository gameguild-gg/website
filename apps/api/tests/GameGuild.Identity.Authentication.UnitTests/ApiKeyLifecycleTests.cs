using System.Security.Claims;
using FluentAssertions;
using GameGuild;
using GameGuild.CQRS;
using GameGuild.Identity.Authentication;
using GameGuild.Identity.Authorization;
using GameGuild.Identity.Context.Actors;
using GameGuild.Configuration.PresentationLayer.Authorization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;
using AuthorizationOptions = Microsoft.AspNetCore.Authorization.AuthorizationOptions;

namespace GameGuild.Identity.Authentication.UnitTests;

/// <summary>
///     Tests for API-key scope enforcement, rotation lifecycle, and lifecycle audit (#215).
/// </summary>
public sealed class ApiKeyScopeEnforcementTests
{
    private static AuthorizationHandlerContext Evaluate(ClaimsPrincipal user, ApiKeyScopeRequirement requirement)
    {
        var context = new AuthorizationHandlerContext([requirement], user, resource: null);
        new ApiKeyScopeHandler().HandleAsync(context).GetAwaiter().GetResult();
        return context;
    }

    private static ClaimsPrincipal ApiKeyPrincipal(Guid keyId, params string[] scopes)
    {
        var claims = new List<Claim> { new("api_key_id", keyId.ToString()) };
        claims.AddRange(scopes.Select(scope => new Claim("scope", scope)));
        return new ClaimsPrincipal(new ClaimsIdentity(claims, "ApiKey"));
    }

    private static ClaimsPrincipal UserPrincipal() =>
        new(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString())], "Bearer"));

    [Fact]
    public void NonApiKeyPrincipals_PassThroughUnchanged()
    {
        var context = Evaluate(UserPrincipal(), new ApiKeyScopeRequirement("api_keys:manage"));

        context.HasSucceeded.Should().BeTrue("user-authenticated requests are not constrained by API-key scopes");
        context.HasFailed.Should().BeFalse();
    }

    [Fact]
    public void AnonymousPrincipal_WithoutApiKeyClaim_PassesRequirement()
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity());

        var context = Evaluate(principal, new ApiKeyScopeRequirement("api_keys:manage"));

        context.HasSucceeded.Should().BeTrue("the requirement only constrains api_key_id principals; authentication itself is enforced elsewhere");
    }

    [Fact]
    public void ApiKeyPrincipal_WithMatchingScope_Succeeds()
    {
        var principal = ApiKeyPrincipal(Guid.NewGuid(), "reports:read", "api_keys:manage");

        var context = Evaluate(principal, new ApiKeyScopeRequirement("api_keys:manage"));

        context.HasSucceeded.Should().BeTrue();
    }

    [Fact]
    public void ApiKeyPrincipal_WithMatchingScope_IgnoresCase()
    {
        var principal = ApiKeyPrincipal(Guid.NewGuid(), "API_KEYS:MANAGE");

        var context = Evaluate(principal, new ApiKeyScopeRequirement("api_keys:manage"));

        context.HasSucceeded.Should().BeTrue();
    }

    [Fact]
    public void ApiKeyPrincipal_OutOfScope_FailsClosed()
    {
        var principal = ApiKeyPrincipal(Guid.NewGuid(), "reports:read");

        var context = Evaluate(principal, new ApiKeyScopeRequirement("api_keys:manage"));

        context.HasFailed.Should().BeTrue("operations outside the key's declared scopes must be denied");
    }

    [Fact]
    public void ApiKeyPrincipal_WithoutScopeClaims_FailsClosed()
    {
        var principal = ApiKeyPrincipal(Guid.NewGuid());

        var context = Evaluate(principal, new ApiKeyScopeRequirement("api_keys:manage"));

        context.HasFailed.Should().BeTrue("a missing/invalid scope claim set must deny api-key-authenticated requests");
    }

    [Fact]
    public void ApiKeyPrincipal_WithWildcardScope_SatisfiesAnyRequirement()
    {
        var principal = ApiKeyPrincipal(Guid.NewGuid(), "*");

        var context = Evaluate(principal, new ApiKeyScopeRequirement("billing:refund"));

        context.HasSucceeded.Should().BeTrue("wildcard scope semantics match ApiKey.HasScope");
    }

    [Fact]
    public void ApiKeyPrincipal_WithBlankScopeClaimsOnly_FailsClosed()
    {
        var principal = ApiKeyPrincipal(Guid.NewGuid(), " ", string.Empty);

        var context = Evaluate(principal, new ApiKeyScopeRequirement("api_keys:manage"));

        context.HasFailed.Should().BeTrue("blank scope claims must not count as a granted scope");
    }

    [Fact]
    public void Requirement_EmptyScope_Throws()
    {
        var act = () => new ApiKeyScopeRequirement(" ");

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void PolicyNames_RoundTrip()
    {
        var name = ApiKeyScopePolicies.For("reports:read");

        name.Should().Be("apikey-scope:reports:read");
        ApiKeyScopePolicies.TryGetScope(name, out var scope).Should().BeTrue();
        scope.Should().Be("reports:read");
        ApiKeyScopePolicies.TryGetScope("RequireAdminRole", out _).Should().BeFalse();
        ApiKeyScopePolicies.TryGetScope(ApiKeyScopePolicies.Prefix, out _).Should().BeFalse("empty scope names are not policies");
    }

    [Fact]
    public void ControllerActions_RequireApiKeyManageScopePolicy()
    {
        var expectedPolicy = ApiKeyScopePolicies.Prefix + ApiKeyScopes.ManageApiKeys;

        foreach (var action in new[]
                 {
                     nameof(ApiKeyController.CreateApiKey),
                     nameof(ApiKeyController.ListApiKeys),
                     nameof(ApiKeyController.RotateApiKey),
                     nameof(ApiKeyController.RevokeApiKey)
                 })
        {
            var authorize = typeof(ApiKeyController)
                .GetMethod(action)!
                .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
                .Cast<AuthorizeAttribute>()
                .Should()
                .ContainSingle()
                .Subject;

            authorize.Policy.Should().Be(expectedPolicy, "{0} must be scope-constrained for API-key callers", action);
        }
    }

    [Fact]
    public async Task ScopePolicyProvider_ResolvesScopePoliciesWithoutDatabase()
    {
        var provider = CreateProvider();

        var policy = await provider.GetPolicyAsync(ApiKeyScopePolicies.For("api_keys:manage"));

        policy.Should().NotBeNull();
        policy!.Requirements.Should().ContainSingle(r => r is ApiKeyScopeRequirement)
            .Which.As<ApiKeyScopeRequirement>().Scope.Should().Be("api_keys:manage");
        policy.Requirements.Should().Contain(r => r is DenyAnonymousAuthorizationRequirement);
    }

    [Fact]
    public async Task ScopePolicyProvider_DelegatesNonScopeNamesToFallback()
    {
        var provider = CreateProvider();

        var unknown = await provider.GetPolicyAsync("not-a-scope-policy");
        unknown.Should().NotBeNull("the database-backed provider answers unknown names with a fail-closed policy");
        unknown!.Requirements.Should().Contain(r => r is AssertionRequirement,
            "unknown policy names must resolve to a deny-all policy");

        var defaultPolicy = await provider.GetDefaultPolicyAsync();
        defaultPolicy.Should().NotBeNull();

        var fallbackPolicy = await provider.GetFallbackPolicyAsync();
        fallbackPolicy.Should().BeNull("AuthorizationOptions has no fallback policy configured");
    }

    [Fact]
    public async Task ResolvedScopePolicy_EndToEndEvaluation()
    {
        var provider = CreateProvider();
        var policy = (await provider.GetPolicyAsync(ApiKeyScopePolicies.For("reports:read")))!;

        var allowed = new AuthorizationHandlerContext(
            policy.Requirements, ApiKeyPrincipal(Guid.NewGuid(), "reports:read"), null);
        var denied = new AuthorizationHandlerContext(
            policy.Requirements, ApiKeyPrincipal(Guid.NewGuid(), "reports:write"), null);

        var handler = new ApiKeyScopeHandler();
        await handler.HandleAsync(allowed);
        await handler.HandleAsync(denied);
        // Mimic the built-in pass-through handler so self-handling requirements
        // (RequireAuthenticatedUser's DenyAnonymousAuthorizationRequirement) are evaluated too.
        foreach (var requirement in policy.Requirements.OfType<IAuthorizationHandler>())
        {
            await requirement.HandleAsync(allowed);
            await requirement.HandleAsync(denied);
        }

        allowed.HasSucceeded.Should().BeTrue();
        denied.HasFailed.Should().BeTrue();
    }

    private static ApiKeyScopePolicyProvider CreateProvider()
    {
        var services = new ServiceCollection();
        var policyStore = new Mock<IPolicyDefinitionStore>();
        policyStore.Setup(store => store.GetPolicyAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((PolicyDefinition?)null);
        services.AddSingleton(policyStore.Object);
        var versionStore = new Mock<ITenantSecurityVersionStore>();
        versionStore.Setup(store => store.GetVersionAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);
        services.AddSingleton(versionStore.Object);
        var providerRoot = services.BuildServiceProvider();

        var fallback = new DbAuthorizationPolicyProvider(
            Options.Create(new AuthorizationOptions()),
            Mock.Of<IPolicyCache>(),
            Mock.Of<IPolicyMerger>(),
            providerRoot.GetRequiredService<IServiceScopeFactory>(),
            Options.Create(new TenancyOptions()),
            NullLogger<DbAuthorizationPolicyProvider>.Instance);
        return new ApiKeyScopePolicyProvider(fallback);
    }
}

public sealed class ApiKeyRotationEntityTests
{
    [Fact]
    public void BeginRotationGrace_KeepsKeyValidDuringWindow()
    {
        var (key, _) = ApiKey.Create(Guid.NewGuid(), Guid.NewGuid(), "key", ["read"]);

        key.BeginRotationGrace(SystemClock.UtcNow.AddHours(1));

        key.RotationGraceEndsAt.Should().BeAfter(SystemClock.UtcNow);
        key.IsValid().Should().BeTrue("the rotated key is honored within the grace window");
    }

    [Fact]
    public void IsValid_FailsClosed_AfterGraceExpires()
    {
        var (key, _) = ApiKey.Create(Guid.NewGuid(), Guid.NewGuid(), "key", ["read"]);

        key.BeginRotationGrace(SystemClock.UtcNow.AddMinutes(-1));

        key.IsValid().Should().BeFalse("the old key must stop working once the overlap window closes");
    }

    [Fact]
    public void FinalizeRotationRevocation_TransitionsOnlyWhenDue()
    {
        var (key, _) = ApiKey.Create(Guid.NewGuid(), Guid.NewGuid(), "key", ["read"]);

        key.FinalizeRotationRevocation().Should().BeFalse("no rotation started yet");

        key.BeginRotationGrace(SystemClock.UtcNow.AddHours(1));
        key.FinalizeRotationRevocation().Should().BeFalse("grace window still open");

        key.BeginRotationGrace(SystemClock.UtcNow.AddMinutes(-1));
        key.FinalizeRotationRevocation().Should().BeTrue("expired rotation must be recorded");
        key.RevokedAt.Should().NotBeNull();
        key.IsActive.Should().BeFalse();
        key.RevocationReason.Should().Contain("Rotated");

        key.FinalizeRotationRevocation().Should().BeFalse("already finalized");
    }

    [Fact]
    public void RotationFields_DefaultToNull()
    {
        var (key, _) = ApiKey.Create(Guid.NewGuid(), Guid.NewGuid(), "key", ["read"]);

        key.ReplacesKeyId.Should().BeNull();
        key.RotationGraceEndsAt.Should().BeNull();
        key.IsRotationGraceExpired().Should().BeFalse();
    }
}

public sealed class ApiKeyRotationHandlerTests
{
    private static ActorContext AuthenticatedActor(Guid userId, Guid? tenantId = null) => new()
    {
        ActorKind = ActorKind.User,
        SubjectId = userId.ToString(),
        TenantId = tenantId ?? Guid.NewGuid(),
        IsAuthenticated = true,
        Roles = new HashSet<string>(),
        Permissions = new HashSet<string>()
    };

    private static async Task<InMemoryApiKeyDbContext> CreateContextAsync()
    {
        var context = new InMemoryApiKeyDbContext(
            new DbContextOptionsBuilder<InMemoryApiKeyDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
                .Options);
        return await Task.FromResult(context).ConfigureAwait(false);
    }

    private static RotateApiKeyHandler CreateHandler(
        IApiKeyRepository apiKeyRepository,
        IActorContextAccessor actorAccessor,
        IApiKeyAuditEventSink? auditSink = null,
        ApiKeyLifecycleOptions? options = null) =>
        new(apiKeyRepository, actorAccessor, NullLogger<RotateApiKeyHandler>.Instance, options, auditSink);

    private static IApiKeyRepository CreateRepository(IApplicationDbContext dbContext) => new ApiKeyRepository(dbContext);

    [Fact]
    public async Task Rotate_ValidKey_IssuesReplacementLinkedToOldKey_WithDefaultGrace()
    {
        var userId = Guid.NewGuid();
        var actorAccessor = new Mock<IActorContextAccessor>();
        actorAccessor.Setup(a => a.ActorContext).Returns(AuthenticatedActor(userId));
        await using var dbContext = await CreateContextAsync();
        var (oldKey, oldPlaintext) = ApiKey.Create(userId, Guid.NewGuid(), "integration", ["reports:read"]);
        dbContext.ApiKeys.Add(oldKey);
        await dbContext.SaveChangesAsync();

        var handler = CreateHandler(CreateRepository(dbContext), actorAccessor.Object);
        var before = SystemClock.UtcNow;

        var result = await handler.Handle(new RotateApiKeyCommand { KeyId = oldKey.Id }, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.OldKeyId.Should().Be(oldKey.Id);
        result.Value.OldKeyRevoked.Should().BeFalse();
        result.Value.OldKeyGraceEndsAt.Should().NotBeNull();
        result.Value.OldKeyGraceEndsAt!.Value.Should().BeAfter(before.AddHours(23));

        var newKey = await dbContext.ApiKeys.SingleAsync(k => k.Id == result.Value.NewKey.Id);
        newKey.ReplacesKeyId.Should().Be(oldKey.Id, "rotation linkage must be persisted");
        newKey.GetScopes().Should().Equal("reports:read");
        newKey.Name.Should().Be("integration");
        newKey.ValidateKey(result.Value.NewKey.ApiKey).Should().BeTrue("the response carries the new key's plaintext");
        newKey.ValidateKey(oldPlaintext).Should().BeFalse();

        var reloadedOld = await dbContext.ApiKeys.SingleAsync(k => k.Id == oldKey.Id);
        reloadedOld.RotationGraceEndsAt.Should().NotBeNull();
        reloadedOld.IsValid().Should().BeTrue("the old key is honored within the grace window");
        reloadedOld.RevokedAt.Should().BeNull();
    }

    [Fact]
    public async Task Rotate_ZeroGrace_RevokesOldKeyImmediately()
    {
        var userId = Guid.NewGuid();
        var actorAccessor = new Mock<IActorContextAccessor>();
        actorAccessor.Setup(a => a.ActorContext).Returns(AuthenticatedActor(userId));
        await using var dbContext = await CreateContextAsync();
        var (oldKey, _) = ApiKey.Create(userId, Guid.NewGuid(), "integration", ["read"]);
        dbContext.ApiKeys.Add(oldKey);
        await dbContext.SaveChangesAsync();

        var handler = CreateHandler(CreateRepository(dbContext), actorAccessor.Object);

        var result = await handler.Handle(
            new RotateApiKeyCommand { KeyId = oldKey.Id, GracePeriod = TimeSpan.Zero }, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.OldKeyRevoked.Should().BeTrue();
        result.Value.OldKeyGraceEndsAt.Should().BeNull();

        var reloadedOld = await dbContext.ApiKeys.SingleAsync(k => k.Id == oldKey.Id);
        reloadedOld.IsValid().Should().BeFalse();
        reloadedOld.RevokedAt.Should().NotBeNull();
        reloadedOld.RevocationReason.Should().Contain("Rotated");
    }

    [Fact]
    public async Task Rotate_UsesConfiguredDefaultGrace_WhenRequestOmitsIt()
    {
        var userId = Guid.NewGuid();
        var actorAccessor = new Mock<IActorContextAccessor>();
        actorAccessor.Setup(a => a.ActorContext).Returns(AuthenticatedActor(userId));
        await using var dbContext = await CreateContextAsync();
        var (oldKey, _) = ApiKey.Create(userId, Guid.NewGuid(), "integration", ["read"]);
        dbContext.ApiKeys.Add(oldKey);
        await dbContext.SaveChangesAsync();
        var options = new ApiKeyLifecycleOptions { RotationGracePeriodMinutes = 10 };
        var before = SystemClock.UtcNow;

        var handler = CreateHandler(CreateRepository(dbContext), actorAccessor.Object, options: options);

        var result = await handler.Handle(new RotateApiKeyCommand { KeyId = oldKey.Id }, CancellationToken.None);

        result.Value.OldKeyGraceEndsAt.Should().NotBeNull();
        (result.Value.OldKeyGraceEndsAt!.Value - before).Duration().Should().BeLessThan(TimeSpan.FromMinutes(11));
        result.Value.OldKeyGraceEndsAt.Value.Should().BeAfter(before.AddMinutes(9));
    }

    [Fact]
    public async Task Rotate_ClampsRequestedGraceToConfiguredMaximum()
    {
        var userId = Guid.NewGuid();
        var actorAccessor = new Mock<IActorContextAccessor>();
        actorAccessor.Setup(a => a.ActorContext).Returns(AuthenticatedActor(userId));
        await using var dbContext = await CreateContextAsync();
        var (oldKey, _) = ApiKey.Create(userId, Guid.NewGuid(), "integration", ["read"]);
        dbContext.ApiKeys.Add(oldKey);
        await dbContext.SaveChangesAsync();
        var before = SystemClock.UtcNow;

        var handler = CreateHandler(CreateRepository(dbContext), actorAccessor.Object);

        var result = await handler.Handle(
            new RotateApiKeyCommand { KeyId = oldKey.Id, GracePeriod = TimeSpan.FromDays(60) }, CancellationToken.None);

        result.Value.OldKeyGraceEndsAt.Should().NotBeNull();
        result.Value.OldKeyGraceEndsAt!.Value.Should().BeBefore(before.AddDays(31), "grace is clamped to the configured maximum");
        result.Value.OldKeyGraceEndsAt.Value.Should().BeAfter(before.AddDays(29));
    }

    [Fact]
    public async Task Rotate_Overrides_NameScopesAndExpiry_WhenProvided()
    {
        var userId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var actorAccessor = new Mock<IActorContextAccessor>();
        actorAccessor.Setup(a => a.ActorContext).Returns(AuthenticatedActor(userId, tenantId));
        await using var dbContext = await CreateContextAsync();
        var (oldKey, _) = ApiKey.Create(
            userId, tenantId, "old-name", ["old:scope"],
            expiresAt: SystemClock.UtcNow.AddYears(1),
            ipWhitelist: "192.0.2.10");
        dbContext.ApiKeys.Add(oldKey);
        await dbContext.SaveChangesAsync();
        var newExpiry = SystemClock.UtcNow.AddMonths(6);

        var handler = CreateHandler(CreateRepository(dbContext), actorAccessor.Object);

        var result = await handler.Handle(new RotateApiKeyCommand
        {
            KeyId = oldKey.Id,
            Name = "new-name",
            Scopes = ["new:scope"],
            ExpiresAt = newExpiry
        }, CancellationToken.None);

        var newKey = await dbContext.ApiKeys.SingleAsync(k => k.Id == result.Value.NewKey.Id);
        newKey.Name.Should().Be("new-name");
        newKey.GetScopes().Should().Equal("new:scope");
        newKey.ExpiresAt.Should().Be(newExpiry);
        newKey.IpWhitelist.Should().Be("192.0.2.10", "IP restrictions are carried over (fail-closed)");
        newKey.TenantId.Should().Be(tenantId);
    }

    [Fact]
    public async Task Rotate_AlreadyRevokedKey_Fails()
    {
        var userId = Guid.NewGuid();
        var actorAccessor = new Mock<IActorContextAccessor>();
        actorAccessor.Setup(a => a.ActorContext).Returns(AuthenticatedActor(userId));
        await using var dbContext = await CreateContextAsync();
        var (oldKey, _) = ApiKey.Create(userId, Guid.NewGuid(), "integration", ["read"]);
        oldKey.Revoke("leaked");
        dbContext.ApiKeys.Add(oldKey);
        await dbContext.SaveChangesAsync();

        var handler = CreateHandler(CreateRepository(dbContext), actorAccessor.Object);

        var result = await handler.Handle(new RotateApiKeyCommand { KeyId = oldKey.Id }, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("ApiKey.Invalid");
    }

    [Fact]
    public async Task Rotate_KeyOwnedByAnotherUser_IsNotFound()
    {
        var userId = Guid.NewGuid();
        var actorAccessor = new Mock<IActorContextAccessor>();
        actorAccessor.Setup(a => a.ActorContext).Returns(AuthenticatedActor(userId));
        await using var dbContext = await CreateContextAsync();
        var (otherKey, _) = ApiKey.Create(Guid.NewGuid(), Guid.NewGuid(), "someone-elses", ["read"]);
        dbContext.ApiKeys.Add(otherKey);
        await dbContext.SaveChangesAsync();

        var handler = CreateHandler(CreateRepository(dbContext), actorAccessor.Object);

        var result = await handler.Handle(new RotateApiKeyCommand { KeyId = otherKey.Id }, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("ApiKey.NotFound");
    }

    [Fact]
    public async Task Rotate_Unauthenticated_Fails()
    {
        var actorAccessor = new Mock<IActorContextAccessor>();
        actorAccessor.Setup(a => a.ActorContext).Returns(ActorContext.Anonymous);
        await using var dbContext = await CreateContextAsync();

        var handler = CreateHandler(CreateRepository(dbContext), actorAccessor.Object);

        var result = await handler.Handle(new RotateApiKeyCommand { KeyId = Guid.NewGuid() }, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Auth.Required");
    }

    [Fact]
    public async Task Rotate_RecordsLifecycleAuditEventKeyedByNewKey()
    {
        var userId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var actorAccessor = new Mock<IActorContextAccessor>();
        actorAccessor.Setup(a => a.ActorContext).Returns(AuthenticatedActor(userId, tenantId));
        await using var dbContext = await CreateContextAsync();
        var (oldKey, _) = ApiKey.Create(userId, tenantId, "integration", ["reports:read"]);
        dbContext.ApiKeys.Add(oldKey);
        await dbContext.SaveChangesAsync();
        var auditSink = new Mock<IApiKeyAuditEventSink>();

        var handler = CreateHandler(CreateRepository(dbContext), actorAccessor.Object, auditSink.Object);

        var result = await handler.Handle(new RotateApiKeyCommand { KeyId = oldKey.Id }, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        auditSink.Verify(sink => sink.RecordAsync(
            It.Is<ApiKeyAuditEvent>(auditEvent =>
                auditEvent.ActionType == ApiKeyAuditActions.Rotated &&
                auditEvent.ApiKeyId == result.Value.NewKey.Id &&
                auditEvent.UserId == userId &&
                auditEvent.TenantId == tenantId &&
                auditEvent.Success),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Rotate_AuditSinkFailure_DoesNotFailTheRotation()
    {
        var userId = Guid.NewGuid();
        var actorAccessor = new Mock<IActorContextAccessor>();
        actorAccessor.Setup(a => a.ActorContext).Returns(AuthenticatedActor(userId));
        await using var dbContext = await CreateContextAsync();
        var (oldKey, _) = ApiKey.Create(userId, Guid.NewGuid(), "integration", ["read"]);
        dbContext.ApiKeys.Add(oldKey);
        await dbContext.SaveChangesAsync();
        var auditSink = new Mock<IApiKeyAuditEventSink>();
        auditSink
            .Setup(sink => sink.RecordAsync(It.IsAny<ApiKeyAuditEvent>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("audit transport down"));

        var handler = CreateHandler(CreateRepository(dbContext), actorAccessor.Object, auditSink.Object);

        var result = await handler.Handle(new RotateApiKeyCommand { KeyId = oldKey.Id }, CancellationToken.None);

        result.IsSuccess.Should().BeTrue("audit failures must not fail lifecycle operations");
    }

    private sealed class InMemoryApiKeyDbContext(DbContextOptions<InMemoryApiKeyDbContext> options)
        : DbContext(options), IApplicationDbContext
    {
        public DbSet<ApiKey> ApiKeys => Set<ApiKey>();

        public Task<Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction> BeginTransactionAsync(
            CancellationToken cancellationToken = default) => Database.BeginTransactionAsync(cancellationToken);
    }
}

public sealed class ApiKeyRotationAuthenticationTests
{
    [Fact]
    public async Task RotatedOldKey_IsHonoredWithinGrace_AndRejectedAfter_WithLazyRevocation()
    {
        var userId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var (oldKey, oldPlaintext) = ApiKey.Create(userId, tenantId, "integration", ["reports:read"]);
        await using var dbContext = new InMemoryApiKeyDbContext(
            new DbContextOptionsBuilder<InMemoryApiKeyDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
                .Options);
        dbContext.ApiKeys.Add(oldKey);
        await dbContext.SaveChangesAsync();

        // Within the grace window the old key still authenticates.
        oldKey.BeginRotationGrace(SystemClock.UtcNow.AddHours(1));
        await dbContext.SaveChangesAsync();
        var withinGrace = await AuthenticateAsync(dbContext, oldPlaintext);
        withinGrace.Succeeded.Should().BeTrue();

        // Grace expires: authentication fails closed and the revocation is recorded lazily.
        oldKey.BeginRotationGrace(SystemClock.UtcNow.AddMinutes(-1));
        await dbContext.SaveChangesAsync();

        var afterGrace = await AuthenticateAsync(dbContext, oldPlaintext);
        afterGrace.Succeeded.Should().BeFalse();

        var reloaded = await dbContext.ApiKeys.SingleAsync(k => k.Id == oldKey.Id);
        reloaded.RevokedAt.Should().NotBeNull("expired rotation must be finalized on use");
        reloaded.IsActive.Should().BeFalse();
    }

    private static async Task<AuthenticateResult> AuthenticateAsync(InMemoryApiKeyDbContext dbContext, string plaintext)
    {
        var options = new Mock<IOptionsMonitor<ApiKeyAuthenticationOptions>>();
        options.Setup(x => x.Get(It.IsAny<string>())).Returns(new ApiKeyAuthenticationOptions());
        options.SetupGet(x => x.CurrentValue).Returns(new ApiKeyAuthenticationOptions());
        var handler = new ApiKeyAuthenticationHandler(
            options.Object,
            NullLoggerFactory.Instance,
            System.Text.Encodings.Web.UrlEncoder.Default,
            new ApiKeyRepository(dbContext));
        var context = new Microsoft.AspNetCore.Http.DefaultHttpContext();
        context.Request.Headers["X-API-Key"] = plaintext;
        await handler.InitializeAsync(
            new AuthenticationScheme(
                ApiKeyAuthenticationOptions.SchemeName,
                ApiKeyAuthenticationOptions.SchemeName,
                typeof(ApiKeyAuthenticationHandler)),
            context);
        return await handler.AuthenticateAsync();
    }

    private sealed class InMemoryApiKeyDbContext(DbContextOptions<InMemoryApiKeyDbContext> options)
        : DbContext(options), IApplicationDbContext
    {
        public DbSet<ApiKey> ApiKeys => Set<ApiKey>();

        public Task<Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction> BeginTransactionAsync(
            CancellationToken cancellationToken = default) => Database.BeginTransactionAsync(cancellationToken);
    }
}

public sealed class ApiKeyLifecycleAuditTests
{
    private static ActorContext AuthenticatedActor(Guid userId) => new()
    {
        ActorKind = ActorKind.User,
        SubjectId = userId.ToString(),
        TenantId = Guid.NewGuid(),
        IsAuthenticated = true,
        Roles = new HashSet<string>(),
        Permissions = new HashSet<string>()
    };

    [Fact]
    public async Task CreateApiKey_RecordsCreatedAuditEventKeyedById()
    {
        var userId = Guid.NewGuid();
        var actorAccessor = new Mock<IActorContextAccessor>();
        actorAccessor.Setup(a => a.ActorContext).Returns(AuthenticatedActor(userId));
        var repository = new Mock<IApiKeyRepository>();
        ApiKey? added = null;
        repository
            .Setup(r => r.AddAsync(It.IsAny<ApiKey>(), It.IsAny<CancellationToken>()))
            .Callback<ApiKey, CancellationToken>((key, _) => added = key)
            .ReturnsAsync((ApiKey key, CancellationToken _) => key);
        var auditSink = new Mock<IApiKeyAuditEventSink>();

        var handler = new CreateApiKeyHandler(repository.Object, actorAccessor.Object,
            NullLogger<CreateApiKeyHandler>.Instance, auditSink.Object);

        var result = await handler.Handle(
            new CreateApiKeyCommand { Name = "reporting", Scopes = ["reports:read"] }, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        auditSink.Verify(sink => sink.RecordAsync(
            It.Is<ApiKeyAuditEvent>(auditEvent =>
                auditEvent.ActionType == ApiKeyAuditActions.Created &&
                auditEvent.ApiKeyId == added!.Id &&
                auditEvent.UserId == userId &&
                auditEvent.Success),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task RevokeApiKey_RecordsRevokedAuditEventKeyedById()
    {
        var userId = Guid.NewGuid();
        var actorAccessor = new Mock<IActorContextAccessor>();
        actorAccessor.Setup(a => a.ActorContext).Returns(AuthenticatedActor(userId));
        var (key, _) = ApiKey.Create(userId, Guid.NewGuid(), "integration", ["read"]);
        var repository = new Mock<IApiKeyRepository>();
        repository
            .Setup(r => r.RevokeAsync(key.Id, userId, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(key);
        var auditSink = new Mock<IApiKeyAuditEventSink>();

        var handler = new RevokeApiKeyHandler(repository.Object, actorAccessor.Object,
            NullLogger<RevokeApiKeyHandler>.Instance, auditSink.Object);

        var result = await handler.Handle(
            new RevokeApiKeyCommand { KeyId = key.Id, Reason = "rotated" }, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        auditSink.Verify(sink => sink.RecordAsync(
            It.Is<ApiKeyAuditEvent>(auditEvent =>
                auditEvent.ActionType == ApiKeyAuditActions.Revoked &&
                auditEvent.ApiKeyId == key.Id &&
                auditEvent.UserId == userId &&
                auditEvent.Success),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
