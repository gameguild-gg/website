using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using GameGuild.Compliance.Audit;
using GameGuild.Identity.Authorization;
using GameGuild.Identity.Context.Actors;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace GameGuild.Commerce.Billing.UnitTests.Security;

/// <summary>
///     Webhook signing-secret rotation (issue #408): the rotation entity's dual-secret
///     window, the signing-secret provider's during/after-window resolution with automatic
///     deprecation, the Stripe verifier accepting either secret during the window and only
///     the new one after it, the rotation command's fail-closed contract, and the admin
///     authorization of the rotation endpoint.
/// </summary>
public class WebhookSecretRotationTests
{
    private const string ConfigSecret = "whsec_config_secret_value";
    private const string NewSecret = "whsec_rotated_secret_value_2";
    private const string ApiVersion = "2023-10-16";

    private sealed class RotationTestDbContext(DbContextOptions<RotationTestDbContext> options)
        : DbContext(options), IApplicationDbContext
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.ApplyConfiguration(new WebhookSecretRotationConfiguration());
            base.OnModelCreating(modelBuilder);
        }

        public Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
            => Database.BeginTransactionAsync(cancellationToken);
    }

    private static async Task<RotationTestDbContext> CreateContextAsync()
    {
        var context = new RotationTestDbContext(
            new DbContextOptionsBuilder<RotationTestDbContext>()
                .UseInMemoryDatabase($"webhook-rotations-{Guid.NewGuid():N}")
                .Options);
        await context.Database.EnsureCreatedAsync();
        return context;
    }

    private static BillingConfiguration Configuration(int windowHours = 24) => new()
    {
        Stripe = new StripeSettings
        {
            WebhookSecret = ConfigSecret,
            WebhookEndpointId = "we_rotation_test",
            AccountId = "acct_platform",
            ApiVersion = ApiVersion,
            WebhookToleranceSeconds = 300
        },
        Webhook = new WebhookSettings { SecretRotationWindowHours = windowHours }
    };

    private static (IDataProtectionProvider Provider, Func<string, string> RoundTrip) DataProtection()
    {
        var provider = new EphemeralDataProtectionProvider();
        return (provider, value =>
        {
            var protector = provider.CreateProtector(WebhookSigningSecretProvider.ProtectionPurpose);
            return protector.Unprotect(protector.Protect(value));
        });
    }

    private static WebhookSigningSecretProvider CreateProvider(
        IApplicationDbContext context,
        BillingConfiguration? configuration = null,
        IDataProtectionProvider? protection = null)
    {
        var serviceProvider = new Mock<IServiceProvider>();
        serviceProvider
            .Setup(provider => provider.GetService(typeof(IApplicationDbContext)))
            .Returns(context);
        var scope = new Mock<IServiceScope>();
        scope.SetupGet(innerScope => innerScope.ServiceProvider).Returns(serviceProvider.Object);
        var scopeFactory = new Mock<IServiceScopeFactory>();
        scopeFactory
            .Setup(factory => factory.CreateScope())
            .Returns(scope.Object);

        return new WebhookSigningSecretProvider(
            scopeFactory.Object,
            protection ?? new EphemeralDataProtectionProvider(),
            Options.Create(configuration ?? Configuration()),
            Mock.Of<ILogger<WebhookSigningSecretProvider>>());
    }

    [Fact]
    public void RotationEntity_TracksTheDualSecretWindow()
    {
        var rotatedAt = new DateTime(2026, 10, 14, 0, 0, 0, DateTimeKind.Utc);
        var rotation = new WebhookSecretRotation(
            "stripe",
            "enc-prev",
            "enc-active",
            rotatedAt,
            rotatedAt.AddHours(24),
            Guid.NewGuid());

        rotation.IsPreviousSecretValid(rotatedAt.AddHours(23)).Should().BeTrue();
        rotation.IsPreviousSecretValid(rotatedAt.AddHours(25)).Should().BeFalse();
        rotation.DeprecatedAtUtc.Should().BeNull();

        rotation.MarkDeprecated(rotatedAt.AddHours(25));
        rotation.DeprecatedAtUtc.Should().NotBeNull();
        rotation.IsPreviousSecretValid(rotatedAt.AddHours(1)).Should().BeFalse(
            "a deprecated rotation never re-admits its previous secret, even inside the historical window");

        var alreadyStamped = rotation.DeprecatedAtUtc;
        rotation.MarkDeprecated(rotatedAt.AddHours(30));
        rotation.DeprecatedAtUtc.Should().Be(alreadyStamped, "deprecation stamping is idempotent");
    }

    [Fact]
    public void RotationEntity_FailsClosed_OnBadWindow()
    {
        var now = SystemClock.UtcNow;
        var act = () => new WebhookSecretRotation("stripe", "enc-prev", "enc-active", now, now.AddHours(-1), null);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public async Task Provider_ReturnsBothSecrets_DuringTheWindow_AndConfigSecret()
    {
        var (protection, roundTrip) = DataProtection();
        await using var context = await CreateContextAsync();
        context.Set<WebhookSecretRotation>().Add(new WebhookSecretRotation(
            "stripe",
            protection.CreateProtector(WebhookSigningSecretProvider.ProtectionPurpose).Protect("whsec_previous_secret"),
            protection.CreateProtector(WebhookSigningSecretProvider.ProtectionPurpose).Protect(NewSecret),
            SystemClock.UtcNow.AddHours(-1),
            SystemClock.UtcNow.AddHours(23),
            Guid.NewGuid()));
        await context.SaveChangesAsync();

        var provider = CreateProvider(context, protection: protection);

        var secrets = provider.GetValidSecrets("stripe");

        secrets.Should().BeEquivalentTo(new[] { NewSecret, "whsec_previous_secret", ConfigSecret });
        roundTrip(NewSecret).Should().Be(NewSecret);
    }

    [Fact]
    public async Task Provider_RejectsThePreviousSecret_AfterTheWindow_AndStampsDeprecation()
    {
        var (protection, _) = DataProtection();
        await using var context = await CreateContextAsync();
        var rotation = new WebhookSecretRotation(
            "stripe",
            protection.CreateProtector(WebhookSigningSecretProvider.ProtectionPurpose).Protect("whsec_previous_secret"),
            protection.CreateProtector(WebhookSigningSecretProvider.ProtectionPurpose).Protect(NewSecret),
            SystemClock.UtcNow.AddHours(-25),
            SystemClock.UtcNow.AddHours(-1),
            Guid.NewGuid());
        context.Set<WebhookSecretRotation>().Add(rotation);
        await context.SaveChangesAsync();

        var provider = CreateProvider(context, protection: protection);

        var secrets = provider.GetValidSecrets("stripe");

        secrets.Should().BeEquivalentTo(new[] { NewSecret, ConfigSecret },
            "the previous secret is automatically deprecated once the window has lapsed");
        secrets.Should().NotContain("whsec_previous_secret");

        var stamped = await context.Set<WebhookSecretRotation>().SingleAsync();
        stamped.DeprecatedAtUtc.Should().NotBeNull();
    }

    [Fact]
    public async Task Provider_FallsBackToConfigSecret_WhenNoRotationExists()
    {
        await using var context = await CreateContextAsync();
        var provider = CreateProvider(context);

        var secrets = provider.GetValidSecrets("stripe");

        secrets.Should().BeEquivalentTo(new[] { ConfigSecret });
    }

    [Fact]
    public async Task Verifier_AcceptsEitherSecret_DuringTheWindow_AndOnlyNewAfterIt()
    {
        var (protection, _) = DataProtection();
        var payload = CreateInvoicePayload("evt_rotation");

        // Rotation whose window is still open.
        await using var openWindowContext = await CreateContextAsync();
        openWindowContext.Set<WebhookSecretRotation>().Add(new WebhookSecretRotation(
            "stripe",
            protection.CreateProtector(WebhookSigningSecretProvider.ProtectionPurpose).Protect("whsec_previous_secret"),
            protection.CreateProtector(WebhookSigningSecretProvider.ProtectionPurpose).Protect(NewSecret),
            SystemClock.UtcNow.AddHours(-1),
            SystemClock.UtcNow.AddHours(23),
            Guid.NewGuid()));
        await openWindowContext.SaveChangesAsync();

        var duringWindow = CreateVerifier(CreateProvider(openWindowContext, protection: protection));
        duringWindow.Verify(payload, Sign(payload, NewSecret)).EventId.Should().Be("evt_rotation");
        duringWindow.Verify(payload, Sign(payload, "whsec_previous_secret")).EventId.Should().Be("evt_rotation");
        duringWindow.Verify(payload, Sign(payload, ConfigSecret)).EventId.Should().Be("evt_rotation");

        // Rotation whose window has lapsed: the previous secret must now be rejected.
        await using var lapsedContext = await CreateContextAsync();
        lapsedContext.Set<WebhookSecretRotation>().Add(new WebhookSecretRotation(
            "stripe",
            protection.CreateProtector(WebhookSigningSecretProvider.ProtectionPurpose).Protect("whsec_previous_secret"),
            protection.CreateProtector(WebhookSigningSecretProvider.ProtectionPurpose).Protect(NewSecret),
            SystemClock.UtcNow.AddHours(-25),
            SystemClock.UtcNow.AddHours(-1),
            Guid.NewGuid()));
        await lapsedContext.SaveChangesAsync();

        var afterWindow = CreateVerifier(CreateProvider(lapsedContext, protection: protection));
        afterWindow.Verify(payload, Sign(payload, NewSecret)).EventId.Should().Be("evt_rotation");

        var rejected = () => afterWindow.Verify(payload, Sign(payload, "whsec_previous_secret"));
        rejected.Should().Throw<InvalidWebhookSignatureException>();
    }

    [Fact]
    public async Task RotateCommand_RecordsEncryptedRotation_AndAudits()
    {
        await using var context = await CreateContextAsync();
        var (protection, _) = DataProtection();
        var configuration = Configuration(windowHours: 24);
        var provider = CreateProvider(context, configuration, protection);
        var auditService = new Mock<IAuditService>();
        var accessor = new Mock<IActorContextAccessor>();
        accessor.SetupGet(value => value.ActorContext).Returns(new ActorContext
        {
            ActorKind = ActorKind.User,
            SubjectId = Guid.NewGuid().ToString(),
            TenantId = Guid.NewGuid(),
            Roles = new HashSet<string>(),
            Permissions = new HashSet<string>(),
            TypedAttributes = ActorAttributes.Empty,
            AuthScheme = "Bearer",
            IsAuthenticated = true
        });

        var handler = new RotateWebhookSecretCommandHandler(
            context,
            provider,
            protection,
            Options.Create(configuration),
            accessor.Object,
            Mock.Of<IAuditService>(),
            Mock.Of<ILogger<RotateWebhookSecretCommandHandler>>());

        var result = await handler.Handle(
            new RotateWebhookSecretCommand("stripe", NewSecret),
            CancellationToken.None);

        result.ProviderKey.Should().Be("stripe");
        result.PreviousSecretValidUntilUtc.Should().BeOnOrAfter(SystemClock.UtcNow.AddHours(23));
        result.PreviousSecretStillAccepted.Should().BeTrue();

        var rotation = await context.Set<WebhookSecretRotation>().SingleAsync();
        rotation.EncryptedActiveSecret.Should().NotBe(NewSecret, "secrets are stored encrypted at rest");
        rotation.EncryptedPreviousSecret.Should().NotBe(ConfigSecret);

        auditService.Verify(
            service => service.LogAsync(It.Is<CreateAuditLogRequest>(request =>
                request.ActionType == AuditActionTypes.BillingWebhookSecretRotated
                && request.ResourceType == "WebhookSecretRotation")),
            Times.Once);
    }

    [Fact]
    public async Task RotateCommand_FailsClosed_WhenTheNewSecretMatchesTheEffectiveOne()
    {
        await using var context = await CreateContextAsync();
        var (protection, _) = DataProtection();
        var configuration = Configuration();
        var provider = CreateProvider(context, configuration, protection);
        var accessor = new Mock<IActorContextAccessor>();
        accessor.SetupGet(value => value.ActorContext).Returns(new ActorContext
        {
            ActorKind = ActorKind.User,
            SubjectId = Guid.NewGuid().ToString(),
            TenantId = Guid.NewGuid(),
            Roles = new HashSet<string>(),
            Permissions = new HashSet<string>(),
            TypedAttributes = ActorAttributes.Empty,
            AuthScheme = "Bearer",
            IsAuthenticated = true
        });

        var handler = new RotateWebhookSecretCommandHandler(
            context,
            provider,
            protection,
            Options.Create(configuration),
            accessor.Object,
            Mock.Of<IAuditService>(),
            Mock.Of<ILogger<RotateWebhookSecretCommandHandler>>());

        var act = () => handler.Handle(new RotateWebhookSecretCommand("stripe", ConfigSecret), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
        (await context.Set<WebhookSecretRotation>().CountAsync()).Should().Be(0);
    }

    [Fact]
    public void RotationEndpoint_RequiresTheSystemAdminPolicy()
    {
        var controllerType = typeof(BillingWebhooksController);
        var method = controllerType.GetMethod("RotateSecret");

        var authorize = method!.GetCustomAttributes(typeof(Microsoft.AspNetCore.Authorization.AuthorizeAttribute), inherit: true)
            .Cast<Microsoft.AspNetCore.Authorization.AuthorizeAttribute>()
            .Single();

        authorize.Policy.Should().Be(Policies.SystemAdmin);
    }

    private static StripeWebhookVerifier CreateVerifier(IWebhookSigningSecretProvider secretProvider) =>
        new(
            Options.Create(Configuration()),
            secretProvider);

    private static string CreateInvoicePayload(string eventId) =>
        JsonSerializer.Serialize(new
        {
            id = eventId,
            @object = "event",
            api_version = ApiVersion,
            created = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            data = new
            {
                @object = new
                {
                    id = "in_rotation",
                    @object = "invoice",
                    subscription = "sub_rotation",
                    payment_intent = "pi_rotation",
                    amount_paid = 1000,
                    currency = "usd"
                }
            },
            livemode = false,
            pending_webhooks = 1,
            request = new { id = (string?)null, idempotency_key = (string?)null },
            type = "invoice.payment_succeeded",
            account = (string?)null
        });

    private static string Sign(string payload, string secret)
    {
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var signedPayload = $"{timestamp}.{payload}";
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var signature = Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(signedPayload))).ToLowerInvariant();
        return $"t={timestamp},v1={signature}";
    }
}
