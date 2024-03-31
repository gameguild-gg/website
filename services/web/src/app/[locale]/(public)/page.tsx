"use client";
import { Card, Col, notification, NotificationArgsProps, Row, Typography } from "antd";
import { useRouter } from "next/navigation";
import React from "react";
import Link from "next/link";

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

    <>
      <Link href={"/sign-in"}>SIGN-IN</Link>
      <video className=" absolute max-h-[500px] w-full object-none overflow-hidden z-3" autoPlay loop muted>
        <source src="assets/videos/hexagon-bg.mp4" type="video/mp4" />
      </video>
      <div className="absolute h-[500px] w-full overflow-hidden opacity-70 z-4 bg-[#000000]" />

      <Row
        justify="space-around"
        style={{
          maxWidth: "1440px",
          width: "100%",
          display: "inline-flex",
          alignItems: "center",
          alignContent: "center",
          overflow: "hidden",
          height: "500px"
        }}
      >
        <Col>
          <div>
            <div className="text-6xl">Building Games Together</div>
            <span className="text-lg">All-in-One Game Development community</span>
          </div>
        </Col>
      </Row>

      <Row
        justify="space-between"
        style={{
          maxWidth: "1440px",
          width: "100%",
          display: "inline-flex",
          alignItems: "center",
          alignContent: "center",
          background: "#18181c"//#18181c
        }}
      >
        <Col span={8}>
          <img
            style={{ maxWidth: "720px", width: "100%", margin: "15x" }}
            src="assets/images/header2.jpeg"
          />
        </Col>
        <Col>
          <Card>
            <Typography.Title>Let's talk!</Typography.Title>
            Join our Discord.
          </Card>
        </Col>
        <Col>
          <iframe
            src="https://discord.com/widget?id=956922983727915078&theme=dark"
            width="280px"
            height="480"
            sandbox="allow-popups allow-popups-to-escape-sandbox allow-same-origin allow-scripts"
            style={{ padding: "10px" }}
          ></iframe>
        </Col>
      </Row>

      <Row
        justify="space-between"
        style={{
          maxWidth: "1440px",
          width: "100%",
          display: "inline-flex",
          alignItems: "center",
          alignContent: "center",
          background: "#18181c"
        }}
      >
        <Col>
        </Col>
        <Col>
          <Card>
            <Typography.Title>Build!</Typography.Title>
            The best place to share games and game development content.
          </Card>
        </Col>
        <Col
          style={{
            overflow: "hidden",
            width: "480px",
            height: "480",
            verticalAlign: "middle",
            zIndex: 2
          }}
        >
          <img
            style={{
              maxWidth: "720px",
              width: "100%",
              margin: "15x",
              scale: 1,
              display: "block"
            }}
            src="assets/images/header1.jpeg"
          />
        </Col>
      </Row>


    </>
  );
}

export default Home;
