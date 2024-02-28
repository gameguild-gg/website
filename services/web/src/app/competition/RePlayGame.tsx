import React, { useEffect } from 'react';
import { Chessboard } from 'react-chessboard';
import { Chess } from 'chess.js';
import { getCookie } from 'cookies-next';
import { useRouter } from 'next/navigation';
import { ChessMatchResultDto } from '@game-guild/common/src/competition/chess-match-result.dto';

// WATCH A REPLAY OF A GAME
// IT NEED TO RECEIVE THE WHOLE HISTORY OF THE GAME OR RECEIVE MATCH ID AND GET THE HISTORY FROM THE SERVER
// SHOULD HAVE BUTTONS TO GO BACK AND FORTH IN THE GAME
const RePlayGame: React.FC = () => {
  const queryParameters = new URLSearchParams(window.location.search);
  const matchId = queryParameters.get('matchId');

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
  }

  useEffect(() => {
    if (!matchFetched) {
      getMatchData();
    }
  });

  return (
    <>
      <p>
        {' '}
        Todo: create ways to go back and forth in the game using the moves
        array.{' '}
      </p>
      <p>Match ID: {matchId}</p>
      <p>
        {matchData && matchData.result}
        <br />
        {matchData && matchData.players.map((player) => player + ' ')}
        <br />
        {matchData && matchData.winner}
        <br />
        {matchData && matchData.moves.map((move) => move + ' ')}
        <br />
        {matchData && matchData.finalFen}
      </p>
    </>
  );
};

export default RePlayGame;
