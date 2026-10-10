using Xunit;

namespace GameGuild.Commerce.Payments.UnitTests;

// Tests that assert the SDK's process-wide configuration must not overlap
// other test classes whose gateway constructors also initialize that state.
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class StripeProcessConfigurationCollection
{
    private StripeProcessConfigurationCollection() { }

    internal const string Name = "Stripe process configuration";
}
