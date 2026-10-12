# Rate limiting configuration

The API uses ASP.NET Core's in-memory rate limiter. Enable it in the presentation layer and configure named policy limits under `RateLimiting`:

```json
{
  "PresentationLayer": {
    "EnableRateLimiting": true
  },
  "RateLimiting": {
    "RedisFailureMode": "FailOpen",
    "Limit": 100,
    "Period": "00:01:00",
    "QueueLimit": 2,
    "ExemptPaths": ["/health"],
    "TrustedProxyAddresses": ["10.0.0.10", "2001:db8::10"],
    "TrustedProxyForwardLimit": 1,
    "AuthenticationRequestsPerMinute": 10,
    "AuthenticationWindow": "00:01:00",
    "AuthorizationRequestsPerMinute": 100,
    "AuthorizationWindow": "00:01:00",
    "ApiRequestsPerMinute": 60,
    "ApiWindow": "00:01:00",
    "IpRequestsPerMinute": 30,
    "IpWindow": "00:01:00",
    "TenantRequestsPerMinute": 1000,
    "TenantWindow": "00:01:00",
    "UserRequestsPerMinute": 300,
    "UserWindow": "00:01:00",
    "InternalRequestsPerMinute": 200,
    "InternalWindow": "00:01:00",
    "StandardApiKeyRequestsPerMinute": 100,
    "PremiumApiKeyRequestsPerMinute": 1000,
    "ApiKeyWindow": "00:01:00",
    "SlidingWindowSegments": 4,
    "TokenBucketLimit": 100,
    "TokenReplenishmentPeriod": "00:00:10",
    "TokensPerPeriod": 20,
    "MaxConcurrentRequests": 10,
    "ConcurrencyLeaseDuration": "00:02:00",
    "EnableProgressivePenalties": false,
    "PenaltyViolationThreshold": 5,
    "PenaltyDecayWindow": "00:15:00",
    "PenaltyBaseDuration": "00:00:30",
    "PenaltyMaxDuration": "00:15:00",
    "Policies": {
      "report-export": {
        "Algorithm": "SlidingWindow",
        "PartitionBy": "Endpoint",
        "PermitLimit": 15,
        "Window": "00:01:00",
        "QueueLimit": 0,
        "SlidingWindowSegments": 4
      }
    },
    "AccessControl": {
      "AllowlistedUserIds": [],
      "DenylistedUserIds": [],
      "AllowlistedIpAddresses": [],
      "DenylistedIpAddresses": []
    }
  }
}
```

The application registers fixed-window policies for authentication, authorization and internal endpoints; sliding-window policies for API, tenant, user and IP limits; token-bucket policies for bursty and API-key traffic; and a concurrency policy for expensive operations. Additional policies under `RateLimiting:Policies` can define fixed-window, sliding-window or token-bucket limits and partition them by `Global`, `User`, `Ip`, `UserOrIp`, `Tenant` or `Endpoint`. Use the policy key with `[EnableRateLimiting("report-export")]`; endpoint partitions use the HTTP method and route template, without route parameter values. Redis mode applies these custom policies through the same shared limiter as built-in policies. Built-in policy names are reserved. API-key and user identities receive separate buckets: API-key requests use the authenticated key ID, never the submitted secret. Tenant partitions use the tenant resolver's validated context. IP partitions use `Connection.RemoteIpAddress`. `TrustedProxyAddresses` lists the exact IPv4/IPv6 proxies allowed to forward both `X-Forwarded-For` and `X-Forwarded-Proto`; with an empty list, the API ignores forwarded headers, including any permissive hosting-level default. `TrustedProxyForwardLimit` controls how many configured proxy hops are consumed. This is required when Basic authentication is used behind a TLS-terminating proxy. Keep the list limited to proxies you operate and configure every proxy hop that is expected to forward client addresses.

`ExemptPaths` exempts matching paths and their descendants from the global limiter. A named endpoint policy still applies if an endpoint explicitly opts into one. Every configured count, period, queue size, sliding-window segment count and exempt path is validated during setup.

Rejected requests return HTTP 429 with Problem Details, `Retry-After`, `RateLimit-Limit`, `RateLimit-Remaining` and `RateLimit-Reset` headers. The rate-limit rejection handler writes a warning log with the request path and selected policy. The documented defaults preserve the prior per-IP limit of 30 requests per minute.

`RateLimiting:AccessControl` accepts exact user IDs and IP addresses. Denylisted identities receive HTTP 403 before endpoint execution; this takes priority if an identity appears in both lists. Allowlisted identities bypass the global and named rate-limit policies, including Redis-backed policies. IPv4-mapped IPv6 client addresses are normalized before matching. Forwarded addresses are considered only after trusted-proxy processing.

## Current scope and migration

This configuration extends the existing named in-process policies. Existing endpoints keep their `EnableRateLimiting` attributes; deployments can tune the values in `RateLimiting` without changing controller code. The older `RequestsPerMinute` and `BurstSize` members remain for configuration compatibility; use the policy-specific settings above for active limits.

The ASP.NET rate limiter remains process-local. When both `PresentationLayer:EnableRateLimiting` and `Redis:Enabled` are true, `RedisEndpointRateLimitingMiddleware` adds shared Redis gates after the local limiter: a fixed window for the global limit and the authentication, authorization and internal policies, a sliding window for API, tenant, user and IP policies, token buckets for bursty traffic and API keys, and renewable leases for expensive-operation concurrency. It uses the same validated identity, tenant and client-IP partitions as the local limiter. In Redis mode, ASP.NET's corresponding global, fixed/sliding, token-bucket, and expensive-operation concurrency policies are pass-through, avoiding duplicate process-local quotas. Configured exempt paths skip the global Redis gate while named endpoint policies still apply; `DisableRateLimiting` endpoints skip both gates.

Redis fixed-window, sliding-window, token-bucket, concurrency, and penalty decisions use atomic Lua scripts. Testcontainers integration tests send concurrent requests through four independent Redis connections and verify global and named-policy limits across separate middleware instances. A 200-request load test configures two TestServer API hosts through `SetupRateLimiting`, `UseRateLimiter`, and the Redis middleware, then verifies that exactly 20 permits are shared. A separate Testcontainers test starts two independent Kestrel containers and sends 200 concurrent requests through both; exactly the shared request limit is admitted. Bursty and API-key token buckets share capacity and refill state across instances. API-key buckets use the authenticated key ID and are isolated from user buckets; the current `ApiKey` record has no persisted tier, so the built-in API-key handler applies the standard limit. A premium limit is used only when a trusted authentication integration supplies the `api_key_tier=premium` claim. Expensive-operation permits are keyed by user, renewed while the request is active, released in a `finally` path, and reclaimed after `ConcurrencyLeaseDuration` if a process stops unexpectedly. Progressive penalties are opt-in (`EnableProgressivePenalties`, default `false`): after the configured number of rejections, each additional violation doubles the temporary block duration up to `PenaltyMaxDuration`; the violation count decays after `PenaltyDecayWindow`. Redis stores the violation window and active block atomically. Use Redis for cross-instance enforcement; the generic distributed-cache fallback does not provide atomic multi-node updates. Rejections emit the `gameguild.api.rate_limit.rejections` counter from meter `GameGuild.API.RateLimiting`, tagged only with policy and enforcement source to keep cardinality bounded. When `OpenTelemetry:Enabled` is true, the API registers this meter with the metrics provider; set `OpenTelemetry:OtlpEndpoint` to export it through OTLP, or enable `ConsoleExporterEnabled` for local inspection. The existing `/metrics` endpoint also exposes the Prometheus counter `gameguild_api_rate_limit_rejections_total` with policy and enforcement labels. `deploy/monitoring/prometheus/gameguild-rate-limiting.rules.yml` provides recording, warning and critical rules for sustained and severe rejection rates. Add the file to Prometheus `rule_files`, scrape the API `/metrics` endpoint, and tune the example thresholds against service traffic before paging. Redis failures use `RateLimiting:RedisFailureMode`. `FailOpen` is the default and preserves availability during Redis outages. `FailClosed` rejects requests whose admission cannot be checked with HTTP 503 Problem Details (`errorCode=rate_limit_store_unavailable`); request cancellation still propagates. Choose the deployment value based on the service's availability and abuse-risk requirements. BenchmarkDotNet's 2026-09-28 MediumRun measured 7.790 µs/request and 7.51 KB without the limiter, and 9.441 µs/request and 8.83 KB with the in-memory global limiter. The local TestServer run is diagnostic, not a production SLO; the report records its confidence intervals and limitations. Validation after syncing the current develop branch on 2026-09-29 passed 28 focused rate-limit unit tests, 14 Redis failure-mode tests, and 16 Redis/Testcontainers integration tests, including multi-host concurrent requests. The Acceptance Criteria and Definition of Done require in-memory and Redis storage; a database-backed store appears only in the Proposed Solution's Core Components.
