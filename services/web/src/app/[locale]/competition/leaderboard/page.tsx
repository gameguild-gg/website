'use client';

import React, {useEffect} from 'react';
import {getCookie} from 'cookies-next';
import {useRouter} from 'next/navigation';
import {Table, TableColumnsType, Typography} from 'antd';
import {ChessLeaderboardResponseEntryDto} from '@game-guild/apiclient';

export default function LeaderboardPage() {
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

  // table for the leaderboard

  interface DataType {
    key: React.Key;
    rank: number;
    username: string;
    elo: string;
  }

  const columns: TableColumnsType<DataType> = [
    {
      title: 'Rank',
      dataIndex: 'rank',
      key: 'rank',
    },
    {
      title: 'Username',
      dataIndex: 'username',
      key: 'username',
    },
    {
      title: 'ELO',
      dataIndex: 'elo',
      key: 'elo',
    },
  ];

  const data: DataType[] = leaderboardData.map(
    (entry: ChessLeaderboardResponseEntryDto, index: number) => {
      return {
        key: index,
        rank: index + 1,
        username: entry.username,
        elo: entry.elo.toFixed(2),
      };
    },
  );

  return (
    <>
      <Typography.Title>Leaderboard</Typography.Title>
      <Table columns={columns} dataSource={data}/>
    </>
  );
}
