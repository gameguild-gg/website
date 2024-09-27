'use client';

import React, { useEffect } from 'react';
import { useRouter } from 'next/navigation';
import { message, Table, TableColumnsType, Typography } from 'antd';
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
    console.log('before request');

    const response = await api.competitionControllerGetChessLeaderboard({
      headers: {
        Authorization: `Bearer ${session?.accessToken}`,
      },
    });

    if (response.status === 401) {
      message.error('You are not authorized to view this page.');
      setTimeout(() => {
        router.push('/connect');
      }, 1000);
      setLeaderboardFetched(true);
      return;
    }

    if (response.status === 500) {
      message.error(
        'Internal server error. Please report this issue to the community.',
      );
      message.error(JSON.stringify(response.body));
      setLeaderboardFetched(true);
      return;
    }

    setLeaderboardData(response.body as ChessLeaderboardResponseEntryDto[]);
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

  if (leaderboardFetched) {
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
  } else return <></>;
}
