using System.Reflection;
using Asp.Versioning;
using FluentAssertions;
using GameGuild.CQRS;
using GameGuild.Identity.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace GameGuild.Commerce.Billing.UnitTests.Controllers;

public sealed class PricingDecisionLedgerControllerTests
{
    private readonly Mock<ISender> _sender = new();

    private PricingDecisionLedgerController CreateController() => new(_sender.Object)
    {
        ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
    };

    // ── Authorization and routing surface (fail-closed admin monitoring) ──────────

    [Fact]
    public void Controller_RequiresSystemAdminPolicy()
    {
        var authorizeAttribute = typeof(PricingDecisionLedgerController)
            .GetCustomAttribute<AuthorizeAttribute>();

        authorizeAttribute.Should().NotBeNull(
            "every pricing-decision ledger endpoint must declare an authorization decision");
        authorizeAttribute!.Policy.Should().Be(Policies.SystemAdmin,
            "ledger inspection matches the sibling billing events monitoring policy");
    }

    [Fact]
    public void Controller_IsVersionedOnTheIssue400Route()
    {
        typeof(PricingDecisionLedgerController)
            .GetCustomAttribute<ApiVersionAttribute>()!
            .Versions.Select(version => version.ToString()).Should().Contain("1.0");
        typeof(PricingDecisionLedgerController)
            .GetCustomAttribute<RouteAttribute>()!
            .Template.Should().Be("api/v{version:apiVersion}/billing/pricing-discounts-ledger");
    }

    [Fact]
    public void AllActions_AreCoveredByTheControllerLevelAuthorizationDecision()
    {
        var actions = typeof(PricingDecisionLedgerController)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(method => method.IsPublic && !method.IsSpecialName)
            .ToList();

        actions.Should().NotBeEmpty();

        foreach (var action in actions)
        {
            var hasAllowAnonymous = action.GetCustomAttribute<AllowAnonymousAttribute>() is not null;
            hasAllowAnonymous.Should().BeFalse(
                "ledger reads and validation must never be anonymous (action {0})", action.Name);
        }
    }

    // ── Query passthrough ─────────────────────────────────────────────────────────

    [Fact]
    public async Task ListEntries_PassesAllFiltersToTheQuery()
    {
        var tenantId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var from = new DateTimeOffset(2026, 10, 1, 0, 0, 0, TimeSpan.Zero);
        var to = new DateTimeOffset(2026, 10, 8, 0, 0, 0, TimeSpan.Zero);
        ListPricingDecisionLedgerEntriesQuery? captured = null;
        _sender
            .Setup(sender => sender.Send(It.IsAny<ListPricingDecisionLedgerEntriesQuery>(), It.IsAny<CancellationToken>()))
            .Callback<GameGuild.CQRS.IRequest<PagedResult<PricingDecisionLedgerEntryDto>>, CancellationToken>((query, _) => captured = (ListPricingDecisionLedgerEntriesQuery)query)
            .ReturnsAsync(new PagedResult<PricingDecisionLedgerEntryDto>([], 0, 0, 20));

        var controller = CreateController();
        await controller.ListEntries(tenantId, orderId, null, "welcome", "CheckoutLineItem", "Drifted", from, to, 5, 15);

        captured.Should().NotBeNull();
        captured!.TenantId.Should().Be(tenantId);
        captured.OrderId.Should().Be(orderId);
        captured.PromoCode.Should().Be("welcome");
        captured.DecisionType.Should().Be("CheckoutLineItem");
        captured.DriftStatus.Should().Be("Drifted");
        captured.FromUtc.Should().Be(from);
        captured.ToUtc.Should().Be(to);
        captured.Skip.Should().Be(5);
        captured.Take.Should().Be(15);
    }

    [Fact]
    public async Task GetEntry_ReturnsNotFoundForMissingEntries()
    {
        _sender
            .Setup(sender => sender.Send(It.IsAny<GetPricingDecisionLedgerEntryQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((PricingDecisionLedgerEntryDetailDto?)null);

        var controller = CreateController();
        var result = await controller.GetEntry(Guid.NewGuid());

        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task GetEffectiveness_PassesWindowAndTenant()
    {
        var from = new DateTimeOffset(2026, 9, 1, 0, 0, 0, TimeSpan.Zero);
        var tenantId = Guid.NewGuid();
        GetPricingLedgerEffectivenessQuery? captured = null;
        _sender
            .Setup(sender => sender.Send(It.IsAny<GetPricingLedgerEffectivenessQuery>(), It.IsAny<CancellationToken>()))
            .Callback<GameGuild.CQRS.IRequest<PricingLedgerEffectivenessAggregate>, CancellationToken>((query, _) => captured = (GetPricingLedgerEffectivenessQuery)query)
            .ReturnsAsync(new PricingLedgerEffectivenessAggregate(
                from.UtcDateTime, null, tenantId,
                new PricingLedgerTotals(0, 0, 0, 0, 0, 0, 0),
                [], [], [], []));

        var controller = CreateController();
        await controller.GetEffectiveness(from, null, tenantId);

        captured.Should().NotBeNull();
        captured!.FromUtc.Should().Be(from);
        captured.TenantId.Should().Be(tenantId);
    }

    [Fact]
    public async Task ValidateDrift_AppliesRequestDefaults()
    {
        ValidatePricingLedgerDriftCommand? captured = null;
        _sender
            .Setup(sender => sender.Send(It.IsAny<ValidatePricingLedgerDriftCommand>(), It.IsAny<CancellationToken>()))
            .Callback<GameGuild.CQRS.IRequest<PricingLedgerDriftValidationResult>, CancellationToken>((command, _) => captured = (ValidatePricingLedgerDriftCommand)command)
            .ReturnsAsync(new PricingLedgerDriftValidationResult(0, 0, 0, 0, 0, null, null, DateTime.UtcNow, []));

        var controller = CreateController();
        await controller.ValidateDrift(null, CancellationToken.None);

        captured.Should().NotBeNull();
        captured!.SampleSize.Should().Be(25, "the default sample is 25 entries");
    }
}
