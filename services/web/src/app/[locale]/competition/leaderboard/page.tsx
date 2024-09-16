'use client';

import React, { useEffect } from 'react';
import { useRouter } from 'next/navigation';
import { Table, TableColumnsType, Typography } from 'antd';
import {
  ChessLeaderboardResponseEntryDto,
  competitionControllerGetChessLeaderboard,
} from '@game-guild/apiclient';
import { getSession } from 'next-auth/react';
import { createClient } from '@hey-api/client-axios';

export default function LeaderboardPage(): JSX.Element {
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

  const getLeaderboardData = async (): Promise<void> => {
    const session = await getSession();
    const client = createClient({
      baseURL: process.env.NEXT_PUBLIC_API_URL,
      throwOnError: false,
      headers: {
        Authorization: `Bearer ${session?.accessToken}`,
      },
    });

    const response = await competitionControllerGetChessLeaderboard({
      client: client,
      throwOnError: false,
    });

    if (response.error) {
      router.push('/connect');
      return;
    }

    const resp = response.data as ChessLeaderboardResponseEntryDto[];
    console.log(data);
    setLeaderboardData(resp);
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
      <Table columns={columns} dataSource={data} />
    </>
  );
}
