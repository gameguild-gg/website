'use client';

import React, { useEffect } from 'react';
import { Button, Table, TableColumnsType, Typography } from 'antd';

import { getCookie } from 'cookies-next';
import { useRouter } from 'next/navigation';
import {
  competitionControllerFindChessMatchResult,
  MatchSearchRequestDto,
  MatchSearchResponseDto,
} from '@game-guild/apiclient';
import { createClient } from '@hey-api/client-axios';
import { getSession } from 'next-auth/react';

export default function MatchesPage() {
  const router = useRouter();

  interface DataType {
    key: React.Key;
    matchId: string;
    winner: string;
    white: string;
    black: string;
  }

  const columns: TableColumnsType<DataType> = [
    {
      title: 'Match ID',
      dataIndex: 'matchId',
      render: (text) => (
        <Button
          type="link"
          onClick={() => {
            router.push('/competition/replay?matchId=' + text);
          }}
        >
          {text}
        </Button>
      ),
    },
    {
      title: 'Winner',
      dataIndex: 'winner',
      render: (text) => (
        <Typography.Text
          strong={true}
          style={{
            color: text === 'DRAW' ? 'gray' : 'black',
          }}
        >
          {text}
        </Typography.Text>
      ),
    },
    {
      title: 'White',
      dataIndex: 'white',
    },
    {
      title: 'Black',
      dataIndex: 'black',
    },
  ];

  const [matchesTable, setMatchesTable] = React.useState<DataType[]>([]);

  const [matchesData, setMatchesData] = React.useState<
    MatchSearchResponseDto[]
  >([]);

  const [matchesFetched, setMatchesFetched] = React.useState<boolean>(false);

  const getMatchesData = async (): Promise<void> => {
    const session = await getSession();

    const client = createClient({
      baseURL: process.env.NEXT_PUBLIC_API_URL,
      throwOnError: false,
      headers: {
        Authorization: `Bearer ${session?.accessToken}`,
      },
    });

    const response = await competitionControllerFindChessMatchResult({
      client: client,
      body: {
        pageSize: 100,
        pageId: 0,
      } as MatchSearchRequestDto,
      throwOnError: false,
    });

    if (response.error) {
      router.push('/connect');
      return;
    }
    const data = response.data as MatchSearchResponseDto[];
    console.log(data);
    setMatchesData(data);
    setMatchesFetched(true);

    const tableData: DataType[] = [];
    for (let i = 0; i < data.length; i++) {
      const match = data[i];
      tableData.push({
        key: i,
        matchId: match.id,
        winner:
          match.winner == null
            ? 'DRAW'
            : match.winner === 'Player1'
              ? match.players[0]
              : match.players[1],
        white: match.players[0],
        black: match.players[1],
      });
    }
    setMatchesTable(tableData);
  };

  useEffect(() => {
    if (!matchesFetched) getMatchesData();
  });

  return <Table columns={columns} dataSource={matchesTable} />;
}
