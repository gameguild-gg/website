import React, { useEffect, useState } from 'react';
import { message } from 'antd';
import { ChessLeaderboardResponseEntryDto } from '@/dtos/competition/chess-leaderboard-response.dto';
import { getCookie } from 'cookies-next';
import { useRouter } from "next/navigation";

const Leaderboard: React.FC = () => {
  const router = useRouter();
  const [leaderboardData, setLeaderboardData] = React.useState<
    ChessLeaderboardResponseEntryDto[]
  >([]);
  // flag for leaderboard fetched
  const [leaderboardFetched, setLeaderboardFetched] =
    React.useState<boolean>(false);

  useEffect(() => {
    if (!leaderboardFetched) getLeaderboardData();
  }, []);

  const getLeaderboardData = async () => {
    // todo: use env var properly
    // todo: process.env.REACT_APP_API_URL is undefined here and I dont know how to fix it
    const baseUrl = process.env.NEXT_PUBLIC_API_URL;
    const headers = new Headers();
    const accessToken = getCookie('access_token');
    headers.append('Authorization', `Bearer ${accessToken}`);
    const response = await fetch(baseUrl + '/Competitions/Chess/Leaderboard', {
      headers: headers,
    });
    if (!response.ok) {
      router.push('/login');
      return;
    }
    const data = (await response.json()) as ChessLeaderboardResponseEntryDto[];
    console.log(data);
    setLeaderboardData(data);
    setLeaderboardFetched(true);
  };

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
  );
};

export default Leaderboard;
