'use client';
import MetamaskSignIn from '@/components/web3/metamask';
import { Row, Col,Layout, Flex,  Card, Typography} from 'antd';
import { useRouter } from 'next/navigation';
import {notification, NotificationArgsProps} from "antd";
import React from "react";
import {NotificationProvider} from "@/app/NotificationContext";


function Home() {
  const [api, contextHolder] = notification.useNotification();
  const router = useRouter();

  const openNotification = (description:string, message:string="info", placement: NotificationArgsProps['placement'] = 'topRight') => {
    notification.info({
      message,
      description: description,
      placement,
    });
  };

  const handleLoginGoogle = async () => {
    openNotification('You just found a WiP feature. Help us finish by coding it for us, or you can pay us a beer or more.');
    async function signInWithGoogle() {}

    try {
      await signInWithGoogle();
      // router.push('/dashboard'); // Redirecionar após o login
    } catch (error) {
      console.error('Error logging in with Google:', error);
    }
  };

  const handleLoginGitHub = async () => {
    openNotification('You just found a WiP feature. Help us finish by coding it for us, or you can pay us a beer or more.');
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
        <Layout style={{overflow: 'hidden',  width: '100%', maxWidth: '100%',}}>
          <Layout.Header style={{textAlign: 'center', color: '#fff', height: 64, paddingInline: 48, lineHeight: '60px', padding: '2px', backgroundColor: '#18181c',}}>
            <Row align="middle" justify="space-between" style={{textAlign: 'center', width:'100%', display: 'inline-flex', alignItems: 'center',}}>
              <Col style={{display: 'flex', minWidth: '150px', alignContent: 'center'}}>
                <img style={{width: 152, height: 54, margin:'15x'}} src='assets/images/logo-text.png'/>
                &nbsp;
                <a href='/'><span style={{paddingLeft: '15px', paddingRight: '15px', color:'#ffffff'}}>About</span></a>
                <a href='/'><span style={{paddingLeft: '15px', paddingRight: '15px', color:'#ffffff'}}>Courses</span></a>
                <a href='/'><span style={{paddingLeft: '15px', paddingRight: '15px', color:'#ffffff'}}>Jobs</span></a>
                <a href='/'><span style={{paddingLeft: '15px', paddingRight: '15px', color:'#ffffff'}}>Blog</span></a>
                <a href='/competition'><span style={{paddingLeft: '15px', paddingRight: '15px', color:'#ffffff'}}>Competition</span></a>
              </Col>
              <Col style={{display: 'flex'}}>
                <a href='/login'><span style={{paddingLeft: '15px', paddingRight: '15px', color:'#ffffff', lineHeight: '64px', border: '0px'}}>Login</span></a>
                <a href='/login'><span style={{padding: '15px', paddingTop: '10px', paddingBottom: '10px', color:'#000000', borderRadius:'30px', backgroundColor:'#ffffff'}}>Register</span></a>
                <img width={25} src='assets/images/language.svg' style={{margin: '5px'}}/>
              </Col>
            </Row>
          </Layout.Header>
          <Layout.Content style={{textAlign: 'center', lineHeight: '120px', color: '#fff', backgroundColor: '#101014', alignItems: 'center', alignContent: 'center'}}>
              <Row justify="space-around" style={{maxWidth: '1440px', width: '100%', display: 'inline-flex', alignItems: 'center', alignContent: 'center', overflow: 'hidden'}}>
                <Col>
                  <Card>
                    <Typography.Title>Crafting Games Together</Typography.Title>
                    All-in-One Game Development community
                  </Card>

                </Col>
                <Col style={{overflow: 'hidden', width: '480px', height:'480', verticalAlign:'middle'}}>
                  <img style={{maxWidth: '720px', width:'100%', margin:'15x', scale: 1, display: 'block'}} src='assets/images/header1.jpeg'/>
                </Col>
              </Row>
              <Row justify="space-around" style={{maxWidth: '1440px', width: '100%', display: 'inline-flex', alignItems: 'center', alignContent: 'center', background:'#18181c  '}}>
                <Col span={8}><img style={{maxWidth: '720px', width:'100%', margin:'15x' }} src='assets/images/header2.jpeg'/></Col>
                <Col>
                  <Card>
                  <Typography.Title>Learn!</Typography.Title>
                    Take and create courses
                  </Card>
                </Col>
              </Row>
            
          </Layout.Content>
          <Layout.Footer style={{textAlign: 'center', color: '#fff', backgroundColor: '#18181c',}}>
          <Flex justify='center' style={{width: "100%"}} align='middle'>
              <Row justify="space-between" style={{maxWidth: '1440px', width: '100%', display: 'inline-flex', alignItems: 'center', alignContent: 'center'}}>
                <Col style={{display:'inline-flex'}}><img style={{width: 30, height: 30, margin:'20x'}} src='assets/images/whatsapp-icon.svg'/><img style={{width: 30, height: 30, margin:'20x'}} src='assets/images/discord-icon.svg'/></Col>
                <Col>Game Guild © 2024 All Rights Reserved</Col>
                <Col>Privacy Policy | Terms of Service</Col>
              </Row>
            </Flex>
          </Layout.Footer>
        </Layout>
      </Flex>
    </main>
  );
}

export default Home;
