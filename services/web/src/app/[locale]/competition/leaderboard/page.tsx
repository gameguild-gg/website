'use client';

import React, { useEffect } from 'react';
import { useRouter } from 'next/navigation';
import { Table, TableColumnsType, Typography } from 'antd';
import { getSession } from 'next-auth/react';

import { Api, CompetitionsApi } from '@game-guild/apiclient';
import ChessLeaderboardResponseEntryDto = Api.ChessLeaderboardResponseEntryDto;

export default function LeaderboardPage(): JSX.Element {
  const router = useRouter();
  const api = new CompetitionsApi({
    basePath: process.env.NEXT_PUBLIC_API_URL,
  });

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

    try {
      const response = await api.competitionControllerGetChessLeaderboard({
        headers: {
          Authorization: `Bearer ${session?.accessToken}`,
        },
      });

      if (response) {
        router.push('/connect');
        return;
      }

      const resp = response;
      console.log(data);
      setLeaderboardData(resp);
      setLeaderboardFetched(true);
    } catch (e) {
      router.push('/connect');
      return;
    }
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
