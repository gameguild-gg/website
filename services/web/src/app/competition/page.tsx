'use client';

import React, { useEffect, useState } from 'react';
import {
  PieChartOutlined,
  OrderedListOutlined,
  FileAddOutlined,
  PlayCircleOutlined,
  HistoryOutlined,
  UserSwitchOutlined,
} from '@ant-design/icons';
import { FloatButton, MenuProps } from 'antd';
import { Layout, Menu, theme } from 'antd';
import Leaderboard from '@/app/competition/Leaderboard';
import SubmitBot from '@/app/competition/SubmitBot';
import PlayGame from '@/app/competition/PlayGame';
import Summary from '@/app/competition/Summary';
import RePlayGame from '@/app/competition/RePlayGame';
import MatchesListUI from '@/app/competition/Matches';
import ChallengeABot from '@/app/competition/ChallengeABot';
import { deleteCookie, getCookie } from 'cookies-next';
import { UserDto } from '@/dtos/user/user.dto';
import { router } from 'next/client';
import { useRouter } from 'next/navigation';
import Tournament from '@/app/competition/Tournament';

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
  Matches = 'Matches',
  Play = 'Play',
  Replay = 'Replay',
  Challenge = 'Challenge',
  Tournament = 'Tournament',
}

interface MenuItemProps {
  key: MenuKeys;
  icon?: React.ReactNode;
  content?: React.ReactNode;
}

const items: MenuItemProps[] = [
  { key: MenuKeys.Summary, icon: <PieChartOutlined />, content: <Summary /> },
  {
    key: MenuKeys.Leaderboard,
    icon: <OrderedListOutlined />,
    content: <Leaderboard />,
  },
  { key: MenuKeys.Submit, icon: <FileAddOutlined />, content: <SubmitBot /> },
  {
    key: MenuKeys.Matches,
    icon: <HistoryOutlined />,
    content: <MatchesListUI />,
  },
  { key: MenuKeys.Play, icon: <PlayCircleOutlined />, content: <PlayGame /> },
  {
    key: MenuKeys.Replay,
    icon: <PlayCircleOutlined />,
    content: <RePlayGame />,
  },
  {
    key: MenuKeys.Challenge,
    icon: <PlayCircleOutlined />,
    content: <ChallengeABot />,
  },
  {
    key: MenuKeys.Tournament,
    icon: <PlayCircleOutlined />,
    content: <Tournament />,
  },
];

const Competition: React.FC = () => {
  const router = useRouter();
  const [collapsed, setCollapsed] = useState(false);
  const {
    token: { colorBgContainer, borderRadiusLG },
  } = theme.useToken();
  const [selectedKeys, setSelectedKeys] = useState([MenuKeys.Summary]);

  const [user, setUser] = useState(null as UserDto | null);

  const [accessToken, setAccessToken] = useState('');
  const [refreshToken, setRefreshToken] = useState('');

  const logout = () => {
    deleteCookie('accessToken');
    deleteCookie('refreshToken');
    deleteCookie('user');
    router.push('/login');
  };

  useEffect(() => {
    // get the user from the cookie
    if (!user) {
      const userCookie = getCookie('user');
      if (userCookie) {
        setUser(JSON.parse(userCookie) as UserDto);
        setAccessToken(getCookie('access_token') as string);
        setRefreshToken(getCookie('refresh_token') as string);
      } else {
        router.push('/login');
      }
    }
  }, []);

  return (
    <Layout style={{ minHeight: '100vh' }}>
      <FloatButton
        shape="circle"
        type="primary"
        style={{ bottom: 50, left: 20 }}
        icon={<UserSwitchOutlined />}
        onClick={logout}
      />
      <Sider
        collapsible
        collapsed={collapsed}
        onCollapse={(value) => setCollapsed(value)}
      >
        <div className="demo-logo-vertical" />
        <Menu
          theme="dark"
          defaultSelectedKeys={[MenuKeys.Summary]}
          mode="inline"
        >
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
          Chess AI competition engine ©{new Date().getFullYear()} Created by
          GameGuild, for Game AI classes at Champlain College. Feel free to use
          and contribute to this project.
        </Footer>
      </Layout>
    </Layout>
  );
};

export default Competition;
