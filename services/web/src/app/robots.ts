import { MetadataRoute } from 'next';

/**
 * Dynamically generates robots.txt content
 * @see https://nextjs.org/docs/app/api-reference/file-conventions/metadata/robots
 */
export default function robots(): MetadataRoute.Robots {
  // Determine host and protocol based on environment
  const host = process.env.NODE_ENV === 'production' ? 'gameguild.gg' : 'localhost:3000';
  const protocol = process.env.NODE_ENV === 'production' ? 'https' : 'http';
  const baseUrl = `${protocol}://${host}`;

  return {
    rules: {
      userAgent: '*',
      allow: '/',
      // Add any paths you want to disallow
      disallow: [
        '/api/',
        '/admin/',
        '/_next/',
        '/dashboard/private/',
      ],
    },
    sitemap: `${baseUrl}/sitemap.xml`,
  };
}