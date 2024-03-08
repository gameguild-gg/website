import React from 'react';
import { fetchPost } from '@/actions/blog';

type Props = {
  params: {
    slug: string;
  };
};

async function Post({ params: { slug } }: Readonly<Props>) {
  const post = await fetchPost(slug);

  return (
    <>
      <h2>POST</h2>
      <div
        style={{
          textAlign: 'center',
          color: '#fff',
          backgroundColor: '#101014',
          alignItems: 'center',
          alignContent: 'center',
          overflow: 'hidden',
          width: '100%',
        }}
      >
        <div
          style={{
            maxWidth: '1200px',
            width: '100%',
            background: '#18181c',
          }}
          className="mx-auto mh-full"
        >
          {post && (
            <div className="mx-[10px]">
              <br />
              <div className="text-5xl">{post.title}</div>
              <br />
              {post.feature_image && (
                <img
                  src={post.feature_image as string}
                  className="object-cover w-[1200px]"
                  alt={post.feature_image_alt as string}
                />
              )}
              <br />
              <div
                className="text-left"
                dangerouslySetInnerHTML={{ __html: post?.html as string }}
              />
            </div>
          )}
        </div>
      </div>
    </>
  );
}

export default Post;
