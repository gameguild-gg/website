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
    <div className='text-center items-center overflow-hidden w-full text-white bg-[#18181c]'>
      <div className="w-full max-w-[1200px] mx-auto mh-full">
        
        {post && (
          <div>
            <br />
            <div className="text-5xl">{post.title}</div>
            <br />
            {post.feature_image && (
              <img
                src={post.feature_image as string}
                alt={post.feature_image_alt as string}
                className="object-cover w-full h-[675px]"
              />
            )}
            <br />
            <div              
              dangerouslySetInnerHTML={{ __html: post?.html as string }}
              className="text-left max-w-[720px] mx-auto [&_img]:mx-auto [&_pre]:border
              [&_pre]:my-3 [&_iframe]:w-full [&_iframe]:h-[404px] [&_figure]:mx-auto
              [&_figure]:my-3 [&_figure]:text-center [&_h3]:text-2xl [&_h3]:my-3
              [&_h2]:text-4xl [&_h2]:my-3 [&_blockquote]:border [&_p]:my-3
              [&_ul]:list-disc [&_ul]:m-2"
            />
          </div>
        )}

      </div>
    </div>
  );
}

export default Post;
