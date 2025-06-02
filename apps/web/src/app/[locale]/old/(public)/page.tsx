'use client';
import React, { useEffect } from 'react';
import Image from 'next/image';
import { useSearchParams } from 'next/navigation';
import Cookies from 'js-cookie';

function Home() {
  const searchParams = useSearchParams();
  const cacheBuster = searchParams.get('cb');

  // If we have a cache buster parameter, it means we're coming from the disconnect page
  // Let's make sure we clear any remaining session data
  useEffect(() => {
    if (cacheBuster) {
      // Clear all auth-related cookies
      const cookiesToClear = [
        'authjs.session-token',
        'authjs.csrf-token',
        'authjs.callback-url',
        'authjs.refresh-token',
        'authjs.pkce.code_verifier',
        'authjs.pkce.state',
        'next-auth.session-token',
        'next-auth.csrf-token',
        'next-auth.callback-url',
        '__Secure-next-auth.session-token',
        '__Secure-next-auth.callback-url',
        '__Secure-next-auth.csrf-token',
        '__Host-next-auth.csrf-token',
        'session',
        'token',
        'user',
      ];

      cookiesToClear.forEach((cookieName) => {
        Cookies.remove(cookieName);
        Cookies.remove(cookieName, { path: '/' });
        document.cookie = `${cookieName}=; expires=Thu, 01 Jan 1970 00:00:00 GMT; path=/;`;
      });

      // Clear localStorage and sessionStorage
      localStorage.clear();
      sessionStorage.clear();

      // Remove the cache buster parameter from the URL
      window.history.replaceState({}, document.title, '/');
    }
  }, [cacheBuster]);

  return (
    <div className="text-center block text-[#ffffff] bg-[#101014] items-center content-center overflow-hidden w-full z-5">
      <div className="absolute h-[600px] w-full object-none overflow-hidden -z-5">
        <Image src="/assets/images/background-hexagons-1.jpg" alt="background" layout="fill" objectFit="cover" objectPosition="center" quality={60} />
      </div>

      <div className="absolute w-full lg:flex h-[400px] overflow-hidden items-center grid grid-cols-1 mx-auto z-10">
        <div className="z-10 text-black mx-auto">
          <div className="text-7xl font-semibold  text-shadow-lg shadow-white">Let's Build Dreams Together!</div>
          <br />
          <span className="text-2xl">The All-in-One Game Dev Toolkit</span>
        </div>
      </div>

      <div className="h-[500px]">{/*espaço vazio para texto e vídeo de fundo coexistirem*/}</div>

      <div className="relative justify-between max-w-[1440px] w-full items-center bg-[#18181c] lg:flex mx-auto z-30">
        <div>
          <Image src="/assets/images/header2.jpeg" alt="header2" width={480} height={480} className="w-full" />
        </div>
        <div>
          <div className="text-neutral-50 rounded-lg p-4 shadow">
            <div className="text-5xl font-semibold ">Learn!</div>
            Interactive Game Development Courses
          </div>
        </div>
        <div></div>
      </div>

      <div className="h-10"></div>

      <div className="justify-between max-w-[1440px] w-full mx-auto lg:flex items-center content-center bg-[#18181c]">
        <div></div>
        <div>
          <div className="text-neutral-50 rounded-lg p-4 shadow">
            <div className="text-5xl font-semibold">Build!</div>
            Code Competitions, Jobs and Game Jams
          </div>
        </div>
        <div>
          <Image src="/assets/images/header3.jpeg" alt="header3" width={480} height={480} className="w-full" />
        </div>
      </div>

      <div className="h-10"></div>

      <div className="justify-between max-w-[1440px] w-full mx-auto lg:flex items-center content-center bg-[#18181c]">
        <div>
          <Image src="/assets/images/header1.jpeg" alt="header1" width={480} height={480} className="w-full lg:max-w-[480px]" />
        </div>
        <div>
          <div className="text-neutral-50 rounded-lg p-4 shadow">
            <div className="text-5xl font-semibold">Test!</div>
            Fully Featured Game Testing Lab
          </div>
        </div>

        <div></div>
      </div>

      <div className="h-10"></div>

      <div className="relative justify-between max-w-[1440px] w-full items-center bg-[#18181c] lg:flex mx-auto z-30">
        <div></div>

        <div>
          <div className="text-neutral-50 rounded-lg p-4 shadow">
            <div className="text-5xl font-semibold ">Share!</div>
            Join our Discord Community
          </div>
        </div>

        <div className="items-center w-full lg:w-[480px]">
          <iframe
            src="https://discord.com/widget?id=956922983727915078&theme=dark"
            width="370px"
            height="480"
            sandbox="allow-popups allow-popups-to-escape-sandbox allow-same-origin allow-scripts"
            className="p-[10px] m-auto"
          />
        </div>
      </div>
    </div>
  );
}

export default Home;
