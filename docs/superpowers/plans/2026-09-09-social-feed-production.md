# Production Social Feed Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Deliver a secure, persisted, non-admin social feed with real streams, publishing, engagement, saves, stories, creator profiles, and Testing Lab community items.

**Architecture:** Keep write ownership in Posts, Follows, Reactions, Profiles, Assets, and Testing Lab. Expand Social Feed into an authenticated read-model boundary with cursor pagination, viewer state, `SavedPost`, `Story`, and `StoryView`; the Web app consumes that contract with server rendering and optimistic client mutations.

**Tech Stack:** .NET 10, ASP.NET Core, EF Core/PostgreSQL, CQRS/MediatR, Next.js App Router, React 19, TypeScript 6, Tailwind CSS, shadcn/ui, Vitest, xUnit, browser automation, Docker Compose.

**Spec:** `docs/superpowers/specs/2026-09-09-social-feed-production-design.md`

## Global Constraints

- Direct messages and real-time chat are excluded; remove the fake Messages link.
- All social mutations require authentication and derive the actor from `IActorContextAccessor`.
- One post/story contains at most one image or video.
- Permit JPEG, PNG, WebP, GIF, and MP4; images are capped at 10 MiB and video at 100 MiB.
- Normalize tags case-insensitively and cap posts at 10 tags.
- New feed pages contain at most 30 items and use validated opaque keyset cursors.
- Use existing shadcn/ui primitives and semantic Tailwind tokens.
- Apply red-green-refactor TDD to every behavior change.
- Verify end-to-end behavior with non-admin users.

---

### Task 1: Bind social mutations to the authenticated actor

**Files:**
- Modify: `apps/api/Source/Modules/GameGuild.Social.Reactions/ReactionModuleApi.cs`
- Modify: `apps/api/Source/Modules/GameGuild.Social.Reactions/GameGuild.Social.Reactions.csproj`
- Modify: `apps/api/Source/Modules/GameGuild.Social.Profiles/Controllers/SocialProfilesController.cs`
- Test: `apps/api/tests/GameGuild.Social.Reactions.UnitTests/ReactionModuleApiTests.cs`
- Test: `apps/api/tests/GameGuild.Social.Profiles.UnitTests/SocialProfilesModuleTests.cs`

**Interfaces:**
- Produces: `SetReactionRequest(Guid TargetId, ReactionTargetType TargetType, ReactionType Type)`.
- Produces: `RemoveReactionRequest(Guid TargetId, ReactionTargetType TargetType)`.
- Produces: profile mutations that forbid changes to another user's profile.

- [ ] **Step 1: Write failing tests for actor binding and profile ownership.**

```csharp
[Fact]
public async Task Set_UsesAuthenticatedActorInsteadOfBodyUser()
{
    var actorId = Guid.NewGuid();
    var targetId = Guid.NewGuid();
    var controller = CreateController(actorId);
    await controller.Set(new SetReactionRequest(targetId, ReactionTargetType.Post, ReactionType.Like), default);
    Sender.Verify(x => x.Send(It.Is<SetReactionCommand>(c => c.UserId == actorId), default));
}
```

Add one test each for remove, unauthenticated mutation, self profile update, and cross-user profile update.

- [ ] **Step 2: Run focused tests and verify they fail for the intended security gaps.**

```powershell
dotnet test apps/api/tests/GameGuild.Social.Reactions.UnitTests/GameGuild.Social.Reactions.UnitTests.csproj --filter "FullyQualifiedName~Actor|FullyQualifiedName~Authenticated"
dotnet test apps/api/tests/GameGuild.Social.Profiles.UnitTests/GameGuild.Social.Profiles.UnitTests.csproj --filter "FullyQualifiedName~Ownership"
```

- [ ] **Step 3: Add `[Authorize]`, inject `IActorContextAccessor`, remove public mutation `UserId` fields, and enforce self ownership in profile PUT routes.**

```csharp
public sealed record SetReactionRequest(Guid TargetId, ReactionTargetType TargetType, ReactionType Type);
public sealed record RemoveReactionRequest(Guid TargetId, ReactionTargetType TargetType);
```

- [ ] **Step 4: Run both complete module suites and verify zero failures.**

```powershell
dotnet test apps/api/tests/GameGuild.Social.Reactions.UnitTests/GameGuild.Social.Reactions.UnitTests.csproj
dotnet test apps/api/tests/GameGuild.Social.Profiles.UnitTests/GameGuild.Social.Profiles.UnitTests.csproj
```

- [ ] **Step 5: Commit.**

```powershell
git add apps/api/Source/Modules/GameGuild.Social.Reactions apps/api/Source/Modules/GameGuild.Social.Profiles apps/api/tests/GameGuild.Social.Reactions.UnitTests apps/api/tests/GameGuild.Social.Profiles.UnitTests
git commit -m "fix(social): bind mutations to authenticated actors"
```

### Task 2: Add saved-post persistence and idempotent endpoints

**Files:**
- Create: `apps/api/Source/Modules/GameGuild.Social.Feed/Entities/SavedPost.cs`
- Create: `apps/api/Source/Modules/GameGuild.Social.Feed/SavedPosts/SavedPostContracts.cs`
- Create: `apps/api/Source/Modules/GameGuild.Social.Feed/SavedPosts/SavedPostService.cs`
- Create: `apps/api/Source/Modules/GameGuild.Social.Feed/SavedPosts/SavedPostsController.cs`
- Modify: `apps/api/Source/Modules/GameGuild.Social.Feed/FeedModuleApi.cs`
- Test: `apps/api/tests/GameGuild.Social.Feed.UnitTests/SavedPostTests.cs`

**Interfaces:**
- Produces: `Task<bool> SaveAsync(Guid userId, Guid postId, CancellationToken ct)`.
- Produces: `Task<bool> UnsaveAsync(Guid userId, Guid postId, CancellationToken ct)`.
- Produces: `GET`, `PUT`, and `DELETE /api/social/saved-posts/{postId}` for the actor.

- [ ] **Step 1: Write failing tests for duplicate save, duplicate unsave, and unavailable post rejection.**

```csharp
[Fact]
public async Task SaveAsync_Twice_CreatesOneRow()
{
    (await Service.SaveAsync(UserId, PostId, default)).Should().BeTrue();
    (await Service.SaveAsync(UserId, PostId, default)).Should().BeFalse();
    (await Db.Set<SavedPost>().CountAsync()).Should().Be(1);
}
```

- [ ] **Step 2: Run the SavedPost tests and verify compilation fails because the domain is absent.**

```powershell
dotnet test apps/api/tests/GameGuild.Social.Feed.UnitTests/GameGuild.Social.Feed.UnitTests.csproj --filter "FullyQualifiedName~SavedPost"
```

- [ ] **Step 3: Implement `SavedPost`, EF configuration, service, controller, and dependency registrations.**

```csharp
public sealed class SavedPost : EntityBase
{
    public Guid UserId { get; private set; }
    public Guid PostId { get; private set; }
    private SavedPost() { }
    public static SavedPost Create(Guid userId, Guid postId) => new() { Id = Guid.NewGuid(), UserId = userId, PostId = postId };
}
```

Configure unique `(UserId, PostId)` and lookup `(UserId, CreatedAt)` indexes.

- [ ] **Step 4: Run the complete Social Feed test project and verify zero failures.**

```powershell
dotnet test apps/api/tests/GameGuild.Social.Feed.UnitTests/GameGuild.Social.Feed.UnitTests.csproj
```

- [ ] **Step 5: Commit.**

```powershell
git add apps/api/Source/Modules/GameGuild.Social.Feed apps/api/tests/GameGuild.Social.Feed.UnitTests
git commit -m "feat(feed): persist saved posts"
```

### Task 3: Add expiring stories and view state

**Files:**
- Create: `apps/api/Source/Modules/GameGuild.Social.Feed/Entities/Story.cs`
- Create: `apps/api/Source/Modules/GameGuild.Social.Feed/Stories/StoryContracts.cs`
- Create: `apps/api/Source/Modules/GameGuild.Social.Feed/Stories/StoryService.cs`
- Create: `apps/api/Source/Modules/GameGuild.Social.Feed/Stories/StoriesController.cs`
- Modify: `apps/api/Source/Modules/GameGuild.Social.Feed/FeedModuleApi.cs`
- Modify: `apps/api/Source/Modules/GameGuild.Social.Feed/GameGuild.Social.Feed.csproj`
- Test: `apps/api/tests/GameGuild.Social.Feed.UnitTests/StoryTests.cs`

**Interfaces:**
- Produces: `StoryDto(Guid Id, Guid AuthorId, Guid AssetId, string MediaUrl, string MediaType, string? Caption, DateTime CreatedAt, DateTime ExpiresAt, bool IsViewed, bool CanDelete)`.
- Produces: list/create/view/delete routes under `/api/social/stories`.

- [ ] **Step 1: Write failing tests for 24-hour expiration, block/mute exclusion, asset ownership, author deletion, and idempotent view recording.**

```csharp
[Fact]
public async Task RecordViewAsync_Twice_CreatesOneView()
{
    (await Service.RecordViewAsync(ViewerId, StoryId, default)).Should().BeTrue();
    (await Service.RecordViewAsync(ViewerId, StoryId, default)).Should().BeFalse();
    (await Db.Set<StoryView>().CountAsync()).Should().Be(1);
}
```

- [ ] **Step 2: Run story tests and verify compilation fails on missing story types.**

```powershell
dotnet test apps/api/tests/GameGuild.Social.Feed.UnitTests/GameGuild.Social.Feed.UnitTests.csproj --filter "FullyQualifiedName~Story"
```

- [ ] **Step 3: Implement `Story`, `StoryView`, EF indexes, service, actor-bound controller, and 24-hour expiration.**

```csharp
public sealed record CreateStoryRequest(Guid AssetId, string? Caption);
public sealed record StoryPageDto(IReadOnlyList<StoryDto> Items);
```

- [ ] **Step 4: Run Social Feed tests and verify zero failures.**

```powershell
dotnet test apps/api/tests/GameGuild.Social.Feed.UnitTests/GameGuild.Social.Feed.UnitTests.csproj
```

- [ ] **Step 5: Commit.**

```powershell
git add apps/api/Source/Modules/GameGuild.Social.Feed apps/api/tests/GameGuild.Social.Feed.UnitTests
git commit -m "feat(feed): add expiring social stories"
```

### Task 4: Complete post ownership, reposts, comments, and counters

**Files:**
- Modify: `apps/api/Source/Modules/GameGuild.Social.Posts/Entities/Post.cs`
- Modify: `apps/api/Source/Modules/GameGuild.Social.Posts/Services/PostCrudService.cs`
- Modify: `apps/api/Source/Modules/GameGuild.Social.Posts/Services/PostEngagementService.cs`
- Modify: `apps/api/Source/Modules/GameGuild.Social.Posts/Controllers/PostsCrudController.cs`
- Modify: `apps/api/Source/Modules/GameGuild.Social.Posts/Controllers/PostInteractionsController.cs`
- Modify: `apps/api/Source/Modules/GameGuild.Social.Posts/Controllers/PostCommentsController.cs`
- Modify: `apps/api/Source/Modules/GameGuild.Social.Posts/Commands/PostEndpointCommands.cs`
- Test: `apps/api/tests/GameGuild.Social.Posts.UnitTests/Services/PostCrudServiceTests.cs`
- Test: `apps/api/tests/GameGuild.Social.Posts.UnitTests/Services/PostEngagementServiceTests.cs`
- Test: `apps/api/tests/GameGuild.Social.Posts.UnitTests/Services/PostCommentServiceTests.cs`

**Interfaces:**
- Produces: actor-owned edit/delete, idempotent repost, and persisted counters consistent with engagement rows.
- Produces: `POST /api/v1/posts/{postId}/reposts` with `CreateRepostRequest(string? Content)`.

- [ ] **Step 1: Write failing tests for cross-author mutation, idempotent repost, self-repost, deleted/private targets, reaction counts, and comment add/delete counts.**

```csharp
[Fact]
public async Task RepostAsync_SameActorAndTarget_IsIdempotent()
{
    var first = await Service.RepostAsync(ActorId, SourceId, "Worth playing", default);
    var second = await Service.RepostAsync(ActorId, SourceId, "Worth playing", default);
    second.Value!.Id.Should().Be(first.Value!.Id);
}
```

- [ ] **Step 2: Run focused Posts tests and verify intended failures.**

```powershell
dotnet test apps/api/tests/GameGuild.Social.Posts.UnitTests/GameGuild.Social.Posts.UnitTests.csproj --filter "FullyQualifiedName~Ownership|FullyQualifiedName~Repost|FullyQualifiedName~Counter"
```

- [ ] **Step 3: Implement actor checks, `Post.CreateRepost`, active actor-target uniqueness, and transactional counter updates.**

```csharp
public static Post CreateRepost(Guid authorId, Guid originalPostId, string content)
{
    var post = Create(authorId, content, PostVisibility.Public);
    post.RepostOfPostId = originalPostId;
    return post;
}
```

- [ ] **Step 4: Run the complete Posts suite and verify zero failures.**

```powershell
dotnet test apps/api/tests/GameGuild.Social.Posts.UnitTests/GameGuild.Social.Posts.UnitTests.csproj
```

- [ ] **Step 5: Commit.**

```powershell
git add apps/api/Source/Modules/GameGuild.Social.Posts apps/api/tests/GameGuild.Social.Posts.UnitTests
git commit -m "feat(posts): secure author actions and reposts"
```

### Task 5: Build the composed cursor feed read model

**Files:**
- Create: `apps/api/Source/Modules/GameGuild.Social.Feed/Queries/SocialFeedContracts.cs`
- Create: `apps/api/Source/Modules/GameGuild.Social.Feed/Queries/FeedCursor.cs`
- Create: `apps/api/Source/Modules/GameGuild.Social.Feed/Queries/SocialFeedQueryService.cs`
- Create: `apps/api/Source/Modules/GameGuild.Social.Feed/Controllers/SocialFeedController.cs`
- Modify: `apps/api/Source/Modules/GameGuild.Social.Feed/GameGuild.Social.Feed.csproj`
- Modify: `apps/api/Source/Modules/GameGuild.Social.Feed/FeedModuleApi.cs`
- Test: `apps/api/tests/GameGuild.Social.Feed.UnitTests/FeedCursorTests.cs`
- Test: `apps/api/tests/GameGuild.Social.Feed.UnitTests/SocialFeedQueryServiceTests.cs`

**Interfaces:**
- Produces: `FeedScope { ForYou, Following, Community, Saved }`.
- Produces: `SocialFeedPageDto(IReadOnlyList<SocialFeedItemDto> Items, string? NextCursor)`.
- Produces: actor-bound `GET /api/social/feed?scope=&cursor=&take=&tag=`.

- [ ] **Step 1: Write failing tests for cursor validation, page stability, all four streams, visibility, block/mute exclusion, tags, viewer state, and Testing Lab items.**

```csharp
[Theory]
[InlineData("garbage")]
[InlineData("eyJ0IjoiYmFkIn0=")]
public void TryDecode_RejectsMalformedCursor(string value)
{
    FeedCursor.TryDecode(value, out _).Should().BeFalse();
}
```

- [ ] **Step 2: Run query tests and verify compilation fails on the missing read model.**

```powershell
dotnet test apps/api/tests/GameGuild.Social.Feed.UnitTests/GameGuild.Social.Feed.UnitTests.csproj --filter "FullyQualifiedName~FeedCursor|FullyQualifiedName~SocialFeedQuery"
```

- [ ] **Step 3: Implement contract projection and keyset ordering.**

```csharp
public enum FeedScope { ForYou, Following, Community, Saved }
public enum SocialFeedItemKind { Post, Repost, TestingSession }
public sealed record SocialFeedPageDto(IReadOnlyList<SocialFeedItemDto> Items, string? NextCursor);
public sealed record FeedViewerStateDto(string? Reaction, bool IsSaved, bool IsFollowingAuthor, bool CanEdit, bool CanDelete);
```

Use `AsNoTracking`, bounded take, timestamp/stable-ID ties, and separate failure from an empty page.

- [ ] **Step 4: Run Social Feed, Follows, and Testing Lab suites and verify zero failures.**

```powershell
dotnet test apps/api/tests/GameGuild.Social.Feed.UnitTests/GameGuild.Social.Feed.UnitTests.csproj
dotnet test apps/api/tests/GameGuild.Social.Follows.UnitTests/GameGuild.Social.Follows.UnitTests.csproj
dotnet test apps/api/tests/GameGuild.TestingLab.UnitTests/GameGuild.TestingLab.UnitTests.csproj
```

- [ ] **Step 5: Commit.**

```powershell
git add apps/api/Source/Modules/GameGuild.Social.Feed apps/api/tests/GameGuild.Social.Feed.UnitTests
git commit -m "feat(feed): compose production social streams"
```

### Task 6: Integrate secure image/video upload

**Files:**
- Modify: `apps/api/Source/Modules/GameGuild.Assets/Controllers/AssetsController.cs`
- Modify: `apps/api/Source/Modules/GameGuild.Assets/Security/AssetUploadAuthorizationService.cs`
- Modify: `apps/api/Source/Modules/GameGuild.Assets/Services/SecureUploadService.cs`
- Modify: `apps/api/Source/Modules/GameGuild.Social.Posts/Controllers/PostsCrudController.cs`
- Modify: `apps/api/Source/Modules/GameGuild.Social.Feed/Stories/StoryService.cs`
- Test: `apps/api/tests/GameGuild.Assets.UnitTests/SocialMediaUploadTests.cs`
- Test: `apps/api/tests/GameGuild.Social.Feed.UnitTests/StoryTests.cs`

**Interfaces:**
- Produces: Assets response with asset ID, delivery URL, MIME type, size, and processing state.
- Consumes: actor-owned completed asset ID in post/story creation.

- [ ] **Step 1: Write failing tests for every allowed MIME type, exact size boundaries, over-limit files, unsupported content, non-owner attachment, unfinished scan, and deleted assets.**

```csharp
[Theory]
[InlineData("image/jpeg", 10485760)]
[InlineData("video/mp4", 104857600)]
public async Task Upload_AllowsExactBoundary(string mime, long bytes)
{
    (await Fixture.Upload(mime, bytes)).IsSuccess.Should().BeTrue();
}
```

- [ ] **Step 2: Run focused Assets and Story tests and verify intended failures.**

```powershell
dotnet test apps/api/tests/GameGuild.Assets.UnitTests/GameGuild.Assets.UnitTests.csproj --filter "FullyQualifiedName~SocialMediaUpload"
dotnet test apps/api/tests/GameGuild.Social.Feed.UnitTests/GameGuild.Social.Feed.UnitTests.csproj --filter "FullyQualifiedName~Asset"
```

- [ ] **Step 3: Add the exact social upload policy and actor-owned attachment checks.**

```csharp
private static readonly IReadOnlyDictionary<string, long> SocialMediaLimits = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase)
{
    ["image/jpeg"] = 10 * 1024 * 1024,
    ["image/png"] = 10 * 1024 * 1024,
    ["image/webp"] = 10 * 1024 * 1024,
    ["image/gif"] = 10 * 1024 * 1024,
    ["video/mp4"] = 100 * 1024 * 1024
};
```

- [ ] **Step 4: Run Assets, Posts, and Social Feed suites and verify zero failures.**

```powershell
dotnet test apps/api/tests/GameGuild.Assets.UnitTests/GameGuild.Assets.UnitTests.csproj
dotnet test apps/api/tests/GameGuild.Social.Posts.UnitTests/GameGuild.Social.Posts.UnitTests.csproj
dotnet test apps/api/tests/GameGuild.Social.Feed.UnitTests/GameGuild.Social.Feed.UnitTests.csproj
```

- [ ] **Step 5: Commit.**

```powershell
git add apps/api/Source/Modules/GameGuild.Assets apps/api/Source/Modules/GameGuild.Social.Posts apps/api/Source/Modules/GameGuild.Social.Feed apps/api/tests/GameGuild.Assets.UnitTests apps/api/tests/GameGuild.Social.Posts.UnitTests apps/api/tests/GameGuild.Social.Feed.UnitTests
git commit -m "feat(social): secure feed media uploads"
```

### Task 7: Add the additive database migration and regenerate the client

**Files:**
- Create: `apps/api/Source/GameGuild.API/Database/Migrations/20260909000100_AddProductionSocialFeed.cs`
- Create: `apps/api/Source/GameGuild.API/Database/Migrations/20260909000100_AddProductionSocialFeed.Designer.cs`
- Modify: `apps/api/Source/GameGuild.API/Database/Migrations/ApplicationDbContextModelSnapshot.cs`
- Test: `apps/api/tests/GameGuild.API.UnitTests/Database/SocialFeedMigrationTests.cs`
- Regenerate: `packages/infrastructure/client/src/generated/types.gen.ts`
- Regenerate: `packages/infrastructure/client/src/generated/modules`

**Interfaces:**
- Produces: `social_saved_posts`, `social_stories`, `social_story_views`, and repost uniqueness/index support.
- Produces: generated TypeScript contracts for feed and its mutations.

- [ ] **Step 1: Write a failing migration contract test that asserts required tables/indexes and rejects destructive operations.**

```csharp
[Fact]
public void Migration_IsAdditive()
{
    var source = ReadMigration("AddProductionSocialFeed");
    source.Should().Contain("social_saved_posts");
    source.Should().Contain("social_story_views");
    source.Should().NotContain("DropTable(");
}
```

- [ ] **Step 2: Run the migration test and verify it fails because the migration is absent.**

```powershell
dotnet test apps/api/tests/GameGuild.API.UnitTests/GameGuild.API.UnitTests.csproj --filter "FullyQualifiedName~SocialFeedMigration"
```

- [ ] **Step 3: Generate and inspect the EF migration, then regenerate the API client.**

```powershell
dotnet ef migrations add AddProductionSocialFeed --project apps/api/Source/GameGuild.API/GameGuild.API.csproj --startup-project apps/api/Source/GameGuild.API/GameGuild.API.csproj --context ApplicationDbContext
corepack pnpm api:client:generate
```

- [ ] **Step 4: Run migration test, API build, and client build.**

```powershell
dotnet test apps/api/tests/GameGuild.API.UnitTests/GameGuild.API.UnitTests.csproj --filter "FullyQualifiedName~SocialFeedMigration"
dotnet build apps/api/Source/GameGuild.API/GameGuild.API.csproj -c Release
corepack pnpm --filter @game-guild/client build
```

- [ ] **Step 5: Commit.**

```powershell
git add apps/api/Source/GameGuild.API/Database/Migrations apps/api/tests/GameGuild.API.UnitTests/Database packages/infrastructure/client/src/generated
git commit -m "chore(feed): add social persistence contracts"
```

### Task 8: Replace Web data access with the composed feed contract

**Files:**
- Create: `apps/web/src/lib/feed/contracts.ts`
- Create: `apps/web/src/lib/feed/queries.ts`
- Create: `apps/web/src/lib/feed/actions.ts`
- Test: `apps/web/src/lib/feed/queries.test.ts`
- Test: `apps/web/src/lib/feed/actions.test.ts`
- Modify: `apps/web/src/components/feed/social-shell.tsx`
- Modify: `apps/web/src/components/feed/infinite-post-feed.tsx`
- Delete: `apps/web/src/lib/posts/demo.ts`

**Interfaces:**
- Produces: `loadSocialFeed({ scope, cursor, take, tag }): Promise<SocialFeedPage>`.
- Produces: typed server actions for reaction, comment, repost, save, follow, post, story, and upload mutations.

- [ ] **Step 1: Write failing tests for scope mapping, opaque cursor forwarding, actor-safe bodies, typed errors, and every mutation's authoritative response.**

```typescript
it("forwards the opaque cursor unchanged", async () => {
  await loadSocialFeed({ scope: "following", cursor: "opaque", take: 12 });
  expect(request).toHaveBeenCalledWith(expect.objectContaining({ params: { scope: "following", cursor: "opaque", take: 12 } }));
});
```

- [ ] **Step 2: Run feed library tests and verify compilation fails on missing contracts.**

```powershell
corepack pnpm --filter @game-guild/web exec vitest run src/lib/feed/queries.test.ts src/lib/feed/actions.test.ts
```

- [ ] **Step 3: Implement the feed data boundary, typed failures, abortable pagination, and remove demo fallback.**

```typescript
export type FeedScope = "for-you" | "following" | "community" | "saved";
export async function loadSocialFeed(input: LoadSocialFeedInput): Promise<SocialFeedPage>;
```

- [ ] **Step 4: Run feed library and existing feed component tests.**

```powershell
corepack pnpm --filter @game-guild/web exec vitest run src/lib/feed src/components/feed
```

- [ ] **Step 5: Commit.**

```powershell
git add apps/web/src/lib/feed apps/web/src/lib/posts apps/web/src/components/feed/social-shell.tsx apps/web/src/components/feed/infinite-post-feed.tsx
git commit -m "refactor(web): consume composed social feed"
```

### Task 9: Implement uploaded-media composer

**Files:**
- Create: `apps/web/src/components/feed/social-media-picker.tsx`
- Create: `apps/web/src/components/feed/social-media-preview.tsx`
- Test: `apps/web/src/components/feed/social-media-picker.test.tsx`
- Test: `apps/web/src/components/feed/social-composer.test.tsx`
- Modify: `apps/web/src/components/feed/social-composer.tsx`
- Modify: `apps/web/src/components/feed/infinite-post-feed.tsx`

**Interfaces:**
- Produces: validated `SelectedSocialMedia` and a returned `SocialPostItem` inserted once the top once.

- [ ] **Step 1: Inspect shadcn configuration and official component docs.**

```powershell
corepack pnpm dlx shadcn@latest info --json
corepack pnpm dlx shadcn@latest docs button dialog progress textarea alert-dialog
```

- [ ] **Step 2: Write failing tests for upload-before-create order, MIME/size rejection, preview removal, progress, retry, duplicate submit, character limit, and successful insertion.**

```typescript
it("uploads media before creating a post", async () => {
  await user.upload(screen.getByLabelText(/add media/i), imageFile);
  await user.click(screen.getByRole("button", { name: /publish/i }));
  expect(uploadSocialMedia).toHaveBeenCalledBefore(createSocialPost);
});
```

- [ ] **Step 3: Run composer tests and verify intended failures.**

```powershell
corepack pnpm --filter @game-guild/web exec vitest run src/components/feed/social-media-picker.test.tsx src/components/feed/social-composer.test.tsx
```

- [ ] **Step 4: Implement file selection, object URL cleanup, upload progress/retry, duplicate-submit protection, and authoritative insertion. Remove the remote URL input.**

- [ ] **Step 5: Run feed component tests and commit.**

```powershell
corepack pnpm --filter @game-guild/web exec vitest run src/components/feed
git add apps/web/src/components/feed
git commit -m "feat(feed): publish uploaded social media"
```

### Task 10: Connect post engagement and owner actions

**Files:**
- Create: `apps/web/src/components/feed/post-engagement.tsx`
- Create: `apps/web/src/components/feed/post-comments.tsx`
- Create: `apps/web/src/components/feed/repost-dialog.tsx`
- Create: `apps/web/src/components/feed/post-owner-menu.tsx`
- Test: `apps/web/src/components/feed/post-engagement.test.tsx`
- Test: `apps/web/src/components/feed/post-comments.test.tsx`
- Test: `apps/web/src/components/feed/repost-dialog.test.tsx`
- Test: `apps/web/src/components/feed/post-owner-menu.test.tsx`
- Modify: `apps/web/src/components/feed/post-card.tsx`

**Interfaces:**
- Consumes: Task 8 server actions.
- Produces: optimistic reaction/save/follow state with rollback and authoritative reconciliation.

- [ ] **Step 1: Write failing tests for reaction, save, repost, comment/reply pagination, share/copy fallback, edit/delete, rollback, and pending double-click behavior.**

```typescript
it("rolls back a failed reaction", async () => {
  setPostReaction.mockRejectedValue(new Error("offline"));
  await user.click(screen.getByRole("button", { name: /like/i }));
  await waitFor(() => expect(screen.getByRole("button", { name: /like/i })).toHaveAttribute("aria-pressed", "false"));
});
```

- [ ] **Step 2: Run interaction tests and verify intended failures.**

```powershell
corepack pnpm --filter @game-guild/web exec vitest run src/components/feed/post-engagement.test.tsx src/components/feed/post-comments.test.tsx src/components/feed/repost-dialog.test.tsx src/components/feed/post-owner-menu.test.tsx
```

- [ ] **Step 3: Implement focused components using `aria-pressed`, Dialog/Drawer, AlertDialog, live counts, and target-scoped pending state.**

- [ ] **Step 4: Run all feed component tests and commit.**

```powershell
corepack pnpm --filter @game-guild/web exec vitest run src/components/feed
git add apps/web/src/components/feed apps/web/src/lib/feed
git commit -m "feat(feed): connect social engagement"
```

### Task 11: Implement stories, profiles, rail data, and truthful navigation

**Files:**
- Create: `apps/web/src/components/feed/story-viewer.tsx`
- Create: `apps/web/src/components/feed/story-composer.tsx`
- Test: `apps/web/src/components/feed/story-viewer.test.tsx`
- Test: `apps/web/src/components/feed/story-composer.test.tsx`
- Modify: `apps/web/src/components/feed/build-stories.tsx`
- Modify: `apps/web/src/components/feed/social-rail.tsx`
- Create: `apps/web/src/app/[locale]/(social)/social/profiles/[handle]/page.tsx`
- Create: `apps/web/src/components/feed/social-profile.tsx`
- Test: `apps/web/src/components/feed/social-profile.test.tsx`
- Modify: `apps/web/src/components/feed/social-feed-tabs.tsx`
- Modify: `apps/web/src/components/feed/social-sidebar.tsx`
- Modify: `apps/web/src/app/[locale]/(social)/social/page.tsx`

**Interfaces:**
- Produces: accessible story viewer, real creator profile, real tags/suggestions, Saved stream, and distinct Community stream.

- [ ] **Step 1: Write failing tests for story keyboard/timer/view behavior, creator follow, real metrics/tags, current-user exclusion, profile route, Messages absence, and all four stream routes.**

```typescript
it("does not expose a Messages destination", () => {
  render(<SocialSidebar />);
  expect(screen.queryByRole("link", { name: "Messages" })).not.toBeInTheDocument();
});
```

- [ ] **Step 2: Run focused stories/profile/navigation tests and verify intended failures.**

```powershell
corepack pnpm --filter @game-guild/web exec vitest run src/components/feed/story-viewer.test.tsx src/components/feed/story-composer.test.tsx src/components/feed/social-profile.test.tsx src/components/feed/social-navigation.test.tsx
```

- [ ] **Step 3: Implement story controls with reduced-motion support, real profile metrics, follow rollback, dynamic tags, and locale-aware feed routes.**

- [ ] **Step 4: Run the complete Web feed suite and commit.**

```powershell
corepack pnpm --filter @game-guild/web exec vitest run src/components/feed src/lib/feed 'src/app/[locale]/(social)'
git add apps/web/src/components/feed apps/web/src/lib/feed 'apps/web/src/app/[locale]/(social)'
git commit -m "feat(feed): add stories profiles and real navigation"
```

### Task 12: Add migration, non-admin browser coverage, and production verification

**Files:**
- Create: `apps/web/scripts/social-feed-browser-e2e.mjs`
- Create: `apps/web/scripts/social-feed-browser-e2e.test.mjs`
- Modify: `apps/web/package.json`
- Modify: `scripts/devops/smoke-check.mjs`
- Test: `scripts/devops/smoke-check.test.mjs`

**Interfaces:**
- Produces: `pnpm --filter @game-guild/web test:browser:social-feed`.
- Produces: merge-readiness evidence without deploying automatically.

- [ ] **Step 1: Write a failing browser-runner contract test requiring published, reacted, commented, reposted, saved, followed, story-viewed, stream-separated, and persisted-after-reload evidence.**

```javascript
test("requires every persisted social capability", () => {
  assert.deepEqual(missingEvidence({}), ["published", "reacted", "commented", "reposted", "saved", "followed", "storyViewed", "streamsSeparated", "persistedAfterReload"]);
});
```

- [ ] **Step 2: Run the runner and smoke tests and verify intended failures.**

```powershell
node --test apps/web/scripts/social-feed-browser-e2e.test.mjs scripts/devops/smoke-check.test.mjs
```

- [ ] **Step 3: Implement a two-user non-admin browser journey and add the script command. Credentials come from existing local test-user environment variables and are never committed.**

- [ ] **Step 4: Run complete verification.**

```powershell
git diff --check develop...HEAD
dotnet test apps/api/tests/GameGuild.Social.Feed.UnitTests/GameGuild.Social.Feed.UnitTests.csproj -c Release
dotnet test apps/api/tests/GameGuild.Social.Posts.UnitTests/GameGuild.Social.Posts.UnitTests.csproj -c Release
dotnet test apps/api/tests/GameGuild.Social.Follows.UnitTests/GameGuild.Social.Follows.UnitTests.csproj -c Release
dotnet test apps/api/tests/GameGuild.Social.Reactions.UnitTests/GameGuild.Social.Reactions.UnitTests.csproj -c Release
dotnet test apps/api/tests/GameGuild.Social.Profiles.UnitTests/GameGuild.Social.Profiles.UnitTests.csproj -c Release
dotnet test apps/api/tests/GameGuild.Assets.UnitTests/GameGuild.Assets.UnitTests.csproj -c Release
dotnet build apps/api/Source/GameGuild.API/GameGuild.API.csproj -c Release
corepack pnpm --filter @game-guild/client build
corepack pnpm --filter @game-guild/web exec vitest run src/components/feed src/lib/feed 'src/app/[locale]/(social)'
corepack pnpm --filter @game-guild/web lint
corepack pnpm --filter @game-guild/web build
corepack pnpm db:dbml:check
node --test apps/web/scripts/social-feed-browser-e2e.test.mjs scripts/devops/smoke-check.test.mjs
docker compose -f compose.yaml --profile app up -d --build
corepack pnpm --filter @game-guild/web test:browser:social-feed
corepack pnpm smoke
```

- [ ] **Step 5: Commit browser/smoke coverage and invoke `superpowers:finishing-a-development-branch`; do not merge to `develop` until the user chooses the integration action.**

```powershell
git add apps/web/scripts apps/web/package.json scripts/devops
git commit -m "test(feed): verify non-admin social journeys"
```
