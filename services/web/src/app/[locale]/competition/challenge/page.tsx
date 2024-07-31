'use client';

import React, { useEffect, useState } from 'react';
import { getCookie } from 'cookies-next';
import { Button, Dropdown, MenuProps, message, Space, Typography } from 'antd';
import { RobotFilled, UserOutlined } from '@ant-design/icons';
import { useRouter } from 'next/navigation';
import { competitionsApi } from '@/lib/apinest';
import { ChessMatchResultDto, UserEntity } from '@game-guild/apiclient';

const ChallengePage: React.FC = () => {
  const router = useRouter();

  const [agentList, setAgentList] = useState<string[]>([]);
  // flag for agent list fetched
  const [agentListFetched, setAgentListFetched] = useState<boolean>(false);

  const [requestInProgress, setRequestInProgress] = useState<boolean>(false);

  const [result, setResult] = useState<ChessMatchResultDto>();

  useEffect(() => {
    if (!agentListFetched) getAgentList();
  });

  const getAgentList = async () => {
    const baseUrl = process.env.NEXT_PUBLIC_API_URL;
    const headers = new Headers();
    const accessToken = getCookie('access_token');
    headers.append('Authorization', `Bearer ${accessToken}`);
    const response = await fetch(baseUrl + '/Competitions/Chess/ListAgents', {
      headers: headers,
    });
    if (!response.ok) {
      router.push('/login');
      return;
    }
    const data = (await response.json()) as string[];

    setAgentList(data);
    setAgentListFetched(true);
    console.log(data);
  };

  const [selectedAgentWhite, setSelectedAgentWhite] = useState<string>('');
  const [selectedAgentBlack, setSelectedAgentBlack] = useState<string>('');

  const handleMenuClickWhite: MenuProps['onClick'] = (e) => {
    const agent = e.key.toString();
    // todo: fix the requirement to play chess to have a username
    // todo: do not use cookies anymor
    const userAgent = (JSON.parse(getCookie('user') as string) as UserEntity)
      .username;
    setSelectedAgentWhite(agent);
  };
  const handleMenuClickBlack: MenuProps['onClick'] = (e) => {
    const agent = e.key.toString();
    const userAgent = (JSON.parse(getCookie('user') as string) as UserEntity)
      .username;
    setSelectedAgentBlack(agent);
  };

  const challengeBot = async () => {
    if (requestInProgress) {
      message.error('Request in progress. Please wait.');
      return;
    }
    if (selectedAgentWhite === '' || selectedAgentBlack === '') {
      message.error('Please select agents to challenge.');
      return;
    }
    const userAgent = (JSON.parse(getCookie('user') as string) as UserEntity)
      .username;
    if (selectedAgentWhite !== userAgent && selectedAgentBlack !== userAgent) {
      message.error('You must be one of the players.');
      return;
    }
    setRequestInProgress(true);

    const response = await competitionsApi.competitionControllerRunChessMatch(
      {
        player1username: selectedAgentWhite,
        player2username: selectedAgentBlack,
      },
      {
        headers: {
          Authorization: `Bearer ${getCookie('access_token')}`,
          'Content-Type': 'application/json',
        },
      },
    );

    console.log(response.data);
    setResult(response.data);
    setRequestInProgress(false);
  };

  return (
    <>
      <Typography.Title level={1}>Challenge a Bot</Typography.Title>
      <Space direction={'vertical'} size={12}>
        <Space>
          White:
          <Dropdown.Button
            type="default"
            menu={{
              items: agentList.map((agent) => {
                return {
                  key: agent,
                  label: agent,
                  icon: <RobotFilled />,
                };
              }),
              onClick: handleMenuClickWhite,
            }}
            placement="topLeft"
            arrow
            style={{ borderColor: 'black', color: 'black' }}
          >
            {selectedAgentWhite ? selectedAgentWhite : 'White'}
          </Dropdown.Button>
          Black:
          <Dropdown.Button
            type="default"
            menu={{
              items: agentList.map((agent) => {
                return {
                  key: agent,
                  label: agent,
                  icon: <RobotFilled />,
                };
              }),
              onClick: handleMenuClickBlack,
            }}
            placement="topLeft"
            arrow
            style={{ borderColor: 'red', color: 'red' }}
          >
            {selectedAgentBlack ? selectedAgentBlack : 'Black'}
          </Dropdown.Button>
          <Button onClick={challengeBot}>Challenge</Button>
        </Space>
        {requestInProgress ? (
          <Typography.Text type="warning">
            Request in progress. Please wait.
          </Typography.Text>
        ) : null}
        {result ? (
          <>
            <Typography.Paragraph>Match ID: {result.id}</Typography.Paragraph>
            {result.winner ? (
              <Typography.Paragraph>
                Winner: {result.winner}
              </Typography.Paragraph>
            ) : null}
            {result.draw ? (
              <Typography.Paragraph>Draw</Typography.Paragraph>
            ) : null}
            <Typography.Paragraph>
              White: {result.players[0]}
            </Typography.Paragraph>
            <Typography.Paragraph>
              Black: {result.players[1]}
            </Typography.Paragraph>
            <Typography.Paragraph>
              Last State: {result.finalFen}
            </Typography.Paragraph>
            <Typography.Paragraph>
              Moves:{' '}
              {result.moves.map((move) => (
                <Typography.Text>{move} </Typography.Text>
              ))}
            </Typography.Paragraph>
          </>
        ) : null}
      </Space>
    </>
  );
};

export default ChallengePage;
