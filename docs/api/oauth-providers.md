# OAuth provider framework

This document specifies the extension contract for external sign-in providers
(issues #235 and #246) and the per-provider setup guides. Configuration reference:
[Authentication options configuration](authentication-options-configuration.md).

## Extension contract (`IOAuthProvider`)

Every external sign-in provider is a self-contained implementation of
`GameGuild.Identity.Authentication.IOAuthProvider`
(`apps/api/Source/Modules/GameGuild.Identity.Authentication/Abstractions/IOAuthProvider.cs`):

| Member | Responsibility |
| --- | --- |
| `ProviderKey` | Lowercase dispatch/config key (`github`, `steam`, …); also persisted on `ExternalLogin.Provider`. |
| `DisplayName` | Display name written to `OAuthUserProfile.Provider`. |
| `DefaultScopes` | Safe application defaults used when neither caller nor config supplies scopes. Empty for OpenID 2.0 (Steam). |
| `IsConfigured()` | Whether credentials are present. Unconfigured providers stay registered and fail closed at call time. |
| `BuildAuthorizationUrl(redirectUri, state, scopes?)` | Authorization-URL (or OpenID redirect) construction. |
| `ResolveScopes(requestedScopes?)` | Scope resolution: requested > configured > defaults; dedup preserving first-seen order; provider-required unions (Microsoft: openid/email/profile). This exact list is persisted as the granted scopes at callback/link time. |
| `ExchangeCodeAsync(code, redirectUri)` | Token exchange. For Steam, `code` is the URL-encoded signed `openid.*` assertion verified via `check_authentication`; the verified SteamID takes the token slot. |
| `GetUserProfileAsync(accessToken)` | Profile claims normalization into the shared `OAuthUserProfile`. |

`OAuthService` implements `IOAuthService` as a pure dispatcher over the
registered provider set (case-insensitive by `ProviderKey`). There is no
per-provider switch to extend — it was removed when the former GitHub/Google/
Discord/Microsoft branches moved into provider classes.

### Adding a provider

1. Create one class deriving from `OAuthProviderBase`
   (`apps/api/Source/Modules/GameGuild.Identity.Authentication/OAuth/`) or
   implementing `IOAuthProvider` directly. Reuse the base helpers for settings
   resolution, credential gates, scope normalization, and bearer requests.
2. Register it in
   `apps/api/Source/Modules/GameGuild.Identity.Authentication/Data/Extensions/DependencyInjection.cs`
   next to the other providers (`services.AddScoped<IOAuthProvider, …>()`).
3. Add the provider key to `ExternalProviderOptions.SupportedProviders`
   (`apps/api/Source/Modules/GameGuild.SharedKernel/Configuration/PresentationLayer/Authentication/ExternalProviderOptions.cs`)
   so hosts can configure it, with fail-closed validation of its credentials.
4. Surface a callback command + handler + validator + `UseCaseEventContract`
   entry if the provider needs its own sign-in surface (follow
   `SteamCallbackCommand` / `SteamCallbackCommandHandler`).
5. Add handler tests with mocked HTTP (follow
   `tests/GameGuild.Identity.Authentication.UnitTests/Services/OAuthProviderRegistryTests.cs`)
   and platform-side sign-in tests (follow
   `ProviderCallbackSignInServiceTests.cs`).

The registry acceptance test
(`CustomProvider_RegistersWithoutEditingTheDispatcher`) proves a provider plugs
in through the interface alone.

### Fail-closed defaults

Unconfigured or disabled providers mirror the NoOp-default geolocation provider
and the disabled threat-intelligence no-op in the same module: they stay
registered, and every operation throws `InvalidOperationException` describing the
missing configuration. `ExternalProviderOptions.Validate` additionally refuses
to start the host when an *enabled* provider lacks credentials. At the API
surface these failures are returned as 503 ("provider not configured"), never a
silent fallback.

## Built-in providers

| Key | Protocol | Credentials | Notes |
| --- | --- | --- | --- |
| `github` | OAuth 2.0 | ClientId + ClientSecret | JSON token exchange with `Accept: application/json`; primary-email fallback; optional `UserEmailEndpoint`. |
| `google` | OAuth 2.0 | ClientId + ClientSecret | `access_type=offline`; ID-token sign-in also exists (`GoogleIdTokenSignInHandler`). |
| `discord` | OAuth 2.0 | ClientId + ClientSecret | Form-encoded token exchange; snowflake-derived default avatar. |
| `microsoft` | OAuth 2.0 / OIDC v2 | ClientId + ClientSecret | `login.microsoftonline.com/{Tenant}` common/organizations/consumers/tenant-GUID endpoints; Graph userinfo; openid/email/profile always unioned. |
| `steam` | OpenID 2.0 | ApiKey (Steam Web API) | Redirect flow with `checkid_setup`; callback assertion verified via `check_authentication`; no email claim — first-time users get an internal unverified identifier (`{steamid}@steam.local`, same policy as wallet sign-in). |
| `epic` | OAuth 2.0 | ClientId + ClientSecret | `epicgames.com/id/authorize` + `api.epicgames.dev` token (HTTP Basic credentials) and `userInfo`. |
| `facebook` | OAuth 2.0 (Graph) | ClientId + ClientSecret | `dialog/oauth` + Graph token endpoint (credentials as query parameters) + `/me` field projection. |

All endpoints can be overridden per provider (`AuthorizationEndpoint`,
`TokenEndpoint`, `UserInformationEndpoint`) for national-cloud or custom
deployments; overrides must be absolute HTTPS URLs without embedded credentials.

## Provider setup guides

Configuration lives under `Authentication:ExternalProviders:Providers` (bound
through `AuthenticationOptions`); legacy `OAuth:<Provider>:ClientId|ClientSecret`
keys are still honored. Secrets belong in the deployment secret store, never in
committed appsettings.

### GitHub

1. Create an OAuth App at <https://github.com/settings/developers>; set the
   callback URL to your `/auth/github` callback.
2. Configure:

   ```json
   "Authentication": {
     "ExternalProviders": {
       "Providers": {
         "github": {
           "Enabled": true,
           "ClientId": "<client id>",
           "ClientSecret": "<secret-store reference>",
           "Scopes": ["read:user", "user:email"]
         }
       }
     }
   }
   ```

### Google

1. Create OAuth 2.0 credentials at <https://console.cloud.google.com/apis/credentials>.
2. Configure the `google` entry the same way with `openid email profile` scopes.

### Discord

1. Create an application at <https://discord.com/developers/applications> and
   add an OAuth2 redirect.
2. Configure the `discord` entry with `identify email` scopes.

### Microsoft

1. Register an application at <https://entra.microsoft.com> (App registrations),
   adding the web redirect URI.
2. Configure the `microsoft` entry; `Tenant` accepts `common`,
   `organizations`, `consumers`, or a tenant GUID. `openid`, `email`, and
   `profile` are always requested (unioned into any configured scopes).

### Steam

1. Obtain a Steam Web API key at <https://steamcommunity.com/dev/apikey>.
2. Configure the `steam` entry with `ApiKey`. There is no client ID/secret —
   sign-in is OpenID 2.0; the return-to URL is the redirect URI.
3. The callback body carries the URL-encoded `openid.*` assertion as `code`.

### Epic Games

1. Create credentials at <https://dev.epicgames.com> (Epic Online Services);
   note the client ID/secret (and set the redirect URI on the account system).
2. Configure the `epic` entry with `basic_profile email` scopes. The token
   endpoint is called with HTTP Basic client credentials.

### Facebook

1. Create an app at <https://developers.facebook.com> and add the Facebook
   Login product with your redirect URI.
2. Configure the `facebook` entry with `email public_profile` scopes (joined
   with commas in the dialog URL).

## Security notes

- The CSRF `state` parameter is generated by the authorize surface and validated
  by the callback handlers; Steam relies on the exact OpenID 2.0 `return_to`
  match in addition.
- Email verification is never inferred: providers that do not attest it
  (GitHub, Microsoft Graph, Facebook's modern Graph, Steam) mark profiles
  unverified, and the auto-link policy then refuses to merge an email collision.
- Steam's email-less internal identifier can only create a fresh account; it
  cannot take over an existing email-matched account.
