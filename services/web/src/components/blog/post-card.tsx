'use client';

import React from 'react';
import { useRouter } from 'next/navigation';
import { PostOrPage, Tag } from '@tryghost/content-api';

type Props = {
  post: PostOrPage;
};

export function PostCard({ post }: Readonly<Props>) {
  const router = useRouter();

  const onClick = () => {
    router.push(`/blog/${post.slug}`);
  };
      
  return (
    <div
      className="p-[10px] align-top overflow-hidden text-white text-left cursor-pointer"
      key={post.id}
      onClick={() => onClick()}
    >
      {post.feature_image && post.title && (
        <img
          src={post.feature_image}
          alt={post.title}
          className="object-cover w-full md:w-[380px] h-[253px]"
        />
      )}

      {post.tags &&
        post.tags.map((tag: Tag) => (
          <span
            className="text-neutral-500 p-1 py-0 mr-1 mt-2 ml-[-4px]"
            key={tag.id}
          >
            {tag.name}
          </span>
        ))}

      <div className="text-2xl">{post.title}</div>
      <br />
      <div>{post.custom_excerpt || post.excerpt}</div>
      {post.published_at && (
        <div className="text-neutral-500 p-1 py-0 ml-[-4px] pt-2">
          {new Date(post.published_at).toLocaleDateString('pt-BR', {
            dateStyle: 'short',
          })}{' '}
          • {post.reading_time} min. de leitura
        </div>
      )}
    </div>
  );
}
