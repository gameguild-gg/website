# Session controls: prevent clicks before hydration

PR #951's hosted session acceptance failed at the first Trust operation. The
source-bound artifact for checkout `743c66360d4b0992d59b260e7a13a08f83dda905`
contains five passing session-state cases and the initial trust-isolation case.
It then times out waiting for the current session's Trusted badge.

The Next log contains preference actions but no `trustCurrentDeviceAction`, and
the API log contains the initial trusted-device GET but no trust mutation. A 200
response from an unrelated preference action satisfied the harness's broad POST
predicate; the subsequent UI and persistence assertions correctly kept acceptance
failed. No timeout increase, retry, mocked transport or deleted assertion is used.

The component renders enabled mutation buttons into server HTML before React has
attached their event handlers. This creates a concrete lost-click window and is
consistent with the hosted failure. Keep all 11 mutation controls disabled during
server rendering and hydration, then enable them using the client snapshot from
`useSyncExternalStore`. Existing pending-operation and session-count restrictions
remain in force. Layout, labels, action contracts and authorization are unchanged.

Two regression cases cover disabled server markup and real React hydration,
including the first Trust click being dispatched exactly once without hydration
errors. The existing hosted browser/API/PostgreSQL workflow must pass its seven
session-state cases, four trust cases and independent persisted-state assertions
before this repair is accepted. No local product tests or browser were run.

The checkout has no installed `next/dist/docs` directory. The relevant official
guidance was consulted directly: [Next.js server/client hydration](https://nextjs.org/docs/app/getting-started/server-and-client-components),
[React server snapshots](https://react.dev/reference/react/useSyncExternalStore),
and [Playwright's lost-click hydration guidance](https://playwright.dev/docs/navigations#hydration).

The log also contains unrelated asset-worker handler-reuse errors; those are not
the terminating assertion and are not attributed to this repair. Full issues #278
and #166 remain subject to their separate acceptance matrices.
