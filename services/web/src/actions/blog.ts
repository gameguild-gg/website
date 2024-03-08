import { ghost } from '@/lib/ghost';
import { cache } from 'react';
export const fetchPost = cache(async (slug: string) => {
  return await ghost.posts.browse({ include: ['tags', 'authors'] });
});
