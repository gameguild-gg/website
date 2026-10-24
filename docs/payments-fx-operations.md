# FX synchronization configuration

The currency and FX foundation keeps external synchronization disabled by default
(`FxRates:Provider=none`, `FxRates:WorkerEnabled=false`). Existing quotations and
unexpired manual overrides remain readable. The currency administration endpoints
require a tenant administrator; platform FX administration requires a system
administrator.

Before selecting `ecb` or `openexchangerates`, configure
`FxRates:AlertRecipientIds` with the account IDs of the operators responsible for
the platform FX service. For environment configuration, use indexed entries such
as `FxRates__AlertRecipientIds__0` and `FxRates__AlertRecipientIds__1`. Empty IDs and
an enabled provider with no recipients fail startup validation. These are account
IDs, not email addresses or tenant IDs. Keep the list aligned with operator access
when administrators change.

After three consecutive failed sync runs (configurable), the service sends a
high-priority in-app notification to each configured account. Stale rates produce
a separate alert. Successful deliveries are throttled for fifteen minutes per
recipient and alert kind within each application process. Failed deliveries remain
eligible on the next alert attempt; other recipients are still attempted. The
notification service applies the account's notification preferences. This does
not configure email or an external paging service.

OpenExchangeRates requires its app ID through protected configuration and a base
currency supported by the account plan. ECB quotations always use EUR as the
base. Provider endpoints require HTTPS, except HTTP loopback test endpoints.
Fallback provider settings are validated as well. Enable the scheduled worker
only after configuring the provider and alert recipients; the authenticated sync
endpoint can be used with the worker disabled.

Manual overrides expire at their configured UTC deadline and are not copied into
the quotation cache. Other processes refresh persisted quotations and overrides
according to `FxRates:CacheTtlMinutes` (default sixty minutes). This foundation does
not by itself complete localized pricing, checkout, reporting, or provider-account
acceptance for issue #412.
