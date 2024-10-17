'use client';

import { Chess } from 'chess.js';
import { useRouter } from 'next/navigation';
import React, { useEffect } from 'react';
import { Button, message, Space, Typography } from 'antd';
import { Chessboard } from 'react-chessboard';
import { getSession } from 'next-auth/react';
import { Api, CompetitionsApi } from '@game-guild/apiclient';
import ChessMatchResultDto = Api.ChessMatchResultDto;

export default function ReplayPage() {
  const api = new CompetitionsApi({
    basePath: process.env.NEXT_PUBLIC_API_URL,
  });
  const [states, setStates] = React.useState<string[]>([]);
  const [currentStateId, setCurrentStateId] = React.useState<number>(0);

  const chess: Chess = new Chess();

  const router = useRouter();

  const [matchData, setMatchData] = React.useState<ChessMatchResultDto>();

  const [matchFetched, setMatchFetched] = React.useState(false);

  async function getMatchData() {
    const queryParameters = new URLSearchParams(window.location.search);
    const matchId = queryParameters.get('matchId');
    message.info('Match ID: ' + matchId);
    if (!matchId) {
      router.push('/competition/matches');
      return;
    }
    const session = await getSession();

    const response = await api.competitionControllerGetChessMatchResult(
      matchId,
      { headers: { Authorization: `Bearer ${session?.user?.accessToken}` } },
    );

    if (response.status === 401) {
      message.error('You are not authorized to view this page.');
      setTimeout(() => {
        router.push('/connect');
      }, 1000);
      setMatchFetched(true);
      return;
    }

    if (response.status === 500) {
      message.error(
        'Internal server error. Please report this issue to the community.',
      );
      message.error(JSON.stringify(response.body));
      setMatchFetched(true);
      return;
    }

    const data = response.body as ChessMatchResultDto;
    console.log(data);
    setMatchData(data);
    setMatchFetched(true);

    // Load the moves
    const list: string[] = [];
    list.push('rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1');
    data?.moves.forEach((move) => {
      chess.move(move);
      list.push(chess.fen());
    });
    setStates(list);
    message.info('Match data fetched');
  }

  useEffect(() => {
    if (!matchFetched) {
      getMatchData();
    }
  });

  return (
    <Space direction={'vertical'}>
      <Typography.Title level={1}>Replay Game</Typography.Title>
      <Typography.Paragraph>
        {matchData?.players[1] + ' vs ' + matchData?.players[0]}
      </Typography.Paragraph>
      <Chessboard
        position={states[currentStateId]}
        boardWidth={400}
        arePiecesDraggable={false}
      />
      <Space direction={'horizontal'}>
        <Button
          onClick={() => {
            if (currentStateId > 0) {
              setCurrentStateId(currentStateId - 1);
            }
          }}
        >
          Previous
        </Button>
        <Button
          onClick={() => {
            if (currentStateId < states.length - 1) {
              setCurrentStateId(currentStateId + 1);
            }
          }}
        >
          Next
        </Button>
      </Space>
    </Space>
  );
}
