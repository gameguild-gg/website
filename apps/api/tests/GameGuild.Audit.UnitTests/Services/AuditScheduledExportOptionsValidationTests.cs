using FluentAssertions;
using GameGuild.Compliance.Audit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace GameGuild.Tests.Audit.Unit.Services;

public sealed class AuditScheduledExportOptionsValidationTests
{
    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("-30")]
    public void NonPositiveStaleClaimThresholdMinutes_FailsOptionsValidation(string configuredMinutes)
    {
        using var provider = BuildProvider(configuredMinutes);

        var act = () => provider.GetRequiredService<IOptions<AuditScheduledExportOptions>>().Value;

        act.Should().Throw<OptionsValidationException>()
            .Which.Message.Should().Contain("positive");
    }

    [Fact]
    public void PositiveStaleClaimThresholdMinutes_IsAccepted()
    {
        using var provider = BuildProvider("5");

        var options = provider.GetRequiredService<IOptions<AuditScheduledExportOptions>>().Value;

        options.StaleClaimThreshold.Should().Be(TimeSpan.FromMinutes(5));
    }

    [Fact]
    public void UnsetStaleClaimThresholdMinutes_KeepsSafeDefault()
    {
        using var provider = BuildProvider(null);

        var options = provider.GetRequiredService<IOptions<AuditScheduledExportOptions>>().Value;

        options.StaleClaimThreshold.Should().Be(TimeSpan.FromMinutes(30));
    }

    private static ServiceProvider BuildProvider(string? configuredMinutes)
    {
        var values = new Dictionary<string, string?>();
        if (configuredMinutes is not null)
        {
            values[$"{AuditScheduledExportOptions.ConfigurationSection}:{AuditScheduledExportOptions.StaleClaimThresholdMinutesKey}"] = configuredMinutes;
        }

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        AuditModule.AddAuditServices(services);
        return services.BuildServiceProvider();
    }
}
