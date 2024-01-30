import React from 'react';
import {Chessboard} from "react-chessboard";
import { Chess } from 'chess.js'

const RePlayGame: React.FC = () => {
  return (
    <Chessboard id="BasicBoard" boardWidth={Math.min(window.innerWidth, window.innerHeight)*0.8} />
  )
}

export default RePlayGame;