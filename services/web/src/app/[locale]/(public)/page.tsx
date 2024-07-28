'use client';
import React from 'react';
import { useRouter } from 'next/navigation';

function Home() {
  return (
    <div className="text-center block text-[#ffffff] bg-[#101014] items-center content-center overflow-hidden w-full">
      <video
        className=" absolute max-h-[600px] w-full object-none overflow-hidden"
        autoPlay
        loop
        muted
      >
        <source src="assets/videos/hexagon-bg.mp4" type="video/mp4" />
      </video>
      <div className="absolute w-full flex h-[400px] overflow-hidden items-center grid grid-cols-1 mx-auto block z-10">
        
        <div className='z-10 text-black'>
          <div className="text-7xl font-semibold">Let's Build Dreams Together!</div>
          <br />
          <span className="text-2xl">The All-in-One Game Dev Toolkit</span>
        </div>
      </div>

      <div className="h-[500px]">
        {/*espaço vazio para texto e vídeo de fundo coexistirem*/}
      </div>

      <div className="relative justify-between max-w-[1440px] w-full items-center bg-[#18181c] flex justify-between max-h-[480px] mx-auto z-30">
        <div>
          <img
            src="/assets/images/header2.jpeg"
            className="max-w-[480px] w-full"
          />
        </div>
        <div>
          <div className="bg-neutral-50 text-black rounded-lg p-4 shadow">
            <div className="text-4xl font-semibold">Let's talk!</div>
            Join our Discord.
          </div>
        </div>
        <div className="justify-end">
          <iframe
            src="https://discord.com/widget?id=956922983727915078&theme=dark"
            width="280px"
            height="480"
            sandbox="allow-popups allow-popups-to-escape-sandbox allow-same-origin allow-scripts"
            className="p-[10px]"
          />
        </div>
      </div>

      <div className="justify-between max-w-[1440px] w-full inline-flex items-center content-center bg-[#18181c]">
        <div></div>
        <div>
          <div className="bg-neutral-50 text-black rounded-lg p-4 shadow">
            <div className="text-4xl font-semibold">Build!</div>
            The best place to share games and game development content.
          </div>
        </div>
        <div>
          <img
            src="/assets/images/header1.jpeg"
            className="max-w-[480px] w-full"
          />
        </div>
      </div>
    </div>
  );
}

export default Home;
