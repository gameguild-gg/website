using FluentAssertions;
using Moq;
using Xunit;

namespace GameGuild.Commerce.Subscriptions.UnitTests.Services;

/// <summary>
///     Unit tests for SubscriptionService facade verifying correct delegation to sub-services:
///     - ISubscriptionLifecycleService
///     - ISubscriptionBillingService
///     - ISubscriptionQueryService
///     - ISubscriptionExternalIdService
/// </summary>
public class SubscriptionServiceTests
{
    private readonly Mock<ISubscriptionLifecycleService> _mockLifecycle;
    private readonly Mock<ISubscriptionBillingService> _mockBilling;
    private readonly Mock<ISubscriptionQueryService> _mockQuery;
    private readonly Mock<ISubscriptionExternalIdService> _mockExternalId;
    private readonly SubscriptionService _service;

    public SubscriptionServiceTests()
    {
        _mockLifecycle = new Mock<ISubscriptionLifecycleService>();
        _mockBilling = new Mock<ISubscriptionBillingService>();
        _mockQuery = new Mock<ISubscriptionQueryService>();
        _mockExternalId = new Mock<ISubscriptionExternalIdService>();

        _service = new SubscriptionService(
            _mockLifecycle.Object,
            _mockBilling.Object,
            _mockQuery.Object,
            _mockExternalId.Object
        );
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_ShouldThrow_WhenLifecycleServiceIsNull()
    {
        var act = () => new SubscriptionService(
            null!,
            _mockBilling.Object,
            _mockQuery.Object,
            _mockExternalId.Object
        );

        act.Should().Throw<ArgumentNullException>().WithParameterName("lifecycleService");
    }

    [Fact]
    public void Constructor_ShouldThrow_WhenBillingServiceIsNull()
    {
        var act = () => new SubscriptionService(
            _mockLifecycle.Object,
            null!,
            _mockQuery.Object,
            _mockExternalId.Object
        );

        act.Should().Throw<ArgumentNullException>().WithParameterName("billingService");
    }

    [Fact]
    public void Constructor_ShouldThrow_WhenQueryServiceIsNull()
    {
        var act = () => new SubscriptionService(
            _mockLifecycle.Object,
            _mockBilling.Object,
            null!,
            _mockExternalId.Object
        );

        act.Should().Throw<ArgumentNullException>().WithParameterName("queryService");
    }

    [Fact]
    public void Constructor_ShouldThrow_WhenExternalIdServiceIsNull()
    {
        var act = () => new SubscriptionService(
            _mockLifecycle.Object,
            _mockBilling.Object,
            _mockQuery.Object,
            null!
        );

        act.Should().Throw<ArgumentNullException>().WithParameterName("externalIdService");
    }

    [Fact]
    public void Constructor_ShouldCreateInstance_WhenAllDependenciesProvided()
    {
        var service = new SubscriptionService(
            _mockLifecycle.Object,
            _mockBilling.Object,
            _mockQuery.Object,
            _mockExternalId.Object
        );

        service.Should().NotBeNull();
    }

    #endregion

    #region ISubscriptionLifecycleService Delegation Tests

    [Fact]
    public async Task CreateAsync_ShouldDelegateToLifecycleService()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var planId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var billingCycle = BillingCycle.Monthly;
        var amount = new Money(29.99m, "USD");
        DateTime? startDate = DateTime.UtcNow.AddDays(1);
        int? trialDays = 14;
        var expected = CreateTestSubscription();

        _mockLifecycle
            .Setup(s => s.CreateAsync(tenantId, planId, userId, billingCycle, amount, startDate, trialDays, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        // Act
        var result = await _service.CreateAsync(tenantId, planId, userId, billingCycle, amount, startDate, trialDays);

        // Assert
        result.Should().BeSameAs(expected);
        _mockLifecycle.Verify(s => s.CreateAsync(tenantId, planId, userId, billingCycle, amount, startDate, trialDays, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ActivateAsync_ShouldDelegateToLifecycleService()
    {
        // Arrange
        var subscriptionId = Guid.NewGuid();
        var expected = CreateTestSubscription();

        _mockLifecycle
            .Setup(s => s.ActivateAsync(subscriptionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        // Act
        var result = await _service.ActivateAsync(subscriptionId);

        // Assert
        result.Should().BeSameAs(expected);
        _mockLifecycle.Verify(s => s.ActivateAsync(subscriptionId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task StartTrialAsync_ShouldDelegateToLifecycleService()
    {
        // Arrange
        var subscriptionId = Guid.NewGuid();
        const int trialDays = 14;
        var expected = CreateTestSubscription();

        _mockLifecycle
            .Setup(s => s.StartTrialAsync(subscriptionId, trialDays, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        // Act
        var result = await _service.StartTrialAsync(subscriptionId, trialDays);

        // Assert
        result.Should().BeSameAs(expected);
        _mockLifecycle.Verify(s => s.StartTrialAsync(subscriptionId, trialDays, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task EndTrialAsync_ShouldDelegateToLifecycleService()
    {
        // Arrange
        var subscriptionId = Guid.NewGuid();
        const bool convertToPaid = true;
        var expected = CreateTestSubscription();

        _mockLifecycle
            .Setup(s => s.EndTrialAsync(subscriptionId, convertToPaid, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        // Act
        var result = await _service.EndTrialAsync(subscriptionId, convertToPaid);

        // Assert
        result.Should().BeSameAs(expected);
        _mockLifecycle.Verify(s => s.EndTrialAsync(subscriptionId, convertToPaid, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CancelAsync_ShouldDelegateToLifecycleService()
    {
        // Arrange
        var subscriptionId = Guid.NewGuid();
        var reason = CancellationReason.UserRequested;
        const string note = "Customer requested";
        DateTime? effectiveDate = DateTime.UtcNow.AddDays(30);
        var expected = CreateTestSubscription();

        _mockLifecycle
            .Setup(s => s.CancelAsync(subscriptionId, reason, note, effectiveDate, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        // Act
        var result = await _service.CancelAsync(subscriptionId, reason, note, effectiveDate);

        // Assert
        result.Should().BeSameAs(expected);
        _mockLifecycle.Verify(s => s.CancelAsync(subscriptionId, reason, note, effectiveDate, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SuspendAsync_ShouldDelegateToLifecycleService()
    {
        // Arrange
        var subscriptionId = Guid.NewGuid();
        const string reason = "Payment failed";
        var expected = CreateTestSubscription();

        _mockLifecycle
            .Setup(s => s.SuspendAsync(subscriptionId, reason, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        // Act
        var result = await _service.SuspendAsync(subscriptionId, reason);

        // Assert
        result.Should().BeSameAs(expected);
        _mockLifecycle.Verify(s => s.SuspendAsync(subscriptionId, reason, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ReactivateAsync_ShouldDelegateToLifecycleService()
    {
        // Arrange
        var subscriptionId = Guid.NewGuid();
        var expected = CreateTestSubscription();

        _mockLifecycle
            .Setup(s => s.ReactivateAsync(subscriptionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        // Act
        var result = await _service.ReactivateAsync(subscriptionId);

        // Assert
        result.Should().BeSameAs(expected);
        _mockLifecycle.Verify(s => s.ReactivateAsync(subscriptionId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpgradePlanAsync_ShouldDelegateToLifecycleService()
    {
        // Arrange
        var subscriptionId = Guid.NewGuid();
        var newPlanId = Guid.NewGuid();
        DateTime? effectiveDate = DateTime.UtcNow.AddDays(1);
        var expected = new Mock<SubscriptionUpgradeResult>().Object;

        _mockLifecycle
            .Setup(s => s.UpgradePlanAsync(subscriptionId, newPlanId, effectiveDate, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        // Act
        var result = await _service.UpgradePlanAsync(subscriptionId, newPlanId, effectiveDate);

        // Assert
        result.Should().BeSameAs(expected);
        _mockLifecycle.Verify(s => s.UpgradePlanAsync(subscriptionId, newPlanId, effectiveDate, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DowngradePlanAsync_ShouldDelegateToLifecycleService()
    {
        // Arrange
        var subscriptionId = Guid.NewGuid();
        var newPlanId = Guid.NewGuid();
        DateTime? effectiveDate = DateTime.UtcNow.AddDays(1);
        var expected = new Mock<SubscriptionDowngradeResult>().Object;

        _mockLifecycle
            .Setup(s => s.DowngradePlanAsync(subscriptionId, newPlanId, effectiveDate, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        // Act
        var result = await _service.DowngradePlanAsync(subscriptionId, newPlanId, effectiveDate);

        // Assert
        result.Should().BeSameAs(expected);
        _mockLifecycle.Verify(s => s.DowngradePlanAsync(subscriptionId, newPlanId, effectiveDate, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ChangeBillingCycleAsync_ShouldDelegateToLifecycleService()
    {
        // Arrange
        var subscriptionId = Guid.NewGuid();
        var newBillingCycle = BillingCycle.Annually;
        var subscription = CreateTestSubscription();
        var expected = ChangeBillingCycleResult.CreateSuccess(subscription, BillingCycle.Monthly, new Money(10m, "USD"), null);

        _mockLifecycle
            .Setup(s => s.ChangeBillingCycleAsync(subscriptionId, newBillingCycle, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        // Act
        var result = await _service.ChangeBillingCycleAsync(subscriptionId, newBillingCycle);

        // Assert
        result.Should().BeSameAs(expected);
        _mockLifecycle.Verify(s => s.ChangeBillingCycleAsync(subscriptionId, newBillingCycle, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SetAutoRenewAsync_ShouldDelegateToLifecycleService()
    {
        // Arrange
        var subscriptionId = Guid.NewGuid();
        const bool autoRenew = true;
        var expected = CreateTestSubscription();

        _mockLifecycle
            .Setup(s => s.SetAutoRenewAsync(subscriptionId, autoRenew, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        // Act
        var result = await _service.SetAutoRenewAsync(subscriptionId, autoRenew);

        // Assert
        result.Should().BeSameAs(expected);
        _mockLifecycle.Verify(s => s.SetAutoRenewAsync(subscriptionId, autoRenew, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateMetadataAsync_ShouldDelegateToLifecycleService()
    {
        // Arrange
        var subscriptionId = Guid.NewGuid();
        const string metadata = "{\"key\": \"value\"}";
        var expected = CreateTestSubscription();

        _mockLifecycle
            .Setup(s => s.UpdateMetadataAsync(subscriptionId, metadata, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        // Act
        var result = await _service.UpdateMetadataAsync(subscriptionId, metadata);

        // Assert
        result.Should().BeSameAs(expected);
        _mockLifecycle.Verify(s => s.UpdateMetadataAsync(subscriptionId, metadata, It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion

    #region ISubscriptionBillingService Delegation Tests

    [Fact]
    public async Task ProcessRenewalAsync_ShouldDelegateToBillingService()
    {
        // Arrange
        var subscriptionId = Guid.NewGuid();
        var expected = new Mock<SubscriptionRenewalResult>().Object;

        _mockBilling
            .Setup(s => s.ProcessRenewalAsync(subscriptionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        // Act
        var result = await _service.ProcessRenewalAsync(subscriptionId);

        // Assert
        result.Should().BeSameAs(expected);
        _mockBilling.Verify(s => s.ProcessRenewalAsync(subscriptionId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RecordPaymentAsync_ShouldDelegateToBillingService()
    {
        // Arrange
        var subscriptionId = Guid.NewGuid();
        const decimal amount = 29.99m;
        const string currency = "USD";
        var paymentDate = DateTime.UtcNow;
        var expected = CreateTestSubscription();

        _mockBilling
            .Setup(s => s.RecordPaymentAsync(subscriptionId, amount, currency, paymentDate, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        // Act
        var result = await _service.RecordPaymentAsync(subscriptionId, amount, currency, paymentDate);

        // Assert
        result.Should().BeSameAs(expected);
        _mockBilling.Verify(s => s.RecordPaymentAsync(subscriptionId, amount, currency, paymentDate, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RecordPaymentFailureAsync_ShouldDelegateToBillingService()
    {
        // Arrange
        var subscriptionId = Guid.NewGuid();
        const string reason = "Card declined";
        var failureDate = DateTime.UtcNow;
        var expected = CreateTestSubscription();

        _mockBilling
            .Setup(s => s.RecordPaymentFailureAsync(subscriptionId, reason, failureDate, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        // Act
        var result = await _service.RecordPaymentFailureAsync(subscriptionId, reason, failureDate);

        // Assert
        result.Should().BeSameAs(expected);
        _mockBilling.Verify(s => s.RecordPaymentFailureAsync(subscriptionId, reason, failureDate, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ProcessBulkRenewalsAsync_ShouldDelegateToBillingService()
    {
        // Arrange
        var subscriptionIds = new[] { Guid.NewGuid(), Guid.NewGuid() };
        var expected = new Mock<BulkRenewalResult>().Object;

        _mockBilling
            .Setup(s => s.ProcessBulkRenewalsAsync(subscriptionIds, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        // Act
        var result = await _service.ProcessBulkRenewalsAsync(subscriptionIds);

        // Assert
        result.Should().BeSameAs(expected);
        _mockBilling.Verify(s => s.ProcessBulkRenewalsAsync(subscriptionIds, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SendRenewalRemindersAsync_ShouldDelegateToBillingService()
    {
        // Arrange
        const int daysBeforeRenewal = 7;

        _mockBilling
            .Setup(s => s.SendRenewalRemindersAsync(daysBeforeRenewal, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        await _service.SendRenewalRemindersAsync(daysBeforeRenewal);

        // Assert
        _mockBilling.Verify(s => s.SendRenewalRemindersAsync(daysBeforeRenewal, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SendTrialExpirationRemindersAsync_ShouldDelegateToBillingService()
    {
        // Arrange
        const int daysBeforeExpiration = 3;

        _mockBilling
            .Setup(s => s.SendTrialExpirationRemindersAsync(daysBeforeExpiration, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        await _service.SendTrialExpirationRemindersAsync(daysBeforeExpiration);

        // Assert
        _mockBilling.Verify(s => s.SendTrialExpirationRemindersAsync(daysBeforeExpiration, It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion

    #region ISubscriptionQueryService Delegation Tests

    [Fact]
    public async Task GetByIdAsync_ShouldDelegateToQueryService()
    {
        // Arrange
        var subscriptionId = Guid.NewGuid();
        var expected = CreateTestSubscription();

        _mockQuery
            .Setup(s => s.GetByIdAsync(subscriptionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        // Act
        var result = await _service.GetByIdAsync(subscriptionId);

        // Assert
        result.Should().BeSameAs(expected);
        _mockQuery.Verify(s => s.GetByIdAsync(subscriptionId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetByIdAsync_ShouldReturnNull_WhenQueryServiceReturnsNull()
    {
        // Arrange
        var subscriptionId = Guid.NewGuid();

        _mockQuery
            .Setup(s => s.GetByIdAsync(subscriptionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Subscription?)null);

        // Act
        var result = await _service.GetByIdAsync(subscriptionId);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task QueryService_GetByExternalIdAsync_ShouldDelegateToQueryService()
    {
        // Arrange
        const string externalId = "ext_12345";
        var expected = CreateTestSubscription();

        _mockQuery
            .Setup(s => s.GetByExternalIdAsync(externalId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        // Act
        var result = await _service.GetByExternalIdAsync(externalId);

        // Assert
        result.Should().BeSameAs(expected);
        _mockQuery.Verify(s => s.GetByExternalIdAsync(externalId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task IsSubscriptionActiveAsync_ShouldDelegateToQueryService()
    {
        // Arrange
        var tenantId = Guid.NewGuid();

        _mockQuery
            .Setup(s => s.IsSubscriptionActiveAsync(tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var result = await _service.IsSubscriptionActiveAsync(tenantId);

        // Assert
        result.Should().BeTrue();
        _mockQuery.Verify(s => s.IsSubscriptionActiveAsync(tenantId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task IsSubscriptionActiveAsync_ShouldReturnFalse_WhenQueryServiceReturnsFalse()
    {
        // Arrange
        var tenantId = Guid.NewGuid();

        _mockQuery
            .Setup(s => s.IsSubscriptionActiveAsync(tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Act
        var result = await _service.IsSubscriptionActiveAsync(tenantId);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task GetActiveTenantSubscriptionAsync_ShouldDelegateToQueryService()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var expected = CreateTestSubscription();

        _mockQuery
            .Setup(s => s.GetActiveTenantSubscriptionAsync(tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        // Act
        var result = await _service.GetActiveTenantSubscriptionAsync(tenantId);

        // Assert
        result.Should().BeSameAs(expected);
        _mockQuery.Verify(s => s.GetActiveTenantSubscriptionAsync(tenantId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetTenantSubscriptionsAsync_ShouldDelegateToQueryService()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var expected = new List<Subscription> { CreateTestSubscription(), CreateTestSubscription() };

        _mockQuery
            .Setup(s => s.GetTenantSubscriptionsAsync(tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        // Act
        var result = await _service.GetTenantSubscriptionsAsync(tenantId);

        // Assert
        result.Should().BeSameAs(expected);
        _mockQuery.Verify(s => s.GetTenantSubscriptionsAsync(tenantId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetTenantSubscriptionHistoryAsync_ShouldDelegateToQueryService()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var expected = new List<Subscription> { CreateTestSubscription() };

        _mockQuery
            .Setup(s => s.GetTenantSubscriptionHistoryAsync(tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        // Act
        var result = await _service.GetTenantSubscriptionHistoryAsync(tenantId);

        // Assert
        result.Should().BeSameAs(expected);
        _mockQuery.Verify(s => s.GetTenantSubscriptionHistoryAsync(tenantId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetExpiringSoonAsync_ShouldDelegateToQueryService()
    {
        // Arrange
        const int days = 7;
        var expected = new List<Subscription> { CreateTestSubscription() };

        _mockQuery
            .Setup(s => s.GetExpiringSoonAsync(days, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        // Act
        var result = await _service.GetExpiringSoonAsync(days);

        // Assert
        result.Should().BeSameAs(expected);
        _mockQuery.Verify(s => s.GetExpiringSoonAsync(days, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetDueForRenewalAsync_ShouldDelegateToQueryService()
    {
        // Arrange
        const int days = 3;
        var expected = new List<Subscription> { CreateTestSubscription() };

        _mockQuery
            .Setup(s => s.GetDueForRenewalAsync(days, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        // Act
        var result = await _service.GetDueForRenewalAsync(days);

        // Assert
        result.Should().BeSameAs(expected);
        _mockQuery.Verify(s => s.GetDueForRenewalAsync(days, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetTrialsExpiringSoonAsync_ShouldDelegateToQueryService()
    {
        // Arrange
        const int days = 2;
        var expected = new List<Subscription> { CreateTestSubscription() };

        _mockQuery
            .Setup(s => s.GetTrialsExpiringSoonAsync(days, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        // Act
        var result = await _service.GetTrialsExpiringSoonAsync(days);

        // Assert
        result.Should().BeSameAs(expected);
        _mockQuery.Verify(s => s.GetTrialsExpiringSoonAsync(days, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ValidateSubscriptionLimitsAsync_ShouldDelegateToQueryService()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        const int userCount = 50;
        const long storageMb = 5000;
        const long apiCallsPerMonth = 50000;
        var expected = new Mock<SubscriptionLimitValidationResult>().Object;

        _mockQuery
            .Setup(s => s.ValidateSubscriptionLimitsAsync(tenantId, userCount, storageMb, apiCallsPerMonth, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        // Act
        var result = await _service.ValidateSubscriptionLimitsAsync(tenantId, userCount, storageMb, apiCallsPerMonth);

        // Assert
        result.Should().BeSameAs(expected);
        _mockQuery.Verify(s => s.ValidateSubscriptionLimitsAsync(tenantId, userCount, storageMb, apiCallsPerMonth, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetUsageStatisticsAsync_ShouldDelegateToQueryService()
    {
        // Arrange
        var subscriptionId = Guid.NewGuid();
        var expected = new Mock<SubscriptionUsageStatistics>().Object;

        _mockQuery
            .Setup(s => s.GetUsageStatisticsAsync(subscriptionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        // Act
        var result = await _service.GetUsageStatisticsAsync(subscriptionId);

        // Assert
        result.Should().BeSameAs(expected);
        _mockQuery.Verify(s => s.GetUsageStatisticsAsync(subscriptionId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetRevenueAnalyticsAsync_ShouldDelegateToQueryService()
    {
        // Arrange
        var startDate = DateTime.UtcNow.AddDays(-30);
        var endDate = DateTime.UtcNow;
        var expected = new Mock<RevenueAnalytics>().Object;

        _mockQuery
            .Setup(s => s.GetRevenueAnalyticsAsync(startDate, endDate, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        // Act
        var result = await _service.GetRevenueAnalyticsAsync(startDate, endDate);

        // Assert
        result.Should().BeSameAs(expected);
        _mockQuery.Verify(s => s.GetRevenueAnalyticsAsync(startDate, endDate, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetSubscriptionAnalyticsAsync_ShouldDelegateToQueryService()
    {
        // Arrange
        var startDate = DateTime.UtcNow.AddDays(-30);
        var endDate = DateTime.UtcNow;
        var expected = new Mock<SubscriptionAnalytics>().Object;

        _mockQuery
            .Setup(s => s.GetSubscriptionAnalyticsAsync(startDate, endDate, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        // Act
        var result = await _service.GetSubscriptionAnalyticsAsync(startDate, endDate);

        // Assert
        result.Should().BeSameAs(expected);
        _mockQuery.Verify(s => s.GetSubscriptionAnalyticsAsync(startDate, endDate, It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion

    #region ISubscriptionExternalIdService Delegation Tests

    [Fact]
    public async Task SetExternalIdsAsync_ShouldDelegateToExternalIdService()
    {
        // Arrange
        var subscriptionId = Guid.NewGuid();
        const string externalSubscriptionId = "sub_12345";
        const string externalCustomerId = "cus_67890";
        var expected = CreateTestSubscription();

        _mockExternalId
            .Setup(s => s.SetExternalIdsAsync(subscriptionId, externalSubscriptionId, externalCustomerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        // Act
        var result = await _service.SetExternalIdsAsync(subscriptionId, externalSubscriptionId, externalCustomerId);

        // Assert
        result.Should().BeSameAs(expected);
        _mockExternalId.Verify(s => s.SetExternalIdsAsync(subscriptionId, externalSubscriptionId, externalCustomerId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExternalIdService_GetByExternalIdAsync_ShouldDelegateToExternalIdService()
    {
        // Arrange
        const string externalId = "ext_12345";
        var expected = CreateTestSubscription();

        _mockExternalId
            .Setup(s => s.GetByExternalIdAsync(externalId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        // Act - call via explicit interface to test the ISubscriptionExternalIdService.GetByExternalIdAsync delegation
        var service = (ISubscriptionExternalIdService)_service;
        var result = await service.GetByExternalIdAsync(externalId);

        // Assert
        result.Should().BeSameAs(expected);
        _mockExternalId.Verify(s => s.GetByExternalIdAsync(externalId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExternalIdService_GetByExternalIdAsync_ShouldReturnNull_WhenNotFound()
    {
        // Arrange
        const string externalId = "ext_nonexistent";

        _mockExternalId
            .Setup(s => s.GetByExternalIdAsync(externalId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Subscription?)null);

        // Act
        var service = (ISubscriptionExternalIdService)_service;
        var result = await service.GetByExternalIdAsync(externalId);

        // Assert
        result.Should().BeNull();
    }

    #endregion

    #region Exception Propagation Tests

    [Fact]
    public async Task CreateAsync_ShouldPropagateException_WhenLifecycleServiceThrows()
    {
        // Arrange
        _mockLifecycle
            .Setup(s => s.CreateAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid>(),
                It.IsAny<BillingCycle>(), It.IsAny<Money>(), It.IsAny<DateTime?>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Plan not found"));

        // Act
        var act = () => _service.CreateAsync(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            BillingCycle.Monthly, new Money(29.99m, "USD"));

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*Plan not found*");
    }

    [Fact]
    public async Task ActivateAsync_ShouldPropagateException_WhenLifecycleServiceThrows()
    {
        // Arrange
        _mockLifecycle
            .Setup(s => s.ActivateAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new SubscriptionNotFoundException(Guid.NewGuid()));

        // Act
        var act = () => _service.ActivateAsync(Guid.NewGuid());

        // Assert
        await act.Should().ThrowAsync<SubscriptionNotFoundException>();
    }

    [Fact]
    public async Task ProcessRenewalAsync_ShouldPropagateException_WhenBillingServiceThrows()
    {
        // Arrange
        _mockBilling
            .Setup(s => s.ProcessRenewalAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new SubscriptionNotFoundException(Guid.NewGuid()));

        // Act
        var act = () => _service.ProcessRenewalAsync(Guid.NewGuid());

        // Assert
        await act.Should().ThrowAsync<SubscriptionNotFoundException>();
    }

    [Fact]
    public async Task GetByIdAsync_ShouldPropagateException_WhenQueryServiceThrows()
    {
        // Arrange
        _mockQuery
            .Setup(s => s.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Database error"));

        // Act
        var act = () => _service.GetByIdAsync(Guid.NewGuid());

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task SetExternalIdsAsync_ShouldPropagateException_WhenExternalIdServiceThrows()
    {
        // Arrange
        _mockExternalId
            .Setup(s => s.SetExternalIdsAsync(It.IsAny<Guid>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new SubscriptionNotFoundException(Guid.NewGuid()));

        // Act
        var act = () => _service.SetExternalIdsAsync(Guid.NewGuid(), "sub_123", "cus_456");

        // Assert
        await act.Should().ThrowAsync<SubscriptionNotFoundException>();
    }

    #endregion

    #region No Cross-Delegation Tests

    [Fact]
    public async Task LifecycleMethods_ShouldNotCallBillingOrQueryOrExternalIdServices()
    {
        // Arrange
        var expected = CreateTestSubscription();
        _mockLifecycle
            .Setup(s => s.ActivateAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        // Act
        await _service.ActivateAsync(Guid.NewGuid());

        // Assert - only lifecycle was called
        _mockBilling.VerifyNoOtherCalls();
        _mockQuery.VerifyNoOtherCalls();
        _mockExternalId.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task BillingMethods_ShouldNotCallLifecycleOrQueryOrExternalIdServices()
    {
        // Arrange
        var expected = new Mock<SubscriptionRenewalResult>().Object;
        _mockBilling
            .Setup(s => s.ProcessRenewalAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        // Act
        await _service.ProcessRenewalAsync(Guid.NewGuid());

        // Assert - only billing was called
        _mockLifecycle.VerifyNoOtherCalls();
        _mockQuery.VerifyNoOtherCalls();
        _mockExternalId.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task QueryMethods_ShouldNotCallLifecycleOrBillingOrExternalIdServices()
    {
        // Arrange
        _mockQuery
            .Setup(s => s.IsSubscriptionActiveAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        await _service.IsSubscriptionActiveAsync(Guid.NewGuid());

        // Assert - only query was called
        _mockLifecycle.VerifyNoOtherCalls();
        _mockBilling.VerifyNoOtherCalls();
        _mockExternalId.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task ExternalIdMethods_ShouldNotCallLifecycleOrBillingOrQueryServices()
    {
        // Arrange
        var expected = CreateTestSubscription();
        _mockExternalId
            .Setup(s => s.SetExternalIdsAsync(It.IsAny<Guid>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        // Act
        await _service.SetExternalIdsAsync(Guid.NewGuid(), "sub_123", "cus_456");

        // Assert - only externalId was called
        _mockLifecycle.VerifyNoOtherCalls();
        _mockBilling.VerifyNoOtherCalls();
        _mockQuery.VerifyNoOtherCalls();
    }

    #endregion

    #region Helper Methods

    private static Subscription CreateTestSubscription()
    {
        return new Subscription(
            tenantId: Guid.NewGuid(),
            planId: Guid.NewGuid(),
            createdByUserId: Guid.NewGuid(),
            billingCycle: BillingCycle.Monthly,
            amount: new Money(29.99m, "USD"),
            startDate: DateTime.UtcNow,
            trialEndDate: null
        );
    }

    #endregion
}
