import {MetadataRoute} from 'next';

export default async function robots(): Promise<MetadataRoute.Robots> {
  const host = '';

  return {
    rules: [
      {
        userAgent: '*',
        allow: '/',
        disallow: '/dashboard/',
      },
    ],
    sitemap: `https://${host}/sitemap.xml`,
  };
}
