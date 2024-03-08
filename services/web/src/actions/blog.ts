'use server';

export const fetchPosts = async () => {
  const admin_domain = process.env.GHOST_ADMIN_DOMAIN;
  const content_api_key = process.env.GHOST_CONTENT_API_KEY;

  const response = await fetch(
    `https://${admin_domain}/ghost/api/content/posts?key=${content_api_key}`,
    {
      headers: {
        'Content-Type': 'application/json',
        'Accept-Version': 'v5.0',
      },
    },
  );

  if (!response.ok) {
    throw new Error('Failed to fetch posts');
  }

  const { posts } = await response.json();

  return posts;
};

export const fetchPost = async (slug: string) => {
  const admin_domain = process.env.GHOST_ADMIN_DOMAIN;
  const content_api_key = process.env.GHOST_CONTENT_API_KEY;

  const response = await fetch(
    `https://${admin_domain}/ghost/api/content/posts/slug/${slug}?key=${content_api_key}`,
    {
      headers: {
        'Content-Type': 'application/json',
        'Accept-Version': 'v5.0',
      },
    },
  );

  if (!response.ok) {
    throw new Error('Failed to fetch post');
  }

  const { posts } = await response.json();

  return posts[0];
};
