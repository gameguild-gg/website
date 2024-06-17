import GhostContentAPI from '@tryghost/content-api';

export const ghost = new GhostContentAPI({
  url: 'https://gameguild.gg',
  key: process.env.GHOST_CONTENT_API_KEY ?? '',
  version: 'v5.0',
});
