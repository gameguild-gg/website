import React, {useState} from 'react';
import { Chessboard } from "react-chessboard";
import {Chess, Move} from 'chess.js'

import { DownOutlined, UserOutlined } from '@ant-design/icons';
import {Flex, MenuProps} from 'antd';
import { Button, Dropdown, message, Space, Tooltip } from 'antd';
import {ChessboardProps} from "react-chessboard/dist/chessboard/types";


const PlayGame: React.FC = () => {
  // dropdown menu for the agents
  const [selectedAgent, setSelectedAgent] = useState<string>('');
  const [game, setGame] = useState(new Chess());
  const [fen, setFen] = useState(game.fen());

  const makeAMove = (move: {from: string, to: string, promotion?: string}|string) => {
    const result = game.move(move);
    setGame(game);
    console.log(game.ascii());
    setFen(game.fen());
    return result;
  }

  const makeRandomMove = () => {
    const possibleMoves = game.moves();
    if (game.isGameOver() || game.isDraw() || possibleMoves.length === 0)
      return; // exit if the game is over
    const randomIndex = Math.floor(Math.random() * possibleMoves.length);
    makeAMove(possibleMoves[randomIndex]);
  }


  const onDrop = (sourceSquare: string, targetSquare:string) => {
    const move = makeAMove({
      from: sourceSquare,
      to: targetSquare,
      promotion: "q", // always promote to a queen for example simplicity
    });

    // illegal move
    if (move === null) return false;
    setTimeout(makeRandomMove, 1000);
    return true;
  }
  
  const handleButtonClick = (e: React.MouseEvent<HTMLButtonElement>) => {
    message.info('Click on left button.');
    console.log('click left button', e);
  };

  const handleMenuClick: MenuProps['onClick'] = (e) => {
    setSelectedAgent(e.key.toString());
    message.info('Click on menu item.');
    console.log('click', e);
  };

  // todo: build this menu dynamically from the api response
  const items: MenuProps['items'] = [
    {
      label: 'Agent 1',
      key: '1',
      icon: <UserOutlined />,
    },
    {
      label: '2nd agent',
      key: '2',
      icon: <UserOutlined />,
    },
    {
      label: '3rd agent',
      key: '3',
      icon: <UserOutlined />,
    },
    {
      label: '4rd agent',
      key: '4',
      icon: <UserOutlined />,
    },
  ];

  const menuProps = {
    items,
    onClick: handleMenuClick,
  };

  // todo: list all agents
  // todo: allow user to select agent as opponent
  // todo: allow user to select the color
  // todo: when the user make a move, send the move to the server and request a move from the opponent agent
  // todo: render the move from the opponent agent locally and wait
  
  return (
    <Flex gap="middle" vertical={false}>
      <div key="1">
        <Dropdown.Button menu={menuProps} placement="bottom" icon={<UserOutlined />}>
          {selectedAgent ? selectedAgent : 'Select the agent'}
        </Dropdown.Button>
      </div>
      <div key="2">
        <Chessboard id="BasicBoard" boardWidth={Math.min(window.innerWidth, window.innerHeight)*0.8} position={fen} onPieceDrop={onDrop} />
      </div>
    </Flex>
  )
}

export default PlayGame;