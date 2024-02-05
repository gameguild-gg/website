import React, { useEffect } from "react";
import { Chessboard } from 'react-chessboard';
import { Chess } from 'chess.js';
import { Col, Row } from "antd";

// LEST MATCHES
// ALLOW USER TO FILTER MATCHES BY DATE, BY PLAYER, BY TOURNAMENT
// IF THE USER CLICKS ON A MATCH, IT WILL REDIRECT TO THE REPLAY PAGE WITH THE CORRECT PARAMETERS TO RENDER
const MatchesListUI: React.FC = () => {
  // todo: properly type the matchesData using the dto from the backend
  const [matchesData, setMatchesData] = React.useState([]);
  
  const getMatchesData = async () => {
    const baseUrl = process.env.NEXT_PUBLIC_API_URL;
    // todo: use the dto from the backend
    const response = await fetch(baseUrl + "/Competitions/Chess/FindMatches", {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json'
      },
      body: JSON.stringify({
        pageSize: 100,
        pageId: 0
      })
    });
    let data = await response.json();
    console.log(data);
    setMatchesData(data);
  }
  
  useEffect(() => {
    if(matchesData.length === 0)
      getMatchesData();
  });
  
  return (
    <Row>
      <Col span={24}>
        <h1>Matches</h1>
        <table>
          <thead>
            <tr>
              <th>Match ID</th>
              <th>Winner</th>
              <th>Whites</th>
              <th>Blacks</th>
              <th>Last State</th>
            </tr>
          </thead>
          <tbody>
            {matchesData.map((entry: any, index: number) => {
              return (
                <tr key={index}>
                  <td>{entry.id}</td>
                  <td>{
                    entry.winner == null ? "DRAW" : entry.winner === "Player1" ? entry.players[0] : entry.players[1]
                  }</td>
                  <td>{entry.players[0]}</td>
                  <td>{entry.players[1]}</td>
                  <td>{entry.lastState}</td>
                </tr>
              );
            })}
          </tbody>
        </table>
      </Col>
    </Row>
  );
};

export default MatchesListUI;