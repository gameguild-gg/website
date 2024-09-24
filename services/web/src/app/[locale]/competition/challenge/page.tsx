'use client';

import React, { useEffect, useState } from 'react';
import { getCookie } from 'cookies-next';
import { Button, Dropdown, MenuProps, message, Space, Typography } from 'antd';
import { RobotFilled } from '@ant-design/icons';
import { useRouter } from 'next/navigation';
import { getSession } from 'next-auth/react';
import { Api, CompetitionsApi } from '@game-guild/apiclient';
import ChessMatchResultDto = Api.ChessMatchResultDto;
import UserEntity = Api.UserEntity;

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

  const api = new CompetitionsApi({
    basePath: process.env.NEXT_PUBLIC_API_URL,
  });

  async function getAgentList(): Promise<void> {
    const session = await getSession();

    try {
      const response = await api.competitionControllerListChessAgents({
        headers: {
          Authorization: `Bearer ${session?.accessToken}`,
        },
      });

      const data = response as string[];

      setAgentList(data);
      setAgentListFetched(true);
      console.log(data);
    } catch (e) {
      router.push('/connect');
      return;
    }
  }

  const [selectedAgentWhite, setSelectedAgentWhite] = useState<string>('');
  const [selectedAgentBlack, setSelectedAgentBlack] = useState<string>('');

  const handleMenuClickWhite: MenuProps['onClick'] = (e) => {
    const agent = e.key.toString();
    setSelectedAgentWhite(agent);
  };
  const handleMenuClickBlack: MenuProps['onClick'] = (e) => {
    const agent = e.key.toString();
    setSelectedAgentBlack(agent);
  };

  const challengeBot = async (): Promise<void> => {
    const session = await getSession();
    if (requestInProgress) {
      message.error('Request in progress. Please wait.');
      return;
    }
    if (selectedAgentWhite === '' || selectedAgentBlack === '') {
      message.error('Please select agents to challenge.');
      return;
    }
    // todo: get user from session
    const userAgent = (JSON.parse(getCookie('user') as string) as UserEntity)
      .username;
    if (selectedAgentWhite !== userAgent && selectedAgentBlack !== userAgent) {
      message.error('You must be one of the players.');
      return;
    }
    setRequestInProgress(true);

    const response = await api.competitionControllerRunChessMatch(
      {
        player1username: selectedAgentWhite,
        player2username: selectedAgentBlack,
      },
      {
        headers: {
          Authorization: `Bearer ${session?.accessToken}`,
        },
      },
    );

    console.log(response);
    setResult(response);
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
                <Typography.Text key={move}>{move} </Typography.Text>
              ))}
            </Typography.Paragraph>
          </>
        ) : null}
      </Space>
    </>
  );
};

export default ChallengePage;
