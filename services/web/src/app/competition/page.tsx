'use client';

import React, { useState } from 'react';
import {
  PieChartOutlined,
  OrderedListOutlined,
  FileAddOutlined,
  PlayCircleOutlined
} from '@ant-design/icons';
import type { MenuProps } from 'antd';
import { Breadcrumb, Layout, Menu, theme } from 'antd';
import Leaderboard from "@/app/competition/Leaderboard";
import SubmitBot from "@/app/competition/SubmitBot";
import PlayGame from "@/app/competition/PlayGame";
import Summary from "@/app/competition/Summary";
import RePlayGame from "@/app/competition/RePlayGame";
import { CookiesProvider, useCookies } from "react-cookie";


const { Header, Content, Footer, Sider } = Layout;

type MenuItem = Required<MenuProps>['items'][number];

function getItem(
  label: React.ReactNode,
  key: React.Key,
  icon?: React.ReactNode,
  children?: MenuItem[],
): MenuItem {
  return {
    key,
    icon,
    children,
    label,
  } as MenuItem;
}

enum MenuKeys {
  Summary = 'Summary',
  Leaderboard = 'Leaderboard',
  Submit = 'Submit',
  Play = 'Play',
  Replay = 'Replay',
}

interface MenuItemProps {
  key: MenuKeys;
  icon?: React.ReactNode;
  content?: React.ReactNode;
}

const items: MenuItemProps[] = [
  {key: MenuKeys.Summary,  icon: <PieChartOutlined />, content: <Summary /> },
  {key: MenuKeys.Leaderboard, icon: <OrderedListOutlined />, content: <Leaderboard />},
  {key: MenuKeys.Submit, icon: <FileAddOutlined />, content: <SubmitBot />},
  {key: MenuKeys.Play, icon: <PlayCircleOutlined />, content: <PlayGame />},
  {key: MenuKeys.Replay, icon: <PlayCircleOutlined />, content: <RePlayGame />},
];

const Competition: React.FC = () => {
  const [cookies, setCookie] = useCookies(["user"]);
  const [collapsed, setCollapsed] = useState(false);
  const {
    token: { colorBgContainer, borderRadiusLG },
  } = theme.useToken();
  const [selectedKeys, setSelectedKeys] = useState([MenuKeys.Summary]);

  return (
    <CookiesProvider>
      
    <Layout style={{ minHeight: '100vh' }}>
      <Sider collapsible collapsed={collapsed} onCollapse={(value) => setCollapsed(value)}>
        <div className="demo-logo-vertical" />
        <Menu theme="dark" defaultSelectedKeys={[MenuKeys.Summary]} mode="inline">
          {items.map((item) => (
            <Menu.Item
              key={item.key}
              icon={item.icon}
              onClick={() => setSelectedKeys([item.key])}
            >
              {item.key.toString()}
            </Menu.Item>
          ))}
        </Menu>
      </Sider>
      <Layout>
        <Content style={{ margin: '0 16px' }}>
          <div
            style={{
              padding: 24,
              minHeight: 360,
              background: colorBgContainer,
              borderRadius: borderRadiusLG,
            }}
          >
            {items.find((item) => item.key === selectedKeys[0])?.content}
          </div>
        </Content>
        <Footer style={{ textAlign: 'center' }}>
          Chess AI competition engine ©{new Date().getFullYear()} Created by GameGuild, for Game AI classes at Champlain College. Feel free to use and contribute to this project.
        </Footer>
      </Layout>
    </Layout>
    </CookiesProvider>
  );
};

export default Competition;