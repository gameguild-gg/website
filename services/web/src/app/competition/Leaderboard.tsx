import React, { useEffect } from "react";
import { message } from "antd";

const Leaderboard: React.FC = () => {
  const [leaderboardData, setLeaderboardData] = React.useState([]);
  
  const getLeaderboardData = async () => {
    // todo: use env var properly
    // todo: process.env.REACT_APP_API_URL is undefined here and I dont know how to fix it
    const baseUrl = process.env.NEXT_PUBLIC_API_URL;
    const response = await fetch(baseUrl + "/Competitions/Chess/Leaderboard");
    let data = await response.json();
    console.log(data);
    setLeaderboardData(data);
  }
  
  useEffect(() => {
    if(leaderboardData.length === 0)
      getLeaderboardData();
  });
  
  return (
    <div>
      <h1>Leaderboard</h1>
      <table>
        <thead>
          <tr>
            <th>Rank</th>
            <th>Username</th>
            <th>ELO</th>
          </tr>
        </thead>
        <tbody>
          {leaderboardData.map((entry: any, index: number) => {
            return (
              <tr key={index}>
                <td>{index + 1}</td>
                <td>{entry.username}</td>
                <td>{entry.elo}</td>
              </tr>
            );
          })}
        </tbody>
      </table>
    </div>
  )
}

export default Leaderboard