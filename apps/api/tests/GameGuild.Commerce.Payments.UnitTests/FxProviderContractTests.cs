using System.Net;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace GameGuild.Commerce.Payments.UnitTests;

public class FxProviderContractTests
{
    private const string OpenRates = """{"base":"USD","timestamp":1792843200,"rates":{"USD":1,"EUR":0.9,"JPY":150,"XXY":3}}""";
    private const string EcbRates = """<Envelope><Cube><Cube time="2026-10-24"><Cube currency="USD" rate="1.1"/></Cube></Cube></Envelope>""";

    [Fact]
    public void OpenExchangeRates_UsesProviderBaseAndDropsIdentityAndUnknownPairs()
    {
        var set = OpenExchangeRatesFxRateProvider.Parse(OpenRates, "USD");
        set.BaseCurrency.Should().Be("USD");
        set.Rates.Should().HaveCount(2);
        set.Rates["EUR"].Should().Be(0.9m);
        set.Rates.Should().NotContainKey("USD");
        set.Rates.Should().NotContainKey("XXY");
        set.RawResponse.Should().Be(OpenRates);
    }

    [Theory]
    [InlineData("{\"timestamp\":1792843200,\"rates\":{\"EUR\":0.9}}")]
    [InlineData("{\"base\":\"GBP\",\"timestamp\":1792843200,\"rates\":{\"EUR\":0.9}}")]
    [InlineData("{\"base\":\"USD\",\"rates\":{\"EUR\":0.9}}")]
    [InlineData("{\"base\":\"USD\",\"timestamp\":\"unknown\",\"rates\":{\"EUR\":0.9}}")]
    [InlineData("{\"base\":\"USD\",\"timestamp\":9223372036854775807,\"rates\":{\"EUR\":0.9}}")]
    [InlineData("[]")]
    public void OpenExchangeRates_RejectsMissingOrMismatchedProvenance(string payload)
    {
        var parse = () => OpenExchangeRatesFxRateProvider.Parse(payload, "USD");
        parse.Should().Throw<FxRateProviderUnavailableException>();
    }

    [Fact]
    public void Ecb_CannotRelabelEuroGridAsAnotherBase()
    {
        var parse = () => EcbFxRateProvider.Parse(EcbRates, "USD", null);
        parse.Should().Throw<FxRateProviderUnavailableException>();
    }

    [Theory]
    [InlineData("<invalid")]
    [InlineData("<Envelope><Cube time=\"invalid\"><Cube currency=\"USD\" rate=\"1.1\"/></Cube></Envelope>")]
    [InlineData("<Envelope><Cube time=\"2026-10-24\"><Cube currency=\"USD\" rate=\"invalid\"/></Cube></Envelope>")]
    public void Ecb_InvalidPayloadUsesProviderFailureSoFallbackCanRun(string payload)
    {
        var parse = () => EcbFxRateProvider.Parse(payload, "EUR", null);
        parse.Should().Throw<FxRateProviderUnavailableException>();
    }

    [Theory]
    [InlineData("openexchangerates", 429)]
    [InlineData("openexchangerates", 503)]
    [InlineData("ecb", 429)]
    [InlineData("ecb", 503)]
    public async Task TransientProviderHttpStatus_RemainsRetryable(string providerName, int status)
    {
        using var client = new HttpClient(new ResponseHandler("sensitive-provider-response", (HttpStatusCode)status));
        IFxRateProvider provider = providerName == "ecb"
            ? new EcbFxRateProvider(client, Options.Create(new FxSyncOptions()), NullLogger<EcbFxRateProvider>.Instance)
            : new OpenExchangeRatesFxRateProvider(client, Options.Create(new FxSyncOptions
            {
                OpenExchangeRates = new() { AppId = "fixture-app-id" },
            }), NullLogger<OpenExchangeRatesFxRateProvider>.Instance);

        var act = () => provider.GetRatesAsync();
        var failure = (await act.Should().ThrowAsync<HttpRequestException>()).Which;
        failure.StatusCode.Should().Be((HttpStatusCode)status);
        failure.Message.Should().NotContain("sensitive-provider-response");
    }

    [Theory]
    [InlineData("missing-fallback-key")]
    [InlineData("insecure-fallback-url")]
    [InlineData("insecure-primary-url")]
    [InlineData("wrong-ecb-base")]
    [InlineData("wrong-oxr-base")]
    public void SelectedFallbackAndPrimaryConfiguration_AreBothValidated(string scenario)
    {
        var options = new FxSyncOptions
        {
            Provider = "ecb", FallbackProvider = "openexchangerates",
            OpenExchangeRates = new() { AppId = "fixture-app-id" },
        };
        switch (scenario)
        {
            case "missing-fallback-key": options.OpenExchangeRates.AppId = ""; break;
            case "insecure-fallback-url": options.OpenExchangeRates.BaseUrl = "http://rates.example.invalid"; break;
            case "insecure-primary-url": options.Ecb.BaseUrl = "http://rates.example.invalid"; break;
            case "wrong-ecb-base": options.Ecb.BaseCurrency = "USD"; break;
            default: options.OpenExchangeRates.BaseCurrency = "XXY"; break;
        }

        new FxSyncOptionsValidator().Validate(null, options).Failed.Should().BeTrue();
    }

    [Theory]
    [InlineData("openexchangerates", nameof(OpenExchangeRatesFxRateProvider))]
    [InlineData("ecb", nameof(EcbFxRateProvider))]
    public async Task RegisteredProvider_UsesItsConfiguredTypedClient(string providerName, string expectedClientName)
    {
        var settings = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["FxRates:Provider"] = providerName,
            ["FxRates:OpenExchangeRates:AppId"] = "fixture-app-id",
            ["FxRates:AlertRecipientIds:0"] = Guid.NewGuid().ToString(),
        }).Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddFxRateSync(settings);
        var factory = new RecordingClientFactory(providerName == "ecb" ? EcbRates : OpenRates);
        services.AddSingleton<IHttpClientFactory>(factory);
        using var container = services.BuildServiceProvider();
        using var scope = container.CreateScope();
        var provider = scope.ServiceProvider.GetServices<IFxRateProvider>().Single(value => value.Name == providerName);

        var grid = await provider.GetRatesAsync();

        grid.ProviderName.Should().Be(providerName);
        factory.Names.Should().ContainSingle().Which.Should().Be(expectedClientName);
    }

    private sealed class ResponseHandler(string payload, HttpStatusCode status = HttpStatusCode.OK) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(payload) });
    }

    private sealed class RecordingClientFactory(string payload) : IHttpClientFactory
    {
        public List<string> Names { get; } = [];
        public HttpClient CreateClient(string name)
        {
            Names.Add(name);
            return new HttpClient(new ResponseHandler(payload));
        }
    }
}
