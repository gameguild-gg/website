import type { MetadataRoute } from 'next';
import { PUBLIC_PROGRAM_PACKAGES } from '@/lib/courses/public-programs';
import { getPublicCourseCatalog } from '@/lib/courses/services/course.service';
import { getPublishedProjects } from '@/lib/projects/public-projects';
import { getBlogIndex } from '@/lib/blogs/queries';
import { buildBlogAbsoluteCanonicalUrl, BLOG_SITE_BASE_URL } from '@/lib/blogs/seo';

const baseUrl = process.env.NEXT_PUBLIC_APP_URL ?? 'https://gameguild.gg';
const staticRoutes = [
  '',
  '/sign-in',
  '/sign-up',
  '/courses',
  '/projects',
  '/community',
  '/testing-lab',
  '/launch-pad',
  '/jobs',
  '/about',
  '/about/roadmap',
  '/about/contributors',
  '/contact',
  '/legal',
  '/terms-of-service',
  '/terms-of-use',
  '/polices/privacy',
  '/polices/cookies',
  '/legal/licenses',
  '/legal/ferpa-waiver',
  '/legal/academic-honesty',
];

// NOTE: This must stay a single sitemap (no `generateSitemaps`). With `generateSitemaps`
// Next.js serves the sitemap at `/sitemap/<id>.xml` and `/sitemap.xml` 404s — which is
// what robots.ts advertises. sitemap.test.ts guards this coupling.
export default async function sitemap(): Promise<MetadataRoute.Sitemap> {
  const now = new Date();
  const [projects, catalog, blogIndex] = await Promise.all([
    getPublishedProjects().catch(() => []),
    getPublicCourseCatalog().catch(() => null),
    getBlogIndex().catch(() => null),
  ]);
  const courseRoutes = catalog?.success
    ? catalog.data
        .map((course) => (typeof course.slug === 'string' && course.slug.length > 0 ? course.slug : null))
        .filter((slug): slug is string => Boolean(slug))
        .map((slug) => ({
          url: `${baseUrl}/courses/${slug}`,
          lastModified: now,
          changeFrequency: 'weekly' as const,
          priority: 0.7,
        }))
    : [];

  return [
    ...staticRoutes.map((route) => ({
      url: `${baseUrl}${route}`,
      lastModified: now,
      changeFrequency: route === '' ? ('daily' as const) : ('monthly' as const),
      priority: route === '' ? 1 : 0.6,
    })),
    ...projects.map((project) => ({
      url: `${baseUrl}/projects/${project.slug}`,
      lastModified: now,
      changeFrequency: 'weekly' as const,
      priority: 0.7,
    })),
    ...PUBLIC_PROGRAM_PACKAGES.map((program) => ({
      url: `${baseUrl}/programs/${program.slug}`,
      lastModified: now,
      changeFrequency: 'weekly' as const,
      priority: 0.7,
    })),
    ...courseRoutes,
    ...(blogIndex?.items ?? [])
      .filter((post) => post.slug && post.primaryAuthorHandle)
      .map((post) => ({
        url: buildBlogAbsoluteCanonicalUrl(BLOG_SITE_BASE_URL, post.primaryAuthorHandle ?? '', post.slug ?? ''),
        lastModified: post.publishedAt ? new Date(post.publishedAt) : now,
        changeFrequency: 'weekly' as const,
        priority: 0.6,
      })),
  ];
}
