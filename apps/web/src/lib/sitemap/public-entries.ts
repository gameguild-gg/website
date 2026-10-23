import { getBlogIndex } from '@/lib/blogs/queries';
import type { BlogPostSummary } from '@/lib/blogs/types';
import type { PublicProject } from '@/lib/community/public-community';
import { getPublishedProjectsPage, PUBLIC_PROJECT_PAGE_SIZE } from '@/lib/projects/public-projects';

export async function getAllPublishedProjectsForSitemap(): Promise<PublicProject[]> {
  const projects = new Map<string, PublicProject>();
  let skip = 0;
  for (;;) {
    const page = await getPublishedProjectsPage(skip, PUBLIC_PROJECT_PAGE_SIZE);
    const previousCount = projects.size;
    for (const project of page) {
      const identity = project.id ? `id:${project.id}` : `slug:${project.slug}`;
      if (!projects.has(identity)) projects.set(identity, project);
    }
    if (page.length < PUBLIC_PROJECT_PAGE_SIZE) return [...projects.values()];
    if (projects.size === previousCount) throw new Error('Public project pagination did not advance.');
    skip += page.length;
  }
}

export async function getAllBlogPostsForSitemap(): Promise<BlogPostSummary[]> {
  const posts = new Map<string, BlogPostSummary>();
  const cursors = new Set<string>();
  let cursor: { beforePublishedAt: string; beforeId: string } | undefined;
  for (;;) {
    const page = await getBlogIndex(cursor);
    if (!page) return [...posts.values()];
    for (const post of page.items) {
      const identity = post.id ? `id:${post.id}` : `route:${post.primaryAuthorHandle ?? ''}/${post.slug ?? ''}`;
      if (!posts.has(identity)) posts.set(identity, post);
    }
    if (!page.hasMore) return [...posts.values()];
    const last = page.items.at(-1);
    if (!last?.id?.trim() || !last.publishedAt || !Number.isFinite(Date.parse(last.publishedAt))) {
      throw new Error('Public blog pagination omitted its continuation cursor.');
    }
    const identity = JSON.stringify([last.publishedAt, last.id]);
    if (cursors.has(identity)) throw new Error('Public blog pagination did not advance.');
    cursors.add(identity);
    cursor = { beforePublishedAt: last.publishedAt, beforeId: last.id };
  }
}
