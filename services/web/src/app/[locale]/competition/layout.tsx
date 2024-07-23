'use client';

import ChallengePage from './challenge/page';
import LeaderboardPage from './leaderboard/page';
import MatchesPage from './matches/page';
import PlayPage from './play/page';
import ReplayPage from './replay/page';
import SubmitPage from './replay/page';
import SummaryPage from './summary/page';
import TournamentPage from './tournament/page';

import {
  FileAddOutlined,
  HistoryOutlined,
  OrderedListOutlined,
  PieChartOutlined,
  PlayCircleOutlined,
  UserSwitchOutlined,
} from '@ant-design/icons';
import { FloatButton, Layout, Menu, MenuProps, theme } from 'antd';
import { deleteCookie, getCookie } from 'cookies-next';
import { useRouter } from 'next/navigation';
import React, { useEffect, useState } from 'react';
import { UserDto } from '@game-guild/apiclient';

const { Content, Footer, Sider } = Layout;

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
  Summary = 'summary',
  Leaderboard = 'leaderboard',
  Submit = 'submit',
  Matches = 'matches',
  Play = 'play',
  Replay = 'replay',
  Challenge = 'challenge',
  Tournament = 'tournament',
}

interface MenuItemProps {
  key: MenuKeys;
  icon?: React.ReactNode;
  content?: React.ReactNode;
}

const items: MenuItemProps[] = [
  {
    key: MenuKeys.Summary,
    icon: <PieChartOutlined />,
    content: <SummaryPage />,
  },
  {
    key: MenuKeys.Leaderboard,
    icon: <OrderedListOutlined />,
    content: <LeaderboardPage />,
  },
  { key: MenuKeys.Submit, icon: <FileAddOutlined />, content: <SubmitPage /> },
  {
    key: MenuKeys.Matches,
    icon: <HistoryOutlined />,
    content: <MatchesPage />,
  },
  { key: MenuKeys.Play, icon: <PlayCircleOutlined />, content: <PlayPage /> },
  {
    key: MenuKeys.Replay,
    icon: <PlayCircleOutlined />,
    content: <ReplayPage />,
  },
  {
    key: MenuKeys.Challenge,
    icon: <PlayCircleOutlined />,
    content: <ChallengePage />,
  },
  {
    key: MenuKeys.Tournament,
    icon: <PlayCircleOutlined />,
    content: <TournamentPage />,
  },
];

export default function CompetitionPage({
  children,
}: {
  children: React.ReactNode;
}) {
  const router = useRouter();
  const [collapsed, setCollapsed] = useState(false);
  const {
    token: { colorBgContainer, borderRadiusLG },
  } = theme.useToken();
  const [selectedKey, setSelectedKey] = useState(MenuKeys.Summary);

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
        // router.push('/login');
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
        <Menu theme="dark" defaultSelectedKeys={[selectedKey]} mode="inline">
          {items.map((item) => (
            <Menu.Item
              key={item.key}
              icon={item.icon}
              onClick={() => {
                setSelectedKey(item.key);
                router.push(`/competition/${item.key}`);
              }}
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
            {children}
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
}
