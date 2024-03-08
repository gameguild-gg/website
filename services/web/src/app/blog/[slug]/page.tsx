'use client';
//import MetamaskSignIn from '@/components/web3/metamask';
import { useRouter, useParams } from 'next/navigation';
import { notification, NotificationArgsProps } from 'antd';
import React, { useEffect, useState } from 'react';
import GhostContentAPI, { PostOrPage } from '@tryghost/content-api';
//import { NotificationProvider } from '@/app/NotificationContext';
//import { UserOutlined } from '@ant-design/icons';

function Blog() {
  const [api, contextHolder] = notification.useNotification();
  const [post, setPost] = useState<PostOrPage>();
  const router = useRouter();
  const params = useParams();

  const ghost = new GhostContentAPI({
    url: 'https://gameguild.gg',
    key: process.env.NEXT_PUBLIC_GHOST_CONTENT_API_KEY ?? '',
    version: 'v5.0',
  });

  const openNotification = (
    description: string,
    message: string = 'info',
    placement: NotificationArgsProps['placement'] = 'topRight',
  ) => {
    notification.info({
      message,
      description: description,
      placement,
    });
  };

  useEffect(() => {
    console.log('useEffect()');
    console.log('slug: ', params.slug);
    async function fetchMyAPI() {
      const slug: string = params.slug as string;
      setPost(await ghost.posts.read({ slug }, { formats: ['html'] })); //, {include: 'tags,authors'}
    }
    fetchMyAPI();
    console.log(post);
  }, []);

  return (
    <>
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

export default Blog;
