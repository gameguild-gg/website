'use client';
import MetamaskSignIn from '@/components/web3/metamask';
import { Row, Col, Layout, Flex, Card, Typography } from 'antd';
import { useRouter } from 'next/navigation';
import { notification, NotificationArgsProps } from 'antd';
import React from 'react';
import { NotificationProvider } from '@/app/NotificationContext';
import { UserOutlined } from '@ant-design/icons';

function Home() {
  const [api, contextHolder] = notification.useNotification();
  const router = useRouter();

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

  const handleLoginGoogle = async () => {
    openNotification(
      'You just found a WiP feature. Help us finish by coding it for us, or you can pay us a beer or more.',
    );
    async function signInWithGoogle() {}

    try {
      await signInWithGoogle();
      // router.push('/dashboard'); // Redirecionar após o login
    } catch (error) {
      console.error('Error logging in with Google:', error);
    }
  };

  const handleLoginGitHub = async () => {
    openNotification(
      'You just found a WiP feature. Help us finish by coding it for us, or you can pay us a beer or more.',
    );
    async function signInWithGitHub() {}

    try {
      await signInWithGitHub();

      // await router.push('/dashboard'); // Redirecionar após o login
    } catch (error) {
      console.error('Error logging in with GitHub:', error);
    }
  };

  return (
    <main>
      <Flex gap="middle" wrap="wrap">
        <Layout style={{ overflow: 'hidden', width: '100%', maxWidth: '100%' }}>
          <Layout.Header
            style={{
              textAlign: 'center',
              color: '#fff',
              height: 64,
              paddingInline: 48,
              lineHeight: '60px',
              padding: '2px',
              backgroundColor: '#18181c',
            }}
          >
            <Row
              align="middle"
              justify="space-between"
              style={{
                textAlign: 'center',
                width: '100%',
                display: 'inline-flex',
                alignItems: 'center',
              }}
            >
              <Col
                style={{
                  display: 'flex',
                  minWidth: '150px',
                  alignContent: 'center',
                }}
              >
                <img
                  style={{ width: 135, height: 46, margin: "7px" }}
                  src="assets/images/logo-text.png"
                />
                &nbsp;
                {/*<a href="/">*/}
                {/*  <span*/}
                {/*    style={{*/}
                {/*      paddingLeft: '15px',*/}
                {/*      paddingRight: '15px',*/}
                {/*      color: '#ffffff',*/}
                {/*    }}*/}
                {/*  >*/}
                {/*    About*/}
                {/*  </span>*/}
                {/*</a>*/}
                {/*<a href="/">*/}
                {/*  <span*/}
                {/*    style={{*/}
                {/*      paddingLeft: '15px',*/}
                {/*      paddingRight: '15px',*/}
                {/*      color: '#ffffff',*/}
                {/*    }}*/}
                {/*  >*/}
                {/*    Courses*/}
                {/*  </span>*/}
                {/*</a>*/}
                {/*<a href="/">*/}
                {/*  <span*/}
                {/*    style={{*/}
                {/*      paddingLeft: '15px',*/}
                {/*      paddingRight: '15px',*/}
                {/*      color: '#ffffff',*/}
                {/*    }}*/}
                {/*  >*/}
                {/*    Jobs*/}
                {/*  </span>*/}
                {/*</a>*/}
                {/*<a href="/">*/}
                {/*  <span*/}
                {/*    style={{*/}
                {/*      paddingLeft: '15px',*/}
                {/*      paddingRight: '15px',*/}
                {/*      color: '#ffffff',*/}
                {/*    }}*/}
                {/*  >*/}
                {/*    Blog*/}
                {/*  </span>*/}
                {/*</a>*/}
                <a href="/competition">
                  <span
                    style={{
                      paddingLeft: '15px',
                      paddingRight: '15px',
                      color: '#ffffff',
                    }}
                  >
                    Competition
                  </span>
                </a>
              </Col>
              <Col style={{ display: 'flex' }}>
                <a href="/login">
                  <span
                    style={{
                      padding: '15px',
                      paddingTop: '10px',
                      paddingBottom: '10px',
                      color: '#000000',
                      borderRadius: '30px',
                      backgroundColor: '#ffffff',
                    }}
                  >
                    <UserOutlined /> Login
                  </span>
                </a>
                <img
                  width={25}
                  src="assets/images/language.svg"
                  style={{ margin: '7px'}}
                />
              </Col>
            </Row>
          </Layout.Header>
          <Layout.Content
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
          >
            <video className=" absolute max-h-[500px] w-full object-none overflow-hidden z-3" autoPlay loop muted>
              <source src="assets/videos/hexagon-bg.mp4" type="video/mp4" />
            </video>
            <div className="absolute h-[500px] w-full overflow-hidden opacity-70 z-4 bg-[#000000]" />

            <Row
              justify="space-around"
              style={{
                maxWidth: '1440px',
                width: '100%',
                display: 'inline-flex',
                alignItems: 'center',
                alignContent: 'center',
                overflow: 'hidden',
                height: '500px',
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
                maxWidth: '1440px',
                width: '100%',
                display: 'inline-flex',
                alignItems: 'center',
                alignContent: 'center',
                background: '#18181c',//#18181c
              }}
            >
              <Col span={8}>
                <img
                  style={{ maxWidth: '720px', width: '100%', margin: '15x' }}
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
                    style={{ padding: '10px'}}
                  ></iframe>
              </Col>
            </Row>

            <Row
              justify="space-between"
              style={{
                maxWidth: '1440px',
                width: '100%',
                display: 'inline-flex',
                alignItems: 'center',
                alignContent: 'center',
                background: '#18181c',
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
                </Col>
              </Row>

          </Layout.Content>
          <Layout.Footer
            style={{
              textAlign: 'center',
              color: '#fff',
              backgroundColor: '#2a2a2a',
            }}
          >
            <Flex justify="center" style={{ width: '100%' }} align="middle">
              <Row
                justify="space-between"
                style={{
                  maxWidth: '1440px',
                  width: '100%',
                  display: 'inline-flex',
                  alignItems: 'center',
                  alignContent: 'center',
                }}
              >
                <Col style={{ display: 'inline-flex' }}>
                  <a href='#'>
                    <img
                      style={{ width: 30, height: 30, margin: 3 }}
                      src="assets/images/whatsapp-icon.svg"
                    />
                  </a>
                  <a href='#'>
                    <img
                      style={{ width: 30, height: 30, margin: 3 }}
                      src="assets/images/discord-icon.svg"
                    />
                  </a>
                </Col>
                <Col>Game Guild © 2024 All Rights Reserved</Col>
                <Col><a href='#' style={{color: 'white'}}>Privacy Policy</a> | <a href='#' style={{color: 'white'}}>Terms of Service</a></Col>
              </Row>
              
            </Flex>
          </Layout.Footer>
        </Layout>
      </Flex>
    </main>
  );
}

export default Home;
