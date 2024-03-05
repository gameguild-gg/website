'use client';
import MetamaskSignIn from '@/components/web3/metamask';
import { Row, Col, Layout, Flex, Card, Typography } from 'antd';
import { useRouter } from 'next/navigation';
import { notification, NotificationArgsProps } from 'antd';
import React, { useEffect, useState } from 'react';
import axios from "axios";
// @ts-ignore
import GhostContentAPI from '@tryghost/content-api'
import { NotificationProvider } from '@/app/NotificationContext';
import { UserOutlined } from '@ant-design/icons';


const baseURL = "https://gameguild.gg";
const config = {
  headers: {
    "Content-Type": "application/json",
  },
};

function Blog() {
  const [api, contextHolder] = notification.useNotification();
  const [posts, setPosts] = useState<any[]>([])
  const router = useRouter();

  const ghost = new GhostContentAPI({
    url: 'https://gameguild.gg',
    key: process.env.NEXT_PUBLIC_GHOST_CONTENT_API_KEY,
    version: "v5.0"
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

  useEffect( () => {
    console.log(process.env.NEXT_PUBLIC_GHOST_CONTENT_API_KEY)
    console.log("useEffect()")
    async function fetchMyAPI() {
      setPosts(await ghost.posts.browse({include: 'tags,authors'})) //pega os 15 primeiros por padrão.
    }
    fetchMyAPI()
    console.log(posts)
  },[]);

  return (
    <div>
      <div
        style={{
          textAlign: 'center',
          color: '#fff',
          backgroundColor: '#101014',
          alignItems: 'center',
          alignContent: 'center',
          overflow: 'hidden',
          width: '100%'
        }}
      >
        <Row
          style={{
            maxWidth: '1200px',
            width: '100%',
            background: '#18181c',//#18181c
          }}
          className='grid grid-cols-3 mx-auto'
        >
          {posts && posts.map((post:any) =>

            <div className='p-[10px] align-top overflow-hidden text-white text-left' key={post.id}>
              <img src={post.feature_image} className='object-cover  w-[380px] h-[253px]'/>

              {post.tags && post.tags.map((tag:any)=>
                <span className='text-neutral-500 p-1 py-0 mr-1 mt-2'>{tag.name}</span>
              )}

              <div className='text-2xl'>{post.title}</div>
              <br />
              <div>{post.custom_excerpt || post.excerpt}</div>
            </div>

          )}
          
        </Row>
      </div>
    </div>
  );
}

export default Blog;
