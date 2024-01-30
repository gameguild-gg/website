import React, {useState} from 'react';
import { Chessboard } from "react-chessboard";
import {Chess, Move} from 'chess.js'

import { DownOutlined, UserOutlined } from '@ant-design/icons';
import {Flex, MenuProps} from 'antd';
import { Button, Dropdown, message, Space, Tooltip } from 'antd';
import {ChessboardProps} from "react-chessboard/dist/chessboard/types";


const PlayGame: React.FC = () => {
  // dropdown menu for the agents
  const [selectedAgentWhite, setSelectedAgentWhite] = useState<string>('human');
  const [selectedAgentBlack, setSelectedAgentBlack] = useState<string>('human');
  const [game, setGame] = useState(new Chess());
  const [fen, setFen] = useState(game.fen());

  const makeAMove = (move: {from: string, to: string, promotion?: string}|string) => {
    try {
      const result = game.move(move);
      if(result === null && game.isGameOver())
        message.info('Game over');
      else if(result === null && game.isDraw())
        message.info('Game draw');
      else if(result === null)
        message.error('Illegal move');
      setGame(game);
      setFen(game.fen());
      return result;
    } catch (error: any) {
      message.error(error.message);
      return null;
    }
  }

  const makeRandomMove = () => {
    const possibleMoves = game.moves();
    if (game.isGameOver() || game.isDraw() || possibleMoves.length === 0)
      return; // exit if the game is over
    const randomIndex = Math.floor(Math.random() * possibleMoves.length);
    makeAMove(possibleMoves[randomIndex]);
  }


  const onDrop = (sourceSquare: string, targetSquare:string) => {
    
    if(!((game.turn() === 'w' && selectedAgentWhite == 'human') || (game.turn() === 'b' && selectedAgentBlack == 'human')))
    {
      message.error('It is not your turn');
      return false;
    }
    if(selectedAgentWhite == 'human' && selectedAgentBlack == 'human'){
      const move = makeAMove({
        from: sourceSquare,
        to: targetSquare,
        promotion: "q", // always promote to a queen for example simplicity
      });

      // illegal move
      return move !== null;
    }
    if((selectedAgentWhite == 'human' && selectedAgentBlack != 'human') || (selectedAgentWhite != 'human' && selectedAgentBlack == 'human')){
      const move = makeAMove({
        from: sourceSquare,
        to: targetSquare,
        promotion: "q", // always promote to a queen for example simplicity
      });
      if(move !== null){
        makeRandomMove();
      }
      // illegal move
      return move !== null;
    }
    
    
    return false;
  }
  
  const handleButtonClick = (e: React.MouseEvent<HTMLButtonElement>) => {
    message.info('Click on left button.');
    console.log('click left button', e);
  };

  // todo: combine both functions below into one 
  const handleMenuClickWhite: MenuProps['onClick'] = (e) => {
    let agent = e.key.toString();
    setSelectedAgentWhite(agent);
    if(agent != 'human' && game.turn() === 'w')
      makeRandomMove();
  };
  const handleMenuClickBlack: MenuProps['onClick'] = (e) => {
    let agent = e.key.toString();
    setSelectedAgentBlack(agent);
    if(agent != 'human' && game.turn() === 'b')
      makeRandomMove();
  };

  // todo: build this menu dynamically from the api response
  const items: MenuProps['items'] = [
    {
      label: 'Human',
      key: 'human',
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

  const menuPropsWhite = {
    items,
    onClick: handleMenuClickWhite,
  };
  const menuPropsBlack = {
    items,
    onClick: handleMenuClickBlack,
  };

  // todo: list all agents
  // todo: allow user to select agent as opponent
  // todo: allow user to select the color
  // todo: when the user make a move, send the move to the server and request a move from the opponent agent
  // todo: render the move from the opponent agent locally and wait
  
  return (
    <Flex gap="middle" vertical={false}>
      <div key="1">
        <p>Select the agent</p>
        <Dropdown.Button type='default' menu={menuPropsWhite} placement="topLeft" arrow style={{ borderColor: "black", color: "black" }}>
          {selectedAgentWhite ? selectedAgentWhite : 'White'}
        </Dropdown.Button>
        <Dropdown.Button type='default' menu={menuPropsBlack} placement="topLeft" arrow style={{ borderColor: "red", color: "red" } } >
          {selectedAgentBlack ? selectedAgentBlack : 'Black'}
        </Dropdown.Button>
      </div>
      <div key="2">
        <Chessboard id="BasicBoard" boardWidth={Math.min(window.innerWidth, window.innerHeight)*0.8} position={fen} onPieceDrop={onDrop} />
      </div>
    </Flex>
  )
}

export default PlayGame;