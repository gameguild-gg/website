# Authentication options configuration

The API host binds `PresentationLayer:Authentication` to the typed
`AuthenticationOptions` contract. The `SetupAuthentication` overload that is called without an options object
binds the root `Authentication` section; the host uses the nested presentation-layer section.

```json
{
  "PresentationLayer": {
    "Authentication": {
      "EnableAuthentication": true,
      "EnableAuthorization": true,
      "JwtIssuer": "GameGuild",
      "JwtAudience": "GameGuild.Users",
      "JwtExpiration": "01:00:00",
      "RefreshTokenExpirationDays": 30,
      "EnableApiKeyAuthentication": false,
      "ApiKeyHeaderName": "X-API-Key",
      "AllowApiKeyInQueryString": false,
      "ApiKeyQueryStringParameterName": "api_key",
      "EnableBasicAuthentication": false,
      "EnableCookieAuthentication": false,
      "PasswordPolicy": {
        "MinPasswordLength": 12,
        "MaxPasswordLength": 128,
        "RequireUppercase": true,
        "RequireLowercase": true,
        "RequireDigit": true,
        "RequireSpecialChar": true
      },
      "ExternalProviders": {
        "Providers": {
          "google": {
            "Enabled": false,
            "ClientId": "",
            "ClientSecret": "",
            "Scopes": []
          },
          "microsoft": {
            "Enabled": false,
            "ClientId": "",
            "ClientSecret": "",
            "Tenant": "common",
            "Scopes": []
          },
          "github": {
            "Enabled": false,
            "ClientId": "",
            "ClientSecret": "",
            "Scopes": []
          },
          "discord": {
            "Enabled": false,
            "ClientId": "",
            "ClientSecret": "",
            "Scopes": []
          }
        }
      }
    },
    "SecurityHeaders": {
      "EnableXContentTypeOptions": true,
      "EnableXFrameOptions": true,
      "XFrameOptionsValue": "DENY",
      "EnableContentSecurityPolicy": true
    }
  },
  "AuthenticationSecurity": {
    "MaxFailedAttemptsPerHour": 5,
    "MaxFailedAttemptsPerDay": 20,
    "MaxAttemptsPerIpPerHour": 50,
    "AccountLockoutDurationMinutes": 30,
    "EnableIpThrottling": true,
    "EnableUserEnumerationProtection": true
  },
  "Mfa": {
    "Enabled": true,
    "RequireMfaByDefault": false,
    "MaxFailedAttempts": 5,
    "LockoutDurationMinutes": 15
  },
  "Session": {
    "IdleTimeoutMinutes": 30,
    "AbsoluteTimeoutMinutes": 1440,
    "TerminateSessionsOnPasswordChange": true
  }
}
```

Supply secrets through the deployment secret store or the .NET environment configuration provider, for example:

```text
PresentationLayer__Authentication__JwtSecretKey=<deployment-secret>
PresentationLayer__Authentication__ExternalProviders__Providers__google__ClientSecret=<google-client-secret>
```

Options are validated at startup. Authentication stays JWT bearer by default; API keys, Basic, and cookie schemes
are opt-in additions. Basic and API-key query credentials require HTTPS. API-key query-string support is disabled
by default because URLs are commonly logged. Cookie authentication emits an HTTP-only, secure cookie with a
validated SameSite mode.

To enable the optional schemes, add only the settings the deployment needs:

```json
{
  "PresentationLayer": {
    "Authentication": {
      "EnableApiKeyAuthentication": true,
      "EnableBasicAuthentication": true,
      "Basic": {
        "SchemeName": "LegacyBasic",
        "Realm": "GameGuild API"
      },
      "EnableCookieAuthentication": true,
      "Cookie": {
        "SchemeName": "GameGuildCookie",
        "Name": "__Host-GameGuild.Auth",
        "Expiration": "08:00:00",
        "SlidingExpiration": false,
        "SameSite": "Strict"
      }
    }
  }
}
```

Production HSTS is enabled by the API pipeline; HSTS is not a mutable header value in
`SecurityHeadersOptions`. CSP and X-Frame-Options are configured through
`PresentationLayer:SecurityHeaders` and validated before the middleware is added.

### Security response headers: shipped defaults and reverse-proxy implications

`SecurityHeadersMiddleware` runs globally before routing, so every response class
(success, 401, ProblemDetails errors, documentation pages) carries the same baseline.
The shipped defaults are:

| Header | Default value |
| --- | --- |
| `Content-Security-Policy` (API responses) | `default-src 'none'; frame-ancestors 'none'` |
| `Content-Security-Policy` (`/documentation`, `/swagger`) | `default-src 'self'; script-src 'self' 'unsafe-inline' 'unsafe-eval'; style-src 'self' 'unsafe-inline'; img-src 'self' data: https:; font-src 'self' data:; connect-src 'self'; frame-ancestors 'none'` |
| `X-Frame-Options` | `DENY` |
| `X-Content-Type-Options` | `nosniff` |
| `Referrer-Policy` | `strict-origin-when-cross-origin` |
| `Permissions-Policy` | `accelerometer=(), camera=(), geolocation=(), gyroscope=(), magnetometer=(), microphone=(), payment=(), usb=()` |
| `X-XSS-Protection` | `0` |
| `Cache-Control` / `Pragma` on `/auth`, `/login`, `/token` and `/password` paths | `no-store, no-cache, must-revalidate` / `no-cache` |
| `Strict-Transport-Security` | emitted only in the Production environment, on HTTPS responses only (ASP.NET Core `UseHsts` defaults; no custom `HstsOptions` are configured) |

These defaults are intentionally restrictive for a JSON API; deployments that need
different CSP values override the strings through `PresentationLayer:SecurityHeaders`
(the values are validated at start-up — malformed header values, including line breaks,
are rejected before the middleware is added). The Swagger/documentation CSP exception
exists because Swagger UI cannot render under the restrictive API policy; the
documentation surface is only mapped in the Development and Staging environments.

Response-level verification lives in
`apps/api/tests/GameGuild.API.IntegrationTests/SecurityHeadersHttpTests.cs`, which
asserts the defaults on success, 401, ProblemDetails, sensitive-path, documentation and
production HTTPS responses.

Reverse-proxy implications:

- HSTS is only emitted on requests the application observes as HTTPS. Behind a
  TLS-terminating proxy the forwarded scheme must reach the API: keep
  `UseForwardedHeaders` early in the pipeline (it already is) and configure the proxy to
  send `X-Forwarded-Proto`, with `ForwardedHeadersOptions.KnownProxies`/`KnownNetworks`
  restricted so the headers cannot be spoofed from untrusted clients.
- The proxy must not strip the security headers. If the proxy adds its own HSTS (for
  example to cover the plain-HTTP edge), keep the emitted `max-age` values consistent to
  avoid downgrade windows; HSTS cannot be removed from a browser once set, so raise
  `max-age` deliberately.
- Redirects at the proxy edge (HTTP to HTTPS) should preserve the `Host` header via
  `X-Forwarded-Host` when host-based routing is used.

API-key credentials can also be read from a deployment-specific request location in host code:

```csharp
var authentication = new AuthenticationOptions
{
    EnableApiKeyAuthentication = true,
    ApiKeyCustomKeyResolver = request => request.Headers["X-Deployment-Key"].FirstOrDefault()
};

services.SetupAuthentication(configuration, authentication);
```

The resolver is programmatic and cannot be represented safely in JSON configuration. It participates in the same
single-source check as the configured header and optional query parameter; supplying credentials through more than
one enabled source is rejected. Hosts can register other ASP.NET Core authentication schemes with the
`configureAdditionalSchemes` callback on `SetupAuthentication`.

Authentication-related concerns remain in their owning typed sections: account lockout and IP throttling use
`AuthenticationSecurity`, second-factor policy uses `Mfa`, session limits use `Session`, and response headers use
`PresentationLayer:SecurityHeaders`. The current platform uses the `GameGuild.Identity.Authentication` services and
repositories for account, password, OAuth, API-key, Basic, cookie, and MFA flows; the historical request to add a
second ASP.NET Core Identity persistence stack is superseded by that platform architecture.
