import { MetadataRoute } from 'next';

type Props = {
  slug: string;
};

export async function generateSitemaps(): Promise<string[]> {
  return [];
}

export default async function sitemap({
                                        slug,
                                      }: Readonly<Props>): Promise<MetadataRoute.Sitemap> {
  return [];
}
