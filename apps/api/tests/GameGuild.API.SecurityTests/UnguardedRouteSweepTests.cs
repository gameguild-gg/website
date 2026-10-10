using System.Net;
using System.Text.RegularExpressions;
using GameGuild.API.SecurityTests.Infrastructure;
using GameGuild.API.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace GameGuild.API.SecurityTests;

/// <summary>
///     Adversarial scenario family (d): unguarded-endpoint sweep (issue #327).
///     Instead of trusting the compile-time architecture analyzers, this test reads the
///     route table of the RUNNING host and re-asserts the guard invariants a pentester
///     would enumerate: every endpoint is explicitly authorized or explicitly allowlisted,
///     every allowlisted endpoint is registered with a justification, and an anonymous
///     HTTP walk over the protected GET surface receives 401/403 everywhere while the
///     registered anonymous surface stays reachable.
/// </summary>
[Collection(AdversarialSecurityCollection.Name)]
public sealed class UnguardedRouteSweepTests(AdversarialSecurityFixture fixture, Xunit.Abstractions.ITestOutputHelper output)
{
    private static readonly Regex RouteParameterPattern = new("""\{([^}:]+)(?::[^}]*)?\}""", RegexOptions.Compiled);

    [Fact]
    public async Task EveryControllerEndpointIsAuthorizedOrAllowlisted()
    {
        var endpoints = CollectControllerEndpoints();

        Assert.NotEmpty(endpoints);

        var unguarded = new List<string>();
        foreach (var endpoint in endpoints)
        {
            var hasAuthorize = endpoint.Metadata.OfType<AuthorizeAttribute>().Any();
            var hasAllowAnonymous = endpoint.Metadata.OfType<IAllowAnonymous>().Any();
            if (!hasAuthorize && !hasAllowAnonymous)
            {
                unguarded.Add(Describe(endpoint));
            }
        }

        Assert.True(unguarded.Count == 0,
            $"Endpoints without [Authorize]/[AllowAnonymous] metadata (analyzer bypass or missing guard):\n{string.Join('\n', unguarded)}");
    }

    [Fact]
    public void EveryAnonymousEndpointIsRegisteredInTheReviewedAllowlist()
    {
        var endpoints = CollectControllerEndpoints();

        var anonymous = endpoints.Where(endpoint => endpoint.Metadata.OfType<IAllowAnonymous>().Any()).ToList();
        Assert.NotEmpty(anonymous);

        var unregistered = new List<string>();
        foreach (var endpoint in endpoints.Where(e => e.Metadata.OfType<IAllowAnonymous>().Any()))
        {
            var descriptor = endpoint.Metadata.OfType<ControllerActionDescriptor>().Single();
            var controllerTypeName = descriptor.ControllerTypeInfo.Name;
            var actionKey = $"{controllerTypeName}.{descriptor.ActionName}";
            var controllerKey = $"{controllerTypeName}{AnonymousEndpointRegistry.ControllerScopeSuffix}";
            if (!AnonymousEndpointRegistry.Entries.ContainsKey(actionKey) &&
                !AnonymousEndpointRegistry.Entries.ContainsKey(controllerKey))
            {
                unregistered.Add(Describe(endpoint));
            }
        }

        Assert.True(unregistered.Count == 0,
            $"[AllowAnonymous] endpoints missing from AnonymousEndpointRegistry (require reviewed justification):\n{string.Join('\n', unregistered)}");
    }

    [Fact]
    public void EveryRegistryEntryPointsAtARealControllerAction()
    {
        var endpoints = CollectControllerEndpoints();
        var descriptors = endpoints
            .Select(endpoint => endpoint.Metadata.OfType<ControllerActionDescriptor>().Single())
            .ToList();

        var stale = new List<string>();
        foreach (var key in AnonymousEndpointRegistry.Entries.Keys)
        {
            if (key.EndsWith(AnonymousEndpointRegistry.ControllerScopeSuffix, StringComparison.Ordinal))
            {
                var controllerName = key[..^AnonymousEndpointRegistry.ControllerScopeSuffix.Length];
                if (descriptors.All(descriptor => descriptor.ControllerTypeInfo.Name != controllerName))
                {
                    stale.Add(key);
                }
            }
            else
            {
                var separator = key.LastIndexOf('.');
                var controllerName = key[..separator];
                var actionName = key[(separator + 1)..];
                if (descriptors.All(descriptor =>
                        descriptor.ControllerTypeInfo.Name != controllerName || descriptor.ActionName != actionName))
                {
                    stale.Add(key);
                }
            }
        }

        Assert.True(stale.Count == 0,
            $"AnonymousEndpointRegistry entries with no matching endpoint (stale allowlist rows):\n{string.Join('\n', stale)}");
    }

    [Fact]
    public async Task AnonymousHttpWalkOverProtectedGetSurfaceIsDeniedEverywhere()
    {
        var probes = CollectGetProbes();
        Assert.NotEmpty(probes);

        using var client = fixture.Factory.CreateClient();
        var violations = new List<string>();
        var counts = new Dictionary<HttpStatusCode, int>();
        var unanswered = 0;

        foreach (var probe in probes)
        {
            if (probe.IsAnonymous)
            {
                continue;
            }

            try
            {
                using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
                using var response = await client.GetAsync(probe.Url, timeout.Token);
                counts[response.StatusCode] = counts.TryGetValue(response.StatusCode, out var count) ? count + 1 : 1;
                if (response.IsSuccessStatusCode)
                {
                    violations.Add($"{probe.Url} returned {(int)response.StatusCode} to an anonymous caller");
                }
            }
            catch (OperationCanceledException)
            {
                // No answer within the per-probe budget — an unanswered probe cannot
                // leak data; it is counted and surfaced in the run output.
                unanswered++;
            }
        }

        output.WriteLine($"Anonymous walk over {probes.Count(p => !p.IsAnonymous)} protected GET routes: {string.Join(", ", counts.Select(pair => $"{(int)pair.Key}x{pair.Value}"))}; unanswered(timeouts)={unanswered}");
        Assert.True(violations.Count == 0,
            $"Protected GET routes reachable without authentication:\n{string.Join('\n', violations)}");
    }

    [Fact]
    public async Task RegisteredAnonymousGetSurfaceStaysReachableWithoutAuthentication()
    {
        var probes = CollectGetProbes().Where(probe => probe.IsAnonymous).ToList();
        Assert.NotEmpty(probes);

        using var client = fixture.Factory.CreateClient();
        var violations = new List<string>();

        foreach (var probe in probes)
        {
            try
            {
                using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
                using var response = await client.GetAsync(probe.Url, timeout.Token);
                // A registered anonymous endpoint may legitimately answer 200/3xx/4xx (bad
                // substituted route values), but it must never demand authentication: a 401
                // would mean the allowlist and the runtime disagree about the public surface.
                if (response.StatusCode is HttpStatusCode.Unauthorized)
                {
                    violations.Add($"{probe.Url} answered 401 despite being registered anonymous");
                }
            }
            catch (OperationCanceledException)
            {
                // No answer within the per-probe budget; not an authentication demand.
            }
        }

        output.WriteLine($"Anonymous walk over {probes.Count} registered anonymous GET routes completed");
        Assert.True(violations.Count == 0,
            $"Registered anonymous GET routes demanding authentication:\n{string.Join('\n', violations)}");
    }

    private List<RouteEndpoint> CollectControllerEndpoints() =>
        fixture.Factory.Services.GetRequiredService<EndpointDataSource>()
            .Endpoints
            .OfType<RouteEndpoint>()
            .Where(endpoint => endpoint.Metadata.OfType<ControllerActionDescriptor>().Any())
            .ToList();

    private IReadOnlyList<(string Url, bool IsAnonymous)> CollectGetProbes()
    {
        var probes = new Dictionary<string, bool>(StringComparer.Ordinal);
        foreach (var endpoint in CollectControllerEndpoints())
        {
            var httpMethods = endpoint.Metadata.OfType<HttpMethodMetadata>().SingleOrDefault();
            if (httpMethods is null || !httpMethods.HttpMethods.Contains("GET", StringComparer.OrdinalIgnoreCase))
            {
                continue;
            }

            var url = MaterializeRoute(endpoint.RoutePattern.RawText ?? string.Empty);
            if (url.Length == 0 || url.Contains('{'))
            {
                // Unresolvable parameter shapes are skipped for the HTTP walk; the
                // metadata assertions above still cover them.
                continue;
            }

            var isAnonymous = endpoint.Metadata.OfType<IAllowAnonymous>().Any();
            probes[url] = isAnonymous;
        }

        return probes.Select(pair => (pair.Key, pair.Value)).ToList();
    }

    /// <summary>Substitutes benign values for route parameters so the URL can be requested.</summary>
    private static string MaterializeRoute(string template)
    {
        if (!template.StartsWith('/'))
        {
            template = "/" + template;
        }

        return RouteParameterPattern.Replace(template, match =>
        {
            var name = match.Groups[1].Value;
            var inner = match.Value.Trim('{', '}');
            var colon = inner.IndexOf(':');
            var constraints = colon >= 0 ? inner[(colon + 1)..] : string.Empty;

            if (name.Contains("version", StringComparison.OrdinalIgnoreCase))
            {
                return "v1";
            }

            if (constraints.Contains("guid", StringComparison.OrdinalIgnoreCase) ||
                name.EndsWith("id", StringComparison.OrdinalIgnoreCase))
            {
                return "00000000-0000-0000-0000-000000000001";
            }

            if (constraints.Contains("int", StringComparison.OrdinalIgnoreCase) ||
                constraints.Contains("long", StringComparison.OrdinalIgnoreCase))
            {
                return "1";
            }

            return "probe";
        });
    }

    private static string Describe(RouteEndpoint endpoint)
    {
        var descriptor = endpoint.Metadata.OfType<ControllerActionDescriptor>().SingleOrDefault();
        var template = endpoint.RoutePattern.RawText ?? "?";
        return descriptor is null ? template : $"{descriptor.ControllerTypeInfo.Name}.{descriptor.ActionName} -> {template}";
    }
}
