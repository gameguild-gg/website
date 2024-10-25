'use client';

import React from 'react';
import { getCookie } from 'cookies-next';
import { useRouter } from 'next/navigation';
import { Button, message, Table, TableColumnsType, Typography } from 'antd';
import { RedoOutlined } from '@ant-design/icons';
import moment from 'moment-timezone';
import { Api, CompetitionsApi } from '@game-guild/apiclient';
import CompetitionRunSubmissionReportEntity = Api.CompetitionRunSubmissionReportEntity;
import { getSession } from 'next-auth/react';

export default function TournamentPage() {
  const api = new CompetitionsApi({
    basePath: process.env.NEXT_PUBLIC_API_URL,
  });

  const router = useRouter();
  const [lastCompetitionState, setLastCompetitionState] = React.useState<
    CompetitionRunSubmissionReportEntity[] | null
  >(null);

  const [dataFetched, setDataFetched] = React.useState(false);

  const fetchData = async () => {
    try {
      const session = await getSession();
      const response =
        await api.competitionControllerGetLatestChessCompetitionReport({
          headers: {
            Authorization: `Bearer ${session?.user?.accessToken}`,
          },
        });

      if (response.status === 401) {
        message.error('You are not authorized to view this page.');
        setTimeout(() => {
          router.push('/disconnect');
        }, 1000);
        return;
      }

      const data = response.body as CompetitionRunSubmissionReportEntity[];
      setLastCompetitionState(data);
    } catch (e) {
      message.error(JSON.stringify(e));
      router.push('/connect');
      return;
    }
  };

  React.useEffect(() => {
    if (!dataFetched) {
      setDataFetched(true);
      setLastCompetitionState([]);
      fetchData();
    }
  }, []);

  interface DataType {
    key: React.Key;
    totalWins: number;
    username: string;
  }

  const columns: TableColumnsType<DataType> = [
    {
      title: 'Username',
      dataIndex: 'username',
      key: 'username',
    },
    {
      title: 'Total Wins',
      dataIndex: 'totalWins',
      key: 'totalWins',
    },
  ];

  const data: DataType[] = [];
  if (lastCompetitionState) {
    for (let i = 0; i < lastCompetitionState.length; i++) {
      const report = lastCompetitionState[i];
      data.push({
        key: i,
        totalWins: report.totalWins,
        username: report.user.username,
      });
    }
  }

  async function triggerTournament() {
    const session = await getSession();

    const response = await api.competitionControllerRunCompetition({
      headers: {
        Authorization: `Bearer ${session?.user?.accessToken}`,
      },
    });
    if (response.status === 401) {
      message.error('You are not authorized to view this page.');
      setTimeout(() => {
        router.push('/disconnect');
      }, 1000);
      return;
    } else if (response.status >= 400) {
      message.error('Error. Please report this issue to the community.');
      message.error(JSON.stringify(response.body));
      return;
    }
  }

  let lastRunDate: Date | null = null;
  if (lastCompetitionState) {
    // get the oldest updatedAt from the lastCompetitionState array
    for (let i = 0; i < lastCompetitionState.length; i++) {
      if (
        lastRunDate === null ||
        new Date(lastCompetitionState[i].updatedAt) > lastRunDate
      )
        lastRunDate = new Date(lastCompetitionState[i].updatedAt);
    }
  }

  let lastRunDateString: string = 'None';
  if (lastRunDate) {
    lastRunDateString =
      moment(lastRunDate).tz('America/New_York').format('YYYY-MM-DD HH:mm:ss') +
      ' EST';
  }

  // print a table of all reports from all users from lastCompetitionState
  return (
    <>
      <Typography.Title level={1}>Tournament</Typography.Title>
      <Typography.Title level={2}>Last Competition State</Typography.Title>
      <Typography.Paragraph>
        Last Run: {lastRunDateString}. Please don't run the tournament too
        often. Once each a day is fine.
      </Typography.Paragraph>
      <Button
        icon={<RedoOutlined />}
        type="primary"
        block
        danger
        size="large"
        onClick={triggerTournament}
      >
        Trigger a tournament
      </Button>
      <Table columns={columns} dataSource={data} />
    </>
  );
}
