using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Options;

namespace GameGuild.Commerce.Payments;

/// <summary>
///     Configuration for the PayPal processing adapter (issue #413), bound from
///     <c>PaymentGateways:PayPal</c>. Fail-closed by default: the adapter is disabled until
///     explicitly enabled, the environment defaults to sandbox, and enabling real API calls
///     without credentials fails startup validation (and every call re-checks, so a runtime
///     misconfiguration can never fall through to a silent success).
/// </summary>
public sealed class PayPalGatewayOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "PaymentGateways:PayPal";

    /// <summary>
    ///     Whether the PayPal adapter participates in gateway routing. Default false
    ///     (fail-closed): no PayPal charge is attempted until an operator opts in.
    /// </summary>
    public bool IsEnabled { get; set; }

    /// <summary>
    ///     Whether to return deterministic simulated responses instead of calling the
    ///     PayPal API. Intended for development and testing; when false the adapter
    ///     performs real HTTP calls (sandbox by default).
    /// </summary>
    public bool UseSimulation { get; set; }

    /// <summary>
    ///     PayPal environment: <c>sandbox</c> (default) or <c>live</c>. The sandbox default
    ///     means a misconfigured deployment can only ever reach the sandbox API.
    /// </summary>
    [Required]
    public string Environment { get; set; } = "sandbox";

    /// <summary>PayPal REST app client id; required for real API calls.</summary>
    public string ClientId { get; set; } = string.Empty;

    /// <summary>PayPal REST app client secret; required for real API calls.</summary>
    public string ClientSecret { get; set; } = string.Empty;

    /// <summary>
    ///     Optional explicit API base URL (for tests or proxies). Empty derives the base
    ///     URL from <see cref="Environment" />.
    /// </summary>
    public string ApiBaseUrl { get; set; } = string.Empty;

    /// <summary>Per-request timeout for PayPal HTTP calls, in seconds.</summary>
    [Range(1, 300)]
    public int RequestTimeoutSeconds { get; set; } = 30;

    /// <summary>
    ///     Resolves the API base URL: the explicit override when set, otherwise the
    ///     sandbox/live PayPal endpoint. Unknown environments resolve to the sandbox
    ///     endpoint (fail-closed to the safer side).
    /// </summary>
    public string ResolveBaseUrl() =>
        !string.IsNullOrWhiteSpace(ApiBaseUrl)
            ? ApiBaseUrl.TrimEnd('/')
            : string.Equals(Environment, "live", StringComparison.OrdinalIgnoreCase)
                ? "https://api-m.paypal.com"
                : "https://api-m.sandbox.paypal.com";

    /// <summary>The environment label recorded on provider mappings ("sandbox" or "live").</summary>
    public string EnvironmentLabel =>
        string.Equals(Environment, "live", StringComparison.OrdinalIgnoreCase) ? "live" : "sandbox";
}

/// <summary>
///     Startup validation for <see cref="PayPalGatewayOptions" />. Fail-closed: enabling the
///     adapter for real API calls without credentials (or with an invalid environment or
///     base URL) fails fast instead of surfacing at the first charge.
/// </summary>
public sealed class PayPalGatewayOptionsValidator : IValidateOptions<PayPalGatewayOptions>
{
    /// <inheritdoc />
    public ValidateOptionsResult Validate(string? name, PayPalGatewayOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var failures = new List<string>();

        if (!string.Equals(options.Environment, "sandbox", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(options.Environment, "live", StringComparison.OrdinalIgnoreCase))
        {
            failures.Add($"{nameof(PayPalGatewayOptions.Environment)} must be 'sandbox' or 'live'.");
        }

        if (options.RequestTimeoutSeconds is < 1 or > 300)
        {
            failures.Add($"{nameof(PayPalGatewayOptions.RequestTimeoutSeconds)} must be between 1 and 300 seconds.");
        }

        if (options.IsEnabled && !options.UseSimulation)
        {
            if (string.IsNullOrWhiteSpace(options.ClientId))
            {
                failures.Add(
                    $"{nameof(PayPalGatewayOptions.ClientId)} is required when PayPal is enabled without simulation.");
            }

            if (string.IsNullOrWhiteSpace(options.ClientSecret))
            {
                failures.Add(
                    $"{nameof(PayPalGatewayOptions.ClientSecret)} is required when PayPal is enabled without simulation.");
            }
        }

        if (!string.IsNullOrWhiteSpace(options.ApiBaseUrl))
        {
            if (!Uri.TryCreate(options.ApiBaseUrl, UriKind.Absolute, out var baseUri))
            {
                failures.Add($"{nameof(PayPalGatewayOptions.ApiBaseUrl)} must be an absolute URL when set.");
            }
            else if (baseUri.Scheme is not "http" and not "https")
            {
                failures.Add($"{nameof(PayPalGatewayOptions.ApiBaseUrl)} must use http or https.");
            }
        }

        return failures.Count > 0
            ? ValidateOptionsResult.Fail(failures)
            : ValidateOptionsResult.Success;
    }
}
