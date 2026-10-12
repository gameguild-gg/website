# OpenAPI options

The API binds the `OpenApi` section to `OpenApiOptions` and validates it during
presentation-layer setup. The current default still advertises the existing
`Bearer` definition and `/documentation` Swagger UI route, so consumers of the
generated document keep their existing configuration.

```json
{
  "OpenApi": {
    "Title": "GameGuild API",
    "Description": "Developer API",
    "MetadataVersion": "2026.09",
    "Servers": [
      {
        "Url": "https://api.example.com/{version}",
        "Description": "Production",
        "Variables": {
          "version": { "Default": "v1", "Enum": ["v1", "v2"] }
        }
      }
    ],
    "SecuritySchemes": {
      "ApiKey": {
        "Kind": "ApiKeyHeader",
        "HeaderName": "X-API-Key",
        "AuthenticationScheme": "ApiKey",
        "Description": "API key issued by GameGuild"
      },
      "ExternalOAuth": {
        "Kind": "OAuth2AuthorizationCode",
        "AuthorizationUrl": "https://identity.example.com/authorize",
        "TokenUrl": "https://identity.example.com/token",
        "Scopes": { "api.read": "Read API data" },
        "AuthenticationScheme": "ExternalOAuth"
      }
    },
    "Ui": {
      "RoutePrefix": "developer/docs",
      "DocumentTitle": "GameGuild developer API",
      "EnableFilter": true,
      "EnableDeepLinking": true,
      "DisplayRequestDuration": true,
      "PersistAuthorization": false
    },
    "Extensions": {
      "x-api-audience": "{\"roles\":[\"developer\",\"operator\"],\"public\":true}"
    },
    "Schemas": {
      "Identity_Users_UserDto": {
        "Description": "The public account profile returned by this API.",
        "ExampleJson": "{\"id\":\"00000000-0000-0000-0000-000000000001\",\"email\":\"ada@example.com\",\"name\":\"Ada\",\"createdAt\":\"2026-09-30T12:00:00Z\"}",
        "Properties": {
          "id": {
            "Description": "Stable account identifier.",
            "ExampleJson": "\"00000000-0000-0000-0000-000000000001\""
          }
        }
      }
    }
  }
}
```

`SecuritySchemes` may also contain `HttpBearer` or `HttpBasic` entries. Their
names are OpenAPI definition names; `AuthenticationScheme` must match a scheme
registered by ASP.NET Core when an action explicitly selects it with
`[Authorize(AuthenticationSchemes = "...")]`. The OpenAPI operation filter maps
those explicit selections to the matching definitions. It leaves the default
Bearer requirement on other actions, and the existing anonymous filter removes
requirements from `[AllowAnonymous]` actions. Defining an OAuth2 scheme here
documents an existing provider; it does not register an authentication handler.

`EnableDefaultBearer` can be set to `false` when migrating a document to other
schemes. Keep it enabled until generated clients and documented authorization
requirements have been reviewed. OAuth2 URLs, API key header names, scheme
names, and UI route prefixes are validated on startup.

Swagger JSON and UI are currently served in Development and Staging. The
`/openapi/{documentName}.json` compatibility route redirects to the corresponding
Swashbuckle document. Versioned documents retain their existing URL structure.

`Extensions` maps OpenAPI `x-` names to JSON-encoded values. An extension value
can therefore be a string, number, boolean, array, object, or `null`. The
options validator rejects invalid names, malformed JSON, and values over 64 KiB
on startup. `Schemas` uses the generated schema ID, not a C# type name; the
schema IDs visible in `/swagger/v1/swagger.json` are the source of truth. A
configured description or example replaces that schema's generated value, while
unconfigured fields keep the existing output. `Properties` uses serialized
property names. Examples must match the model's wire representation; JSON
syntax is validated at startup, and the API owner must verify semantic validity.
The default document is unchanged when these dictionaries are empty.

Localized documents are opt-in under `OpenApi:Locales`. Each configured locale
adds a separate document named `{apiVersion}.{culture}`, such as
`/swagger/v1.pt-BR/swagger.json`, and an entry in Swagger UI. Locale keys must
be valid non-invariant .NET culture names and cannot normalize to the same
culture. Missing translations fall back to the existing generated text. The
base document URL and contents stay unchanged. Supported translation keys are
the document `Title` and `Description`, tag names under `Tags`, operation IDs
or `HTTP-METHOD /path` keys under `Operations`, and generated schema IDs under
`Schemas`; schema `Properties` keys are serialized wire-format property names.

```json
{
  "OpenApi": {
    "Locales": {
      "pt-BR": {
        "Title": "API GameGuild",
        "Description": "Documentação para desenvolvedores.",
        "Tags": {
          "ai": "Inteligência artificial"
        },
        "Operations": {
          "GET /v1/ai/status": {
            "Summary": "Verificar o status da IA",
            "Description": "Mostra os provedores configurados."
          }
        },
        "Schemas": {
          "Identity_Users_UserDto": {
            "Description": "Perfil público da conta.",
            "Properties": {
              "id": "Identificador da conta."
            }
          }
        }
      }
    }
  }
}
```

Swashbuckle also reads the `GameGuild.*.xml` compiler documentation files next
to the API assemblies. Existing type and member summaries become schema,
property, and operation descriptions. A configured `Schemas` value still takes
precedence over the corresponding XML summary. The TypeScript client must be
regenerated whenever these descriptions change.

In the 2026-09-30 `v1` export, schema descriptions increased from 6 to 641 of
1,579, property descriptions from 0 to 1,986 of 9,186, and operation summaries
from 359 to 1,017 of 1,544. Paths, schemas, and every non-documentation field
were unchanged. This makes existing comments visible without inventing text for
undocumented models. The XML comments do not supply examples in this export.

Structured extension data, opt-in examples, compiler XML comments, and
configurable locale-specific documents advance issue #147. Descriptions and
examples for the remaining models and final acceptance remain before it can
close.

## Version document reconciliation

Global document selection follows the explorer's resolved full API version and
explicit action mappings. Custom groups retain unambiguous aliases or receive
version-qualified aliases; Swagger UI uses the same catalog as document
registration. See [versioned-openapi-reconciliation.md](versioned-openapi-reconciliation.md)
for naming, migration and current original-criterion verification. The earlier
coverage and pending-work paragraphs above are dated implementation history;
they do not supersede the current reconciliation.

## Document generation measurement

The `OpenApiDocumentGenerationBenchmarks` fixture generates the full `v1`
document with default options and with one structured extension and one schema
override. The 2026-09-30 local BenchmarkDotNet `ShortRun` used one launch, three
warmups, and three measured iterations on Windows 10, .NET 10.0.10, and an Intel
Xeon W-2235. The document has 1,261 paths and 1,579 schemas. Before XML comment
loading, the results were:

| Configuration | Mean | Error | Allocated |
| --- | ---: | ---: | ---: |
| Default | 708.3 ms | 376.1 ms | 223.97 MB |
| Configured | 690.9 ms | 1,444.4 ms | 223.97 MB |

The error intervals overlap substantially, so this short local run does not
establish a speed difference. It measures document generation only, without
HTTP startup, serialization, or downstream client generation. Reproduce it from
the repository root with:

```powershell
dotnet run --project apps/api/tests/GameGuild.API.Versioning.PerformanceTests/GameGuild.API.Versioning.PerformanceTests.csproj --configuration Release -- --filter '*OpenApiDocumentGenerationBenchmarks*' --job Short --buildTimeout 600
```

Loading all 69 XML files as separate Swashbuckle filter sets was rejected after
the same local job measured 2.624 s and 2.58 GB allocated for the default
document. The implementation combines the files into one immutable XML index
and registers one filter set. With that implementation, a repeat `ShortRun`
measured:

| Configuration with XML comments | Mean | Error | Allocated |
| --- | ---: | ---: | ---: |
| Default | 754.1 ms | 1,112.2 ms | 326.08 MB |
| Configured | 1,067.2 ms | 2,034.3 ms | 325.65 MB |

These small local samples have wide error intervals and are not production
latency targets. They show that the one-index implementation avoids the large
allocation observed with 69 filter sets; they do not establish a precise
performance difference from the pre-XML implementation.
