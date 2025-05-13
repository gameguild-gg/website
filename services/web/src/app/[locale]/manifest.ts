import { MetadataRoute } from 'next';

export default async function manifest(): Promise<MetadataRoute.Manifest> {
  // Determine host and protocol based on environment
  const host = process.env.NODE_ENV === 'production' ? 'gameguild.gg' : 'localhost:3000';
  const protocol = process.env.NODE_ENV === 'production' ? 'https' : 'http';
  const baseUrl = `${protocol}://${host}`;

  return {
    name: 'Game Guild',
    short_name: 'Game Guild',
    description: 'A community platform for game developers to learn, build, test, and share.',
    start_url: '/',
    display: 'standalone',
    background_color: '#101014',
    theme_color: '#18181c',
    orientation: 'portrait-primary',
    prefer_related_applications: false,
    id: 'gg.gameguild',
    scope: '/',
    categories: ['education', 'development', 'games'],
    icons: [
      {
        src: `${baseUrl}/assets/images/icons/icon-72x72.png`,
        sizes: '72x72',
        type: 'image/png',
        purpose: 'any',
      },
      {
        src: `${baseUrl}/assets/images/icons/icon-96x96.png`,
        sizes: '96x96',
        type: 'image/png',
        purpose: 'any',
      },
      {
        src: `${baseUrl}/assets/images/icons/icon-128x128.png`,
        sizes: '128x128',
        type: 'image/png',
        purpose: 'any',
      },
      {
        src: `${baseUrl}/assets/images/icons/icon-144x144.png`,
        sizes: '144x144',
        type: 'image/png',
        purpose: 'any',
      },
      {
        src: `${baseUrl}/assets/images/icons/icon-152x152.png`,
        sizes: '152x152',
        type: 'image/png',
        purpose: 'any',
      },
      {
        src: `${baseUrl}/assets/images/icons/icon-192x192.png`,
        sizes: '192x192',
        type: 'image/png',
        purpose: 'any',
      },
      {
        src: `${baseUrl}/assets/images/icons/icon-384x384.png`,
        sizes: '384x384',
        type: 'image/png',
        purpose: 'any',
      },
      {
        src: `${baseUrl}/assets/images/icons/icon-512x512.png`,
        sizes: '512x512',
        type: 'image/png',
        purpose: 'any',
      },
    ],
    screenshots: [
      // {
      //   src: `${baseUrl}/assets/images/screenshots/screenshot1.png`,
      //   sizes: '1280x720',
      //   type: 'image/png',
      //   label: 'Game Guild Dashboard'
      // },
      // {
      //   src: `${baseUrl}/assets/images/screenshots/screenshot2.png`,
      //   sizes: '1280x720',
      //   type: 'image/png',
      //   label: 'Game Guild Learning Platform'
      // }
    ],
    shortcuts: [
      {
        name: 'Dashboard',
        short_name: 'Dashboard',
        description: 'View your Game Guild dashboard',
        url: '/dashboard',
        icons: [{ src: `${baseUrl}/assets/images/icons/dashboard-icon.png`, sizes: '96x96' }],
      },
      {
        name: 'Learning',
        short_name: 'Learn',
        description: 'Access Game Guild courses',
        url: '/learn',
        icons: [{ src: `${baseUrl}/assets/images/icons/learn-icon.png`, sizes: '96x96' }],
      },
    ],
  };
}
