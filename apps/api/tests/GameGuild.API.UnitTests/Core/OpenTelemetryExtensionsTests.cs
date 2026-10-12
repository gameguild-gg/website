using System.Reflection;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using GameGuild.API.Setup;
using OpenTelemetry.Metrics;
using OpenTelemetry.Exporter;
using OpenTelemetry.Trace;

namespace GameGuild.API.UnitTests.Core;

public sealed class OpenTelemetryExtensionsTests
{
    [Fact]
    public void AddOpenTelemetryObservability_WhenSectionIsMissing_ReturnsBuilderWithoutProvider()
    {
        var builder = CreateBuilder(new Dictionary<string, string?>());
        builder.Configuration.Sources.Clear();

        var result = builder.AddOpenTelemetryObservability();

        result.Should().BeSameAs(builder);
        builder.Services.Should().NotContain(descriptor => descriptor.ServiceType == typeof(TracerProvider));
        builder.Services.Should().NotContain(descriptor => descriptor.ServiceType == typeof(MeterProvider));
    }

    [Fact]
    public void AddOpenTelemetryObservability_WhenDisabled_ReturnsBuilderWithoutProvider()
    {
        var builder = CreateBuilder(new Dictionary<string, string?>
        {
            ["OpenTelemetry:Enabled"] = "false"
        });

        var result = builder.AddOpenTelemetryObservability();

        result.Should().BeSameAs(builder);
        builder.Services.Should().NotContain(descriptor => descriptor.ServiceType == typeof(TracerProvider));
        builder.Services.Should().NotContain(descriptor => descriptor.ServiceType == typeof(MeterProvider));
    }

    [Theory]
    [InlineData("", true, null, null)]
    [InlineData("custom-api", true, "http://127.0.0.1:4317", "grpc")]
    public void AddOpenTelemetryObservability_WhenEnabled_RegistersResolvableTracing(
        string serviceName,
        bool consoleExporterEnabled,
        string? otlpEndpoint,
        string? otlpProtocol)
    {
        var builder = CreateBuilder(new Dictionary<string, string?>
        {
            ["OpenTelemetry:Enabled"] = "true",
            ["OpenTelemetry:ServiceName"] = serviceName,
            ["OpenTelemetry:ConsoleExporterEnabled"] = consoleExporterEnabled.ToString(),
            ["OpenTelemetry:OtlpEndpoint"] = otlpEndpoint,
            ["OpenTelemetry:OtlpProtocol"] = otlpProtocol,
            ["OpenTelemetry:IncludeSqlStatements"] = "true"
        });

        builder.AddOpenTelemetryObservability();
        using var provider = builder.Services.BuildServiceProvider();

        provider.GetRequiredService<TracerProvider>().Should().NotBeNull();
        provider.GetRequiredService<MeterProvider>().Should().NotBeNull();
    }

    [Fact]
    public void AddOpenTelemetryObservability_WhenEnabledWithoutExporter_ThrowsConfigurationError()
    {
        var builder = CreateBuilder(new Dictionary<string, string?>
        {
            ["OpenTelemetry:Enabled"] = "true",
            ["OpenTelemetry:ConsoleExporterEnabled"] = "false"
        });

        var act = () => builder.AddOpenTelemetryObservability();

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*no exporter is configured*");
    }

    [Theory]
    [InlineData("relative/collector")]
    [InlineData("ftp://collector:4318")]
    public void AddOpenTelemetryObservability_WhenEnabledWithInvalidEndpoint_ThrowsConfigurationError(
        string endpoint)
    {
        var builder = CreateBuilder(new Dictionary<string, string?>
        {
            ["OpenTelemetry:Enabled"] = "true",
            ["OpenTelemetry:OtlpEndpoint"] = endpoint
        });

        var act = () => builder.AddOpenTelemetryObservability();

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*absolute HTTP or HTTPS URI*");
    }

    [Theory]
    [InlineData("grpc", OtlpExportProtocol.Grpc)]
    [InlineData("GRPC", OtlpExportProtocol.Grpc)]
    [InlineData("http/protobuf", OtlpExportProtocol.HttpProtobuf)]
    [InlineData("HTTP/PROTOBUF", OtlpExportProtocol.HttpProtobuf)]
    [InlineData(null, OtlpExportProtocol.HttpProtobuf)]
    public void ResolveProtocol_ShouldMapSupportedValues(string? protocol, OtlpExportProtocol expected)
    {
        InvokePrivate<OtlpExportProtocol>("ResolveProtocol", protocol).Should().Be(expected);
    }

    [Fact]
    public void AddOpenTelemetryObservability_WhenEnabledWithUnsupportedProtocol_ThrowsConfigurationError()
    {
        var builder = CreateBuilder(new Dictionary<string, string?>
        {
            ["OpenTelemetry:Enabled"] = "true",
            ["OpenTelemetry:OtlpEndpoint"] = "http://127.0.0.1:4318",
            ["OpenTelemetry:OtlpProtocol"] = "http/json"
        });

        var act = () => builder.AddOpenTelemetryObservability();

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*unsupported*");
    }

    [Theory]
    [InlineData("/health", true)]
    [InlineData("/health/dependencies", true)]
    [InlineData("/live", true)]
    [InlineData("/ready", true)]
    [InlineData("/api/health-report", false)]
    public void IsHealthPath_ShouldRecognizeOperationalEndpoints(string path, bool expected)
    {
        InvokePrivate<bool>("IsHealthPath", new PathString(path)).Should().Be(expected);
    }

    private static WebApplicationBuilder CreateBuilder(IReadOnlyDictionary<string, string?> values)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = "Testing"
        });
        builder.Configuration.AddInMemoryCollection(values);
        return builder;
    }

    private static T InvokePrivate<T>(string name, object? argument)
    {
        var method = typeof(OpenTelemetryExtensions).GetMethod(name, BindingFlags.Static | BindingFlags.NonPublic);
        method.Should().NotBeNull();
        return method!.Invoke(null, [argument]).Should().BeAssignableTo<T>().Subject;
    }
}
