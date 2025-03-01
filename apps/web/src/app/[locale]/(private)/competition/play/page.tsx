'use client';

import React, { useEffect, useState } from 'react';
import { Chessboard } from 'react-chessboard';
import { Chess, WHITE } from 'chess.js';

import { RobotFilled, UserOutlined } from '@ant-design/icons';
import { Dropdown, MenuProps, message, Space } from 'antd';

import { useRouter } from 'next/navigation';

import { getSession } from 'next-auth/react';
import { CompetitionsApi } from '@game-guild/apiclient';

export default function PlayPage() {
  const router = useRouter();
  const api = new CompetitionsApi({
    basePath: process.env.NEXT_PUBLIC_API_URL,
  });

  const [agentList, setAgentList] = useState<string[]>(['human']);
  // flag for agent list fetched
  const [agentListFetched, setAgentListFetched] = useState<boolean>(false);

  // boolean to check if the move request is being made
  const [requestMoveInProgress, setRequestMoveInProgress] =
    useState<boolean>(false);

  // dropdown menu for the agents
  const [selectedAgentWhite, setSelectedAgentWhite] = useState<string>('human');
  const [selectedAgentBlack, setSelectedAgentBlack] = useState<string>('human');
  const [game, setGame] = useState(new Chess());
  const [fen, setFen] = useState(game.fen());

  const requestMove = async (): Promise<void> => {
    try {
      if (requestMoveInProgress) return;
      setRequestMoveInProgress(true);
      const fen = game.fen();
      const turn = game.turn();
      const username = turn === WHITE ? selectedAgentWhite : selectedAgentBlack;
      const baseURL = process.env.NEXT_PUBLIC_API_URL;
      const headers = new Headers();

      const session = await getSession();
      const response = await api.competitionControllerRequestChessMove(
        {
          fen: fen,
          username: username,
        },
        {
          headers: {
            Authorization: `Bearer ${session?.user?.accessToken}`,
          },
        },
      );

      setRequestMoveInProgress(false);
      if (response.status === 401) {
        message.error('You are not authorized to view this page.');
        setTimeout(() => {
          router.push('/disconnect');
        }, 1000);
        return;
      }

      if (response.status === 500) {
        message.error(
          'Internal server error. Please report this issue to the community.',
        );
        message.error(JSON.stringify(response.body));
        return;
      }

      const move = response.body as string;
      makeAMove(move);
    } catch (e) {
      message.error(JSON.stringify(e));
      router.push('/connect');
      return;
    }
  };

  const getAgentList = async (): Promise<void> => {
    try {
      const session = await getSession();
      console.log('before request');
      const response = await api.competitionControllerListChessAgents({
        headers: {
          Authorization: `Bearer ${session?.user?.accessToken}`,
        },
      });

      if (response.status === 401) {
        message.error('You are not authorized to view this page.');
        setTimeout(() => {
          router.push('/disconnect');
        }, 1000);
        return;
      }

      if (response.status === 500) {
        message.error(
          'Internal server error. Please report this issue to the community.',
        );
        message.error(JSON.stringify(response.body));
        return;
      }

      console.log('agents list');
      console.log(response);

      const data = response.body as string[];

      // add human to the front of the list
      data.unshift('human');
      setAgentList(data);
      setAgentListFetched(true);
      console.log(data);
    } catch (e) {
      message.error(JSON.stringify(e));
      setAgentListFetched(true);
      // router.push('/connect');
      return;
    }
  };

  useEffect(() => {
    if (!agentListFetched) getAgentList();

    if (
      selectedAgentWhite != 'human' &&
      selectedAgentBlack != 'human' &&
      !game.isGameOver()
    ) {
      requestMove();
    }
  });

  const makeAMove = (
    move: { from: string; to: string; promotion?: string } | string,
  ) => {
    try {
      const result = game.move(move);
      if (result === null || game.isGameOver()) message.info('Game over');
      else if (result === null || game.isDraw()) message.info('Game draw');
      else if (result === null) message.error('Illegal move');
      setGame(game);
      setFen(game.fen());
      return result;
    } catch (error: any) {
      message.error(error.message);
      return null;
    }
  };

  const onDrop = (sourceSquare: string, targetSquare: string) => {
    if (
      !(
        (game.turn() === 'w' && selectedAgentWhite == 'human') ||
        (game.turn() === 'b' && selectedAgentBlack == 'human')
      )
    ) {
      message.error('It is not your turn');
      return false;
    }
    if (selectedAgentWhite == 'human' && selectedAgentBlack == 'human') {
      const move = makeAMove({
        from: sourceSquare,
        to: targetSquare,
        promotion: 'q', // always promote to a queen for example simplicity
      });

      // illegal move
      return move !== null;
    }
    if (
      (selectedAgentWhite == 'human' && selectedAgentBlack != 'human') ||
      (selectedAgentWhite != 'human' && selectedAgentBlack == 'human')
    ) {
      const move = makeAMove({
        from: sourceSquare,
        to: targetSquare,
        promotion: 'q', // always promote to a queen for example simplicity
      });
      if (move !== null) requestMove();
      // illegal move
      return move !== null;
    }

    return false;
  };

  // todo: combine both functions below into one
  const handleMenuClickWhite: MenuProps['onClick'] = (e) => {
    const agent = e.key.toString();
    setSelectedAgentWhite(agent);
  };
  const handleMenuClickBlack: MenuProps['onClick'] = (e) => {
    const agent = e.key.toString();
    setSelectedAgentBlack(agent);
    if (agent != 'human' && game.turn() === 'b') requestMove();
  };

  // todo: list all agents
  // todo: allow user to select agent as opponent
  // todo: allow user to select the color
  // todo: when the user make a move, send the move to the server and request a move from the opponent agent
  // todo: render the move from the opponent agent locally and wait
  return (
    <>
      <Space direction="vertical" size={12}>
        <Space>
          White:
          <Dropdown.Button
            type="default"
            menu={{
              items: agentList.map((agent) => {
                return {
                  key: agent,
                  label: agent,
                  icon: agent === 'human' ? <UserOutlined /> : <RobotFilled />,
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
                  icon: agent === 'human' ? <UserOutlined /> : <RobotFilled />,
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
        </Space>
        <div style={{ width: '500px' }}>
          <Chessboard position={fen} onPieceDrop={onDrop} />{' '}
        </div>
        .<p>Current turn: {game.turn() === 'w' ? 'White' : 'Black'}</p>
        <p>Current FEN: {game.fen()}</p>
        <p>In Check: {game.inCheck() ? 'Yes' : 'No'}</p>
        <p>
          Game status:{' '}
          {game.isGameOver()
            ? 'Game over'
            : game.isDraw()
              ? 'Game draw'
              : 'Game in progress'}
        </p>
        <p>
          Game result:{' '}
          {game.isGameOver()
            ? game.isCheckmate()
              ? game.turn() === 'w'
                ? 'Black wins'
                : 'White wins'
              : 'Draw'
            : 'In progress'}
        </p>
      </Space>
    </>
  );
}
