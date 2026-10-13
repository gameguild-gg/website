using FluentAssertions;
using GameGuild.Commerce.Billing;
using GameGuild.Commerce.Subscriptions;
using GameGuild.Compliance.Audit;
using GameGuild.Identity.Context.Actors;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace GameGuild.Commerce.Billing.UnitTests.Commands;

public class MigrateBillingProviderCommandHandlerTests
{
    private readonly Mock<ISubscriptionRepository> _subscriptionRepository = new(MockBehavior.Strict);
    private readonly Mock<IAuditService> _auditService = new();
    private readonly Mock<IActorContextAccessor> _actorContextAccessor = new();

    private MigrateBillingProviderCommandHandler CreateHandler() => new(
        _subscriptionRepository.Object,
        _actorContextAccessor.Object,
        _auditService.Object,
        NullLogger<MigrateBillingProviderCommandHandler>.Instance);

    public MigrateBillingProviderCommandHandlerTests()
    {
        _actorContextAccessor
            .SetupGet(accessor => accessor.ActorContext)
            .Returns(new ActorContext
            {
                ActorKind = ActorKind.User,
                SubjectId = Guid.NewGuid().ToString(),
                Roles = new HashSet<string>(["SystemAdmin"]),
                Permissions = new HashSet<string>(),
                TypedAttributes = ActorAttributes.Empty,
                AuthScheme = "Test",
                IsAuthenticated = true
            });
    }

    private static Subscription CreateSubscription(string? externalId)
    {
        var subscription = new Subscription(
            tenantId: Guid.NewGuid(),
            planId: Guid.NewGuid(),
            createdByUserId: Guid.NewGuid(),
            billingCycle: BillingCycle.Monthly,
            amount: new Money(10m, "USD"),
            startDate: SystemClock.UtcNow);

        if (externalId is not null)
        {
            subscription.SetExternalIds(externalId, null);
        }

        return subscription;
    }

    private void SetupPages(params Subscription[][] pages)
    {
        var sequence = _subscriptionRepository
            .SetupSequence(repository => repository.GetPagedAsync(
                It.IsAny<int>(),
                It.IsAny<int>(),
                null,
                null,
                null,
                It.IsAny<CancellationToken>()));

        var total = pages.Sum(page => page.Length);
        var skip = 0;
        foreach (var page in pages)
        {
            var capturedSkip = skip;
            sequence.ReturnsAsync(new PagedResult<Subscription>(page, total, capturedSkip, page.Length));
            skip += page.Length;
        }
    }

    [Fact]
    public async Task Handle_Should_Classify_Subscription_Bindings_Correctly()
    {
        var stripeBound = CreateSubscription("sub_source_1");
        var alreadyOnPayPal = CreateSubscription("I-TARGET1");
        var appleBound = CreateSubscription("987654321");
        var googleToken = CreateSubscription("goog.opaque.token");
        var withoutExternalId = CreateSubscription(null);

        SetupPages([stripeBound, alreadyOnPayPal, appleBound, googleToken, withoutExternalId]);

        var report = await CreateHandler().Handle(
            new MigrateBillingProviderCommand(PaymentProviders.Stripe, PaymentProviders.PayPal),
            CancellationToken.None);

        report.SourceProvider.Should().Be(PaymentProviders.Stripe);
        report.TargetProvider.Should().Be(PaymentProviders.PayPal);
        report.DryRun.Should().BeTrue();
        report.MutationApplied.Should().BeFalse();
        report.TotalSubscriptionsScanned.Should().Be(5);

        report.BoundToSourceMissingTargetExternalIdCount.Should().Be(1);
        report.AlreadyOnTargetProviderCount.Should().Be(1);
        report.UnattributedExternalIdCount.Should().Be(2); // numeric Apple id + opaque Google token
        report.NotExternallyBoundCount.Should().Be(1);

        report.Entries.Should().Contain(entry =>
            entry.SubscriptionId == stripeBound.Id &&
            entry.Classification == BillingProviderMigrationEntryClassification.BoundToSourceMissingTargetExternalId);
        report.Entries.Should().Contain(entry =>
            entry.SubscriptionId == alreadyOnPayPal.Id &&
            entry.Classification == BillingProviderMigrationEntryClassification.AlreadyOnTargetProvider);
        report.Entries.Should().Contain(entry =>
            entry.SubscriptionId == withoutExternalId.Id &&
            entry.Classification == BillingProviderMigrationEntryClassification.NotExternallyBound);
    }

    [Fact]
    public async Task Handle_Should_Classify_Numeric_Apple_Identifiers_As_Target_When_Migrating_To_Apple()
    {
        var appleBound = CreateSubscription("987654321");
        SetupPages([appleBound]);

        var report = await CreateHandler().Handle(
            new MigrateBillingProviderCommand(PaymentProviders.Stripe, PaymentProviders.AppleAppStore),
            CancellationToken.None);

        report.AlreadyOnTargetProviderCount.Should().Be(1);
        report.BoundToSourceMissingTargetExternalIdCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_Should_Be_Report_Only_No_Subscription_Mutations()
    {
        // Strict mock: any repository member other than GetPagedAsync (notably the
        // write paths) makes the test fail.
        SetupPages([CreateSubscription("sub_1")]);

        var report = await CreateHandler().Handle(
            new MigrateBillingProviderCommand(PaymentProviders.Stripe, PaymentProviders.PayPal),
            CancellationToken.None);

        report.MutationApplied.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_Should_Scan_All_Pages()
    {
        // The handler pages with a fixed page size of 200; a full first page forces a second read.
        var firstPage = Enumerable.Range(0, 200).Select(_ => CreateSubscription("sub_1")).ToArray();
        var secondPage = new[] { CreateSubscription("sub_2"), CreateSubscription(null) };
        SetupPages(firstPage, secondPage);

        var report = await CreateHandler().Handle(
            new MigrateBillingProviderCommand(PaymentProviders.Stripe, PaymentProviders.PayPal),
            CancellationToken.None);

        report.TotalSubscriptionsScanned.Should().Be(202);
        report.BoundToSourceMissingTargetExternalIdCount.Should().Be(201);
        report.NotExternallyBoundCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_Should_Write_An_Audit_Event_With_The_Report_Counts()
    {
        SetupPages([CreateSubscription("sub_1"), CreateSubscription(null)]);

        await CreateHandler().Handle(
            new MigrateBillingProviderCommand(PaymentProviders.Stripe, PaymentProviders.PayPal),
            CancellationToken.None);

        _auditService.Verify(
            service => service.LogAsync(
                It.Is<CreateAuditLogRequest>(request =>
                    request.ActionType == AuditActionTypes.BillingProviderMigrationDryRun &&
                    request.ResourceId == "stripe->paypal" &&
                    request.Success)),
            Times.Once);
    }

    [Fact]
    public async Task Handle_Should_Return_The_Report_Even_When_Audit_Delivery_Fails()
    {
        _auditService
            .Setup(service => service.LogAsync(It.IsAny<CreateAuditLogRequest>()))
            .ThrowsAsync(new InvalidOperationException("audit pipeline down"));
        SetupPages([CreateSubscription("sub_1")]);

        var report = await CreateHandler().Handle(
            new MigrateBillingProviderCommand(PaymentProviders.Stripe, PaymentProviders.PayPal),
            CancellationToken.None);

        report.BoundToSourceMissingTargetExternalIdCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_Should_Be_Robust_To_Repositories_Reporting_Zero_TotalCount()
    {
        // Some repository implementations only populate Items reliably.
        _subscriptionRepository
            .Setup(repository => repository.GetPagedAsync(
                It.IsAny<int>(),
                It.IsAny<int>(),
                null,
                null,
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PagedResult<Subscription>([CreateSubscription("sub_1"), CreateSubscription("I-1")], 0, 0, 2));

        var report = await CreateHandler().Handle(
            new MigrateBillingProviderCommand(PaymentProviders.Stripe, PaymentProviders.PayPal),
            CancellationToken.None);

        report.TotalSubscriptionsScanned.Should().Be(2);
    }
}

public class MigrateBillingProviderCommandValidatorTests
{
    private readonly MigrateBillingProviderCommandValidator _validator = new();

    [Fact]
    public void Validate_Should_Fail_Closed_For_Unknown_Source_Provider()
    {
        var result = _validator.Validate(new MigrateBillingProviderCommand("chargebee", PaymentProviders.Stripe));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(error => error.PropertyName == "SourceProvider");
    }

    [Fact]
    public void Validate_Should_Fail_Closed_For_Unknown_Target_Provider()
    {
        var result = _validator.Validate(new MigrateBillingProviderCommand(PaymentProviders.Stripe, "recurly"));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(error => error.PropertyName == "TargetProvider");
    }

    [Fact]
    public void Validate_Should_Reject_Identical_Source_And_Target()
    {
        var result = _validator.Validate(new MigrateBillingProviderCommand(PaymentProviders.Stripe, "Stripe"));

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validate_Should_Accept_Distinct_Supported_Providers()
    {
        var result = _validator.Validate(new MigrateBillingProviderCommand(PaymentProviders.Stripe, PaymentProviders.PayPal));

        result.IsValid.Should().BeTrue();
    }
}
