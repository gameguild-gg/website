import React from 'react';
import {Chessboard} from "react-chessboard";
import { Chess } from 'chess.js'

// WATCH A REPLAY OF A GAME
// IT NEED TO RECEIVE THE WHOLE HISTORY OF THE GAME OR RECEIVE MATCH ID AND GET THE HISTORY FROM THE SERVER
// SHOULD HAVE BUTTONS TO GO BACK AND FORTH IN THE GAME
const RePlayGame: React.FC = () => {
  return (
    <Chessboard id="BasicBoard" boardWidth={Math.min(window.innerWidth, window.innerHeight)*0.8} />
  )
}

export default RePlayGame;