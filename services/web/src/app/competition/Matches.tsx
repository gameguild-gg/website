import React from 'react';
import { Chessboard } from 'react-chessboard';
import { Chess } from 'chess.js';

// LEST MATCHES
// ALLOW USER TO FILTER MATCHES BY DATE, BY PLAYER, BY TOURNAMENT
// IF THE USER CLICKS ON A MATCH, IT WILL REDIRECT TO THE REPLAY PAGE WITH THE CORRECT PARAMETERS TO RENDER
const MatchesListUI: React.FC = () => {
  return (
    <Chessboard
      id="BasicBoard"
      boardWidth={Math.min(window.innerWidth, window.innerHeight) * 0.8}
    />
  );
};

export default MatchesListUI;