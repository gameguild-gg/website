using FluentAssertions;
using GameGuild.Commerce.Payments;
using GameGuild.Commerce;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace GameGuild.Commerce.Payments.UnitTests.Services;

/// <summary>
///     Payment-method registry service tests (issue #414): CRUD scoping, single-default
/// invariant with subscription re-pointing, fail-closed delete guard and checkout upsert.
/// </summary>
public class PaymentMethodRegistryServiceTests : IDisposable
{
    private readonly PaymentsPersistenceTestDbContext _context;
    private readonly PaymentMethodRepository _repository;
    private readonly Mock<IStripeCustomerService> _stripeCustomerService = new();
    private readonly Mock<ISubscriptionPaymentMethodCoordination> _coordination = new();
    private readonly PaymentMethodRegistryService _service;

    public PaymentMethodRegistryServiceTests()
    {
        var options = new DbContextOptionsBuilder<PaymentsPersistenceTestDbContext>()
            .UseInMemoryDatabase($"payment-methods-{Guid.NewGuid()}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        _context = new PaymentsPersistenceTestDbContext(options);
        _repository = new PaymentMethodRepository(_context, NullLogger<PaymentMethodRepository>.Instance);
        _service = new PaymentMethodRegistryService(
            _repository,
            _stripeCustomerService.Object,
            _coordination.Object,
            _context,
            NullLogger<PaymentMethodRegistryService>.Instance);
    }

    public void Dispose() => _context.Dispose();

    private static PaymentMethodActor OwnerActor(Guid tenantId, Guid userId) => new(tenantId, userId, IsSystemAdmin: false);

    private static PaymentMethodActor AdminActor() => new(TenantId: null, UserId: Guid.Empty, IsSystemAdmin: true);

    private void SetupGatewayDetails(string gatewayId, int? expMonth = 12, int? expYear = 2031)
    {
        _stripeCustomerService
            .Setup(service => service.GetPaymentMethodAsync(gatewayId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GatewayPaymentMethodDetailsResult(
                Success: true,
                PaymentMethodId: gatewayId,
                Details: new GatewayPaymentMethodDetails("card", "visa", "4242", expMonth, expYear, $"fp_{gatewayId}", null, null, null),
                ErrorCode: null,
                ErrorMessage: null));
    }

    private async Task<PaymentMethod> SeedAsync(
        Guid tenantId,
        Guid userId,
        string gatewayId,
        bool isDefault = false,
        PaymentMethodStatus status = PaymentMethodStatus.Active)
    {
        var paymentMethod = new PaymentMethod(tenantId, userId, "stripe", gatewayId, PaymentMethodType.Card, isDefault);
        paymentMethod.UpdateGatewayMetadata(PaymentMethodType.Card, "visa", "4242", 12, 2031, $"fp_{gatewayId}");
        if (status == PaymentMethodStatus.Expired)
        {
            paymentMethod.MarkExpired();
        }

        paymentMethod.Version = 1;
        _context.Set<PaymentMethod>().Add(paymentMethod);
        await _context.SaveChangesAsync();
        return paymentMethod;
    }

    // ── Create ───────────────────────────────────────────────────────────

    [Fact]
    public async Task Create_ShouldRejectNonTokenizedPaymentMethodIds()
    {
        var actor = OwnerActor(Guid.NewGuid(), Guid.NewGuid());

        var result = await _service.CreateAsync(actor, new CreatePaymentMethodRequest(actor.TenantId!.Value, "4242424242424242"), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("PaymentMethods.InvalidToken");
        _stripeCustomerService.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Create_ShouldFailClosed_WhenGatewayLookupFails()
    {
        var actor = OwnerActor(Guid.NewGuid(), Guid.NewGuid());
        _stripeCustomerService
            .Setup(service => service.GetPaymentMethodAsync("pm_unknown", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GatewayPaymentMethodDetailsResult(false, null, null, "resource_missing", "no such pm"));

        var result = await _service.CreateAsync(actor, new CreatePaymentMethodRequest(actor.TenantId!.Value, "pm_unknown"), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("PaymentMethods.GatewayLookupFailed");
    }

    [Fact]
    public async Task Create_ShouldDenyCrossTenantRegistration()
    {
        var actor = OwnerActor(Guid.NewGuid(), Guid.NewGuid());

        var result = await _service.CreateAsync(actor, new CreatePaymentMethodRequest(Guid.NewGuid(), "pm_other_tenant"), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("PaymentMethods.CrossTenant");
    }

    [Fact]
    public async Task Create_FirstMethodForOwner_ShouldAutoBecomeDefault()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        SetupGatewayDetails("pm_auto_default");

        var result = await _service.CreateAsync(actor: OwnerActor(tenantId, userId), new CreatePaymentMethodRequest(tenantId, "pm_auto_default"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.IsDefault.Should().BeTrue();
        result.Value.CardLast4.Should().Be("4242");
        result.Value.CardBrand.Should().Be("visa");
        result.Value.ExpiresAt.Should().NotBeNull();
    }

    [Fact]
    public async Task Create_WithExistingDefault_ShouldDemotePreviousDefaultAndRepointSubscriptions()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var previous = await SeedAsync(tenantId, userId, "pm_old_default", isDefault: true);
        SetupGatewayDetails("pm_new_default");
        _coordination
            .Setup(c => c.RepointSubscriptionsAsync(tenantId, previous.Id, It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(2);

        var result = await _service.CreateAsync(
            OwnerActor(tenantId, userId),
            new CreatePaymentMethodRequest(tenantId, "pm_new_default", SetAsDefault: true),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.IsDefault.Should().BeTrue();

        var demoted = await _repository.GetByIdAsync(previous.Id, CancellationToken.None);
        demoted!.IsDefault.Should().BeFalse();

        _coordination.Verify(
            c => c.RepointSubscriptionsAsync(tenantId, previous.Id, result.Value.Id, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Create_SameGatewayTokenTwice_ShouldUpsertWithoutDuplicating()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        SetupGatewayDetails("pm_duplicate");

        var first = await _service.CreateAsync(OwnerActor(tenantId, userId), new CreatePaymentMethodRequest(tenantId, "pm_duplicate", Nickname: "First"), CancellationToken.None);
        var second = await _service.CreateAsync(OwnerActor(tenantId, userId), new CreatePaymentMethodRequest(tenantId, "pm_duplicate", Nickname: "Second"), CancellationToken.None);

        first.IsSuccess.Should().BeTrue();
        second.IsSuccess.Should().BeTrue();
        second.Value.Id.Should().Be(first.Value.Id);

        var all = await _repository.ListAsync(tenantId, null, CancellationToken.None);
        all.Should().ContainSingle();
    }

    [Fact]
    public async Task Create_ReRegisteringSoftDeletedToken_ShouldRestoreRow()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var seeded = await SeedAsync(tenantId, userId, "pm_removed");
        seeded.SoftDelete();
        await _repository.UpdateAsync(seeded, CancellationToken.None);
        SetupGatewayDetails("pm_removed");

        var result = await _service.CreateAsync(OwnerActor(tenantId, userId), new CreatePaymentMethodRequest(tenantId, "pm_removed"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Id.Should().Be(seeded.Id);
        (await _repository.GetByIdAsync(seeded.Id, CancellationToken.None)).Should().NotBeNull();
    }

    [Fact]
    public async Task Create_BySystemAdmin_ShouldRequireOwnerUserId()
    {
        var tenantId = Guid.NewGuid();
        SetupGatewayDetails("pm_admin");

        var missingOwner = await _service.CreateAsync(AdminActor(), new CreatePaymentMethodRequest(tenantId, "pm_admin"), CancellationToken.None);
        missingOwner.IsSuccess.Should().BeFalse();
        missingOwner.Error.Code.Should().Be("PaymentMethods.OwnerRequired");

        var withOwner = await _service.CreateAsync(AdminActor(), new CreatePaymentMethodRequest(tenantId, "pm_admin", OwnerUserId: Guid.NewGuid()), CancellationToken.None);
        withOwner.IsSuccess.Should().BeTrue();
    }

    // ── List / Get scoping ───────────────────────────────────────────────

    [Fact]
    public async Task List_NonAdminSeesOwnMethods_AdminSeesWholeTenant()
    {
        var tenantId = Guid.NewGuid();
        var owner = Guid.NewGuid();
        var otherUser = Guid.NewGuid();
        await SeedAsync(tenantId, owner, "pm_owner_card");
        await SeedAsync(tenantId, otherUser, "pm_other_card");

        var selfList = await _service.ListAsync(OwnerActor(tenantId, owner), tenantId, CancellationToken.None);
        var adminList = await _service.ListAsync(AdminActor(), tenantId, CancellationToken.None);

        selfList.Value.Should().ContainSingle().Which.GatewayPaymentMethodId.Should().Be("pm_owner_card");
        adminList.Value.Should().HaveCount(2);
    }

    [Fact]
    public async Task List_ActorWithoutTenantContext_ShouldFailClosed()
    {
        var result = await _service.ListAsync(new PaymentMethodActor(TenantId: null, Guid.NewGuid(), IsSystemAdmin: false), Guid.NewGuid(), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("PaymentMethods.NoTenantContext");
    }

    [Fact]
    public async Task Get_ForeignTenantMethod_ShouldReturnNotFound_NotForbidden()
    {
        var tenantId = Guid.NewGuid();
        var foreignTenant = Guid.NewGuid();
        var seeded = await SeedAsync(foreignTenant, Guid.NewGuid(), "pm_foreign");

        var result = await _service.GetByIdAsync(OwnerActor(tenantId, Guid.NewGuid()), seeded.Id, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Type.Should().Be(ErrorType.NotFound);
    }

    [Fact]
    public async Task Get_SameTenantNonOwner_ShouldReturnForbidden()
    {
        var tenantId = Guid.NewGuid();
        var owner = Guid.NewGuid();
        var seeded = await SeedAsync(tenantId, owner, "pm_owner_only");

        var result = await _service.GetByIdAsync(OwnerActor(tenantId, Guid.NewGuid()), seeded.Id, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("PaymentMethods.NotOwner");
        result.Error.Type.Should().Be(ErrorType.Forbidden);
    }

    [Fact]
    public async Task Get_OwnerAndAdmin_ShouldSucceed()
    {
        var tenantId = Guid.NewGuid();
        var owner = Guid.NewGuid();
        var seeded = await SeedAsync(tenantId, owner, "pm_visible");

        var asOwner = await _service.GetByIdAsync(OwnerActor(tenantId, owner), seeded.Id, CancellationToken.None);
        var asAdmin = await _service.GetByIdAsync(AdminActor(), seeded.Id, CancellationToken.None);

        asOwner.IsSuccess.Should().BeTrue();
        asOwner.Value.Id.Should().Be(seeded.Id);
        asAdmin.IsSuccess.Should().BeTrue();
    }

    // ── Update / SetDefault ─────────────────────────────────────────────

    [Fact]
    public async Task Update_ShouldChangeDisplayFieldsOnly()
    {
        var tenantId = Guid.NewGuid();
        var owner = Guid.NewGuid();
        var seeded = await SeedAsync(tenantId, owner, "pm_display");

        var result = await _service.UpdateAsync(OwnerActor(tenantId, owner), seeded.Id, new UpdatePaymentMethodRequest("Corp Card", "{\"city\":\"Porto\"}"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Nickname.Should().Be("Corp Card");
        result.Value.BillingAddress.Should().Be("{\"city\":\"Porto\"}");
        result.Value.GatewayPaymentMethodId.Should().Be("pm_display");
    }

    [Fact]
    public async Task Update_NonOwner_ShouldReturnForbidden()
    {
        var tenantId = Guid.NewGuid();
        var seeded = await SeedAsync(tenantId, Guid.NewGuid(), "pm_locked");

        var result = await _service.UpdateAsync(OwnerActor(tenantId, Guid.NewGuid()), seeded.Id, new UpdatePaymentMethodRequest("nope"), CancellationToken.None);

        result.Error.Code.Should().Be("PaymentMethods.NotOwner");
    }

    [Fact]
    public async Task SetDefault_ShouldDemotePreviousDefaultAndRepointSubscriptions()
    {
        var tenantId = Guid.NewGuid();
        var owner = Guid.NewGuid();
        var oldDefault = await SeedAsync(tenantId, owner, "pm_default_old", isDefault: true);
        var candidate = await SeedAsync(tenantId, owner, "pm_default_new");
        _coordination
            .Setup(c => c.RepointSubscriptionsAsync(tenantId, oldDefault.Id, candidate.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(3);

        var result = await _service.SetDefaultAsync(OwnerActor(tenantId, owner), candidate.Id, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        (await _repository.GetByIdAsync(oldDefault.Id, CancellationToken.None))!.IsDefault.Should().BeFalse();
        (await _repository.GetByIdAsync(candidate.Id, CancellationToken.None))!.IsDefault.Should().BeTrue();
        _coordination.Verify(c => c.RepointSubscriptionsAsync(tenantId, oldDefault.Id, candidate.Id, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SetDefault_AlreadyDefault_ShouldNotRepoint()
    {
        var tenantId = Guid.NewGuid();
        var owner = Guid.NewGuid();
        var seeded = await SeedAsync(tenantId, owner, "pm_stays_default", isDefault: true);

        var result = await _service.SetDefaultAsync(OwnerActor(tenantId, owner), seeded.Id, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        _coordination.Verify(
            c => c.RepointSubscriptionsAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task SetDefault_ExpiredMethod_ShouldFailClosed()
    {
        var tenantId = Guid.NewGuid();
        var owner = Guid.NewGuid();
        var expired = await SeedAsync(tenantId, owner, "pm_expired", status: PaymentMethodStatus.Expired);

        var result = await _service.SetDefaultAsync(OwnerActor(tenantId, owner), expired.Id, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("PaymentMethods.NotUsable");
        result.Error.Type.Should().Be(ErrorType.Conflict);
    }

    // ── Delete ──────────────────────────────────────────────────────────

    [Fact]
    public async Task Delete_WithBillableSubscriptions_ShouldFailClosedWithConflict()
    {
        var tenantId = Guid.NewGuid();
        var owner = Guid.NewGuid();
        var seeded = await SeedAsync(tenantId, owner, "pm_in_use");
        _coordination
            .Setup(c => c.CountBillableSubscriptionsOnPaymentMethodAsync(tenantId, seeded.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(2);

        var result = await _service.DeleteAsync(OwnerActor(tenantId, owner), seeded.Id, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("PaymentMethods.InUseBySubscriptions");
        result.Error.Type.Should().Be(ErrorType.Conflict);
        result.Error.Description.Should().Contain("2 active subscription(s)");

        var stillThere = await _repository.GetByIdAsync(seeded.Id, CancellationToken.None);
        stillThere.Should().NotBeNull();
        stillThere!.IsDeleted.Should().BeFalse();
    }

    [Fact]
    public async Task Delete_WithoutSubscriptions_ShouldSoftDelete()
    {
        var tenantId = Guid.NewGuid();
        var owner = Guid.NewGuid();
        var seeded = await SeedAsync(tenantId, owner, "pm_removable");
        _coordination
            .Setup(c => c.CountBillableSubscriptionsOnPaymentMethodAsync(tenantId, seeded.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        var result = await _service.DeleteAsync(OwnerActor(tenantId, owner), seeded.Id, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        (await _repository.GetByIdAsync(seeded.Id, CancellationToken.None)).Should().BeNull();
        var deletedRow = await _context.Set<PaymentMethod>().SingleAsync(pm => pm.Id == seeded.Id);
        deletedRow.IsDeleted.Should().BeTrue();
    }

    [Fact]
    public async Task Delete_NonOwner_ShouldReturnForbidden()
    {
        var tenantId = Guid.NewGuid();
        var seeded = await SeedAsync(tenantId, Guid.NewGuid(), "pm_owner_delete");

        var result = await _service.DeleteAsync(OwnerActor(tenantId, Guid.NewGuid()), seeded.Id, CancellationToken.None);

        result.Error.Code.Should().Be("PaymentMethods.NotOwner");
    }

    // ── Checkout upsert ─────────────────────────────────────────────────

    [Fact]
    public async Task UpsertFromCheckout_ShouldAssignSubscriptionAndMarkUsed()
    {
        var tenantId = Guid.NewGuid();
        var subscriptionId = Guid.NewGuid();
        var billingUser = Guid.NewGuid();
        SetupGatewayDetails("pm_checkout");
        _coordination
            .Setup(c => c.GetSubscriptionBillingUserIdAsync(subscriptionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(billingUser);

        // Service actor without a user subject (UserId empty) resolves the billing owner.
        var actor = new PaymentMethodActor(tenantId, Guid.Empty, IsSystemAdmin: false);
        var result = await _service.UpsertFromCheckoutAsync(actor, tenantId, subscriptionId, "pm_checkout", CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.UserId.Should().Be(billingUser);
        result.Value.IsDefault.Should().BeTrue();
        result.Value.LastUsedAt.Should().NotBeNull();

        _coordination.Verify(
            c => c.AssignSubscriptionPaymentMethodAsync(subscriptionId, result.Value.Id, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task UpsertFromCheckout_ShouldRejectMalformedTokens()
    {
        var actor = new PaymentMethodActor(Guid.NewGuid(), Guid.NewGuid(), IsSystemAdmin: false);

        var result = await _service.UpsertFromCheckoutAsync(actor, actor.TenantId!.Value, Guid.NewGuid(), "not_a_pm", CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("PaymentMethods.InvalidToken");
    }

    [Fact]
    public async Task UpsertFromCheckout_WhenGatewayDetailsUnavailable_ShouldStillRegisterTheReference()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var subscriptionId = Guid.NewGuid();
        _stripeCustomerService
            .Setup(service => service.GetPaymentMethodAsync("pm_gateway_down", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GatewayPaymentMethodDetailsResult(false, null, null, "api_error", "gateway down"));

        var result = await _service.UpsertFromCheckoutAsync(
            new PaymentMethodActor(tenantId, userId, IsSystemAdmin: false),
            tenantId, subscriptionId, "pm_gateway_down", CancellationToken.None);

        result.IsSuccess.Should().BeTrue("checkout already succeeded at the gateway; the local mirror must not fail the flow");
        result.Value.GatewayPaymentMethodId.Should().Be("pm_gateway_down");
        result.Value.CardLast4.Should().BeNull();
    }
}
