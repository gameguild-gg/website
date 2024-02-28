import React, { useEffect } from 'react';
import { Chessboard } from 'react-chessboard';
import { getCookie } from 'cookies-next';
import { useRouter } from 'next/navigation';
import { ChessMatchResultDto } from '@game-guild/common/src/competition/chess-match-result.dto';
import { Button, message, Space, Typography } from 'antd';
import { Chess, Move, WHITE } from 'chess.js';

// WATCH A REPLAY OF A GAME
// IT NEED TO RECEIVE THE WHOLE HISTORY OF THE GAME OR RECEIVE MATCH ID AND GET THE HISTORY FROM THE SERVER
// SHOULD HAVE BUTTONS TO GO BACK AND FORTH IN THE GAME
const RePlayGame: React.FC = () => {
  const queryParameters = new URLSearchParams(window.location.search);
  const matchId = queryParameters.get('matchId');
  const [states, setStates] = React.useState<string[]>([]);
  const [currentStateId, setCurrentStateId] = React.useState<number>(0);

  const chess: Chess = new Chess();

  const router = useRouter();

  const [matchData, setMatchData] = React.useState<ChessMatchResultDto>();

  const [matchFetched, setMatchFetched] = React.useState(false);

  async function getMatchData() {
    if (!matchId) {
      router.push('/competition?page=Matches');
      window.location.reload();
      return;
    }
    const baseUrl = process.env.NEXT_PUBLIC_API_URL;
    const headers = new Headers();
    const accessToken = getCookie('access_token');
    headers.append('Authorization', `Bearer ${accessToken}`);
    const response = await fetch(
      baseUrl + '/Competitions/Chess/Match/' + matchId,
      {
        method: 'GET',
        headers: headers,
      },
    );
    if (!response.ok) {
      router.push('/login');
      return;
    }
    const data = (await response.json()) as ChessMatchResultDto;
    console.log(data);
    setMatchData(data);
    setMatchFetched(true);

    // Load the moves
    const list: string[] = [];
    list.push('rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1');
    data.moves.forEach((move) => {
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
};

export default RePlayGame;
