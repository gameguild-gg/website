using FluentAssertions;
using GameGuild.Finance.Economy.Risk;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace GameGuild.Compliance.FinancialCrime.UnitTests;

public sealed class FinancialCrimeModuleTests
{
    [Fact]
    public async Task DisabledCompositionReturnsAuditableUnavailableEvidence()
    {
        var module = new FinancialCrimeModule();
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder().Build();
        var now = new DateTimeOffset(2026, 7, 19, 12, 0, 0, TimeSpan.Zero);

        module.Name.Should().Be("Compliance.FinancialCrime");
        module.EnabledByDefault.Should().BeTrue();
        module.ConfigureServices(services, configuration).Should().BeSameAs(services);
        services.AddFinancialCrimeComposition().Should().BeSameAs(services);

        using var provider = services.BuildServiceProvider();
        var source = provider.GetRequiredService<IFinancialCrimeRiskInputSource>();
        var input = await source.ReadAsync("opaque-actor", now);
        input.Outcome.Should().Be(ExternalRiskOutcome.Unavailable);
        input.IsAuditable.Should().BeTrue();
        input.ToEvidence().Source.Should().Be(ExternalRiskSource.FinancialCrime);
        await FluentActions.Awaiting(() => source.ReadAsync(" ", now).AsTask())
            .Should().ThrowAsync<ArgumentException>();
    }
}
