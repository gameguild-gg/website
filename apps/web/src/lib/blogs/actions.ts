'use server';

import { getRequestAuthContext } from '@/auth';
import { assertSafeServiceUrl } from '@/lib/security/safe-remote-url';

import type {
  AddBlogCommentInput,
  BlogComment,
  BlogPostDetail,
  CreateBlogPostInput,
  UpdateBlogPostDraftInput,
} from './types';

const apiBaseUrl = (
  process.env.API_URL ||
  process.env.NEXT_PUBLIC_API_URL ||
  'http://localhost:8080'
).replace(/\/$/, '');

export type BlogActionResult<T> =
  | { success: true; data: T }
  | { success: false; error: string; status: number; code?: string }
  | {
      success: false;
      error: string;
      status: 409;
      code: 'revision-conflict';
      expectedRevision: number;
      currentRevision: number;
    };

interface ProblemDetails {
  title?: unknown;
  detail?: unknown;
  currentRevision?: unknown;
  expectedRevision?: unknown;
  errors?: unknown;
}

function readProblemField(body: unknown, name: string): unknown {
  if (body && typeof body === 'object' && name in body) {
    return (body as Record<string, unknown>)[name];
  }
  return undefined;
}

/**
 * Maps a failed API response to a typed action result. 409 ProblemDetails
 * responses carry `expectedRevision`/`currentRevision` and surface as the
 * `revision-conflict` variant so the editor can offer a reload.
 */
async function blogRequest<T>(path: string, init?: RequestInit): Promise<BlogActionResult<T>> {
  const { token, tenantId } = await getRequestAuthContext();
  if (!token || !tenantId) {
    return {
      success: false,
      error: 'You must be signed in to manage blog posts.',
      status: 401,
    };
  }

  const response = await fetch(assertSafeServiceUrl(`${apiBaseUrl}${path}`, apiBaseUrl), {
    ...init,
    redirect: 'error',
    headers: {
      Authorization: `Bearer ${token}`,
      'X-Tenant-Id': tenantId,
      ...(init?.body ? { 'Content-Type': 'application/json' } : {}),
      ...init?.headers,
    },
    cache: 'no-store',
  });

  const body = (await response.json().catch(() => null)) as ProblemDetails | T | null;
  if (!response.ok) {
    const error = readProblemField(body, 'detail');
    const message =
      (typeof error === 'string' && error) ||
      (typeof readProblemField(body, 'title') === 'string' && (readProblemField(body, 'title') as string)) ||
      response.statusText ||
      'Blog request failed.';

    if (response.status === 409) {
      return {
        success: false,
        error: message,
        status: 409,
        code: 'revision-conflict',
        expectedRevision: Number(readProblemField(body, 'expectedRevision') ?? 0),
        currentRevision: Number(readProblemField(body, 'currentRevision') ?? 0),
      };
    }

    return {
      success: false,
      error: message,
      status: response.status,
      code: typeof readProblemField(body, 'code') === 'string' ? (readProblemField(body, 'code') as string) : undefined,
    };
  }

  return { success: true, data: body as T };
}

function path(postId: string, suffix = ''): string {
  return `/api/social/blog/posts/${encodeURIComponent(postId)}${suffix}`;
}

const REACTION_TARGET_TYPE = 'BlogPost';
const REACTION_TYPE = 'Like';

export async function fetchViewerReaction(postId: string): Promise<{ reacted: boolean }> {
  const { token } = await getRequestAuthContext();
  if (!token) return { reacted: false };
  const response = await fetch(
    assertSafeServiceUrl(`${apiBaseUrl}/api/social/reactions/me/target/${REACTION_TARGET_TYPE}/${encodeURIComponent(postId)}`, apiBaseUrl),
    { redirect: 'error', headers: { Authorization: `Bearer ${token}` }, cache: 'no-store' },
  );
  if (!response.ok) return { reacted: false };
  const body = (await response.json().catch(() => null)) as { type?: string } | null;
  return { reacted: body?.type != null };
}

export async function setReaction(postId: string, react: boolean): Promise<{ ok: boolean }> {
  const { token, tenantId } = await getRequestAuthContext();
  if (!token || !tenantId) return { ok: false };
  const response = await fetch(assertSafeServiceUrl(`${apiBaseUrl}/api/social/reactions`, apiBaseUrl), {
    redirect: 'error',
    method: react ? 'PUT' : 'DELETE',
    headers: {
      Authorization: `Bearer ${token}`,
      'X-Tenant-Id': tenantId,
      ...(react ? { 'Content-Type': 'application/json' } : {}),
    },
    cache: 'no-store',
    ...(react ? { body: JSON.stringify({ targetType: REACTION_TARGET_TYPE, targetId: postId, type: REACTION_TYPE }) } : { body: JSON.stringify({ targetType: REACTION_TARGET_TYPE, targetId: postId }) }),
  });
  return { ok: response.ok };
}

export async function createPost(input: CreateBlogPostInput) {
  const result = await blogRequest<BlogPostDetail>('/api/social/blog/posts', {
    method: 'POST',
    body: JSON.stringify(input),
  });
  if (!result.success) return result;

  const primaryAuthorId = (result.data as { primaryAuthorId?: string }).primaryAuthorId;
  const handle = primaryAuthorId ? await fetchProfileHandle(primaryAuthorId) : null;
  const slug = result.data.slug;
  const editUrl = handle && slug ? `/blogs/${encodeURIComponent(handle)}/${encodeURIComponent(slug)}/edit` : null;
  return { success: true as const, data: result.data, editUrl };
}

async function fetchProfileHandle(userId: string): Promise<string | null> {
  const { token, tenantId } = await getRequestAuthContext();
  if (!token || !tenantId) return null;

  const response = await fetch(assertSafeServiceUrl(`${apiBaseUrl}/api/social/profiles/users/${encodeURIComponent(userId)}/or-create`, apiBaseUrl), {
    redirect: 'error',
    headers: { Authorization: `Bearer ${token}`, 'X-Tenant-Id': tenantId },
    cache: 'no-store',
  });
  if (!response.ok) return null;

  const profile = (await response.json().catch(() => null)) as { handle?: unknown } | null;
  return typeof profile?.handle === 'string' && profile.handle ? profile.handle : null;
}

/**
 * Public comment page for a post. Server action wrapper so client components
 * never import `@/lib/blogs/queries` (it chains to `@/auth` → `next/headers`,
 * which cannot be bundled for the client).
 */
export async function getBlogPostCommentsPage(
  postId: string,
  cursor?: { afterCreatedAt?: string; afterId?: string },
): Promise<{ items: BlogComment[]; hasMore: boolean } | null> {
  const { getBlogPostComments } = await import('./queries');
  const page = await getBlogPostComments(postId, cursor);
  if (!page) return null;
  return { items: page.items, hasMore: page.hasMore };
}

export async function updateDraft(postId: string, input: UpdateBlogPostDraftInput) {
  return blogRequest<BlogPostDetail>(path(postId), {
    method: 'PUT',
    body: JSON.stringify(input),
  });
}

export async function changeSlug(postId: string, newSlug: string) {
  return blogRequest<BlogPostDetail>(path(postId, '/slug'), {
    method: 'POST',
    body: JSON.stringify({ newSlug }),
  });
}

export async function coauthorAdd(postId: string, userId: string) {
  return blogRequest<void>(path(postId, '/coauthors'), {
    method: 'POST',
    body: JSON.stringify({ userId }),
  });
}

export async function coauthorRemove(postId: string, userId: string) {
  return blogRequest<void>(path(postId, `/coauthors/${encodeURIComponent(userId)}`), {
    method: 'DELETE',
  });
}

export async function transferPrimary(postId: string, newPrimaryUserId: string) {
  return blogRequest<BlogPostDetail>(path(postId, '/transfer-primary'), {
    method: 'POST',
    body: JSON.stringify({ newPrimaryUserId }),
  });
}

export async function publish(postId: string) {
  return blogRequest<BlogPostDetail>(path(postId, '/publish'), { method: 'POST' });
}

export async function unpublish(postId: string) {
  return blogRequest<BlogPostDetail>(path(postId, '/unpublish'), { method: 'POST' });
}

export async function deletePost(postId: string) {
  return blogRequest<void>(path(postId), { method: 'DELETE' });
}

export async function addComment(postId: string, input: AddBlogCommentInput) {
  return blogRequest<BlogComment>(path(postId, '/comments'), {
    method: 'POST',
    body: JSON.stringify(input),
  });
}

export async function deleteComment(commentId: string) {
  return blogRequest<void>(`/api/social/blog/comments/${encodeURIComponent(commentId)}`, {
    method: 'DELETE',
  });
}

export interface CreateAiRunInput {
  conversationId?: string | null;
  postRevision: number;
  instruction: string;
  proposalKind: string;
  selection?: string | null;
  idempotencyKey?: string | null;
}

export async function createAiRun(postId: string, input: CreateAiRunInput) {
  return blogRequest<Record<string, unknown>>(path(postId, '/ai/runs'), {
    method: 'POST',
    body: JSON.stringify(input),
  });
}

export async function cancelAiRun(postId: string, runId: string) {
  return blogRequest<Record<string, unknown>>(path(postId, `/ai/runs/${encodeURIComponent(runId)}/cancel`), {
    method: 'POST',
  });
}

export async function getAiRun(postId: string, runId: string) {
  return blogRequest<Record<string, unknown>>(path(postId, `/ai/runs/${encodeURIComponent(runId)}`));
}

/**
 * Conflict recovery: re-fetches the acting user's post by its current route
 * through the authoring API (server-only client), returning the latest
 * revision + full draft so the editor can discard local edits.
 */
export async function reloadLatestPost(handle: string, slug: string) {
  const { getMyPostBySlug } = await import('./queries');
  const lookup = await getMyPostBySlug(handle, slug);
  if (lookup.status !== 'ok') {
    return { success: false as const, error: 'The latest version could not be loaded.' };
  }
  return { success: true as const, post: lookup.post };
}

/** Resolves a profile handle to a userId for co-author adds. */
export async function resolveProfileByHandle(handle: string): Promise<
  { success: true; userId: string } | { success: false; error: string }
> {
  const clean = handle.trim().replace(/^@/, '');
  if (!clean) return { success: false, error: 'A handle is required.' };

  const { token, tenantId } = await getRequestAuthContext();
  const headers: Record<string, string> = {
    ...(token ? { Authorization: `Bearer ${token}` } : {}),
    ...(tenantId ? { 'X-Tenant-Id': tenantId } : {}),
  };

  const response = await fetch(assertSafeServiceUrl(`${apiBaseUrl}/api/social/profiles/@${encodeURIComponent(clean)}`, apiBaseUrl), {
    redirect: 'error',
    headers,
    cache: 'no-store',
  });
  if (!response.ok) return { success: false, error: `No profile found for @${clean}.` };

  const profile = (await response.json().catch(() => null)) as { userId?: unknown } | null;
  if (!profile || typeof profile.userId !== 'string' || !profile.userId) {
    return { success: false, error: `No profile found for @${clean}.` };
  }
  return { success: true, userId: profile.userId };
}

/** Viewer identity + handle for the blog editor surfaces. */
export async function getViewerBlogAuthor(): Promise<{ userId: string | null; handle: string | null }> {
  const { session, token, tenantId } = await getRequestAuthContext();
  const userId = session && typeof session !== 'function' ? session.user?.id ?? null : null;
  if (!userId) return { userId: null, handle: null };

  const headers: Record<string, string> = {
    ...(token ? { Authorization: `Bearer ${token}` } : {}),
    ...(tenantId ? { 'X-Tenant-Id': tenantId } : {}),
  };

  const response = await fetch(assertSafeServiceUrl(`${apiBaseUrl}/api/social/profiles/users/${encodeURIComponent(userId)}/or-create`, apiBaseUrl), {
    redirect: 'error',
    headers,
    cache: 'no-store',
  });
  if (!response.ok) return { userId, handle: null };

  const profile = (await response.json().catch(() => null)) as { handle?: unknown } | null;
  return { userId, handle: typeof profile?.handle === 'string' ? profile.handle : null };
}

export async function applyProposal(postId: string, proposalId: string, postRevision: number, cursorOffset?: number) {
  return blogRequest<Record<string, unknown>>(path(postId, `/ai/proposals/${encodeURIComponent(proposalId)}/apply`), {
    method: 'POST',
    body: JSON.stringify({ postRevision, cursorOffset }),
  });
}

export async function discardProposal(postId: string, proposalId: string) {
  return blogRequest<Record<string, unknown>>(path(postId, `/ai/proposals/${encodeURIComponent(proposalId)}`), {
    method: 'DELETE',
  });
}
