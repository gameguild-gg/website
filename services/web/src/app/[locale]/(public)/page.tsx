"use client";
import { Card, notification, NotificationArgsProps } from "antd";
import { useRouter } from "next/navigation";
import React from "react";


function Home() {
  const [api, contextHolder] = notification.useNotification();
  const router = useRouter();

  const openNotification = (
    description: string,
    message: string = "info",
    placement: NotificationArgsProps["placement"] = "topRight"
  ) => {
    notification.info({
      message,
      description: description,
      placement
    });
  };

  const handleLoginGoogle = async () => {
    openNotification(
      "You just found a WiP feature. Help us finish by coding it for us, or you can pay us a beer or more."
    );

    async function signInWithGoogle() {
    }

    try {
      await signInWithGoogle();
      // router.push('/dashboard'); // Redirecionar após o login
    } catch (error) {
      console.error("Error logging in with Google:", error);
    }
  };

  const handleLoginGitHub = async () => {
    openNotification(
      "You just found a WiP feature. Help us finish by coding it for us, or you can pay us a beer or more."
    );

    async function signInWithGitHub() {
    }

    try {
      await signInWithGitHub();

      // await router.push('/dashboard'); // Redirecionar após o login
    } catch (error) {
      console.error("Error logging in with GitHub:", error);
    }
  };

  return (

    <div
      style={{
        textAlign: 'center',
        lineHeight: '120px',
        color: '#fff',
        backgroundColor: '#101014',
        alignItems: 'center',
        alignContent: 'center',
        overflow: 'hidden',
        width: '100%'
      }}
      className="text-center block"
    >
      <video className=" absolute max-h-[500px] w-full object-none overflow-hidden -z-3" autoPlay loop muted>
        <source src="assets/videos/hexagon-bg.mp4" type="video/mp4" />
      </video>
      <div className="absolute h-[500px] w-full overflow-hidden opacity-70 z-10 bg-[#000000]" />
        <div className="absolute left-1/2 -translate-x-1/2 justify-around max-w-[1440px] w-full flex h-[500px] overflow-hidden items-center grid grid-cols-1 mx-auto block z-20 ">
            <div>
              <div className="text-6xl z-20">Building Games Together</div>
              <span className="text-lg">All-in-One Game Development community</span>
            </div>
      </div>
        
      <div className="h-[500px]">
        {/*Espaço*/}
      </div>
      <div className="justify-between max-w-[1440px] w-full items-center bg-[#18181c] flex justify-between max-h-[480px] mx-auto" >
        <div>
          <img
            style={{ maxWidth: '720px', width: '100%', margin: '15x' }}
            src="assets/images/header2.jpeg"
          />
        </div>
        <div>
          <Card>
            <div className="text-4xl font-semibold">Let's talk!</div>
            Join our Discord.
          </Card>
        </div>
        <div className="justify-end">
            <iframe
              src="https://discord.com/widget?id=956922983727915078&theme=dark"
              width="280px"
              height="480"
              sandbox="allow-popups allow-popups-to-escape-sandbox allow-same-origin allow-scripts"
              className=" p-[10px]"
            />
        </div>
      </div>

      <div
        style={{
          maxWidth: '1440px',
          width: '100%',
          display: 'inline-flex',
          alignItems: 'center',
          alignContent: 'center',
          background: '#18181c',
        }}
        className="justify-between grid grid-cols-3"
      >
        <div>
        </div>
        <div>
          <Card>
              <div className="text-4xl font-semibold">Build!</div>
              The best place to share games and game development content.
          </Card>
        </div>
        <div
          style={{
            overflow: 'hidden',
            width: '480px',
            height: '480',
            verticalAlign: 'middle',
            zIndex: 2
          }}
        >
          <img
            style={{
              maxWidth: '720px',
              width: '100%',
              margin: '15x',
              scale: 1,
              display: 'block',
            }}
            src="assets/images/header1.jpeg"
          />
        </div>
      </div>

    </div>
  );
}

export default Home;
