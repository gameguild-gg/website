using FluentAssertions;
using GameGuild.Finance.Economy.Risk;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace GameGuild.TrustSafety.UnitTests;

public sealed class TrustSafetyModuleTests
{
    [Fact]
    public async Task DisabledCompositionReturnsAuditableUnavailableEvidence()
    {
        var module = new TrustSafetyModule();
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder().Build();
        var now = new DateTimeOffset(2026, 7, 19, 12, 0, 0, TimeSpan.Zero);

        module.Name.Should().Be("TrustSafety");
        module.EnabledByDefault.Should().BeTrue();
        module.ConfigureServices(services, configuration).Should().BeSameAs(services);
        services.AddTrustSafetyComposition().Should().BeSameAs(services);

        using var provider = services.BuildServiceProvider();
        var source = provider.GetRequiredService<ITrustSafetyRiskInputSource>();
        var input = await source.ReadAsync("opaque-actor", now);
        input.Outcome.Should().Be(ExternalRiskOutcome.Unavailable);
        input.IsAuditable.Should().BeTrue();
        input.ToEvidence().Source.Should().Be(ExternalRiskSource.TrustSafety);
        await FluentActions.Awaiting(() => source.ReadAsync(" ", now).AsTask())
            .Should().ThrowAsync<ArgumentException>();
    }
}
