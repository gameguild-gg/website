import { describe, expect, it, vi } from 'vitest';

const BASE_URL = vi.hoisted(() => {
  process.env.NEXT_PUBLIC_APP_URL = 'https://sitemap-test.gameguild.gg';
  return process.env.NEXT_PUBLIC_APP_URL;
});

const getPublishedProjects = vi.hoisted(() => vi.fn());
const getPublicCourseCatalog = vi.hoisted(() => vi.fn());
const getBlogIndex = vi.hoisted(() => vi.fn());

vi.mock('@/lib/projects/public-projects', () => ({
  getPublishedProjects: (...args: unknown[]) => getPublishedProjects(...args),
}));
vi.mock('@/lib/courses/services/course.service', () => ({
  getPublicCourseCatalog: (...args: unknown[]) => getPublicCourseCatalog(...args),
}));
vi.mock('@/lib/blogs/queries', () => ({
  getBlogIndex: (...args: unknown[]) => getBlogIndex(...args),
}));
vi.mock('@/lib/courses/public-programs', () => ({
  PUBLIC_PROGRAM_PACKAGES: [{ slug: 'game-ai-systems' }],
}));

const sitemapModule = await import('./sitemap');
const robotsModule = await import('./robots');

const sitemap = sitemapModule.default;

function urlsOf(entries: Awaited<ReturnType<typeof sitemap>>): string[] {
  return entries.map((entry) => entry.url);
}

function mockHealthySources(): void {
  getPublishedProjects.mockResolvedValue([{ slug: 'proj-one' }]);
  getPublicCourseCatalog.mockResolvedValue({
    success: true,
    data: [{ slug: 'course-one' }, { slug: '' }, {}],
    source: 'api',
  });
  getBlogIndex.mockResolvedValue({
    items: [
      { slug: 'post-one', primaryAuthorHandle: 'alice', publishedAt: '2026-01-15T10:00:00Z' },
      { slug: 'post-no-handle', primaryAuthorHandle: '', publishedAt: '2026-02-01T10:00:00Z' },
      { slug: '', primaryAuthorHandle: 'bob', publishedAt: '2026-02-02T10:00:00Z' },
    ],
    hasMore: false,
  });
}

describe('app sitemap', () => {
  it('assembles static, program, project, course and blog URLs into one sitemap', async () => {
    mockHealthySources();

    const entries = await sitemap();
    const urls = urlsOf(entries);

    // static routes
    expect(urls).toContain(BASE_URL);
    expect(urls).toContain(`${BASE_URL}/sign-in`);
    expect(urls).toContain(`${BASE_URL}/courses`);
    expect(urls).toContain(`${BASE_URL}/legal/licenses`);

    // dynamic routes
    expect(urls).toContain(`${BASE_URL}/programs/game-ai-systems`);
    expect(urls).toContain(`${BASE_URL}/projects/proj-one`);
    expect(urls).toContain(`${BASE_URL}/courses/course-one`);
    expect(urls).toContain(`${BASE_URL}/blogs/alice/post-one`);

    // every entry is absolute on the advertised origin
    for (const url of urls) {
      const onOrigin = url === BASE_URL || url.startsWith(`${BASE_URL}/`);
      expect(onOrigin, `unexpected origin for ${url}`).toBe(true);
    }

    // entries without a slug or primary author handle are dropped
    expect(urls.filter((url) => url.startsWith(`${BASE_URL}/courses/`))).toEqual([
      `${BASE_URL}/courses/course-one`,
    ]);
    expect(urls.filter((url) => url.includes('/blogs/'))).toEqual([`${BASE_URL}/blogs/alice/post-one`]);

    const home = entries.find((entry) => entry.url === BASE_URL);
    expect(home?.changeFrequency).toBe('daily');
    expect(home?.priority).toBe(1);

    const blogPost = entries.find((entry) => entry.url === `${BASE_URL}/blogs/alice/post-one`);
    expect(blogPost?.lastModified).toEqual(new Date('2026-01-15T10:00:00Z'));
  });

  it('omits dynamic entries and still serves the sitemap when every data source rejects', async () => {
    getPublishedProjects.mockRejectedValue(new Error('projects down'));
    getPublicCourseCatalog.mockRejectedValue(new Error('catalog down'));
    getBlogIndex.mockRejectedValue(new Error('blog index down'));

    // must resolve instead of throwing when all dynamic sources fail
    const entries = await sitemap();
    const urls = urlsOf(entries);

    // static routes and build-time program packages remain
    expect(urls).toContain(BASE_URL);
    expect(urls).toContain(`${BASE_URL}/courses`);
    expect(urls).toContain(`${BASE_URL}/programs/game-ai-systems`);

    // failed dynamic sources contribute no entries
    expect(urls.filter((url) => url.startsWith(`${BASE_URL}/courses/`))).toEqual([]);
    expect(urls.filter((url) => url.startsWith(`${BASE_URL}/projects/`))).toEqual([]);
    expect(urls.filter((url) => url.includes('/blogs/'))).toEqual([]);
  });

  it('omits course entries when the catalog service reports a failure result', async () => {
    getPublishedProjects.mockResolvedValue([]);
    getPublicCourseCatalog.mockResolvedValue({ success: false, data: [], error: 'API 503' });
    getBlogIndex.mockResolvedValue(null);

    const urls = urlsOf(await sitemap());

    expect(urls).toContain(`${BASE_URL}/courses`);
    expect(urls.filter((url) => url.startsWith(`${BASE_URL}/courses/`))).toEqual([]);
  });

  it('serves the sitemap at the URL robots.ts advertises', async () => {
    const advertised = robotsModule.default().sitemap;

    // robots.ts advertises the conventional /sitemap.xml
    expect(advertised).toBe(`${BASE_URL}/sitemap.xml`);

    // Next.js only serves /sitemap.xml when the module does NOT export
    // generateSitemaps — exporting it moves the route to /sitemap/0.xml and
    // leaves the advertised URL 404ing (the production defect this guards).
    expect('generateSitemaps' in sitemapModule).toBe(false);
  });
});
