import type { MetadataRoute } from 'next';

export default async function manifest(): Promise<MetadataRoute.Manifest> {
  return {
    name: 'GameGuild',
    short_name: 'GameGuild',
    description: 'Game development learning and community platform',
    start_url: '/',
    display: 'standalone',
    background_color: '#ffffff',
    theme_color: '#0f172a',
    icons: [
      {
        src: '/favicon.svg',
        sizes: 'any',
        type: 'image/svg+xml',
      },
    ],
  };
}
