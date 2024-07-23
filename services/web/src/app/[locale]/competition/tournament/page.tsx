'use client';

import React from 'react';
import { getCookie } from 'cookies-next';
import { useRouter } from 'next/navigation';
import { Button, message, Table, TableColumnsType, Typography } from 'antd';
import { RedoOutlined } from '@ant-design/icons';

import { Moment } from 'moment';
import moment from 'moment-timezone';
import { CompetitionRunSubmissionReportDto } from '@game-guild/apiclient';

export default function TournamentPage() {
  const router = useRouter();
  const [lastCompetitionState, setLastCompetitionState] = React.useState<
    CompetitionRunSubmissionReportDto[] | null
  >(null);

  const [dataFetched, setDataFetched] = React.useState(false);

  const fetchData = async () => {
    const baseUrl = process.env.NEXT_PUBLIC_API_URL;
    const headers = new Headers();
    const accessToken = getCookie('access_token');
    headers.append('Authorization', `Bearer ${accessToken}`);
    const response = await fetch(
      baseUrl + '/Competitions/Chess/LatestCompetitionReport',
      {
        headers: headers,
      },
    );
    if (!response.ok) {
      router.push('/login');
      return;
    }
    const data = (await response.json()) as CompetitionRunSubmissionReportDto[];
    setLastCompetitionState(data);
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
    const baseUrl = process.env.NEXT_PUBLIC_API_URL;
    const headers = new Headers();
    const accessToken = getCookie('access_token');
    headers.append('Authorization', `Bearer ${accessToken}`);
    const response = await fetch(
      baseUrl + '/Competitions/Chess/RunCompetition',
      {
        headers: headers,
      },
    );
    if (response.status === 409) {
      message.error(response.text());
    } else if (!response.ok) {
      message.error;
      router.push('/login');
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
