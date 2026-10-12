# API versioning configuration

GameGuild configures ASP.NET API versioning through the `ApiVersioning` options section. The default parser remains the native ASP.NET format so existing `1.0` routes and controller attributes keep their current behavior.

```json
{
  "ApiVersioning": {
    "DefaultVersion": "1.0",
    "VersionFormat": "Native",
    "AssumeDefaultVersionWhenUnspecified": true,
    "ReadingStrategy": "UrlSegment",
    "ReportApiVersions": true,
    "EnableUsageTelemetry": true,
    "EnableUsageLogging": true,
    "CompatibilityMatrix": {
      "1.0": ["1.1"]
    },
    "SunsetPolicies": {
      "1.0": {
        "EffectiveAt": "2027-01-01T00:00:00Z",
        "PolicyUrl": "https://gameguild.gg/developer/api-versioning"
      }
    }
  }
}
```

`ReadingStrategy` supports URL segments, query strings, headers, media types, selected combinations, and all configured readers. `QueryParameterName`, `HeaderName`, and `MediaTypeParameterName` control the corresponding reader names. Invalid strategies, versions, and sunset policy URLs fail configuration validation instead of silently falling back.

## Version formats

`VersionFormat` accepts `Native` or `SemanticVersion`:

- `Native` uses the ASP.NET parser for major/minor versions, prerelease status, and date-based group versions such as `2026-09-29`.
- `SemanticVersion` additionally accepts numeric `major.minor.patch` values, prerelease identifiers such as `2.3.4-rc.1`, and build metadata such as `2.3.4+build.7`. Build metadata is preserved when formatting but does not affect precedence. Numeric components must not have leading zeroes, numeric prerelease identifiers must not have leading zeroes, and components larger than `Int32` are rejected.
- Date-based versions continue to use the native parser in either mode. Existing major/minor declarations remain compatible with semantic patch-zero requests.

Declare semantic versions with the attributes from `GameGuild.Configuration.PresentationLayer.ApiVersioning` so route metadata uses the same parser:

```csharp
[SemanticApiVersion("2.3.4")]
[Route("api/v{version:apiVersion}/widgets")]
public sealed class WidgetsController : ControllerBase { }

[SemanticApiVersion("2.4.0-beta.1")]
[Route("api/v{version:apiVersion}/preview-widgets")]
public sealed class PreviewWidgetsController : ControllerBase
{
    [HttpGet]
    public IActionResult GetPreview() => Ok();
}
```

## Deprecation and discovery

Set `ReportApiVersions` to advertise supported and deprecated versions in responses. Each `SunsetPolicies` entry is keyed by a version string understood by the selected parser; `EffectiveAt` configures the sunset date, and `PolicyUrl` adds the policy link. The versioned API Explorer continues to produce OpenAPI groups using `GroupNameFormat` and can substitute versions into URL templates.

## OpenAPI names and controller groups

Global document names use the complete discovered version and `GroupNameFormat`.
Numeric versions use `'v'VVV` by default. Include the date with `'v'GGGGVVV` for
concurrent date-based versions. A format producing the same name for distinct
versions is rejected. Single-version group aliases retain unambiguous names;
shared groups receive qualified aliases such as `Administration.v1.1`.
JSON registration and Swagger UI use the same catalog. See
[versioned-openapi-reconciliation.md](versioned-openapi-reconciliation.md)
for operation selection, aliases and original #144/#147 requirement mapping.

## Compatibility matrix

`CompatibilityMatrix` is directional: each key is a selected API version and each value is a version with an explicitly compatible request/response contract. For a matched request, the middleware returns those values in `X-API-Compatible-Versions`. The matrix does not rewrite routes or silently serve a different version. Add an entry only after verifying the wire-contract compatibility; omit versions whose compatibility has not been established.
## Usage telemetry

The request pipeline records `gameguild.api.versioning.requests` and `gameguild.api.versioning.request.duration` from the `GameGuild.API.Versioning` meter. Measurements include the selected API version, route template, HTTP method, and response status. Unmatched requests use the fixed `unmatched` version label, so arbitrary client input is never copied into metric dimensions. Set `EnableUsageTelemetry` to `false` to disable these measurements.

When `EnableUsageLogging` is enabled, the pipeline writes a structured record with the selected version, route template, method, status, and elapsed time. These fields support adoption reports and deprecation planning without logging raw query strings or version text that did not resolve to a matched endpoint.

## Migration guide

1. Keep the current default at `1.0` while existing clients migrate. `AssumeDefaultVersionWhenUnspecified` applies only when the selected reader has no version value; it does not make a required URL version segment optional. During migration from unversioned paths, retain the existing route or use a non-URL reader until clients move to versioned paths.
2. Declare the current contract explicitly with `[ApiVersion("1.0")]`. To preserve clients using `/api/widgets` while adding `/api/v1.0/widgets`, keep both route templates on the controller during migration, or use a non-URL reader. Keep the same request and response schema for that version.
3. Publish the version through API Explorer and generated OpenAPI documents. Verify the route in the generated document before changing client URLs or regenerating clients.
4. Choose one version source for new clients: URL segment, query parameter, header, or media type. Combined readers are supported; avoid sending conflicting values in more than one source.
5. If a new contract changes request or response compatibility, add a new API version and retain the old endpoint during the migration window. Do not silently change the contract served by `1.0`.
6. Enable `ReportApiVersions` and configure a `SunsetPolicies` entry before announcing a retirement date. Include a public policy URL and UTC `EffectiveAt` value.
7. Monitor request counts by selected version and route. Contact or migrate consumers still using the deprecated version before its sunset date; remove the old mapping only after usage has stopped and the compatibility policy allows retirement.
8. For semantic versions, set `VersionFormat` to `SemanticVersion`, update the default version, and use `SemanticApiVersion` or `MapToSemanticApiVersion` on every controller or action declaring a patch or prerelease version.

Existing clients do not need to change format until the corresponding versioned route or reader is enabled for them. Deploy reader changes alongside generated OpenAPI/client updates and verify each configured reader with an integration request.

## Acceptance and baseline for issue #144

Semantic document names honor quoted literals and native component tokens.
For mixed semantic/date versions, use `GroupNameFormat: "'Version-'GVVV"` to
produce names such as `Version-1.2.3` and `Version-2026-10-04`. `VVV`/`VVVV` and
`F`/`FF` retain patch, prerelease and build metadata; `V`/`VV` and padded tokens
project components, while `S` returns the actual prerelease. A format that
collapses discovered versions into the same document name fails at setup.
See the [complete reconciliation](versioned-openapi-reconciliation.md) for
scoped aliases, independent HTTP/UI checks and the current acceptance status.

The implementation includes native and semantic parsing, prerelease/date support, version reporting, sunset policies, OpenAPI grouping, structured version-usage telemetry, a validated compatibility matrix surfaced through `X-API-Compatible-Versions`, and routed tests for URL, query, header, media-type, and combined readers. Repeatable parser and reader-plus-parser benchmarks are provided by `GameGuild.API.Versioning.PerformanceTests`.

Run the short benchmark with:

```powershell
dotnet run --project apps/api/tests/GameGuild.API.Versioning.PerformanceTests/GameGuild.API.Versioning.PerformanceTests.csproj --configuration Release -- --filter "*ApiVersioningResolutionBenchmarks*"
```

### Baseline measurement

One `ShortRun` was recorded on 2026-09-29 with BenchmarkDotNet 0.14.0, .NET SDK 10.0.302, .NET runtime 10.0.10, Windows 10, and an Intel Xeon W-2235 at 3.80 GHz. The job used one launch, three warmup iterations, and three measurement iterations.

| Operation | Mean | 99.9% CI margin | Allocated |
| --- | ---: | ---: | ---: |
| Native version parse (`2.0`) | 37.23 ns | 90.47 ns | 56 B |
| Semantic prerelease parse (`2.0.0-rc.1`) | 719.66 ns | 297.56 ns | 1,200 B |
| Query reader plus native parse | 103.88 ns | 73.13 ns | 112 B |
| Header reader plus native parse | 93.19 ns | 51.67 ns | 112 B |

The reader benchmarks measure extraction and parsing from an already-created request context; the separate TestServer integration tests verify that the configured readers route to the requested controller version and reject conflicting values. This short baseline is diagnostic, not a production latency guarantee; its three measurement iterations have wide confidence intervals. Rerun the command above on the deployment hardware before using these numbers for capacity planning.
