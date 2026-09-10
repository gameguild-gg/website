# Task 9 recovery report

## Changed files

- `apps/web/src/components/feed/social-media-picker.tsx` and `social-media-preview.tsx`: validated single-file media contract and preview URL cleanup.
- `apps/web/src/lib/feed/social-media-upload.ts`: same-origin XHR upload transport with byte progress and cancellation.
- `apps/web/src/app/api/social/media/route.ts`: authenticated server-side upload proxy; no caller actor or credential is forwarded from the browser.
- `apps/web/src/components/feed/social-composer.tsx`: upload-before-create flow, retry/cancel state, character feedback, normalized tags, and callback of the authoritative created item.
- `apps/web/src/components/feed/social-feed-client.tsx`, `social-shell.tsx`, and `infinite-post-feed.tsx`: stable composer/feed state boundary and one-time top insertion by item ID.
- Focused component, transport, route, feed, and action tests.

## RED/GREEN evidence

- RED: `corepack pnpm --filter @game-guild/web exec vitest run src/components/feed/social-composer.test.tsx --reporter=verbose` — authoritative publication callback failed with zero calls.
- RED: `corepack pnpm --filter @game-guild/web exec vitest run src/lib/feed/social-media-upload.test.ts --reporter=verbose` — missing upload transport import.
- GREEN: picker validation/preview cleanup, XHR byte-progress/cancellation, composer, infinite-feed insertion, route authentication, and action suites passed in focused executions.
- GREEN: `corepack pnpm --filter @game-guild/web exec tsc --noEmit --pretty false` completed with no output.
- `git diff --check` passed.

## Commit

`02f76a6d4d5b50e64bdafc93fb101ae6432bf5f2` (`fix(feed): recover uploaded media composer`).

## Residual risks

- The backend remains the authoritative enforcement point for MIME/size/content scanning; the browser picker mirrors its first-party policy for early feedback.
- Media processing polling is bounded and reports timeout while retaining the selected media so retry is possible.

## Preserved unstaged Task 11 work

- `apps/web/src/lib/feed/queries.ts`: `loadSocialProfileByHandle` follow-state hydration hunk.
- `apps/web/src/lib/feed/queries.test.ts`: matching profile follow-state test hunk.
