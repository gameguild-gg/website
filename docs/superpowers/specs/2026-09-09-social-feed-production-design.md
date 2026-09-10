# Production Social Feed Design

**Status:** Approved on 2026-09-09

## Context

The current social page renders a useful shell, but it is not a complete social product. The `Following` tab uses the same post stream as `For You`; post reactions, comments, reposts, saves, creator follows, stories, and trending tags are either disconnected or presentation-only; publishing accepts a remote URL instead of uploading media; author data is fetched with an N+1 request pattern; and the sidebar exposes fake `Messages` and `Saved` destinations.

The API already contains reusable Posts, Follows, Social Profiles, Reactions, Assets, Testing Lab, and Social Feed modules. The implementation will preserve those ownership boundaries, harden unsafe mutation contracts, and add only the missing persistence for saved posts and stories.

## Goals

- Deliver real `For You`, `Following`, `Community`, and `Saved` streams.
- Support text and first-party image/video publishing through the existing Assets storage pipeline.
- Support persistent reactions, comments and replies, reposts, saves, creator follows, post edit/delete, and share links.
- Support expiring stories with creation, viewing, view state, and author deletion.
- Replace hard-coded creator metrics and trending tags with persisted data.
- Render Testing Lab sessions as a distinct item type in the Community stream.
- Remove fake navigation and demo fallbacks from the production path.
- Make every mutation secure, idempotent where appropriate, observable, accessible, and resilient to optimistic-update failure.
- Verify the feature using non-admin users in unit, integration, browser, Docker, and smoke tests.

## Non-goals

- Direct messages or real-time chat. The fake Messages link will be removed.
- A machine-learning ranking system. Ranking will be deterministic and explainable.
- Live video, audio rooms, advertising, or creator monetization.
- Multiple media attachments in one post. A post or story may contain one image or one video in this release.
- Anonymous interaction. Reading public profile/post content may remain public where the existing product permits it, but all social mutations require authentication.

## Chosen Approach

Use an incremental, contract-first implementation across the existing social modules.

- Posts remains the source of post content, comments, repost relationships, and aggregate counters.
- Follows remains the source of creator relationships, blocks, and mutes.
- Reactions remains the source of the authenticated viewer's reaction.
- Social Profiles remains the source of handles, avatars, bios, and public creator metadata.
- Assets remains the source of uploaded media and its security lifecycle.
- Testing Lab remains the source of public community sessions.
- Social Feed becomes the read-model boundary that composes those modules into page-sized cursor results.
- Saved posts and stories are added to Social Feed because both are viewer-specific feed capabilities and do not justify new top-level modules.

This avoids a UI-only facade that would preserve incorrect data semantics, and avoids replacing the existing platform with a new event-driven feed service before scale requires it.

## Feed Contract

Add an authenticated endpoint:

`GET /api/social/feed?scope={for-you|following|community|saved}&cursor={opaque}&take={1..30}&tag={optional}`

The response is:

```json
{
  "items": [],
  "nextCursor": "opaque-or-null"
}
```

Each item is a discriminated union with a stable `kind`:

- `post`: original post data, author summary, tags, aggregate counts, and viewer state.
- `repost`: the repost author/context plus the embedded original post.
- `testing-session`: title, dates, format, capacity, registration status, and canonical route.

Post viewer state includes `reaction`, `isSaved`, `isFollowingAuthor`, `canEdit`, and `canDelete`. Returning viewer state in the initial query prevents one request per button and lets server-rendered HTML match the hydrated client.

Pagination uses an opaque keyset cursor built from the ordering timestamp, score, kind, and stable identifier. Cursors are signed or otherwise validated by the API and malformed cursors return a validation response. Skip-based pagination is not used for the new feed because inserting a new post must not duplicate or omit items while the user scrolls.

### Stream semantics

- `For You`: public/follower-visible content available to the viewer, excluding blocked and muted relationships. Order by pinned status, followed-author affinity, bounded engagement score, then recency.
- `Following`: content authored or reposted by users the viewer follows, newest first. It must return an empty state rather than silently falling back to For You.
- `Community`: public Testing Lab sessions and public community posts in chronological order. Session items keep their Testing Lab source of truth.
- `Saved`: posts explicitly saved by the current viewer, ordered by save time. Deleted or no-longer-visible posts are excluded.
- `tag`: when present, limits post/repost items to the normalized tag while retaining the selected scope's authorization rules.

## Write Contracts

All mutation endpoints derive the acting user from `IActorContextAccessor`. Client-provided `UserId` values are removed from public mutation bodies.

Required capabilities:

- Create, edit, and soft-delete a post owned by the actor.
- Upload an image or video through Assets, then attach only an authorized, completed asset to the post or story.
- Set or remove one reaction per actor and target.
- List, add, edit, and delete comments; edits/deletes require comment ownership or an existing moderator permission.
- Create one repost that references an existing visible post; duplicate repost requests are idempotent for the actor and target.
- Save and unsave a post idempotently.
- Follow and unfollow a creator idempotently; following self is rejected.
- Create and delete a story; record story views idempotently.
- Record external share intent without pretending it created a repost.

Posts, reactions, comments, saves, follows, and reposts update the affected feed result immediately. The Web app uses optimistic state for reversible actions and rolls back with a visible toast on failure.

## Persistence

Add `SavedPost` with:

- `UserId`
- `PostId`
- `CreatedAt`
- unique index on `(UserId, PostId)`
- indexes supporting `(UserId, CreatedAt desc)` and post cleanup

Add `Story` with:

- `AuthorId`
- `AssetId` and resolved media URL/type
- optional caption
- `CreatedAt`, `ExpiresAt`, `DeletedAt`
- index on `(AuthorId, ExpiresAt)` and expiration query

Add `StoryView` with:

- `UserId`
- `StoryId`
- `ViewedAt`
- unique index on `(UserId, StoryId)`

The migration is additive. Existing posts and profiles remain valid. Deployment does not require destructive schema changes.

## Security and Privacy

- Require authentication for reactions, saves, follows, comments, reposts, story views, and uploads.
- Ignore or remove mutation request user identifiers; always use the actor context.
- Enforce ownership for profile, post, comment, and story mutations.
- Apply block/mute rules to stream composition and creator suggestions.
- Validate target existence and visibility before reactions, saves, comments, reposts, or story views.
- Validate upload MIME type, extension, content length, scan/moderation result, and asset ownership before attachment.
- Permit JPEG, PNG, WebP, GIF, and MP4; cap images at 10 MiB and video at 100 MiB.
- Normalize tags case-insensitively and cap tags per post at 10.
- Apply existing API rate-limiting infrastructure to creation and engagement mutations.
- Do not expose private profiles, follower-only posts, blocked users, or expired/deleted stories through aggregate queries.

## Web Experience

The existing shell, theme tokens, header, sidebar, and card visual language remain intact. New controls use the repository's shadcn/ui components and semantic Tailwind tokens.

### Composer

- Compact closed state and focused expanded state.
- Text, image, and video selection with preview, progress, cancel, retry, and remove.
- Character count near the limit, validation before upload, disabled duplicate submit, and a recoverable draft while the composer stays mounted.
- Successful publication inserts the returned post at the top without a full-page refresh.

### Post card

- Reaction toggle with count and selected state.
- Comment drawer/dialog on mobile and inline expansion on larger screens; paginated comments and replies.
- Repost action with optional comment and visible original-post attribution.
- Native share sheet when available, copy-link fallback otherwise.
- Save toggle and a functional Saved destination.
- Owner menu for edit/delete; destructive confirmation uses AlertDialog.
- Follow/unfollow from author context without showing the action on the current user's own post.

### Stories

- The current user's add control opens the media flow.
- Story rings indicate unseen/seen state.
- The viewer supports previous/next, close, progress, keyboard controls, pause on pointer hold, author/date context, and accessible labels.
- Expired, deleted, blocked, or private stories are never displayed.

### Rail and profiles

- Suggested creators exclude the current actor, blocked/muted users, and already-followed users.
- Counts come from posts and follows, not writable client-supplied profile stats.
- Trending tags come from the existing post-tag aggregate endpoint, link into the active feed, and have a truthful empty state.
- `View profile` opens a real public creator profile showing bio, projects, posts, followers/following counts, and follow state.

### Navigation

- Remove Messages until a messaging product exists.
- Saved links to the real Saved stream.
- Community tab renders the community stream rather than public posts mislabeled as events.
- Deep links preserve locale behavior through the existing routing helpers.

## Error and Loading Behavior

- SSR returns the first page and authenticated viewer state.
- Subsequent pages use the client boundary with an abortable request and retry affordance.
- Empty, unauthenticated, forbidden, missing, rate-limited, upload-processing, and generic failure states have distinct copy/actions.
- Optimistic actions are serialized per target, ignore double clicks while pending, and reconcile with the server response.
- A single failed rail query does not blank the main feed.

## Performance and Observability

- Eliminate per-post user lookups by composing author/profile and viewer state in the API read model.
- Query no more than 30 feed items per page and project only fields required by the Web contract.
- Use `AsNoTracking` for read models and indexed keyset predicates.
- Lazy-load offscreen media and provide explicit image/video dimensions.
- Add structured logs and metrics for feed latency, result count, mutation failures, upload failures, and cursor validation failures without logging post bodies or tokens.
- Target p95 API response under 500 ms against the staging dataset and avoid layout shift in the first rendered page.

## Testing

- Domain/unit tests for cursor parsing, visibility, ranking, blocked/muted filtering, following-only behavior, saves, story expiry/views, ownership, idempotency, and counters.
- API integration tests against PostgreSQL for all feed scopes and mutations, including cross-user authorization failures.
- Assets integration tests for accepted/rejected MIME types, size limits, ownership, and failed scan state.
- Web component tests for optimistic success/rollback, keyboard interaction, comments, composer upload, story viewer, empty/error/loading states, and accessible names.
- Contract/client generation verification after API changes.
- Playwright scenarios with two non-admin users: publish media, follow, verify Following, react, comment/reply, repost, save, view Saved, create/view story, filter tag, and open a Testing Lab session from Community.
- Docker smoke verifies Web, API, PostgreSQL, uploaded media, persistence across refresh, and no dependency on demo data.

## Acceptance Criteria

The feature is production-ready only when:

1. Every visible feed control performs a real persisted action or is removed.
2. A non-admin user can complete every planned flow and see persisted state after refresh.
3. A second non-admin user cannot mutate the first user's profile, posts, comments, stories, saves, or reactions.
4. For You, Following, Community, and Saved return observably different, correct datasets.
5. Media uploads are first-party, validated, and attached only after Assets accepts them.
6. Stories expire and viewer state persists.
7. The page contains no production demo fallback, fake count, fake badge, or misleading route.
8. Targeted unit, integration, typecheck, lint, build, Playwright, Docker, and smoke checks pass from a clean worktree.
9. The API contract and generated client are in sync.
10. Release notes document the additive migration, environment/storage requirements, and rollback behavior.
