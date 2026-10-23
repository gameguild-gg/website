import { beforeEach, describe, expect, it, vi } from 'vitest';
import type { BlogPostSummary } from '@/lib/blogs/types';
import type { PublicProject } from '@/lib/community/public-community';

const projectsPage = vi.hoisted(() => vi.fn());
const blogPage = vi.hoisted(() => vi.fn());
vi.mock('@/lib/projects/public-projects', () => ({
  PUBLIC_PROJECT_PAGE_SIZE: 24,
  getPublishedProjectsPage: (...args: unknown[]) => projectsPage(...args),
}));
vi.mock('@/lib/blogs/queries', () => ({ getBlogIndex: (...args: unknown[]) => blogPage(...args) }));
const { getAllPublishedProjectsForSitemap, getAllBlogPostsForSitemap } = await import('./public-entries');

function project(index: number): PublicProject {
  return {
    id: `project-${index}`, slug: `project-${index}`, title: 'Fixture', creator: 'Fixture author',
    creatorRole: 'Developer', summary: 'Fixture', description: 'Fixture', status: 'Published', tags: [],
    coursePath: '', accent: '', previewImage: '', buildType: 'Web', feedbackGoal: '', metrics: [], media: [],
  };
}

function post(index: number): BlogPostSummary {
  return {
    id: `00000000-0000-4000-8000-${String(index).padStart(12, '0')}`,
    slug: `post-${index}`, primaryAuthorHandle: 'fixture', publishedAt: '2026-10-01T10:00:00.000Z',
  };
}

describe('sitemap public entry pagination', () => {
  beforeEach(() => vi.resetAllMocks());

  it('includes projects beyond the first 24 entries and stops on the final partial page', async () => {
    const first = Array.from({ length: 24 }, (_, index) => project(index));
    projectsPage.mockResolvedValueOnce(first).mockResolvedValueOnce([project(24), project(25)]);
    const result = await getAllPublishedProjectsForSitemap();
    expect(result.map(item => item.slug)).toEqual(Array.from({ length: 26 }, (_, index) => `project-${index}`));
    expect(projectsPage.mock.calls).toEqual([[0, 24], [24, 24]]);
  });

  it('keeps each project once when adjacent pages overlap', async () => {
    projectsPage.mockResolvedValueOnce(Array.from({ length: 24 }, (_, index) => project(index)))
      .mockResolvedValueOnce([project(23), project(24)]);
    const result = await getAllPublishedProjectsForSitemap();
    expect(result).toHaveLength(25);
    expect(result.filter(item => item.id === 'project-23')).toHaveLength(1);
  });

  it('rejects a full repeated project page instead of looping', async () => {
    projectsPage.mockResolvedValue(Array.from({ length: 24 }, (_, index) => project(index)));
    await expect(getAllPublishedProjectsForSitemap()).rejects.toThrow('did not advance');
    expect(projectsPage).toHaveBeenCalledTimes(2);
  });

  it('follows the published-at and ID cursor and includes the second blog page', async () => {
    blogPage.mockResolvedValueOnce({ items: [post(3), post(2)], hasMore: true })
      .mockResolvedValueOnce({ items: [post(1)], hasMore: false });
    const result = await getAllBlogPostsForSitemap();
    expect(result.map(item => item.slug)).toEqual(['post-3', 'post-2', 'post-1']);
    expect(blogPage.mock.calls).toEqual([[undefined], [{ beforePublishedAt: post(2).publishedAt, beforeId: post(2).id }]]);
  });

  it('returns an empty list when no public blog index is available', async () => {
    blogPage.mockResolvedValue(null);
    await expect(getAllBlogPostsForSitemap()).resolves.toEqual([]);
    expect(blogPage).toHaveBeenCalledTimes(1);
  });

  it('rejects a repeated blog cursor instead of looping or silently truncating', async () => {
    blogPage.mockResolvedValue({ items: [post(1)], hasMore: true });
    await expect(getAllBlogPostsForSitemap()).rejects.toThrow('did not advance');
    expect(blogPage).toHaveBeenCalledTimes(2);
  });

  it.each([
    { items: [], hasMore: true },
    { items: [{ ...post(1), id: undefined }], hasMore: true },
    { items: [{ ...post(1), publishedAt: 'invalid-date' }], hasMore: true },
  ])('rejects an unavailable continuation cursor %#', async page => {
    blogPage.mockResolvedValue(page);
    await expect(getAllBlogPostsForSitemap()).rejects.toThrow('continuation cursor');
    expect(blogPage).toHaveBeenCalledTimes(1);
  });

  it('propagates a later provider failure to the existing sitemap fallback', async () => {
    blogPage.mockResolvedValueOnce({ items: [post(2)], hasMore: true }).mockRejectedValueOnce(new Error('provider unavailable'));
    await expect(getAllBlogPostsForSitemap()).rejects.toThrow('provider unavailable');
    expect(blogPage).toHaveBeenCalledTimes(2);
  });
});
