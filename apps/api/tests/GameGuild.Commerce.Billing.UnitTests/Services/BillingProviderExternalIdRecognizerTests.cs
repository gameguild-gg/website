using FluentAssertions;
using GameGuild.Commerce.Billing;
using Xunit;

namespace GameGuild.Commerce.Billing.UnitTests.Services;

public class BillingProviderExternalIdRecognizerTests
{
    [Theory]
    [InlineData("sub_123456", PaymentProviders.Stripe)]
    [InlineData("I-ABC123", PaymentProviders.PayPal)]
    [InlineData("1234567890", PaymentProviders.AppleAppStore)]
    public void TryRecognize_Should_Attribute_Documented_Identifier_Prefixes(string externalId, string expectedProvider)
    {
        var recognized = BillingProviderExternalIdRecognizer.TryRecognize(externalId, out var providerKey);

        recognized.Should().BeTrue();
        providerKey.Should().Be(expectedProvider);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void TryRecognize_Should_Reject_Missing_Identifiers(string? externalId)
    {
        var recognized = BillingProviderExternalIdRecognizer.TryRecognize(externalId, out _);

        recognized.Should().BeFalse();
    }

    [Theory]
    [InlineData("goog.somepurchase.token")] // Google purchase tokens have no stable public prefix
    [InlineData("SUB_uppercase")] // prefix matching is ordinal case-sensitive
    [InlineData("cus_customer_id")] // customer identifiers are not subscription identifiers
    public void TryRecognize_Should_Fail_Closed_For_Unattributable_Identifiers(string externalId)
    {
        var recognized = BillingProviderExternalIdRecognizer.TryRecognize(externalId, out _);

        // Fail-closed: never guess an attribution.
        recognized.Should().BeFalse();
    }
}
