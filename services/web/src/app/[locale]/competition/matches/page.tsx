'use client';

import React, { useEffect } from 'react';
import { Button, Table, TableColumnsType, Typography } from 'antd';

import { useRouter } from 'next/navigation';
import { getSession } from 'next-auth/react';
import { Api, CompetitionsApi } from '@game-guild/apiclient';
import MatchSearchResponseDto = Api.MatchSearchResponseDto;
import MatchSearchRequestDto = Api.MatchSearchRequestDto;

export default function MatchesPage() {
  const router = useRouter();
  const api = new CompetitionsApi({
    basePath: process.env.NEXT_PUBLIC_API_URL,
  });

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
    try {
      const session = await getSession();

      const response = await api.competitionControllerFindChessMatchResult(
        {
          pageSize: 100,
          pageId: 0,
        } as MatchSearchRequestDto,
        {
          headers: {
            Authorization: `Bearer ${session?.accessToken}`,
          },
        },
      );

      const data = response as MatchSearchResponseDto[];
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
    } catch (e) {
      router.push('/connect');
      return;
    }
  };

  useEffect(() => {
    if (!matchesFetched) getMatchesData();
  });

  return <Table columns={columns} dataSource={matchesTable} />;
}
