#!/usr/bin/env node

import { pathToFileURL } from 'node:url';
import { chromium } from 'playwright';
import { writeBrowserEvidence } from './browser-smoke-evidence.mjs';

export const REQUIRED_SOCIAL_EVIDENCE = [
  'nonAdminActors',
  'published',
  'mediaPublished',
  'postEdited',
  'trendingTagVisible',
  'followed',
  'profileMetrics',
  'followingVisible',
  'reacted',
  'commented',
  'replyCreated',
  'commentEdited',
  'commentDeleted',
  'reposted',
  'saved',
  'shared',
  'permalinkOpened',
  'savedVisible',
  'storyPublished',
  'storyMediaDelivered',
  'storyViewed',
  'storyDeleted',
  'persistedAfterReload',
  'streamsSeparated',
  'unfollowed',
  'postDeleted',
];

export function assertSocialEvidence(evidence) {
  const missing = REQUIRED_SOCIAL_EVIDENCE.filter((key) => evidence[key] !== true);
  if (missing.length > 0) throw new Error(`Missing social feed evidence: ${missing.join(', ')}`);
}

const apiBaseUrl = (process.env.API_BASE_URL ?? process.env.NEXT_PUBLIC_API_URL ?? 'http://localhost:8080').replace(/\/$/, '');
const webBaseUrl = (process.env.SOCIAL_FEED_E2E_BASE_URL ?? process.env.NEXT_PUBLIC_APP_URL ?? 'http://localhost:3004').replace(/\/$/, '');
const headless = !['0', 'false', 'no'].includes((process.env.SOCIAL_FEED_E2E_HEADLESS ?? 'true').toLowerCase());
const browserChannel = process.env.SOCIAL_FEED_E2E_BROWSER_CHANNEL ?? (process.platform === 'win32' ? 'chrome' : undefined);
const evidencePath = process.env.PLAYWRIGHT_JSON_OUTPUT_NAME;

function unique() {
  return `${Date.now()}-${Math.random().toString(36).slice(2, 8)}`;
}

async function waitFor(read, predicate, timeoutMs = 60_000) {
  const deadline = Date.now() + timeoutMs;
  let value;
  while (Date.now() < deadline) {
    value = await read();
    if (predicate(value)) return value;
    await new Promise((resolve) => setTimeout(resolve, 250));
  }
  throw new Error(`Timed out waiting for persisted social state: ${JSON.stringify(value)}`);
}

async function assertBrowserMedia(page, locator, label) {
  await locator.waitFor();
  const source = await locator.getAttribute('src');
  if (!source) throw new Error(`${label} did not expose a media URL.`);
  const url = new URL(source, webBaseUrl).toString();
  const response = await page.request.get(url);
  const contentType = response.headers()['content-type'] ?? '';
  const bytes = await response.body();
  if (!response.ok() || !contentType.startsWith('image/') || bytes.byteLength === 0) {
    throw new Error(
      `${label} delivery failed: ${response.status()} ${url}, content-type=${contentType || 'missing'}, bytes=${bytes.byteLength}`,
    );
  }

  try {
    await locator.evaluate((image) => image instanceof HTMLImageElement ? image.decode() : Promise.reject(new Error('not an image')));
  } catch (error) {
    throw new Error(`${label} could not be decoded from ${url}: ${error instanceof Error ? error.message : String(error)}`);
  }
  return locator.evaluate(
    (image) => image instanceof HTMLImageElement && image.complete && image.naturalWidth > 0,
  );
}

async function apiRequest(path, init = {}, accessToken, tenantId) {
  const response = await fetch(`${apiBaseUrl}${path}`, {
    ...init,
    headers: {
      ...(init.body instanceof FormData ? {} : { 'content-type': 'application/json' }),
      ...(accessToken ? { authorization: `Bearer ${accessToken}` } : {}),
      ...(tenantId ? { 'x-tenant-id': tenantId } : {}),
      ...init.headers,
    },
  });
  const body = response.status === 204 ? null : await response.json().catch(() => null);
  if (!response.ok) {
    throw new Error(`${init.method ?? 'GET'} ${path} failed with ${response.status}: ${JSON.stringify(body)}`);
  }
  return body;
}

async function apiStatus(path, init = {}, accessToken, tenantId) {
  const response = await fetch(`${apiBaseUrl}${path}`, {
    ...init,
    headers: {
      ...(accessToken ? { authorization: `Bearer ${accessToken}` } : {}),
      ...(tenantId ? { 'x-tenant-id': tenantId } : {}),
      ...init.headers,
    },
  });
  return response.status;
}

async function createActor(label, tag) {
  const configuredEmail = process.env[`SOCIAL_FEED_E2E_USER_${label}_EMAIL`];
  const configuredPassword = process.env[`SOCIAL_FEED_E2E_USER_${label}_PASSWORD`];
  const email = configuredEmail ?? `social-feed-${label.toLowerCase()}-${tag}@example.test`;
  const password = configuredPassword ?? 'Str0ng!Passw0rd123!';
  const created = !configuredEmail;
  let session;
  if (created) {
    session = await apiRequest('/v1/auth/sign-up', {
      method: 'POST',
      body: JSON.stringify({
        username: `social_feed_${label.toLowerCase()}_${tag.replace(/[^a-z0-9]/gi, '_')}`,
        email,
        password,
      }),
    });
  } else {
    session = await apiRequest('/v1/auth/sign-in', {
      method: 'POST',
      body: JSON.stringify({ email, password }),
    });
  }
  if (!session.accessToken || !session.userId || !session.tenantId) {
    throw new Error(`Social test user ${email} has an incomplete session.`);
  }
  const handle = `e2e-${label.toLowerCase()}-${tag}`.replace(/[^a-z0-9-]/g, '').slice(0, 70);
  await apiRequest(`/api/social/profiles/users/${session.userId}`, {
    method: 'PUT',
    body: JSON.stringify({ handle, displayName: `Feed E2E ${label}`, socialLinksJson: '{}' }),
  }, session.accessToken, session.tenantId);
  return {
    id: session.userId,
    email,
    password,
    handle,
    created,
    accessToken: session.accessToken,
    tenantId: session.tenantId,
  };
}

async function bootstrap() {
  const tag = unique();
  const actorA = await createActor('A', tag);
  const actorB = await createActor('B', tag);
  if (actorA.tenantId !== actorB.tenantId) {
    throw new Error('Social E2E actors must belong to the same tenant.');
  }
  return { actorA, actorB, tenantId: actorA.tenantId, tag };
}

async function signIn(page, actor) {
  await page.goto(`${webBaseUrl}/sign-in?callbackUrl=%2F`, { waitUntil: 'domcontentloaded' });
  await page.locator('form[data-auth-ready="true"]').waitFor({ timeout: 60_000 });
  await page.getByLabel('Email').fill(actor.email);
  await page.getByLabel('Password', { exact: true }).fill(actor.password);
  await page.getByRole('button', { name: 'Sign in', exact: true }).click();
  await page.waitForURL((url) => url.pathname === '/social' || url.pathname === '/', { timeout: 60_000 });
  await page.getByTestId('social-shell').waitFor({ timeout: 60_000 });
  await page.waitForLoadState('networkidle');
}

function monitorPage(page, label, browserErrors, failedResponses) {
  page.on('pageerror', (error) => browserErrors.push(`${label}: ${error.message}`));
  page.on('console', (message) => {
    if (message.type() === 'error' && !/favicon|cloudflareinsights|Failed to load resource.*404/i.test(message.text())) {
      browserErrors.push(`${label}: ${message.text()}`);
    }
  });
  page.on('response', (response) => {
    const url = new URL(response.url());
    if (url.searchParams.has('_rsc') || response.status() < 400) return;
    if (url.origin === webBaseUrl || url.origin === apiBaseUrl) {
      failedResponses.push(`${label}: ${response.status()} ${response.request().method()} ${url.pathname}`);
    }
  });
}

async function feedItem(accessToken, scope, marker, tenantId) {
  const page = await apiRequest(`/api/social/feed?scope=${scope}&take=30`, {}, accessToken, tenantId);
  return page.items?.find((item) => item.post?.content?.includes(marker)) ?? null;
}

async function cleanupFixture(fixture, postId, storyId) {
  if (storyId) await apiStatus(`/api/social/stories/${storyId}`, { method: 'DELETE' }, fixture.actorA.accessToken, fixture.tenantId);
  if (postId) await apiStatus(`/api/v1/posts/${postId}`, { method: 'DELETE' }, fixture.actorA.accessToken, fixture.tenantId);
  await apiStatus(`/api/followers/unfollow?entityId=${fixture.actorA.id}&entityType=User`, { method: 'DELETE' }, fixture.actorB.accessToken, fixture.tenantId);
  for (const actor of [fixture.actorA, fixture.actorB]) {
    if (actor.created) await apiStatus(`/v1/users/${actor.id}`, { method: 'DELETE' }, actor.accessToken, actor.tenantId);
  }
}

export async function runSocialFeedBrowserE2e() {
  const fixture = await bootstrap();
  let browser;
  try {
    browser = await chromium.launch({ headless, ...(browserChannel ? { channel: browserChannel } : {}) });
  } catch (error) {
    await cleanupFixture(fixture, null, null);
    throw error;
  }
  const contextA = await browser.newContext({ viewport: { width: 1440, height: 1000 } });
  const contextB = await browser.newContext({ viewport: { width: 1440, height: 1000 } });
  await contextB.grantPermissions(['clipboard-read', 'clipboard-write'], { origin: webBaseUrl });
  await contextB.addInitScript(() => {
    Object.defineProperty(navigator, 'share', { configurable: true, value: undefined });
  });
  const pageA = await contextA.newPage();
  const pageB = await contextB.newPage();
  const browserErrors = [];
  const failedResponses = [];
  const evidence = Object.fromEntries(REQUIRED_SOCIAL_EVIDENCE.map((key) => [key, false]));
  const marker = `Production feed ${fixture.tag} #e2efeed`;
  const editedMarker = `${marker} edited`;
  const comment = `Persisted comment ${fixture.tag}`;
  const reply = `Persisted reply ${fixture.tag}`;
  const editedReply = `${reply} edited`;
  const png = Buffer.from('iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII=', 'base64');
  let postId = null;
  let storyId = null;
  monitorPage(pageA, 'actor A', browserErrors, failedResponses);
  monitorPage(pageB, 'actor B', browserErrors, failedResponses);
  pageA.setDefaultTimeout(60_000);
  pageB.setDefaultTimeout(60_000);

  try {
    evidence.nonAdminActors = fixture.actorA.id !== fixture.actorB.id &&
      fixture.actorA.email !== fixture.actorB.email;

    await signIn(pageA, fixture.actorA);
    await pageA.getByRole('button', { name: 'Share your progress' }).click();
    await pageA.getByPlaceholder('What are you building?').fill(marker);
    await pageA.getByLabel('Add photo or video').setInputFiles({
      name: 'feed.png',
      mimeType: 'image/png',
      buffer: png,
    });
    await pageA.getByAltText('Selected media preview').waitFor();
    await pageA.getByRole('button', { name: 'Publish', exact: true }).click();
    await pageA.getByText(marker, { exact: false }).first().waitFor();
    const published = await waitFor(
      () => feedItem(fixture.actorA.accessToken, 'for-you', marker, fixture.tenantId),
      (item) => Boolean(item),
    );
    postId = published?.id ?? null;
    evidence.published = Boolean(postId);
    const publishedMedia = pageA.getByAltText('Media shared by Feed E2E A').first();
    await publishedMedia.waitFor();
    evidence.mediaPublished = await assertBrowserMedia(pageA, publishedMedia, 'Post media');

    const ownCard = pageA.getByTestId('post-card').filter({ hasText: marker }).first();
    await ownCard.getByRole('button', { name: 'Post options' }).click();
    await pageA.getByRole('menuitem', { name: 'Edit post' }).click();
    await ownCard.getByLabel('Edit post').fill(editedMarker);
    await ownCard.getByRole('button', { name: 'Save', exact: true }).click();
    await ownCard.getByText(editedMarker, { exact: true }).waitFor();
    await waitFor(
      () => apiRequest(`/api/social/feed/posts/${postId}`, {}, fixture.actorA.accessToken, fixture.tenantId),
      (item) => item?.post?.content === editedMarker && item?.post?.isEdited === true,
    );
    evidence.postEdited = true;

    await waitFor(
      () => apiRequest('/api/v1/posts/tags/popular?count=20', {}, fixture.actorA.accessToken, fixture.tenantId),
      (tags) => Array.isArray(tags) && tags.some((tag) => (tag.name ?? tag.tag)?.toLowerCase() === 'e2efeed'),
    );
    await pageA.reload({ waitUntil: 'networkidle' });
    const trending = pageA.getByRole('heading', { name: 'Trending now' }).locator('xpath=..');
    await trending.getByText('#e2efeed', { exact: true }).waitFor();
    evidence.trendingTagVisible = true;

    await signIn(pageB, fixture.actorB);
    await pageB.goto(`${webBaseUrl}/social/profiles/${fixture.actorA.handle}`, { waitUntil: 'networkidle' });
    await pageB.getByRole('button', { name: /^Follow Feed E2E A$/ }).click();
    await pageB.getByRole('button', { name: /^Unfollow Feed E2E A$/ }).waitFor();
    await waitFor(
      () => apiRequest(
        `/api/followers/is-following?entityId=${fixture.actorA.id}&entityType=User`,
        {},
        fixture.actorB.accessToken,
        fixture.tenantId,
      ),
      (isFollowing) => isFollowing === true,
    );
    evidence.followed = true;

    const profile = await waitFor(
      () => apiRequest(
        `/api/social/feed/profiles/${fixture.actorA.handle}`,
        {},
        fixture.actorB.accessToken,
        fixture.tenantId,
      ),
      (value) => value?.isFollowing === true && value?.postCount >= 1 && value?.followerCount >= 1,
    );
    evidence.profileMetrics = profile.displayName === 'Feed E2E A';

    await waitFor(
      () => feedItem(fixture.actorB.accessToken, 'following', marker, fixture.tenantId),
      (item) => Boolean(item),
    );
    await pageB.goto(`${webBaseUrl}/social?tab=following`, { waitUntil: 'networkidle' });
    const card = pageB.getByTestId('post-card').filter({ hasText: marker }).first();
    await card.waitFor();
    evidence.followingVisible = true;

    await card.getByRole('button', { name: 'React to post' }).click();
    await card.getByRole('button', { name: 'Remove reaction' }).waitFor();
    await waitFor(
      () => feedItem(fixture.actorB.accessToken, 'following', marker, fixture.tenantId),
      (item) => item?.viewer?.reaction === 'Like',
    );
    evidence.reacted = true;

    await card.getByRole('button', { name: /\d+ comments/ }).click();
    await card.getByPlaceholder('Add a comment…').fill(comment);
    await card.getByRole('button', { name: 'Publish comment' }).click();
    const comments = await waitFor(
      () => apiRequest(`/api/v1/posts/${postId}/comments`, {}, fixture.actorB.accessToken, fixture.tenantId),
      (comments) => Array.isArray(comments) && comments.some((entry) => entry.content === comment),
    );
    await card.getByText(comment, { exact: true }).waitFor();
    evidence.commented = true;

    const rootComment = comments.find((entry) => entry.content === comment);
    if (!rootComment?.id) throw new Error('The persisted root comment did not return an id.');
    const rootCommentBlock = card.getByText(comment, { exact: true }).locator('xpath=../..');
    await rootCommentBlock.getByRole('button', { name: 'Reply', exact: true }).click();
    await card.getByPlaceholder('Add a comment…').fill(reply);
    await card.getByRole('button', { name: 'Publish comment' }).click();
    const commentsWithReply = await waitFor(
      () => apiRequest(`/api/v1/posts/${postId}/comments`, {}, fixture.actorB.accessToken, fixture.tenantId),
      (items) => Array.isArray(items) && items.some(
        (entry) => entry.content === reply && entry.parentCommentId === rootComment.id,
      ),
    );
    const persistedReply = commentsWithReply.find((entry) => entry.content === reply);
    if (!persistedReply?.id) throw new Error('The persisted reply did not return an id.');
    await card.getByText(reply, { exact: true }).waitFor();
    evidence.replyCreated = true;

    const replyBlock = card.getByText(reply, { exact: true }).locator('xpath=../..');
    await replyBlock.getByRole('button', { name: 'Edit', exact: true }).click();
    const replyEditor = card.getByLabel('Edit comment');
    await replyEditor.fill(editedReply);
    await replyEditor.locator('xpath=..').getByRole('button', { name: 'Save', exact: true }).click();
    await card.getByText(editedReply, { exact: true }).waitFor();
    await waitFor(
      () => apiRequest(`/api/v1/posts/${postId}/comments`, {}, fixture.actorB.accessToken, fixture.tenantId),
      (items) => Array.isArray(items) && items.some(
        (entry) => entry.id === persistedReply.id && entry.content === editedReply && entry.isEdited === true,
      ),
    );
    evidence.commentEdited = true;

    const editedReplyBlock = card.getByText(editedReply, { exact: true }).locator('xpath=../..');
    await editedReplyBlock.getByRole('button', { name: 'Delete', exact: true }).click();
    const deleteCommentDialog = pageB.getByRole('alertdialog');
    await deleteCommentDialog.getByRole('button', { name: 'Delete', exact: true }).click();
    await waitFor(
      () => apiRequest(`/api/v1/posts/${postId}/comments`, {}, fixture.actorB.accessToken, fixture.tenantId),
      (items) => Array.isArray(items) && !items.some((entry) => entry.id === persistedReply.id),
    );
    await card.getByRole('button', { name: '1 comments' }).waitFor();
    evidence.commentDeleted = true;

    await card.getByRole('button', { name: 'Repost' }).click();
    await card.getByRole('button', { name: 'Repost' }).evaluate((element) => {
      if (element.getAttribute('aria-pressed') !== 'true') throw new Error('Repost state was not persisted in the UI.');
    });
    await waitFor(
      () => feedItem(fixture.actorB.accessToken, 'following', marker, fixture.tenantId),
      (item) => item?.viewer?.hasReposted === true,
    );
    evidence.reposted = true;

    await card.getByRole('button', { name: 'Save post' }).click();
    await card.getByRole('button', { name: 'Remove saved post' }).waitFor();
    await waitFor(
      () => feedItem(fixture.actorB.accessToken, 'saved', marker, fixture.tenantId),
      (item) => item?.viewer?.isSaved === true,
    );
    evidence.saved = true;

    await card.getByRole('button', { name: 'Share post' }).click();
    await pageB.getByText('Post link copied.', { exact: true }).waitFor();
    const permalink = await pageB.evaluate(() => navigator.clipboard.readText());
    evidence.shared = permalink === `${webBaseUrl}/social/posts/${postId}`;
    await pageB.goto(permalink, { waitUntil: 'networkidle' });
    await pageB.getByTestId('post-card').filter({ hasText: editedMarker }).first().waitFor();
    evidence.permalinkOpened = true;

    await pageB.goto(`${webBaseUrl}/social?tab=saved`, { waitUntil: 'networkidle' });
    await pageB.getByTestId('post-card').filter({ hasText: marker }).first().waitFor();
    evidence.savedVisible = true;

    await pageA.goto(`${webBaseUrl}/social`, { waitUntil: 'networkidle' });
    await pageA.getByLabel('Add story').click();
    await pageA.getByLabel('Choose story media').setInputFiles({
      name: 'story.png',
      mimeType: 'image/png',
      buffer: png,
    });
    const stories = await waitFor(
      () => apiRequest('/api/social/stories', {}, fixture.actorA.accessToken, fixture.tenantId),
      (items) => items.some((story) => story.authorId === fixture.actorA.id),
    );
    storyId = stories.find((story) => story.authorId === fixture.actorA.id)?.id ?? null;
    evidence.storyPublished = Boolean(storyId);

    await pageB.reload({ waitUntil: 'networkidle' });
    await pageB.getByLabel(`View Feed E2E A's story`).click();
    const storyDialog = pageB.getByRole('dialog');
    await storyDialog.waitFor();
    const storyMedia = storyDialog.getByRole('img');
    await storyMedia.waitFor();
    evidence.storyMediaDelivered = await assertBrowserMedia(pageB, storyMedia, 'Story media');
    await waitFor(
      () => apiRequest('/api/social/stories', {}, fixture.actorB.accessToken, fixture.tenantId),
      (items) => Array.isArray(items) && items.some((story) => story.id === storyId && story.isViewed === true),
    );
    evidence.storyViewed = true;

    await pageA.reload({ waitUntil: 'networkidle' });
    await pageA.getByLabel(`View Feed E2E A's story`).click();
    const ownStoryDialog = pageA.getByRole('dialog');
    await ownStoryDialog.getByRole('button', { name: 'Delete story' }).click();
    await waitFor(
      () => apiRequest('/api/social/stories', {}, fixture.actorA.accessToken, fixture.tenantId),
      (items) => Array.isArray(items) && !items.some((story) => story.id === storyId),
    );
    evidence.storyDeleted = true;

    await pageB.goto(`${webBaseUrl}/social?tab=saved`, { waitUntil: 'networkidle' });
    const persisted = pageB.getByTestId('post-card').filter({ hasText: marker }).first();
    await persisted.waitFor();
    await persisted.getByRole('button', { name: 'Remove reaction' }).waitFor();
    await persisted.getByRole('button', { name: 'Remove saved post' }).waitFor();
    evidence.persistedAfterReload = true;

    const [followingItem, savedItem, communityItem] = await Promise.all([
      feedItem(fixture.actorB.accessToken, 'following', marker, fixture.tenantId),
      feedItem(fixture.actorB.accessToken, 'saved', marker, fixture.tenantId),
      feedItem(fixture.actorB.accessToken, 'community', marker, fixture.tenantId),
    ]);
    evidence.streamsSeparated = Boolean(followingItem && savedItem && !communityItem);

    await pageB.goto(`${webBaseUrl}/social/profiles/${fixture.actorA.handle}`, { waitUntil: 'networkidle' });
    await pageB.getByRole('button', { name: /^Unfollow Feed E2E A$/ }).click();
    await pageB.getByRole('button', { name: /^Follow Feed E2E A$/ }).waitFor();
    await waitFor(
      () => apiRequest(
        `/api/followers/is-following?entityId=${fixture.actorA.id}&entityType=User`,
        {},
        fixture.actorB.accessToken,
        fixture.tenantId,
      ),
      (isFollowing) => isFollowing === false,
    );
    await waitFor(
      () => feedItem(fixture.actorB.accessToken, 'following', marker, fixture.tenantId),
      (item) => item === null,
    );
    evidence.unfollowed = true;

    await pageA.goto(`${webBaseUrl}/social/posts/${postId}`, { waitUntil: 'networkidle' });
    const deletablePost = pageA.getByTestId('post-card').filter({ hasText: editedMarker }).first();
    await deletablePost.getByRole('button', { name: 'Post options' }).click();
    await pageA.getByRole('menuitem', { name: 'Delete post' }).click();
    const deletePostDialog = pageA.getByRole('alertdialog');
    await deletePostDialog.getByRole('button', { name: 'Delete', exact: true }).click();
    await deletablePost.waitFor({ state: 'detached' });
    await waitFor(
      () => apiStatus(`/api/social/feed/posts/${postId}`, {}, fixture.actorA.accessToken, fixture.tenantId),
      (status) => status === 404,
    );
    evidence.postDeleted = true;

    if (browserErrors.length > 0) throw new Error(`Browser errors:\n${browserErrors.join('\n')}`);
    if (failedResponses.length > 0) throw new Error(`Failed responses:\n${failedResponses.join('\n')}`);
    assertSocialEvidence(evidence);
    await writeBrowserEvidence(evidencePath, { passed: true, errors: [] });
    process.stdout.write(`Social feed browser E2E passed against ${webBaseUrl}.\n`);
    return evidence;
  } finally {
    await browser.close();
    await cleanupFixture(fixture, postId, storyId);
  }
}

if (process.argv[1] && import.meta.url === pathToFileURL(process.argv[1]).href) {
  runSocialFeedBrowserE2e().catch(async (error) => {
    const message = error instanceof Error ? error.message : String(error);
    await writeBrowserEvidence(evidencePath, { passed: false, errors: [message] });
    console.error(message);
    process.exitCode = 1;
  });
}
