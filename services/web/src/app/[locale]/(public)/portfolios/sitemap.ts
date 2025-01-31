import { MetadataRoute } from 'next';

type Props = {
  slug: string;
};

export async function generateSitemaps(): Promise<string[]> {
  return [];
}

export async function sitemap({
                                slug,
                              }: Readonly<Props>): Promise<MetadataRoute.Sitemap> {
  return [];
}

export default sitemap;
