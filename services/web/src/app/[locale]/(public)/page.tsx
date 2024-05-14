"use client";
import React from "react";
import { useRouter } from "next/navigation";


function Home() {
  return (
    <div className="text-center block text-[#ffffff] bg-[#101014] items-center content-center overflow-hidden w-full">
      <video className=" absolute max-h-[500px] w-full object-none overflow-hidden -z-3" autoPlay loop muted>
        <source src="assets/videos/hexagon-bg.mp4" type="video/mp4" />
      </video>
      <div className="absolute h-[500px] w-full overflow-hidden opacity-70 z-10 bg-[#000000]" />
        <div className="absolute left-1/2 -translate-x-1/2 justify-around max-w-[1440px] w-full flex h-[500px] overflow-hidden items-center grid grid-cols-1 mx-auto block z-20 ">
          <div>
            <div className="text-6xl z-20">Building Games Together</div>
            <br />
            <span className="text-lg">All-in-One Game Development community</span>
          </div>
      </div>
        
      <div className="h-[500px]">
        {/*espaço vazio para texto e vídeo de fundo coexistirem*/}
      </div>

      <div className="justify-between max-w-[1440px] w-full items-center bg-[#18181c] flex justify-between max-h-[480px] mx-auto" >
        <div>
          <img
            src="assets/images/header2.jpeg"
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
        <div>
        </div>
        <div>
          <div className="bg-neutral-50 text-black rounded-lg p-4 shadow">
              <div className="text-4xl font-semibold">Build!</div>
              The best place to share games and game development content.
          </div>
        </div>
        <div>
          <img
            src="assets/images/header1.jpeg"
            className="max-w-[480px] w-full"
          />
        </div>
      </div>

    </div>
  );
}

export default Home;
