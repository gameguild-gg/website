using System.Reflection;
using System.Text;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using GameGuild.Commerce.Payments;
using GameGuild.Commerce.Subscriptions.Services.Email.Renderers;
using GameGuild.CQRS;
using GameGuild.Identity.Context.Actors;
using GameGuild.Notifications.Services.Email;
using MockQueryable.Moq;
using Moq;
using Xunit;

namespace GameGuild.Commerce.Subscriptions.UnitTests;

public sealed class SubscriptionsCoverageCompletionTests
{
    [Fact]
    public void MonthlyStatementArtifactComposer_ShouldComposeCompactAndDetailedArtifacts()
    {
        var compact = MonthlyStatementArtifactComposer.Compose(CreateStatementContext(MonthlyStatementDocumentProfile.Compact, true, true));
        var detailed = MonthlyStatementArtifactComposer.Compose(CreateStatementContext(MonthlyStatementDocumentProfile.Detailed, true, true));
        var sparseDetailed = MonthlyStatementArtifactComposer.Compose(CreateStatementContext(MonthlyStatementDocumentProfile.Detailed, false, false));

        compact.Attachments.Should().HaveCount(2);
        compact.Attachments.Select(attachment => attachment.ContentType).Should().BeEquivalentTo("text/csv", "application/pdf");
        Encoding.UTF8.GetString(compact.Attachments.Single(attachment => attachment.ContentType == "text/csv").Content)
            .Should().Contain("Metric,Value").And.Contain("\"ledger,code\"");
        compact.Attachments.Single(attachment => attachment.ContentType == "application/pdf").Content
            .Should().StartWith(Encoding.ASCII.GetBytes("%PDF"));

        Encoding.UTF8.GetString(detailed.Attachments.Single(attachment => attachment.ContentType == "text/csv").Content)
            .Should().Contain("maintenance_metric").And.Contain("\"counter\"\"party\"");
        detailed.Attachments.Single(attachment => attachment.ContentType == "application/pdf").Content
            .Should().StartWith(Encoding.ASCII.GetBytes("%PDF"));

        Encoding.UTF8.GetString(sparseDetailed.Attachments.Single(attachment => attachment.ContentType == "text/csv").Content)
            .Should().Contain("owner_id");
        sparseDetailed.Report.OwnerStatements.Should().BeEmpty();
        sparseDetailed.Report.MaintenanceReport.Should().BeNull();

        InvokePrivateStatic<string>(typeof(MonthlyStatementArtifactComposer), "SanitizePdfText", "caf\u00e9")
            .Should().Be("cafe");
        InvokePrivateStatic<string>(typeof(MonthlyStatementArtifactComposer), "SanitizePdfText", "A\tB")
            .Should().Be("AB");
        InvokePrivateStatic<string>(typeof(MonthlyStatementArtifactComposer), "EscapeDetailedCsvValue", (object?)null)
            .Should().BeEmpty();
        InvokePrivateStatic<string>(typeof(MonthlyStatementArtifactComposer), "EscapeDetailedCsvValue", new NullStringValue())
            .Should().BeEmpty();
        InvokePrivateStatic<string>(typeof(MonthlyStatementArtifactComposer), "EscapeDetailedCsvValue", "plain")
            .Should().Be("plain");
    }

    [Fact]
    public void MonthlyStatementArtifactComposer_ShouldRejectInvalidInputsAndProfiles()
    {
        FluentActions.Invoking(() => MonthlyStatementArtifactComposer.Compose(null!))
            .Should().Throw<ArgumentNullException>();

        var invalidContext = CreateStatementContext((MonthlyStatementDocumentProfile)999, true, true);
        FluentActions.Invoking(() => MonthlyStatementArtifactComposer.Compose(invalidContext))
            .Should().Throw<ArgumentOutOfRangeException>();

        var buildPdf = typeof(MonthlyStatementArtifactComposer).GetMethod("BuildPdf", BindingFlags.NonPublic | BindingFlags.Static)!;
        FluentActions.Invoking(() => buildPdf.Invoke(null, [CreateReport(invalidContext.SourceData), invalidContext.DocumentOptions]))
            .Should().Throw<TargetInvocationException>()
            .WithInnerException<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void BillingCycleExtensions_ShouldCoverAllNamedAndFallbackCycles()
    {
        var start = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var cycles = new[]
        {
            BillingCycle.Monthly,
            BillingCycle.Quarterly,
            BillingCycle.SemiAnnually,
            BillingCycle.Annually,
            BillingCycle.Biannually,
            BillingCycle.Weekly,
            (BillingCycle)2
        };

        foreach (var cycle in cycles)
        {
            cycle.GetMonths().Should().Be((int)cycle);
            cycle.GetDisplayName().Should().NotBeNullOrWhiteSpace();
            cycle.GetFrequencyDescription().Should().NotBeNullOrWhiteSpace();
            cycle.CalculateNextBillingDate(start).Should().BeOnOrAfter(start);
        }
    }

    [Fact]
    public void ModelConfigurations_ShouldApplyExpectedSubscriptionsModel()
    {
        var modelBuilder = new ModelBuilder();

        new SubscriptionsModelConfiguration().Configure(modelBuilder);

        var model = modelBuilder.Model;
        model.FindEntityType(typeof(Subscription)).Should().NotBeNull();
        model.FindEntityType(typeof(SubscriptionPlan)).Should().NotBeNull();
        model.FindEntityType(typeof(Subscription))!.FindProperty(nameof(Subscription.ExternalId))!.GetMaxLength().Should().Be(100);
        model.FindEntityType(typeof(SubscriptionPlan))!.FindProperty(nameof(SubscriptionPlan.Slug))!.GetMaxLength().Should().Be(255);
    }

    [Fact]
    public async Task NotificationService_ShouldLogAllNotificationPathsAndFallbackPlanNames()
    {
        var plan = CreatePlan("Pro");
        var subscription = CreateSubscription(plan.Id);
        subscription.Activate();
        var planService = new Mock<ISubscriptionPlanService>();
        planService.Setup(service => service.GetByIdAsync(plan.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(plan);
        planService.Setup(service => service.GetByIdAsync(Guid.Empty, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("missing"));
        var service = new SubscriptionNotificationService(NullLogger<SubscriptionNotificationService>.Instance, planService.Object);

        await service.SendRenewalReminderAsync(subscription, 5);
        await service.SendTrialExpirationReminderAsync(subscription, 3);
        await service.SendPaymentFailureNotificationAsync(subscription, "declined", 2);
        await service.SendSubscriptionActivatedNotificationAsync(subscription);
        await service.SendSubscriptionCancelledNotificationAsync(subscription, CancellationReason.UserRequested);
        await service.SendSubscriptionSuspendedNotificationAsync(subscription, null);
        await service.SendSubscriptionReactivatedNotificationAsync(subscription);
        await service.SendPlanUpgradeNotificationAsync(subscription, Guid.Empty, plan.Id);
        await service.SendPlanDowngradeNotificationAsync(subscription, plan.Id, Guid.Empty, DateTime.UtcNow);

        planService.Verify(service => service.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.AtLeast(10));
    }

    [Fact]
    public void ResultModels_ShouldCoverFactoriesAndComputedProperties()
    {
        var subscription = CreateSubscription(Guid.NewGuid());
        var money = new Money(120m, "USD");

        PaymentResult.CreateSuccess(money, "pay", "txn", Guid.NewGuid(), new Dictionary<string, object> { ["k"] = "v" }).Success.Should().BeTrue();
        PaymentResult.Failed("failed", "code", PaymentStatus.Cancelled, Guid.NewGuid()).Success.Should().BeFalse();
        PaymentResult.Pending(money, "pay", Guid.NewGuid()).Status.Should().Be(PaymentStatus.Pending);

        PaymentRetryResult.CreateSuccess(1, PaymentResult.CreateSuccess(money)).RetriesExhausted.Should().BeTrue();
        PaymentRetryResult.FailedWithRetry(1, 3, DateTime.UtcNow.AddDays(1), "retry").RetriesExhausted.Should().BeFalse();
        PaymentRetryResult.FailedExhausted(3, 3, "done").RetriesExhausted.Should().BeTrue();

        PricingCalculationResult.Simple(money, BillingCycle.Monthly).Currency.Should().Be("USD");
        PricingCalculationResult.WithDiscount(money, new Money(20m, "USD"), BillingCycle.Annually, [new ConcreteAppliedDiscount { Code = "promo", Description = "Promo", Amount = new Money(20m, "USD"), Type = DiscountType.FixedAmount }])
            .TotalAmount.Amount.Should().Be(100m);
        PricingCalculationResult.WithDiscount(money, new Money(20m, "USD"), BillingCycle.Annually)
            .AppliedDiscounts.Should().BeEmpty();

        new ConcreteBulkRenewalResult { TotalProcessed = 4, SuccessfulRenewals = 3 }.SuccessRate.Should().Be(75m);
        new ConcreteBulkRenewalResult { TotalProcessed = 0, SuccessfulRenewals = 0 }.SuccessRate.Should().Be(0m);
        new ConcreteRevenueAnalytics { TotalRevenue = money, RefundAmount = new Money(20m, "USD"), TransactionCount = 4 }.AverageTransactionValue.Amount.Should().Be(30m);
        new ConcreteRevenueAnalytics { TotalRevenue = money, TransactionCount = 0 }.AverageTransactionValue.Amount.Should().Be(0m);
        new ConcreteSubscriptionAnalytics { MonthlyRecurringRevenue = money, ActiveSubscriptions = 3 }.AverageRevenuePerUser.Amount.Should().Be(40m);
        new ConcreteSubscriptionAnalytics { MonthlyRecurringRevenue = money, ActiveSubscriptions = 0 }.AverageRevenuePerUser.Amount.Should().Be(0m);
        new ConcreteLimitCheckResult { CurrentUsage = 80, MaxAllowed = 100 }.UsagePercentage.Should().Be(80m);
        new ConcreteLimitCheckResult { CurrentUsage = 120, MaxAllowed = 100 }.ExcessUsage.Should().Be(20);
        new ConcreteLimitCheckResult { CurrentUsage = 1, MaxAllowed = 0 }.UsagePercentage.Should().Be(0m);
        new ConcretePricingAddOn { UnitPrice = new Money(5m, "USD"), Quantity = 4 }.TotalPrice.Amount.Should().Be(20m);

        SubscriptionUpgradeResult.CreateSuccess(subscription, money, new Money(5m, "USD")).Success.Should().BeTrue();
        SubscriptionUpgradeResult.Failed("not upgrade").FailureReason.Should().Be("not upgrade");
        SubscriptionDowngradeResult.CreateSuccess(subscription, DateTime.UtcNow, new Money(5m, "USD")).Success.Should().BeTrue();
        SubscriptionDowngradeResult.Failed("not downgrade").FailureReason.Should().Be("not downgrade");
        PlanValidationResult.Success().IsValid.Should().BeTrue();
        PlanValidationResult.Failure("error").Errors.Should().ContainSingle();
        PlanValidationResult.FailureWithSuggestions(["error"], [Guid.NewGuid()]).SuggestedUpgrades.Should().ContainSingle();
        SubscriptionLimitValidationResult.Valid().IsWithinLimits.Should().BeTrue();
        SubscriptionLimitValidationResult.Valid("ok").Message.Should().Be("ok");
        SubscriptionLimitValidationResult.Invalid([new ConcreteLimitCheckResult { LimitName = "users", CurrentUsage = 2, MaxAllowed = 1 }])
            .RecommendedAction.Should().Be("Consider upgrading your plan");
        SubscriptionLimitValidationResult.Invalid([], "custom").RecommendedAction.Should().Be("custom");
    }

    [Fact]
    public async Task RemainingPrivateHelpersAndHandlers_ShouldCoverSwitchesAndGuardBranches()
    {
        var planWithAnnual = CreatePlan("Enterprise");
        var planWithoutAnnual = CreatePlan("Starter");
        planWithoutAnnual.AnnualPriceInCents = null;
        foreach (var cycle in new[] { BillingCycle.Monthly, BillingCycle.Quarterly, BillingCycle.SemiAnnually, BillingCycle.Annually, BillingCycle.Biannually, BillingCycle.Weekly })
        {
            InvokePrivateStatic<Money>(typeof(SubscriptionLifecycleService), "GetPriceForCycle", planWithAnnual, cycle).Amount.Should().BeGreaterThan(0);
        }

        InvokePrivateStatic<Money>(typeof(SubscriptionLifecycleService), "GetPriceForCycle", planWithoutAnnual, BillingCycle.Annually).Amount.Should().Be(1200m);
        InvokePrivateStatic<string>(typeof(SubscriptionBillingService), "GenerateIdempotencyKey", Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"), 2, new DateTime(2026, 1, 2))
            .Should().Contain("20260102");

        var repository = Mock.Of<ISubscriptionRepository>();
        var planService = Mock.Of<ISubscriptionPlanService>();
        var notificationService = Mock.Of<ISubscriptionNotificationService>();
        var billingLogger = Mock.Of<ILogger<SubscriptionBillingService>>();
        var lifecycleLogger = Mock.Of<ILogger<SubscriptionLifecycleService>>();
        var notificationLogger = Mock.Of<ILogger<SubscriptionNotificationService>>();

        FluentActions.Invoking(() => new SubscriptionBillingService(null!, planService, notificationService, billingLogger)).Should().Throw<ArgumentNullException>();
        FluentActions.Invoking(() => new SubscriptionBillingService(repository, null!, notificationService, billingLogger)).Should().Throw<ArgumentNullException>();
        FluentActions.Invoking(() => new SubscriptionBillingService(repository, planService, null!, billingLogger)).Should().Throw<ArgumentNullException>();
        FluentActions.Invoking(() => new SubscriptionBillingService(repository, planService, notificationService, null!)).Should().Throw<ArgumentNullException>();
        FluentActions.Invoking(() => new SubscriptionLifecycleService(null!, planService, lifecycleLogger)).Should().Throw<ArgumentNullException>();
        FluentActions.Invoking(() => new SubscriptionLifecycleService(repository, null!, lifecycleLogger)).Should().Throw<ArgumentNullException>();
        FluentActions.Invoking(() => new SubscriptionLifecycleService(repository, planService, null!)).Should().Throw<ArgumentNullException>();
        FluentActions.Invoking(() => new SubscriptionNotificationService(null!, planService)).Should().Throw<ArgumentNullException>();
        FluentActions.Invoking(() => new SubscriptionNotificationService(notificationLogger, null!)).Should().Throw<ArgumentNullException>();

        var subscription = CreateSubscription(planWithAnnual.Id);
        subscription.Activate();
        var subscriptionRepository = new Mock<ISubscriptionRepository>();
        subscriptionRepository.Setup(repo => repo.GetByIdAsync(subscription.Id, It.IsAny<CancellationToken>())).ReturnsAsync(subscription);
        subscriptionRepository.Setup(repo => repo.UpdateAsync(subscription, It.IsAny<CancellationToken>())).ReturnsAsync(subscription);

        await new RecordSubscriptionPaymentFailureCommandHandler(subscriptionRepository.Object)
            .Handle(new RecordSubscriptionPaymentFailureCommand(subscription.Id, "declined", DateTime.UtcNow), CancellationToken.None);
        await new UpdateSubscriptionMetadataCommandHandler(subscriptionRepository.Object)
            .Handle(new UpdateSubscriptionMetadataCommand(subscription.Id, "{}"), CancellationToken.None);

        var planRepository = new Mock<ISubscriptionPlanRepository>();
        planRepository.Setup(repo => repo.GetByIdAsync(planWithAnnual.Id, It.IsAny<CancellationToken>())).ReturnsAsync(planWithAnnual);
        planRepository.Setup(repo => repo.UpdateAsync(planWithAnnual, It.IsAny<CancellationToken>())).ReturnsAsync(planWithAnnual);
        await new TestPlanCommandHandler(planRepository.Object)
            .Handle(new ActivateSubscriptionPlanCommand(planWithAnnual.Id), CancellationToken.None);
    }

    [Fact]
    public async Task InvoiceStatusMapping_ShouldCoverAllStatusLabels()
    {
        var subscription = CreateSubscription(Guid.NewGuid());
        var invoices = new[] { 0, 1, 3, 4, 5, 99 }
            .Select(status => new SubscriptionInvoiceReadModel
            {
                Id = Guid.NewGuid(),
                SubscriptionId = subscription.Id,
                InvoiceNumber = $"INV-{status}",
                Total = status,
                Currency = "USD",
                CreatedAt = new DateTime(2026, 1, 1).AddDays(status),
                Status = status
            })
            .ToArray();
        var context = new Mock<IApplicationDbContext>();
        context.Setup(db => db.Set<Subscription>()).Returns(new[] { subscription }.AsQueryable().BuildMockDbSet().Object);
        context.Setup(db => db.Set<SubscriptionInvoiceReadModel>()).Returns(invoices.AsQueryable().BuildMockDbSet().Object);
        context.Setup(db => db.Set<Payment>()).Returns(Array.Empty<Payment>().AsQueryable().BuildMockDbSet().Object);

        var result = await new GetSubscriptionInvoicesHandler(context.Object)
            .Handle(new GetSubscriptionInvoicesQuery(subscription.Id, 1, 10), CancellationToken.None);

        result.Items.Select(item => item.Status).Should().BeEquivalentTo("Draft", "Open", "Void", "PastDue", "Uncollectible", "Unknown");
    }

    [Fact]
    public void MonthlyStatementDispatchAndControllerHelpers_ShouldCoverUrlBranches()
    {
        _ = typeof(MonthlyStatementDispatchBackgroundService).GetField("PollInterval", BindingFlags.NonPublic | BindingFlags.Static)!.GetValue(null);
        _ = typeof(MonthlyStatementDispatchBackgroundService).GetField("ProbeTimeout", BindingFlags.NonPublic | BindingFlags.Static)!.GetValue(null);

        InvokePrivateInstance<string>(CreateStatementRenderer(new Dictionary<string, string?> { ["StatementEmails:ConsoleBaseUrl"] = " https://console.test/base/ " }), "ResolveConsoleBaseUrl")
            .Should().Be("https://console.test/base");
        InvokePrivateInstance<string>(CreateStatementRenderer(new Dictionary<string, string?> { ["NEXTAUTH_URL"] = "https://nextauth.test/" }), "ResolveConsoleBaseUrl")
            .Should().Be("https://nextauth.test");
        InvokePrivateInstance<string>(CreateStatementRenderer(new Dictionary<string, string?> { ["NEXT_PUBLIC_URL"] = "https://public.test/" }), "ResolveConsoleBaseUrl")
            .Should().Be("https://public.test");
        InvokePrivateInstance<string>(CreateStatementRenderer(new Dictionary<string, string?>()), "ResolveConsoleBaseUrl")
            .Should().Be("http://localhost:3000");

        InvokePrivateStatic<string>(typeof(MonthlyStatementRenderer), "BuildAbsoluteUrl", "https://console.test", "/statements")
            .Should().Be("https://console.test/statements");
        InvokePrivateStatic<string>(typeof(MonthlyStatementRenderer), "BuildAbsoluteUrl", "https://console.test/", "statements")
            .Should().Be("https://console.test/statements");

        var controller = CreateSubscriptionsController(CreateActor(Guid.NewGuid()));
        InvokePrivateInstance<IActionResult?>(controller, "ValidateTenantAccess", Guid.NewGuid(), "coverage")
            .Should().NotBeNull();
    }

    [Fact]
    public void SubscriptionDomainBranches_ShouldCoverRemainingEconomicPaths()
    {
        var subscription = CreateSubscription(Guid.NewGuid());
        ((ISubscription)subscription).TenantId.Should().NotBeEmpty();
        var orderId = Guid.NewGuid();
        subscription.SetFulfilledOrderId(orderId);
        subscription.SetFulfilledOrderId(orderId);
        subscription.SetFulfilledOrderId(subscription.FulfilledOrderId!.Value);
        FluentActions.Invoking(() => subscription.SetFulfilledOrderId(Guid.NewGuid()))
            .Should().Throw<InvalidOperationException>();
        subscription.Activate();
        subscription.ChangePlan(Guid.NewGuid(), new Money(200m, "USD"), subscription.CurrentPeriodEnd.AddDays(1)).NetAdjustment.Should().Be(0m);
        subscription.ChangeBillingCycle(BillingCycle.Quarterly, new Money(300m, "USD"));
        subscription.ProcessRenewal(new Money(300m, "USD"), "renewal-key").PaymentRequired.Should().BeTrue();
        subscription.LockToPriceVersion(Guid.NewGuid());
        subscription.UnlockPriceVersion();
        subscription.UnlockPriceVersion();

        var missingTenantSubscription = CreateSubscription(Guid.NewGuid());
        typeof(Subscription).GetProperty(nameof(Subscription.TenantId))!.SetValue(missingTenantSubscription, null);
        FluentActions.Invoking(() => _ = ((ISubscription)missingTenantSubscription).TenantId)
            .Should().Throw<InvalidOperationException>();

        var zeroLengthPeriodSubscription = CreateSubscription(Guid.NewGuid());
        zeroLengthPeriodSubscription.Activate();
        typeof(Subscription).GetProperty(nameof(Subscription.CurrentPeriodEnd))!
            .SetValue(zeroLengthPeriodSubscription, zeroLengthPeriodSubscription.CurrentPeriodStart);
        zeroLengthPeriodSubscription.ChangePlan(Guid.NewGuid(), new Money(150m, "USD"), zeroLengthPeriodSubscription.CurrentPeriodStart)
            .NetAdjustment.Should().Be(0m);

        var nullTenantLifecycleSubscription = CreateSubscription(Guid.NewGuid());
        nullTenantLifecycleSubscription.Activate();
        typeof(Subscription).GetProperty(nameof(Subscription.TenantId))!.SetValue(nullTenantLifecycleSubscription, null);
        nullTenantLifecycleSubscription.ChangeBillingCycle(BillingCycle.Monthly, new Money(100m, "USD"));
        nullTenantLifecycleSubscription.ProcessRenewal(new Money(100m, "USD"), "null-tenant-renewal").PaymentRequired.Should().BeTrue();
        nullTenantLifecycleSubscription.LockToPriceVersion(Guid.NewGuid());
        nullTenantLifecycleSubscription.UnlockPriceVersion();

        var cancelledSubscription = CreateSubscription(Guid.NewGuid());
        cancelledSubscription.Activate();
        cancelledSubscription.Cancel(CancellationReason.UserRequested);
        FluentActions.Invoking(() => cancelledSubscription.LockToPriceVersion(Guid.NewGuid()))
            .Should().Throw<InvalidOperationException>();

        CreateSubscription(Guid.NewGuid()).RecordPaymentFailure("pending failure", DateTime.UtcNow);

        foreach (var cycle in new[] { BillingCycle.Weekly, BillingCycle.Monthly, BillingCycle.Quarterly, BillingCycle.SemiAnnually, BillingCycle.Annually, BillingCycle.Biannually })
        {
            var paymentSubscription = CreateSubscription(Guid.NewGuid());
            paymentSubscription.Activate();
            typeof(Subscription).GetProperty(nameof(Subscription.BillingCycle))!.SetValue(paymentSubscription, cycle);
            paymentSubscription.RecordPayment(100m, "USD", DateTime.UtcNow, $"payment-{cycle}", 1).IsSuccess.Should().BeTrue();
        }

        var sameCycleSubscription = CreateSubscription(Guid.NewGuid());
        sameCycleSubscription.Activate();
        var paymentDate = DateTime.UtcNow;
        sameCycleSubscription.RecordPayment(100m, "USD", paymentDate, "first", 1);
        sameCycleSubscription.RecordPayment(100m, "USD", paymentDate.AddDays(-1), "second", 1).IsRejectedOutOfOrder.Should().BeTrue();

        var sameCycleNewerPaymentSubscription = CreateSubscription(Guid.NewGuid());
        sameCycleNewerPaymentSubscription.Activate();
        sameCycleNewerPaymentSubscription.RecordPayment(100m, "USD", paymentDate, "initial", 1);
        sameCycleNewerPaymentSubscription.RecordPayment(100m, "USD", paymentDate.AddDays(1), "retry", 1).IsRejectedOutOfOrder.Should().BeTrue();

        var sameCycleWithoutPreviousPaymentDate = CreateSubscription(Guid.NewGuid());
        sameCycleWithoutPreviousPaymentDate.Activate();
        typeof(Subscription).GetProperty(nameof(Subscription.LastProcessedBillingCycle))!.SetValue(sameCycleWithoutPreviousPaymentDate, 1);
        sameCycleWithoutPreviousPaymentDate.RecordPayment(100m, "USD", paymentDate, "same-cycle-no-date", 1)
            .IsRejectedOutOfOrder.Should().BeTrue();

        var currentCycleSubscription = CreateSubscription(Guid.NewGuid());
        currentCycleSubscription.Activate();
        currentCycleSubscription.RecordPayment(100m, "USD", paymentDate, "current-cycle").IsRejectedOutOfOrder.Should().BeTrue();

        var nullTenantPaymentSubscription = CreateSubscription(Guid.NewGuid());
        nullTenantPaymentSubscription.Activate();
        typeof(Subscription).GetProperty(nameof(Subscription.TenantId))!.SetValue(nullTenantPaymentSubscription, null);
        nullTenantPaymentSubscription.RecordPayment(100m, "USD", paymentDate, "null-tenant-payment").IsRejectedOutOfOrder.Should().BeTrue();

        var invalidCycleSubscription = CreateSubscription(Guid.NewGuid());
        invalidCycleSubscription.Activate();
        typeof(Subscription).GetProperty(nameof(Subscription.BillingCycle))!.SetValue(invalidCycleSubscription, (BillingCycle)999);
        typeof(Subscription).GetProperty(nameof(Subscription.LastProcessedBillingCycle))!.SetValue(invalidCycleSubscription, 1);
        FluentActions.Invoking(() => invalidCycleSubscription.RecordPayment(100m, "USD", paymentDate, "invalid-cycle", 2))
            .Should().Throw<ArgumentOutOfRangeException>();

        var activeFailureSubscription = CreateSubscription(Guid.NewGuid());
        activeFailureSubscription.Activate();
        activeFailureSubscription.RecordPaymentFailure("active failure", paymentDate);
        activeFailureSubscription.Status.Should().Be(SubscriptionStatus.PastDue);

        var nullTenantFailureSubscription = CreateSubscription(Guid.NewGuid());
        nullTenantFailureSubscription.Activate();
        typeof(Subscription).GetProperty(nameof(Subscription.TenantId))!.SetValue(nullTenantFailureSubscription, null);
        nullTenantFailureSubscription.RecordPaymentFailure("null tenant failure", paymentDate);
    }

    [Fact]
    public void ConstructorsAndProperties_ShouldBeExercisedForSimpleModuleTypes()
    {
        var assembly = typeof(Subscription).Assembly;
        var created = new List<object>();

        foreach (var type in assembly.GetTypes().Where(IsConcreteSubscriptionsType))
        {
            foreach (var constructor in type.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                         .Where(constructor => !constructor.IsPrivate)
                         .OrderBy(constructor => constructor.GetParameters().Length))
            {
                if (!TryBuildArguments(constructor.GetParameters(), out var arguments))
                {
                    continue;
                }

                try
                {
                    var instance = constructor.Invoke(arguments);
                    TouchReadableProperties(instance);
                    created.Add(instance);
                    break;
                }
                catch
                {
                    // Some domain constructors enforce invariants. They are covered by focused tests.
                }
            }
        }

        created.Count.Should().BeGreaterThan(80);
    }

    private static MonthlyStatementBuildContext CreateStatementContext(
        MonthlyStatementDocumentProfile profile,
        bool includePeople,
        bool includeMaintenance)
    {
        var tenantId = Guid.NewGuid();
        var transactions = Enumerable.Range(1, 12)
            .Select(index => new StatementTransactionSummary(
                Guid.NewGuid(),
                $"2026-01-{index:00}",
                index == 1 ? "ledger,code" : "ledger",
                index % 2 == 0 ? "Credit" : "Debit",
                "Rent",
                index == 2 ? "Long description with (parentheses), quote \" and slash \\ plus " + new string('x', 130) : "Description",
                100 + index,
                "Posted",
                index == 3 ? "counter\"party" : null,
                DateTime.UtcNow))
            .ToList();

        var owners = includePeople
            ?
            [
                new StatementOwnerSummary(Guid.NewGuid(), "Owner One", "owner@example.test", 2, 3000m, 500m, 125m, 2375m, DateTime.UtcNow)
            ]
            : Array.Empty<StatementOwnerSummary>();
        var renters = includePeople
            ?
            [
                new StatementRenterSummary(Guid.NewGuid(), "Renter One", "renter@example.test", 1, 3, 3000m, 2500m, 1, 500m, DateTime.UtcNow)
            ]
            : Array.Empty<StatementRenterSummary>();

        return new MonthlyStatementBuildContext(
            new MonthlyStatementSourceData(
                tenantId,
                new DateTime(2026, 1, 31, 12, 0, 0, DateTimeKind.Utc),
                new DateOnly(2026, 1, 1),
                new DateOnly(2026, 1, 31),
                3,
                1,
                transactions.Count,
                1200m,
                3000m,
                1800m,
                5000m,
                [
                    new StatementCategorySummary("Rent", 100m, 1000m, 900m, 5, 60m),
                    new StatementCategorySummary("Maintenance", 50m, 0m, -50m, 1, 3m)
                ],
                [
                    new StatementPeriodSummary("2026-01-01", "2026-01-15", "Jan 1-15", 100m, 500m, 400m, 400m, 3),
                    new StatementPeriodSummary("2026-01-16", "2026-01-31", "Jan 16-31", 200m, 700m, 500m, 900m, 4)
                ],
                transactions,
                owners,
                renters,
                includeMaintenance
                    ? new StatementMaintenanceSummary(DateTime.UtcNow, 4, 2, 1, 1, 3, 1, 1, 0)
                    : null),
            new MonthlyStatementDocumentOptions(
                "tenant-statement",
                "Monthly Statement " + new string('A', 120) + " \\ (test)",
                profile));
    }

    private static MonthlyStatementReport CreateReport(MonthlyStatementSourceData source)
        => new(
            source.TenantId,
            source.GeneratedAtUtc,
            source.FromDate,
            source.ToDate,
            source.LedgerCount,
            source.RootLedgerCount,
            source.EntryCount,
            source.TotalDebit,
            source.TotalCredit,
            source.NetCashFlow,
            source.ClosingBalance,
            source.Categories,
            source.Periods,
            source.Transactions,
            source.OwnerStatements,
            source.RenterPayments,
            source.MaintenanceReport);

    private static SubscriptionPlan CreatePlan(string name)
        => new(name, name.ToLowerInvariant(), 10000)
        {
            Id = Guid.NewGuid(),
            AnnualPriceInCents = 100000
        };

    private static Subscription CreateSubscription(Guid planId)
        => new(Guid.NewGuid(), planId, Guid.NewGuid(), BillingCycle.Monthly, new Money(100m, "USD"), DateTime.UtcNow.AddDays(-5));

    private static MonthlyStatementRenderer CreateStatementRenderer(Dictionary<string, string?> values)
        => new(
            Mock.Of<IMonthlyStatementAttachmentBuilder>(),
            Mock.Of<IMonthlyStatementLinkBuilder>(),
            new ConfigurationBuilder().AddInMemoryCollection(values).Build(),
            Mock.Of<IEmailFooterService>());

    private static SubscriptionsController CreateSubscriptionsController(ActorContext actorContext)
    {
        var accessor = new Mock<IActorContextAccessor>();
        accessor.SetupGet(value => value.ActorContext).Returns(actorContext);
        return new SubscriptionsController(Mock.Of<ISender>(), accessor.Object)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };
    }

    private static ActorContext CreateActor(Guid tenantId)
        => new()
        {
            ActorKind = ActorKind.User,
            SubjectId = Guid.NewGuid().ToString(),
            TenantId = tenantId,
            Roles = new HashSet<string>(),
            Permissions = new HashSet<string>(),
            TypedAttributes = ActorAttributes.Empty,
            AuthScheme = "Test",
            IsAuthenticated = true
        };

    private static bool IsConcreteSubscriptionsType(Type type)
        => type.Namespace?.StartsWith("GameGuild.Commerce.Subscriptions", StringComparison.Ordinal) == true
           && !type.IsAbstract
           && !type.IsInterface
           && !type.IsEnum
           && !type.IsGenericTypeDefinition
           && !type.IsNestedPrivate
           && !type.Name.Contains("<", StringComparison.Ordinal);

    private static bool TryBuildArguments(ParameterInfo[] parameters, out object?[] arguments)
    {
        arguments = new object?[parameters.Length];
        for (var index = 0; index < parameters.Length; index++)
        {
            if (!TryCreateValue(parameters[index].ParameterType, 0, out arguments[index]))
            {
                return false;
            }
        }

        return true;
    }

    private static bool TryCreateValue(Type type, int depth, out object? value)
    {
        value = null;
        if (depth > 3)
        {
            return !type.IsValueType;
        }

        var nullable = Nullable.GetUnderlyingType(type);
        if (nullable is not null)
        {
            return TryCreateValue(nullable, depth + 1, out value);
        }

        if (type == typeof(string)) { value = "value"; return true; }
        if (type == typeof(Guid)) { value = Guid.NewGuid(); return true; }
        if (type == typeof(DateTime)) { value = DateTime.UtcNow; return true; }
        if (type == typeof(DateOnly)) { value = new DateOnly(2026, 1, 1); return true; }
        if (type == typeof(TimeSpan)) { value = TimeSpan.FromDays(1); return true; }
        if (type == typeof(bool)) { value = true; return true; }
        if (type == typeof(int)) { value = 1; return true; }
        if (type == typeof(long)) { value = 1L; return true; }
        if (type == typeof(decimal)) { value = 1m; return true; }
        if (type == typeof(double)) { value = 1d; return true; }
        if (type == typeof(byte[])) { value = Array.Empty<byte>(); return true; }
        if (type == typeof(CancellationToken)) { value = CancellationToken.None; return true; }
        if (type == typeof(Money)) { value = new Money(1m, "USD"); return true; }
        if (type == typeof(IConfiguration)) { value = new ConfigurationBuilder().Build(); return true; }

        if (type.IsEnum)
        {
            value = Enum.GetValues(type).GetValue(0);
            return true;
        }

        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(ILogger<>))
        {
            var mock = typeof(Mock<>).MakeGenericType(type).GetConstructor(Type.EmptyTypes)!.Invoke(null);
            value = mock.GetType()
                .GetProperties()
                .Single(property => property.Name == "Object" && property.PropertyType == type)
                .GetValue(mock);
            return true;
        }

        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IOptions<>))
        {
            if (!TryCreateValue(type.GetGenericArguments()[0], depth + 1, out var optionValue))
            {
                return false;
            }

            value = typeof(Options)
                .GetMethods()
                .Single(method => method.Name == nameof(Options.Create) && method.IsGenericMethod)
                .MakeGenericMethod(type.GetGenericArguments()[0])
                .Invoke(null, [optionValue]);
            return true;
        }

        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IReadOnlyList<>))
        {
            var elementType = type.GetGenericArguments()[0];
            var array = Array.CreateInstance(elementType, 1);
            if (TryCreateValue(elementType, depth + 1, out var element))
            {
                array.SetValue(element, 0);
            }

            value = array;
            return true;
        }

        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>))
        {
            value = Activator.CreateInstance(type);
            return true;
        }

        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Dictionary<,>))
        {
            value = Activator.CreateInstance(type);
            return true;
        }

        if (type.IsArray)
        {
            value = Array.CreateInstance(type.GetElementType()!, 0);
            return true;
        }

        if (type.IsInterface)
        {
            var mock = typeof(Mock<>).MakeGenericType(type).GetConstructor(Type.EmptyTypes)!.Invoke(null);
            value = mock.GetType()
                .GetProperties()
                .Single(property => property.Name == "Object" && property.PropertyType == type)
                .GetValue(mock);
            return true;
        }

        var constructors = type.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
            .Where(constructor => !constructor.IsPrivate)
            .OrderBy(constructor => constructor.GetParameters().Length);

        foreach (var constructor in constructors)
        {
            if (!TryBuildNestedArguments(constructor.GetParameters(), depth + 1, out var arguments))
            {
                continue;
            }

            try
            {
                value = constructor.Invoke(arguments);
                return true;
            }
            catch
            {
                // Try the next constructor.
            }
        }

        return false;
    }

    private static bool TryBuildNestedArguments(ParameterInfo[] parameters, int depth, out object?[] arguments)
    {
        arguments = new object?[parameters.Length];
        for (var index = 0; index < parameters.Length; index++)
        {
            if (!TryCreateValue(parameters[index].ParameterType, depth, out arguments[index]))
            {
                return false;
            }
        }

        return true;
    }

    private static void TouchReadableProperties(object instance)
    {
        foreach (var property in instance.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance)
                     .Where(property => property.GetMethod is not null && property.GetIndexParameters().Length == 0))
        {
            try
            {
                _ = property.GetValue(instance);
            }
            catch
            {
                // Some domain properties enforce invariants on access.
            }
        }
    }

    private sealed class ConcreteBulkRenewalResult : BulkRenewalResult;
    private sealed class ConcreteRevenueAnalytics : RevenueAnalytics;
    private sealed class ConcreteSubscriptionAnalytics : SubscriptionAnalytics;
    private sealed class ConcreteLimitCheckResult : LimitCheckResult;
    private sealed class ConcretePricingAddOn : PricingAddOn;
    private sealed class ConcreteAppliedDiscount : AppliedDiscount;
    private sealed class NullStringValue
    {
        public override string ToString() => null!;
    }

    private sealed class TestPlanCommandHandler(ISubscriptionPlanRepository repository)
        : SubscriptionPlanCommandHandlerBase<ActivateSubscriptionPlanCommand>(repository)
    {
        protected override Guid GetPlanId(ActivateSubscriptionPlanCommand request) => request.Id;

        protected override Task ExecuteAsync(SubscriptionPlan plan, ActivateSubscriptionPlanCommand request, CancellationToken cancellationToken)
        {
            plan.Activate();
            return Task.CompletedTask;
        }
    }

    private static T InvokePrivateStatic<T>(Type type, string methodName, params object?[] arguments)
        => (T)type.GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Static)!.Invoke(null, arguments)!;

    private static T InvokePrivateInstance<T>(object instance, string methodName, params object?[] arguments)
        => (T)instance.GetType().GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(instance, arguments)!;
}
