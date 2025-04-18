import { MetadataRoute } from 'next';

export default async function robots(): Promise<MetadataRoute.Robots> {
  const host = '';

  return {
    rules: [
      {
        userAgent: '*',
        disallow: '/',
      },
    ],
    sitemap: `https://${host}/sitemap.xml`,
  };
}
