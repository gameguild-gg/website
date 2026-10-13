using FluentAssertions;
using GameGuild.Commerce.Payments;
using Xunit;

namespace GameGuild.Commerce.Payments.UnitTests.Entities;

/// <summary>
///     Payment-method entity lifecycle tests (issue #414): fail-closed construction,
/// display metadata, expiry computation, default transitions and soft delete.
/// </summary>
public class PaymentMethodEntityTests
{
    private static readonly Guid Tenant = Guid.NewGuid();
    private static readonly Guid User = Guid.NewGuid();

    private static PaymentMethod CreatePersisted()
    {
        var paymentMethod = new PaymentMethod(Tenant, User, "stripe", "pm_entity_1", PaymentMethodType.Card, isDefault: false);
        paymentMethod.UpdateGatewayMetadata(PaymentMethodType.Card, "visa", "4242", 12, 2030, "fp_entity_1");
        return SimulatePersisted(paymentMethod);
    }

    private static PaymentMethod SimulatePersisted(PaymentMethod paymentMethod)
    {
        // SoftDelete refuses unpersisted entities (Version == 0); simulate a save.
        paymentMethod.Version = 1;
        return paymentMethod;
    }

    [Fact]
    public void Constructor_ShouldFailClosed_WhenTenantIdIsEmpty()
    {
        var act = () => new PaymentMethod(Guid.Empty, User, "stripe", "pm_x", PaymentMethodType.Card, false);
        act.Should().Throw<ArgumentException>().WithMessage("*TenantId*");
    }

    [Fact]
    public void Constructor_ShouldFailClosed_WhenUserIdIsEmpty()
    {
        var act = () => new PaymentMethod(Tenant, Guid.Empty, "stripe", "pm_x", PaymentMethodType.Card, false);
        act.Should().Throw<ArgumentException>().WithMessage("*UserId*");
    }

    [Fact]
    public void Constructor_ShouldFailClosed_WhenGatewayReferenceMissing()
    {
        var act = () => new PaymentMethod(Tenant, User, "", "pm_x", PaymentMethodType.Card, false);
        act.Should().Throw<ArgumentException>().WithMessage("*Gateway provider*");

        var act2 = () => new PaymentMethod(Tenant, User, "stripe", "  ", PaymentMethodType.Card, false);
        act2.Should().Throw<ArgumentException>().WithMessage("*Gateway payment method*");
    }

    [Fact]
    public void Constructor_ShouldNormalizeProviderAndDefaultToActive()
    {
        var paymentMethod = new PaymentMethod(Tenant, User, " Stripe ", "pm_norm", PaymentMethodType.Card, true);

        paymentMethod.GatewayProvider.Should().Be("stripe");
        paymentMethod.GatewayPaymentMethodId.Should().Be("pm_norm");
        paymentMethod.Status.Should().Be(PaymentMethodStatus.Active);
        paymentMethod.IsDefault.Should().BeTrue();
        paymentMethod.IsDeleted.Should().BeFalse();
    }

    [Fact]
    public void ComputeExpiresAt_ShouldCoverTheWholeExpiryMonth()
    {
        var expiresAt = PaymentMethod.ComputeExpiresAt(12, 2030);

        // End of the expiry month, inclusive: the last tick before the next month starts.
        var endOfMonth = new DateTime(2031, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddTicks(-1);
        expiresAt.Should().Be(endOfMonth);
        expiresAt.Should().BeOnOrAfter(new DateTime(2030, 12, 31, 23, 59, 59, DateTimeKind.Utc));
    }

    [Theory]
    [InlineData(null, 2030)]
    [InlineData(12, null)]
    [InlineData(0, 2030)]
    [InlineData(13, 2030)]
    [InlineData(12, 1999)]
    public void ComputeExpiresAt_ShouldReturnNull_ForInvalidInput(int? month, int? year)
    {
        PaymentMethod.ComputeExpiresAt(month, year).Should().BeNull();
    }

    [Fact]
    public void UpdateGatewayMetadata_ShouldRefreshDisplayFieldsAndExpiry()
    {
        var paymentMethod = CreatePersisted();

        paymentMethod.UpdateGatewayMetadata(PaymentMethodType.Card, "mastercard", "5555", 8, 2027, "fp_2");

        paymentMethod.CardBrand.Should().Be("mastercard");
        paymentMethod.CardLast4.Should().Be("5555");
        paymentMethod.CardFingerprint.Should().Be("fp_2");
        paymentMethod.ExpiresAt.Should().Be(new DateTime(2027, 9, 1, 0, 0, 0, DateTimeKind.Utc).AddTicks(-1));
    }

    [Fact]
    public void UpdateDisplay_ShouldTrimAndStoreNicknameAndBillingAddress()
    {
        var paymentMethod = CreatePersisted();

        paymentMethod.UpdateDisplay("  Corporate Card  ", "{\"city\":\"Lisbon\"}");

        paymentMethod.Nickname.Should().Be("Corporate Card");
        paymentMethod.BillingAddress.Should().Be("{\"city\":\"Lisbon\"}");
    }

    [Fact]
    public void UpdateDisplay_ShouldClearValues_WhenWhitespaceProvided()
    {
        var paymentMethod = CreatePersisted();
        paymentMethod.UpdateDisplay("Corporate Card", "{}");

        paymentMethod.UpdateDisplay("   ", "  ");

        paymentMethod.Nickname.Should().BeNull();
        paymentMethod.BillingAddress.Should().BeNull();
    }

    [Fact]
    public void UpdateDisplay_ShouldRejectOversizedValues()
    {
        var paymentMethod = CreatePersisted();

        var act = () => paymentMethod.UpdateDisplay(new string('x', 101), null);
        act.Should().Throw<ArgumentException>().WithMessage("*Nickname*");

        var act2 = () => paymentMethod.UpdateDisplay(null, new string('x', 1001));
        act2.Should().Throw<ArgumentException>().WithMessage("*Billing address*");
    }

    [Fact]
    public void DefaultTransitions_ShouldBeIdempotent()
    {
        var paymentMethod = CreatePersisted();
        paymentMethod.IsDefault.Should().BeFalse();

        paymentMethod.MarkAsDefault();
        paymentMethod.MarkAsDefault();
        paymentMethod.IsDefault.Should().BeTrue();

        paymentMethod.ClearDefault();
        paymentMethod.ClearDefault();
        paymentMethod.IsDefault.Should().BeFalse();
    }

    [Fact]
    public void MarkExpired_ShouldFlipStatusOnce()
    {
        var paymentMethod = CreatePersisted();

        paymentMethod.MarkExpired();
        paymentMethod.MarkExpired();

        paymentMethod.Status.Should().Be(PaymentMethodStatus.Expired);
        paymentMethod.DomainEvents.Should().ContainSingle(e => e is PaymentMethodExpiredEvent);
    }

    [Fact]
    public void MarkAsDefault_ShouldRaiseDomainEvent()
    {
        var paymentMethod = CreatePersisted();

        paymentMethod.MarkAsDefault();

        paymentMethod.DomainEvents.Should().ContainSingle(e => e is PaymentMethodSetAsDefaultEvent);
    }

    [Fact]
    public void RecordExpiryNotice_ShouldBeIdempotentPerHorizon_AndRejectUnknownHorizons()
    {
        var paymentMethod = CreatePersisted();
        var now = DateTime.UtcNow;

        paymentMethod.RecordExpiryNotice(30, now);
        paymentMethod.RecordExpiryNotice(30, now.AddHours(24));

        paymentMethod.ExpiryNotice30SentAt.Should().Be(now);
        paymentMethod.HasExpiryNotice(30).Should().BeTrue();
        paymentMethod.HasExpiryNotice(14).Should().BeFalse();

        var act = () => paymentMethod.RecordExpiryNotice(10, now);
        act.Should().Throw<ArgumentOutOfRangeException>();

        var act2 = () => paymentMethod.HasExpiryNotice(5);
        act2.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void GetDaysUntilExpiry_ShouldRoundUpAndGoNegative()
    {
        var paymentMethod = CreatePersisted();
        paymentMethod.UpdateGatewayMetadata(PaymentMethodType.Card, "visa", "4242", 12, 2030, "fp");

        var days = paymentMethod.GetDaysUntilExpiry(new DateTime(2030, 11, 15, 0, 0, 0, DateTimeKind.Utc));

        days.Should().BeGreaterThan(0).And.BeLessOrEqualTo(47);

        var after = paymentMethod.GetDaysUntilExpiry(new DateTime(2031, 2, 1, 0, 0, 0, DateTimeKind.Utc));
        after.Should().BeNegative();

        paymentMethod.UpdateGatewayMetadata(PaymentMethodType.Card, "visa", "4242", null, null, "fp");
        paymentMethod.GetDaysUntilExpiry(DateTime.UtcNow).Should().BeNull();
    }

    [Fact]
    public void MarkUsed_ShouldStampLastUsedAt()
    {
        var paymentMethod = CreatePersisted();
        var usedAt = DateTime.UtcNow;

        paymentMethod.MarkUsed(usedAt);

        paymentMethod.LastUsedAt.Should().Be(usedAt);
    }

    [Fact]
    public void SoftDeleteAndRestore_ShouldToggleDeletedMarker()
    {
        var paymentMethod = CreatePersisted();

        paymentMethod.SoftDelete();
        paymentMethod.IsDeleted.Should().BeTrue();
        paymentMethod.DeletedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));

        paymentMethod.Restore();
        paymentMethod.IsDeleted.Should().BeFalse();
        paymentMethod.DeletedAt.Should().BeNull();
    }

    [Fact]
    public void SoftDelete_ShouldRefuseUnpersistedEntities()
    {
        var paymentMethod = new PaymentMethod(Tenant, User, "stripe", "pm_unpersisted", PaymentMethodType.Card, false);

        var act = () => paymentMethod.SoftDelete();

        act.Should().Throw<InvalidOperationException>().WithMessage("*not been persisted*");
    }
}
