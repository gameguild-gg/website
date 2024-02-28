'use client';

import React, { useEffect, useState } from 'react';
import { getCookie } from 'cookies-next';
import { Button, Dropdown, MenuProps, message, Space, Typography } from 'antd';
import { RobotFilled } from '@ant-design/icons';
import { UserDto } from '@/dtos/user/user.dto';
import { ChessMatchRequestDto } from '@/dtos/competition/chess-match-request.dto';
import { useRouter } from 'next/navigation';
import { ChessMatchResultDto } from '@/dtos/competition/chess-match-result.dto';
export default function ChallengePage() {
  // flag for agent list fetched
  const [agentList, setAgentList] = useState<string[]>([]);

  const router = useRouter();
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
    const userAgent = (JSON.parse(getCookie('user') as string) as UserDto)
      .username;
    setSelectedAgentWhite(agent);
  };
  const handleMenuClickBlack: MenuProps['onClick'] = (e) => {
    const agent = e.key.toString();
    const userAgent = (JSON.parse(getCookie('user') as string) as UserDto)
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
    const userAgent = (JSON.parse(getCookie('user') as string) as UserDto)
      .username;
    if (selectedAgentWhite !== userAgent && selectedAgentBlack !== userAgent) {
      message.error('You must be one of the players.');
      return;
    }
    setRequestInProgress(true);
    const baseUrl = process.env.NEXT_PUBLIC_API_URL;
    const headers = new Headers();
    const accessToken = getCookie('access_token');
    headers.append('Authorization', `Bearer ${accessToken}`);
    headers.append('Content-Type', 'application/json');
    const body: ChessMatchRequestDto = {
      player1username: selectedAgentWhite,
      player2username: selectedAgentBlack,
    };
    const response = await fetch(baseUrl + '/Competitions/Chess/RunMatch', {
      method: 'POST',
      headers: headers,
      body: JSON.stringify(body),
    });
    const data = (await response.json()) as ChessMatchResultDto;
    console.log(data);
    setResult(data);
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
}
