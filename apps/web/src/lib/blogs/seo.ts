/**
 * Single source of truth for blog URLs (metadata, RSS, sitemap, JSON-LD).
 *
 * Canonical rule (plan todo 8, evidence `apps/web/src/proxy.ts:46-64`):
 * default locale `en-US` is externally unprefixed, so canonical URLs NEVER
 * carry a locale prefix.
 */
import type { Metadata } from 'next';

import type { BlogPostDetail } from './types';

export const BLOG_INDEX_PATH = '/blogs';

/** Site origin for absolute URLs — mirrors `app/sitemap.ts`. */
export const BLOG_SITE_BASE_URL = process.env.NEXT_PUBLIC_APP_URL ?? 'https://gameguild.gg';

export function buildBlogCanonicalUrl(handle: string, slug: string): string {
  return `${BLOG_INDEX_PATH}/${handle}/${slug}`;
}

export function buildBlogCanonicalPath(handle: string, slug: string): string {
  return buildBlogCanonicalUrl(handle, slug);
}

export function buildBlogAbsoluteCanonicalUrl(baseUrl: string, handle: string, slug: string): string {
  return `${baseUrl.replace(/\/$/, '')}${buildBlogCanonicalUrl(handle, slug)}`;
}

/** Author profile path — unprefixed canonical form (RSS channel link, cards). */
export function buildBlogAuthorPath(handle: string): string {
  return `${BLOG_INDEX_PATH}/${handle}`;
}

export function buildBlogAuthorAbsoluteUrl(baseUrl: string, handle: string): string {
  return `${baseUrl.replace(/\/$/, '')}${buildBlogAuthorPath(handle)}`;
}

/**
 * Next static routes shadow `[username]`; handles colliding with them must
 * never reach the dynamic route. `rss.xml` is reserved by the per-author RSS
 * route at `blogs/[username]/rss.xml`.
 */
const RESERVED_BLOG_SEGMENTS = new Set(['rss.xml']);

export function isReservedBlogSegment(username: string): boolean {
  return RESERVED_BLOG_SEGMENTS.has(username);
}

/** Author display info for metadata/JSON-LD (co-authors carry handles only). */
export interface BlogAuthorProfile {
  handle?: string | null;
  displayName?: string | null;
}

function blogAuthorNames(post: BlogPostDetail, authorProfiles?: BlogAuthorProfile[]): string[] {
  const names = authorProfiles?.map((profile) => profile.displayName ?? profile.handle).filter((n): n is string => Boolean(n));
  if (names && names.length > 0) return names;

  const primary = post.primaryAuthorDisplayName ?? post.primaryAuthorHandle;
  const coAuthors = (post.coAuthorHandles ?? []).filter((h): h is string => Boolean(h));
  return [...(primary ? [primary] : []), ...coAuthors];
}

function blogDescription(post: BlogPostDetail): string | undefined {
  return post.metaDescription ?? post.excerpt ?? undefined;
}

/**
 * Full Ghost-grade metadata for a published post. Canonical is an absolute
 * UNPREFIXED URL; `alternates.languages` maps every locale explicitly
 * (default locale without prefix, others with).
 */
export function buildBlogPostMetadata(post: BlogPostDetail, authorProfiles?: BlogAuthorProfile[]): Metadata {
  const handle = post.primaryAuthorHandle ?? '';
  const slug = post.slug ?? '';
  const canonical = buildBlogAbsoluteCanonicalUrl(BLOG_SITE_BASE_URL, handle, slug);
  const title = post.metaTitle ?? post.title ?? undefined;
  const description = blogDescription(post);
  const publishedTime = post.publishedAt ?? undefined;
  const twitterCard = (post.twitterCard ?? 'summary_large_image') as 'summary' | 'summary_large_image';

  return {
    title,
    description,
    robots: { index: true, follow: true },
    alternates: {
      canonical,
      languages: {
        'en-US': canonical,
        'pt-BR': `${BLOG_SITE_BASE_URL.replace(/\/$/, '')}/pt-BR${buildBlogCanonicalUrl(handle, slug)}`,
      },
    },
    openGraph: {
      type: 'article',
      title,
      description,
      url: canonical,
      siteName: 'GameGuild',
      ...(publishedTime ? { publishedTime } : {}),
      ...(post.ogImageUrl ? { images: [post.ogImageUrl] } : {}),
      authors: blogAuthorNames(post, authorProfiles),
      ...(post.tags && post.tags.length > 0 ? { tags: post.tags } : {}),
    },
    twitter: {
      card: twitterCard,
      title,
      description,
      ...(post.ogImageUrl ? { images: [post.ogImageUrl] } : {}),
    },
  };
}

/** `BlogPosting` JSON-LD object per plan todo 8 spec. */
export function buildBlogJsonLd(post: BlogPostDetail, authorProfiles?: BlogAuthorProfile[]): Record<string, unknown> {
  const handle = post.primaryAuthorHandle ?? '';
  const slug = post.slug ?? '';
  const canonical = buildBlogAbsoluteCanonicalUrl(BLOG_SITE_BASE_URL, handle, slug);
  const description = blogDescription(post);

  return {
    '@context': 'https://schema.org',
    '@type': 'BlogPosting',
    headline: post.title ?? '',
    ...(description ? { description } : {}),
    ...(post.ogImageUrl ? { image: [post.ogImageUrl] } : {}),
    ...(post.publishedAt ? { datePublished: post.publishedAt } : {}),
    dateModified: post.updatedAt ?? post.publishedAt ?? undefined,
    mainEntityOfPage: canonical,
    author: blogAuthorNames(post, authorProfiles).map((name) => ({ '@type': 'Person', name })),
    ...(post.tags && post.tags.length > 0 ? { keywords: post.tags.join(', ') } : {}),
    publisher: { '@type': 'Organization', name: 'GameGuild', url: BLOG_SITE_BASE_URL },
  };
}

/**
 * JSON-LD script payload: use the stored override when present or generate a
 * `BlogPosting`, then escape HTML script delimiters without changing JSON values.
 */
export function resolveBlogJsonLd(post: BlogPostDetail, authorProfiles?: BlogAuthorProfile[]): string {
  const payload = post.structuredDataOverride && post.structuredDataOverride.trim().length > 0
    ? post.structuredDataOverride
    : JSON.stringify(buildBlogJsonLd(post, authorProfiles));
  return payload.replace(/</g, '\\u003c');
}

export const BLOG_API_BASE_URL = (
  process.env.API_URL ||
  process.env.NEXT_PUBLIC_API_URL ||
  'http://localhost:8080'
).replace(/\/$/, '');
