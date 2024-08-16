'use client';
import React from 'react';
import { useRouter } from 'next/navigation';

function Home() {
  return (
    <div className="text-center block text-[#ffffff] bg-[#101014] items-center content-center overflow-hidden w-full">
      <video
        className="absolute h-[600px] w-full object-none overflow-hidden"
        autoPlay
        loop
        muted
      >
        <source src="assets/videos/hexagon-bg.mp4" type="video/mp4" />
      </video>
      <div className="absolute w-full lg:flex h-[400px] overflow-hidden items-center grid grid-cols-1 mx-auto block z-10">
        
        <div className='z-10 text-black mx-auto'>
          <div className="text-7xl font-semibold  text-shadow-lg shadow-white">Let's Build Dreams Together!</div>
          <br />
          <span className="text-2xl">The All-in-One Game Dev Toolkit</span>
        </div>
      </div>

      <div className="h-[500px]">
        {/*espaço vazio para texto e vídeo de fundo coexistirem*/}
      </div>

      <div className="relative justify-between max-w-[1440px] w-full items-center bg-[#18181c] lg:flex mx-auto z-30">
        <div>
          <img
            src="/assets/images/header2.jpeg"
            className="w-full lg:max-w-[480px] w-full"
          />
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
          <img
            src="/assets/images/header3.jpeg"
            className="w-full lg:max-w-[480px] w-full"
          />
        </div>
      </div>

      <div className="h-10"></div>

      <div className="justify-between max-w-[1440px] w-full mx-auto lg:flex items-center content-center bg-[#18181c]">
        <div>
          <img
            src="/assets/images/header1.jpeg"
            className="w-full lg:max-w-[480px] w-full"
          />
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

      <div className="relative justify-between max-w-[1440px] w-full items-center bg-[#18181c] lg:flex justify-between mx-auto z-30">
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
