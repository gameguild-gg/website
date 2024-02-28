'use client';

import React, { useEffect } from 'react';
import { Button, Col, message, Row } from 'antd';
import { MatchSearchRequestDto } from '@/dtos/competition/match-search-request.dto';
import { MatchSearchResponseDto } from '@/dtos/competition/match-search-response.dto';
import { getCookie } from 'cookies-next';
import { useRouter } from 'next/navigation';

export default function MatchesPage() {
  const [matchesData, setMatchesData] = React.useState<
    MatchSearchResponseDto[]
  >([]);

  const router = useRouter();
  const [matchesFetched, setMatchesFetched] = React.useState<boolean>(false);

  const getMatchesData = async () => {
    const baseUrl = process.env.NEXT_PUBLIC_API_URL;
    const headers = new Headers();
    const accessToken = getCookie('access_token');
    headers.append('Authorization', `Bearer ${accessToken}`);
    headers.append('Content-Type', 'application/json');
    const response = await fetch(baseUrl + '/Competitions/Chess/FindMatches', {
      method: 'POST',
      headers: headers,
      body: JSON.stringify({
        pageSize: 100,
        pageId: 0,
      } as MatchSearchRequestDto),
    });
    if (!response.ok) {
      router.push('/login');
      return;
    }
    const data = (await response.json()) as MatchSearchResponseDto[];
    console.log(data);
    setMatchesData(data);
    setMatchesFetched(true);
  };

  useEffect(() => {
    if (!matchesFetched) getMatchesData();
  });

  return (
    <Row>
      <Col span={24}>
        <h1>Matches</h1>
        <p>todo: link this ids to the replay feature</p>
        <table>
          <thead>
            <tr>
              <th>Match ID</th>
              <th>Winner</th>
              <th>Whites</th>
              <th>Blacks</th>
              <th>Last State</th>
            </tr>
          </thead>
          <tbody>
            {matchesData.map((entry: MatchSearchResponseDto, index: number) => {
              return (
                <tr key={index}>
                  <td>
                    <Button
                      onClick={() => {
                        router.push('/competition/replay?matchId=' + entry.id);
                      }}
                    >
                      {entry.id}
                    </Button>
                  </td>
                  <td>
                    {entry.winner == null
                      ? 'DRAW'
                      : entry.winner === 'Player1'
                        ? entry.players[0]
                        : entry.players[1]}
                  </td>
                  <td>{entry.players[0]}</td>
                  <td>{entry.players[1]}</td>
                  <td>{entry.lastState}</td>
                </tr>
              );
            })}
          </tbody>
        </table>
      </Col>
    </Row>
  );
}
